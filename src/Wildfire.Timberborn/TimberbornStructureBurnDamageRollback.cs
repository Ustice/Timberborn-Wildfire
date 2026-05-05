using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public enum TimberbornStructureBurnRollbackStage
{
    None,
    Scorched,
    PartialConstruction,
    Unfinished,
}

public readonly record struct TimberbornStructureBurnDamageConsequence(
    int CellIndex,
    uint Tick,
    int DamageUnits,
    int Fuel,
    int Heat,
    int Water)
{
    private const int DangerousHeat = 8;
    private const int RepairBlockedHeat = 5;

    public bool HasBurnPressure => true;

    public bool ShouldClose => Fuel > 0 || Heat >= DangerousHeat;

    public bool ShouldBlockRepair => Fuel > 0 || Heat >= RepairBlockedHeat;

    public static TimberbornStructureBurnDamageConsequence FromDecision(
        uint tick,
        TimberbornFireCellDeltaDecision decision)
    {
        return new TimberbornStructureBurnDamageConsequence(
            decision.CellIndex,
            tick,
            Math.Max(0, decision.OldFuel - decision.NewFuel),
            decision.NewFuel,
            decision.NewHeat,
            decision.NewWater);
    }
}

public sealed record TimberbornStructureBurnDamageTarget(
    string StableId,
    string SpecId,
    int CellIndex,
    IReadOnlyList<TimberbornBurnDamageResourceStack> ConstructionResources,
    bool CanClose,
    bool CanApplyRollbackVisual,
    bool CanRepairAfterDanger);

public readonly record struct TimberbornStructureBurnDamageApplyRequest(
    int DamageApplied,
    int DamageTaken,
    int DamageCapacity,
    TimberbornStructureBurnRollbackStage RollbackStage,
    bool ShouldClose,
    bool RepairBlocked,
    bool RepairEligible,
    bool ShouldApplyRollbackVisual);

public readonly record struct TimberbornStructureBurnDamageApplyResult(
    bool Closed,
    bool VisualRollbackApplied,
    bool SkippedNoSafeApi,
    bool RepairEligible);

public readonly record struct TimberbornStructureBurnDamageRollbackSummary(
    int ConsideredDeltaCount,
    int MatchedStructureCellCount,
    int DuplicateStructureTargetSuppressedCount,
    int ZeroBurnableCapacityTargetCount,
    int MaterialValueLost,
    int ClosedStructureCount,
    int RepairBlockedCount,
    int RepairEligibleCount,
    int ScorchedStageCount,
    int PartialConstructionStageCount,
    int UnfinishedStageCount,
    int VisualRollbackAppliedCount,
    int SkippedNoSafeApiCount,
    int TotalDamageApplied)
{
    public static readonly TimberbornStructureBurnDamageRollbackSummary Empty = new(
        ConsideredDeltaCount: 0,
        MatchedStructureCellCount: 0,
        DuplicateStructureTargetSuppressedCount: 0,
        ZeroBurnableCapacityTargetCount: 0,
        MaterialValueLost: 0,
        ClosedStructureCount: 0,
        RepairBlockedCount: 0,
        RepairEligibleCount: 0,
        ScorchedStageCount: 0,
        PartialConstructionStageCount: 0,
        UnfinishedStageCount: 0,
        VisualRollbackAppliedCount: 0,
        SkippedNoSafeApiCount: 0,
        TotalDamageApplied: 0);

    public string ToLogToken(uint tick)
    {
        return "wildfire_timberborn_structure_burn_damage_rollback_applied " +
            $"tick={tick} " +
            $"considered_deltas={ConsideredDeltaCount} " +
            $"matched_structure_cells={MatchedStructureCellCount} " +
            $"duplicate_structure_targets_suppressed={DuplicateStructureTargetSuppressedCount} " +
            $"zero_burnable_capacity_targets={ZeroBurnableCapacityTargetCount} " +
            $"material_value_lost={MaterialValueLost} " +
            $"closed_structures={ClosedStructureCount} " +
            $"repair_blocked={RepairBlockedCount} " +
            $"repair_eligible={RepairEligibleCount} " +
            $"rollback_stage_scorched={ScorchedStageCount} " +
            $"rollback_stage_partial_construction={PartialConstructionStageCount} " +
            $"rollback_stage_unfinished={UnfinishedStageCount} " +
            $"visual_rollback_applied={VisualRollbackAppliedCount} " +
            $"skipped_no_safe_api={SkippedNoSafeApiCount} " +
            $"total_damage_applied={TotalDamageApplied}";
    }
}

