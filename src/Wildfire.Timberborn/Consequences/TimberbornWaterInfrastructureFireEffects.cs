using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Consequences;

public readonly record struct TimberbornWaterInfrastructureFireConsequence(
    int CellIndex,
    uint Tick,
    int DamageUnits,
    int Heat)
{
    public bool ShouldApplyDamage => DamageUnits > 0;

    public static TimberbornWaterInfrastructureFireConsequence FromDecision(
        uint tick,
        TimberbornFireCellDeltaDecision decision)
    {
        return new TimberbornWaterInfrastructureFireConsequence(
            decision.CellIndex,
            tick,
            Math.Max(0, decision.OldFuel - decision.NewFuel),
            decision.NewHeat);
    }
}

public sealed record TimberbornWaterInfrastructureFireTarget(
    string StableId,
    string SpecId,
    int CellIndex,
    IReadOnlyList<TimberbornBurnDamageResourceStack> ConstructionResources,
    bool CanMarkDamaged,
    bool CanMutateWaterState,
    bool RepairEligible);

public readonly record struct TimberbornWaterInfrastructureApplyResult(
    bool AppliedDamage,
    bool AttemptedWaterStateMutation,
    bool SkippedNoSafeApi,
    bool RepairEligible);

public readonly record struct TimberbornWaterInfrastructureFireSummary(
    int ConsideredDeltaCount,
    int MatchedTargetCellCount,
    int DuplicateTargetSuppressedCount,
    int InertMaterialNoOpTargetCount,
    int DifficultToBurnNoOpTargetCount,
    int BurnableMaterialValue,
    int DamagedTargetCount,
    int WaterStateMutationAttemptCount,
    int SkippedNoSafeApiCount,
    int RepairEligibleTargetCount,
    int TotalDamageApplied)
{
    public static readonly TimberbornWaterInfrastructureFireSummary Empty = new(
        ConsideredDeltaCount: 0,
        MatchedTargetCellCount: 0,
        DuplicateTargetSuppressedCount: 0,
        InertMaterialNoOpTargetCount: 0,
        DifficultToBurnNoOpTargetCount: 0,
        BurnableMaterialValue: 0,
        DamagedTargetCount: 0,
        WaterStateMutationAttemptCount: 0,
        SkippedNoSafeApiCount: 0,
        RepairEligibleTargetCount: 0,
        TotalDamageApplied: 0);

    public string ToLogToken(uint tick)
    {
        return "wildfire_timberborn_water_infrastructure_fire_applied " +
            $"tick={tick} " +
            $"considered_deltas={ConsideredDeltaCount} " +
            $"matched_target_cells={MatchedTargetCellCount} " +
            $"duplicate_targets_suppressed={DuplicateTargetSuppressedCount} " +
            $"inert_material_noop_targets={InertMaterialNoOpTargetCount} " +
            $"difficult_to_burn_noop_targets={DifficultToBurnNoOpTargetCount} " +
            $"burnable_material_value={BurnableMaterialValue} " +
            $"damaged_targets={DamagedTargetCount} " +
            $"water_state_mutation_attempts={WaterStateMutationAttemptCount} " +
            $"skipped_no_safe_api={SkippedNoSafeApiCount} " +
            $"repair_eligible_targets={RepairEligibleTargetCount} " +
            $"total_damage_applied={TotalDamageApplied}";
    }
}

public interface ITimberbornWaterInfrastructureFireSink
{
    TimberbornWaterInfrastructureFireSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions);
}

public interface ITimberbornWaterInfrastructureFireTargetApi
{
    TimberbornWaterInfrastructureFireTarget? ResolveTarget(
        TimberbornWaterInfrastructureFireConsequence consequence);

    TimberbornWaterInfrastructureApplyResult ApplyDamage(
        TimberbornWaterInfrastructureFireTarget target,
        int damageApplied,
        bool isFullyDamaged);
}

public sealed class TimberbornWaterInfrastructureFireSink : ITimberbornWaterInfrastructureFireSink
{
    private const int DifficultToBurnDamageResistance = 3;

    private readonly ITimberbornWaterInfrastructureFireTargetApi _targetApi;
    private readonly TimberbornBurnDamageCapacityCalculator _capacityCalculator;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly ITimberbornBurnDamageTargetStateProvider? _burnDamageTargets;
    private readonly Dictionary<string, WaterDamageState> _statesByStableId = new(StringComparer.Ordinal);

