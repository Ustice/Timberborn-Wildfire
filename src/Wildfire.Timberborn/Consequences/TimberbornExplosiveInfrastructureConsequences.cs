using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using Timberborn.Explosions;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public readonly record struct TimberbornExplosiveInfrastructureConsequence(
    int CellIndex,
    uint Tick,
    int Heat,
    bool IsBurning)
{
    public bool ShouldExposeTarget => Heat > 0 || IsBurning;

    public static TimberbornExplosiveInfrastructureConsequence FromDecision(
        uint tick,
        TimberbornFireCellDeltaDecision decision)
    {
        return new TimberbornExplosiveInfrastructureConsequence(
            decision.CellIndex,
            tick,
            decision.NewHeat,
            decision.IsBurning);
    }
}

public sealed record TimberbornExplosiveInfrastructureTarget(
    string StableId,
    TimberbornExplosiveInfrastructureKind Kind,
    int CellIndex,
    int Depth,
    bool CanTriggerNative);

public enum TimberbornExplosiveInfrastructureKind
{
    Dynamite,
}

public enum TimberbornExplosiveInfrastructureNativeTriggerStatus
{
    SkippedNoSafeApi,
    SkippedAlreadyTriggered,
    Triggered,
}

public readonly record struct TimberbornExplosiveInfrastructureNativeTriggerResult(
    TimberbornExplosiveInfrastructureNativeTriggerStatus Status);

public readonly record struct TimberbornExplosiveInfrastructureConsequenceSettings(
    bool ExplosiveInfrastructureEnabled,
    bool NativeDynamiteTriggerEnabled,
    int ArmedThresholdTicks,
    int PulseHeat,
    int PulseRadius)
{
    public static readonly TimberbornExplosiveInfrastructureConsequenceSettings Disabled = new(
        ExplosiveInfrastructureEnabled: false,
        NativeDynamiteTriggerEnabled: false,
        ArmedThresholdTicks: WildfireReleaseSettings.DefaultExplosiveInfrastructureArmedThresholdTicks,
        PulseHeat: WildfireReleaseSettings.DefaultExplosiveInfrastructurePulseHeat,
        PulseRadius: WildfireReleaseSettings.DefaultExplosiveInfrastructurePulseRadius);

    public static TimberbornExplosiveInfrastructureConsequenceSettings FromSnapshot(
        WildfireReleaseSettingsSnapshot snapshot)
    {
        return new TimberbornExplosiveInfrastructureConsequenceSettings(
            snapshot.IsExplosiveInfrastructureEnabled,
            snapshot.IsNativeDynamiteTriggerEnabled,
            snapshot.ExplosiveInfrastructureArmedThresholdTicks,
            snapshot.ExplosiveInfrastructurePulseHeat,
            snapshot.ExplosiveInfrastructurePulseRadius);
    }
}

public readonly record struct TimberbornExplosiveInfrastructureConsequenceSummary(
    int ConsideredDeltaCount,
    int MatchedTargetCellCount,
    int DuplicateTargetSuppressedCount,
    int ArmedTargetCount,
    int TriggeredTargetCount,
    int NativeTriggeredTargetCount,
    int HeatPulseCellCount,
    int SkippedSettingDisabledCount,
    int SkippedNoSafeApiCount,
    int SkippedAlreadyTriggeredCount,
    int LastTriggeredDepth)
{
    public static readonly TimberbornExplosiveInfrastructureConsequenceSummary Empty = new(
        ConsideredDeltaCount: 0,
        MatchedTargetCellCount: 0,
        DuplicateTargetSuppressedCount: 0,
        ArmedTargetCount: 0,
        TriggeredTargetCount: 0,
        NativeTriggeredTargetCount: 0,
        HeatPulseCellCount: 0,
        SkippedSettingDisabledCount: 0,
        SkippedNoSafeApiCount: 0,
        SkippedAlreadyTriggeredCount: 0,
        LastTriggeredDepth: 0);

    public string ToLogToken(uint tick)
    {
        return "wildfire_timberborn_explosive_infrastructure_applied " +
            $"tick={tick} " +
            $"considered_deltas={ConsideredDeltaCount} " +
            $"matched_target_cells={MatchedTargetCellCount} " +
            $"duplicate_targets_suppressed={DuplicateTargetSuppressedCount} " +
            $"armed_targets={ArmedTargetCount} " +
            $"triggered_targets={TriggeredTargetCount} " +
            $"native_triggered_targets={NativeTriggeredTargetCount} " +
            $"heat_pulse_cells={HeatPulseCellCount} " +
            $"skipped_setting_disabled={SkippedSettingDisabledCount} " +
            $"skipped_no_safe_api={SkippedNoSafeApiCount} " +
            $"skipped_already_triggered={SkippedAlreadyTriggeredCount} " +
            $"last_triggered_depth={LastTriggeredDepth}";
    }
}

