using System.Runtime.CompilerServices;
using System.Reflection;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.ConstructionSites;
using Timberborn.Coordinates;
using Timberborn.EnterableSystem;
using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.Navigation;
using Timberborn.WorkSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Consequences;

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
    private const int RepairBlockedHeat = 5;

    public bool HasBurnPressure => true;

    public bool ShouldClose => DamageUnits > 0 || Heat >= RepairBlockedHeat;

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
    bool ConstructionPhaseEntered,
    bool SkippedNativeConstructionApi,
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
    int ConstructionPhaseEnteredCount,
    int SkippedNativeConstructionApiCount,
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
        ConstructionPhaseEnteredCount: 0,
        SkippedNativeConstructionApiCount: 0,
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
            $"construction_phase_entered={ConstructionPhaseEnteredCount} " +
            $"skipped_native_construction_api={SkippedNativeConstructionApiCount} " +
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
    internal const int UnfinishedDamageThresholdPercent = 10;

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
            ConstructionPhaseEnteredCount: outcomes.Count(static outcome => outcome.ApplyResult.ConstructionPhaseEntered),
            SkippedNativeConstructionApiCount: outcomes.Count(static outcome => outcome.ApplyResult.SkippedNativeConstructionApi),
            TotalDamageApplied: outcomes.Sum(static outcome => outcome.Request.DamageApplied));
        if (TimberbornReleaseLogNoisePolicy.ShouldLogConsequenceSummary(
            summary.MatchedStructureCellCount,
            summary.ClosedStructureCount,
            summary.RepairEligibleCount,
            summary.VisualRollbackAppliedCount))
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
                new TimberbornStructureBurnDamageApplyResult(false, false, false, false, false));
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
            ShouldClose: resolvedTarget.Consequence.ShouldClose,
            RepairBlocked: repairBlocked,
            RepairEligible: repairEligible,
            ShouldApplyRollbackVisual: stage != TimberbornStructureBurnRollbackStage.None);
        TimberbornStructureBurnDamageApplyResult result = request.ShouldClose ||
            request.ShouldApplyRollbackVisual ||
            request.RepairBlocked ||
            request.RepairEligible
                ? _targetApi.ApplyState(target, request)
                : new TimberbornStructureBurnDamageApplyResult(false, false, false, false, repairEligible);

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

        return TimberbornStructureBurnRollbackStage.Unfinished;
    }

    internal static int MinimumUnfinishedDamage(int damageCapacity)
    {
        if (damageCapacity <= 0)
        {
            return 0;
        }

        return Math.Max(1, (int)(((long)damageCapacity * UnfinishedDamageThresholdPercent + 99) / 100));
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
    private readonly EntityService? _entityService;
    private readonly ConstructionFactory? _constructionFactory;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly TimberbornRuntimeBurnedTextureDeriver _textureDeriver;

    public TimberbornStructureBurnDamageRollbackTargetApi(
        FireGrid grid,
        IBlockService blockService,
        ITimberbornFireLogSink? logSink = null,
        TimberbornRuntimeBurnedTextureDeriver? textureDeriver = null,
        EntityService? entityService = null,
        ConstructionFactory? constructionFactory = null)
    {
        _grid = grid;
        _blockService = blockService ?? throw new ArgumentNullException(nameof(blockService));
        _entityService = entityService;
        _constructionFactory = constructionFactory;
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
        _textureDeriver = textureDeriver ?? new TimberbornRuntimeBurnedTextureDeriver(_logSink);
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

        bool canEnterUnfinishedState = CanEnterUnfinishedState(blockObject);
        bool canClose = canEnterUnfinishedState || HasInterruptibleBuildingUse(blockObject);

        return new TimberbornStructureBurnDamageTarget(
            StableId: $"structure:{RuntimeHelpers.GetHashCode(blockObject)}",
            SpecId: blockObject.Name,
            CellIndex: consequence.CellIndex,
            ConstructionResources: TimberbornBurnDamageResourceGuesses.ForStructure(blockObject.Name),
            CanClose: canClose,
            CanApplyRollbackVisual: true,
            CanRepairAfterDanger: canClose || canEnterUnfinishedState);
    }

    public TimberbornStructureBurnDamageApplyResult ApplyState(
        TimberbornStructureBurnDamageTarget target,
        TimberbornStructureBurnDamageApplyRequest request)
    {
        (int x, int y, int z) = _grid.FromIndex(target.CellIndex);
        Vector3Int coordinates = new(x, y, z);
        BlockObject? blockObject = _blockService
            .GetObjectsWithComponentAt<BlockObject>(coordinates)
            .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
            .FirstOrDefault();
        StructureOccupancySnapshot occupancySnapshot = blockObject is null || !request.ShouldClose
            ? StructureOccupancySnapshot.Empty
            : GetOccupancySnapshot(blockObject);
        int interruptedWorkerCount = blockObject is null || !request.ShouldClose
            ? 0
            : UnassignWorkers(blockObject);
        bool enteredUnfinishedState = false;
        int removedConstructionMaterialCount = 0;
        bool resetConstructionProgress = false;
        int burnedMaterialCount = 0;
        bool hasBurnedTextures = false;

        if (blockObject is not null && (request.ShouldApplyRollbackVisual || request.ShouldClose))
        {
            ConstructionSite? constructionSite = null;
            enteredUnfinishedState = request.ShouldClose || RequestsNativeConstructionPhase(request)
                ? TryApplyNativeConstructionPhase(blockObject, target, out blockObject, out constructionSite)
                : false;

            constructionSite ??= TryGetConstructionSite(blockObject, coordinates);
            float remainingConstructionFraction = CalculateRemainingConstructionFraction(request);
            removedConstructionMaterialCount = request.ShouldApplyRollbackVisual && ShouldRemoveConstructionMaterials(request)
                ? SynchronizeConstructionMaterials(constructionSite, target, remainingConstructionFraction)
                : 0;
            resetConstructionProgress = request.ShouldApplyRollbackVisual &&
                target.CanRepairAfterDanger &&
                request.RepairBlocked &&
                TrySetConstructionProgress(constructionSite, remainingConstructionFraction);
            burnedMaterialCount = request.ShouldApplyRollbackVisual
                ? ApplyBurnedTextures(blockObject, target.SpecId)
                : 0;
            hasBurnedTextures = burnedMaterialCount > 0 || HasBurnedTextures(blockObject);
            LogRollbackVisual(target, request, enteredUnfinishedState, removedConstructionMaterialCount, resetConstructionProgress, burnedMaterialCount);
        }

        bool closed = request.ShouldClose &&
            target.CanClose &&
            blockObject is not null &&
            (enteredUnfinishedState || blockObject.IsUnfinished || interruptedWorkerCount > 0);
        if (closed)
        {
            LogStructureClosed(
                target,
                enteredUnfinishedState,
                blockObject?.IsUnfinished == true,
                interruptedWorkerCount,
                occupancySnapshot);
            LogWorkerExposureUnavailable(target, occupancySnapshot);
        }

        bool visualRollbackApplied = enteredUnfinishedState || removedConstructionMaterialCount > 0 || hasBurnedTextures;
        bool skippedNativeConstructionApi = RequestsNativeConstructionPhase(request) && !enteredUnfinishedState;
        if (request.ShouldClose && !closed)
        {
            throw new InvalidOperationException(
                $"Structure burn damage rollback failed to close {target.SpecId} at cell {target.CellIndex}.");
        }

        return new TimberbornStructureBurnDamageApplyResult(
            Closed: closed,
            VisualRollbackApplied: visualRollbackApplied,
            ConstructionPhaseEntered: enteredUnfinishedState,
            SkippedNativeConstructionApi: skippedNativeConstructionApi,
            RepairEligible: request.RepairEligible);
    }

    public static bool RequestsNativeConstructionPhase(TimberbornStructureBurnDamageApplyRequest request)
    {
        return request.ShouldApplyRollbackVisual &&
            request.RollbackStage is (
                TimberbornStructureBurnRollbackStage.PartialConstruction or
                TimberbornStructureBurnRollbackStage.Unfinished);
    }

    public static bool IsDistrictCenterName(string name)
    {
        return name.Contains("DistrictCenter", StringComparison.OrdinalIgnoreCase);
    }

    private static Accessible[] GetBuildingAccessibles(BlockObject blockObject)
    {
        return blockObject.GetComponentsAllocating<Accessible>()
            .Concat(blockObject.Transform.GetComponentsInChildren<Accessible>(includeInactive: true))
            .GroupBy(static accessible => RuntimeHelpers.GetHashCode(accessible))
            .Select(static group => group.First())
            .ToArray();
    }

    private static Workplace[] GetWorkplaces(BlockObject blockObject)
    {
        return blockObject.GetComponentsAllocating<Workplace>()
            .Concat(blockObject.Transform.GetComponentsInChildren<Workplace>(includeInactive: true))
            .GroupBy(static workplace => RuntimeHelpers.GetHashCode(workplace))
            .Select(static group => group.First())
            .ToArray();
    }

    private static Enterable[] GetEnterables(BlockObject blockObject)
    {
        return blockObject.GetComponentsAllocating<Enterable>()
            .Concat(blockObject.Transform.GetComponentsInChildren<Enterable>(includeInactive: true))
            .GroupBy(static enterable => RuntimeHelpers.GetHashCode(enterable))
            .Select(static group => group.First())
            .ToArray();
    }

    private static bool HasInterruptibleBuildingUse(BlockObject blockObject)
    {
        return GetWorkplaces(blockObject).Length > 0 ||
            GetEnterables(blockObject).Length > 0 ||
            GetBuildingAccessibles(blockObject).Any(static accessible => accessible.Accesses.Count > 0);
    }

    private void LogStructureClosed(
        TimberbornStructureBurnDamageTarget target,
        bool enteredUnfinishedState,
        bool isUnfinished,
        int interruptedWorkerCount,
        StructureOccupancySnapshot occupancySnapshot)
    {
        string closureKind = enteredUnfinishedState
            ? "native_unfinished"
            : isUnfinished
                ? "already_unfinished"
                : "workers_interrupted";
        _logSink.Info(
            "wildfire_timberborn_structure_closed " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(target.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(target.SpecId)} " +
            $"closure={closureKind} " +
            $"construction_phase_entered={enteredUnfinishedState} " +
            $"assigned_workers_before={occupancySnapshot.AssignedWorkerCount} " +
            $"assigned_workers_interrupted={interruptedWorkerCount} " +
            $"enterers_inside_before={occupancySnapshot.EnterersInsideCount}");
    }

    private static StructureOccupancySnapshot GetOccupancySnapshot(BlockObject blockObject)
    {
        return new StructureOccupancySnapshot(
            AssignedWorkerCount: GetWorkplaces(blockObject).Sum(static workplace => workplace.NumberOfAssignedWorkers),
            EnterersInsideCount: GetEnterables(blockObject).Sum(static enterable => enterable.NumberOfEnterersInside));
    }

    private static int UnassignWorkers(BlockObject blockObject)
    {
        return GetWorkplaces(blockObject)
            .Select(static workplace =>
            {
                int assignedWorkerCount = workplace.NumberOfAssignedWorkers;
                if (assignedWorkerCount > 0)
                {
                    workplace.UnassignAllWorkers();
                }

                return assignedWorkerCount;
            })
            .Sum();
    }

    private void LogWorkerExposureUnavailable(
        TimberbornStructureBurnDamageTarget target,
        StructureOccupancySnapshot occupancySnapshot)
    {
        _logSink.Info(
            "wildfire_timberborn_structure_worker_exposure " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(target.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(target.SpecId)} " +
            $"candidate_count={occupancySnapshot.CandidateCount} " +
            $"assigned_workers={occupancySnapshot.AssignedWorkerCount} " +
            $"enterers_inside={occupancySnapshot.EnterersInsideCount} " +
            "status=skipped_worker_exposure");
    }

    private static bool CanEnterUnfinishedState(BlockObject blockObject)
    {
        try
        {
            return blockObject.IsFinished;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"Cannot determine unfinished rollback eligibility for {blockObject.Name}.",
                exception);
        }
    }

    private static bool TryEnterUnfinishedState(BlockObject blockObject)
    {
        try
        {
            if (blockObject.IsUnfinished)
            {
                return true;
            }

            if (!blockObject.IsFinished)
            {
                return false;
            }

            object? blockObjectState = typeof(BlockObject).GetField(
                "_blockObjectState",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(blockObject);
            if (blockObjectState is null)
            {
                throw new InvalidOperationException("BlockObject private state is unavailable for unfinished rollback.");
            }

            MethodInfo? enterStateMethod = blockObjectState.GetType().GetMethod(
                "EnterState",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Type? stateType = enterStateMethod?.GetParameters().FirstOrDefault()?.ParameterType;
            if (enterStateMethod is null || stateType is null)
            {
                throw new InvalidOperationException("BlockObject EnterState API is unavailable for unfinished rollback.");
            }

            object unfinishedState = Enum.Parse(stateType, "Unfinished");
            enterStateMethod.Invoke(blockObjectState, new[] { unfinishedState });
            return blockObject.IsUnfinished;
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException("BlockObject unfinished rollback failed.", exception);
        }
    }

    private bool TryApplyNativeConstructionPhase(
        BlockObject blockObject,
        TimberbornStructureBurnDamageTarget target,
        out BlockObject activeBlockObject,
        out ConstructionSite? constructionSite)
    {
        constructionSite = null;
        activeBlockObject = blockObject;
        return TryRebuildStructureAsUnfinished(blockObject, target, out activeBlockObject, out constructionSite);
    }

    private bool TryRebuildStructureAsUnfinished(
        BlockObject blockObject,
        TimberbornStructureBurnDamageTarget target,
        out BlockObject rebuiltBlockObject,
        out ConstructionSite? rebuiltConstructionSite)
    {
        rebuiltBlockObject = blockObject;
        rebuiltConstructionSite = null;
        try
        {
            if (blockObject.IsUnfinished)
            {
                rebuiltConstructionSite = TryGetConstructionSite(blockObject, blockObject.Coordinates);
                return true;
            }

            if (!blockObject.IsFinished)
            {
                return false;
            }

            if (_entityService is null || _constructionFactory is null)
            {
                throw new InvalidOperationException("Native construction rebuild requires Timberborn entity and construction services.");
            }

            if (!blockObject.TryGetComponent(out Building building) || building.Spec is null)
            {
                throw new InvalidOperationException($"Native construction rebuild cannot resolve building spec for {blockObject.Name}.");
            }

            bool isDistrictCenter = IsDistrictCenterName(blockObject.Name) || IsDistrictCenterName(target.SpecId);
            object? districtUpdater = isDistrictCenter
                ? ResolveDistrictUpdater(ResolveDistrictService(blockObject))
                : null;
            Placement placement = blockObject.Placement;

            _entityService.Delete(blockObject);
            if (districtUpdater is not null)
            {
                FlushDistrictRemoval(districtUpdater);
            }

            rebuiltConstructionSite = _constructionFactory.CreateAsUnfinished(building.Spec, placement);
            rebuiltBlockObject = rebuiltConstructionSite.GetComponent<BlockObject>();
            if (!rebuiltBlockObject.IsUnfinished)
            {
                throw new InvalidOperationException($"Native construction rebuild recreated {target.SpecId} outside unfinished state.");
            }

            _logSink.Info(
                "wildfire_timberborn_structure_burn_rebuilt_unfinished " +
                $"stable_id={TimberbornQaCommandBridge.FormatToken(target.StableId)} " +
                $"target={TimberbornQaCommandBridge.FormatToken(target.SpecId)} " +
                $"district_center={isDistrictCenter.ToString().ToLowerInvariant()} " +
                $"cell={target.CellIndex}");
            return true;
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException("Native construction rebuild failed.", exception);
        }
    }

    private static object ResolveDistrictService(BlockObject blockObject)
    {
        object? districtCenter = blockObject.AllComponents
            .FirstOrDefault(static component =>
                component.GetType().FullName == "Timberborn.GameDistricts.DistrictCenter");
        if (districtCenter is null)
        {
            throw new InvalidOperationException($"Native construction rebuild cannot resolve DistrictCenter component for {blockObject.Name}.");
        }

        object? districtService = districtCenter.GetType()
            .GetField("_districtService", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(districtCenter);
        return districtService ?? throw new InvalidOperationException("Native construction rebuild cannot resolve DistrictService.");
    }

    private static object ResolveDistrictUpdater(object districtService)
    {
        object? districtUpdater = districtService.GetType()
            .GetField("_districtUpdater", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(districtService);
        return districtUpdater ?? throw new InvalidOperationException("Native construction rebuild cannot resolve DistrictUpdater.");
    }

    private static void FlushDistrictRemoval(object districtUpdater)
    {
        InvokeDistrictUpdaterFlush(districtUpdater, "ProcessRegularChanges");
        InvokeDistrictUpdaterFlush(districtUpdater, "ProcessInstantChanges");
    }

    private static void InvokeDistrictUpdaterFlush(object districtUpdater, string methodName)
    {
        MethodInfo? method = districtUpdater.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method is null)
        {
            throw new InvalidOperationException($"Native construction rebuild cannot flush {methodName}.");
        }

        method.Invoke(districtUpdater, new object?[] { null });
    }

    private ConstructionSite? TryGetConstructionSite(BlockObject blockObject, Vector3Int coordinates)
    {
        if (blockObject.TryGetComponent(out ConstructionSite directConstructionSite))
        {
            return directConstructionSite;
        }

        return _blockService
            .GetObjectsWithComponentAt<ConstructionSite>(coordinates)
            .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
            .FirstOrDefault();
    }

    private int SynchronizeConstructionMaterials(
        ConstructionSite? constructionSite,
        TimberbornStructureBurnDamageTarget target,
        float remainingConstructionFraction)
    {
        if (constructionSite?.Inventory is null || target.ConstructionResources.Count == 0)
        {
            return 0;
        }

        return target.ConstructionResources
            .GroupBy(static resource => resource.ResourceId, StringComparer.Ordinal)
            .Select(static group => new TimberbornBurnDamageResourceStack(
                group.Key,
                group.Sum(static resource => resource.Amount)))
            .Select(resource => SynchronizeConstructionMaterial(
                constructionSite,
                target,
                resource.ResourceId,
                CalculateRequiredConstructionMaterialAmount(resource.Amount, remainingConstructionFraction)))
            .Sum();
    }

    private int SynchronizeConstructionMaterial(
        ConstructionSite constructionSite,
        TimberbornStructureBurnDamageTarget target,
        string resourceId,
        int requiredAmount)
    {
        int amount = Math.Max(0, constructionSite.Inventory.AmountInStock(resourceId));
        int delta = amount - requiredAmount;
        if (delta == 0)
        {
            return 0;
        }

        if (delta > 0)
        {
            constructionSite.Inventory.Take(new GoodAmount(resourceId, delta));
        }
        else
        {
            constructionSite.Inventory.GiveIgnoringCapacity(new GoodAmount(resourceId, -delta));
        }

        _logSink.Info(
            "wildfire_timberborn_structure_construction_material_burned " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(target.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(target.SpecId)} " +
            $"resource={TimberbornQaCommandBridge.FormatToken(resourceId)} " +
            $"amount={Math.Max(0, delta)} " +
            $"remaining={requiredAmount}");
        return Math.Max(0, delta);
    }

    private static bool TrySetConstructionProgress(ConstructionSite? constructionSite, float remainingConstructionFraction)
    {
        if (constructionSite is null)
        {
            return false;
        }

        try
        {
            MethodInfo? getConstructionTimeMethod = constructionSite.GetType()
                .GetField(
                    "_constructionSiteBuildTimeCalculator",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(constructionSite)
                ?.GetType()
                .GetMethod(
                    "GetConstructionTimeInHours",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    new[] { typeof(ConstructionSite) },
                    modifiers: null);
            if (getConstructionTimeMethod is null)
            {
                throw new InvalidOperationException("ConstructionSite build-time calculator API is unavailable.");
            }

            float constructionTimeInHours = Convert.ToSingle(getConstructionTimeMethod.Invoke(
                constructionSite,
                new object[] { constructionSite }));
            MethodInfo? method = constructionSite.GetType().GetMethod(
                "SetBuildTimeProgress",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                new[] { typeof(float) },
                modifiers: null);
            if (method is null)
            {
                throw new InvalidOperationException("ConstructionSite.SetBuildTimeProgress API is unavailable.");
            }

            float buildTimeProgressInHours = Math.Clamp(
                constructionTimeInHours * remainingConstructionFraction,
                0f,
                constructionTimeInHours);
            method.Invoke(constructionSite, new object[] { buildTimeProgressInHours });
            return true;
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException("ConstructionSite progress reset failed.", exception);
        }
    }

    public static bool ShouldRemoveConstructionMaterials(TimberbornStructureBurnDamageApplyRequest request)
    {
        return request.ShouldApplyRollbackVisual && request.RepairBlocked;
    }

    public static float CalculateRemainingConstructionFraction(TimberbornStructureBurnDamageApplyRequest request)
    {
        if (request.DamageCapacity <= 0)
        {
            return 0f;
        }

        float damageFraction = Math.Clamp((float)request.DamageTaken / request.DamageCapacity, 0f, 1f);
        return Math.Clamp(1f - damageFraction, 0f, 1f);
    }

    public static int CalculateRequiredConstructionMaterialAmount(int originalAmount, float remainingConstructionFraction)
    {
        if (originalAmount <= 0)
        {
            return 0;
        }

        return (int)Math.Ceiling(originalAmount * Math.Clamp(remainingConstructionFraction, 0f, 1f));
    }

    private int ApplyBurnedTextures(BlockObject blockObject, string textureLabel)
    {
        return blockObject.Transform
            .GetComponentsInChildren<Renderer>(includeInactive: true)
            .Sum(renderer => ApplyBurnedTextures(renderer, textureLabel));
    }

    private static bool HasBurnedTextures(BlockObject blockObject)
    {
        return blockObject.Transform
            .GetComponentsInChildren<Renderer>(includeInactive: true)
            .SelectMany(static renderer => renderer.sharedMaterials)
            .OfType<Material>()
            .Any(IsBurnedMaterial);
    }

    private int ApplyBurnedTextures(Renderer renderer, string textureLabel)
    {
        Material?[] materials = renderer.sharedMaterials;
        Material?[] updatedMaterials = materials
            .Select(material => CreateBurnedMaterialOrOriginal(material, textureLabel))
            .ToArray();
        int updatedMaterialCount = Enumerable.Range(0, materials.Length)
            .Count(index => !ReferenceEquals(materials[index], updatedMaterials[index]));

        if (updatedMaterialCount > 0)
        {
            renderer.sharedMaterials = updatedMaterials;
        }

        return updatedMaterialCount;
    }

    private Material? CreateBurnedMaterialOrOriginal(Material? source, string textureLabel)
    {
        if (source is null || IsBurnedMaterial(source))
        {
            return source;
        }

        if (source.HasProperty("_MainTex") && source.mainTexture is not null)
        {
            Texture2D? burnedTexture = _textureDeriver.DeriveBurnedTexture(source.mainTexture, textureLabel);
            if (burnedTexture is not null)
            {
                return CreateBurnedMaterial(source, burnedTexture);
            }
        }

        return CreateBurnedTintMaterial(source);
    }

    private static Material CreateBurnedMaterial(Material source, Texture burnedTexture)
    {
        Material material = new(source)
        {
            name = $"{source.name} Wildfire Burned",
            mainTexture = burnedTexture,
            hideFlags = HideFlags.HideAndDontSave,
        };
        return material;
    }

    private static Material CreateBurnedTintMaterial(Material source)
    {
        Material material = new(source)
        {
            name = $"{source.name} Wildfire Burned",
            hideFlags = HideFlags.HideAndDontSave,
        };
        SetColorIfPresent(material, "_BaseColor", new Color(0.18f, 0.16f, 0.14f, 1f));
        SetColorIfPresent(material, "_Color", new Color(0.18f, 0.16f, 0.14f, 1f));
        return material;
    }

    private static void SetColorIfPresent(Material material, string propertyName, Color color)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }

    private static bool IsBurnedMaterial(Material material)
    {
        return material.name.EndsWith(" Wildfire Burned", StringComparison.Ordinal);
    }

    private void LogRollbackVisual(
        TimberbornStructureBurnDamageTarget target,
        TimberbornStructureBurnDamageApplyRequest request,
        bool enteredUnfinishedState,
        int removedConstructionMaterialCount,
        bool resetConstructionProgress,
        int burnedMaterialCount)
    {
        _logSink.Info(
            "wildfire_timberborn_structure_burned_visual_applied " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(target.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(target.SpecId)} " +
            $"stage={TimberbornQaCommandBridge.FormatToken(request.RollbackStage.ToString())} " +
            $"entered_unfinished={enteredUnfinishedState} " +
            $"construction_materials_removed={removedConstructionMaterialCount} " +
            $"repair_blocked={request.RepairBlocked} " +
            $"construction_progress_reset={resetConstructionProgress} " +
            $"materials={burnedMaterialCount}");
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

    private readonly record struct StructureOccupancySnapshot(int AssignedWorkerCount, int EnterersInsideCount)
    {
        public static readonly StructureOccupancySnapshot Empty = new(0, 0);

        public int CandidateCount => AssignedWorkerCount + EnterersInsideCount;
    }
}