    public TimberbornWaterInfrastructureFireSink(
        ITimberbornWaterInfrastructureFireTargetApi targetApi,
        TimberbornBurnDamageCapacityCalculator? capacityCalculator = null,
        ITimberbornFireLogSink? logSink = null,
        ITimberbornBurnDamageTargetStateProvider? burnDamageTargets = null)
    {
        _targetApi = targetApi ?? throw new ArgumentNullException(nameof(targetApi));
        _capacityCalculator = capacityCalculator ?? new TimberbornBurnDamageCapacityCalculator();
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
        _burnDamageTargets = burnDamageTargets;
    }

    public TimberbornWaterInfrastructureFireSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        TimberbornWaterInfrastructureFireConsequence[] consequences = decisions
            .Select(decision => TimberbornWaterInfrastructureFireConsequence.FromDecision(tick, decision))
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
        WaterApplyOutcome[] outcomes = uniqueTargets
            .Select(ApplyTarget)
            .ToArray();

        TimberbornWaterInfrastructureFireSummary summary = new(
            ConsideredDeltaCount: consequences.Length,
            MatchedTargetCellCount: matchedTargets.Length,
            DuplicateTargetSuppressedCount: matchedTargets.Length - uniqueTargets.Length,
            InertMaterialNoOpTargetCount: outcomes.Count(static outcome => outcome.IsInertMaterialNoOp),
            DifficultToBurnNoOpTargetCount: outcomes.Count(static outcome => outcome.IsDifficultToBurnNoOp),
            BurnableMaterialValue: outcomes.Sum(static outcome => outcome.BurnableMaterialValue),
            DamagedTargetCount: outcomes.Count(static outcome => outcome.ApplyResult.AppliedDamage),
            WaterStateMutationAttemptCount: outcomes.Count(static outcome => outcome.ApplyResult.AttemptedWaterStateMutation),
            SkippedNoSafeApiCount: outcomes.Count(static outcome => outcome.ApplyResult.SkippedNoSafeApi),
            RepairEligibleTargetCount: outcomes.Count(static outcome => outcome.ApplyResult.RepairEligible),
            TotalDamageApplied: outcomes.Sum(static outcome => outcome.DamageApplied));
        if (TimberbornReleaseLogNoisePolicy.ShouldLogConsequenceSummary(
            summary.MatchedTargetCellCount,
            summary.DamagedTargetCount,
            summary.WaterStateMutationAttemptCount,
            summary.SkippedNoSafeApiCount))
        {
            _logSink.Info(summary.ToLogToken(tick));
        }
        return summary;
    }

    private WaterApplyOutcome ApplyTarget(ResolvedTarget resolvedTarget)
    {
        TimberbornWaterInfrastructureFireTarget target = resolvedTarget.Target ??
            throw new InvalidOperationException("Resolved water infrastructure target cannot be null during application.");
        TimberbornBurnDamageTargetState? burnDamageState = resolvedTarget.BurnDamageState;
        TimberbornBurnDamageDescriptor descriptor = new(
            target.SpecId,
            TimberbornBurnDamageTargetKind.Infrastructure,
            target.ConstructionResources.Count == 0
                ? TimberbornBurnMaterialKind.NonBurnable
                : TimberbornBurnMaterialKind.Constructed,
            constructionResources: target.ConstructionResources);
        int damageCapacity = burnDamageState?.DamageCapacity ?? _capacityCalculator.Calculate(descriptor).Capacity;
        bool isInertMaterialNoOp = damageCapacity == 0;
        if (isInertMaterialNoOp)
        {
            return new WaterApplyOutcome(
                IsInertMaterialNoOp: true,
                IsDifficultToBurnNoOp: false,
                BurnableMaterialValue: 0,
                DamageApplied: 0,
                new TimberbornWaterInfrastructureApplyResult(
                    AppliedDamage: false,
                    AttemptedWaterStateMutation: false,
                    SkippedNoSafeApi: false,
                    RepairEligible: target.RepairEligible));
        }

        int effectiveDamageUnits = Math.Max(0, resolvedTarget.Consequence.DamageUnits - DifficultToBurnDamageResistance);
        if (effectiveDamageUnits == 0)
        {
            return new WaterApplyOutcome(
                IsInertMaterialNoOp: false,
                IsDifficultToBurnNoOp: true,
                BurnableMaterialValue: damageCapacity,
                DamageApplied: 0,
                new TimberbornWaterInfrastructureApplyResult(
                    AppliedDamage: false,
                    AttemptedWaterStateMutation: false,
                    SkippedNoSafeApi: false,
                    RepairEligible: target.RepairEligible));
        }

        WaterDamageState localState = _statesByStableId.GetValueOrDefault(target.StableId, new WaterDamageState(0));
        int damageTaken = burnDamageState?.DamageTaken ?? localState.DamageTaken;
        int damageApplied = burnDamageState is null
            ? Math.Min(effectiveDamageUnits, Math.Max(0, damageCapacity - localState.DamageTaken))
            : Math.Min(effectiveDamageUnits, resolvedTarget.AppliedEvent?.DamageApplied ?? 0);
        if (burnDamageState is null)
        {
            WaterDamageState nextState = new(localState.DamageTaken + damageApplied);
            _statesByStableId[target.StableId] = nextState;
            damageTaken = nextState.DamageTaken;
        }

        TimberbornWaterInfrastructureApplyResult applyResult = damageApplied <= 0
            ? new TimberbornWaterInfrastructureApplyResult(false, false, false, target.RepairEligible)
            : _targetApi.ApplyDamage(target, damageApplied, damageTaken >= damageCapacity);

        return new WaterApplyOutcome(
            IsInertMaterialNoOp: false,
            IsDifficultToBurnNoOp: false,
            BurnableMaterialValue: damageCapacity,
            DamageApplied: damageApplied,
            applyResult);
    }

    private ResolvedTarget ResolveTarget(TimberbornWaterInfrastructureFireConsequence consequence)
    {
        TimberbornWaterInfrastructureFireTarget? target = _targetApi.ResolveTarget(consequence);
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
        TimberbornWaterInfrastructureFireConsequence Consequence,
        TimberbornWaterInfrastructureFireTarget? Target,
        TimberbornBurnDamageTargetState? BurnDamageState,
        TimberbornBurnDamageAppliedEvent? AppliedEvent = null);

    private readonly record struct WaterDamageState(int DamageTaken);

    private readonly record struct WaterApplyOutcome(
        bool IsInertMaterialNoOp,
        bool IsDifficultToBurnNoOp,
        int BurnableMaterialValue,
        int DamageApplied,
        TimberbornWaterInfrastructureApplyResult ApplyResult);
}

