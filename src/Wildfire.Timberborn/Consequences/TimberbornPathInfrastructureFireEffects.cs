using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Consequences;

public readonly record struct TimberbornPathInfrastructureFireConsequence(
    int CellIndex,
    uint Tick,
    int DamageUnits,
    int Heat)
{
    public bool ShouldApplyDamage => DamageUnits > 0;

    public static TimberbornPathInfrastructureFireConsequence FromDecision(
        uint tick,
        TimberbornFireCellDeltaDecision decision)
    {
        return new TimberbornPathInfrastructureFireConsequence(
            decision.CellIndex,
            tick,
            Math.Max(0, decision.OldFuel - decision.NewFuel),
            decision.NewHeat);
    }
}

public sealed record TimberbornPathInfrastructureFireTarget(
    string StableId,
    string SpecId,
    int CellIndex,
    IReadOnlyList<TimberbornBurnDamageResourceStack> ConstructionResources,
    bool CanMarkDamaged,
    bool CanBlockPath,
    bool RepairEligible);

public readonly record struct TimberbornPathInfrastructureApplyResult(
    bool AppliedDamage,
    bool AppliedBlock,
    bool SkippedUnavailablePath,
    bool RepairEligible);

public readonly record struct TimberbornPathInfrastructureFireSummary(
    int ConsideredDeltaCount,
    int MatchedTargetCellCount,
    int DuplicateTargetSuppressedCount,
    int ZeroCostPathTargetCount,
    int DamagedTargetCount,
    int BlockedTargetCount,
    int SkippedUnavailablePathCount,
    int RepairEligibleTargetCount,
    int TotalDamageApplied)
{
    public static readonly TimberbornPathInfrastructureFireSummary Empty = new(
        ConsideredDeltaCount: 0,
        MatchedTargetCellCount: 0,
        DuplicateTargetSuppressedCount: 0,
        ZeroCostPathTargetCount: 0,
        DamagedTargetCount: 0,
        BlockedTargetCount: 0,
        SkippedUnavailablePathCount: 0,
        RepairEligibleTargetCount: 0,
        TotalDamageApplied: 0);

    public string ToLogToken(uint tick)
    {
        return "wildfire_timberborn_path_infrastructure_fire_applied " +
            $"tick={tick} " +
            $"considered_deltas={ConsideredDeltaCount} " +
            $"matched_target_cells={MatchedTargetCellCount} " +
            $"duplicate_targets_suppressed={DuplicateTargetSuppressedCount} " +
            $"zero_cost_path_targets={ZeroCostPathTargetCount} " +
            $"damaged_targets={DamagedTargetCount} " +
            $"blocked_targets={BlockedTargetCount} " +
            $"repair_eligible_targets={RepairEligibleTargetCount} " +
            $"total_damage_applied={TotalDamageApplied}";
    }
}

public interface ITimberbornPathInfrastructureFireSink
{
    TimberbornPathInfrastructureFireSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions);
}

public interface ITimberbornPathInfrastructureFireTargetApi
{
    TimberbornPathInfrastructureFireTarget? ResolveTarget(
        TimberbornPathInfrastructureFireConsequence consequence);

    TimberbornPathInfrastructureApplyResult ApplyDamage(
        TimberbornPathInfrastructureFireTarget target,
        int damageApplied,
        bool isFullyDamaged);
}

public sealed class TimberbornPathInfrastructureFireSink : ITimberbornPathInfrastructureFireSink
{
    private readonly ITimberbornPathInfrastructureFireTargetApi _targetApi;
    private readonly TimberbornBurnDamageCapacityCalculator _capacityCalculator;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly ITimberbornBurnDamageTargetStateProvider? _burnDamageTargets;
    private readonly Dictionary<string, PathDamageState> _statesByStableId = new(StringComparer.Ordinal);

    public TimberbornPathInfrastructureFireSink(
        ITimberbornPathInfrastructureFireTargetApi targetApi,
        TimberbornBurnDamageCapacityCalculator? capacityCalculator = null,
        ITimberbornFireLogSink? logSink = null,
        ITimberbornBurnDamageTargetStateProvider? burnDamageTargets = null)
    {
        _targetApi = targetApi ?? throw new ArgumentNullException(nameof(targetApi));
        _capacityCalculator = capacityCalculator ?? new TimberbornBurnDamageCapacityCalculator();
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
        _burnDamageTargets = burnDamageTargets;
    }

    public TimberbornPathInfrastructureFireSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        TimberbornPathInfrastructureFireConsequence[] consequences = decisions
            .Select(decision => TimberbornPathInfrastructureFireConsequence.FromDecision(tick, decision))
            .Where(static consequence => consequence.ShouldApplyDamage)
            .ToArray();
        ResolvedTarget[] matchedTargets = consequences
            .Select(ResolveTarget)
            .Where(static resolvedTarget => resolvedTarget.Target is not null)
            .ToArray();
        ResolvedTarget[] uniqueTargets = matchedTargets
            .GroupBy(static resolvedTarget => resolvedTarget.Target!.StableId, StringComparer.Ordinal)
            .Select(static group => group
                .OrderByDescending(static resolvedTarget => resolvedTarget.Consequence.DamageUnits)
                .ThenByDescending(static resolvedTarget => resolvedTarget.Consequence.Heat)
                .ThenBy(static resolvedTarget => resolvedTarget.Consequence.CellIndex)
                .First())
            .ToArray();
        PathApplyOutcome[] outcomes = uniqueTargets
            .Select(ApplyTarget)
            .ToArray();

        TimberbornPathInfrastructureFireSummary summary = new(
            ConsideredDeltaCount: consequences.Length,
            MatchedTargetCellCount: matchedTargets.Length,
            DuplicateTargetSuppressedCount: matchedTargets.Length - uniqueTargets.Length,
            ZeroCostPathTargetCount: outcomes.Count(static outcome => outcome.IsZeroCostPath),
            DamagedTargetCount: outcomes.Count(static outcome => outcome.ApplyResult.AppliedDamage),
            BlockedTargetCount: outcomes.Count(static outcome => outcome.ApplyResult.AppliedBlock),
            SkippedUnavailablePathCount: outcomes.Count(static outcome => outcome.ApplyResult.SkippedUnavailablePath),
            RepairEligibleTargetCount: outcomes.Count(static outcome => outcome.ApplyResult.RepairEligible),
            TotalDamageApplied: outcomes.Sum(static outcome => outcome.DamageApplied));
        if (TimberbornReleaseLogNoisePolicy.ShouldLogConsequenceSummary(
            summary.MatchedTargetCellCount,
            summary.DamagedTargetCount,
            summary.BlockedTargetCount,
            summary.SkippedUnavailablePathCount))
        {
            _logSink.Info(summary.ToLogToken(tick));
        }
        return summary;
    }

    private PathApplyOutcome ApplyTarget(ResolvedTarget resolvedTarget)
    {
        TimberbornPathInfrastructureFireTarget target = resolvedTarget.Target ??
            throw new InvalidOperationException("Resolved path infrastructure target cannot be null during application.");
        TimberbornBurnDamageTargetState? burnDamageState = resolvedTarget.BurnDamageState;
        TimberbornBurnDamageDescriptor descriptor = new(
            target.SpecId,
            TimberbornBurnDamageTargetKind.Infrastructure,
            target.ConstructionResources.Count == 0
                ? TimberbornBurnMaterialKind.NonBurnable
                : TimberbornBurnMaterialKind.Constructed,
            constructionResources: target.ConstructionResources);
        int damageCapacity = burnDamageState?.DamageCapacity ?? _capacityCalculator.Calculate(descriptor).Capacity;
        bool isZeroCostPath = damageCapacity == 0;
        if (isZeroCostPath)
        {
            return new PathApplyOutcome(
                IsZeroCostPath: true,
                DamageApplied: 0,
                new TimberbornPathInfrastructureApplyResult(
                    AppliedDamage: false,
                    AppliedBlock: false,
                    SkippedUnavailablePath: false,
                    RepairEligible: target.RepairEligible));
        }

        PathDamageState localState = _statesByStableId.GetValueOrDefault(target.StableId, new PathDamageState(0));
        int damageTaken = burnDamageState?.DamageTaken ?? localState.DamageTaken;
        int damageApplied = burnDamageState is null
            ? Math.Min(resolvedTarget.Consequence.DamageUnits, Math.Max(0, damageCapacity - localState.DamageTaken))
            : Math.Min(resolvedTarget.Consequence.DamageUnits, resolvedTarget.AppliedEvent?.DamageApplied ?? 0);
        if (burnDamageState is null)
        {
            PathDamageState nextState = new(localState.DamageTaken + damageApplied);
            _statesByStableId[target.StableId] = nextState;
            damageTaken = nextState.DamageTaken;
        }

        TimberbornPathInfrastructureApplyResult applyResult = damageApplied <= 0
            ? new TimberbornPathInfrastructureApplyResult(false, false, false, target.RepairEligible)
            : _targetApi.ApplyDamage(target, damageApplied, damageTaken >= damageCapacity);

        return new PathApplyOutcome(
            IsZeroCostPath: false,
            DamageApplied: damageApplied,
            applyResult);
    }

    private ResolvedTarget ResolveTarget(TimberbornPathInfrastructureFireConsequence consequence)
    {
        TimberbornPathInfrastructureFireTarget? target = _targetApi.ResolveTarget(consequence);
        if (_burnDamageTargets is null)
        {
            return new ResolvedTarget(consequence, target, BurnDamageState: null);
        }

        if (!_burnDamageTargets.TryGetStateForCell(consequence.CellIndex, out TimberbornBurnDamageTargetState state) ||
            state.TargetKind != TimberbornBurnDamageTargetKind.Infrastructure ||
            target is null ||
            !string.Equals(target.StableId, state.TargetKey.StableId, StringComparison.Ordinal))
        {
            return new ResolvedTarget(consequence, Target: null, BurnDamageState: null);
        }

        bool hasAppliedEvent = _burnDamageTargets.TryGetAppliedEvent(
            state.TargetKey,
            out TimberbornBurnDamageAppliedEvent appliedEvent);
        TimberbornBurnDamageAppliedEvent? currentTickEvent = hasAppliedEvent && appliedEvent.Tick == consequence.Tick
            ? appliedEvent
            : null;

        return new ResolvedTarget(consequence, target, state, currentTickEvent);
    }

    private readonly record struct ResolvedTarget(
        TimberbornPathInfrastructureFireConsequence Consequence,
        TimberbornPathInfrastructureFireTarget? Target,
        TimberbornBurnDamageTargetState? BurnDamageState,
        TimberbornBurnDamageAppliedEvent? AppliedEvent = null);

    private readonly record struct PathDamageState(int DamageTaken);

    private readonly record struct PathApplyOutcome(
        bool IsZeroCostPath,
        int DamageApplied,
        TimberbornPathInfrastructureApplyResult ApplyResult);
}

