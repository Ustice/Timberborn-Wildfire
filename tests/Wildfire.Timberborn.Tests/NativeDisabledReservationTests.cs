using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeDisabledReservationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DisabledNativeInventoryReleasesReservationsAndGuardsPostMutationFailure(bool stock, bool failAfterDecrement)
    {
        using var fixture = new NativeReservationFixture(stock ? "Water" : "FertileAsh");
        fixture.Reserve(capacity: !stock);
        fixture.Disable();
        if (failAfterDecrement) fixture.OnInventoryChanged(() => throw new InvalidOperationException("release subscriber"));
        Action release = () => fixture.Call(fixture.Inventory, stock ? "UnreserveStock" : "UnreserveCapacity", fixture.Amount);
        if (failAfterDecrement)
        {
            Assert.Throws<TargetInvocationException>(() => fixture.Transfer(release));
            Assert.Equal(true, fixture.Get(fixture.Resources, "IsIndeterminate"));
            Assert.Throws<TargetInvocationException>(() => fixture.Transfer(release));
        }
        else { fixture.Transfer(release); fixture.Call(fixture.Resources, "ThrowIfSaveUnsafe"); }
        Assert.Equal(0, fixture.Reserved(stock));
    }

    [Fact]
    public void AshClearsRawStaleReferenceWithoutReenteringGuard()
    {
        using var fixture = new NativeReservationFixture("FertileAsh");
        fixture.Reserve(capacity: true);
        fixture.Disable();
        Assert.Equal(true, fixture.Get(fixture.Cargo, "HasAnyReservation"));
        fixture.Transfer(() => fixture.Call(fixture.Cargo, "ReleaseReservation"));
        Assert.Null(fixture.ReservedInventory(capacity: true));
        fixture.Call(fixture.Resources, "ThrowIfSaveUnsafe");
        // The fixture has no live GameObject: this exercises native stale-reference cleanup.
        // Disabled LIVE inventory decrement is covered directly above plus inspected Unreserve IL.
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AshRejectsDisabledForeignReservations(bool capacity)
    {
        using var fixture = new NativeReservationFixture("Water");
        fixture.Reserve(capacity);
        fixture.Disable();
        Assert.Equal(true, fixture.Get(fixture.Cargo, "HasAnyReservation"));
        Assert.Throws<TargetInvocationException>(() => fixture.Call(fixture.Cargo, "Validate", fixture.Cycle));
    }

    [Fact]
    public void NativeReservationCallbackCanDisableTargetWithoutThrowingBeforeOwnerIsRecorded()
    {
        using var fixture = new NativeReservationFixture("FertileAsh");
        int calls = 0;
        fixture.OnInventoryChanged(() =>
        {
            calls++;
            Assert.Null(fixture.ReservedInventory(capacity: true));
            fixture.Disable();
        });
        fixture.Transfer(() => fixture.Call(fixture.Reserver, "ReserveCapacity", fixture.Inventory, fixture.Amount));
        Assert.Equal(1, calls);
        Assert.Equal(false, fixture.Get(fixture.Inventory, "Enabled"));
        Assert.Same(fixture.Inventory, fixture.ReservedInventory(capacity: true));
        Assert.Equal(1, fixture.Reserved(stock: false));
        fixture.Call(fixture.Resources, "ThrowIfSaveUnsafe");
        // Native callback reachability is proven here. Full Ash.TryReserve eligibility includes
        // Unity object liveness; its paired phase/cleanup branch still needs controller validation.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WardenClearsOwnedRawStaleStockButDoesNotRetryPoisonedWrites(bool poisoned)
    {
        using var fixture = new NativeReservationFixture("Water");
        fixture.Reserve(capacity: false);
        fixture.Disable();
        if (poisoned) Assert.Throws<TargetInvocationException>(() => fixture.Transfer(() => throw new InvalidOperationException("prior fault")));
        fixture.Call(fixture.Warden(ownedStation: true), "ReleaseReservation");
        Assert.Equal(poisoned, fixture.ReservedInventory(capacity: false) is not null);
        Assert.Equal(poisoned, fixture.Get(fixture.Resources, "IsIndeterminate"));
    }

    [Fact]
    public void WardenDoesNotReleaseForeignDisabledStock()
    {
        using var fixture = new NativeReservationFixture("Water");
        fixture.Reserve(capacity: false);
        fixture.Disable();
        fixture.Call(fixture.Warden(ownedStation: false), "ReleaseReservation");
        Assert.Equal(1, fixture.Reserved(stock: true));
        fixture.Call(fixture.Resources, "ThrowIfSaveUnsafe");
    }

    [Fact]
    public void NativeReserveCallbackFailurePoisonsBeforeReservationOwnerIsRecorded()
    {
        using var fixture = new NativeReservationFixture("Water");
        fixture.Call(fixture.Inventory, "GiveExisting", fixture.Amount);
        fixture.OnInventoryChanged(() => throw new InvalidOperationException("reserve subscriber"));
        bool phaseCommitted = false;
        Assert.Throws<TargetInvocationException>(() => fixture.Transfer(() =>
        {
            fixture.Call(fixture.Reserver, "ReserveExactStockAmount", fixture.Inventory, fixture.Amount);
            phaseCommitted = true;
        }));
        Assert.False(phaseCommitted);
        Assert.Equal(1, fixture.Reserved(stock: true));
        Assert.Null(fixture.ReservedInventory(capacity: false));
        Assert.Equal(true, fixture.Get(fixture.Resources, "IsIndeterminate"));
        Assert.Throws<TargetInvocationException>(() => fixture.Call(fixture.Resources, "ThrowIfSaveUnsafe"));
    }
}

/// <summary>Installed native inventory/reserver methods, no Unity entity, navigation or live world.</summary>
internal sealed class NativeReservationFixture : IDisposable
{
    private readonly NativeManagedTestContext _native = new();
    private readonly Assembly _mod;
    private readonly Type _baseType;
    internal object Inventory { get; }
    internal object Reserver { get; }
    internal object Resources { get; }
    internal object Cargo { get; }
    internal object Cycle { get; }
    internal object Amount { get; }
    internal NativeReservationFixture(string goodId)
    {
        _mod = _native.LoadMod();
        var inventories = _native.LoadNative("Timberborn.InventorySystem");
        var goods = _native.LoadNative("Timberborn.Goods");
        var carrying = _native.LoadNative("Timberborn.Carrying");
        var components = _native.LoadNative("Timberborn.BaseComponentSystem");
        _baseType = components.GetType("Timberborn.BaseComponentSystem.BaseComponent")!;
        Inventory = Make(inventories.GetType("Timberborn.InventorySystem.Inventory")!);
        Reserver = Make(inventories.GetType("Timberborn.InventorySystem.GoodReserver")!);
        foreach (var field in new[] { "_storage", "_reservedStock", "_reservedCapacity" })
            Set(Inventory, field, Activator.CreateInstance(goods.GetType("Timberborn.Goods.GoodRegistry")!));
        Set(Inventory, "<Capacity>k__BackingField", 1);
        Set(Inventory, "_goodDisallower", Activator.CreateInstance(inventories.GetType("Timberborn.InventorySystem.NullGoodDisallower")!));
        var allowed = Activator.CreateInstance(goods.GetType("Timberborn.Goods.StorableGoodRegistry")!)!;
        var storable = goods.GetType("Timberborn.Goods.StorableGood")!.GetMethod("CreateAsTakeable")!.Invoke(null, new[] { goodId });
        var storableAmount = goods.GetType("Timberborn.Goods.StorableGoodAmount")!;
        var allowedArray = Array.CreateInstance(storableAmount, 1);
        allowedArray.SetValue(Activator.CreateInstance(storableAmount, storable, 1), 0);
        Call(allowed, "Add", allowedArray);
        Set(Inventory, "_allowedGoods", allowed);
        _baseType.GetField("<Enabled>k__BackingField", Flags)!.SetValue(Inventory, true);
        // A null cached GameObject models the native stale/deleted-reference branch. We do not
        // fake Unity liveness or instantiate an engine object. Live decrement is tested on Inventory.
        var cache = Make(components.GetType("Timberborn.BaseComponentSystem.ComponentCache")!);
        _baseType.GetField("_componentCache", Flags)!.SetValue(Inventory, cache);
        Amount = Activator.CreateInstance(goods.GetType("Timberborn.Goods.GoodAmount")!, goodId, 1)!;
        Resources = Activator.CreateInstance(_mod.GetType("Wildfire.Timberborn.Resources.NativeResourceCoordinator")!)!;
        Cycle = Activator.CreateInstance(_mod.GetType("Wildfire.Timberborn.Ash.AshHarvestCycle")!)!;
        Cargo = Activator.CreateInstance(_mod.GetType("Wildfire.Timberborn.Ash.AshHarvestCargo")!, Flags, null,
            new[] { Make(carrying.GetType("Timberborn.Carrying.GoodCarrier")!), Reserver, Resources }, null)!;
    }
    internal void Reserve(bool capacity)
    {
        if (!capacity) Call(Inventory, "GiveExisting", Amount);
        Call(Reserver, capacity ? "ReserveCapacity" : "ReserveExactStockAmount", Inventory, Amount);
    }
    internal object? Reserved(bool stock) => Call(Inventory.GetType().GetField(stock ? "_reservedStock" : "_reservedCapacity", Flags)!.GetValue(Inventory)!, "Amount", Get(Amount, "GoodId"));
    internal object? ReservedInventory(bool capacity) => Get(Get(Reserver, capacity ? "CapacityReservation" : "StockReservation")!, "Inventory");
    internal void Disable() => _baseType.GetField("<Enabled>k__BackingField", Flags)!.SetValue(Inventory, false);
    internal void Transfer(Action action) => Call(Resources, "TransferInventory", action);
    internal object Warden(bool ownedStation)
    {
        var station = Make(_mod.GetType("Wildfire.Timberborn.FireResponse.WardenStation")!);
        Call(station, "InitializeInventory", ownedStation ? Inventory : Make(Inventory.GetType()));
        var warden = Make(_mod.GetType("Wildfire.Timberborn.FireResponse.WardenExecutor")!);
        Set(warden, "_station", station); Set(warden, "_reserver", Reserver); Set(warden, "_delivery", Resources);
        return warden;
    }
    internal void OnInventoryChanged(Action action)
    {
        var eventInfo = Inventory.GetType().GetEvent("InventoryChanged")!;
        var parameters = eventInfo.EventHandlerType!.GetMethod("Invoke")!.GetParameters().Select(p => Expression.Parameter(p.ParameterType));
        var handler = Expression.Lambda(eventInfo.EventHandlerType,
            Expression.Call(Expression.Constant(action), typeof(Action).GetMethod("Invoke")!), parameters).Compile();
        eventInfo.AddEventHandler(Inventory, handler);
    }
    internal object? Call(object owner, string name, params object?[] args) => owner.GetType().GetMethods(Flags)
        .Single(m => m.Name == name && m.GetParameters().Length == args.Length && m.GetParameters()
            .Select((p, i) => args[i] is null || p.ParameterType.IsInstanceOfType(args[i])).All(value => value)).Invoke(owner, args);
    internal object? Get(object owner, string name) => owner.GetType().GetProperty(name, Flags)!.GetValue(owner);
    private static object Make(Type type) => RuntimeHelpers.GetUninitializedObject(type);
    private static void Set(object owner, string name, object? value) => owner.GetType().GetField(name, Flags)!.SetValue(owner, value);
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public void Dispose() => _native.Dispose();
}
