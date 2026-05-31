using System.Runtime.CompilerServices;
using System.Reflection;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.ConstructionSites;
using Timberborn.Coordinates;
using Timberborn.EnterableSystem;
using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Navigation;
using Timberborn.StatusSystem;
using Timberborn.TerrainPhysics;
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
    bool CanUseNativeConstructionRollback,
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
            $"total_damage_applied={TotalDamageApplied}";
    }
}

public readonly record struct TimberbornStructureBurnDamageRollbackEvidenceRequirement(
    string Behavior,
    IReadOnlyList<string> RequiredEvidenceTokens);

public static class TimberbornStructureBurnDamageRollbackEvidence
{
    public static readonly IReadOnlyList<TimberbornStructureBurnDamageRollbackEvidenceRequirement> LiveQaRequirements =
        new TimberbornStructureBurnDamageRollbackEvidenceRequirement[]
    {
        new(
            "repair-unlocks-structure-visuals",
            new[]
            {
                "wildfire_timberborn_structure_repair_unlocked",
                "wildfire_timberborn_structure_repair_completed_visual_restored",
            }),
        new(
            "status-icon-lane-stays-separate-from-burned-materials",
            new[]
            {
                "wildfire_timberborn_structure_burning_status_synchronized",
            }),
        new(
            "storage-rebuild-preserves-runtime-settings-and-suppresses-recovered-goods",
            new[]
            {
                "wildfire_timberborn_structure_burn_rebuilt_unfinished",
            }),
        new(
            "overlapping-path-and-collapse-dependents-rebuild-through-native-paths",
            new[]
            {
                "wildfire_timberborn_structure_overlapping_path_rebuilt_unfinished",
                "wildfire_timberborn_structure_native_structural_collapse_applied",
                "wildfire_timberborn_structure_collapse_dependent_rebuilt_unfinished",
            }),
    };
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

public interface ITimberbornStructureBurnRepairCompletionMaintenance
{
    int RestoreRepairCompletedStructures();
}

public interface ITimberbornStructureBurningStatusMaintenance
{
    int SynchronizeBurningStatuses(IReadOnlyCollection<string> activeRepairBlockedStableIds);
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
        (_targetApi as ITimberbornStructureBurnRepairCompletionMaintenance)?.RestoreRepairCompletedStructures();
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
        string[] activeRepairBlockedStableIds = outcomes
            .Where(static outcome => outcome.Request.RepairBlocked)
            .Select(static outcome => outcome.TargetStableId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        (_targetApi as ITimberbornStructureBurningStatusMaintenance)?.SynchronizeBurningStatuses(
            activeRepairBlockedStableIds);

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
                target.StableId,
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

        TimberbornStructureBurnRollbackStage stage = SelectStage(
            damageTaken,
            damageCapacity,
            target.CanUseNativeConstructionRollback);
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
                : new TimberbornStructureBurnDamageApplyResult(false, false, false, repairEligible);
        if (target.CanUseNativeConstructionRollback &&
            TimberbornStructureBurnDamageRollbackTargetApi.RequestsNativeConstructionPhase(request) &&
            !result.ConstructionPhaseEntered)
        {
            throw new InvalidOperationException(
                $"Structure burn damage rollback failed to enter native construction phase for {target.SpecId} at cell {target.CellIndex}.");
        }

        return new StructureApplyOutcome(
            target.StableId,
            IsZeroBurnableCapacity: false,
            MaterialValueLost: damageApplied,
            request,
            result);
    }

    private static TimberbornStructureBurnRollbackStage SelectStage(
        int damageTaken,
        int damageCapacity,
        bool canUseNativeConstructionRollback = true)
    {
        if (damageTaken <= 0 || damageCapacity <= 0)
        {
            return TimberbornStructureBurnRollbackStage.None;
        }

        return canUseNativeConstructionRollback
            ? TimberbornStructureBurnRollbackStage.Unfinished
            : TimberbornStructureBurnRollbackStage.Scorched;
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
            target is null)
        {
            return new ResolvedTarget(consequence, Target: null, BurnDamageState: null);
        }