public interface ITimberbornExplosiveInfrastructureConsequenceSink
{
    TimberbornExplosiveInfrastructureConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions);
}

public interface ITimberbornExplosiveInfrastructureTargetApi
{
    TimberbornExplosiveInfrastructureTarget? ResolveTarget(
        TimberbornExplosiveInfrastructureConsequence consequence);

    TimberbornExplosiveInfrastructureNativeTriggerResult TriggerNative(
        TimberbornExplosiveInfrastructureTarget target,
        int delayTicks);
}

public interface ITimberbornExplosiveInfrastructureHeatPulseSink
{
    int EnqueueHeatPulse(
        TimberbornExplosiveInfrastructureTarget target,
        int pulseHeat,
        int pulseRadius);
}

public sealed class TimberbornExplosiveInfrastructureConsequenceSink :
    ITimberbornExplosiveInfrastructureConsequenceSink
{
    private readonly Func<TimberbornExplosiveInfrastructureConsequenceSettings> _settingsProvider;
    private readonly ITimberbornExplosiveInfrastructureTargetApi _targetApi;
    private readonly ITimberbornExplosiveInfrastructureHeatPulseSink _heatPulseSink;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly Dictionary<string, int> _exposureTicksByTarget = new(StringComparer.Ordinal);
    private readonly HashSet<string> _triggeredTargets = new(StringComparer.Ordinal);

    public TimberbornExplosiveInfrastructureConsequenceSink(
        Func<TimberbornExplosiveInfrastructureConsequenceSettings> settingsProvider,
        ITimberbornExplosiveInfrastructureTargetApi targetApi,
        ITimberbornExplosiveInfrastructureHeatPulseSink heatPulseSink,
        ITimberbornFireLogSink? logSink = null)
    {
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        _targetApi = targetApi ?? throw new ArgumentNullException(nameof(targetApi));
        _heatPulseSink = heatPulseSink ?? throw new ArgumentNullException(nameof(heatPulseSink));
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
    }

    public TimberbornExplosiveInfrastructureConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        TimberbornExplosiveInfrastructureConsequenceSettings settings = _settingsProvider();
        TimberbornExplosiveInfrastructureConsequence[] consequences = decisions
            .Select(decision => TimberbornExplosiveInfrastructureConsequence.FromDecision(tick, decision))
            .Where(static consequence => consequence.ShouldExposeTarget)
            .ToArray();

        if (!settings.ExplosiveInfrastructureEnabled)
        {
            TimberbornExplosiveInfrastructureConsequenceSummary disabledSummary = new(
                ConsideredDeltaCount: consequences.Length,
                MatchedTargetCellCount: 0,
                DuplicateTargetSuppressedCount: 0,
                ArmedTargetCount: 0,
                TriggeredTargetCount: 0,
                NativeTriggeredTargetCount: 0,
                HeatPulseCellCount: 0,
                SkippedSettingDisabledCount: consequences.Length,
                SkippedNoSafeApiCount: 0,
                SkippedAlreadyTriggeredCount: 0,
                LastTriggeredDepth: 0);
            _logSink.Info(disabledSummary.ToLogToken(tick));
            return disabledSummary;
        }

        ResolvedTarget[] resolvedTargets = consequences
            .Select(ResolveTargetSafely)
            .ToArray();
        int resolutionFailureCount = resolvedTargets.Count(static resolvedTarget =>
            resolvedTarget.ResolutionFailed);
        ResolvedTarget[] matchedTargets = resolvedTargets
            .Where(static resolvedTarget => resolvedTarget.Target is not null)
            .ToArray();
        ResolvedTarget[] uniqueTargets = matchedTargets
            .GroupBy(static resolvedTarget => resolvedTarget.Target!.StableId, StringComparer.Ordinal)
            .Select(static group => group
                .OrderByDescending(static resolvedTarget => resolvedTarget.Consequence.Heat)
                .ThenBy(static resolvedTarget => resolvedTarget.Consequence.CellIndex)
                .First())
            .ToArray();
        ResolvedTarget[] exposedTargets = uniqueTargets
            .GroupBy(static resolvedTarget => GetExposureKey(resolvedTarget.Target!), StringComparer.Ordinal)
            .Select(static group => group
                .OrderByDescending(static resolvedTarget => resolvedTarget.Consequence.Heat)
                .ThenBy(static resolvedTarget => resolvedTarget.Consequence.CellIndex)
                .First())
            .ToArray();
        TriggeredTargetResult[] triggeredResults = exposedTargets
            .Select(resolvedTarget => ApplyTarget(settings, resolvedTarget))
            .Where(static result => result.WasArmedThisTick || result.WasTriggeredThisTick)
            .ToArray();

        TimberbornExplosiveInfrastructureConsequenceSummary summary = new(
            ConsideredDeltaCount: consequences.Length,
            MatchedTargetCellCount: matchedTargets.Length,
            DuplicateTargetSuppressedCount: matchedTargets.Length - uniqueTargets.Length,
            ArmedTargetCount: triggeredResults.Count(static result => result.WasArmedThisTick),
            TriggeredTargetCount: triggeredResults.Count(static result => result.WasTriggeredThisTick),
            NativeTriggeredTargetCount: triggeredResults.Count(static result => result.NativeStatus ==
                TimberbornExplosiveInfrastructureNativeTriggerStatus.Triggered),
            HeatPulseCellCount: triggeredResults.Sum(static result => result.HeatPulseCellCount),
            SkippedSettingDisabledCount: 0,
            SkippedNoSafeApiCount: resolutionFailureCount + triggeredResults.Count(static result =>
                result.NativeStatus == TimberbornExplosiveInfrastructureNativeTriggerStatus.SkippedNoSafeApi),
            SkippedAlreadyTriggeredCount: triggeredResults.Count(static result => result.NativeStatus ==
                TimberbornExplosiveInfrastructureNativeTriggerStatus.SkippedAlreadyTriggered),
            LastTriggeredDepth: triggeredResults
                .Where(static result => result.WasTriggeredThisTick)
                .Select(static result => result.Depth)
                .DefaultIfEmpty(0)
                .Last());
        _logSink.Info(summary.ToLogToken(tick));
        return summary;
    }

    private ResolvedTarget ResolveTargetSafely(TimberbornExplosiveInfrastructureConsequence consequence)
    {
        try
        {
            return new ResolvedTarget(
                consequence,
                _targetApi.ResolveTarget(consequence),
                ResolutionFailed: false);
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                "wildfire_timberborn_explosive_infrastructure_safe_unavailable " +
                $"reason=resolve_target_failed cell_index={consequence.CellIndex} " +
                $"exception_type={exception.GetType().Name}");
            return new ResolvedTarget(
                consequence,
                Target: null,
                ResolutionFailed: true);
        }
    }

    private TriggeredTargetResult ApplyTarget(
        TimberbornExplosiveInfrastructureConsequenceSettings settings,
        ResolvedTarget resolvedTarget)
    {
        TimberbornExplosiveInfrastructureTarget target = resolvedTarget.Target ??
            throw new InvalidOperationException("Resolved explosive target cannot be null during application.");
        string exposureKey = GetExposureKey(target);
        if (_triggeredTargets.Contains(exposureKey))
        {
            return TriggeredTargetResult.NotTriggered(target.Depth);
        }

        int exposureTicks = _exposureTicksByTarget.GetValueOrDefault(exposureKey) + 1;
        _exposureTicksByTarget[exposureKey] = exposureTicks;
        if (exposureTicks < settings.ArmedThresholdTicks)
        {
            return TriggeredTargetResult.NotTriggered(target.Depth);
        }

        _triggeredTargets.Add(exposureKey);
        int heatPulseCellCount = _heatPulseSink.EnqueueHeatPulse(
            target,
            settings.PulseHeat,
            settings.PulseRadius);
        TimberbornExplosiveInfrastructureNativeTriggerStatus nativeStatus =
            TriggerNativeIfEnabled(settings, target);

        return new TriggeredTargetResult(
            WasArmedThisTick: true,
            WasTriggeredThisTick: true,
            NativeStatus: nativeStatus,
            HeatPulseCellCount: heatPulseCellCount,
            Depth: target.Depth);
    }

    private TimberbornExplosiveInfrastructureNativeTriggerStatus TriggerNativeIfEnabled(
        TimberbornExplosiveInfrastructureConsequenceSettings settings,
        TimberbornExplosiveInfrastructureTarget target)
    {
        if (!settings.NativeDynamiteTriggerEnabled || !target.CanTriggerNative)
        {
            return TimberbornExplosiveInfrastructureNativeTriggerStatus.SkippedNoSafeApi;
        }

        try
        {
            return _targetApi.TriggerNative(target, delayTicks: 1).Status;
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                "wildfire_timberborn_explosive_infrastructure_safe_unavailable " +
                $"reason=native_trigger_failed stable_id={target.StableId} " +
                $"exception_type={exception.GetType().Name}");
            return TimberbornExplosiveInfrastructureNativeTriggerStatus.SkippedNoSafeApi;
        }
    }

    private static string GetExposureKey(TimberbornExplosiveInfrastructureTarget target)
    {
        return $"{target.Kind}:{target.CellIndex}:{target.Depth}";
    }

    private readonly record struct ResolvedTarget(
        TimberbornExplosiveInfrastructureConsequence Consequence,
        TimberbornExplosiveInfrastructureTarget? Target,
        bool ResolutionFailed);

    private readonly record struct TriggeredTargetResult(
        bool WasArmedThisTick,
        bool WasTriggeredThisTick,
        TimberbornExplosiveInfrastructureNativeTriggerStatus NativeStatus,
        int HeatPulseCellCount,
        int Depth)
    {
        public static TriggeredTargetResult NotTriggered(int depth)
        {
            return new TriggeredTargetResult(
                WasArmedThisTick: false,
                WasTriggeredThisTick: false,
                NativeStatus: TimberbornExplosiveInfrastructureNativeTriggerStatus.SkippedNoSafeApi,
                HeatPulseCellCount: 0,
                Depth: depth);
        }
    }
}

