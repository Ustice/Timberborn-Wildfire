using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornFireDeltaConsumer
{
    private readonly Dictionary<int, TimberbornFireDebugVisualCellState> _debugVisualCells = new();
    private readonly ITimberbornFireLogSink _logSink;
    private readonly TimberbornFireDeltaConsumerSinks _sinks;

    public TimberbornFireDeltaConsumer(ITimberbornFireLogSink logSink)
        : this(logSink, TimberbornFireDeltaConsumerSinks.Null)
    {
    }

    public TimberbornFireDeltaConsumer(ITimberbornFireLogSink logSink, TimberbornFireDeltaConsumerSinks sinks)
    {
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        _sinks = sinks ?? throw new ArgumentNullException(nameof(sinks));
        LastSummary = TimberbornFireDeltaConsumerSummary.Empty;
    }

    public TimberbornFireDeltaConsumerSummary LastSummary { get; private set; }

    public uint LastPositiveWaterChangedTick { get; private set; }

    public int LastPositiveWaterChangedCount { get; private set; }

    public IReadOnlyDictionary<int, TimberbornFireDebugVisualCellState> DebugVisualCells => _debugVisualCells;

    public void Reset()
    {
        _debugVisualCells.Clear();
        LastSummary = TimberbornFireDeltaConsumerSummary.Empty;
        LastPositiveWaterChangedTick = 0;
        LastPositiveWaterChangedCount = 0;
    }

    public TimberbornFireDeltaConsumerSummary Consume(uint tick, ReadOnlySpan<CellDelta> deltas)
    {
        TimberbornFireCellDeltaDecision[] decisions = deltas
            .ToArray()
            .Select(TimberbornFireCellDeltaDecision.FromDelta)
            .ToArray();

        TimberbornFireDebugVisualCellState[] debugVisualStates = decisions
            .Where(static decision => decision.ShouldUpdateDebugVisualState)
            .Select(decision => TimberbornFireDebugVisualCellState.FromDecision(tick, decision))
            .ToArray();

        Array.ForEach(debugVisualStates, state =>
        {
            _debugVisualCells[state.CellIndex] = state;
            _sinks.DebugVisualSink.UpdateDebugVisualState(state);
        });

        TimberbornFireVisualEffectEvent[] visualEffectEvents = decisions
            .Where(static decision => decision.ShouldTriggerVisualEffect)
            .Select(decision => TimberbornFireVisualEffectEvent.FromDecision(tick, decision))
            .ToArray();
        int visualEffectFailureCount = ConsumeVisualEffects(tick, visualEffectEvents);

        TimberbornFireGameplayConsequence[] gameplayConsequences = decisions
            .Where(static decision => decision.ShouldApplyGameplayConsequence)
            .Select(decision => TimberbornFireGameplayConsequence.FromDecision(tick, decision))
            .ToArray();
        Array.ForEach(gameplayConsequences, _sinks.GameplayConsequenceSink.ApplyConsequence);

        TimberbornBuildingBurnoutConsequenceSummary buildingBurnoutSummary =
            _sinks.BuildingBurnoutConsequenceSink.ApplyConsequences(tick, decisions);

        TimberbornFireAlertEvent[] alertEvents = decisions
            .Where(static decision => decision.ShouldEmitAlert)
            .Select(decision => TimberbornFireAlertEvent.FromDecision(tick, decision))
            .ToArray();
        Array.ForEach(alertEvents, _sinks.AlertSink.PublishAlert);

        LastSummary = TimberbornFireDeltaConsumerSummary.FromDecisions(
            tick,
            decisions,
            debugVisualStates.Length,
            _debugVisualCells.Count,
            visualEffectEvents.Length,
            visualEffectFailureCount,
            gameplayConsequences.Length,
            buildingBurnoutSummary,
            alertEvents.Length);
        if (LastSummary.WaterChangedCount > 0)
        {
            LastPositiveWaterChangedTick = tick;
            LastPositiveWaterChangedCount = LastSummary.WaterChangedCount;
        }

        _logSink.Info(LastSummary.ToLogToken());
        return LastSummary;
    }

    private int ConsumeVisualEffects(uint tick, IReadOnlyList<TimberbornFireVisualEffectEvent> visualEffectEvents)
    {
        ITimberbornFireVisualEffectDispatchSink? visualEffectDispatchSink =
            _sinks.VisualEffectSink as ITimberbornFireVisualEffectDispatchSink;
        int failureCount = 0;
        bool canUpdateVisualEffects = true;

        if (visualEffectDispatchSink is not null)
        {
            try
            {
                visualEffectDispatchSink.BeginVisualEffectDispatch(tick);
            }
            catch (Exception exception)
            {
                failureCount++;
                canUpdateVisualEffects = false;
                LogVisualEffectFailure(tick, null, "begin", exception);
            }
        }

        if (canUpdateVisualEffects)
        {
            visualEffectEvents
                .ToList()
                .ForEach(effectEvent =>
                {
                    try
                    {
                        _sinks.VisualEffectSink.UpdateVisualEffect(effectEvent);
                    }
                    catch (Exception exception)
                    {
                        failureCount++;
                        LogVisualEffectFailure(tick, effectEvent.CellIndex, "update", exception);
                    }
                });
        }

        if (visualEffectDispatchSink is not null)
        {
            try
            {
                visualEffectDispatchSink.CompleteVisualEffectDispatch(tick);
            }
            catch (Exception exception)
            {
                failureCount++;
                LogVisualEffectFailure(tick, null, "complete", exception);
            }
        }

        return failureCount;
    }

    private void LogVisualEffectFailure(uint tick, int? cellIndex, string stage, Exception exception)
    {
        _logSink.Warning(
            "wildfire_timberborn_visual_effect_sink_failed " +
            $"tick={tick} " +
            $"stage={stage} " +
            $"cell_index={FormatNumber(cellIndex)} " +
            $"message=\"{EscapeLogValue(exception.Message)}\"");
    }

    private static string FormatNumber(int? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }

    private static string EscapeLogValue(string value)
    {
        return value.Replace('\\', '/').Replace('"', '\'');
    }
}

