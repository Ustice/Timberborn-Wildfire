using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Consequences;

public readonly record struct TimberbornPowerInfrastructureFireConsequence(
    int CellIndex,
    uint Tick,
    int DamageUnits,
    int Heat)
{
    public bool ShouldApplyDamage => DamageUnits > 0;

    public static TimberbornPowerInfrastructureFireConsequence FromDecision(
        uint tick,
        TimberbornFireCellDeltaDecision decision)
    {
        return new TimberbornPowerInfrastructureFireConsequence(
            decision.CellIndex,
            tick,
            Math.Max(0, decision.OldFuel - decision.NewFuel),
            decision.NewHeat);
    }
}

public sealed record TimberbornPowerInfrastructureFireTarget(
    string StableId,
    string SpecId,
    int CellIndex,
    IReadOnlyList<TimberbornBurnDamageResourceStack> ConstructionResources,
    bool CanMarkDamaged,
    bool CanDisableOrDisconnect,
    bool RepairEligible);

public readonly record struct TimberbornPowerInfrastructureApplyResult(
    bool AppliedDamage,
    bool DisabledOrDisconnected,
    bool RepairEligible);

public readonly record struct TimberbornPowerInfrastructureFireSummary(
    int ConsideredDeltaCount,
    int MatchedTargetCellCount,
    int DuplicateTargetSuppressedCount,
    int MetalOnlyNoOpTargetCount,
    int DamagedTargetCount,
    int DisabledOrDisconnectedTargetCount,
    int RepairEligibleTargetCount,
    int TotalDamageApplied)
{
    public static readonly TimberbornPowerInfrastructureFireSummary Empty = new(
        ConsideredDeltaCount: 0,
        MatchedTargetCellCount: 0,
        DuplicateTargetSuppressedCount: 0,
        MetalOnlyNoOpTargetCount: 0,
        DamagedTargetCount: 0,
        DisabledOrDisconnectedTargetCount: 0,
        RepairEligibleTargetCount: 0,
        TotalDamageApplied: 0);

    public string ToLogToken(uint tick)
    {
        return "wildfire_timberborn_power_infrastructure_fire_applied " +
            $"tick={tick} " +
            $"considered_deltas={ConsideredDeltaCount} " +
            $"matched_target_cells={MatchedTargetCellCount} " +
            $"duplicate_targets_suppressed={DuplicateTargetSuppressedCount} " +
            $"metal_only_noop_targets={MetalOnlyNoOpTargetCount} " +
            $"damaged_targets={DamagedTargetCount} " +
            $"disabled_or_disconnected_targets={DisabledOrDisconnectedTargetCount} " +
            $"repair_eligible_targets={RepairEligibleTargetCount} " +
            $"total_damage_applied={TotalDamageApplied}";
    }
}

public interface ITimberbornPowerInfrastructureFireSink
{
    TimberbornPowerInfrastructureFireSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions);
}

public interface ITimberbornPowerInfrastructureFireTargetApi
{
    TimberbornPowerInfrastructureFireTarget? ResolveTarget(
        TimberbornPowerInfrastructureFireConsequence consequence);

    TimberbornPowerInfrastructureApplyResult ApplyDamage(
        TimberbornPowerInfrastructureFireTarget target,
        int damageApplied,
        bool isFullyDamaged);
}

public sealed class TimberbornPowerInfrastructureFireSink : ITimberbornPowerInfrastructureFireSink
{
    private readonly ITimberbornPowerInfrastructureFireTargetApi _targetApi;
    private readonly TimberbornBurnDamageCapacityCalculator _capacityCalculator;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly ITimberbornBurnDamageTargetStateProvider? _burnDamageTargets;
    private readonly Dictionary<string, PowerDamageState> _statesByStableId = new(StringComparer.Ordinal);