public sealed class NullTimberbornWaterInfrastructureFireSink : ITimberbornWaterInfrastructureFireSink
{
    public static readonly NullTimberbornWaterInfrastructureFireSink Instance = new();

    private NullTimberbornWaterInfrastructureFireSink()
    {
    }

    public TimberbornWaterInfrastructureFireSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        return TimberbornWaterInfrastructureFireSummary.Empty;
    }
}

public sealed class TimberbornWaterInfrastructureFireTargetApi : ITimberbornWaterInfrastructureFireTargetApi
{
    private readonly FireGrid _grid;
    private readonly IBlockService _blockService;

    public TimberbornWaterInfrastructureFireTargetApi(FireGrid grid, IBlockService blockService)
    {
        _grid = grid;
        _blockService = blockService ?? throw new ArgumentNullException(nameof(blockService));
    }

    public TimberbornWaterInfrastructureFireTarget? ResolveTarget(
        TimberbornWaterInfrastructureFireConsequence consequence)
    {
        (int x, int y, int z) = _grid.FromIndex(consequence.CellIndex);
        Vector3Int coordinates = new(x, y, z);
        BlockObject? blockObject = _blockService
            .GetObjectsWithComponentAt<BlockObject>(coordinates)
            .Where(static candidate => IsWaterInfrastructureName(candidate.Name))
            .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
            .FirstOrDefault();
        if (blockObject is null)
        {
            return null;
        }

        return new TimberbornWaterInfrastructureFireTarget(
            StableId: $"water_infrastructure:{RuntimeHelpers.GetHashCode(blockObject)}",
            SpecId: blockObject.Name,
            CellIndex: consequence.CellIndex,
            ConstructionResources: TimberbornBurnDamageResourceGuesses.ForWaterInfrastructure(blockObject.Name),
            CanMarkDamaged: false,
            CanMutateWaterState: false,
            RepairEligible: false);
    }

    public TimberbornWaterInfrastructureApplyResult ApplyDamage(
        TimberbornWaterInfrastructureFireTarget target,
        int damageApplied,
        bool isFullyDamaged)
    {
        throw new InvalidOperationException(
            $"Water infrastructure fire effect is not implemented for {target.SpecId}.");
    }

    private static bool IsWaterInfrastructureName(string name)
    {
        return name.Contains("Dam", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Levee", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Floodgate", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Valve", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Sluice", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Water", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Irrigation", StringComparison.OrdinalIgnoreCase);
    }
}
