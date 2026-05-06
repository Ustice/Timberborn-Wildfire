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
    private readonly ITimberbornBurnDamageTargetStateProvider? _burnDamageTargets;
    private readonly Dictionary<string, StructureDamageState> _statesByStableId = new(StringComparer.Ordinal);

    public TimberbornStructureBurnDamageRollbackSink(
        ITimberbornStructureBurnDamageRollbackTargetApi targetApi,
        TimberbornBurnDamageCapacityCalculator? capacityCalculator = null,
        ITimberbornFireLogSink? logSink = null,
        ITimberbornBurnDamageTargetStateProvider? burnDamageTargets = null)
    {
        _targetApi = targetApi ?? throw new ArgumentNullException(nameof(targetApi));
        _capacityCalculator = capacityCalculator ?? new TimberbornBurnDamageCapacityCalculator();
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
        _burnDamageTargets = burnDamageTargets;
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
        if (TimberbornReleaseLogNoisePolicy.ShouldLogConsequenceSummary(
            summary.MatchedStructureCellCount,
            summary.ClosedStructureCount,
            summary.RepairEligibleCount,
            summary.VisualRollbackAppliedCount,
            summary.SkippedNoSafeApiCount))
        {
            _logSink.Info(summary.ToLogToken(tick));
        }
        return summary;
    }

    private StructureApplyOutcome ApplyTarget(ResolvedTarget resolvedTarget)
    {
        TimberbornStructureBurnDamageTarget target = resolvedTarget.Target ??
            throw new InvalidOperationException("Resolved structure burn damage target cannot be null during application.");
        TimberbornBurnDamageTargetState? burnDamageState = resolvedTarget.BurnDamageState;
        TimberbornBurnDamageDescriptor descriptor = new(
            target.SpecId,
            TimberbornBurnDamageTargetKind.Structure,
            target.ConstructionResources.Count == 0
                ? TimberbornBurnMaterialKind.NonBurnable
                : TimberbornBurnMaterialKind.Constructed,
            constructionResources: target.ConstructionResources);
        int damageCapacity = burnDamageState?.DamageCapacity ?? _capacityCalculator.Calculate(descriptor).Capacity;
        bool isZeroBurnableCapacity = damageCapacity == 0;
        if (isZeroBurnableCapacity)
        {
            return new StructureApplyOutcome(
                IsZeroBurnableCapacity: true,
                MaterialValueLost: 0,
                TimberbornStructureBurnDamageApplyRequestDefaults.None,
                new TimberbornStructureBurnDamageApplyResult(false, false, false, false));
        }

        StructureDamageState localState = _statesByStableId.GetValueOrDefault(target.StableId, new StructureDamageState(0));
        int damageTaken = burnDamageState?.DamageTaken ?? localState.DamageTaken;
        int damageApplied = burnDamageState is null
            ? Math.Min(
                resolvedTarget.Consequence.DamageUnits,
                Math.Max(0, damageCapacity - localState.DamageTaken))
            : resolvedTarget.AppliedEvent?.DamageApplied ?? 0;
        if (burnDamageState is null)
        {
            StructureDamageState nextState = new(localState.DamageTaken + damageApplied);
            _statesByStableId[target.StableId] = nextState;
            damageTaken = nextState.DamageTaken;
        }

        TimberbornStructureBurnRollbackStage stage = SelectStage(damageTaken, damageCapacity);
        bool repairBlocked = resolvedTarget.Consequence.ShouldBlockRepair;
        bool repairEligible = damageTaken > 0 && !repairBlocked && target.CanRepairAfterDanger;
        TimberbornStructureBurnDamageApplyRequest request = new(
            DamageApplied: damageApplied,
            DamageTaken: damageTaken,
            DamageCapacity: damageCapacity,
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

    private ResolvedTarget ResolveTarget(TimberbornStructureBurnDamageConsequence consequence)
    {
        TimberbornStructureBurnDamageTarget? target = _targetApi.ResolveTarget(consequence);
        if (_burnDamageTargets is null)
        {
            return new ResolvedTarget(consequence, target, BurnDamageState: null);
        }

        if (!_burnDamageTargets.TryGetStateForCell(consequence.CellIndex, out TimberbornBurnDamageTargetState state) ||
            state.TargetKind != TimberbornBurnDamageTargetKind.Structure ||
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
        TimberbornStructureBurnDamageConsequence Consequence,
        TimberbornStructureBurnDamageTarget? Target,
        TimberbornBurnDamageTargetState? BurnDamageState,
        TimberbornBurnDamageAppliedEvent? AppliedEvent = null);

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
            ConstructionResources: TimberbornBurnDamageResourceGuesses.ForStructure(blockObject.Name),
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
}