public sealed class NullTimberbornExplosiveInfrastructureConsequenceSink :
    ITimberbornExplosiveInfrastructureConsequenceSink
{
    public static readonly NullTimberbornExplosiveInfrastructureConsequenceSink Instance = new();

    private NullTimberbornExplosiveInfrastructureConsequenceSink()
    {
    }

    public TimberbornExplosiveInfrastructureConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        return TimberbornExplosiveInfrastructureConsequenceSummary.Empty;
    }
}

public sealed class TimberbornQueuedFireSimHeatPulseSink : ITimberbornExplosiveInfrastructureHeatPulseSink
{
    private readonly FireGrid _grid;
    private TimberbornFireSystem? _fireSystem;

    public TimberbornQueuedFireSimHeatPulseSink(FireGrid grid)
    {
        _grid = grid;
    }

    public void Attach(TimberbornFireSystem fireSystem)
    {
        _fireSystem = fireSystem ?? throw new ArgumentNullException(nameof(fireSystem));
    }

    public void Detach()
    {
        _fireSystem = null;
    }

    public int EnqueueHeatPulse(
        TimberbornExplosiveInfrastructureTarget target,
        int pulseHeat,
        int pulseRadius)
    {
        if (_fireSystem is null || pulseHeat <= 0 || pulseRadius < 0)
        {
            return 0;
        }

        FireSimChange[] changes = CreatePulseChanges(_grid, target.CellIndex, pulseHeat, pulseRadius);
        changes
            .ToList()
            .ForEach(change => _fireSystem.RegisterChange(change, "explosive_infrastructure_heat_pulse", shouldLog: false));
        if (changes.Length > 0)
        {
            _fireSystem.LogRegisteredChanges("explosive_infrastructure_heat_pulse", changes.Length);
        }

        return changes.Length;
    }