        target = target with
        {
            StableId = state.TargetKey.StableId,
            SpecId = state.SpecId,
        };
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
        string TargetStableId,
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

public sealed class TimberbornStructureBurnDamageRollbackTargetApi :
    ITimberbornStructureBurnDamageRollbackTargetApi,
    ITimberbornStructureBurnRepairCompletionMaintenance,
    ITimberbornStructureBurningStatusMaintenance
{
    private readonly FireGrid _grid;
    private readonly IBlockService _blockService;
    private readonly EntityService? _entityService;
    private readonly ConstructionFactory? _constructionFactory;
    private readonly ITerrainPhysicsService? _terrainPhysicsService;
    private readonly TerrainDestroyer? _terrainDestroyer;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly TimberbornRuntimeBurnedTextureDeriver _textureDeriver;
    private readonly Dictionary<int, Material> _originalStructureMaterialsByBurnedInstanceId = new();
    private readonly Dictionary<string, Material> _originalStructureMaterialsByBurnedName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _burnedStructureCellIndexByStableId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _lastConstructionMaterialStockByStableId = new(StringComparer.Ordinal);
    private readonly HashSet<string> _repairWaitingForMaterialsLoggedStableIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _restoredStructureTextureStableIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, StructureBurningStatusBinding> _burningStatusTogglesByStableId =
        new(StringComparer.Ordinal);

    public TimberbornStructureBurnDamageRollbackTargetApi(
        FireGrid grid,
        IBlockService blockService,
        ITimberbornFireLogSink? logSink = null,
        TimberbornRuntimeBurnedTextureDeriver? textureDeriver = null,
        EntityService? entityService = null,
        ConstructionFactory? constructionFactory = null,
        ITerrainPhysicsService? terrainPhysicsService = null,
        TerrainDestroyer? terrainDestroyer = null)
    {
        _grid = grid;
        _blockService = blockService ?? throw new ArgumentNullException(nameof(blockService));
        _entityService = entityService;
        _constructionFactory = constructionFactory;
        _terrainPhysicsService = terrainPhysicsService;
        _terrainDestroyer = terrainDestroyer;
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
        _textureDeriver = textureDeriver ?? new TimberbornRuntimeBurnedTextureDeriver(_logSink);
    }

    public TimberbornStructureBurnDamageTarget? ResolveTarget(
        TimberbornStructureBurnDamageConsequence consequence)
    {
        (int x, int y, int z) = _grid.FromIndex(consequence.CellIndex);
        Vector3Int coordinates = new(x, y, z);
        BlockObject? blockObject = ResolveStructureBlockObject(coordinates);
        if (blockObject is null)
        {
            return null;
        }

        bool canUseNativeConstructionRollback = CanEnterUnfinishedState(blockObject);
        bool canClose = canUseNativeConstructionRollback || HasInterruptibleBuildingUse(blockObject);

        return new TimberbornStructureBurnDamageTarget(
            StableId: $"structure:{RuntimeHelpers.GetHashCode(blockObject)}",
            SpecId: blockObject.Name,
            CellIndex: consequence.CellIndex,
            ConstructionResources: TimberbornBurnDamageResourceGuesses.ForStructure(blockObject.Name),
            CanClose: canClose,
            CanApplyRollbackVisual: true,
            CanUseNativeConstructionRollback: canUseNativeConstructionRollback,
            CanRepairAfterDanger: canUseNativeConstructionRollback);
    }

    public TimberbornStructureBurnDamageApplyResult ApplyState(
        TimberbornStructureBurnDamageTarget target,
        TimberbornStructureBurnDamageApplyRequest request)
    {
        (int x, int y, int z) = _grid.FromIndex(target.CellIndex);
        Vector3Int coordinates = new(x, y, z);
        BlockObject? blockObject = ResolveStructureBlockObject(coordinates);
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
        int overlappingPathConstructionPhaseCount = 0;
        bool hasBurnedTextures = false;

        if (blockObject is not null && (request.ShouldApplyRollbackVisual || request.ShouldClose || request.RepairEligible))
        {
            ConstructionSite? constructionSite = null;
            enteredUnfinishedState = target.CanUseNativeConstructionRollback &&
                    (request.ShouldClose || RequestsNativeConstructionPhase(request))
                ? TryApplyNativeConstructionPhase(
                    blockObject,
                    target,
                    out blockObject,
                    out constructionSite,
                    out overlappingPathConstructionPhaseCount)
                : false;

            constructionSite ??= TryGetConstructionSite(blockObject, coordinates);
            SynchronizeBurningStatus(blockObject, target, request.RepairBlocked);
            float remainingConstructionFraction = CalculateRemainingConstructionFraction(request);
            bool shouldSynchronizeConstructionState = ShouldSynchronizeConstructionState(
                request,
                enteredUnfinishedState,
                target.CanUseNativeConstructionRollback);
            removedConstructionMaterialCount = shouldSynchronizeConstructionState && request.RepairBlocked
                ? SynchronizeConstructionMaterials(constructionSite, target, remainingConstructionFraction)
                : 0;
            int constructionMaterialStock = TotalConstructionMaterialStock(constructionSite, target);
            if (request.RepairBlocked)
            {
                _burnedStructureCellIndexByStableId[target.StableId] = target.CellIndex;
                _lastConstructionMaterialStockByStableId[target.StableId] = constructionMaterialStock;
                _repairWaitingForMaterialsLoggedStableIds.Remove(target.StableId);
                _restoredStructureTextureStableIds.Remove(target.StableId);
            }

            resetConstructionProgress = shouldSynchronizeConstructionState &&
                target.CanRepairAfterDanger &&
                request.RepairBlocked &&
                TrySetConstructionProgress(constructionSite, remainingConstructionFraction);
            burnedMaterialCount = request.ShouldApplyRollbackVisual
                ? ApplyBurnedTextures(blockObject, target.SpecId)
                : 0;
            if (burnedMaterialCount > 0)
            {
                _burnedStructureCellIndexByStableId[target.StableId] = target.CellIndex;
            }

            if (request.RepairEligible)
            {
                SynchronizeBurningStatus(blockObject, target, isBurning: false);
                int lastConstructionMaterialStock = _lastConstructionMaterialStockByStableId.GetValueOrDefault(
                    target.StableId,
                    constructionMaterialStock);
                bool constructionMaterialAddedAfterFire = constructionMaterialStock > lastConstructionMaterialStock;
                bool hasRepairMaterial = constructionMaterialStock > 0;
                if ((constructionMaterialAddedAfterFire || hasRepairMaterial) &&
                    _restoredStructureTextureStableIds.Add(target.StableId))
                {
                    int restoredMaterialCount = RestoreBurnedTextures(blockObject);
                    LogRepairUnlocked(
                        target,
                        restoredMaterialCount,
                        lastConstructionMaterialStock,
                        constructionMaterialStock);
                    _lastConstructionMaterialStockByStableId[target.StableId] = constructionMaterialStock;
                }
                else if (_repairWaitingForMaterialsLoggedStableIds.Add(target.StableId))
                {
                    LogRepairWaitingForMaterials(
                        target,
                        lastConstructionMaterialStock,
                        constructionMaterialStock);
                }
            }

            hasBurnedTextures = burnedMaterialCount > 0 || HasBurnedTextures(blockObject);
            LogRollbackVisual(
                target,
                request,
                enteredUnfinishedState,
                removedConstructionMaterialCount,
                resetConstructionProgress,
                burnedMaterialCount,
                overlappingPathConstructionPhaseCount);
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

        bool visualRollbackApplied = enteredUnfinishedState ||
            removedConstructionMaterialCount > 0 ||
            overlappingPathConstructionPhaseCount > 0 ||
            hasBurnedTextures;
        bool nativeConstructionRollbackFailed = target.CanUseNativeConstructionRollback &&
            RequestsNativeConstructionPhase(request) &&
            !enteredUnfinishedState;
        if (nativeConstructionRollbackFailed)
        {
            throw new InvalidOperationException(
                $"Structure burn damage rollback failed to enter native construction phase for {target.SpecId} at cell {target.CellIndex}.");
        }

        if (request.ShouldClose && !closed && target.CanUseNativeConstructionRollback)
        {
            throw new InvalidOperationException(
                $"Structure burn damage rollback failed to close {target.SpecId} at cell {target.CellIndex}.");
        }

        return new TimberbornStructureBurnDamageApplyResult(
            Closed: closed,
            VisualRollbackApplied: visualRollbackApplied,
            ConstructionPhaseEntered: enteredUnfinishedState,
            RepairEligible: request.RepairEligible);
    }

    public int RestoreRepairCompletedStructures()
    {
        StructureRepairCompletion[] completedStructures = _burnedStructureCellIndexByStableId
            .Select(entry => new StructureRepairCompletion(
                StableId: entry.Key,
                CellIndex: entry.Value,
                BlockObject: ResolveBlockObject(entry.Value)))
            .Where(static completion => completion.BlockObject?.IsFinished == true)
            .ToArray();
        int restoredMaterialCount = completedStructures
            .Select(RestoreRepairCompletedStructure)
            .Sum();
        completedStructures
            .Select(static completion => completion.StableId)
            .ToList()
            .ForEach(stableId =>
            {
                _burnedStructureCellIndexByStableId.Remove(stableId);
                _lastConstructionMaterialStockByStableId.Remove(stableId);
                _repairWaitingForMaterialsLoggedStableIds.Remove(stableId);
                _restoredStructureTextureStableIds.Remove(stableId);
                if (_burningStatusTogglesByStableId.Remove(stableId, out StructureBurningStatusBinding binding))
                {
                    binding.StatusToggle.Deactivate();
                }
        });
        return restoredMaterialCount;
    }

    public int SynchronizeBurningStatuses(IReadOnlyCollection<string> activeRepairBlockedStableIds)
    {
        HashSet<string> activeStableIds = activeRepairBlockedStableIds.ToHashSet(StringComparer.Ordinal);
        string[] inactiveStableIds = _burningStatusTogglesByStableId.Keys
            .Where(stableId => !activeStableIds.Contains(stableId))
            .ToArray();
        inactiveStableIds
            .Select(stableId => _burningStatusTogglesByStableId[stableId])
            .ToList()
            .ForEach(static binding => binding.StatusToggle.Deactivate());
        inactiveStableIds
            .ToList()
            .ForEach(stableId => _burningStatusTogglesByStableId.Remove(stableId));
        if (inactiveStableIds.Length > 0)
        {
            _logSink.Info(
                "wildfire_timberborn_structure_burning_status_synchronized " +
                $"active_repair_blocked={activeStableIds.Count} " +
                $"deactivated={inactiveStableIds.Length}");
        }

        return inactiveStableIds.Length;
    }

    private int RestoreRepairCompletedStructure(StructureRepairCompletion completion)
    {
        if (completion.BlockObject is null)
        {
            return 0;
        }

        int restoredMaterialCount = RestoreBurnedTextures(completion.BlockObject);
        _logSink.Info(
            "wildfire_timberborn_structure_repair_completed_visual_restored " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(completion.StableId)} " +
            $"cell={completion.CellIndex} " +
            $"restored_materials={restoredMaterialCount}");
        return restoredMaterialCount;
    }

    private BlockObject? ResolveBlockObject(int cellIndex)
    {
        (int x, int y, int z) = _grid.FromIndex(cellIndex);
        Vector3Int coordinates = new(x, y, z);
        return ResolveStructureBlockObject(coordinates);
    }

    private BlockObject? ResolveStructureBlockObject(Vector3Int coordinates)
    {
        return _blockService
            .GetObjectsWithComponentAt<BlockObject>(coordinates)
            .Where(static candidate => !IsInfrastructureLikeName(candidate.Name))
            .Where(static candidate => !IsRecoverableGoodStackName(candidate.Name))
            .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
            .FirstOrDefault();
    }

    private readonly record struct StructureRepairCompletion(
        string StableId,
        int CellIndex,
        BlockObject? BlockObject);

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

    public static bool IsStorageLikeName(string name)
    {
        return name.Contains("Warehouse", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Storage", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Stockpile", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Pile", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Tank", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRecoverableGoodStackName(string name)
    {
        return name.Contains("RecoveredGoodStack", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("RecoverableGoodStack", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("GoodStack", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("GoodStack(", StringComparison.OrdinalIgnoreCase);
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
        if (IsDistrictCenterName(blockObject.Name) ||
            IsStorageLikeName(blockObject.Name) ||
            IsRecoverableGoodStackName(blockObject.Name))
        {
            return false;
        }

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

    private bool TryApplyNativeConstructionPhase(
        BlockObject blockObject,
        TimberbornStructureBurnDamageTarget target,
        out BlockObject activeBlockObject,
        out ConstructionSite? constructionSite,
        out int overlappingPathConstructionPhaseCount)
    {
        constructionSite = null;
        activeBlockObject = blockObject;
        overlappingPathConstructionPhaseCount = 0;
        return TryRebuildStructureAsUnfinished(
            blockObject,
            target,
            out activeBlockObject,
            out constructionSite,
            out overlappingPathConstructionPhaseCount);
    }

    private bool TryRebuildStructureAsUnfinished(
        BlockObject blockObject,
        TimberbornStructureBurnDamageTarget target,
        out BlockObject rebuiltBlockObject,
        out ConstructionSite? rebuiltConstructionSite,
        out int overlappingPathConstructionPhaseCount)
    {
        rebuiltBlockObject = blockObject;
        rebuiltConstructionSite = null;
        overlappingPathConstructionPhaseCount = 0;
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

            StructuralCollapseResult structuralCollapse = ApplyNativeStructuralCollapseDependents(blockObject, target);
            BlockObject[] overlappingPathInfrastructure = structuralCollapse.RebuiltBlockObjectCount == 0
                ? ResolveOverlappingPathInfrastructure(blockObject).ToArray()
                : Array.Empty<BlockObject>();
            if (!blockObject.TryGetComponent(out Building building) || building.Spec is null)
            {
                throw new InvalidOperationException($"Native construction rebuild cannot resolve building spec for {blockObject.Name}.");
            }

            StructureRuntimeSettingsSnapshot runtimeSettingsSnapshot = CaptureRuntimeSettings(blockObject);
            int disabledRecoverableGoodProviders = DisableRecoverableGoodProviders(blockObject);
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
            overlappingPathConstructionPhaseCount = structuralCollapse.RebuiltBlockObjectCount +
                RebuildOverlappingPathInfrastructureAsUnfinished(
                target,
                overlappingPathInfrastructure);
            runtimeSettingsSnapshot.ApplyTo(rebuiltBlockObject);
            if (!rebuiltBlockObject.IsUnfinished)
            {
                throw new InvalidOperationException($"Native construction rebuild recreated {target.SpecId} outside unfinished state.");
            }

            _logSink.Info(
                "wildfire_timberborn_structure_burn_rebuilt_unfinished " +
                $"stable_id={TimberbornQaCommandBridge.FormatToken(target.StableId)} " +
                $"target={TimberbornQaCommandBridge.FormatToken(target.SpecId)} " +
                $"district_center={isDistrictCenter.ToString().ToLowerInvariant()} " +
                $"structural_collapse_rebuilt_dependents={structuralCollapse.RebuiltBlockObjectCount} " +
                $"structural_collapse_destroyed_terrain={structuralCollapse.DestroyedTerrainCount} " +
                $"recoverable_good_providers_disabled={disabledRecoverableGoodProviders} " +
                $"runtime_settings_restored={runtimeSettingsSnapshot.HasSettings.ToString().ToLowerInvariant()} " +
                $"cell={target.CellIndex}");
            return true;
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException("Native construction rebuild failed.", exception);
        }
    }

    private StructuralCollapseResult ApplyNativeStructuralCollapseDependents(
        BlockObject compromisedBlockObject,
        TimberbornStructureBurnDamageTarget target)
    {
        if (_terrainPhysicsService is null)
        {
            return StructuralCollapseResult.Empty;
        }

        if (_entityService is null || _constructionFactory is null)
        {
            throw new InvalidOperationException("Native structural collapse requires Timberborn entity and construction services.");
        }

        HashSet<Vector3Int> terrainToDestroy = new();
        HashSet<BlockObject> blockObjectsToCollapse = new();
        _terrainPhysicsService.GetTerrainAndBlockObjectStack(
            new[] { compromisedBlockObject },
            terrainToDestroy,
            blockObjectsToCollapse);

        int compromisedObjectHash = RuntimeHelpers.GetHashCode(compromisedBlockObject);
        StructuralCollapseBlockObjectSnapshot[] dependentSnapshots = blockObjectsToCollapse
            .Where(candidate => RuntimeHelpers.GetHashCode(candidate) != compromisedObjectHash)
            .GroupBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
            .Select(static group => group.First())
            .Where(static candidate => candidate.IsFinished)
            .Select(CreateStructuralCollapseSnapshot)
            .ToArray();
        Vector3Int[] terrain = terrainToDestroy
            .OrderByDescending(static coordinates => coordinates.z)
            .ThenBy(static coordinates => coordinates.x)
            .ThenBy(static coordinates => coordinates.y)
            .ToArray();

        if (dependentSnapshots.Length == 0 && terrain.Length == 0)
        {
            return StructuralCollapseResult.Empty;
        }

        if (terrain.Length > 0 && _terrainDestroyer is null)
        {
            throw new InvalidOperationException("Native structural collapse found terrain but TerrainDestroyer is unavailable.");
        }

        int deletedBlockObjects = dependentSnapshots
            .OrderByDescending(static snapshot => snapshot.BaseZ)
            .ThenBy(static snapshot => snapshot.StableHash)
            .Select(snapshot =>
            {
                _entityService.Delete(snapshot.BlockObject);
                return 1;
            })
            .Sum();
        int destroyedTerrain = terrain
            .Select(coordinates =>
            {
                _terrainDestroyer?.DestroyTerrain(coordinates);
                return 1;
            })
            .Sum();
        int rebuiltBlockObjects = dependentSnapshots
            .OrderBy(static snapshot => snapshot.BaseZ)
            .ThenBy(static snapshot => snapshot.StableHash)
            .Select(snapshot => RecreateStructuralCollapsePlan(target, snapshot) ? 1 : 0)
            .Sum();

        _logSink.Info(
            "wildfire_timberborn_structure_native_structural_collapse_applied " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(target.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(target.SpecId)} " +
            $"deleted_dependents={deletedBlockObjects} " +
            $"rebuilt_dependents_unfinished={rebuiltBlockObjects} " +
            $"destroyed_terrain={destroyedTerrain} " +
            $"cell={target.CellIndex}");
        return new StructuralCollapseResult(rebuiltBlockObjects, destroyedTerrain);
    }

    private static StructuralCollapseBlockObjectSnapshot CreateStructuralCollapseSnapshot(BlockObject blockObject)
    {
        if (!blockObject.TryGetComponent(out Building building) || building.Spec is null)
        {
            throw new InvalidOperationException($"Native structural collapse cannot resolve building spec for dependent {blockObject.Name}.");
        }

        return new StructuralCollapseBlockObjectSnapshot(
            RuntimeHelpers.GetHashCode(blockObject),
            blockObject.Name,
            blockObject,
            building.Spec,
            blockObject.Placement,
            blockObject.BaseZ,
            CaptureRuntimeSettings(blockObject));
    }

    private bool RecreateStructuralCollapsePlan(
        TimberbornStructureBurnDamageTarget target,
        StructuralCollapseBlockObjectSnapshot snapshot)
    {
        ConstructionSite constructionSite = _constructionFactory!.CreateAsUnfinished(snapshot.Spec, snapshot.Placement);
        BlockObject rebuiltBlockObject = constructionSite.GetComponent<BlockObject>();
        snapshot.RuntimeSettings.ApplyTo(rebuiltBlockObject);
        if (!rebuiltBlockObject.IsUnfinished)
        {
            throw new InvalidOperationException($"Native structural collapse recreated {snapshot.SpecId} outside unfinished state.");
        }

        _logSink.Info(
            "wildfire_timberborn_structure_collapse_dependent_rebuilt_unfinished " +
            $"structure_stable_id={TimberbornQaCommandBridge.FormatToken(target.StableId)} " +
            $"structure={TimberbornQaCommandBridge.FormatToken(target.SpecId)} " +
            $"dependent={TimberbornQaCommandBridge.FormatToken(snapshot.SpecId)} " +
            $"runtime_settings_restored={snapshot.RuntimeSettings.HasSettings.ToString().ToLowerInvariant()} " +
            $"cell={target.CellIndex}");
        return true;
    }

    private IEnumerable<BlockObject> ResolveOverlappingPathInfrastructure(BlockObject structureBlockObject)
    {
        Vector3Int[] structureCoordinates = structureBlockObject.PositionedBlocks
            .GetOccupiedCoordinates()
            .ToArray();
        IEnumerable<Vector3Int> sameVolumeCoordinates = structureCoordinates;
        IEnumerable<Vector3Int> overheadFootprintCoordinates = ResolveOverheadFootprintCoordinates(structureCoordinates);
        return sameVolumeCoordinates
            .Concat(overheadFootprintCoordinates)
            .SelectMany(coordinates => _blockService.GetObjectsWithComponentAt<BlockObject>(coordinates))
            .Where(static candidate => IsPathInfrastructureName(candidate.Name))
            .Where(candidate => RuntimeHelpers.GetHashCode(candidate) != RuntimeHelpers.GetHashCode(structureBlockObject))
            .GroupBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
            .Select(static group => group.First())
            .Where(static candidate => candidate.IsFinished);
    }

    private IEnumerable<Vector3Int> ResolveOverheadFootprintCoordinates(IReadOnlyList<Vector3Int> structureCoordinates)
    {
        if (structureCoordinates.Count == 0)
        {
            return Array.Empty<Vector3Int>();
        }

        int minZ = structureCoordinates.Min(static coordinates => coordinates.z);
        int maxZ = Math.Min(_grid.Depth - 1, structureCoordinates.Max(static coordinates => coordinates.z) + 8);
        return structureCoordinates
            .GroupBy(static coordinates => (coordinates.x, coordinates.y))
            .Select(static group => group.Key)
            .SelectMany(column => Enumerable
                .Range(minZ, maxZ - minZ + 1)
                .Select(z => new Vector3Int(column.x, column.y, z)));
    }

    private int RebuildOverlappingPathInfrastructureAsUnfinished(
        TimberbornStructureBurnDamageTarget structureTarget,
        IReadOnlyList<BlockObject> pathInfrastructure)
    {
        if (pathInfrastructure.Count == 0)
        {
            return 0;
        }

        if (_entityService is null || _constructionFactory is null)
        {
            throw new InvalidOperationException("Overlapping path construction rollback requires Timberborn entity and construction services.");
        }

        return pathInfrastructure
            .Select(path => TryRebuildOverlappingPathInfrastructureAsUnfinished(structureTarget, path) ? 1 : 0)
            .Sum();
    }

    private bool TryRebuildOverlappingPathInfrastructureAsUnfinished(
        TimberbornStructureBurnDamageTarget structureTarget,
        BlockObject pathBlockObject)
    {
        if (pathBlockObject.IsUnfinished)
        {
            return true;
        }

        if (!pathBlockObject.IsFinished)
        {
            return false;
        }

        if (!pathBlockObject.TryGetComponent(out Building pathBuilding) || pathBuilding.Spec is null)
        {
            throw new InvalidOperationException($"Overlapping path construction rollback cannot resolve building spec for {pathBlockObject.Name}.");
        }

        string pathSpecId = pathBlockObject.Name;
        Placement placement = pathBlockObject.Placement;
        _entityService!.Delete(pathBlockObject);
        ConstructionSite constructionSite = _constructionFactory!.CreateAsUnfinished(pathBuilding.Spec, placement);
        BlockObject rebuiltPathBlockObject = constructionSite.GetComponent<BlockObject>();
        if (!rebuiltPathBlockObject.IsUnfinished)
        {
            throw new InvalidOperationException($"Overlapping path construction rollback recreated {pathSpecId} outside unfinished state.");
        }

        _logSink.Info(
            "wildfire_timberborn_structure_overlapping_path_rebuilt_unfinished " +
            $"structure_stable_id={TimberbornQaCommandBridge.FormatToken(structureTarget.StableId)} " +
            $"structure={TimberbornQaCommandBridge.FormatToken(structureTarget.SpecId)} " +
            $"path={TimberbornQaCommandBridge.FormatToken(pathSpecId)} " +
            $"cell={structureTarget.CellIndex}");
        return true;
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

    private static int TotalConstructionMaterialStock(
        ConstructionSite? constructionSite,
        TimberbornStructureBurnDamageTarget target)
    {
        if (constructionSite?.Inventory is null || target.ConstructionResources.Count == 0)
        {
            return 0;
        }

        return target.ConstructionResources
            .Select(static resource => resource.ResourceId)
            .Distinct(StringComparer.Ordinal)
            .Sum(resourceId => Math.Max(0, constructionSite.Inventory.AmountInStock(resourceId)));
    }

    private static bool TrySetConstructionProgress(ConstructionSite? constructionSite, float remainingConstructionFraction)
    {
        if (constructionSite is null)
        {
            return false;
        }

        try
        {
            object? buildTimeCalculator = constructionSite.GetType()
                .GetField(
                    "_constructionSiteBuildTimeCalculator",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(constructionSite);
            MethodInfo? getConstructionTimeMethod = buildTimeCalculator
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
                buildTimeCalculator,
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

    public static bool ShouldSynchronizeConstructionState(
        TimberbornStructureBurnDamageApplyRequest request,
        bool enteredUnfinishedState)
    {
        return ShouldSynchronizeConstructionState(
            request,
            enteredUnfinishedState,
            canUseNativeConstructionRollback: true);
    }

    public static bool ShouldSynchronizeConstructionState(
        TimberbornStructureBurnDamageApplyRequest request,
        bool enteredUnfinishedState,
        bool canUseNativeConstructionRollback)
    {
        return enteredUnfinishedState || (canUseNativeConstructionRollback && request.ShouldApplyRollbackVisual);
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
            .GetComponentsInChildren<Renderer>(includeInactive: false)
            .Where(IsStructureVisualRenderer)
            .Sum(renderer => ApplyBurnedTextures(renderer, textureLabel));
    }

    private static bool HasBurnedTextures(BlockObject blockObject)
    {
        return blockObject.Transform
            .GetComponentsInChildren<Renderer>(includeInactive: true)
            .Where(IsStructureVisualRenderer)
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

    private Material CreateBurnedMaterial(Material source, Texture burnedTexture)
    {
        Material material = new(source)
        {
            name = $"{source.name} Wildfire Burned",
            mainTexture = burnedTexture,
            hideFlags = HideFlags.HideAndDontSave,
        };
        _originalStructureMaterialsByBurnedInstanceId[material.GetInstanceID()] = source;
        _originalStructureMaterialsByBurnedName[material.name] = source;
        return material;
    }

    private Material CreateBurnedTintMaterial(Material source)
    {
        Material material = new(source)
        {
            name = $"{source.name} Wildfire Burned",
            hideFlags = HideFlags.HideAndDontSave,
        };
        SetColorIfPresent(material, "_BaseColor", new Color(0.18f, 0.16f, 0.14f, 1f));
        SetColorIfPresent(material, "_Color", new Color(0.18f, 0.16f, 0.14f, 1f));
        _originalStructureMaterialsByBurnedInstanceId[material.GetInstanceID()] = source;
        _originalStructureMaterialsByBurnedName[material.name] = source;
        return material;
    }

    private int RestoreBurnedTextures(BlockObject blockObject)
    {
        return blockObject.Transform
            .GetComponentsInChildren<Renderer>(includeInactive: true)
            .Where(IsStructureVisualRenderer)
            .Sum(RestoreBurnedTextures);
    }

    private int RestoreBurnedTextures(Renderer renderer)
    {
        Material?[] materials = renderer.sharedMaterials;
        Material?[] restoredMaterials = materials
            .Select(RestoreBurnedMaterialOrOriginal)
            .ToArray();
        int restoredMaterialCount = Enumerable.Range(0, materials.Length)
            .Count(index => !ReferenceEquals(materials[index], restoredMaterials[index]));

        if (restoredMaterialCount > 0)
        {
            renderer.sharedMaterials = restoredMaterials;
        }

        return restoredMaterialCount;
    }

    private Material? RestoreBurnedMaterialOrOriginal(Material? material)
    {
        if (material is null || !IsBurnedMaterial(material))
        {
            return material;
        }

        if (_originalStructureMaterialsByBurnedInstanceId.TryGetValue(material.GetInstanceID(), out Material? original))
        {
            return original;
        }

        return _originalStructureMaterialsByBurnedName.TryGetValue(material.name, out Material? originalByName)
            ? originalByName
            : material;
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

    private static bool IsStructureVisualRenderer(Renderer renderer)
    {
        if (renderer is SpriteRenderer or LineRenderer or TrailRenderer or ParticleSystemRenderer)
        {
            return false;
        }

        string path = TransformPath(renderer.transform);
        string[] skippedPathTokens =
        {
            "Status",
            "Icon",
            "Notification",
            "Marker",
            "Highlight",
            "Selection",
            "Preview",
            "Guideline",
            "Floating",
        };
        return !skippedPathTokens.Any(token => path.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string TransformPath(Transform transform)
    {
        return transform.parent is null
            ? transform.name
            : $"{TransformPath(transform.parent)}/{transform.name}";
    }

    private void SynchronizeBurningStatus(
        BlockObject blockObject,
        TimberbornStructureBurnDamageTarget target,
        bool isBurning)
    {
        if (!blockObject.TryGetComponent(out StatusSubject statusSubject))
        {
            return;
        }

        StatusToggle statusToggle = GetOrCreateBurningStatusToggle(target.StableId, statusSubject);
        if (isBurning)
        {
            statusToggle.Activate();
            return;
        }

        statusToggle.Deactivate();
    }

    private StatusToggle GetOrCreateBurningStatusToggle(string stableId, StatusSubject statusSubject)
    {
        if (_burningStatusTogglesByStableId.TryGetValue(stableId, out StructureBurningStatusBinding binding) &&
            ReferenceEquals(binding.StatusSubject, statusSubject))
        {
            return binding.StatusToggle;
        }

        StatusToggle createdStatusToggle = StatusToggle.CreatePriorityStatusWithFloatingIcon(
            "WildfireBurningStatus",
            "Building is burning",
            delayInHours: 0f);
        statusSubject.RegisterStatus(createdStatusToggle);
        _burningStatusTogglesByStableId[stableId] = new StructureBurningStatusBinding(
            statusSubject,
            createdStatusToggle);
        return createdStatusToggle;
    }

    private readonly record struct StructureBurningStatusBinding(
        StatusSubject StatusSubject,
        StatusToggle StatusToggle);

    private static int DisableRecoverableGoodProviders(BlockObject blockObject)
    {
        object[] transformComponents = blockObject.Transform
            .GetComponentsInChildren<Component>(includeInactive: true)
            .Where(static component => component is not null)
            .Cast<object>()
            .ToArray();
        return blockObject.AllComponents
            .Cast<object>()
            .Concat(transformComponents)
            .GroupBy(static component => RuntimeHelpers.GetHashCode(component))
            .Select(static group => group.First())
            .Where(static component =>
                component.GetType().FullName == "Timberborn.RecoverableGoodSystem.RecoverableGoodProvider")
            .Select(static component => TryInvokeNoArgumentMethod(component, "DisableGoodRecovery") ? 1 : 0)
            .Sum();
    }

    private static StructureRuntimeSettingsSnapshot CaptureRuntimeSettings(BlockObject blockObject)
    {
        object[] manufactorySnapshots = blockObject.AllComponents
            .Where(static component => component.GetType().FullName == "Timberborn.Workshops.Manufactory")
            .Select(ManufactoryRuntimeSettingsSnapshot.Capture)
            .Where(static snapshot => snapshot.HasSettings)
            .Cast<object>()
            .ToArray();
        InventoryRuntimeSettingsSnapshot[] inventorySnapshots = blockObject.AllComponents
            .OfType<Inventory>()
            .Select(InventoryRuntimeSettingsSnapshot.Capture)
            .ToArray();
        return new StructureRuntimeSettingsSnapshot(manufactorySnapshots, inventorySnapshots);
    }

    private static bool TryInvokeNoArgumentMethod(object target, string methodName)
    {
        try
        {
            MethodInfo? method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                Type.EmptyTypes,
                modifiers: null);
            if (method is null)
            {
                return false;
            }

            method.Invoke(target, null);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private readonly record struct StructureRuntimeSettingsSnapshot(
        object[] ManufactorySnapshots,
        InventoryRuntimeSettingsSnapshot[] InventorySnapshots)
    {
        public bool HasSettings => ManufactorySnapshots.Length > 0 || InventorySnapshots.Length > 0;

        public void ApplyTo(BlockObject blockObject)
        {
            ManufactorySnapshots
                .OfType<ManufactoryRuntimeSettingsSnapshot>()
                .ToList()
                .ForEach(snapshot => snapshot.ApplyTo(blockObject));
            InventoryRuntimeSettingsSnapshot.ApplyAll(InventorySnapshots, blockObject);
        }
    }

    private readonly record struct ManufactoryRuntimeSettingsSnapshot(
        object? CurrentRecipe,
        float? FuelRemaining,
        float? ProductionProgress)
    {
        public bool HasSettings => CurrentRecipe is not null || FuelRemaining.HasValue || ProductionProgress.HasValue;

        public static ManufactoryRuntimeSettingsSnapshot Capture(object manufactory)
        {
            Type type = manufactory.GetType();
            return new ManufactoryRuntimeSettingsSnapshot(
                CurrentRecipe: type.GetProperty("CurrentRecipe", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(manufactory),
                FuelRemaining: TryGetFloatProperty(manufactory, type, "FuelRemaining"),
                ProductionProgress: TryGetFloatProperty(manufactory, type, "ProductionProgress"));
        }

        public void ApplyTo(BlockObject blockObject)
        {
            object? manufactory = blockObject.AllComponents
                .FirstOrDefault(static component => component.GetType().FullName == "Timberborn.Workshops.Manufactory");
            if (manufactory is null)
            {
                return;
            }

            Type type = manufactory.GetType();
            if (CurrentRecipe is not null)
            {
                MethodInfo? setRecipeMethod = type.GetMethod(
                    "SetRecipe",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                setRecipeMethod?.Invoke(manufactory, new[] { CurrentRecipe });
            }

            TrySetFloatProperty(manufactory, type, "FuelRemaining", FuelRemaining);
            TrySetFloatProperty(manufactory, type, "ProductionProgress", ProductionProgress);
        }

        private static float? TryGetFloatProperty(object target, Type type, string propertyName)
        {
            try
            {
                object? value = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.GetValue(target);
                return value is null ? null : Convert.ToSingle(value);
            }
            catch
            {
                return null;
            }
        }

        private static void TrySetFloatProperty(object target, Type type, string propertyName, float? value)
        {
            if (!value.HasValue)
            {
                return;
            }

            try
            {
                PropertyInfo? property = type.GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                property?.SetValue(target, value.Value);
            }
            catch
            {
                // Best-effort state preservation; failed settings should not block fire aftermath.
            }
        }
    }

    private readonly record struct InventoryRuntimeSettingsSnapshot(
        string ComponentName,
        int Capacity,
        bool PublicInput,
        bool PublicOutput,
        bool IgnorableCapacity,
        object? GoodDisallower,
        StorableGoodAmount[] AllowedGoods)
    {
        public static InventoryRuntimeSettingsSnapshot Capture(Inventory inventory)
        {
            return new InventoryRuntimeSettingsSnapshot(
                inventory.ComponentName,
                inventory.Capacity,
                inventory.PublicInput,
                inventory.PublicOutput,
                IgnorableCapacity: TryGetPrivateBoolField(inventory, "_ignorableCapacity"),
                GoodDisallower: TryGetPrivateField<object>(inventory, "_goodDisallower"),
                AllowedGoods: inventory.AllowedGoods.ToArray());
        }

        public static void ApplyAll(InventoryRuntimeSettingsSnapshot[] snapshots, BlockObject blockObject)
        {
            Inventory[] inventories = blockObject.AllComponents.OfType<Inventory>().ToArray();
            snapshots
                .Select(snapshot => (
                    Snapshot: snapshot,
                    Inventory: inventories.FirstOrDefault(inventory =>
                        string.Equals(inventory.ComponentName, snapshot.ComponentName, StringComparison.Ordinal))))
                .Where(static pair => pair.Inventory is not null)
                .ToList()
                .ForEach(static pair => pair.Snapshot.ApplyTo(pair.Inventory!));
        }

        private void ApplyTo(Inventory inventory)
        {
            if (AllowedGoods.Length == 0)
            {
                return;
            }

            try
            {
                MethodInfo? initializeMethod = typeof(Inventory).GetMethod(
                    "Initialize",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                initializeMethod?.Invoke(
                    inventory,
                    new object?[]
                    {
                        ComponentName,
                        Capacity,
                        AllowedGoods,
                        PublicInput,
                        PublicOutput,
                        IgnorableCapacity,
                        GoodDisallower,
                    });
            }
            catch
            {
                // Best-effort storage policy preservation; stock burning/removal must remain authoritative.
            }
        }

        private static T? TryGetPrivateField<T>(object target, string fieldName)
        {
            try
            {
                object? value = target.GetType()
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(target);
                return value is T typedValue ? typedValue : default;
            }
            catch
            {
                return default;
            }
        }

        private static bool TryGetPrivateBoolField(object target, string fieldName)
        {
            try
            {
                object? value = target.GetType()
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(target);
                return value is bool typedValue && typedValue;
            }
            catch
            {
                return false;
            }
        }
    }

    private readonly record struct StructuralCollapseResult(
        int RebuiltBlockObjectCount,
        int DestroyedTerrainCount)
    {
        public static readonly StructuralCollapseResult Empty = new(
            RebuiltBlockObjectCount: 0,
            DestroyedTerrainCount: 0);
    }

    private sealed record StructuralCollapseBlockObjectSnapshot(
        int StableHash,
        string SpecId,
        BlockObject BlockObject,
        BuildingSpec Spec,
        Placement Placement,
        int BaseZ,
        StructureRuntimeSettingsSnapshot RuntimeSettings);

    private void LogRollbackVisual(
        TimberbornStructureBurnDamageTarget target,
        TimberbornStructureBurnDamageApplyRequest request,
        bool enteredUnfinishedState,
        int removedConstructionMaterialCount,
        bool resetConstructionProgress,
        int burnedMaterialCount,
        int overlappingPathConstructionPhaseCount)
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
            $"overlapping_paths_rebuilt_unfinished={overlappingPathConstructionPhaseCount} " +
            $"materials={burnedMaterialCount}");
    }

    private void LogRepairUnlocked(
        TimberbornStructureBurnDamageTarget target,
        int restoredMaterialCount,
        int previousConstructionMaterialStock,
        int constructionMaterialStock)
    {
        _logSink.Info(
            "wildfire_timberborn_structure_repair_unlocked " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(target.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(target.SpecId)} " +
            $"restored_materials={restoredMaterialCount} " +
            $"previous_construction_materials={previousConstructionMaterialStock} " +
            $"construction_materials={constructionMaterialStock}");
    }

    private void LogRepairWaitingForMaterials(
        TimberbornStructureBurnDamageTarget target,
        int previousConstructionMaterialStock,
        int constructionMaterialStock)
    {
        _logSink.Info(
            "wildfire_timberborn_structure_repair_waiting_for_materials " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(target.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(target.SpecId)} " +
            $"previous_construction_materials={previousConstructionMaterialStock} " +
            $"construction_materials={constructionMaterialStock}");
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

    private static bool IsPathInfrastructureName(string name)
    {
        return name.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Platform", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Bridge", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Stair", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Overhang", StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct StructureOccupancySnapshot(int AssignedWorkerCount, int EnterersInsideCount)
    {
        public static readonly StructureOccupancySnapshot Empty = new(0, 0);

        public int CandidateCount => AssignedWorkerCount + EnterersInsideCount;
    }
}
