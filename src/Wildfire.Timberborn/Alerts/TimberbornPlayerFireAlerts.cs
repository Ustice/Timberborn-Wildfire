using Timberborn.QuickNotificationSystem;
using Timberborn.CameraSystem;
using Timberborn.Coordinates;
using Timberborn.SingletonSystem;
using Timberborn.UILayoutSystem;
using UnityEngine;
using UnityEngine.UIElements;
using Wildfire.Core;

namespace Wildfire.Timberborn.Alerts;

public sealed class TimberbornPlayerFireAlertSink : ITimberbornFireAlertDispatchSink
{
    private readonly ITimberbornPlayerNotificationSink _notificationSink;
    private readonly ITimberbornFireLogSink _logSink;
    private int _currentFireStartedCount;
    private int _currentFuelSpentCount;
    private int _currentMaxHeat;
    private int? _currentFocusCellIndex;

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
        _currentFocusCellIndex = null;
    }

    public void PublishAlert(TimberbornFireAlertEvent alertEvent)
    {
        if (alertEvent.Kind == TimberbornFireAlertKind.FireStarted)
        {
            _currentFireStartedCount++;
            _currentFocusCellIndex ??= alertEvent.CellIndex;
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
            _notificationSink.SendWarning(new TimberbornPlayerFireNotification(message, _currentFocusCellIndex));
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
            LastFocusCellIndex: _currentFocusCellIndex,
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
            $"focus_cell_index={FormatNullableNumber(_currentFocusCellIndex)} " +
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
        _currentFocusCellIndex = null;
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

    private static string FormatNullableNumber(int? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none";
    }
}

public sealed record TimberbornPlayerFireAlertCounters(
    uint LastAlertTick,
    int LastFireStartedCount,
    int LastFuelSpentCount,
    int LastMaxHeat,
    int? LastFocusCellIndex,
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
        LastFocusCellIndex: null,
        TotalNotificationCount: 0,
        PresentationFailureCount: 0,
        LastNotificationSent: false,
        LastMessage: string.Empty);
}

public readonly record struct TimberbornPlayerFireNotification(string Message, int? FocusCellIndex);

public interface ITimberbornPlayerNotificationSink
{
    void SendWarning(TimberbornPlayerFireNotification notification);
}

public sealed class TimberbornQuickNotificationSink : ITimberbornPlayerNotificationSink
{
    private readonly QuickNotificationService _quickNotificationService;
    private readonly ITimberbornPlayerFireAlertFocusSink _focusSink;

    public TimberbornQuickNotificationSink(
        QuickNotificationService quickNotificationService,
        ITimberbornPlayerFireAlertFocusSink focusSink)
    {
        _quickNotificationService =
            quickNotificationService ?? throw new ArgumentNullException(nameof(quickNotificationService));
        _focusSink = focusSink ?? throw new ArgumentNullException(nameof(focusSink));
    }

    public void SendWarning(TimberbornPlayerFireNotification notification)
    {
        _focusSink.SetLatestFocusCell(notification.FocusCellIndex);
        _quickNotificationService.SendWarningNotification(notification.Message);
    }
}

public interface ITimberbornPlayerFireAlertFocusSink
{
    void SetLatestFocusCell(int? cellIndex);
}

public sealed class TimberbornPlayerFireAlertCameraFocus :
    ILoadableSingleton,
    IUnloadableSingleton,
    IUpdatableSingleton,
    ITimberbornPlayerFireAlertFocusSink
{
    private const float ClickTargetWidth = 720f;
    private const float ClickTargetHeight = 96f;
    private const float ClickTargetTop = 20f;
    private const float ClickTargetDurationSeconds = 9f;

    private readonly UILayout _uiLayout;
    private readonly CameraService _cameraService;
    private readonly ITimberbornFireLogSink _logSink;
    private VisualElement? _clickTarget;
    private FireGrid? _grid;
    private int? _latestFocusCellIndex;
    private float _hideTime;

    public TimberbornPlayerFireAlertCameraFocus(
        UILayout uiLayout,
        CameraService cameraService)
    {
        _uiLayout = uiLayout ?? throw new ArgumentNullException(nameof(uiLayout));
        _cameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));
        _logSink = new UnityTimberbornFireLogSink();
    }

    public void Load()
    {
        _clickTarget = CreateClickTarget();
        _uiLayout.AddAbsoluteItem(_clickTarget);
    }

    public void Unload()
    {
        _clickTarget?.RemoveFromHierarchy();
        _clickTarget = null;
        Clear();
    }

    public void UpdateSingleton()
    {
        if (_clickTarget is not null &&
            _clickTarget.style.display.value != DisplayStyle.None &&
            Time.unscaledTime > _hideTime)
        {
            _clickTarget.style.display = DisplayStyle.None;
        }
    }

    public void ConfigureGrid(FireGrid grid)
    {
        _grid = grid;
        _latestFocusCellIndex = null;
    }

    public void Clear()
    {
        _latestFocusCellIndex = null;
        _hideTime = 0f;
        if (_clickTarget is not null)
        {
            _clickTarget.style.display = DisplayStyle.None;
        }
    }

    public void SetLatestFocusCell(int? cellIndex)
    {
        _latestFocusCellIndex = cellIndex;
        if (cellIndex is null || _clickTarget is null)
        {
            return;
        }

        _hideTime = Time.unscaledTime + ClickTargetDurationSeconds;
        _clickTarget.style.display = DisplayStyle.Flex;
        _logSink.Info($"wildfire_timberborn_player_fire_alert_focus_ready cell_index={cellIndex.Value}");
    }

    private VisualElement CreateClickTarget()
    {
        VisualElement clickTarget = new()
        {
            name = "WildfirePlayerFireAlertFocusClickTarget",
            pickingMode = PickingMode.Position,
        };
        clickTarget.style.position = Position.Absolute;
        clickTarget.style.left = Length.Percent(50);
        clickTarget.style.marginLeft = -ClickTargetWidth / 2f;
        clickTarget.style.top = ClickTargetTop;
        clickTarget.style.width = ClickTargetWidth;
        clickTarget.style.height = ClickTargetHeight;
        clickTarget.style.display = DisplayStyle.None;
        clickTarget.style.opacity = 0.001f;
        clickTarget.RegisterCallback<ClickEvent>(_ => FocusLatestFireCell());
        return clickTarget;
    }

    public void FocusLatestFireCell()
    {
        if (_grid is not { } grid || _latestFocusCellIndex is not { } cellIndex)
        {
            _logSink.Warning("wildfire_timberborn_player_fire_alert_focus_skipped reason=missing_target");
            return;
        }

        try
        {
            (int x, int y, int z) = grid.FromIndex(cellIndex);
            Vector3 worldPosition = CoordinateSystem.GridToWorldCentered(new Vector3Int(x, y, z));
            _cameraService.MoveTargetTo(worldPosition);
            _clickTarget!.style.display = DisplayStyle.None;
            _logSink.Info(
                "wildfire_timberborn_player_fire_alert_focused " +
                $"cell_index={cellIndex} " +
                $"x={x} y={y} z={z}");
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                "wildfire_timberborn_player_fire_alert_focus_failed " +
                $"cell_index={cellIndex} " +
                $"message=\"{EscapeLogValue(exception.Message)}\"");
        }
    }

    private static string EscapeLogValue(string value)
    {
        return value.Replace('\\', '/').Replace('"', '\'');
    }
}