public interface ITimberbornStructureBurnDamageRollbackSink
{
    TimberbornStructureBurnDamageRollbackSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions);
}

public interface ITimberbornStructureBurnDamageRollbackTargetApi
{
    TimberbornStructureBurnDamageTarget? ResolveTarget(
        TimberbornStructureBurnDamageConsequence consequence);

    TimberbornStructureBurnDamageApplyResult ApplyState(
        TimberbornStructureBurnDamageTarget target,
        TimberbornStructureBurnDamageApplyRequest request);
}

public sealed class TimberbornStructureBurnDamageRollbackSink : ITimberbornStructureBurnDamageRollbackSink
{
    private readonly ITimberbornStructureBurnDamageRollbackTargetApi _targetApi;
    private readonly TimberbornBurnDamageCapacityCalculator _capacityCalculator;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly Dictionary<string, StructureDamageState> _statesByStableId = new(StringComparer.Ordinal);

    public TimberbornStructureBurnDamageRollbackSink(
        ITimberbornStructureBurnDamageRollbackTargetApi targetApi,
        TimberbornBurnDamageCapacityCalculator? capacityCalculator = null,
        ITimberbornFireLogSink? logSink = null)
    {
        _targetApi = targetApi ?? throw new ArgumentNullException(nameof(targetApi));
        _capacityCalculator = capacityCalculator ?? new TimberbornBurnDamageCapacityCalculator();
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
    }

    public TimberbornStructureBurnDamageRollbackSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        TimberbornStructureBurnDamageConsequence[] consequences = decisions
            .Select(decision => TimberbornStructureBurnDamageConsequence.FromDecision(tick, decision))
            .Where(static consequence => consequence.HasBurnPressure)
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
        StructureApplyOutcome[] outcomes = uniqueTargets
            .Select(ApplyTarget)
            .ToArray();