public readonly record struct TimberbornFireCellDeltaDecision(
    int CellIndex,
    ushort OldCell,
    ushort NewCell,
    int OldFuel,
    int NewFuel,
    int OldHeat,
    int NewHeat,
    int OldWater,
    int NewWater,
    bool WasBurning,
    bool IsBurning,
    bool FuelDepleted)
{
    public bool StartedBurning => !WasBurning && IsBurning;

    public bool StoppedBurning => WasBurning && !IsBurning;

    public bool HeatChanged => OldHeat != NewHeat;

    public bool FuelChanged => OldFuel != NewFuel;

    public bool WaterChanged => OldWater != NewWater;

    public bool ShouldUpdateDebugVisualState =>
        StartedBurning ||
        StoppedBurning ||
        HeatChanged ||
        FuelChanged ||
        WaterChanged;

    public bool ShouldTriggerVisualEffect => ShouldUpdateDebugVisualState;

    public bool ShouldApplyGameplayConsequence =>
        StartedBurning ||
        StoppedBurning ||
        FuelDepleted;

    public bool ShouldEmitAlert =>
        StartedBurning ||
        FuelDepleted;

    public static TimberbornFireCellDeltaDecision FromDelta(CellDelta delta)
    {
        int oldFuel = PackedCell.Fuel(delta.OldCell);
        int newFuel = PackedCell.Fuel(delta.NewCell);

        return new TimberbornFireCellDeltaDecision(
            delta.CellIndex,
            delta.OldCell,
            delta.NewCell,
            oldFuel,
            newFuel,
            PackedCell.Heat(delta.OldCell),
            PackedCell.Heat(delta.NewCell),
            PackedCell.Water(delta.OldCell),
            PackedCell.Water(delta.NewCell),
            PackedCell.IsBurning(delta.OldCell),
            PackedCell.IsBurning(delta.NewCell),
            FuelDepleted: oldFuel > 0 && newFuel == 0);
    }
}

