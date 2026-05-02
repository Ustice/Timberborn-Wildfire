using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornPlayerFireAlertTests
{
    [Fact]
    public void PublishesOneWarningPerDispatchWithAggregatedAlertCounts()
    {
        RecordingPlayerNotificationSink notificationSink = new();
        RecordingFireLogSink logSink = new();
        TimberbornPlayerFireAlertSink sink = new(notificationSink, logSink);

        sink.BeginAlertDispatch(12);
        sink.PublishAlert(new TimberbornFireAlertEvent(3, 12, TimberbornFireAlertKind.FireStarted, Heat: 11));
        sink.PublishAlert(new TimberbornFireAlertEvent(4, 12, TimberbornFireAlertKind.FireStarted, Heat: 15));
        sink.PublishAlert(new TimberbornFireAlertEvent(5, 12, TimberbornFireAlertKind.FuelSpent, Heat: 9));
        sink.CompleteAlertDispatch(12);

        Assert.Equal(["Wildfire alert: 2 new fires, 1 burned-out cell. Max heat 15."], notificationSink.WarningMessages);
        Assert.Equal(12u, sink.Counters.LastAlertTick);
        Assert.Equal(2, sink.Counters.LastFireStartedCount);
        Assert.Equal(1, sink.Counters.LastFuelSpentCount);
        Assert.Equal(15, sink.Counters.LastMaxHeat);
        Assert.Equal(1, sink.Counters.TotalNotificationCount);
        Assert.Equal(0, sink.Counters.PresentationFailureCount);
        Assert.True(sink.Counters.LastNotificationSent);
        Assert.Contains("wildfire_timberborn_player_fire_alert_updated", logSink.InfoMessages.Single());
    }

    [Fact]
    public void ZeroAlertDispatchDoesNotPublishOrClearLastVisibleAlertEvidence()
    {
        RecordingPlayerNotificationSink notificationSink = new();
        RecordingFireLogSink logSink = new();
        TimberbornPlayerFireAlertSink sink = new(notificationSink, logSink);

        sink.BeginAlertDispatch(12);
        sink.PublishAlert(new TimberbornFireAlertEvent(3, 12, TimberbornFireAlertKind.FireStarted, Heat: 11));
        sink.CompleteAlertDispatch(12);
        sink.BeginAlertDispatch(13);
        sink.CompleteAlertDispatch(13);

        Assert.Single(notificationSink.WarningMessages);
        Assert.Equal(12u, sink.Counters.LastAlertTick);
        Assert.Equal(1, sink.Counters.TotalNotificationCount);
        Assert.Equal("Wildfire alert: 1 new fire. Max heat 11.", sink.Counters.LastMessage);
        Assert.Single(logSink.InfoMessages);
    }

    [Fact]
    public void PresentationFailuresAreRecordedWithoutThrowing()
    {
        ThrowingPlayerNotificationSink notificationSink = new();
        RecordingFireLogSink logSink = new();
        TimberbornPlayerFireAlertSink sink = new(notificationSink, logSink);

        sink.BeginAlertDispatch(12);
        sink.PublishAlert(new TimberbornFireAlertEvent(3, 12, TimberbornFireAlertKind.FireStarted, Heat: 11));
        sink.CompleteAlertDispatch(12);

        Assert.Equal(0, sink.Counters.TotalNotificationCount);
        Assert.Equal(1, sink.Counters.PresentationFailureCount);
        Assert.False(sink.Counters.LastNotificationSent);
        Assert.Contains(
            logSink.WarningMessages,
            message => message.Contains("wildfire_timberborn_player_fire_alert_failed tick=12"));
    }

    private sealed class RecordingPlayerNotificationSink : ITimberbornPlayerNotificationSink
    {
        public List<string> WarningMessages { get; } = [];

        public void SendWarning(string message)
        {
            WarningMessages.Add(message);
        }
    }

    private sealed class ThrowingPlayerNotificationSink : ITimberbornPlayerNotificationSink
    {
        public void SendWarning(string message)
        {
            throw new InvalidOperationException("notification boom");
        }
    }

    private sealed class RecordingFireLogSink : ITimberbornFireLogSink
    {
        public List<string> InfoMessages { get; } = [];

        public List<string> WarningMessages { get; } = [];

        public void Info(string message)
        {
            InfoMessages.Add(message);
        }

        public void Warning(string message)
        {
            WarningMessages.Add(message);
        }
    }
}
