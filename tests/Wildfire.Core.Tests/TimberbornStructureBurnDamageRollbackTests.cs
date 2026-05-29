using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class TimberbornStructureBurnDamageRollbackTests
{
    [Fact]
    public void SinkClosesDamagedStructureAndBlocksRepairWhileDangerous()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 2)],
            canClose: true,
            canRepairAfterDanger: true));
        TimberbornStructureBurnDamageRollbackSink sink = new(targetApi);

        TimberbornStructureBurnDamageRollbackSummary summary = sink.ApplyConsequences(
            1,
            [Decision(4, oldFuel: 8, newFuel: 4, heat: 10)]);

        Assert.Equal(1, summary.MatchedStructureCellCount);
        Assert.Equal(1, summary.ClosedStructureCount);
        Assert.Equal(1, summary.RepairBlockedCount);
        Assert.Equal(0, summary.RepairEligibleCount);
        Assert.Equal(4, summary.MaterialValueLost);
        TimberbornStructureBurnDamageApplyRequest request = Assert.Single(targetApi.Requests);
        Assert.True(request.ShouldClose);
        Assert.True(request.RepairBlocked);
    }

    [Fact]
    public void SinkClosesStructureOnDangerHeatBeforeDamageThreshold()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 10)],
            canClose: true,
            canApplyRollbackVisual: true));
        TimberbornStructureBurnDamageRollbackSink sink = new(targetApi);

        TimberbornStructureBurnDamageRollbackSummary summary = sink.ApplyConsequences(
            1,
            [Decision(4, oldFuel: 10, newFuel: 10, heat: 5)]);

        Assert.Equal(1, summary.ClosedStructureCount);
        Assert.Equal(1, summary.RepairBlockedCount);
        TimberbornStructureBurnDamageApplyRequest request = Assert.Single(targetApi.Requests);
        Assert.True(request.ShouldClose);
        Assert.False(request.ShouldApplyRollbackVisual);
        Assert.Equal(TimberbornStructureBurnRollbackStage.None, request.RollbackStage);
    }

    [Fact]
    public void SinkRequiresBurnDamageOwnershipWhenProviderIsBound()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 2)],
            canClose: true));
        TimberbornBurnDamageTestStateProvider burnDamageTargets = new(
            [
                TimberbornBurnDamageTestStateProvider.State(
                    "storage-1",
                    "Warehouse.Folktails",
                    TimberbornBurnDamageTargetKind.Storage,
                    damageCapacity: 12,
                    damageTaken: 4,
                    ownedCellIndices: [4]),
            ]);
        TimberbornStructureBurnDamageRollbackSink sink = new(
            targetApi,
            burnDamageTargets: burnDamageTargets);

        TimberbornStructureBurnDamageRollbackSummary summary = sink.ApplyConsequences(
            1,
            [Decision(4, oldFuel: 8, newFuel: 4, heat: 10)]);

        Assert.Equal(0, summary.MatchedStructureCellCount);
        Assert.Empty(targetApi.Requests);
    }

    [Fact]
    public void SinkUsesSharedBurnDamageStateWhenProviderOwnsTarget()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [],
            canClose: true,
            canApplyRollbackVisual: true));
        TimberbornBurnDamageTestStateProvider burnDamageTargets = new(
            [
                TimberbornBurnDamageTestStateProvider.State(
                    "structure-1",
                    "Building.LumberMill",
                    TimberbornBurnDamageTargetKind.Structure,
                    damageCapacity: 8,
                    damageTaken: 6,
                    ownedCellIndices: [4]),
            ],
            [
                TimberbornBurnDamageTestStateProvider.AppliedEvent(
                    "structure-1",
                    "Building.LumberMill",
                    sourceCellIndex: 4,
                    damageApplied: 4,
                    damageTaken: 6,
                    damageCapacity: 8,
                    tick: 1),
            ]);
        TimberbornStructureBurnDamageRollbackSink sink = new(
            targetApi,
            burnDamageTargets: burnDamageTargets);

        TimberbornStructureBurnDamageRollbackSummary summary = sink.ApplyConsequences(
            1,
            [Decision(4, oldFuel: 8, newFuel: 4, heat: 10)]);

        Assert.Equal(1, summary.MatchedStructureCellCount);
        Assert.Equal(4, summary.TotalDamageApplied);
        TimberbornStructureBurnDamageApplyRequest request = Assert.Single(targetApi.Requests);
        Assert.Equal(6, request.DamageTaken);
        Assert.Equal(8, request.DamageCapacity);
        Assert.Equal(TimberbornStructureBurnRollbackStage.Unfinished, request.RollbackStage);
    }

    [Fact]
    public void SinkUsesSharedAppliedEventForPartiallyPreDamagedTarget()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [],
            canClose: true,
            canApplyRollbackVisual: true));
        TimberbornBurnDamageTestStateProvider burnDamageTargets = new(
            [
                TimberbornBurnDamageTestStateProvider.State(
                    "structure-1",
                    "Building.LumberMill",
                    TimberbornBurnDamageTargetKind.Structure,
                    damageCapacity: 8,
                    damageTaken: 8,
                    ownedCellIndices: [4]),
            ],
            [
                TimberbornBurnDamageTestStateProvider.AppliedEvent(
                    "structure-1",
                    "Building.LumberMill",
                    sourceCellIndex: 4,
                    damageApplied: 2,
                    damageTaken: 8,
                    damageCapacity: 8,
                    tick: 1),
            ]);
        TimberbornStructureBurnDamageRollbackSink sink = new(
            targetApi,
            burnDamageTargets: burnDamageTargets);

        TimberbornStructureBurnDamageRollbackSummary summary = sink.ApplyConsequences(
            1,
            [Decision(4, oldFuel: 8, newFuel: 4, heat: 10)]);

        Assert.Equal(1, summary.MatchedStructureCellCount);
        Assert.Equal(2, summary.TotalDamageApplied);
        TimberbornStructureBurnDamageApplyRequest request = Assert.Single(targetApi.Requests);
        Assert.Equal(8, request.DamageTaken);
        Assert.Equal(TimberbornStructureBurnRollbackStage.Unfinished, request.RollbackStage);
    }

    [Fact]
    public void SinkDoesNotRecountSharedDamageFromEarlierTicks()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [],
            canClose: true,
            canApplyRollbackVisual: true));
        TimberbornBurnDamageTestStateProvider burnDamageTargets = new(
            [
                TimberbornBurnDamageTestStateProvider.State(
                    "structure-1",
                    "Building.LumberMill",
                    TimberbornBurnDamageTargetKind.Structure,
                    damageCapacity: 8,
                    damageTaken: 8,
                    lastDamagedTick: 1,
                    ownedCellIndices: [4]),
            ]);
        TimberbornStructureBurnDamageRollbackSink sink = new(
            targetApi,
            burnDamageTargets: burnDamageTargets);

        TimberbornStructureBurnDamageRollbackSummary summary = sink.ApplyConsequences(
            2,
            [Decision(4, oldFuel: 8, newFuel: 4, heat: 10)]);

        Assert.Equal(1, summary.MatchedStructureCellCount);
        Assert.Equal(0, summary.TotalDamageApplied);
        TimberbornStructureBurnDamageApplyRequest request = Assert.Single(targetApi.Requests);
        Assert.Equal(8, request.DamageTaken);
        Assert.Equal(TimberbornStructureBurnRollbackStage.Unfinished, request.RollbackStage);
    }

    [Fact]
    public void SinkAllowsRepairAfterDangerEnds()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 2)],
            canClose: true,
            canRepairAfterDanger: true));
        TimberbornStructureBurnDamageRollbackSink sink = new(targetApi);

        sink.ApplyConsequences(2, [Decision(4, oldFuel: 8, newFuel: 4, heat: 10)]);
        TimberbornStructureBurnDamageRollbackSummary recovery = sink.ApplyConsequences(
            3,
            [Decision(4, oldFuel: 0, newFuel: 0, heat: 4, oldHeat: 10)]);

        Assert.Equal(0, recovery.RepairBlockedCount);
        Assert.Equal(1, recovery.RepairEligibleCount);
        Assert.False(targetApi.Requests.Last().ShouldClose);
        Assert.True(targetApi.Requests.Last().RepairEligible);
    }

    [Fact]
    public void SinkAllowsRepairAfterNativeRebuildChangesLiveObjectStableId()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 2)],
            stableId: "structure:new-construction-site",
            canClose: true,
            canApplyRollbackVisual: true,
            canRepairAfterDanger: true));
        TimberbornBurnDamageTestStateProvider burnDamageTargets = new(
            [
                TimberbornBurnDamageTestStateProvider.State(
                    "structure:original-building",
                    "Building.LumberMill",
                    TimberbornBurnDamageTargetKind.Structure,
                    damageCapacity: 8,
                    damageTaken: 4,
                    ownedCellIndices: [4]),
            ]);
        TimberbornStructureBurnDamageRollbackSink sink = new(
            targetApi,
            burnDamageTargets: burnDamageTargets);

        TimberbornStructureBurnDamageRollbackSummary recovery = sink.ApplyConsequences(
            3,
            [Decision(4, oldFuel: 0, newFuel: 0, heat: 4, oldHeat: 10)]);

        Assert.Equal(1, recovery.RepairEligibleCount);
        TimberbornStructureBurnDamageApplyRequest request = Assert.Single(targetApi.Requests);
        Assert.False(request.ShouldClose);
        Assert.True(request.RepairEligible);
    }

    [Fact]
    public void SinkIgnoresNonBurnableConstructionValueForMaterialLoss()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources:
            [
                new TimberbornBurnDamageResourceStack("MetalBlock", 4),
                new TimberbornBurnDamageResourceStack("Water", 2),
            ],
            canClose: true));
        TimberbornStructureBurnDamageRollbackSink sink = new(targetApi);

        TimberbornStructureBurnDamageRollbackSummary summary = sink.ApplyConsequences(
            4,
            [Decision(4, oldFuel: 8, newFuel: 2, heat: 10)]);

        Assert.Equal(1, summary.ZeroBurnableCapacityTargetCount);
        Assert.Equal(0, summary.MaterialValueLost);
        Assert.Equal(0, summary.TotalDamageApplied);
        Assert.Empty(targetApi.Requests);
    }

    [Fact]
    public void SinkSuppressesMultiCellStructureRollup()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 2)],
            canClose: true));
        TimberbornStructureBurnDamageRollbackSink sink = new(targetApi);

        TimberbornStructureBurnDamageRollbackSummary summary = sink.ApplyConsequences(
            5,
            [
                Decision(4, oldFuel: 8, newFuel: 6, heat: 8),
                Decision(5, oldFuel: 8, newFuel: 2, heat: 10),
            ]);

        Assert.Equal(2, summary.MatchedStructureCellCount);
        Assert.Equal(1, summary.DuplicateStructureTargetSuppressedCount);
        Assert.Equal(1, summary.ClosedStructureCount);
        Assert.Equal([6], targetApi.Requests.Select(static request => request.DamageApplied).ToArray());
    }

    [Fact]
    public void SinkRevertsLightDamageToUnfinishedConstructionPhase()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 100)],
            canClose: true,
            canApplyRollbackVisual: true,
            canRepairAfterDanger: true));
        TimberbornStructureBurnDamageRollbackSink sink = new(targetApi);

        TimberbornStructureBurnDamageRollbackSummary summary = sink.ApplyConsequences(
            6,
            [Decision(4, oldFuel: 10, newFuel: 9, heat: 10)]);

        TimberbornStructureBurnDamageApplyRequest request = Assert.Single(targetApi.Requests);
        Assert.True(request.ShouldClose);
        Assert.Equal(TimberbornStructureBurnRollbackStage.Unfinished, request.RollbackStage);
        Assert.Equal(1, summary.ClosedStructureCount);
        Assert.Equal(0, summary.ScorchedStageCount);
        Assert.Equal(1, summary.UnfinishedStageCount);
        Assert.Equal(1, summary.ConstructionPhaseEnteredCount);
    }

    [Fact]
    public void SinkRevertsStructureToUnfinishedAtFirstMaterialLoss()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [],
            canClose: true,
            canApplyRollbackVisual: true));
        TimberbornStructureBurnDamageRollbackSink scorchedSink = new(
            targetApi,
            burnDamageTargets: new TimberbornBurnDamageTestStateProvider(
                [
                    TimberbornBurnDamageTestStateProvider.State(
                        "structure-1",
                        "Building.LumberMill",
                        TimberbornBurnDamageTargetKind.Structure,
                        damageCapacity: 100,
                        damageTaken: 9,
                        ownedCellIndices: [4]),
                ],
                [
                    TimberbornBurnDamageTestStateProvider.AppliedEvent(
                        "structure-1",
                        "Building.LumberMill",
                        sourceCellIndex: 4,
                        damageApplied: 9,
                        damageTaken: 9,
                        damageCapacity: 100,
                        tick: 6),
                ]));
        TimberbornStructureBurnDamageRollbackSink unfinishedSink = new(
            targetApi,
            burnDamageTargets: new TimberbornBurnDamageTestStateProvider(
                [
                    TimberbornBurnDamageTestStateProvider.State(
                        "structure-1",
                        "Building.LumberMill",
                        TimberbornBurnDamageTargetKind.Structure,
                        damageCapacity: 100,
                        damageTaken: 10,
                        ownedCellIndices: [4]),
                ],
                [
                    TimberbornBurnDamageTestStateProvider.AppliedEvent(
                        "structure-1",
                        "Building.LumberMill",
                        sourceCellIndex: 4,
                        damageApplied: 1,
                        damageTaken: 10,
                        damageCapacity: 100,
                        tick: 7),
                ]));

        TimberbornStructureBurnDamageRollbackSummary firstDamage = scorchedSink.ApplyConsequences(
            6,
            [Decision(4, oldFuel: 10, newFuel: 9, heat: 10)]);
        TimberbornStructureBurnDamageRollbackSummary unfinished = unfinishedSink.ApplyConsequences(
            7,
            [Decision(4, oldFuel: 10, newFuel: 5, heat: 10)]);

        Assert.Equal(0, firstDamage.ScorchedStageCount);
        Assert.Equal(1, firstDamage.UnfinishedStageCount);
        Assert.Equal(1, unfinished.UnfinishedStageCount);
        Assert.Equal(2, targetApi.Requests.Count(static request => request.ShouldApplyRollbackVisual));
    }

    [Fact]
    public void SinkRevertsSmallWoodenBuildingToUnfinishedAtConstructionRollbackThreshold()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources:
            [
                new TimberbornBurnDamageResourceStack("Log", 2),
                new TimberbornBurnDamageResourceStack("Plank", 2),
            ],
            canClose: true,
            canApplyRollbackVisual: true,
            canRepairAfterDanger: true));
        TimberbornStructureBurnDamageRollbackSink sink = new(
            targetApi,
            burnDamageTargets: new TimberbornBurnDamageTestStateProvider(
                [
                    TimberbornBurnDamageTestStateProvider.State(
                        "structure-1",
                        "GathererFlag.Folktails(Clone)",
                        TimberbornBurnDamageTargetKind.Structure,
                        damageCapacity: 30,
                        damageTaken: 3,
                        ownedCellIndices: [4]),
                ],
                [
                    TimberbornBurnDamageTestStateProvider.AppliedEvent(
                        "structure-1",
                        "GathererFlag.Folktails(Clone)",
                        sourceCellIndex: 4,
                        damageApplied: 3,
                        damageTaken: 3,
                        damageCapacity: 30,
                        tick: 8),
                ]));

        TimberbornStructureBurnDamageRollbackSummary summary = sink.ApplyConsequences(
            8,
            [Decision(4, oldFuel: 12, newFuel: 9, heat: 10)]);

        TimberbornStructureBurnDamageApplyRequest request = Assert.Single(targetApi.Requests);
        Assert.Equal(TimberbornStructureBurnRollbackStage.Unfinished, request.RollbackStage);
        Assert.Equal(1, summary.UnfinishedStageCount);
        Assert.Equal(1, summary.ConstructionPhaseEnteredCount);
        Assert.Equal(0, summary.ScorchedStageCount);
    }

    [Fact]
    public void SinkAttemptsUnfinishedRollbackForDamagedStructures()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [],
            canClose: true,
            canApplyRollbackVisual: true,
            canRepairAfterDanger: true));
        TimberbornStructureBurnDamageRollbackSink sink = new(
            targetApi,
            burnDamageTargets: new TimberbornBurnDamageTestStateProvider(
                [
                    TimberbornBurnDamageTestStateProvider.State(
                        "structure-1",
                        "Building.LumberMill",
                        TimberbornBurnDamageTargetKind.Structure,
                        damageCapacity: 100,
                        damageTaken: 10,
                        ownedCellIndices: [4]),
                ],
                [
                    TimberbornBurnDamageTestStateProvider.AppliedEvent(
                        "structure-1",
                        "Building.LumberMill",
                        sourceCellIndex: 4,
                        damageApplied: 10,
                        damageTaken: 10,
                        damageCapacity: 100,
                        tick: 8),
                ]));

        TimberbornStructureBurnDamageRollbackSummary summary = sink.ApplyConsequences(
            8,
            [Decision(4, oldFuel: 10, newFuel: 0, heat: 10)]);

        Assert.Equal(1, summary.UnfinishedStageCount);
        Assert.True(Assert.Single(targetApi.Requests).ShouldApplyRollbackVisual);
    }

    [Fact]
    public void TargetApiStopsRemovingConstructionMaterialsOnceRepairEligible()
    {
        TimberbornStructureBurnDamageApplyRequest burningRequest = new(
            DamageApplied: 10,
            DamageTaken: 10,
            DamageCapacity: 100,
            RollbackStage: TimberbornStructureBurnRollbackStage.Unfinished,
            ShouldClose: false,
            RepairBlocked: true,
            RepairEligible: false,
            ShouldApplyRollbackVisual: true);
        TimberbornStructureBurnDamageApplyRequest repairRequest = burningRequest with
        {
            RepairBlocked = false,
            RepairEligible = true,
        };

        Assert.True(TimberbornStructureBurnDamageRollbackTargetApi.ShouldRemoveConstructionMaterials(burningRequest));
        Assert.False(TimberbornStructureBurnDamageRollbackTargetApi.ShouldRemoveConstructionMaterials(repairRequest));
    }

    [Fact]
    public void TargetApiSynchronizesConstructionStateWhenClosureEntersUnfinishedBeforeDamage()
    {
        TimberbornStructureBurnDamageApplyRequest closeRequest = new(
            DamageApplied: 0,
            DamageTaken: 0,
            DamageCapacity: 100,
            RollbackStage: TimberbornStructureBurnRollbackStage.None,
            ShouldClose: true,
            RepairBlocked: true,
            RepairEligible: false,
            ShouldApplyRollbackVisual: false);

        Assert.False(TimberbornStructureBurnDamageRollbackTargetApi.ShouldSynchronizeConstructionState(
            closeRequest,
            enteredUnfinishedState: false));
        Assert.True(TimberbornStructureBurnDamageRollbackTargetApi.ShouldSynchronizeConstructionState(
            closeRequest,
            enteredUnfinishedState: true));
        Assert.Equal(1f, TimberbornStructureBurnDamageRollbackTargetApi.CalculateRemainingConstructionFraction(closeRequest));
        Assert.Equal(10, TimberbornStructureBurnDamageRollbackTargetApi.CalculateRequiredConstructionMaterialAmount(10, 1f));
    }

    [Fact]
    public void TargetApiDoesNotSynchronizeConstructionInventoryForScorchedStorageTargets()
    {
        TimberbornStructureBurnDamageApplyRequest scorchedStorageRequest = new(
            DamageApplied: 3,
            DamageTaken: 3,
            DamageCapacity: 6,
            RollbackStage: TimberbornStructureBurnRollbackStage.Scorched,
            ShouldClose: true,
            RepairBlocked: true,
            RepairEligible: false,
            ShouldApplyRollbackVisual: true);

        Assert.False(TimberbornStructureBurnDamageRollbackTargetApi.ShouldSynchronizeConstructionState(
            scorchedStorageRequest,
            enteredUnfinishedState: false,
            canUseNativeConstructionRollback: false));
        Assert.True(TimberbornStructureBurnDamageRollbackTargetApi.ShouldSynchronizeConstructionState(
            scorchedStorageRequest,
            enteredUnfinishedState: true,
            canUseNativeConstructionRollback: false));
    }

    [Fact]
    public void TargetApiKeepsLightBurnDamageNearlyComplete()
    {
        TimberbornStructureBurnDamageApplyRequest request = new(
            DamageApplied: 1,
            DamageTaken: 1,
            DamageCapacity: 100,
            RollbackStage: TimberbornStructureBurnRollbackStage.Unfinished,
            ShouldClose: true,
            RepairBlocked: true,
            RepairEligible: false,
            ShouldApplyRollbackVisual: true);

        Assert.Equal(0.99f, TimberbornStructureBurnDamageRollbackTargetApi.CalculateRemainingConstructionFraction(request));
        Assert.Equal(99, TimberbornStructureBurnDamageRollbackTargetApi.CalculateRequiredConstructionMaterialAmount(100, 0.99f));
    }

    [Fact]
    public void TargetApiReducesConstructionMaterialsWithBurnDamage()
    {
        TimberbornStructureBurnDamageApplyRequest halfBurnedRequest = new(
            DamageApplied: 50,
            DamageTaken: 50,
            DamageCapacity: 100,
            RollbackStage: TimberbornStructureBurnRollbackStage.Unfinished,
            ShouldClose: true,
            RepairBlocked: true,
            RepairEligible: false,
            ShouldApplyRollbackVisual: true);
        TimberbornStructureBurnDamageApplyRequest fullyBurnedRequest = halfBurnedRequest with
        {
            DamageTaken = 100,
        };

        Assert.Equal(0.5f, TimberbornStructureBurnDamageRollbackTargetApi.CalculateRemainingConstructionFraction(halfBurnedRequest));
        Assert.Equal(5, TimberbornStructureBurnDamageRollbackTargetApi.CalculateRequiredConstructionMaterialAmount(10, 0.5f));
        Assert.Equal(0f, TimberbornStructureBurnDamageRollbackTargetApi.CalculateRemainingConstructionFraction(fullyBurnedRequest));
        Assert.Equal(0, TimberbornStructureBurnDamageRollbackTargetApi.CalculateRequiredConstructionMaterialAmount(10, 0f));
    }

    [Fact]
    public void TargetApiRestoresStructureTexturesWhenRepairUnlocks()
    {
        string source = ReadTimberbornSource("TimberbornStructureBurnDamageRollback.cs");

        Assert.Contains("private readonly Dictionary<int, Material> _originalStructureMaterialsByBurnedInstanceId", source, StringComparison.Ordinal);
        Assert.Contains("if (request.RepairEligible)", source, StringComparison.Ordinal);
        Assert.Contains("constructionMaterialStock > lastConstructionMaterialStock", source, StringComparison.Ordinal);
        Assert.Contains("constructionMaterialStock > 0", source, StringComparison.Ordinal);
        Assert.Contains("RestoreBurnedTextures(blockObject)", source, StringComparison.Ordinal);
        Assert.Contains("wildfire_timberborn_structure_repair_unlocked", source, StringComparison.Ordinal);
        Assert.Contains("wildfire_timberborn_structure_repair_waiting_for_materials", source, StringComparison.Ordinal);
        Assert.Contains("RestoreRepairCompletedStructures()", source, StringComparison.Ordinal);
        Assert.Contains("_burnedStructureCellIndexByStableId", source, StringComparison.Ordinal);
        Assert.Contains("wildfire_timberborn_structure_repair_completed_visual_restored", source, StringComparison.Ordinal);
        Assert.Contains("_originalStructureMaterialsByBurnedName", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetApiKeepsStructureBurnVisualsAwayFromFloatingStatusIcons()
    {
        string source = ReadTimberbornSource("TimberbornStructureBurnDamageRollback.cs");

        Assert.Contains("IsStructureVisualRenderer", source, StringComparison.Ordinal);
        Assert.Contains("renderer is SpriteRenderer or LineRenderer or TrailRenderer or ParticleSystemRenderer", source, StringComparison.Ordinal);
        Assert.Contains("\"Status\"", source, StringComparison.Ordinal);
        Assert.Contains("\"Icon\"", source, StringComparison.Ordinal);
        Assert.Contains(".GetComponentsInChildren<Renderer>(includeInactive: false)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetApiShowsBurningStatusWhileRepairIsBlocked()
    {
        string source = ReadTimberbornSource("TimberbornStructureBurnDamageRollback.cs");

        Assert.Contains("StatusToggle.CreatePriorityStatusWithFloatingIcon", source, StringComparison.Ordinal);
        Assert.Contains("\"WildfireBurningStatus\"", source, StringComparison.Ordinal);
        Assert.Contains("SynchronizeBurningStatus(blockObject, target, request.RepairBlocked)", source, StringComparison.Ordinal);
        Assert.Contains("SynchronizeBurningStatus(blockObject, target, isBurning: false)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetApiPreservesRuntimeSettingsAndSuppressesRecoveredGoodsDuringFireRebuild()
    {
        string source = ReadTimberbornSource("TimberbornStructureBurnDamageRollback.cs");

        Assert.Contains("CaptureRuntimeSettings(blockObject)", source, StringComparison.Ordinal);
        Assert.Contains("runtimeSettingsSnapshot.ApplyTo(rebuiltBlockObject)", source, StringComparison.Ordinal);
        Assert.Contains("DisableRecoverableGoodProviders(blockObject)", source, StringComparison.Ordinal);
        Assert.Contains("\"DisableGoodRecovery\"", source, StringComparison.Ordinal);
        Assert.Contains("\"Timberborn.Workshops.Manufactory\"", source, StringComparison.Ordinal);
        Assert.Contains("AllowedGoods: inventory.AllowedGoods.ToArray()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetApiKeepsStorageAndWaterTanksOutOfNativeConstructionRollback()
    {
        string source = ReadTimberbornSource("TimberbornStructureBurnDamageRollback.cs");

        Assert.Contains("IsStorageLikeName(blockObject.Name)", source, StringComparison.Ordinal);
        Assert.Contains("TryRebuildStructureAsUnfinished", source, StringComparison.Ordinal);
        Assert.Contains("DisableRecoverableGoodProviders(blockObject)", source, StringComparison.Ordinal);
        Assert.Contains("_entityService.Delete(blockObject)", source, StringComparison.Ordinal);
        Assert.Contains("ApplyBurnedTextures(blockObject, target.SpecId)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetApiDisablesRecoverableGoodProvidersFromAllComponentsBeforeRebuild()
    {
        string source = ReadTimberbornSource("TimberbornStructureBurnDamageRollback.cs");

        Assert.Contains("blockObject.AllComponents", source, StringComparison.Ordinal);
        Assert.Contains("GetComponentsInChildren<Component>(includeInactive: true)", source, StringComparison.Ordinal);
        Assert.Contains(".Cast<object>()", source, StringComparison.Ordinal);
        Assert.Contains("RecoverableGoodProvider", source, StringComparison.Ordinal);
        Assert.Contains("DisableGoodRecovery", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Cast<Component>()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetApiRevertsOverlappingPathInfrastructureWhenStructureBurns()
    {
        string source = ReadTimberbornSource("TimberbornStructureBurnDamageRollback.cs");

        Assert.Contains("ResolveStructureBlockObject(coordinates)", source, StringComparison.Ordinal);
        Assert.Contains(".Where(static candidate => !IsInfrastructureLikeName(candidate.Name))", source, StringComparison.Ordinal);
        Assert.Contains("ResolveOverlappingPathInfrastructure(blockObject)", source, StringComparison.Ordinal);
        Assert.Contains("ResolveOverheadFootprintCoordinates", source, StringComparison.Ordinal);
        Assert.Contains("GroupBy(static coordinates => (coordinates.x, coordinates.y))", source, StringComparison.Ordinal);
        Assert.Contains("Math.Min(_grid.Depth - 1", source, StringComparison.Ordinal);
        Assert.Contains("RebuildOverlappingPathInfrastructureAsUnfinished", source, StringComparison.Ordinal);
        Assert.Contains("wildfire_timberborn_structure_overlapping_path_rebuilt_unfinished", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetApiUsesNativeTerrainPhysicsForStructuralCollapseCone()
    {
        string source = ReadTimberbornSource("TimberbornStructureBurnDamageRollback.cs");
        string initializer = ReadTimberbornSource("TimberbornFireRuntimeInitializer.cs");

        Assert.Contains("ITerrainPhysicsService", source, StringComparison.Ordinal);
        Assert.Contains("TerrainDestroyer", source, StringComparison.Ordinal);
        Assert.Contains("ApplyNativeStructuralCollapseDependents(blockObject, target)", source, StringComparison.Ordinal);
        Assert.Contains("_terrainPhysicsService.GetTerrainAndBlockObjectStack", source, StringComparison.Ordinal);
        Assert.Contains("HashSet<Vector3Int> terrainToDestroy", source, StringComparison.Ordinal);
        Assert.Contains("HashSet<BlockObject> blockObjectsToCollapse", source, StringComparison.Ordinal);
        Assert.Contains("RecreateStructuralCollapsePlan", source, StringComparison.Ordinal);
        Assert.Contains("wildfire_timberborn_structure_native_structural_collapse_applied", source, StringComparison.Ordinal);
        Assert.Contains("wildfire_timberborn_structure_collapse_dependent_rebuilt_unfinished", source, StringComparison.Ordinal);
        Assert.Contains("terrainPhysicsService: _terrainPhysicsService", initializer, StringComparison.Ordinal);
        Assert.Contains("terrainDestroyer: _terrainDestroyer", initializer, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetApiKeepsBurnedSupportAndCollapsedDependentsOnDifferentRecoveryPaths()
    {
        string source = ReadTimberbornSource("TimberbornStructureBurnDamageRollback.cs");
        string initializer = ReadTimberbornSource("TimberbornFireRuntimeInitializer.cs");

        Assert.Contains("DisableRecoverableGoodProviders(blockObject)", source, StringComparison.Ordinal);
        Assert.Contains("_entityService.Delete(snapshot.BlockObject)", source, StringComparison.Ordinal);
        Assert.Contains("snapshot.RuntimeSettings.ApplyTo(rebuiltBlockObject)", source, StringComparison.Ordinal);
        Assert.Contains("structural_collapse_rebuilt_dependents", source, StringComparison.Ordinal);
        Assert.Contains("structural_collapse_destroyed_terrain", source, StringComparison.Ordinal);
        Assert.Contains("ComponentBlockObjects<GoodStack>", initializer, StringComparison.Ordinal);
        Assert.Contains("recoverableGoodStackSources", initializer, StringComparison.Ordinal);
        Assert.Contains(".Concat(recoverableGoodStackSources)", initializer, StringComparison.Ordinal);
    }

    [Fact]
    public void SinkRunsRepairCompletionMaintenanceEvenWithoutNewFireDeltas()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 1)],
            canClose: true,
            canApplyRollbackVisual: true,
            canRepairAfterDanger: true));
        TimberbornStructureBurnDamageRollbackSink sink = new(targetApi);

        TimberbornStructureBurnDamageRollbackSummary summary = sink.ApplyConsequences(9, []);

        Assert.Equal(1, targetApi.RestoreRepairCompletedStructuresCallCount);
        Assert.Equal(TimberbornStructureBurnDamageRollbackSummary.Empty, summary);
    }

    [Fact]
    public void SinkClearsBurningStatusesWhenFireNoLongerProducesStructureDeltas()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            stableId: "structure:district-center",
            resources: [new TimberbornBurnDamageResourceStack("Log", 1)],
            canClose: true,
            canApplyRollbackVisual: true,
            canRepairAfterDanger: true));
        TimberbornStructureBurnDamageRollbackSink sink = new(targetApi);

        sink.ApplyConsequences(9, [Decision(4, oldFuel: 8, newFuel: 4, heat: 10)]);
        sink.ApplyConsequences(10, []);

        Assert.Equal(
            [
                ["structure:district-center"],
                [],
            ],
            targetApi.BurningStatusSynchronizations);
    }

    [Fact]
    public void SinkThrowsWhenNativeConstructionRollbackAttemptDoesNotEnterConstructionPhase()
    {
        RecordingStructureTargetApi targetApi = new(
            Target(
                resources: [new TimberbornBurnDamageResourceStack("Log", 1)],
                canClose: true,
                canApplyRollbackVisual: true,
                canRepairAfterDanger: false),
            failNativeConstructionRollback: true);
        TimberbornStructureBurnDamageRollbackSink sink = new(targetApi);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => sink.ApplyConsequences(
            9,
            [Decision(4, oldFuel: 8, newFuel: 0, heat: 10)]));

        TimberbornStructureBurnDamageApplyRequest request = Assert.Single(targetApi.Requests);
        Assert.Equal(TimberbornStructureBurnRollbackStage.Unfinished, request.RollbackStage);
        Assert.True(TimberbornStructureBurnDamageRollbackTargetApi.RequestsNativeConstructionPhase(request));
        Assert.Contains("failed to enter native construction phase", exception.Message);
    }

    [Fact]
    public void SinkUsesScorchedVisualWithoutNativeConstructionForTargetsWithoutConstructionRollback()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 1)],
            specId: "DistrictCenter.Folktails(Clone)",
            canClose: true,
            canApplyRollbackVisual: true,
            canUseNativeConstructionRollback: false,
            canRepairAfterDanger: false));
        TimberbornStructureBurnDamageRollbackSink sink = new(targetApi);

        TimberbornStructureBurnDamageRollbackSummary summary = sink.ApplyConsequences(
            9,
            [Decision(4, oldFuel: 8, newFuel: 0, heat: 10)]);

        TimberbornStructureBurnDamageApplyRequest request = Assert.Single(targetApi.Requests);
        Assert.Equal(TimberbornStructureBurnRollbackStage.Scorched, request.RollbackStage);
        Assert.False(TimberbornStructureBurnDamageRollbackTargetApi.RequestsNativeConstructionPhase(request));
        Assert.Equal(0, summary.UnfinishedStageCount);
        Assert.Equal(1, summary.ScorchedStageCount);
        Assert.Equal(1, summary.VisualRollbackAppliedCount);
        Assert.Equal(0, summary.ConstructionPhaseEnteredCount);
    }

    [Theory]
    [InlineData("DistrictCenter")]
    [InlineData("DistrictCenter.Folktails(Clone)")]
    [InlineData("DistrictCenter.IronTeeth(Clone)")]
    public void TargetApiRecognizesDistrictCenterRebuildTargets(string specId)
    {
        Assert.True(TimberbornStructureBurnDamageRollbackTargetApi.IsDistrictCenterName(specId));
    }

    [Theory]
    [InlineData("DistrictCrossing.Folktails(Clone)")]
    [InlineData("LumberMill.Folktails(Clone)")]
    public void TargetApiDoesNotRebuildOtherBuildingsAsDistrictCenters(string specId)
    {
        Assert.False(TimberbornStructureBurnDamageRollbackTargetApi.IsDistrictCenterName(specId));
    }

    [Theory]
    [InlineData("SmallWarehouse.Folktails(Clone)")]
    [InlineData("UndergroundWarehouse.Folktails(Clone)")]
    [InlineData("SmallTank.IronTeeth(Clone)")]
    [InlineData("StoragePile.Log(Clone)")]
    [InlineData("Stockpile")]
    public void TargetApiRecognizesStorageLikeRebuildTargets(string specId)
    {
        Assert.True(TimberbornStructureBurnDamageRollbackTargetApi.IsStorageLikeName(specId));
    }

    [Theory]
    [InlineData("LumberMill.Folktails(Clone)")]
    [InlineData("WaterPump.Folktails(Clone)")]
    [InlineData("Forester.Folktails(Clone)")]
    [InlineData("DistrictCenter.Folktails(Clone)")]
    public void TargetApiDoesNotTreatOtherBuildingsAsStorageLikeRebuildTargets(string specId)
    {
        Assert.False(TimberbornStructureBurnDamageRollbackTargetApi.IsStorageLikeName(specId));
    }

    [Fact]
    public void SinkThrowsWhenClosureCannotBeApplied()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 1)],
            canClose: false,
            canApplyRollbackVisual: false));
        TimberbornStructureBurnDamageRollbackSink sink = new(targetApi);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            sink.ApplyConsequences(9, [Decision(4, oldFuel: 8, newFuel: 0, heat: 10)]));

        Assert.Contains("failed to close", exception.Message);
    }

    [Fact]
    public void DeltaConsumerRoutesStructureRollbackTelemetry()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 1)],
            canClose: true));
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(
                structureBurnDamageRollbackSink: new TimberbornStructureBurnDamageRollbackSink(targetApi)));

        TimberbornFireDeltaConsumerSummary summary = consumer.Consume(
            10,
            [new CellDelta(4, Cell(fuel: 8, heat: 10), Cell(fuel: 5, heat: 10))]);

        Assert.Equal(1, summary.StructureBurnDamageRollbackConsideredDeltaCount);
        Assert.Equal(1, summary.StructureBurnDamageRollbackClosedStructureCount);
        Assert.Equal(3, summary.StructureBurnDamageRollbackTotalDamageApplied);
    }

    private static TimberbornStructureBurnDamageTarget Target(
        IReadOnlyList<TimberbornBurnDamageResourceStack> resources,
        string stableId = "structure-1",
        string specId = "Building.LumberMill",
        bool canClose = false,
        bool canApplyRollbackVisual = false,
        bool canUseNativeConstructionRollback = true,
        bool canRepairAfterDanger = false)
    {
        return new TimberbornStructureBurnDamageTarget(
            stableId,
            specId,
            CellIndex: 4,
            resources,
            canClose,
            canApplyRollbackVisual,
            canUseNativeConstructionRollback,
            canRepairAfterDanger);
    }

    private static TimberbornFireCellDeltaDecision Decision(
        int cellIndex,
        int oldFuel,
        int newFuel,
        int heat,
        int? oldHeat = null)
    {
        return TimberbornFireCellDeltaDecision.FromDelta(
            new CellDelta(cellIndex, Cell(oldFuel, oldHeat ?? heat), Cell(newFuel, heat)));
    }

    private static ushort Cell(int fuel, int heat)
    {
        return PackedCell.Pack(fuel, heat, flammability: 3, water: 0, terrain: 1, burningLevel: 0);
    }

    private static string ReadTimberbornSource(string fileName)
    {
        string root = FindRepoRoot();
        string timberbornRoot = Path.Combine(root, "src", "Wildfire.Timberborn");
        string path = Directory
            .EnumerateFiles(timberbornRoot, fileName, SearchOption.AllDirectories)
            .First();
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Wildfire.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate Wildfire repo root.");
    }

    private sealed class RecordingStructureTargetApi(
        TimberbornStructureBurnDamageTarget? target,
        bool failNativeConstructionRollback = false)
        : ITimberbornStructureBurnDamageRollbackTargetApi,
            ITimberbornStructureBurnRepairCompletionMaintenance,
            ITimberbornStructureBurningStatusMaintenance
    {
        public List<TimberbornStructureBurnDamageApplyRequest> Requests { get; } = [];

        public List<string[]> BurningStatusSynchronizations { get; } = [];

        public int RestoreRepairCompletedStructuresCallCount { get; private set; }

        public TimberbornStructureBurnDamageTarget? ResolveTarget(
            TimberbornStructureBurnDamageConsequence consequence)
        {
            return target is null ? null : target with { CellIndex = consequence.CellIndex };
        }

        public TimberbornStructureBurnDamageApplyResult ApplyState(
            TimberbornStructureBurnDamageTarget damageTarget,
            TimberbornStructureBurnDamageApplyRequest request)
        {
            Requests.Add(request);
            bool requestsNativeConstructionPhase =
                TimberbornStructureBurnDamageRollbackTargetApi.RequestsNativeConstructionPhase(request);
            bool constructionPhaseEntered = damageTarget.CanUseNativeConstructionRollback &&
                requestsNativeConstructionPhase &&
                !failNativeConstructionRollback;
            bool visualRollbackApplied = damageTarget.CanApplyRollbackVisual && request.ShouldApplyRollbackVisual;
            if (request.ShouldClose && !damageTarget.CanClose)
            {
                throw new InvalidOperationException(
                    $"Structure burn damage rollback failed to close {damageTarget.SpecId} at cell {damageTarget.CellIndex}.");
            }

            return new TimberbornStructureBurnDamageApplyResult(
                Closed: damageTarget.CanClose && request.ShouldClose,
                VisualRollbackApplied: visualRollbackApplied,
                ConstructionPhaseEntered: constructionPhaseEntered,
                RepairEligible: request.RepairEligible);
        }

        public int RestoreRepairCompletedStructures()
        {
            RestoreRepairCompletedStructuresCallCount++;
            return 0;
        }

        public int SynchronizeBurningStatuses(IReadOnlyCollection<string> activeRepairBlockedStableIds)
        {
            BurningStatusSynchronizations.Add(activeRepairBlockedStableIds.ToArray());
            return 0;
        }
    }

    private sealed class RecordingFireLogSink : ITimberbornFireLogSink
    {
        public void Info(string message)
        {
        }

        public void Warning(string message)
        {
        }
    }
}
