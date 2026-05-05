using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

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
    bool SkippedNoSafeApi,
    bool RepairEligible);

public readonly record struct TimberbornPowerInfrastructureFireSummary(
    int ConsideredDeltaCount,
    int MatchedTargetCellCount,
    int DuplicateTargetSuppressedCount,
    int MetalOnlyNoOpTargetCount,
    int DamagedTargetCount,
    int DisabledOrDisconnectedTargetCount,
    int SkippedNoSafeApiCount,
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
        SkippedNoSafeApiCount: 0,
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
            $"skipped_no_safe_api={SkippedNoSafeApiCount} " +
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
    private readonly Dictionary<string, PowerDamageState> _statesByStableId = new(StringComparer.Ordinal);

    public TimberbornPowerInfrastructureFireSink(
        ITimberbornPowerInfrastructureFireTargetApi targetApi,
        TimberbornBurnDamageCapacityCalculator? capacityCalculator = null,
        ITimberbornFireLogSink? logSink = null)
    {
        _targetApi = targetApi ?? throw new ArgumentNullException(nameof(targetApi));
        _capacityCalculator = capacityCalculator ?? new TimberbornBurnDamageCapacityCalculator();
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
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
            SkippedNoSafeApiCount: outcomes.Count(static outcome => outcome.ApplyResult.SkippedNoSafeApi),
            RepairEligibleTargetCount: outcomes.Count(static outcome => outcome.ApplyResult.RepairEligible),
            TotalDamageApplied: outcomes.Sum(static outcome => outcome.DamageApplied));
        _logSink.Info(summary.ToLogToken(tick));
        return summary;
    }

    private PowerApplyOutcome ApplyTarget(ResolvedTarget resolvedTarget)
    {
        TimberbornPowerInfrastructureFireTarget target = resolvedTarget.Target ??
            throw new InvalidOperationException("Resolved power infrastructure target cannot be null during application.");
        TimberbornBurnDamageDescriptor descriptor = new(
            target.SpecId,
            TimberbornBurnDamageTargetKind.Infrastructure,
            target.ConstructionResources.Count == 0
                ? TimberbornBurnMaterialKind.NonBurnable
                : TimberbornBurnMaterialKind.Constructed,
            constructionResources: target.ConstructionResources);
        TimberbornBurnDamageCapacity capacity = _capacityCalculator.Calculate(descriptor);
        bool isMetalOnlyNoOp = capacity.Capacity == 0;
        if (isMetalOnlyNoOp)
        {
            return new PowerApplyOutcome(
                IsMetalOnlyNoOp: true,
                DamageApplied: 0,
                new TimberbornPowerInfrastructureApplyResult(
                    AppliedDamage: false,
                    DisabledOrDisconnected: false,
                    SkippedNoSafeApi: false,
                    RepairEligible: target.RepairEligible));
        }

        PowerDamageState state = _statesByStableId.GetValueOrDefault(target.StableId, new PowerDamageState(0));
        int damageApplied = Math.Min(resolvedTarget.Consequence.DamageUnits, Math.Max(0, capacity.Capacity - state.DamageTaken));
        PowerDamageState nextState = new(state.DamageTaken + damageApplied);
        _statesByStableId[target.StableId] = nextState;
        TimberbornPowerInfrastructureApplyResult applyResult = damageApplied <= 0
            ? new TimberbornPowerInfrastructureApplyResult(false, false, false, target.RepairEligible)
            : _targetApi.ApplyDamage(target, damageApplied, nextState.DamageTaken >= capacity.Capacity);

        return new PowerApplyOutcome(
            IsMetalOnlyNoOp: false,
            DamageApplied: damageApplied,
            applyResult);
    }

    private readonly record struct ResolvedTarget(
        TimberbornPowerInfrastructureFireConsequence Consequence,
        TimberbornPowerInfrastructureFireTarget? Target);

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
            ConstructionResources: GuessConstructionResources(blockObject.Name),
            CanMarkDamaged: false,
            CanDisableOrDisconnect: false,
            RepairEligible: false);
    }

    public TimberbornPowerInfrastructureApplyResult ApplyDamage(
        TimberbornPowerInfrastructureFireTarget target,
        int damageApplied,
        bool isFullyDamaged)
    {
        return new TimberbornPowerInfrastructureApplyResult(
            AppliedDamage: false,
            DisabledOrDisconnected: false,
            SkippedNoSafeApi: true,
            RepairEligible: target.RepairEligible);
    }

    private static bool IsPowerInfrastructureName(string name)
    {
        return name.Contains("Power", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Shaft", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Gear", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Mechanical", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<TimberbornBurnDamageResourceStack> GuessConstructionResources(string name)
    {
        if (name.Contains("Metal", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { new TimberbornBurnDamageResourceStack("MetalBlock", 1) };
        }

        if (name.Contains("Shaft", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Gear", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Power", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Mechanical", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new TimberbornBurnDamageResourceStack("Log", 1),
                new TimberbornBurnDamageResourceStack("Plank", 1),
            };
        }

        return Array.Empty<TimberbornBurnDamageResourceStack>();
    }
}