        TimberbornStructureBurnDamageRollbackSummary summary = new(
            ConsideredDeltaCount: consequences.Length,
            MatchedStructureCellCount: matchedTargets.Length,
            DuplicateStructureTargetSuppressedCount: matchedTargets.Length - uniqueTargets.Length,
            ZeroBurnableCapacityTargetCount: outcomes.Count(static outcome => outcome.IsZeroBurnableCapacity),
            MaterialValueLost: outcomes.Sum(static outcome => outcome.MaterialValueLost),
            ClosedStructureCount: outcomes.Count(static outcome => outcome.ApplyResult.Closed),
            RepairBlockedCount: outcomes.Count(static outcome => outcome.Request.RepairBlocked),
            RepairEligibleCount: outcomes.Count(static outcome => outcome.ApplyResult.RepairEligible),
            ScorchedStageCount: outcomes.Count(static outcome => outcome.Request.RollbackStage == TimberbornStructureBurnRollbackStage.Scorched),
            PartialConstructionStageCount: outcomes.Count(static outcome => outcome.Request.RollbackStage == TimberbornStructureBurnRollbackStage.PartialConstruction),
            UnfinishedStageCount: outcomes.Count(static outcome => outcome.Request.RollbackStage == TimberbornStructureBurnRollbackStage.Unfinished),
            VisualRollbackAppliedCount: outcomes.Count(static outcome => outcome.ApplyResult.VisualRollbackApplied),
            SkippedNoSafeApiCount: outcomes.Count(static outcome => outcome.ApplyResult.SkippedNoSafeApi),
            TotalDamageApplied: outcomes.Sum(static outcome => outcome.Request.DamageApplied));
        _logSink.Info(summary.ToLogToken(tick));
        return summary;
    }

    private StructureApplyOutcome ApplyTarget(ResolvedTarget resolvedTarget)
    {
        TimberbornStructureBurnDamageTarget target = resolvedTarget.Target ??
            throw new InvalidOperationException("Resolved structure burn damage target cannot be null during application.");
        TimberbornBurnDamageDescriptor descriptor = new(
            target.SpecId,
            TimberbornBurnDamageTargetKind.Structure,
            target.ConstructionResources.Count == 0
                ? TimberbornBurnMaterialKind.NonBurnable
                : TimberbornBurnMaterialKind.Constructed,
            constructionResources: target.ConstructionResources);
        TimberbornBurnDamageCapacity capacity = _capacityCalculator.Calculate(descriptor);
        bool isZeroBurnableCapacity = capacity.Capacity == 0;
        if (isZeroBurnableCapacity)
        {
            return new StructureApplyOutcome(
                IsZeroBurnableCapacity: true,
                MaterialValueLost: 0,
                TimberbornStructureBurnDamageApplyRequestDefaults.None,
                new TimberbornStructureBurnDamageApplyResult(false, false, false, false));
        }

        StructureDamageState state = _statesByStableId.GetValueOrDefault(target.StableId, new StructureDamageState(0));
        int damageApplied = Math.Min(
            resolvedTarget.Consequence.DamageUnits,
            Math.Max(0, capacity.Capacity - state.DamageTaken));
        StructureDamageState nextState = new(state.DamageTaken + damageApplied);
        _statesByStableId[target.StableId] = nextState;
        TimberbornStructureBurnRollbackStage stage = SelectStage(nextState.DamageTaken, capacity.Capacity);
        bool repairBlocked = resolvedTarget.Consequence.ShouldBlockRepair;
        bool repairEligible = nextState.DamageTaken > 0 && !repairBlocked && target.CanRepairAfterDanger;
        TimberbornStructureBurnDamageApplyRequest request = new(
            DamageApplied: damageApplied,
            DamageTaken: nextState.DamageTaken,
            DamageCapacity: capacity.Capacity,
            RollbackStage: stage,
            ShouldClose: resolvedTarget.Consequence.ShouldClose || damageApplied > 0,
            RepairBlocked: repairBlocked,
            RepairEligible: repairEligible,
            ShouldApplyRollbackVisual: stage != TimberbornStructureBurnRollbackStage.None);
        TimberbornStructureBurnDamageApplyResult result = request.ShouldClose ||
            request.ShouldApplyRollbackVisual ||
            request.RepairBlocked ||
            request.RepairEligible
                ? _targetApi.ApplyState(target, request)
                : new TimberbornStructureBurnDamageApplyResult(false, false, false, repairEligible);

        return new StructureApplyOutcome(
            IsZeroBurnableCapacity: false,
            MaterialValueLost: damageApplied,
            request,
            result);
    }

    private static TimberbornStructureBurnRollbackStage SelectStage(int damageTaken, int damageCapacity)
    {
        if (damageTaken <= 0 || damageCapacity <= 0)
        {
            return TimberbornStructureBurnRollbackStage.None;
        }

        if (damageTaken >= damageCapacity)
        {
            return TimberbornStructureBurnRollbackStage.Unfinished;
        }

        return damageTaken * 2 >= damageCapacity
            ? TimberbornStructureBurnRollbackStage.PartialConstruction
            : TimberbornStructureBurnRollbackStage.Scorched;
    }

    private readonly record struct ResolvedTarget(
        TimberbornStructureBurnDamageConsequence Consequence,
        TimberbornStructureBurnDamageTarget? Target);

    private readonly record struct StructureDamageState(int DamageTaken);

    private readonly record struct StructureApplyOutcome(
        bool IsZeroBurnableCapacity,
        int MaterialValueLost,
        TimberbornStructureBurnDamageApplyRequest Request,
        TimberbornStructureBurnDamageApplyResult ApplyResult);
}

public sealed class NullTimberbornStructureBurnDamageRollbackSink : ITimberbornStructureBurnDamageRollbackSink
{
    public static readonly NullTimberbornStructureBurnDamageRollbackSink Instance = new();