public readonly record struct TimberbornFireDebugVisualCellState(
    int CellIndex,
    uint Tick,
    ushort PackedCellValue)
{
    public int Fuel => PackedCell.Fuel(PackedCellValue);

    public int Heat => PackedCell.Heat(PackedCellValue);

    public int Water => PackedCell.Water(PackedCellValue);

    public bool IsBurning => PackedCell.IsBurning(PackedCellValue);

    public bool IsSpentFuel => Fuel == 0;

    public static TimberbornFireDebugVisualCellState FromDecision(uint tick, TimberbornFireCellDeltaDecision decision)
    {
        return new TimberbornFireDebugVisualCellState(
            decision.CellIndex,
            tick,
            decision.NewCell);
    }
}

public enum TimberbornFireVisualEffectKind
{
    HeatChanged,
    BurningStarted,
    BurningStopped,
    FuelSpent,
    FuelChanged,
    WaterChanged,
}

public readonly record struct TimberbornFireVisualEffectEvent(
    int CellIndex,
    uint Tick,
    TimberbornFireVisualEffectKind Kind,
    int Fuel,
    int Heat,
    int Water,
    bool IsBurning)
{
    public static TimberbornFireVisualEffectEvent FromDecision(uint tick, TimberbornFireCellDeltaDecision decision)
    {
        return new TimberbornFireVisualEffectEvent(
            decision.CellIndex,
            tick,
            SelectKind(decision),
            decision.NewFuel,
            decision.NewHeat,
            decision.NewWater,
            decision.IsBurning);
    }

    private static TimberbornFireVisualEffectKind SelectKind(TimberbornFireCellDeltaDecision decision)
    {
        if (decision.FuelDepleted)
        {
            return TimberbornFireVisualEffectKind.FuelSpent;
        }

        if (decision.StartedBurning)
        {
            return TimberbornFireVisualEffectKind.BurningStarted;
        }

        if (decision.StoppedBurning)
        {
            return TimberbornFireVisualEffectKind.BurningStopped;
        }

        if (decision.FuelChanged)
        {
            return TimberbornFireVisualEffectKind.FuelChanged;
        }

        return decision.WaterChanged
            ? TimberbornFireVisualEffectKind.WaterChanged
            : TimberbornFireVisualEffectKind.HeatChanged;
    }
}

public enum TimberbornFireGameplayConsequenceKind
{
    FireStarted,
    FireStopped,
    FuelSpent,
}

public readonly record struct TimberbornFireGameplayConsequence(
    int CellIndex,
    uint Tick,
    TimberbornFireGameplayConsequenceKind Kind,
    int Fuel,
    int Heat,
    int Water)
{
    public static TimberbornFireGameplayConsequence FromDecision(uint tick, TimberbornFireCellDeltaDecision decision)
    {
        return new TimberbornFireGameplayConsequence(
            decision.CellIndex,
            tick,
            SelectKind(decision),
            decision.NewFuel,
            decision.NewHeat,
            decision.NewWater);
    }

    private static TimberbornFireGameplayConsequenceKind SelectKind(TimberbornFireCellDeltaDecision decision)
    {
        if (decision.FuelDepleted)
        {
            return TimberbornFireGameplayConsequenceKind.FuelSpent;
        }

        return decision.StartedBurning
            ? TimberbornFireGameplayConsequenceKind.FireStarted
            : TimberbornFireGameplayConsequenceKind.FireStopped;
    }
}

