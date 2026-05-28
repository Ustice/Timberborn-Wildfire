using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class TimberbornStoredGoodBurnConsequenceTests
{
    [Fact]
    public void SinkDestroysBurnableStoredGoodsByFuelValueBudget()
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
        Assert.Equal(1, summary.DestroyedItemCount);
        Assert.Equal(20, summary.SkippedNonBurnableItemCount);
        Assert.Equal([new TimberbornStoredGoodStack("Log", 1)], inventoryApi.DestroyedStacks);
    }

    [Fact]
    public void SinkCarriesPartialFuelValueBurnProgressAcrossTicks()
    {
        RecordingStoredGoodBurnInventoryApi inventoryApi = new(
            new TimberbornStoredGoodBurnTarget(
                "storage-1",
                [new TimberbornStoredGoodStack("Log", 5)],
                CanMutateInventory: true));
        TimberbornStoredGoodBurnConsequenceSink sink = new(inventoryApi);

        TimberbornStoredGoodBurnConsequenceSummary first = sink.ApplyConsequences(
            8,
            [Decision(4, oldFuel: 5, newFuel: 4)]);
        TimberbornStoredGoodBurnConsequenceSummary second = sink.ApplyConsequences(
            9,
            [Decision(4, oldFuel: 4, newFuel: 3)]);

        Assert.Equal(0, first.DestroyedItemCount);
        Assert.Equal(1, second.DestroyedItemCount);
        Assert.Equal([new TimberbornStoredGoodStack("Log", 1)], inventoryApi.DestroyedStacks);
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
    public void SinkAcceptsStorageInventoryOwnedByStructureRollbackTarget()
    {
        RecordingStoredGoodBurnInventoryApi inventoryApi = new(
            new TimberbornStoredGoodBurnTarget(
                "stockpile:1",
                [new TimberbornStoredGoodStack("Log", 2)],
                CanMutateInventory: true));
        TimberbornBurnDamageTestStateProvider burnDamageTargets = new(
            [
                TimberbornBurnDamageTestStateProvider.State(
                    "stockpile:1",
                    "SmallWarehouse.Folktails",
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
            [Decision(4, oldFuel: 5, newFuel: 3)]);

        Assert.Equal(1, summary.MatchedStorageCellCount);
        Assert.Equal(1, summary.DestroyedItemCount);
        Assert.Equal([new TimberbornStoredGoodStack("Log", 1)], inventoryApi.DestroyedStacks);
    }

    [Fact]
    public void SinkResolvesStructureRollbackStorageInventoryFromBurnDamageState()
    {
        StateAwareStoredGoodBurnInventoryApi inventoryApi = new(
            directTarget: null,
            stateTarget: new TimberbornStoredGoodBurnTarget(
                "stockpile:1",
                [new TimberbornStoredGoodStack("Log", 2)],
                CanMutateInventory: true));
        TimberbornBurnDamageTestStateProvider burnDamageTargets = new(
            [
                TimberbornBurnDamageTestStateProvider.State(
                    "stockpile:1",
                    "LargeWarehouse.Folktails(Clone)",
                    TimberbornBurnDamageTargetKind.Structure,
                    damageCapacity: 8,
                    damageTaken: 3,
                    ownedCellIndices: [4, 5]),
            ]);
        TimberbornStoredGoodBurnConsequenceSink sink = new(
            inventoryApi,
            burnDamageTargets: burnDamageTargets);

        TimberbornStoredGoodBurnConsequenceSummary summary = sink.ApplyConsequences(
            8,
            [Decision(4, oldFuel: 5, newFuel: 3)]);

        Assert.Equal(1, summary.MatchedStorageCellCount);
        Assert.Equal(1, summary.DestroyedItemCount);
        Assert.Equal("stockpile:1", inventoryApi.ResolvedStateKeys.Single());
        Assert.Equal([new TimberbornStoredGoodStack("Log", 1)], inventoryApi.DestroyedStacks);
    }

    [Fact]
    public void SinkPrefersBurnDamageStateStorageInventoryWhenDirectCellLookupFindsWrongStockpile()
    {
        StateAwareStoredGoodBurnInventoryApi inventoryApi = new(
            directTarget: new TimberbornStoredGoodBurnTarget(
                "stockpile:wrong",
                [new TimberbornStoredGoodStack("Log", 9)],
                CanMutateInventory: true),
            stateTarget: new TimberbornStoredGoodBurnTarget(
                "stockpile:current",
                [new TimberbornStoredGoodStack("Log", 2)],
                CanMutateInventory: true));
        TimberbornBurnDamageTestStateProvider burnDamageTargets = new(
            [
                TimberbornBurnDamageTestStateProvider.State(
                    "stockpile:1",
                    "SmallTank.Folktails(Clone)",
                    TimberbornBurnDamageTargetKind.Structure,
                    damageCapacity: 8,
                    damageTaken: 3,
                    ownedCellIndices: [4, 5]),
            ]);
        TimberbornStoredGoodBurnConsequenceSink sink = new(
            inventoryApi,
            burnDamageTargets: burnDamageTargets);

        TimberbornStoredGoodBurnConsequenceSummary summary = sink.ApplyConsequences(
            8,
            [Decision(4, oldFuel: 5, newFuel: 3)]);

        Assert.Equal(1, summary.MatchedStorageCellCount);
        Assert.Equal(1, summary.DestroyedItemCount);
        Assert.Equal("stockpile:1", inventoryApi.ResolvedStateKeys.Single());
        Assert.Equal([new TimberbornStoredGoodStack("Log", 1)], inventoryApi.DestroyedStacks);
    }

    [Fact]
    public void SinkThrowsWhenLiveInventoryApiCannotResolveOwnedStorage()
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

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => sink.ApplyConsequences(
            8,
            [Decision(4, oldFuel: 5, newFuel: 2)]));

        Assert.Contains("failed to resolve inventory target", exception.Message);
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
    public void SinkThrowsWhenInventoryMutationIsUnavailable()
    {
        RecordingStoredGoodBurnInventoryApi inventoryApi = new(
            new TimberbornStoredGoodBurnTarget(
                "storage-1",
                [new TimberbornStoredGoodStack("Paper", 6)],
                CanMutateInventory: false));
        TimberbornStoredGoodBurnConsequenceSink sink = new(inventoryApi);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => sink.ApplyConsequences(
            10,
            [Decision(4, oldFuel: 5, newFuel: 1)]));

        Assert.Contains("inventory mutation is unavailable", exception.Message);
        Assert.Empty(inventoryApi.DestroyedStacks);
    }

    [Fact]
    public void SinkCountsHazardousAndUnknownGoodsWithoutDestroyingThem()
    {
        RecordingStoredGoodHazardConsequenceSink hazardSink = new();
        RecordingStoredGoodBurnInventoryApi inventoryApi = new(
            new TimberbornStoredGoodBurnTarget(
                "storage-1",
                [
                    new TimberbornStoredGoodStack("Explosives", 2),
                    new TimberbornStoredGoodStack("MysteryGood", 3),
                    new TimberbornStoredGoodStack("Paper", 5),
                ],
                CanMutateInventory: true));
        TimberbornStoredGoodBurnConsequenceSink sink = new(inventoryApi, hazardSink);

        TimberbornStoredGoodBurnConsequenceSummary summary = sink.ApplyConsequences(
            11,
            [Decision(4, oldFuel: 5, newFuel: 0)]);

        Assert.Equal(2, summary.HazardousGoodCount);
        Assert.Equal(2, summary.ExplosiveGoodCount);
        Assert.Equal(1, summary.ExplosiveBlastTriggeredCount);
        Assert.Equal(1, summary.UnknownResourceCount);
        Assert.Equal(5, summary.DestroyedItemCount);
        Assert.Equal(
            [
                new TimberbornStoredGoodStack("Explosives", 2),
                new TimberbornStoredGoodStack("Paper", 3),
            ],
            inventoryApi.DestroyedStacks);
        Assert.Equal([new TimberbornStoredGoodHazardStack("Explosives", 2, 1, Explosive: true, Contaminated: false)], hazardSink.HazardStacks);
    }

    [Fact]
    public void SinkRoutesContaminatedStoredGoodsToHazardConsequences()
    {
        RecordingStoredGoodHazardConsequenceSink hazardSink = new();
        RecordingStoredGoodBurnInventoryApi inventoryApi = new(
            new TimberbornStoredGoodBurnTarget(
                "storage-1",
                [new TimberbornStoredGoodStack("Badwater", 4)],
                CanMutateInventory: true));
        TimberbornStoredGoodBurnConsequenceSink sink = new(inventoryApi, hazardSink);

        TimberbornStoredGoodBurnConsequenceSummary summary = sink.ApplyConsequences(
            12,
            [Decision(4, oldFuel: 5, newFuel: 4)]);

        Assert.Equal(4, summary.HazardousGoodCount);
        Assert.Equal(4, summary.ContaminatedGoodCount);
        Assert.Equal(1, summary.ContaminationPulseCellCount);
        Assert.Equal(4, summary.DestroyedItemCount);
        Assert.Equal([new TimberbornStoredGoodStack("Badwater", 4)], inventoryApi.DestroyedStacks);
        Assert.Equal([new TimberbornStoredGoodHazardStack("Badwater", 4, 0, Explosive: false, Contaminated: true)], hazardSink.HazardStacks);
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
        Assert.Equal(1, summary.StoredGoodBurnDestroyedItemCount);
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
                ExplosiveGoodCount: 0,
                ExplosiveBlastTriggeredCount: 0,
                ContaminatedGoodCount: 0,
                ContaminationPulseCellCount: 0,
                UnknownResourceCount: 0,
                SkippedNonBurnableItemCount: 0);
        }
    }

    private sealed class StateAwareStoredGoodBurnInventoryApi(
        TimberbornStoredGoodBurnTarget? directTarget,
        TimberbornStoredGoodBurnTarget? stateTarget)
        : ITimberbornStoredGoodBurnInventoryApi, ITimberbornStoredGoodBurnDamageInventoryApi
    {
        public List<TimberbornStoredGoodStack> DestroyedStacks { get; } = [];

        public List<string> ResolvedStateKeys { get; } = [];

        public TimberbornStoredGoodBurnTarget? ResolveTarget(TimberbornStoredGoodBurnConsequence consequence)
        {
            return directTarget;
        }

        public TimberbornStoredGoodBurnTarget? ResolveTarget(TimberbornBurnDamageTargetState state)
        {
            ResolvedStateKeys.Add(state.TargetKey.StableId);
            return stateTarget;
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
                ExplosiveGoodCount: 0,
                ExplosiveBlastTriggeredCount: 0,
                ContaminatedGoodCount: 0,
                ContaminationPulseCellCount: 0,
                UnknownResourceCount: 0,
                SkippedNonBurnableItemCount: 0);
        }
    }

    private sealed class RecordingStoredGoodHazardConsequenceSink : ITimberbornStoredGoodHazardConsequenceSink
    {
        public List<TimberbornStoredGoodHazardStack> HazardStacks { get; } = [];

        public TimberbornStoredGoodHazardConsequenceResult ApplyHazards(
            TimberbornStoredGoodBurnTarget target,
            TimberbornStoredGoodBurnConsequence consequence,
            IReadOnlyList<TimberbornStoredGoodHazardStack> hazardStacks)
        {
            HazardStacks.AddRange(hazardStacks);
            return new TimberbornStoredGoodHazardConsequenceResult(
                ExplosiveGoodCount: hazardStacks.Where(static stack => stack.Explosive).Sum(static stack => stack.Amount),
                ExplosiveBlastTriggeredCount: hazardStacks.Any(static stack => stack.Explosive) ? 1 : 0,
                NativeBlastAffectedTileCount: hazardStacks.Any(static stack => stack.Explosive) ? 1 : 0,
                ContaminatedGoodCount: hazardStacks.Where(static stack => stack.Contaminated).Sum(static stack => stack.Amount),
                ContaminationPulseCellCount: hazardStacks.Any(static stack => stack.Contaminated) ? 1 : 0);
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
