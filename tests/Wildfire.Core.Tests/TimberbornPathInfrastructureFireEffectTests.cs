using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class TimberbornPathInfrastructureFireEffectTests
{
    [Fact]
    public void SinkTreatsZeroCostPathsAsSafeNoOps()
    {
        RecordingPathInfrastructureTargetApi targetApi = new(Target(resources: []));
        TimberbornPathInfrastructureFireSink sink = new(targetApi);

        TimberbornPathInfrastructureFireSummary summary = sink.ApplyConsequences(
            1,
            [Decision(4, oldFuel: 5, newFuel: 1)]);

        Assert.Equal(1, summary.MatchedTargetCellCount);
        Assert.Equal(1, summary.ZeroCostPathTargetCount);
        Assert.Equal(0, summary.DamagedTargetCount);
        Assert.Equal(0, summary.SkippedNoSafeApiCount);
        Assert.Empty(targetApi.AppliedDamage);
    }

    [Fact]
    public void SinkRequiresBurnDamageInfrastructureOwnershipWhenProviderIsBound()
    {
        RecordingPathInfrastructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 1)],
            canMarkDamaged: true));
        TimberbornBurnDamageTestStateProvider burnDamageTargets = new(
            [
                TimberbornBurnDamageTestStateProvider.State(
                    "structure-1",
                    "Building.LumberMill",
                    TimberbornBurnDamageTargetKind.Structure,
                    damageCapacity: 8,
                    damageTaken: 4,
                    ownedCellIndices: [4]),
            ]);
        TimberbornPathInfrastructureFireSink sink = new(
            targetApi,
            burnDamageTargets: burnDamageTargets);

        TimberbornPathInfrastructureFireSummary summary = sink.ApplyConsequences(
            1,
            [Decision(4, oldFuel: 5, newFuel: 1)]);

        Assert.Equal(0, summary.MatchedTargetCellCount);
        Assert.Empty(targetApi.AppliedDamage);
    }

    [Fact]
    public void SinkUsesSharedBurnDamageStateWhenProviderOwnsPathTarget()
    {
        RecordingPathInfrastructureTargetApi targetApi = new(Target(
            resources: [],
            canMarkDamaged: true));
        TimberbornBurnDamageTestStateProvider burnDamageTargets = new(
            [
                TimberbornBurnDamageTestStateProvider.State(
                    "path-1",
                    "Path.Folktails",
                    TimberbornBurnDamageTargetKind.Infrastructure,
                    damageCapacity: 10,
                    damageTaken: 4,
                    ownedCellIndices: [4]),
            ],
            [
                TimberbornBurnDamageTestStateProvider.AppliedEvent(
                    "path-1",
                    "Path.Folktails",
                    sourceCellIndex: 4,
                    damageApplied: 4,
                    damageTaken: 4,
                    damageCapacity: 10,
                    tick: 1),
            ]);
        TimberbornPathInfrastructureFireSink sink = new(
            targetApi,
            burnDamageTargets: burnDamageTargets);

        TimberbornPathInfrastructureFireSummary summary = sink.ApplyConsequences(
            1,
            [Decision(4, oldFuel: 5, newFuel: 1)]);

        Assert.Equal(1, summary.MatchedTargetCellCount);
        Assert.Equal(4, summary.TotalDamageApplied);
        Assert.Equal([4], targetApi.AppliedDamage.Select(static damage => damage.DamageApplied).ToArray());
    }

    [Fact]
    public void SinkAppliesBurnCapacityFromBurnableConstructionResources()
    {
        RecordingPathInfrastructureTargetApi targetApi = new(Target(
            resources:
            [
                new TimberbornBurnDamageResourceStack("Plank", 2),
                new TimberbornBurnDamageResourceStack("Water", 5),
            ],
            canMarkDamaged: true,
            repairEligible: true));
        TimberbornPathInfrastructureFireSink sink = new(targetApi);

        TimberbornPathInfrastructureFireSummary summary = sink.ApplyConsequences(
            2,
            [Decision(4, oldFuel: 8, newFuel: 3)]);

        Assert.Equal(1, summary.DamagedTargetCount);
        Assert.Equal(1, summary.RepairEligibleTargetCount);
        Assert.Equal(5, summary.TotalDamageApplied);
        Assert.Equal([5], targetApi.AppliedDamage.Select(static damage => damage.DamageApplied).ToArray());
    }

    [Fact]
    public void SinkSuppressesDuplicateCellsForSamePathTarget()
    {
        RecordingPathInfrastructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 2)],
            canMarkDamaged: true));
        TimberbornPathInfrastructureFireSink sink = new(targetApi);

        TimberbornPathInfrastructureFireSummary summary = sink.ApplyConsequences(
            3,
            [
                Decision(4, oldFuel: 8, newFuel: 6),
                Decision(5, oldFuel: 8, newFuel: 3),
            ]);

        Assert.Equal(2, summary.MatchedTargetCellCount);
        Assert.Equal(1, summary.DuplicateTargetSuppressedCount);
        Assert.Equal(1, summary.DamagedTargetCount);
        Assert.Equal([5], targetApi.AppliedDamage.Select(static damage => damage.DamageApplied).ToArray());
    }

    [Fact]
    public void SinkReportsNoSafeApiWhenPathMutationIsUnavailable()
    {
        RecordingPathInfrastructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 1)],
            canMarkDamaged: false));
        TimberbornPathInfrastructureFireSink sink = new(targetApi);

        TimberbornPathInfrastructureFireSummary summary = sink.ApplyConsequences(
            4,
            [Decision(4, oldFuel: 8, newFuel: 3)]);

        Assert.Equal(0, summary.DamagedTargetCount);
        Assert.Equal(1, summary.SkippedNoSafeApiCount);
        Assert.Equal(5, summary.TotalDamageApplied);
    }

    [Fact]
    public void DeltaConsumerRoutesPathInfrastructureTelemetry()
    {
        RecordingPathInfrastructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 1)],
            canMarkDamaged: true));
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(
                pathInfrastructureFireSink: new TimberbornPathInfrastructureFireSink(targetApi)));

        TimberbornFireDeltaConsumerSummary summary = consumer.Consume(
            5,
            [new CellDelta(4, Cell(fuel: 8), Cell(fuel: 6))]);

        Assert.Equal(1, summary.PathInfrastructureConsideredDeltaCount);
        Assert.Equal(1, summary.PathInfrastructureDamagedTargetCount);
        Assert.Equal(2, summary.PathInfrastructureTotalDamageApplied);
    }

    private static TimberbornPathInfrastructureFireTarget Target(
        IReadOnlyList<TimberbornBurnDamageResourceStack> resources,
        bool canMarkDamaged = false,
        bool canBlockPath = false,
        bool repairEligible = false)
    {
        return new TimberbornPathInfrastructureFireTarget(
            "path-1",
            "Path.Folktails",
            CellIndex: 4,
            resources,
            canMarkDamaged,
            canBlockPath,
            repairEligible);
    }

    private static TimberbornFireCellDeltaDecision Decision(int cellIndex, int oldFuel, int newFuel)
    {
        return TimberbornFireCellDeltaDecision.FromDelta(
            new CellDelta(cellIndex, Cell(oldFuel), Cell(newFuel)));
    }

    private static ushort Cell(int fuel)
    {
        return PackedCell.Pack(fuel, heat: 10, flammability: 3, water: 0, terrain: 1, burningLevel: 0);
    }

    private sealed class RecordingPathInfrastructureTargetApi(TimberbornPathInfrastructureFireTarget? target)
        : ITimberbornPathInfrastructureFireTargetApi
    {
        public List<(TimberbornPathInfrastructureFireTarget Target, int DamageApplied)> AppliedDamage { get; } = [];

        public TimberbornPathInfrastructureFireTarget? ResolveTarget(
            TimberbornPathInfrastructureFireConsequence consequence)
        {
            return target is null ? null : target with { CellIndex = consequence.CellIndex };
        }

        public TimberbornPathInfrastructureApplyResult ApplyDamage(
            TimberbornPathInfrastructureFireTarget damageTarget,
            int damageApplied,
            bool isFullyDamaged)
        {
            AppliedDamage.Add((damageTarget, damageApplied));
            return new TimberbornPathInfrastructureApplyResult(
                AppliedDamage: damageTarget.CanMarkDamaged,
                AppliedBlock: false,
                SkippedNoSafeApi: !damageTarget.CanMarkDamaged,
                RepairEligible: damageTarget.RepairEligible);
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
