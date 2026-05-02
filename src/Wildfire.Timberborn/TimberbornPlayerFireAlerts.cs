using Timberborn.QuickNotificationSystem;

namespace Wildfire.Timberborn;

public sealed class TimberbornPlayerFireAlertSink : ITimberbornFireAlertDispatchSink
{
    private readonly ITimberbornPlayerNotificationSink _notificationSink;
    private readonly ITimberbornFireLogSink _logSink;
    private int _currentFireStartedCount;
    private int _currentFuelSpentCount;
    private int _currentMaxHeat;

    public TimberbornPlayerFireAlertSink(
        ITimberbornPlayerNotificationSink notificationSink,
        ITimberbornFireLogSink logSink)
    {
        _notificationSink = notificationSink ?? throw new ArgumentNullException(nameof(notificationSink));
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        Counters = TimberbornPlayerFireAlertCounters.Empty;
    }

    public TimberbornPlayerFireAlertCounters Counters { get; private set; }

    public void BeginAlertDispatch(uint tick)
    {
        _currentFireStartedCount = 0;
        _currentFuelSpentCount = 0;
        _currentMaxHeat = 0;
    }

    public void PublishAlert(TimberbornFireAlertEvent alertEvent)
    {
        if (alertEvent.Kind == TimberbornFireAlertKind.FireStarted)
        {
            _currentFireStartedCount++;
        }

        if (alertEvent.Kind == TimberbornFireAlertKind.FuelSpent)
        {
            _currentFuelSpentCount++;
        }

        _currentMaxHeat = Math.Max(_currentMaxHeat, alertEvent.Heat);
    }

    public void CompleteAlertDispatch(uint tick)
    {
        int alertCount = _currentFireStartedCount + _currentFuelSpentCount;
        if (alertCount == 0)
        {
            return;
        }

        string message = FormatMessage(_currentFireStartedCount, _currentFuelSpentCount, _currentMaxHeat);
        bool notificationSent = false;
        int failureCount = Counters.PresentationFailureCount;

        try
        {
            _notificationSink.SendWarning(message);
            notificationSent = true;
        }
        catch (Exception exception)
        {
            failureCount++;
            _logSink.Warning(
                "wildfire_timberborn_player_fire_alert_failed " +
                $"tick={tick} " +
                $"message=\"{EscapeLogValue(exception.Message)}\"");
        }

        Counters = new TimberbornPlayerFireAlertCounters(
            LastAlertTick: tick,
            LastFireStartedCount: _currentFireStartedCount,
            LastFuelSpentCount: _currentFuelSpentCount,
            LastMaxHeat: _currentMaxHeat,
            TotalNotificationCount: Counters.TotalNotificationCount + (notificationSent ? 1 : 0),
            PresentationFailureCount: failureCount,
            LastNotificationSent: notificationSent,
            LastMessage: message);
        _logSink.Info(
            "wildfire_timberborn_player_fire_alert_updated " +
            $"tick={tick} " +
            $"fire_started={_currentFireStartedCount} " +
            $"fuel_spent={_currentFuelSpentCount} " +
            $"max_heat={_currentMaxHeat} " +
            $"notification_sent={notificationSent.ToString().ToLowerInvariant()} " +
            $"total_notifications={Counters.TotalNotificationCount} " +
            $"presentation_failures={Counters.PresentationFailureCount} " +
            $"message=\"{EscapeLogValue(message)}\"");
    }

    public void Clear()
    {
        _currentFireStartedCount = 0;
        _currentFuelSpentCount = 0;
        _currentMaxHeat = 0;
        Counters = TimberbornPlayerFireAlertCounters.Empty;
    }

    private static string FormatMessage(int fireStartedCount, int fuelSpentCount, int maxHeat)
    {
        string[] parts =
        {
            FormatCount(fireStartedCount, "new fire", "new fires"),
            FormatCount(fuelSpentCount, "burned-out cell", "burned-out cells"),
        };
        string summary = string.Join(", ", parts.Where(static part => part.Length > 0));

        return $"Wildfire alert: {summary}. Max heat {maxHeat}.";
    }

    private static string FormatCount(int count, string singular, string plural)
    {
        return count == 0
            ? string.Empty
            : $"{count} {(count == 1 ? singular : plural)}";
    }

    private static string EscapeLogValue(string value)
    {
        return value.Replace('\\', '/').Replace('"', '\'');
    }
}

public sealed record TimberbornPlayerFireAlertCounters(
    uint LastAlertTick,
    int LastFireStartedCount,
    int LastFuelSpentCount,
    int LastMaxHeat,
    int TotalNotificationCount,
    int PresentationFailureCount,
    bool LastNotificationSent,
    string LastMessage)
{
    public static readonly TimberbornPlayerFireAlertCounters Empty = new(
        LastAlertTick: 0,
        LastFireStartedCount: 0,
        LastFuelSpentCount: 0,
        LastMaxHeat: 0,
        TotalNotificationCount: 0,
        PresentationFailureCount: 0,
        LastNotificationSent: false,
        LastMessage: string.Empty);
}

public interface ITimberbornPlayerNotificationSink
{
    void SendWarning(string message);
}

public sealed class TimberbornQuickNotificationSink : ITimberbornPlayerNotificationSink
{
    private readonly QuickNotificationService _quickNotificationService;

    public TimberbornQuickNotificationSink(QuickNotificationService quickNotificationService)
    {
        _quickNotificationService =
            quickNotificationService ?? throw new ArgumentNullException(nameof(quickNotificationService));
    }

    public void SendWarning(string message)
    {
        _quickNotificationService.SendWarningNotification(message);
    }
}
