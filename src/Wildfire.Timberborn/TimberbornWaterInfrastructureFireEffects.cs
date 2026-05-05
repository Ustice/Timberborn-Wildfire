using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

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
    private readonly Dictionary<string, WaterDamageState> _statesByStableId = new(StringComparer.Ordinal);

    public TimberbornWaterInfrastructureFireSink(
        ITimberbornWaterInfrastructureFireTargetApi targetApi,
        TimberbornBurnDamageCapacityCalculator? capacityCalculator = null,
        ITimberbornFireLogSink? logSink = null)
    {
        _targetApi = targetApi ?? throw new ArgumentNullException(nameof(targetApi));
        _capacityCalculator = capacityCalculator ?? new TimberbornBurnDamageCapacityCalculator();
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
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
            .Select(consequence => new ResolvedTarget(consequence, _targetApi.ResolveTarget(consequence)))
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
        _logSink.Info(summary.ToLogToken(tick));
        return summary;
    }

    private WaterApplyOutcome ApplyTarget(ResolvedTarget resolvedTarget)
    {
        TimberbornWaterInfrastructureFireTarget target = resolvedTarget.Target ??
            throw new InvalidOperationException("Resolved water infrastructure target cannot be null during application.");
        TimberbornBurnDamageDescriptor descriptor = new(
            target.SpecId,
            TimberbornBurnDamageTargetKind.Infrastructure,
            target.ConstructionResources.Count == 0
                ? TimberbornBurnMaterialKind.NonBurnable
                : TimberbornBurnMaterialKind.Constructed,
            constructionResources: target.ConstructionResources);
        TimberbornBurnDamageCapacity capacity = _capacityCalculator.Calculate(descriptor);
        bool isInertMaterialNoOp = capacity.Capacity == 0;
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
                BurnableMaterialValue: capacity.Capacity,
                DamageApplied: 0,
                new TimberbornWaterInfrastructureApplyResult(
                    AppliedDamage: false,
                    AttemptedWaterStateMutation: false,
                    SkippedNoSafeApi: false,
                    RepairEligible: target.RepairEligible));
        }

        WaterDamageState state = _statesByStableId.GetValueOrDefault(target.StableId, new WaterDamageState(0));
        int damageApplied = Math.Min(effectiveDamageUnits, Math.Max(0, capacity.Capacity - state.DamageTaken));
        WaterDamageState nextState = new(state.DamageTaken + damageApplied);
        _statesByStableId[target.StableId] = nextState;
        TimberbornWaterInfrastructureApplyResult applyResult = damageApplied <= 0
            ? new TimberbornWaterInfrastructureApplyResult(false, false, false, target.RepairEligible)
            : _targetApi.ApplyDamage(target, damageApplied, nextState.DamageTaken >= capacity.Capacity);

        return new WaterApplyOutcome(
            IsInertMaterialNoOp: false,
            IsDifficultToBurnNoOp: false,
            BurnableMaterialValue: capacity.Capacity,
            DamageApplied: damageApplied,
            applyResult);
    }

    private readonly record struct ResolvedTarget(
        TimberbornWaterInfrastructureFireConsequence Consequence,
        TimberbornWaterInfrastructureFireTarget? Target);

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
            ConstructionResources: GuessConstructionResources(blockObject.Name),
            CanMarkDamaged: false,
            CanMutateWaterState: false,
            RepairEligible: false);
    }

    public TimberbornWaterInfrastructureApplyResult ApplyDamage(
        TimberbornWaterInfrastructureFireTarget target,
        int damageApplied,
        bool isFullyDamaged)
    {
        return new TimberbornWaterInfrastructureApplyResult(
            AppliedDamage: false,
            AttemptedWaterStateMutation: false,
            SkippedNoSafeApi: true,
            RepairEligible: target.RepairEligible);
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

    private static IReadOnlyList<TimberbornBurnDamageResourceStack> GuessConstructionResources(string name)
    {
        if (name.Contains("Metal", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Mechanical", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { new TimberbornBurnDamageResourceStack("MetalBlock", 1) };
        }

        if (name.Contains("Dam", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Levee", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Floodgate", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Valve", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Sluice", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Irrigation", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new TimberbornBurnDamageResourceStack("Log", 1),
                new TimberbornBurnDamageResourceStack("Plank", 1),
                new TimberbornBurnDamageResourceStack("Water", 1),
            };
        }

        return Array.Empty<TimberbornBurnDamageResourceStack>();
    }
}