public readonly record struct TimberbornBuildingBurnoutConsequence(
    int CellIndex,
    uint Tick,
    bool ShouldApplyBurnout,
    int Fuel,
    int Heat,
    int Water)
{
    public static TimberbornBuildingBurnoutConsequence FromDecision(
        uint tick,
        TimberbornFireCellDeltaDecision decision)
    {
        return new TimberbornBuildingBurnoutConsequence(
            decision.CellIndex,
            tick,
            decision.FuelDepleted,
            decision.NewFuel,
            decision.NewHeat,
            decision.NewWater);
    }
}

public readonly record struct TimberbornBuildingBurnoutConsequenceResult(
    bool MatchedBuildingCell,
    bool AppliedConsequence);

public readonly record struct TimberbornBuildingBurnoutConsequenceSummary(
    int ConsideredDeltaCount,
    int MatchedBuildingCellCount,
    int AppliedConsequenceCount)
{
    public static readonly TimberbornBuildingBurnoutConsequenceSummary Empty = new(
        ConsideredDeltaCount: 0,
        MatchedBuildingCellCount: 0,
        AppliedConsequenceCount: 0);
}

public enum TimberbornFireAlertKind
{
    FireStarted,
    FuelSpent,
}

public readonly record struct TimberbornFireAlertEvent(
    int CellIndex,
    uint Tick,
    TimberbornFireAlertKind Kind,
    int Heat)
{
    public static TimberbornFireAlertEvent FromDecision(uint tick, TimberbornFireCellDeltaDecision decision)
    {
        return new TimberbornFireAlertEvent(
            decision.CellIndex,
            tick,
            decision.FuelDepleted ? TimberbornFireAlertKind.FuelSpent : TimberbornFireAlertKind.FireStarted,
            decision.NewHeat);
    }
}

public readonly record struct TimberbornFireDeltaConsumerSummary(
    uint Tick,
    int ChangedCellCount,
    int DebugVisualUpdatedCellCount,
    int DebugVisualCellCount,
    int StartedBurningCount,
    int StoppedBurningCount,
    int FuelDepletedCount,
    int HeatChangedCount,
    int WaterChangedCount,
    int VisualEffectEventCount,
    int VisualEffectFailureCount,
    int GameplayConsequenceCount,
    int BuildingBurnoutConsideredDeltaCount,
    int BuildingBurnoutMatchedCellCount,
    int BuildingBurnoutAppliedConsequenceCount,
    int AlertCount,
    int MaxHeat)
{
    public static readonly TimberbornFireDeltaConsumerSummary Empty = new(
        Tick: 0,
        ChangedCellCount: 0,
        DebugVisualUpdatedCellCount: 0,
        DebugVisualCellCount: 0,
        StartedBurningCount: 0,
        StoppedBurningCount: 0,
        FuelDepletedCount: 0,
        HeatChangedCount: 0,
        WaterChangedCount: 0,
        VisualEffectEventCount: 0,
        VisualEffectFailureCount: 0,
        GameplayConsequenceCount: 0,
        BuildingBurnoutConsideredDeltaCount: 0,
        BuildingBurnoutMatchedCellCount: 0,
        BuildingBurnoutAppliedConsequenceCount: 0,
        AlertCount: 0,
        MaxHeat: 0);

    public static TimberbornFireDeltaConsumerSummary FromDecisions(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions,
        int debugVisualUpdatedCellCount,
        int debugVisualCellCount,
        int visualEffectEventCount,
        int visualEffectFailureCount,
        int gameplayConsequenceCount,
        TimberbornBuildingBurnoutConsequenceSummary buildingBurnoutSummary,
        int alertCount)
    {
        return new TimberbornFireDeltaConsumerSummary(
            tick,
            decisions.Count,
            debugVisualUpdatedCellCount,
            debugVisualCellCount,
            decisions.Count(static decision => decision.StartedBurning),
            decisions.Count(static decision => decision.StoppedBurning),
            decisions.Count(static decision => decision.FuelDepleted),
            decisions.Count(static decision => decision.HeatChanged),
            decisions.Count(static decision => decision.WaterChanged),
            visualEffectEventCount,
            visualEffectFailureCount,
            gameplayConsequenceCount,
            buildingBurnoutSummary.ConsideredDeltaCount,
            buildingBurnoutSummary.MatchedBuildingCellCount,
            buildingBurnoutSummary.AppliedConsequenceCount,
            alertCount,
            decisions.Select(static decision => decision.NewHeat).DefaultIfEmpty(0).Max());
    }

    public string ToLogToken()
    {
        return "wildfire_timberborn_delta_consumer_completed " +
            $"tick={Tick} " +
            $"changed_cells={ChangedCellCount} " +
            $"debug_visual_updated_cells={DebugVisualUpdatedCellCount} " +
            $"debug_visual_cells={DebugVisualCellCount} " +
            $"started_burning={StartedBurningCount} " +
            $"stopped_burning={StoppedBurningCount} " +
            $"fuel_depleted={FuelDepletedCount} " +
            $"heat_changed={HeatChangedCount} " +
            $"water_changed={WaterChangedCount} " +
            $"visual_effect_events={VisualEffectEventCount} " +
            $"visual_effect_failures={VisualEffectFailureCount} " +
            $"gameplay_consequences={GameplayConsequenceCount} " +
            $"building_burnout_considered_deltas={BuildingBurnoutConsideredDeltaCount} " +
            $"building_burnout_matched_cells={BuildingBurnoutMatchedCellCount} " +
            $"building_burnout_applied_consequences={BuildingBurnoutAppliedConsequenceCount} " +
            $"alerts={AlertCount} " +
            $"max_heat={MaxHeat}";
    }
}

