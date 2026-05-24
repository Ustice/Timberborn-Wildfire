using Timberborn.QuickNotificationSystem;
using Timberborn.CameraSystem;
using Timberborn.Coordinates;
using Timberborn.SingletonSystem;
using Timberborn.UILayoutSystem;
using UnityEngine;
using UnityEngine.UIElements;
using Wildfire.Core;
using Wildfire.Timberborn.Beavers;

namespace Wildfire.Timberborn.Alerts;

public sealed class TimberbornPlayerFireAlertSink : ITimberbornWorldConsequenceFeedbackSink
{
    private const uint DefaultNotificationThrottleTicks = 3;
    private readonly ITimberbornPlayerNotificationSink _notificationSink;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly uint _notificationThrottleTicks;
    private readonly List<TimberbornWorldConsequenceFeedbackEvent> _currentEvents = new();
    private int _currentFireStartedCount;
    private int _currentFuelSpentCount;
    private int _currentMaxHeat;
    private int? _currentFocusCellIndex;
    private uint? _lastNotificationTick;
    private TimberbornBeaverFieldBehaviorCounters? _lastBeaverCounters;

    public TimberbornPlayerFireAlertSink(
        ITimberbornPlayerNotificationSink notificationSink,
        ITimberbornFireLogSink logSink,
        uint notificationThrottleTicks = DefaultNotificationThrottleTicks)
    {
        _notificationSink = notificationSink ?? throw new ArgumentNullException(nameof(notificationSink));
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        if (notificationThrottleTicks == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(notificationThrottleTicks),
                notificationThrottleTicks,
                "The consequence feedback throttle must be positive.");
        }