    private NullTimberbornStructureBurnDamageRollbackSink()
    {
    }

    public TimberbornStructureBurnDamageRollbackSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        return TimberbornStructureBurnDamageRollbackSummary.Empty;
    }
}

public static class TimberbornStructureBurnDamageApplyRequestDefaults
{
    public static readonly TimberbornStructureBurnDamageApplyRequest None = new(
        DamageApplied: 0,
        DamageTaken: 0,
        DamageCapacity: 0,
        RollbackStage: TimberbornStructureBurnRollbackStage.None,
        ShouldClose: false,
        RepairBlocked: false,
        RepairEligible: false,
        ShouldApplyRollbackVisual: false);
}

public sealed class TimberbornStructureBurnDamageRollbackTargetApi : ITimberbornStructureBurnDamageRollbackTargetApi
{
    private readonly FireGrid _grid;
    private readonly IBlockService _blockService;

    public TimberbornStructureBurnDamageRollbackTargetApi(FireGrid grid, IBlockService blockService)
    {
        _grid = grid;
        _blockService = blockService ?? throw new ArgumentNullException(nameof(blockService));
    }

    public TimberbornStructureBurnDamageTarget? ResolveTarget(
        TimberbornStructureBurnDamageConsequence consequence)
    {
        (int x, int y, int z) = _grid.FromIndex(consequence.CellIndex);
        Vector3Int coordinates = new(x, y, z);
        BlockObject? blockObject = _blockService
            .GetObjectsWithComponentAt<BlockObject>(coordinates)
            .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
            .FirstOrDefault();
        if (blockObject is null || IsInfrastructureLikeName(blockObject.Name))
        {
            return null;
        }

        bool hasPausableBuilding = _blockService
            .GetObjectsWithComponentAt<PausableBuilding>(coordinates)
            .Any();

        return new TimberbornStructureBurnDamageTarget(
            StableId: $"structure:{RuntimeHelpers.GetHashCode(blockObject)}",
            SpecId: blockObject.Name,
            CellIndex: consequence.CellIndex,
            ConstructionResources: GuessConstructionResources(blockObject.Name),
            CanClose: hasPausableBuilding,
            CanApplyRollbackVisual: false,
            CanRepairAfterDanger: false);
    }

    public TimberbornStructureBurnDamageApplyResult ApplyState(
        TimberbornStructureBurnDamageTarget target,
        TimberbornStructureBurnDamageApplyRequest request)
    {
        (int x, int y, int z) = _grid.FromIndex(target.CellIndex);
        Vector3Int coordinates = new(x, y, z);
        PausableBuilding[] buildingsToPause = request.ShouldClose && target.CanClose
            ? _blockService
                .GetObjectsWithComponentAt<PausableBuilding>(coordinates)
                .Where(static building => !building.Paused)
                .ToArray()
            : Array.Empty<PausableBuilding>();

        Array.ForEach(buildingsToPause, static building => building.Pause());

        return new TimberbornStructureBurnDamageApplyResult(
            Closed: buildingsToPause.Length > 0,
            VisualRollbackApplied: false,
            SkippedNoSafeApi: request.ShouldApplyRollbackVisual || (!target.CanClose && request.ShouldClose),
            RepairEligible: request.RepairEligible);
    }

    private static bool IsInfrastructureLikeName(string name)
    {
        return name.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Platform", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Bridge", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Stair", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Power", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Shaft", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Dam", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Levee", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Floodgate", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Dynamite", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<TimberbornBurnDamageResourceStack> GuessConstructionResources(string name)
    {
        if (name.Contains("Metal", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { new TimberbornBurnDamageResourceStack("MetalBlock", 1) };
        }

        if (name.Contains("House", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Lodge", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Mill", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Workshop", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Warehouse", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Tank", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Storage", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new TimberbornBurnDamageResourceStack("Log", 2),
                new TimberbornBurnDamageResourceStack("Plank", 2),
            };
        }

        return Array.Empty<TimberbornBurnDamageResourceStack>();
    }
}
