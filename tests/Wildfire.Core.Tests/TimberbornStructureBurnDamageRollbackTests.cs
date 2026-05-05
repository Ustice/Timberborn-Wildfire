using Wildfire.Core;
using Wildfire.Timberborn;

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
        Assert.Equal(1, summary.ClosedStructureCount);
        Assert.Equal([6], targetApi.Requests.Select(static request => request.DamageApplied).ToArray());
    }

    [Fact]
    public void SinkReportsRollbackThresholdTransitions()
    {
        RecordingStructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 1)],
            canClose: true,
            canApplyRollbackVisual: true));
        TimberbornStructureBurnDamageRollbackSink sink = new(targetApi);

        TimberbornStructureBurnDamageRollbackSummary scorched = sink.ApplyConsequences(
            6,
            [Decision(4, oldFuel: 8, newFuel: 4, heat: 10)]);
        TimberbornStructureBurnDamageRollbackSummary unfinished = sink.ApplyConsequences(
            7,
            [Decision(4, oldFuel: 8, newFuel: 0, heat: 10)]);

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
        Assert.Equal(1, summary.StructureBurnDamageRollbackClosedStructureCount);
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
        return PackedCell.Pack(fuel, heat, flammability: 3, water: 0, terrain: 1, heatLoss: 0);
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