public sealed class TimberbornFireDeltaConsumerSinks
{
    public static readonly TimberbornFireDeltaConsumerSinks Null = new();

    public TimberbornFireDeltaConsumerSinks(
        ITimberbornFireDebugVisualSink? debugVisualSink = null,
        ITimberbornFireVisualEffectSink? visualEffectSink = null,
        ITimberbornFireGameplayConsequenceSink? gameplayConsequenceSink = null,
        ITimberbornBuildingBurnoutConsequenceSink? buildingBurnoutConsequenceSink = null,
        ITimberbornFireAlertSink? alertSink = null)
    {
        DebugVisualSink = debugVisualSink ?? NullTimberbornFireDebugVisualSink.Instance;
        VisualEffectSink = visualEffectSink ?? NullTimberbornFireVisualEffectSink.Instance;
        GameplayConsequenceSink = gameplayConsequenceSink ?? NullTimberbornFireGameplayConsequenceSink.Instance;
        BuildingBurnoutConsequenceSink =
            buildingBurnoutConsequenceSink ?? NullTimberbornBuildingBurnoutConsequenceSink.Instance;
        AlertSink = alertSink ?? NullTimberbornFireAlertSink.Instance;
    }

    public ITimberbornFireDebugVisualSink DebugVisualSink { get; }

    public ITimberbornFireVisualEffectSink VisualEffectSink { get; }

    public ITimberbornFireGameplayConsequenceSink GameplayConsequenceSink { get; }

    public ITimberbornBuildingBurnoutConsequenceSink BuildingBurnoutConsequenceSink { get; }

    public ITimberbornFireAlertSink AlertSink { get; }
}

public sealed class TimberbornFireDebugVisualStateSink : ITimberbornFireDebugVisualSink
{
    private readonly Dictionary<int, TimberbornFireDebugVisualCellState> _states = new();

    public IReadOnlyDictionary<int, TimberbornFireDebugVisualCellState> States => _states;

    public void Clear()
    {
        _states.Clear();
    }

    public void UpdateDebugVisualState(TimberbornFireDebugVisualCellState state)
    {
        _states[state.CellIndex] = state;
    }
}

public interface ITimberbornFireDebugVisualSink
{
    void UpdateDebugVisualState(TimberbornFireDebugVisualCellState state);
}