public sealed class NullTimberbornPathInfrastructureFireSink : ITimberbornPathInfrastructureFireSink
{
    public static readonly NullTimberbornPathInfrastructureFireSink Instance = new();

    private NullTimberbornPathInfrastructureFireSink()
    {
    }

    public TimberbornPathInfrastructureFireSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        return TimberbornPathInfrastructureFireSummary.Empty;
    }
}

public sealed class TimberbornPathInfrastructureFireTargetApi : ITimberbornPathInfrastructureFireTargetApi
{
    private readonly FireGrid _grid;
    private readonly IBlockService _blockService;

    public TimberbornPathInfrastructureFireTargetApi(FireGrid grid, IBlockService blockService)
    {
        _grid = grid;
        _blockService = blockService ?? throw new ArgumentNullException(nameof(blockService));
    }

    public TimberbornPathInfrastructureFireTarget? ResolveTarget(
        TimberbornPathInfrastructureFireConsequence consequence)
    {
        (int x, int y, int z) = _grid.FromIndex(consequence.CellIndex);
        Vector3Int coordinates = new(x, y, z);
        BlockObject? blockObject = _blockService
            .GetObjectsWithComponentAt<BlockObject>(coordinates)
            .Where(static candidate => IsPathInfrastructureName(candidate.Name))
            .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
            .FirstOrDefault();
        if (blockObject is null)
        {
            return null;
        }

        return new TimberbornPathInfrastructureFireTarget(
            StableId: $"path_infrastructure:{RuntimeHelpers.GetHashCode(blockObject)}",
            SpecId: blockObject.Name,
            CellIndex: consequence.CellIndex,
            ConstructionResources: TimberbornBurnDamageResourceGuesses.ForPathInfrastructure(blockObject.Name),
            CanMarkDamaged: false,
            CanBlockPath: false,
            RepairEligible: false);
    }

    public TimberbornPathInfrastructureApplyResult ApplyDamage(
        TimberbornPathInfrastructureFireTarget target,
        int damageApplied,
        bool isFullyDamaged)
    {
        throw new InvalidOperationException(
            $"Path infrastructure fire effect is not implemented for {target.SpecId}.");
    }

    private static bool IsPathInfrastructureName(string name)
    {
        return name.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Platform", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Bridge", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Stair", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Overhang", StringComparison.OrdinalIgnoreCase);
    }
}