    public static FireSimChange[] CreatePulseChanges(FireGrid grid, int originCellIndex, int pulseHeat, int pulseRadius)
    {
        (int originX, int originY, int originZ) = grid.FromIndex(originCellIndex);
        int radius = Math.Max(0, pulseRadius);
        byte heat = checked((byte)Math.Clamp(pulseHeat, 0, byte.MaxValue));

        return Enumerable.Range(-radius, checked((radius * 2) + 1))
            .SelectMany(dx => Enumerable.Range(-radius, checked((radius * 2) + 1))
                .SelectMany(dy => Enumerable.Range(-radius, checked((radius * 2) + 1))
                    .Select(dz => new { dx, dy, dz })))
            .Where(offset => Math.Abs(offset.dx) + Math.Abs(offset.dy) + Math.Abs(offset.dz) <= radius)
            .Select(offset => new
            {
                X = originX + offset.dx,
                Y = originY + offset.dy,
                Z = originZ + offset.dz,
            })
            .Where(coordinates =>
                coordinates.X >= 0 &&
                coordinates.Y >= 0 &&
                coordinates.Z >= 0 &&
                coordinates.X < grid.Width &&
                coordinates.Y < grid.Height &&
                coordinates.Z < grid.Depth)
            .Select(coordinates => new FireSimChange(
                grid.ToIndex(coordinates.X, coordinates.Y, coordinates.Z),
                AddHeat: heat))
            .ToArray();
    }
}

