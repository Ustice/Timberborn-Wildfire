using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class TimberbornWaterInfrastructureFireEffectTests
{
    [Fact]
    public void SinkTreatsInertWaterInfrastructureAsValidNoOps()
    {
        RecordingWaterInfrastructureTargetApi targetApi = new(Target(
            resources:
            [
                new TimberbornBurnDamageResourceStack("Water", 3),
                new TimberbornBurnDamageResourceStack("Dirt", 2),
                new TimberbornBurnDamageResourceStack("MetalBlock", 1),
            ]));
        TimberbornWaterInfrastructureFireSink sink = new(targetApi);

        TimberbornWaterInfrastructureFireSummary summary = sink.ApplyConsequences(
            1,
            [Decision(4, oldFuel: 8, newFuel: 1)]);

        Assert.Equal(1, summary.MatchedTargetCellCount);
        Assert.Equal(1, summary.InertMaterialNoOpTargetCount);
        Assert.Equal(0, summary.BurnableMaterialValue);
        Assert.Equal(0, summary.DamagedTargetCount);
        Assert.Empty(targetApi.AppliedDamage);
    }

    [Fact]
    public void SinkRequiresBurnDamageInfrastructureOwnershipWhenProviderIsBound()
    {
        RecordingWaterInfrastructureTargetApi targetApi = new(Target(
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
        TimberbornWaterInfrastructureFireSink sink = new(
            targetApi,
            burnDamageTargets: burnDamageTargets);

        TimberbornWaterInfrastructureFireSummary summary = sink.ApplyConsequences(
            1,
            [Decision(4, oldFuel: 8, newFuel: 2)]);

        Assert.Equal(0, summary.MatchedTargetCellCount);
        Assert.Empty(targetApi.AppliedDamage);
    }

    [Fact]
    public void SinkUsesSharedBurnDamageStateWhenProviderOwnsWaterTarget()
    {
        RecordingWaterInfrastructureTargetApi targetApi = new(Target(
            resources: [],
            canMarkDamaged: true));
        TimberbornBurnDamageTestStateProvider burnDamageTargets = new(
            [
                TimberbornBurnDamageTestStateProvider.State(
                    "water-1",
                    "Floodgate.Folktails",
                    TimberbornBurnDamageTargetKind.Infrastructure,
                    damageCapacity: 10,
                    damageTaken: 6,
                    ownedCellIndices: [4]),
            ],
            [
                TimberbornBurnDamageTestStateProvider.AppliedEvent(
                    "water-1",
                    "Floodgate.Folktails",
                    sourceCellIndex: 4,
                    damageApplied: 6,
                    damageTaken: 6,
                    damageCapacity: 10,
                    tick: 1),
            ]);
        TimberbornWaterInfrastructureFireSink sink = new(
            targetApi,
            burnDamageTargets: burnDamageTargets);

        TimberbornWaterInfrastructureFireSummary summary = sink.ApplyConsequences(
            1,
            [Decision(4, oldFuel: 8, newFuel: 2)]);

        Assert.Equal(1, summary.MatchedTargetCellCount);
        Assert.Equal(3, summary.TotalDamageApplied);
        Assert.Equal([3], targetApi.AppliedDamage.Select(static damage => damage.DamageApplied).ToArray());
    }

    [Fact]
    public void SinkAppliesOnlyBurnableMixedResourcesAfterResistance()
    {
        RecordingWaterInfrastructureTargetApi targetApi = new(Target(
            resources:
            [
                new TimberbornBurnDamageResourceStack("Log", 1),
                new TimberbornBurnDamageResourceStack("Plank", 1),
                new TimberbornBurnDamageResourceStack("Water", 5),
            ],
            canMarkDamaged: true,
            repairEligible: true));
        TimberbornWaterInfrastructureFireSink sink = new(targetApi);

        TimberbornWaterInfrastructureFireSummary summary = sink.ApplyConsequences(
            2,
            [Decision(4, oldFuel: 9, newFuel: 2)]);

        Assert.Equal(1, summary.DamagedTargetCount);
        Assert.Equal(1, summary.RepairEligibleTargetCount);
        Assert.True(summary.BurnableMaterialValue > 0);
        Assert.Equal(4, summary.TotalDamageApplied);
        Assert.Equal([4], targetApi.AppliedDamage.Select(static damage => damage.DamageApplied).ToArray());
    }

    [Fact]
    public void SinkSuppressesDuplicateCellsForSameWaterTarget()
    {
        RecordingWaterInfrastructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 2)],
            canMarkDamaged: true));
        TimberbornWaterInfrastructureFireSink sink = new(targetApi);

        TimberbornWaterInfrastructureFireSummary summary = sink.ApplyConsequences(
            3,
            [
                Decision(4, oldFuel: 8, newFuel: 5),
                Decision(5, oldFuel: 9, newFuel: 2),
            ]);

        Assert.Equal(2, summary.MatchedTargetCellCount);
        Assert.Equal(1, summary.DuplicateTargetSuppressedCount);
        Assert.Equal(1, summary.DamagedTargetCount);
        Assert.Equal([4], targetApi.AppliedDamage.Select(static damage => damage.DamageApplied).ToArray());
    }

    [Fact]
    public void SinkRequiresDamageToClearDifficultToBurnResistance()
    {
        RecordingWaterInfrastructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 2)],
            canMarkDamaged: true));
        TimberbornWaterInfrastructureFireSink sink = new(targetApi);

        TimberbornWaterInfrastructureFireSummary summary = sink.ApplyConsequences(
            4,
            [Decision(4, oldFuel: 8, newFuel: 5)]);

        Assert.Equal(1, summary.DifficultToBurnNoOpTargetCount);
        Assert.Equal(0, summary.DamagedTargetCount);
        Assert.Equal(0, summary.TotalDamageApplied);
        Assert.Empty(targetApi.AppliedDamage);
    }

    [Fact]
    public void SinkThrowsWhenWaterMutationIsUnavailable()
    {
        RecordingWaterInfrastructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 1)],
            canMarkDamaged: false,
            canMutateWaterState: false));
        TimberbornWaterInfrastructureFireSink sink = new(targetApi);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => sink.ApplyConsequences(
            5,
            [Decision(4, oldFuel: 9, newFuel: 2)]));

        Assert.Contains("Water infrastructure damage mutation is unavailable", exception.Message);
    }

    [Fact]
    public void DeltaConsumerRoutesWaterInfrastructureTelemetry()
    {
        RecordingWaterInfrastructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 1)],
            canMarkDamaged: true));
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(
                waterInfrastructureFireSink: new TimberbornWaterInfrastructureFireSink(targetApi)));

        TimberbornFireDeltaConsumerSummary summary = consumer.Consume(
            6,
            [new CellDelta(4, Cell(fuel: 8), Cell(fuel: 3))]);

        Assert.Equal(1, summary.WaterInfrastructureConsideredDeltaCount);
        Assert.Equal(1, summary.WaterInfrastructureDamagedTargetCount);
        Assert.Equal(2, summary.WaterInfrastructureTotalDamageApplied);
    }

    private static TimberbornWaterInfrastructureFireTarget Target(
        IReadOnlyList<TimberbornBurnDamageResourceStack> resources,
        bool canMarkDamaged = false,
        bool canMutateWaterState = false,
        bool repairEligible = false)
    {
        return new TimberbornWaterInfrastructureFireTarget(
            "water-1",
            "Floodgate.Folktails",
            CellIndex: 4,
            resources,
            canMarkDamaged,
            canMutateWaterState,
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

    private sealed class RecordingWaterInfrastructureTargetApi(TimberbornWaterInfrastructureFireTarget? target)
        : ITimberbornWaterInfrastructureFireTargetApi
    {
        public List<(TimberbornWaterInfrastructureFireTarget Target, int DamageApplied)> AppliedDamage { get; } = [];

        public TimberbornWaterInfrastructureFireTarget? ResolveTarget(
            TimberbornWaterInfrastructureFireConsequence consequence)
        {
            return target is null ? null : target with { CellIndex = consequence.CellIndex };
        }

        public TimberbornWaterInfrastructureApplyResult ApplyDamage(
            TimberbornWaterInfrastructureFireTarget damageTarget,
            int damageApplied,
            bool isFullyDamaged)
        {
            AppliedDamage.Add((damageTarget, damageApplied));
            return new TimberbornWaterInfrastructureApplyResult(
                AppliedDamage: damageTarget.CanMarkDamaged,
                AttemptedWaterStateMutation: damageTarget.CanMutateWaterState && isFullyDamaged,
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
