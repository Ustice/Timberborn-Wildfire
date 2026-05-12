using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornStructureBurnDamageRollbackTests
{
    [Fact]
    public void SinkKeepsDamagedStructureAccessibleAndBlocksRepairWhileDangerous()
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
        Assert.Equal(0, summary.ClosedStructureCount);
        Assert.Equal(1, summary.RepairBlockedCount);
        Assert.Equal(0, summary.RepairEligibleCount);
        Assert.Equal(4, summary.MaterialValueLost);
        TimberbornStructureBurnDamageApplyRequest request = Assert.Single(targetApi.Requests);
        Assert.False(request.ShouldClose);
        Assert.True(request.RepairBlocked);
    }

    [Fact]
    public void SinkDoesNotCloseStructureOnRepairBlockingFirePressureBeforeDamageThreshold()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 10)],
            canClose: true,
            canApplyRollbackVisual: true));
        TimberbornStructureBurnDamageRollbackSink sink = new(targetApi);

        TimberbornStructureBurnDamageRollbackSummary summary = sink.ApplyConsequences(
            1,
            [Decision(4, oldFuel: 10, newFuel: 10, heat: 5)]);

        Assert.Equal(0, summary.ClosedStructureCount);
        Assert.Equal(1, summary.RepairBlockedCount);
        TimberbornStructureBurnDamageApplyRequest request = Assert.Single(targetApi.Requests);
        Assert.False(request.ShouldClose);
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
        Assert.True(targetApi.Requests.Last().RepairEligible);
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
        Assert.Equal(0, summary.ClosedStructureCount);
        Assert.Equal([6], targetApi.Requests.Select(static request => request.DamageApplied).ToArray());
    }

    [Fact]
    public void SinkRevertsStructureToUnfinishedAtTenPercentMaterialLoss()
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

        TimberbornStructureBurnDamageRollbackSummary scorched = scorchedSink.ApplyConsequences(
            6,
            [Decision(4, oldFuel: 10, newFuel: 9, heat: 10)]);
        TimberbornStructureBurnDamageRollbackSummary unfinished = unfinishedSink.ApplyConsequences(
            7,
            [Decision(4, oldFuel: 10, newFuel: 5, heat: 10)]);

        Assert.Equal(1, scorched.ScorchedStageCount);
        Assert.Equal(1, unfinished.UnfinishedStageCount);
        Assert.Equal(2, targetApi.Requests.Count(static request => request.ShouldApplyRollbackVisual));
    }

    [Fact]
    public void SinkReportsSafeLimitationWhenVisualRollbackIsUnavailable()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 1)],
            canClose: false,
            canApplyRollbackVisual: false));
        TimberbornStructureBurnDamageRollbackSink sink = new(targetApi);

        TimberbornStructureBurnDamageRollbackSummary summary = sink.ApplyConsequences(
            8,
            [Decision(4, oldFuel: 8, newFuel: 0, heat: 10)]);

        Assert.Equal(1, summary.SkippedNoSafeApiCount);
        Assert.Equal(0, summary.ClosedStructureCount);
        Assert.Equal(0, summary.VisualRollbackAppliedCount);
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
            9,
            [new CellDelta(4, Cell(fuel: 8, heat: 10), Cell(fuel: 5, heat: 10))]);

        Assert.Equal(1, summary.StructureBurnDamageRollbackConsideredDeltaCount);
        Assert.Equal(0, summary.StructureBurnDamageRollbackClosedStructureCount);
        Assert.Equal(3, summary.StructureBurnDamageRollbackTotalDamageApplied);
    }

    private static TimberbornStructureBurnDamageTarget Target(
        IReadOnlyList<TimberbornBurnDamageResourceStack> resources,
        bool canClose = false,
        bool canApplyRollbackVisual = false,
        bool canRepairAfterDanger = false)
    {
        return new TimberbornStructureBurnDamageTarget(
            "structure-1",
            "Building.LumberMill",
            CellIndex: 4,
            resources,
            canClose,
            canApplyRollbackVisual,
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

    private sealed class RecordingStructureTargetApi(TimberbornStructureBurnDamageTarget? target)
        : ITimberbornStructureBurnDamageRollbackTargetApi
    {
        public List<TimberbornStructureBurnDamageApplyRequest> Requests { get; } = [];

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
            return new TimberbornStructureBurnDamageApplyResult(
                Closed: damageTarget.CanClose && request.ShouldClose,
                VisualRollbackApplied: damageTarget.CanApplyRollbackVisual && request.ShouldApplyRollbackVisual,
                SkippedNoSafeApi:
                    (!damageTarget.CanClose && request.ShouldClose) ||
                    (!damageTarget.CanApplyRollbackVisual && request.ShouldApplyRollbackVisual),
                RepairEligible: request.RepairEligible);
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