public sealed class TimberbornDynamiteExplosiveInfrastructureTargetApi :
    ITimberbornExplosiveInfrastructureTargetApi
{
    private readonly FireGrid _grid;
    private readonly IBlockService _blockService;
    private readonly Dictionary<string, Dynamite> _dynamitesByStableId = new(StringComparer.Ordinal);

    public TimberbornDynamiteExplosiveInfrastructureTargetApi(FireGrid grid, IBlockService blockService)
    {
        _grid = grid;
        _blockService = blockService ?? throw new ArgumentNullException(nameof(blockService));
    }

    public TimberbornExplosiveInfrastructureTarget? ResolveTarget(
        TimberbornExplosiveInfrastructureConsequence consequence)
    {
        (int x, int y, int z) = _grid.FromIndex(consequence.CellIndex);
        Vector3Int coordinates = new(x, y, z);
        Dynamite? dynamite = _blockService
            .GetObjectsWithComponentAt<Dynamite>(coordinates)
            .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
            .FirstOrDefault();

        if (dynamite is null)
        {
            return null;
        }

        string stableId = $"dynamite:{RuntimeHelpers.GetHashCode(dynamite)}";
        _dynamitesByStableId[stableId] = dynamite;

        return new TimberbornExplosiveInfrastructureTarget(
            stableId,
            TimberbornExplosiveInfrastructureKind.Dynamite,
            consequence.CellIndex,
            dynamite.Depth,
            CanTriggerNative: true);
    }

    public TimberbornExplosiveInfrastructureNativeTriggerResult TriggerNative(
        TimberbornExplosiveInfrastructureTarget target,
        int delayTicks)
    {
        if (!_dynamitesByStableId.TryGetValue(target.StableId, out Dynamite dynamite))
        {
            return new TimberbornExplosiveInfrastructureNativeTriggerResult(
                TimberbornExplosiveInfrastructureNativeTriggerStatus.SkippedNoSafeApi);
        }

        bool isTriggered;
        try
        {
            isTriggered = dynamite.IsTriggered;
        }
        catch
        {
            return new TimberbornExplosiveInfrastructureNativeTriggerResult(
                TimberbornExplosiveInfrastructureNativeTriggerStatus.SkippedNoSafeApi);
        }

        if (isTriggered)
        {
            return new TimberbornExplosiveInfrastructureNativeTriggerResult(
                TimberbornExplosiveInfrastructureNativeTriggerStatus.SkippedAlreadyTriggered);
        }

        try
        {
            dynamite.TriggerDelayed(Math.Max(1, delayTicks));
            return new TimberbornExplosiveInfrastructureNativeTriggerResult(
                TimberbornExplosiveInfrastructureNativeTriggerStatus.Triggered);
        }
        catch
        {
            return new TimberbornExplosiveInfrastructureNativeTriggerResult(
                TimberbornExplosiveInfrastructureNativeTriggerStatus.SkippedNoSafeApi);
        }
    }
}