    public TimberbornPowerInfrastructureFireSink(
        ITimberbornPowerInfrastructureFireTargetApi targetApi,
        TimberbornBurnDamageCapacityCalculator? capacityCalculator = null,
        ITimberbornFireLogSink? logSink = null,
        ITimberbornBurnDamageTargetStateProvider? burnDamageTargets = null)
    {
        _targetApi = targetApi ?? throw new ArgumentNullException(nameof(targetApi));
        _capacityCalculator = capacityCalculator ?? new TimberbornBurnDamageCapacityCalculator();
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
        _burnDamageTargets = burnDamageTargets;
    }

    public TimberbornPowerInfrastructureFireSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        TimberbornPowerInfrastructureFireConsequence[] consequences = decisions
            .Select(decision => TimberbornPowerInfrastructureFireConsequence.FromDecision(tick, decision))
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
        PowerApplyOutcome[] outcomes = uniqueTargets
            .Select(ApplyTarget)
            .ToArray();

        TimberbornPowerInfrastructureFireSummary summary = new(
            ConsideredDeltaCount: consequences.Length,
            MatchedTargetCellCount: matchedTargets.Length,
            DuplicateTargetSuppressedCount: matchedTargets.Length - uniqueTargets.Length,
            MetalOnlyNoOpTargetCount: outcomes.Count(static outcome => outcome.IsMetalOnlyNoOp),
            DamagedTargetCount: outcomes.Count(static outcome => outcome.ApplyResult.AppliedDamage),
            DisabledOrDisconnectedTargetCount: outcomes.Count(static outcome => outcome.ApplyResult.DisabledOrDisconnected),
            RepairEligibleTargetCount: outcomes.Count(static outcome => outcome.ApplyResult.RepairEligible),
            TotalDamageApplied: outcomes.Sum(static outcome => outcome.DamageApplied));
        if (TimberbornReleaseLogNoisePolicy.ShouldLogConsequenceSummary(
            summary.MatchedTargetCellCount,
            summary.DamagedTargetCount,
            summary.DisabledOrDisconnectedTargetCount))
        {
            _logSink.Info(summary.ToLogToken(tick));
        }
        return summary;
    }

    private PowerApplyOutcome ApplyTarget(ResolvedTarget resolvedTarget)
    {
        TimberbornPowerInfrastructureFireTarget target = resolvedTarget.Target ??
            throw new InvalidOperationException("Resolved power infrastructure target cannot be null during application.");
        TimberbornBurnDamageTargetState? burnDamageState = resolvedTarget.BurnDamageState;
        TimberbornBurnDamageDescriptor descriptor = new(
            target.SpecId,
            TimberbornBurnDamageTargetKind.Infrastructure,
            target.ConstructionResources.Count == 0
                ? TimberbornBurnMaterialKind.NonBurnable
                : TimberbornBurnMaterialKind.Constructed,
            constructionResources: target.ConstructionResources);
        int damageCapacity = burnDamageState?.DamageCapacity ?? _capacityCalculator.Calculate(descriptor).Capacity;
        bool isMetalOnlyNoOp = damageCapacity == 0;
        if (isMetalOnlyNoOp)
        {
            return new PowerApplyOutcome(
                IsMetalOnlyNoOp: true,
                DamageApplied: 0,
                new TimberbornPowerInfrastructureApplyResult(
                    AppliedDamage: false,
                    DisabledOrDisconnected: false,
                    RepairEligible: target.RepairEligible));
        }

        PowerDamageState localState = _statesByStableId.GetValueOrDefault(target.StableId, new PowerDamageState(0));
        int damageTaken = burnDamageState?.DamageTaken ?? localState.DamageTaken;
        int damageApplied = burnDamageState is null
            ? Math.Min(resolvedTarget.Consequence.DamageUnits, Math.Max(0, damageCapacity - localState.DamageTaken))
            : Math.Min(resolvedTarget.Consequence.DamageUnits, resolvedTarget.AppliedEvent?.DamageApplied ?? 0);
        if (burnDamageState is null)
        {
            PowerDamageState nextState = new(localState.DamageTaken + damageApplied);
            _statesByStableId[target.StableId] = nextState;
            damageTaken = nextState.DamageTaken;
        }

        if (damageApplied > 0 && !target.CanMarkDamaged && !target.CanDisableOrDisconnect)
        {
            throw new InvalidOperationException(
                $"Power infrastructure damage mutation is unavailable for {target.StableId}.");
        }

        TimberbornPowerInfrastructureApplyResult applyResult = damageApplied <= 0
            ? new TimberbornPowerInfrastructureApplyResult(false, false, target.RepairEligible)
            : _targetApi.ApplyDamage(target, damageApplied, damageTaken >= damageCapacity);

        return new PowerApplyOutcome(
            IsMetalOnlyNoOp: false,
            DamageApplied: damageApplied,
            applyResult);
    }

    private ResolvedTarget ResolveTarget(TimberbornPowerInfrastructureFireConsequence consequence)
    {
        TimberbornPowerInfrastructureFireTarget? target = _targetApi.ResolveTarget(consequence);
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
        TimberbornPowerInfrastructureFireConsequence Consequence,
        TimberbornPowerInfrastructureFireTarget? Target,
        TimberbornBurnDamageTargetState? BurnDamageState,
        TimberbornBurnDamageAppliedEvent? AppliedEvent = null);

    private readonly record struct PowerDamageState(int DamageTaken);

    private readonly record struct PowerApplyOutcome(
        bool IsMetalOnlyNoOp,
        int DamageApplied,
        TimberbornPowerInfrastructureApplyResult ApplyResult);
}

