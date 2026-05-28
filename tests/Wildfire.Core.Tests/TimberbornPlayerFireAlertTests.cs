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
        Assert.Equal([3], notificationSink.FocusCellIndices);
        Assert.Equal(12u, sink.Counters.LastAlertTick);
        Assert.Equal(2, sink.Counters.LastFireStartedCount);
        Assert.Equal(1, sink.Counters.LastFuelSpentCount);
        Assert.Equal(15, sink.Counters.LastMaxHeat);
        Assert.Equal(1, sink.Counters.TotalNotificationCount);
        Assert.Equal(3, sink.Counters.LastFocusCellIndex);
        Assert.Equal(0, sink.Counters.PresentationFailureCount);
        Assert.True(sink.Counters.LastNotificationSent);
        Assert.Equal(3, sink.Counters.TotalSourceEventCount);
        Assert.Equal(2, sink.Counters.TotalCoalescedEventCount);
        Assert.Equal(3, sink.Counters.ActiveFireEventCount);
        Assert.Equal(1, sink.Counters.ActiveFireNotificationCount);
        Assert.Contains("wildfire_timberborn_world_consequence_feedback_updated", logSink.InfoMessages.Single());
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
        Assert.Equal([3], notificationSink.FocusCellIndices);
        Assert.Equal(12u, sink.Counters.LastAlertTick);
        Assert.Equal(1, sink.Counters.TotalNotificationCount);
        Assert.Equal("Wildfire alert: 1 new fire. Max heat 11.", sink.Counters.LastMessage);
        Assert.Equal(3, sink.Counters.LastFocusCellIndex);
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
        Assert.Equal(1, sink.Counters.LogOnlyFallbackCount);
        Assert.False(sink.Counters.LastNotificationSent);
        Assert.Contains(
            logSink.WarningMessages,
            message => message.Contains("wildfire_timberborn_world_consequence_feedback_failed tick=12"));
    }

    [Fact]
    public void AggregatesWorldConsequenceClassesIntoOneNotification()
    {
        RecordingPlayerNotificationSink notificationSink = new();
        RecordingFireLogSink logSink = new();
        TimberbornPlayerFireAlertSink sink = new(notificationSink, logSink);

        sink.BeginAlertDispatch(21);
        sink.PublishAlert(new TimberbornFireAlertEvent(5, 21, TimberbornFireAlertKind.FireStarted, Heat: 9));
        sink.PublishConsequences(new TimberbornWorldConsequenceFeedbackInput(
            21,
            [
                Event(TimberbornWorldConsequenceFeedbackClass.BuildingDamageClosure, 3, 2, 6),
                Event(TimberbornWorldConsequenceFeedbackClass.PlantCropResourceLoss, 8, 8, 7),
                Event(TimberbornWorldConsequenceFeedbackClass.AshAftermath, 5, 5, 8),
            ]));
        sink.CompleteAlertDispatch(21);

        string message = Assert.Single(notificationSink.WarningMessages);
        Assert.Contains("Wildfire consequence: building damage/closure", message);
        Assert.Contains("active fire: 1", message);
        Assert.Contains("building damage/closure: 3", message);
        Assert.Contains("plant/crop/resource loss: 8", message);
        Assert.Contains("ash aftermath: 5", message);
        Assert.Equal([6], notificationSink.FocusCellIndices);
        Assert.Equal(17, sink.Counters.TotalSourceEventCount);
        Assert.Equal(16, sink.Counters.TotalCoalescedEventCount);
        Assert.Equal(1, sink.Counters.ActiveFireEventCount);
        Assert.Equal(3, sink.Counters.BuildingDamageClosureEventCount);
        Assert.Equal(8, sink.Counters.PlantCropResourceLossEventCount);
        Assert.Equal(5, sink.Counters.AshAftermathEventCount);
        Assert.Equal(1, sink.Counters.TotalNotificationCount);
        Assert.Equal(1, sink.Counters.BuildingDamageClosureNotificationCount);
        Assert.Equal(TimberbornWorldConsequenceFeedbackClass.BuildingDamageClosure, sink.Counters.LastPrimaryClass);
    }

    [Fact]
    public void StructureOnFireFeedbackUsesDistinctAlertClassAndCounters()
    {
        RecordingPlayerNotificationSink notificationSink = new();
        RecordingFireLogSink logSink = new();
        TimberbornPlayerFireAlertSink sink = new(notificationSink, logSink);

        sink.BeginAlertDispatch(24);
        sink.PublishConsequences(new TimberbornWorldConsequenceFeedbackInput(
            24,
            [Event(TimberbornWorldConsequenceFeedbackClass.StructureOnFire, 4, 2, 9, "2 structures on fire")]));
        sink.CompleteAlertDispatch(24);

        Assert.Equal(["Wildfire alert: 2 structures on fire."], notificationSink.WarningMessages);
        Assert.Equal([9], notificationSink.FocusCellIndices);
        Assert.Equal(4, sink.Counters.StructureOnFireEventCount);
        Assert.Equal(2, sink.Counters.StructureOnFireCoalescedEventCount);
        Assert.Equal(1, sink.Counters.StructureOnFireNotificationCount);
        Assert.Equal(0, sink.Counters.StructureOnFireNotificationSuppressedThrottleCount);
        Assert.Equal(0, sink.Counters.StructureOnFirePresentationFailureCount);
        Assert.Equal(TimberbornWorldConsequenceFeedbackClass.StructureOnFire, sink.Counters.LastPrimaryClass);
    }

    [Fact]
    public void ThrottlesRepeatedConsequenceNotificationsWithoutDroppingCounters()
    {
        RecordingPlayerNotificationSink notificationSink = new();
        RecordingFireLogSink logSink = new();
        TimberbornPlayerFireAlertSink sink = new(notificationSink, logSink, notificationThrottleTicks: 5);

        sink.BeginAlertDispatch(30);
        sink.PublishConsequences(new TimberbornWorldConsequenceFeedbackInput(
            30,
            [Event(TimberbornWorldConsequenceFeedbackClass.PlantCropResourceLoss, 3, 3, 4)]));
        sink.CompleteAlertDispatch(30);
        sink.BeginAlertDispatch(32);
        sink.PublishConsequences(new TimberbornWorldConsequenceFeedbackInput(
            32,
            [Event(TimberbornWorldConsequenceFeedbackClass.AshAftermath, 2, 2, 5)]));
        sink.CompleteAlertDispatch(32);

        Assert.Single(notificationSink.WarningMessages);
        Assert.Equal(5, sink.Counters.TotalSourceEventCount);
        Assert.Equal(1, sink.Counters.TotalNotificationCount);
        Assert.Equal(1, sink.Counters.NotificationSuppressedThrottleCount);
        Assert.Equal(3, sink.Counters.PlantCropResourceLossEventCount);
        Assert.Equal(2, sink.Counters.AshAftermathEventCount);
        Assert.False(sink.Counters.LastNotificationSent);
        Assert.True(sink.Counters.LastNotificationSuppressed);
        Assert.Contains(
            logSink.InfoMessages,
            message => message.Contains("wildfire_timberborn_world_consequence_feedback_suppressed tick=32"));
    }

    [Fact]
    public void ThrottlesRepeatedStructureOnFireNotificationsWithoutDroppingCounters()
    {
        RecordingPlayerNotificationSink notificationSink = new();
        RecordingFireLogSink logSink = new();
        TimberbornPlayerFireAlertSink sink = new(notificationSink, logSink, notificationThrottleTicks: 5);

        sink.BeginAlertDispatch(30);
        sink.PublishConsequences(new TimberbornWorldConsequenceFeedbackInput(
            30,
            [Event(TimberbornWorldConsequenceFeedbackClass.StructureOnFire, 3, 1, 4)]));
        sink.CompleteAlertDispatch(30);
        sink.BeginAlertDispatch(32);
        sink.PublishConsequences(new TimberbornWorldConsequenceFeedbackInput(
            32,
            [Event(TimberbornWorldConsequenceFeedbackClass.StructureOnFire, 5, 2, 7)]));
        sink.CompleteAlertDispatch(32);

        Assert.Single(notificationSink.WarningMessages);
        Assert.Equal(8, sink.Counters.StructureOnFireEventCount);
        Assert.Equal(3, sink.Counters.StructureOnFireCoalescedEventCount);
        Assert.Equal(1, sink.Counters.StructureOnFireNotificationCount);
        Assert.Equal(1, sink.Counters.StructureOnFireNotificationSuppressedThrottleCount);
        Assert.False(sink.Counters.LastNotificationSent);
        Assert.True(sink.Counters.LastNotificationSuppressed);
    }

    [Fact]
    public void StructureOnFirePresentationFailuresAreIsolated()
    {
        ThrowingPlayerNotificationSink notificationSink = new();
        RecordingFireLogSink logSink = new();
        TimberbornPlayerFireAlertSink sink = new(notificationSink, logSink);

        sink.BeginAlertDispatch(35);
        sink.PublishConsequences(new TimberbornWorldConsequenceFeedbackInput(
            35,
            [Event(TimberbornWorldConsequenceFeedbackClass.StructureOnFire, 2, 1, 6)]));
        sink.CompleteAlertDispatch(35);

        Assert.Equal(0, sink.Counters.TotalNotificationCount);
        Assert.Equal(2, sink.Counters.StructureOnFireEventCount);
        Assert.Equal(1, sink.Counters.StructureOnFireCoalescedEventCount);
        Assert.Equal(1, sink.Counters.StructureOnFirePresentationFailureCount);
        Assert.Equal(1, sink.Counters.PresentationFailureCount);
        Assert.False(sink.Counters.LastNotificationSent);
    }

    [Fact]
    public void BeaverDangerFeedbackUsesHighestPriorityClass()
    {
        RecordingPlayerNotificationSink notificationSink = new();
        RecordingFireLogSink logSink = new();
        TimberbornPlayerFireAlertSink sink = new(notificationSink, logSink);

        sink.PublishBeaverBehavior(40, BeaverCounters(tracked: 2, coughingSlowdowns: 1, chokingSlowdowns: 1));

        string message = Assert.Single(notificationSink.WarningMessages);
        Assert.Contains("Wildfire consequence: beaver danger/death", message);
        Assert.Equal(2, sink.Counters.BeaverDangerDeathEventCount);
        Assert.Equal(1, sink.Counters.BeaverDangerDeathNotificationCount);
        Assert.Equal(TimberbornWorldConsequenceFeedbackClass.BeaverDangerDeath, sink.Counters.LastPrimaryClass);
    }

    private static TimberbornWorldConsequenceFeedbackEvent Event(
        TimberbornWorldConsequenceFeedbackClass eventClass,
        int sourceEvents,
        int affectedCells,
        int? focusCellIndex,
        string? detail = null)
    {
        return new TimberbornWorldConsequenceFeedbackEvent(
            eventClass,
            Tick: 0,
            SourceEventCount: sourceEvents,
            AffectedCellCount: affectedCells,
            FocusCellIndex: focusCellIndex,
            Detail: detail ?? eventClass.ToString());
    }

    private static TimberbornBeaverFieldBehaviorCounters BeaverCounters(
        int tracked,
        int coughingSlowdowns,
        int chokingSlowdowns)
    {
        return new TimberbornBeaverFieldBehaviorCounters(
            DispatcherEnabled: true,
            TrackedBeaverCount: tracked,
            DecisionsEvaluated: tracked,
            SmokeDecisionsApplied: 0,
            ToxicSmokeDecisionsApplied: 0,
            FireHeatDecisionsApplied: 0,
            NoOpDecisionsApplied: 0,
            DecisionsSkippedCooldown: 0,
            DecisionsSkippedBatch: 0,
            FailedDecisions: 0,
            RecoveryActions: 0,
            SmokeExposedSamples: 0,
            SmokeExposureAccumulatedSamples: 0,
            SmokeCoughingEntered: 0,
            SmokeCoughingRecovered: 0,
            SmokeCoughingSlowdownsApplied: coughingSlowdowns,
            SmokeCoughingSlowdownsRecovered: 0,
            SmokeRecoveryDecays: 0,
            SmokeChokingSlowdownsApplied: chokingSlowdowns,
            SmokeChokingSlowdownsRecovered: 0,
            ToxicSmokeExposedBeavers: 0,
            ToxicSmokeExposureAccumulatedSamples: 0,
            ToxicSmokeRecoveryDecays: 0,
            FireHeatExposedBeavers: 0,
            FireHeatActiveFlameContacts: 0,
            FireHeatRecoveryDecays: 0,
            PersistenceSaveCount: 0,
            PersistenceLoadCount: 0,
            LastDecisionTick: null);
    }

    private sealed class RecordingPlayerNotificationSink : ITimberbornPlayerNotificationSink
    {
        public List<string> WarningMessages { get; } = [];

        public List<int?> FocusCellIndices { get; } = [];

        public void SendWarning(TimberbornPlayerFireNotification notification)
        {
            WarningMessages.Add(notification.Message);
            FocusCellIndices.Add(notification.FocusCellIndex);
        }
    }

    private sealed class ThrowingPlayerNotificationSink : ITimberbornPlayerNotificationSink
    {
        public void SendWarning(TimberbornPlayerFireNotification notification)
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
