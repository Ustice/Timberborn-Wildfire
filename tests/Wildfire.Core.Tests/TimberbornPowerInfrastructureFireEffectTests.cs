using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornPowerInfrastructureFireEffectTests
{
    [Fact]
    public void SinkTreatsMetalOnlyPowerPartsAsNoOps()
    {
        RecordingPowerInfrastructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("MetalBlock", 2)]));
        TimberbornPowerInfrastructureFireSink sink = new(targetApi);

        TimberbornPowerInfrastructureFireSummary summary = sink.ApplyConsequences(
            1,
            [Decision(4, oldFuel: 5, newFuel: 1)]);

        Assert.Equal(1, summary.MatchedTargetCellCount);
        Assert.Equal(1, summary.MetalOnlyNoOpTargetCount);
        Assert.Equal(0, summary.DamagedTargetCount);
        Assert.Empty(targetApi.AppliedDamage);
    }

    [Fact]
    public void SinkAppliesBurnCapacityFromWoodPowerResources()
    {
        RecordingPowerInfrastructureTargetApi targetApi = new(Target(
            resources:
            [
                new TimberbornBurnDamageResourceStack("Log", 1),
                new TimberbornBurnDamageResourceStack("Plank", 1),
            ],
            canMarkDamaged: true,
            repairEligible: true));
        TimberbornPowerInfrastructureFireSink sink = new(targetApi);

        TimberbornPowerInfrastructureFireSummary summary = sink.ApplyConsequences(
            2,
            [Decision(4, oldFuel: 8, newFuel: 3)]);

        Assert.Equal(1, summary.DamagedTargetCount);
        Assert.Equal(1, summary.RepairEligibleTargetCount);
        Assert.Equal(5, summary.TotalDamageApplied);
        Assert.Equal([5], targetApi.AppliedDamage.Select(static damage => damage.DamageApplied).ToArray());
    }

    [Fact]
    public void SinkSuppressesDuplicateCellsForSamePowerTarget()
    {
        RecordingPowerInfrastructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 2)],
            canMarkDamaged: true));
        TimberbornPowerInfrastructureFireSink sink = new(targetApi);

        TimberbornPowerInfrastructureFireSummary summary = sink.ApplyConsequences(
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
    public void SinkReportsNoSafeApiWhenPowerMutationIsUnavailable()
    {
        RecordingPowerInfrastructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 1)],
            canMarkDamaged: false));
        TimberbornPowerInfrastructureFireSink sink = new(targetApi);

        TimberbornPowerInfrastructureFireSummary summary = sink.ApplyConsequences(
            4,
            [Decision(4, oldFuel: 8, newFuel: 3)]);

        Assert.Equal(0, summary.DamagedTargetCount);
        Assert.Equal(1, summary.SkippedNoSafeApiCount);
        Assert.Equal(5, summary.TotalDamageApplied);
    }

    [Fact]
    public void DeltaConsumerRoutesPowerInfrastructureTelemetry()
    {
        RecordingPowerInfrastructureTargetApi targetApi = new(Target(
            resources: [new TimberbornBurnDamageResourceStack("Log", 1)],
            canMarkDamaged: true));
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(
                powerInfrastructureFireSink: new TimberbornPowerInfrastructureFireSink(targetApi)));

        TimberbornFireDeltaConsumerSummary summary = consumer.Consume(
            5,
            [new CellDelta(4, Cell(fuel: 8), Cell(fuel: 6))]);

        Assert.Equal(1, summary.PowerInfrastructureConsideredDeltaCount);
        Assert.Equal(1, summary.PowerInfrastructureDamagedTargetCount);
        Assert.Equal(2, summary.PowerInfrastructureTotalDamageApplied);
    }

    private static TimberbornPowerInfrastructureFireTarget Target(
        IReadOnlyList<TimberbornBurnDamageResourceStack> resources,
        bool canMarkDamaged = false,
        bool canDisableOrDisconnect = false,
        bool repairEligible = false)
    {
        return new TimberbornPowerInfrastructureFireTarget(
            "power-1",
            "PowerShaft.Folktails",
            CellIndex: 4,
            resources,
            canMarkDamaged,
            canDisableOrDisconnect,
            repairEligible);
    }

    private static TimberbornFireCellDeltaDecision Decision(int cellIndex, int oldFuel, int newFuel)
    {
        return TimberbornFireCellDeltaDecision.FromDelta(
            new CellDelta(cellIndex, Cell(oldFuel), Cell(newFuel)));
    }

    private static ushort Cell(int fuel)
    {
        return PackedCell.Pack(fuel, heat: 10, flammability: 3, water: 0, terrain: 1, heatLoss: 0);
    }

    private sealed class RecordingPowerInfrastructureTargetApi(TimberbornPowerInfrastructureFireTarget? target)
        : ITimberbornPowerInfrastructureFireTargetApi
    {
        public List<(TimberbornPowerInfrastructureFireTarget Target, int DamageApplied)> AppliedDamage { get; } = [];

        public TimberbornPowerInfrastructureFireTarget? ResolveTarget(
            TimberbornPowerInfrastructureFireConsequence consequence)
        {
            return target is null ? null : target with { CellIndex = consequence.CellIndex };
        }

        public TimberbornPowerInfrastructureApplyResult ApplyDamage(
            TimberbornPowerInfrastructureFireTarget damageTarget,
            int damageApplied,
            bool isFullyDamaged)
        {
            AppliedDamage.Add((damageTarget, damageApplied));
            return new TimberbornPowerInfrastructureApplyResult(
                AppliedDamage: damageTarget.CanMarkDamaged,
                DisabledOrDisconnected: damageTarget.CanDisableOrDisconnect && isFullyDamaged,
                SkippedNoSafeApi: !damageTarget.CanMarkDamaged && !damageTarget.CanDisableOrDisconnect,
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