public interface ITimberbornFireVisualEffectSink
{
    void UpdateVisualEffect(TimberbornFireVisualEffectEvent effectEvent);
}

public interface ITimberbornFireVisualEffectDispatchSink : ITimberbornFireVisualEffectSink
{
    void BeginVisualEffectDispatch(uint tick);

    void CompleteVisualEffectDispatch(uint tick);
}

public interface ITimberbornFireGameplayConsequenceSink
{
    void ApplyConsequence(TimberbornFireGameplayConsequence consequence);
}

public interface ITimberbornBuildingBurnoutConsequenceSink
{
    TimberbornBuildingBurnoutConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions);
}

public interface ITimberbornBuildingBurnoutConsequenceApi
{
    TimberbornBuildingBurnoutConsequenceResult ApplyConsequence(TimberbornBuildingBurnoutConsequence consequence);
}

public interface ITimberbornFireAlertSink
{
    void PublishAlert(TimberbornFireAlertEvent alertEvent);
}

public sealed class TimberbornBuildingBurnoutConsequenceSink : ITimberbornBuildingBurnoutConsequenceSink
{
    private readonly ITimberbornBuildingBurnoutConsequenceApi _consequenceApi;

    public TimberbornBuildingBurnoutConsequenceSink(ITimberbornBuildingBurnoutConsequenceApi consequenceApi)
    {
        _consequenceApi = consequenceApi ?? throw new ArgumentNullException(nameof(consequenceApi));
    }

    public TimberbornBuildingBurnoutConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        TimberbornBuildingBurnoutConsequenceResult[] results = decisions
            .Select(decision => TimberbornBuildingBurnoutConsequence.FromDecision(tick, decision))
            .Select(_consequenceApi.ApplyConsequence)
            .ToArray();

        return new TimberbornBuildingBurnoutConsequenceSummary(
            ConsideredDeltaCount: decisions.Count,
            MatchedBuildingCellCount: results.Count(static result => result.MatchedBuildingCell),
            AppliedConsequenceCount: results.Count(static result => result.AppliedConsequence));
    }
}

public sealed class NullTimberbornFireDebugVisualSink : ITimberbornFireDebugVisualSink
{
    public static readonly NullTimberbornFireDebugVisualSink Instance = new();

    private NullTimberbornFireDebugVisualSink()
    {
    }

    public void UpdateDebugVisualState(TimberbornFireDebugVisualCellState state)
    {
    }
}

public sealed class NullTimberbornFireVisualEffectSink : ITimberbornFireVisualEffectSink
{
    public static readonly NullTimberbornFireVisualEffectSink Instance = new();

    private NullTimberbornFireVisualEffectSink()
    {
    }

    public void UpdateVisualEffect(TimberbornFireVisualEffectEvent effectEvent)
    {
    }
}

public sealed class NullTimberbornFireGameplayConsequenceSink : ITimberbornFireGameplayConsequenceSink
{
    public static readonly NullTimberbornFireGameplayConsequenceSink Instance = new();

    private NullTimberbornFireGameplayConsequenceSink()
    {
    }

    public void ApplyConsequence(TimberbornFireGameplayConsequence consequence)
    {
    }
}

public sealed class NullTimberbornBuildingBurnoutConsequenceSink : ITimberbornBuildingBurnoutConsequenceSink
{
    public static readonly NullTimberbornBuildingBurnoutConsequenceSink Instance = new();

    private NullTimberbornBuildingBurnoutConsequenceSink()
    {
    }

    public TimberbornBuildingBurnoutConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        return TimberbornBuildingBurnoutConsequenceSummary.Empty;
    }
}

public sealed class NullTimberbornFireAlertSink : ITimberbornFireAlertSink
{
    public static readonly NullTimberbornFireAlertSink Instance = new();

    private NullTimberbornFireAlertSink()
    {
    }

    public void PublishAlert(TimberbornFireAlertEvent alertEvent)
    {
    }
}