public sealed class NullTimberbornPowerInfrastructureFireSink : ITimberbornPowerInfrastructureFireSink
{
    public static readonly NullTimberbornPowerInfrastructureFireSink Instance = new();

    private NullTimberbornPowerInfrastructureFireSink()
    {
    }

    public TimberbornPowerInfrastructureFireSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        return TimberbornPowerInfrastructureFireSummary.Empty;
    }
}

public sealed class TimberbornPowerInfrastructureFireTargetApi : ITimberbornPowerInfrastructureFireTargetApi
{
    private readonly FireGrid _grid;
    private readonly IBlockService _blockService;

    public TimberbornPowerInfrastructureFireTargetApi(FireGrid grid, IBlockService blockService)
    {
        _grid = grid;
        _blockService = blockService ?? throw new ArgumentNullException(nameof(blockService));
    }

    public TimberbornPowerInfrastructureFireTarget? ResolveTarget(
        TimberbornPowerInfrastructureFireConsequence consequence)
    {
        (int x, int y, int z) = _grid.FromIndex(consequence.CellIndex);
        Vector3Int coordinates = new(x, y, z);
        BlockObject? blockObject = _blockService
            .GetObjectsWithComponentAt<BlockObject>(coordinates)
            .Where(static candidate => IsPowerInfrastructureName(candidate.Name))
            .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
            .FirstOrDefault();
        if (blockObject is null)
        {
            return null;
        }

        return new TimberbornPowerInfrastructureFireTarget(
            StableId: $"power_infrastructure:{RuntimeHelpers.GetHashCode(blockObject)}",
            SpecId: blockObject.Name,
            CellIndex: consequence.CellIndex,
            ConstructionResources: TimberbornBurnDamageResourceGuesses.ForPowerInfrastructure(blockObject.Name),
            CanMarkDamaged: false,
            CanDisableOrDisconnect: false,
            RepairEligible: false);
    }

    public TimberbornPowerInfrastructureApplyResult ApplyDamage(
        TimberbornPowerInfrastructureFireTarget target,
        int damageApplied,
        bool isFullyDamaged)
    {
        throw new InvalidOperationException(
            $"Power infrastructure fire effect is not implemented for {target.SpecId}.");
    }

    private static bool IsPowerInfrastructureName(string name)
    {
        return name.Contains("Power", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Shaft", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Gear", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Mechanical", StringComparison.OrdinalIgnoreCase);
    }
}
