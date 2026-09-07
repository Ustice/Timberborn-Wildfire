using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedStorageDeltaConsumerTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly FireGrid Grid = new(2, 1, 1);

    [Fact]
    public void SameCellOriginsUseTheirOwnInventoryAndSumDistinctCellDamage()
    {
        var f = new Fixture();
        var result = f.Consumer.Consume(1, [f.Delta(A, 2), f.Delta(A, 4, 1), f.Delta(B, 2)]);
        Assert.Equal(6, f.Damage.States[Registration(A).TargetKey].DamageTaken);
        Assert.Equal(2, f.Damage.States[Registration(B).TargetKey].DamageTaken);
        Assert.Equal(2, result.Damage.DamageAppliedTargetCount);
        Assert.Equal(0, result.Damage.DuplicateCellSuppressedCount);
        Assert.Equal(8, f.Inventory.Stock[A]["Log"]); // Raw storage sink still has its separate per-owner budget rule.
        Assert.Equal(9, f.Inventory.Stock[B]["Log"]);
        Assert.Equal(3, result.DestroyedItems);
    }

    [Fact]
    public void RemovedOriginCannotConsumeReplacementEvenAfterBindingRestore()
    {
        var f = new Fixture();
        f.Inventory.Live.Remove(A);
        f.Damage.RemoveTarget(Registration(A).TargetKey);
        f.Registry.Reconcile([], [A]);
        var restored = new TimberbornNativeMaterialRegistry(Grid, []);
        restored.RestoreBindings(f.Registry.CaptureBindings());
        var consumer = new TimberbornOwnedStorageDeltaConsumer(restored, f.Damage, f.Inventory, f.Hazards,
            f.Resources, [Registration(B)], f.Catalog);
        // Retain registered origin in the original session even after the live state disappears.
        var result = f.Consumer.Consume(2, [f.Delta(A, 15)]);
        Assert.Equal(1, result.NotLiveCount);
        Assert.Empty(f.Inventory.Calls);
        Assert.Equal(10, f.Inventory.Stock[B]["Log"]);
        // A fresh consumer cannot silently recreate deleted damage registrations from current cells.
        Assert.Throws<NotSupportedException>(() => consumer.Consume(2, [f.Delta(A, 15)]));
    }

    [Fact]
    public void HiddenLiveOriginStillConsumesItsExactInventory()
    {
        var f = new Fixture();
        f.Registry.Reconcile([], [A]);
        f.Damage.SuspendTarget(Registration(A).TargetKey);
        Assert.Equal(B, f.Registry.ResolveCell(0).Owner!.Value.EntityId);
        f.Consumer.Consume(3, [f.Delta(A, 2)]);
        Assert.Equal(9, f.Inventory.Stock[A]["Log"]);
        Assert.Equal(10, f.Inventory.Stock[B]["Log"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TrailingUnknownOrUnregisteredOwnerRejectsWholeBatchBeforeEffects(bool unregistered)
    {
        var f = new Fixture(registerB: !unregistered);
        var bad = unregistered ? f.Delta(B, 15) : new CellDelta(0, 0, 0, 99);
        Assert.ThrowsAny<Exception>(() => f.Consumer.Consume(4, [f.Delta(A, 15), bad]));
        Assert.Empty(f.Inventory.Calls);
        Assert.All(f.Damage.States.Values, state => Assert.Equal(0, state.DamageTaken));
        Assert.False(f.Resources.IsIndeterminate); // Identity rejection preceded the mutation guard.
    }

    [Fact]
    public void ZeroOriginDoesNotBorrowStorageUnderTheCell()
    {
        var f = new Fixture();
        var result = f.Consumer.Consume(4, [new CellDelta(0, 0xffff, 0, 0)]);
        Assert.Equal(1, result.UnownedCount);
        Assert.Empty(f.Inventory.Calls);
    }

    [Fact]
    public void DeletionAfterFirstRemovalStopsNextStackAndNeverUsesOtherOwner()
    {
        var f = new Fixture();
        f.Inventory.Stock[A] = new() { ["Dynamite"] = 1, ["Log"] = 4 };
        f.Inventory.AfterConsume = (_, _) => f.Inventory.Live.Remove(A);
        var result = f.Consumer.Consume(5, [f.Delta(A, 5)]);
        Assert.Equal(1, result.DestroyedItems);
        Assert.Equal(1, result.HazardousItems);
        Assert.Equal(1, result.NotLiveCount);
        Assert.Equal(4, f.Inventory.Stock[A]["Log"]);
        Assert.Equal(10, f.Inventory.Stock[B]["Log"]);
        Assert.All(f.Inventory.Calls, id => Assert.Equal(A, id));
        Assert.Equal(new[] { "consume:Dynamite:1", "hazard:1" }, f.Events);
    }

    [Fact]
    public void HazardsUseActualRemovalAfterAvailabilityChangesAndKeepOriginLocation()
    {
        var f = new Fixture();
        f.Inventory.Stock[A] = new() { ["Dynamite"] = 5 };
        f.Inventory.BeforeConsume = (id, _) => f.Inventory.Stock[id]["Dynamite"] = 1;
        var result = f.Consumer.Consume(6, [f.Delta(A, 4, 1)]);
        Assert.Equal(1, result.DestroyedItems);
        Assert.Equal(1, result.HazardousItems);
        Assert.Equal(1, f.Hazards.LastCell);
        Assert.Equal(new[] { "consume:Dynamite:1", "hazard:1" }, f.Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailureAfterConsumptionPoisonsSharedRuntimeGuardAndNeverRetries(bool hazardFailure)
    {
        var f = new Fixture();
        f.Inventory.Stock[A] = new() { ["Dynamite"] = 5 };
        var failure = new ApplicationException("callback after physical removal");
        if (hazardFailure) f.Hazards.Callback = () => throw failure;
        else f.Inventory.AfterConsume = (_, _) => throw failure;
        Assert.Same(failure, Assert.Throws<ApplicationException>(() => f.Consumer.Consume(7, [f.Delta(A, 2)])));
        Assert.Equal(3, f.Inventory.Stock[A]["Dynamite"]);
        Assert.True(f.Resources.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(f.Resources.ThrowIfSaveUnsafe);
        Assert.Throws<InvalidOperationException>(() => f.Consumer.Consume(8, [f.Delta(A, 2)]));
        Assert.Equal(3, f.Inventory.Stock[A]["Dynamite"]);
    }

    [Fact]
    public void SaveDuringNativeCallbackFailsBeforeItCanCapturePartialState()
    {
        var f = new Fixture();
        f.Inventory.AfterConsume = (_, _) => Assert.Throws<InvalidOperationException>(f.Resources.ThrowIfSaveUnsafe);
        f.Consumer.Consume(9, [f.Delta(A, 2)]);
        Assert.False(f.Resources.IsIndeterminate);
        f.Resources.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void FractionalCreditSurvivesTicksButIsExplicitlyNotRestoredByNewConsumer()
    {
        var f = new Fixture();
        Assert.Equal(0, f.Consumer.Consume(10, [f.Delta(A, 1)]).DestroyedItems);
        Assert.True(f.Consumer.HasTransientFuelCredit);
        Assert.Equal(1, f.Consumer.Consume(11, [f.Delta(A, 1)]).DestroyedItems);
        Assert.False(f.Consumer.HasTransientFuelCredit);
        f.Consumer.Consume(12, [f.Delta(A, 1)]);
        var reconstructed = f.CreateConsumer(); // No credit import exists; this is the activation gap, not a save proof.
        Assert.False(reconstructed.HasTransientFuelCredit);
        Assert.Equal(0, reconstructed.Consume(13, [f.Delta(A, 1)]).DestroyedItems);
    }

    [Fact]
    public void PendingHazardCreditCannotAlsoFundAnOrdinaryStack()
    {
        var f = new Fixture();
        f.Inventory.Stock[A] = new() { ["Dynamite"] = 1, ["Log"] = 1 };
        var catalog = new TimberbornResourceFuelCatalog([
            new("Log", 2, 3, false, false, true), new("Dynamite", 3, 3, true, false, true)]);
        var consumer = new TimberbornOwnedStorageDeltaConsumer(f.Registry, f.Damage, f.Inventory, f.Hazards,
            f.Resources, [Registration(A), Registration(B)], catalog);
        for (uint tick = 1; tick <= 3; tick++) consumer.Consume(tick, [f.Delta(A, 1)]);
        Assert.Equal(0, f.Inventory.Stock[A]["Dynamite"]);
        Assert.Equal(1, f.Inventory.Stock[A]["Log"]); // Three budget units fund only the cost-three hazard.
    }

    [Fact]
    public void ConflictingInventoryRolesCannotRegisterTwoBodyStatesForOneGuid()
    {
        var f = new Fixture();
        var output = new TimberbornOwnedStorageRegistration(A, TimberbornOwnedInventoryRole.SimpleOutput);
        f.Damage.UpsertTarget(Grid, new(output.TargetKey, "Output", [new(0, 0, 0)], 1));
        Assert.Throws<ArgumentException>(() => f.Consumer.Register(output));
        Assert.Empty(f.Inventory.Calls);
    }

    private static TimberbornOwnedStorageRegistration Registration(Guid id) => new(id, TimberbornOwnedInventoryRole.Stockpile);
    private sealed class Fixture
    {
        internal readonly TimberbornNativeMaterialRegistry Registry = new(Grid, []);
        internal readonly TimberbornBurnDamageService Damage = new(new TimberbornBurnDamageDescriptorCatalog(
            [new("Warehouse", TimberbornBurnDamageTargetKind.Storage, TimberbornBurnMaterialKind.Constructed, constructionResources: [new("Log", 20)]),
                new("Output", TimberbornBurnDamageTargetKind.Structure, TimberbornBurnMaterialKind.Constructed, constructionResources: [new("Log", 20)])]));
        internal readonly NativeResourceTransaction Resources = new();
        internal readonly List<string> Events = [];
        internal readonly InventoryFake Inventory;
        internal readonly HazardFake Hazards;
        internal readonly TimberbornOwnedStorageDeltaConsumer Consumer;
        internal readonly TimberbornResourceFuelCatalog Catalog = new([
            new("Log", 2, 3, false, false, true), new("Dynamite", 1, 3, true, false, true)]);
        private readonly bool _registerB;
        internal Fixture(bool registerB = true)
        {
            _registerB = registerB;
            Registry.Reconcile(new[] { A, B }.Select(id => new TimberbornMaterialProjection(id,
                [new(new(0, 0, 0), 0), new(new(1, 0, 0), 1)], [TimberbornMaterialPart.StoredGood("Log")])), []);
            Damage.RegisterTargets(Grid, [new(Registration(A).TargetKey, "Warehouse", [new(0, 0, 0), new(1, 0, 0)], 10),
                new(Registration(B).TargetKey, "Warehouse", [new(0, 0, 0)], 20)]);
            Inventory = new(Events); Hazards = new(Events);
            Consumer = CreateConsumer();
        }
        internal TimberbornOwnedStorageDeltaConsumer CreateConsumer() => new(Registry, Damage, Inventory, Hazards, Resources,
            _registerB ? [Registration(A), Registration(B)] : [Registration(A)], Catalog);
        internal CellDelta Delta(Guid id, int budget, int cell = 0) => new(cell,
            PackedCell.Pack(15, 10, 3, 0, 0, 1), PackedCell.Pack(15 - budget, 10, 3, 0, 0, 1),
            Registry.CaptureBindings().Entities.Single(binding => binding.EntityId == id).TargetId);
    }
    private sealed class InventoryFake(List<string> events) : ITimberbornOwnedStorageInventoryApi
    {
        internal readonly HashSet<Guid> Live = [A, B];
        internal readonly Dictionary<Guid, Dictionary<string, int>> Stock = new()
            { [A] = new() { ["Log"] = 10 }, [B] = new() { ["Log"] = 10 } };
        internal readonly List<Guid> Calls = [];
        internal Action<Guid, TimberbornStoredGoodStack>? BeforeConsume;
        internal Action<Guid, TimberbornStoredGoodStack>? AfterConsume;
        public TimberbornOwnedInventorySnapshot Read(TimberbornOwnedStorageRegistration owner) => !Live.Contains(owner.EntityId) ?
            new(TimberbornOwnedInventoryStatus.NotLive, []) : new(TimberbornOwnedInventoryStatus.Available,
                Stock[owner.EntityId].Select(pair => new TimberbornStoredGoodStack(pair.Key, pair.Value)).ToArray());
        public TimberbornOwnedInventoryRemoval Consume(TimberbornOwnedStorageRegistration owner, TimberbornStoredGoodStack requested)
        {
            Calls.Add(owner.EntityId);
            if (!Live.Contains(owner.EntityId)) return new(TimberbornOwnedInventoryStatus.NotLive, 0);
            BeforeConsume?.Invoke(owner.EntityId, requested);
            int amount = Math.Min(requested.Amount, Stock[owner.EntityId].GetValueOrDefault(requested.ResourceId));
            Stock[owner.EntityId][requested.ResourceId] -= amount;
            events.Add($"consume:{requested.ResourceId}:{amount}");
            AfterConsume?.Invoke(owner.EntityId, requested);
            return new(TimberbornOwnedInventoryStatus.Available, amount);
        }
    }
    private sealed class HazardFake(List<string> events) : ITimberbornStoredGoodHazardConsequenceSink
    {
        internal Action? Callback;
        internal int LastCell;
        public TimberbornStoredGoodHazardConsequenceResult ApplyHazards(TimberbornStoredGoodBurnTarget target,
            TimberbornStoredGoodBurnConsequence consequence, IReadOnlyList<TimberbornStoredGoodHazardStack> stacks)
        {
            LastCell = consequence.CellIndex;
            events.Add($"hazard:{stacks.Sum(stack => stack.Amount)}");
            Callback?.Invoke();
            return new(stacks.Sum(stack => stack.Amount), 1, 0, 0, 0);
        }
    }
}
