using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornStoredGoodBurnConsequenceTests
{
    [Fact]
    public void SinkDestroysBurnableStoredGoodsWithPartialStackBudget()
    {
        RecordingStoredGoodBurnInventoryApi inventoryApi = new(
            new TimberbornStoredGoodBurnTarget(
                "storage-1",
                [
                    new TimberbornStoredGoodStack("Log", 5),
                    new TimberbornStoredGoodStack("Water", 20),
                ],
                CanMutateInventory: true));
        TimberbornStoredGoodBurnConsequenceSink sink = new(inventoryApi);

        TimberbornStoredGoodBurnConsequenceSummary summary = sink.ApplyConsequences(
            8,
            [Decision(4, oldFuel: 5, newFuel: 2)]);

        Assert.Equal(1, summary.ConsideredDeltaCount);
        Assert.Equal(1, summary.MatchedStorageCellCount);
        Assert.Equal(1, summary.BurnableStackCount);
        Assert.Equal(3, summary.DestroyedItemCount);
        Assert.Equal(20, summary.SkippedNonBurnableItemCount);
        Assert.Equal([new TimberbornStoredGoodStack("Log", 3)], inventoryApi.DestroyedStacks);
    }

    [Fact]
    public void SinkRequiresBurnDamageStorageOwnershipWhenProviderIsBound()
    {
        RecordingStoredGoodBurnInventoryApi inventoryApi = new(
            new TimberbornStoredGoodBurnTarget(
                "storage-1",
                [new TimberbornStoredGoodStack("Log", 5)],
                CanMutateInventory: true));
        TimberbornBurnDamageTestStateProvider burnDamageTargets = new(
            [
                TimberbornBurnDamageTestStateProvider.State(
                    "structure-1",
                    "Building.LumberMill",
                    TimberbornBurnDamageTargetKind.Structure,
                    damageCapacity: 8,
                    damageTaken: 3,
                    ownedCellIndices: [4]),
            ]);
        TimberbornStoredGoodBurnConsequenceSink sink = new(
            inventoryApi,
            burnDamageTargets: burnDamageTargets);

        TimberbornStoredGoodBurnConsequenceSummary summary = sink.ApplyConsequences(
            8,
            [Decision(4, oldFuel: 5, newFuel: 2)]);

        Assert.Equal(0, summary.MatchedStorageCellCount);
        Assert.Empty(inventoryApi.DestroyedStacks);
    }

    [Fact]
    public void SinkReportsOwnedStorageWhenLiveInventoryApiCannotResolveTarget()
    {
        RecordingStoredGoodBurnInventoryApi inventoryApi = new(target: null);
        TimberbornBurnDamageTestStateProvider burnDamageTargets = new(
            [
                TimberbornBurnDamageTestStateProvider.State(
                    "storage-1",
                    "Warehouse.Folktails",
                    TimberbornBurnDamageTargetKind.Storage,
                    damageCapacity: 8,
                    damageTaken: 3,
                    ownedCellIndices: [4]),
            ]);
        TimberbornStoredGoodBurnConsequenceSink sink = new(
            inventoryApi,
            burnDamageTargets: burnDamageTargets);

        TimberbornStoredGoodBurnConsequenceSummary summary = sink.ApplyConsequences(
            8,
            [Decision(4, oldFuel: 5, newFuel: 2)]);

        Assert.Equal(1, summary.MatchedStorageCellCount);
        Assert.Equal(3, summary.SkippedNoInventoryApiCount);
        Assert.Empty(inventoryApi.DestroyedStacks);
    }

    [Fact]
    public void SinkSuppressesDuplicateCellsForSameStorageTarget()
    {
        RecordingStoredGoodBurnInventoryApi inventoryApi = new(
            new TimberbornStoredGoodBurnTarget(
                "storage-1",
                [new TimberbornStoredGoodStack("Plank", 10)],
                CanMutateInventory: true));
        TimberbornStoredGoodBurnConsequenceSink sink = new(inventoryApi);

        TimberbornStoredGoodBurnConsequenceSummary summary = sink.ApplyConsequences(
            9,
            [
                Decision(4, oldFuel: 5, newFuel: 3),
                Decision(5, oldFuel: 5, newFuel: 1),
            ]);

        Assert.Equal(2, summary.ConsideredDeltaCount);
        Assert.Equal(2, summary.MatchedStorageCellCount);
        Assert.Equal(1, summary.DuplicateStorageTargetSuppressedCount);
        Assert.Equal(4, summary.DestroyedItemCount);
        Assert.Equal([new TimberbornStoredGoodStack("Plank", 4)], inventoryApi.DestroyedStacks);
    }

    [Fact]
    public void SinkSkipsMutationWhenInventoryApiIsUnavailable()
    {
        RecordingStoredGoodBurnInventoryApi inventoryApi = new(
            new TimberbornStoredGoodBurnTarget(
                "storage-1",
                [new TimberbornStoredGoodStack("Paper", 6)],
                CanMutateInventory: false));
        TimberbornStoredGoodBurnConsequenceSink sink = new(inventoryApi);

        TimberbornStoredGoodBurnConsequenceSummary summary = sink.ApplyConsequences(
            10,
            [Decision(4, oldFuel: 5, newFuel: 1)]);

        Assert.Equal(1, summary.BurnableStackCount);
        Assert.Equal(0, summary.DestroyedItemCount);
        Assert.Equal(6, summary.SkippedNoInventoryApiCount);
        Assert.Empty(inventoryApi.DestroyedStacks);
    }

    [Fact]
    public void SinkCountsHazardousAndUnknownGoodsWithoutDestroyingThem()
    {
        RecordingStoredGoodBurnInventoryApi inventoryApi = new(
            new TimberbornStoredGoodBurnTarget(
                "storage-1",
                [
                    new TimberbornStoredGoodStack("Explosives", 2),
                    new TimberbornStoredGoodStack("MysteryGood", 3),
                    new TimberbornStoredGoodStack("Carrot", 5),
                ],
                CanMutateInventory: true));
        TimberbornStoredGoodBurnConsequenceSink sink = new(inventoryApi);

        TimberbornStoredGoodBurnConsequenceSummary summary = sink.ApplyConsequences(
            11,
            [Decision(4, oldFuel: 5, newFuel: 0)]);

        Assert.Equal(2, summary.HazardousGoodCount);
        Assert.Equal(1, summary.SkippedUnknownResourceCount);
        Assert.Equal(5, summary.DestroyedItemCount);
        Assert.Equal([new TimberbornStoredGoodStack("Carrot", 5)], inventoryApi.DestroyedStacks);
    }

    [Fact]
    public void DeltaConsumerRoutesFuelLossToStoredGoodsSinkTelemetry()
    {
        RecordingStoredGoodBurnInventoryApi inventoryApi = new(
            new TimberbornStoredGoodBurnTarget(
                "storage-1",
                [new TimberbornStoredGoodStack("Log", 4)],
                CanMutateInventory: true));
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(
                storedGoodBurnConsequenceSink: new TimberbornStoredGoodBurnConsequenceSink(inventoryApi)));

        TimberbornFireDeltaConsumerSummary summary = consumer.Consume(
            12,
            [new CellDelta(7, Cell(fuel: 4, heat: 10), Cell(fuel: 1, heat: 10))]);

        Assert.Equal(1, summary.StoredGoodBurnConsideredDeltaCount);
        Assert.Equal(1, summary.StoredGoodBurnMatchedStorageCellCount);
        Assert.Equal(3, summary.StoredGoodBurnDestroyedItemCount);
    }

    private static TimberbornFireCellDeltaDecision Decision(int cellIndex, int oldFuel, int newFuel)
    {
        return TimberbornFireCellDeltaDecision.FromDelta(
            new CellDelta(cellIndex, Cell(oldFuel, heat: 10), Cell(newFuel, heat: 10)));
    }

    private static ushort Cell(int fuel, int heat, int flammability = 3, int water = 0, int terrain = 1, int burningLevel = 0)
    {
        return PackedCell.Pack(fuel, heat, flammability, water, terrain, burningLevel);
    }

    private sealed class RecordingStoredGoodBurnInventoryApi(TimberbornStoredGoodBurnTarget? target)
        : ITimberbornStoredGoodBurnInventoryApi
    {
        public List<TimberbornStoredGoodStack> DestroyedStacks { get; } = [];

        public TimberbornStoredGoodBurnTarget? ResolveTarget(TimberbornStoredGoodBurnConsequence consequence)
        {
            return target;
        }

        public TimberbornStoredGoodBurnConsequenceResult BurnStoredGoods(
            TimberbornStoredGoodBurnTarget burnTarget,
            int burnBudget,
            IReadOnlyList<TimberbornStoredGoodStack> stacksToDestroy)
        {
            DestroyedStacks.AddRange(stacksToDestroy);
            return new TimberbornStoredGoodBurnConsequenceResult(
                MatchedStorageCell: true,
                AppliedConsequence: stacksToDestroy.Count > 0,
                BurnableStackCount: 0,
                DestroyedItemCount: stacksToDestroy.Sum(static stack => stack.Amount),
                HazardousGoodCount: 0,
                SkippedNoInventoryApiCount: 0,
                SkippedUnknownResourceCount: 0,
                SkippedNonBurnableItemCount: 0);
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