        _notificationThrottleTicks = notificationThrottleTicks;
        Counters = TimberbornPlayerFireAlertCounters.Empty;
    }

    public TimberbornPlayerFireAlertCounters Counters { get; private set; }

    public void BeginAlertDispatch(uint tick)
    {
        _currentFireStartedCount = 0;
        _currentFuelSpentCount = 0;
        _currentMaxHeat = 0;
        _currentFocusCellIndex = null;
        _currentEvents.Clear();
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

    public void PublishConsequences(TimberbornWorldConsequenceFeedbackInput input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        _currentEvents.AddRange(input.Events.Where(static feedbackEvent => feedbackEvent.SourceEventCount > 0));
    }

    public void PublishBeaverBehavior(uint tick, TimberbornBeaverFieldBehaviorCounters counters)
    {
        TimberbornWorldConsequenceFeedbackEvent? feedbackEvent = CreateBeaverFeedbackEvent(tick, counters);
        _lastBeaverCounters = counters;
        if (feedbackEvent is null)
        {
            return;
        }

        BeginAlertDispatch(tick);
        _currentEvents.Add(feedbackEvent.Value);
        CompleteAlertDispatch(tick);
    }

    public void CompleteAlertDispatch(uint tick)
    {
        IReadOnlyList<TimberbornWorldConsequenceFeedbackEvent> events = BuildCurrentEvents(tick);
        int sourceEventCount = events.Sum(static feedbackEvent => feedbackEvent.SourceEventCount);
        if (sourceEventCount == 0)
        {
            return;
        }

        TimberbornWorldConsequenceFeedbackClass primaryClass = SelectPrimaryClass(events);
        string message = FormatMessage(events, primaryClass);
        bool suppressedByThrottle = ShouldSuppressByThrottle(tick);
        bool notificationSent = false;
        bool presentationFailed = false;
        int failureCount = Counters.PresentationFailureCount;
        int logOnlyFallbackCount = Counters.LogOnlyFallbackCount;

        if (suppressedByThrottle)
        {
            _logSink.Info(
                "wildfire_timberborn_world_consequence_feedback_suppressed " +
                $"tick={tick} " +
                $"source_events={sourceEventCount} " +
                $"classes={FormatClasses(events)} " +
                $"message=\"{EscapeLogValue(message)}\"");
        }
        else
        {
            try
            {
                _notificationSink.SendWarning(new TimberbornPlayerFireNotification(message, SelectFocusCell(events)));
                notificationSent = true;
                _lastNotificationTick = tick;
            }
            catch (Exception exception)
            {
                presentationFailed = true;
                failureCount++;
                logOnlyFallbackCount++;
                _logSink.Warning(
                    "wildfire_timberborn_world_consequence_feedback_failed " +
                    $"tick={tick} " +
                    $"message=\"{EscapeLogValue(exception.Message)}\" " +
                    $"fallback=\"{EscapeLogValue(message)}\"");
            }
        }

        Counters = Counters.AddDispatch(
            LastAlertTick: tick,
            LastFireStartedCount: _currentFireStartedCount,
            LastFuelSpentCount: _currentFuelSpentCount,
            LastMaxHeat: _currentMaxHeat,
            LastFocusCellIndex: SelectFocusCell(events),
            PresentationFailureCount: failureCount,
            LogOnlyFallbackCount: logOnlyFallbackCount,
            LastNotificationSent: notificationSent,
            LastNotificationSuppressed: suppressedByThrottle,
            LastPresentationFailed: presentationFailed,
            LastPrimaryClass: primaryClass,
            LastMessage: message,
            Events: events);
        _logSink.Info(
            "wildfire_timberborn_player_fire_alert_updated " +
            "wildfire_timberborn_world_consequence_feedback_updated " +
            $"tick={tick} " +
            $"fire_started={_currentFireStartedCount} " +
            $"fuel_spent={_currentFuelSpentCount} " +
            $"max_heat={_currentMaxHeat} " +
            $"focus_cell_index={FormatNullableNumber(Counters.LastFocusCellIndex)} " +
            $"source_events={sourceEventCount} " +
            $"coalesced_events={Counters.TotalCoalescedEventCount} " +
            $"classes={FormatClasses(events)} " +
            $"notification_sent={notificationSent.ToString().ToLowerInvariant()} " +
            $"notification_suppressed={suppressedByThrottle.ToString().ToLowerInvariant()} " +
            $"total_notifications={Counters.TotalNotificationCount} " +
            $"presentation_failures={Counters.PresentationFailureCount} " +
            $"log_only_fallbacks={Counters.LogOnlyFallbackCount} " +
            $"message=\"{EscapeLogValue(message)}\"");
    }

    public void Clear()
    {
        _currentFireStartedCount = 0;
        _currentFuelSpentCount = 0;
        _currentMaxHeat = 0;
        _currentFocusCellIndex = null;
        _currentEvents.Clear();
        _lastNotificationTick = null;
        _lastBeaverCounters = null;
        Counters = TimberbornPlayerFireAlertCounters.Empty;
    }

    private IReadOnlyList<TimberbornWorldConsequenceFeedbackEvent> BuildCurrentEvents(uint tick)
    {
        TimberbornWorldConsequenceFeedbackEvent? activeFireEvent =
            CreateActiveFireFeedbackEvent(tick, _currentFireStartedCount, _currentFuelSpentCount);
        return activeFireEvent is null
            ? _currentEvents.ToArray()
            : _currentEvents.Append(activeFireEvent.Value).ToArray();
    }

    private TimberbornWorldConsequenceFeedbackEvent? CreateActiveFireFeedbackEvent(
        uint tick,
        int fireStartedCount,
        int fuelSpentCount)
    {
        int alertCount = fireStartedCount + fuelSpentCount;
        return alertCount == 0
            ? null
            : new TimberbornWorldConsequenceFeedbackEvent(
                TimberbornWorldConsequenceFeedbackClass.ActiveFire,
                tick,
                SourceEventCount: alertCount,
                AffectedCellCount: alertCount,
                FocusCellIndex: _currentFocusCellIndex,
                Detail: FormatActiveFireDetail(fireStartedCount, fuelSpentCount, _currentMaxHeat));
    }

    private TimberbornWorldConsequenceFeedbackEvent? CreateBeaverFeedbackEvent(
        uint tick,
        TimberbornBeaverFieldBehaviorCounters counters)
    {
        TimberbornBeaverFieldBehaviorCounters previous = _lastBeaverCounters ??
            new TimberbornBeaverFieldBehaviorCounters(
                DispatcherEnabled: counters.DispatcherEnabled,
                TrackedBeaverCount: counters.TrackedBeaverCount,
                DecisionsEvaluated: 0,
                SmokeDecisionsApplied: 0,
                ToxicSmokeDecisionsApplied: 0,
                FireHeatDecisionsApplied: 0,
                NoOpDecisionsApplied: 0,
                DecisionsSkippedCooldown: 0,
                DecisionsSkippedBatch: 0,
                SkippedNoSafeApi: 0,
                FailedDecisions: 0,
                RecoveryActions: 0,
                SmokeExposedSamples: 0,
                SmokeExposureAccumulatedSamples: 0,
                SmokeCoughingEntered: 0,
                SmokeCoughingRecovered: 0,
                SmokeCoughingSlowdownsApplied: 0,
                SmokeCoughingSlowdownsRecovered: 0,
                SmokeCoughingSlowdownsSkippedNoSafeApi: 0,
                SmokeRecoveryDecays: 0,
                SmokeChokingCandidates: 0,
                SmokeChokingSlowdownsApplied: 0,
                SmokeChokingSlowdownsRecovered: 0,
                SmokeChokingSlowdownsSkippedNoSafeApi: 0,
                SmokeChokingSkippedUnsafeApi: 0,
                SmokeChokingIncapacitationCandidates: 0,
                SmokeChokingIncapacitationAttempts: 0,
                SmokeChokingIncapacitationsApplied: 0,
                SmokeChokingIncapacitationsRecovered: 0,
                SmokeChokingIncapacitationSkippedUnsafeApi: 0,
                SmokeChokingIncapacitationFailures: 0,
                SmokeDeathCandidates: 0,
                SmokeDeathAttempts: 0,
                SmokeDeathsApplied: 0,
                SmokeDeathSkippedUnsafeApi: 0,
                SmokeDeathFailures: 0,
                ToxicSmokeExposedBeavers: 0,
                ToxicSmokeExposureAccumulatedSamples: 0,
                ToxicSmokeContaminationEffectAttempts: 0,
                ToxicSmokeContaminationEffectSuccesses: 0,
                ToxicSmokeContaminationEffectFailures: 0,
                ToxicSmokeContaminationEffectSkippedUnsafeApi: 0,
                ToxicSmokeChokingCandidates: 0,
                ToxicSmokeDeathCandidates: 0,
                ToxicSmokeRecoveryDecays: 0,
                FireHeatExposedBeavers: 0,
                FireHeatActiveFlameContacts: 0,
                FireHeatAvoidanceCandidates: 0,
                FireHeatAvoidedCells: 0,
                FireHeatAvoidanceSkippedNoSafeApi: 0,
                FireHeatInterruptedJobCandidates: 0,
                FireHeatInterruptedJobs: 0,
                FireHeatInterruptedJobsSkippedNoSafeApi: 0,
                FireHeatSingedEntered: 0,
                FireHeatSingedRecovered: 0,
                FireHeatSingedSkippedNoSafeApi: 0,
                FireHeatBurnedEntered: 0,
                FireHeatBurnedRecovered: 0,
                FireHeatBurnedSkippedNoSafeApi: 0,
                FireHeatDeathCandidates: 0,
                FireHeatDeathSkippedUnsafeApi: 0,
                FireHeatRecoveryDecays: 0,
                PersistenceSaveCount: 0,
                PersistenceLoadCount: 0,
                LastDecisionTick: null);
        int dangerEvents =
            PositiveDelta(counters.SmokeCoughingEntered, previous.SmokeCoughingEntered) +
            PositiveDelta(counters.SmokeChokingCandidates, previous.SmokeChokingCandidates) +
            PositiveDelta(counters.SmokeChokingIncapacitationCandidates, previous.SmokeChokingIncapacitationCandidates) +
            PositiveDelta(counters.ToxicSmokeChokingCandidates, previous.ToxicSmokeChokingCandidates) +
            PositiveDelta(counters.FireHeatSingedEntered, previous.FireHeatSingedEntered) +
            PositiveDelta(counters.FireHeatBurnedEntered, previous.FireHeatBurnedEntered);
        int deathEvents =
            PositiveDelta(counters.SmokeDeathCandidates, previous.SmokeDeathCandidates) +
            PositiveDelta(counters.SmokeDeathsApplied, previous.SmokeDeathsApplied) +
            PositiveDelta(counters.ToxicSmokeDeathCandidates, previous.ToxicSmokeDeathCandidates) +
            PositiveDelta(counters.FireHeatDeathCandidates, previous.FireHeatDeathCandidates);
        int eventCount = dangerEvents + deathEvents;

        return eventCount == 0
            ? null
            : new TimberbornWorldConsequenceFeedbackEvent(
                TimberbornWorldConsequenceFeedbackClass.BeaverDangerDeath,
                tick,
                SourceEventCount: eventCount,
                AffectedCellCount: Math.Max(1, counters.TrackedBeaverCount),
                FocusCellIndex: null,
                Detail: deathEvents > 0
                    ? FormatCount(deathEvents, "beaver death risk", "beaver death risks")
                    : FormatCount(dangerEvents, "beaver danger", "beaver dangers"));
    }

    private bool ShouldSuppressByThrottle(uint tick)
    {
        return _lastNotificationTick is { } lastTick &&
            tick >= lastTick &&
            tick - lastTick < _notificationThrottleTicks;
    }

    private static TimberbornWorldConsequenceFeedbackClass SelectPrimaryClass(
        IReadOnlyList<TimberbornWorldConsequenceFeedbackEvent> events)
    {
        return events
            .OrderByDescending(static feedbackEvent => Priority(feedbackEvent.EventClass))
            .ThenByDescending(static feedbackEvent => feedbackEvent.SourceEventCount)
            .Select(static feedbackEvent => feedbackEvent.EventClass)
            .First();
    }

    private static int? SelectFocusCell(IReadOnlyList<TimberbornWorldConsequenceFeedbackEvent> events)
    {
        return events
            .OrderByDescending(static feedbackEvent => Priority(feedbackEvent.EventClass))
            .Select(static feedbackEvent => feedbackEvent.FocusCellIndex)
            .FirstOrDefault(static cellIndex => cellIndex is not null);
    }

    private static string FormatMessage(
        IReadOnlyList<TimberbornWorldConsequenceFeedbackEvent> events,
        TimberbornWorldConsequenceFeedbackClass primaryClass)
    {
        TimberbornWorldConsequenceFeedbackEvent[] orderedEvents = events
            .GroupBy(static feedbackEvent => feedbackEvent.EventClass)
            .Select(static group => group.Aggregate(static (left, right) => left.Combine(right)))
            .OrderByDescending(static feedbackEvent => Priority(feedbackEvent.EventClass))
            .ThenBy(static feedbackEvent => feedbackEvent.EventClass.ToString())
            .ToArray();
        TimberbornWorldConsequenceFeedbackEvent? activeFire =
            orderedEvents.SingleOrDefault(static feedbackEvent =>
                feedbackEvent.EventClass == TimberbornWorldConsequenceFeedbackClass.ActiveFire);

        if (orderedEvents.Length == 1 &&
            primaryClass == TimberbornWorldConsequenceFeedbackClass.ActiveFire &&
            activeFire is { Detail.Length: > 0 })
        {
            return activeFire.Value.Detail;
        }

        TimberbornWorldConsequenceFeedbackEvent? structureOnFire =
            orderedEvents.SingleOrDefault(static feedbackEvent =>
                feedbackEvent.EventClass == TimberbornWorldConsequenceFeedbackClass.StructureOnFire);
        if (orderedEvents.Length == 1 &&
            primaryClass == TimberbornWorldConsequenceFeedbackClass.StructureOnFire &&
            structureOnFire is { Detail.Length: > 0 })
        {
            return $"Wildfire alert: {structureOnFire.Value.Detail}.";
        }

        string classSummary = string.Join(
            "; ",
            orderedEvents.Select(static feedbackEvent =>
                $"{FormatClassName(feedbackEvent.EventClass)}: {feedbackEvent.SourceEventCount}"));
        string regionSummary = FormatRegion(orderedEvents);
        if (primaryClass == TimberbornWorldConsequenceFeedbackClass.StructureOnFire &&
            structureOnFire is { Detail.Length: > 0 })
        {
            return $"Wildfire alert: {structureOnFire.Value.Detail}. {classSummary}{regionSummary}.";
        }

        return $"Wildfire consequence: {FormatClassName(primaryClass)}. {classSummary}{regionSummary}.";
    }

    private static string FormatActiveFireDetail(int fireStartedCount, int fuelSpentCount, int maxHeat)
    {
        string[] parts =
        {
            FormatCount(fireStartedCount, "new fire", "new fires"),
            FormatCount(fuelSpentCount, "burned-out cell", "burned-out cells"),
        };
        string summary = string.Join(", ", parts.Where(static part => part.Length > 0));

        return $"Wildfire alert: {summary}. Max heat {maxHeat}.";
    }

    private static string FormatRegion(IReadOnlyList<TimberbornWorldConsequenceFeedbackEvent> events)
    {
        int affectedCellCount = events.Sum(static feedbackEvent => feedbackEvent.AffectedCellCount);
        int? focusCellIndex = SelectFocusCell(events);
        string affectedCells = affectedCellCount > 0
            ? $" across {FormatCount(affectedCellCount, "affected cell", "affected cells")}"
            : string.Empty;
        return focusCellIndex is null
            ? affectedCells
            : $"{affectedCells} near cell {focusCellIndex.Value}";
    }

    private static string FormatClassName(TimberbornWorldConsequenceFeedbackClass eventClass)
    {
        return eventClass switch
        {
            TimberbornWorldConsequenceFeedbackClass.ActiveFire => "active fire",
            TimberbornWorldConsequenceFeedbackClass.StructureOnFire => "structure on fire",
            TimberbornWorldConsequenceFeedbackClass.BuildingDamageClosure => "building damage/closure",
            TimberbornWorldConsequenceFeedbackClass.PlantCropResourceLoss => "plant/crop/resource loss",
            TimberbornWorldConsequenceFeedbackClass.BeaverDangerDeath => "beaver danger/death",
            TimberbornWorldConsequenceFeedbackClass.AshAftermath => "ash aftermath",
            _ => eventClass.ToString(),
        };
    }

    private static string FormatClasses(IReadOnlyList<TimberbornWorldConsequenceFeedbackEvent> events)
    {
        return string.Join(
            ",",
            events
                .Select(static feedbackEvent => feedbackEvent.EventClass.ToString().ToLowerInvariant())
                .Distinct()
                .OrderBy(static value => value));
    }

    private static int Priority(TimberbornWorldConsequenceFeedbackClass eventClass)
    {
        return eventClass switch
        {
            TimberbornWorldConsequenceFeedbackClass.BeaverDangerDeath => 50,
            TimberbornWorldConsequenceFeedbackClass.StructureOnFire => 45,
            TimberbornWorldConsequenceFeedbackClass.BuildingDamageClosure => 40,
            TimberbornWorldConsequenceFeedbackClass.ActiveFire => 30,
            TimberbornWorldConsequenceFeedbackClass.PlantCropResourceLoss => 20,
            TimberbornWorldConsequenceFeedbackClass.AshAftermath => 10,
            _ => 0,
        };
    }

    private static int PositiveDelta(int current, int previous)
    {
        return Math.Max(0, current - previous);
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
    int TotalSourceEventCount,
    int TotalCoalescedEventCount,
    int ActiveFireEventCount,
    int StructureOnFireEventCount,
    int StructureOnFireCoalescedEventCount,
    int BuildingDamageClosureEventCount,
    int PlantCropResourceLossEventCount,
    int BeaverDangerDeathEventCount,
    int AshAftermathEventCount,
    int TotalNotificationCount,
    int ActiveFireNotificationCount,
    int StructureOnFireNotificationCount,
    int StructureOnFireNotificationSuppressedThrottleCount,
    int StructureOnFirePresentationFailureCount,
    int BuildingDamageClosureNotificationCount,
    int PlantCropResourceLossNotificationCount,
    int BeaverDangerDeathNotificationCount,
    int AshAftermathNotificationCount,
    int NotificationSuppressedThrottleCount,
    int PresentationFailureCount,
    int LogOnlyFallbackCount,
    bool LastNotificationSent,
    bool LastNotificationSuppressed,
    TimberbornWorldConsequenceFeedbackClass LastPrimaryClass,
    string LastMessage)
{
    public static readonly TimberbornPlayerFireAlertCounters Empty = new(
        LastAlertTick: 0,
        LastFireStartedCount: 0,
        LastFuelSpentCount: 0,
        LastMaxHeat: 0,
        LastFocusCellIndex: null,
        TotalSourceEventCount: 0,
        TotalCoalescedEventCount: 0,
        ActiveFireEventCount: 0,
        StructureOnFireEventCount: 0,
        StructureOnFireCoalescedEventCount: 0,
        BuildingDamageClosureEventCount: 0,
        PlantCropResourceLossEventCount: 0,
        BeaverDangerDeathEventCount: 0,
        AshAftermathEventCount: 0,
        TotalNotificationCount: 0,
        ActiveFireNotificationCount: 0,
        StructureOnFireNotificationCount: 0,
        StructureOnFireNotificationSuppressedThrottleCount: 0,
        StructureOnFirePresentationFailureCount: 0,
        BuildingDamageClosureNotificationCount: 0,
        PlantCropResourceLossNotificationCount: 0,
        BeaverDangerDeathNotificationCount: 0,
        AshAftermathNotificationCount: 0,
        NotificationSuppressedThrottleCount: 0,
        PresentationFailureCount: 0,
        LogOnlyFallbackCount: 0,
        LastNotificationSent: false,
        LastNotificationSuppressed: false,
        LastPrimaryClass: TimberbornWorldConsequenceFeedbackClass.None,
        LastMessage: string.Empty);

    public TimberbornPlayerFireAlertCounters AddDispatch(
        uint LastAlertTick,
        int LastFireStartedCount,
        int LastFuelSpentCount,
        int LastMaxHeat,
        int? LastFocusCellIndex,
        int PresentationFailureCount,
        int LogOnlyFallbackCount,
        bool LastNotificationSent,
        bool LastNotificationSuppressed,
        bool LastPresentationFailed,
        TimberbornWorldConsequenceFeedbackClass LastPrimaryClass,
        string LastMessage,
        IReadOnlyList<TimberbornWorldConsequenceFeedbackEvent> Events)
    {
        int sourceEventCount = Events.Sum(static feedbackEvent => feedbackEvent.SourceEventCount);
        int coalescedEventCount = Math.Max(0, sourceEventCount - 1);
        bool sent = LastNotificationSent;
        return this with
        {
            LastAlertTick = LastAlertTick,
            LastFireStartedCount = LastFireStartedCount,
            LastFuelSpentCount = LastFuelSpentCount,
            LastMaxHeat = LastMaxHeat,
            LastFocusCellIndex = LastFocusCellIndex,
            TotalSourceEventCount = TotalSourceEventCount + sourceEventCount,
            TotalCoalescedEventCount = TotalCoalescedEventCount + coalescedEventCount,
            ActiveFireEventCount = ActiveFireEventCount +
                SumClass(Events, TimberbornWorldConsequenceFeedbackClass.ActiveFire),
            StructureOnFireEventCount = StructureOnFireEventCount +
                SumClass(Events, TimberbornWorldConsequenceFeedbackClass.StructureOnFire),
            StructureOnFireCoalescedEventCount = StructureOnFireCoalescedEventCount +
                SumAffectedClass(Events, TimberbornWorldConsequenceFeedbackClass.StructureOnFire),
            BuildingDamageClosureEventCount = BuildingDamageClosureEventCount +
                SumClass(Events, TimberbornWorldConsequenceFeedbackClass.BuildingDamageClosure),
            PlantCropResourceLossEventCount = PlantCropResourceLossEventCount +
                SumClass(Events, TimberbornWorldConsequenceFeedbackClass.PlantCropResourceLoss),
            BeaverDangerDeathEventCount = BeaverDangerDeathEventCount +
                SumClass(Events, TimberbornWorldConsequenceFeedbackClass.BeaverDangerDeath),
            AshAftermathEventCount = AshAftermathEventCount +
                SumClass(Events, TimberbornWorldConsequenceFeedbackClass.AshAftermath),
            TotalNotificationCount = TotalNotificationCount + (sent ? 1 : 0),
            ActiveFireNotificationCount = ActiveFireNotificationCount +
                NotificationClassCount(Events, TimberbornWorldConsequenceFeedbackClass.ActiveFire, sent),
            StructureOnFireNotificationCount = StructureOnFireNotificationCount +
                NotificationClassCount(Events, TimberbornWorldConsequenceFeedbackClass.StructureOnFire, sent),
            StructureOnFireNotificationSuppressedThrottleCount =
                StructureOnFireNotificationSuppressedThrottleCount +
                NotificationClassCount(
                    Events,
                    TimberbornWorldConsequenceFeedbackClass.StructureOnFire,
                    LastNotificationSuppressed),
            StructureOnFirePresentationFailureCount = StructureOnFirePresentationFailureCount +
                NotificationClassCount(
                    Events,
                    TimberbornWorldConsequenceFeedbackClass.StructureOnFire,
                    LastPresentationFailed),
            BuildingDamageClosureNotificationCount = BuildingDamageClosureNotificationCount +
                NotificationClassCount(Events, TimberbornWorldConsequenceFeedbackClass.BuildingDamageClosure, sent),
            PlantCropResourceLossNotificationCount = PlantCropResourceLossNotificationCount +
                NotificationClassCount(Events, TimberbornWorldConsequenceFeedbackClass.PlantCropResourceLoss, sent),
            BeaverDangerDeathNotificationCount = BeaverDangerDeathNotificationCount +
                NotificationClassCount(Events, TimberbornWorldConsequenceFeedbackClass.BeaverDangerDeath, sent),
            AshAftermathNotificationCount = AshAftermathNotificationCount +
                NotificationClassCount(Events, TimberbornWorldConsequenceFeedbackClass.AshAftermath, sent),
            NotificationSuppressedThrottleCount = NotificationSuppressedThrottleCount +
                (LastNotificationSuppressed ? 1 : 0),
            PresentationFailureCount = PresentationFailureCount,
            LogOnlyFallbackCount = LogOnlyFallbackCount,
            LastNotificationSent = LastNotificationSent,
            LastNotificationSuppressed = LastNotificationSuppressed,
            LastPrimaryClass = LastPrimaryClass,
            LastMessage = LastMessage,
        };
    }

    private static int SumClass(
        IReadOnlyList<TimberbornWorldConsequenceFeedbackEvent> events,
        TimberbornWorldConsequenceFeedbackClass eventClass)
    {
        return events
            .Where(feedbackEvent => feedbackEvent.EventClass == eventClass)
            .Sum(static feedbackEvent => feedbackEvent.SourceEventCount);
    }

    private static int SumAffectedClass(
        IReadOnlyList<TimberbornWorldConsequenceFeedbackEvent> events,
        TimberbornWorldConsequenceFeedbackClass eventClass)
    {
        return events
            .Where(feedbackEvent => feedbackEvent.EventClass == eventClass)
            .Sum(static feedbackEvent => feedbackEvent.AffectedCellCount);
    }

    private static int NotificationClassCount(
        IReadOnlyList<TimberbornWorldConsequenceFeedbackEvent> events,
        TimberbornWorldConsequenceFeedbackClass eventClass,
        bool sent)
    {
        return sent && events.Any(feedbackEvent => feedbackEvent.EventClass == eventClass) ? 1 : 0;
    }
}

public readonly record struct TimberbornPlayerFireNotification(string Message, int? FocusCellIndex);

public interface ITimberbornPlayerNotificationSink
{
    void SendWarning(TimberbornPlayerFireNotification notification);
}

public enum TimberbornWorldConsequenceFeedbackClass
{
    None = 0,
    ActiveFire = 1,
    StructureOnFire = 2,
    BuildingDamageClosure = 3,
    PlantCropResourceLoss = 4,
    BeaverDangerDeath = 5,
    AshAftermath = 6,
}

public sealed record TimberbornWorldConsequenceFeedbackInput(
    uint Tick,
    IReadOnlyList<TimberbornWorldConsequenceFeedbackEvent> Events)
{
    public static readonly TimberbornWorldConsequenceFeedbackInput Empty = new(
        Tick: 0,
        Array.Empty<TimberbornWorldConsequenceFeedbackEvent>());
}

public readonly record struct TimberbornWorldConsequenceFeedbackEvent(
    TimberbornWorldConsequenceFeedbackClass EventClass,
    uint Tick,
    int SourceEventCount,
    int AffectedCellCount,
    int? FocusCellIndex,
    string Detail)
{
    public TimberbornWorldConsequenceFeedbackEvent Combine(TimberbornWorldConsequenceFeedbackEvent other)
    {
        return new TimberbornWorldConsequenceFeedbackEvent(
            EventClass,
            Math.Max(Tick, other.Tick),
            SourceEventCount + other.SourceEventCount,
            AffectedCellCount + other.AffectedCellCount,
            FocusCellIndex ?? other.FocusCellIndex,
            Detail.Length > 0 ? Detail : other.Detail);
    }
}

public interface ITimberbornWorldConsequenceFeedbackSink : ITimberbornFireAlertDispatchSink
{
    void PublishConsequences(TimberbornWorldConsequenceFeedbackInput input);

    void PublishBeaverBehavior(uint tick, TimberbornBeaverFieldBehaviorCounters counters);
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
