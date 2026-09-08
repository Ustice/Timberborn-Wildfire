using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

// Actual managed Inventory/GoodReserver operations; no positive Unity object truth is supplied.
internal sealed class NativeWardenReturnFixture : IDisposable
{
    private readonly NativeManagedTestContext _native = new();
    internal const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    internal object Resources { get; }
    internal object Equipment { get; }
    internal object Source { get; }
    internal object Destination { get; }
    internal object Reserver { get; }
    internal object Balance { get; }
    private readonly object _goods, _serializer;
    internal NativeWardenReturnFixture(string name = "Wildfire.WardenEquipment", int capacity = 1, bool publicInput = false)
    {
        Resources = Activator.CreateInstance(Mod("Resources.NativeResourceCoordinator"))!;
        Equipment = Activator.CreateInstance(Mod("FireResponse.WardenEquipment"), Resources)!;
        _goods = RuntimeHelpers.GetUninitializedObject(T("Timberborn.Goods.GoodService"));
        var specs = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), T("Timberborn.Goods.GoodSpec")))!;
        foreach (var id in new[] { "Water", "FertileAsh" })
        {
            var spec = New("Timberborn.Goods.GoodSpec");
            spec.GetType().GetProperty("Id")!.SetValue(spec, id); specs.Add(id, spec);
        }
        Set(_goods, "_goodSpecsById", specs);
        _serializer = New("Timberborn.Goods.GoodRegistryValueSerializer", New("Timberborn.Goods.GoodAmountSerializer", New("Timberborn.Goods.SerializedGoodValueSerializer", _goods)));
        Source = Inventory(capacity, name, publicInput); Destination = Inventory(10, "Fixture.Destination", false);
        Call(Equipment, "InitializeInventory", Source);
        Reserver = RuntimeHelpers.GetUninitializedObject(T("Timberborn.InventorySystem.GoodReserver"));
        Balance = New("Timberborn.ResourceCountingSystem.DistrictGoodsBalance", _serializer);
        var district = New("Timberborn.InventorySystem.DistrictInventoryRegistry", _goods);
        var evt = district.GetType().GetEvent("InventoryRegistered")!;
        evt.AddEventHandler(district, Delegate.CreateDelegate(evt.EventHandlerType!, Balance, Balance.GetType().GetMethod("OnInventoryRegistered", Flags)!));
        // Public-input case is declaration-only refusal, not a supplied public district/validator graph.
        if (!publicInput) Call(district, "Add", Source);
        Call(district, "Add", Destination);
    }
    private object Inventory(int capacity, string name, bool publicInput)
    {
        var inventory = RuntimeHelpers.GetUninitializedObject(T("Timberborn.InventorySystem.Inventory"));
        foreach (var field in new[] { "_storage", "_reservedStock", "_reservedCapacity" }) Set(inventory, field, New("Timberborn.Goods.GoodRegistry"));
        Set(inventory, "_allowedGoods", New("Timberborn.Goods.StorableGoodRegistry")); Set(inventory, "_goodRegistryValueSerializer", _serializer);
        var inventories = New("Timberborn.InventorySystem.Inventories");
        var list = new List<object> { inventory, inventories };
        var cache = RuntimeHelpers.GetUninitializedObject(T("Timberborn.BaseComponentSystem.ComponentCache"));
        var map = New("Timberborn.BaseComponentSystem.TypeIndexMap");
        var readOnly = Activator.CreateInstance(T("Timberborn.Common.ReadOnlyList`1").MakeGenericType(typeof(object)), Flags, null, [list], null)!;
        foreach (var component in list) map.GetType().GetMethod("CacheType")!.MakeGenericMethod(component.GetType()).Invoke(map, [readOnly]);
        Set(cache, "_components", list); Set(cache, "_typeIndexMap", map);
        foreach (var component in list) T("Timberborn.BaseComponentSystem.BaseComponent").GetField("_componentCache", Flags)!.SetValue(component, cache);
        var initializer = Call(New("Timberborn.InventorySystem.InventoryInitializerFactory", _goods), "Create", inventory, capacity, name)!;
        if (publicInput) Call(initializer, "HasPublicInput");
        var good = T("Timberborn.Goods.StorableGood").GetMethod("CreateAsGivable")!.Invoke(null, ["Water"]);
        Call(initializer, "AddAllowedGood", New("Timberborn.Goods.StorableGoodAmount", good, capacity));
        Call(initializer, "Initialize"); Call(inventory, "Enable"); return inventory;
    }
    internal Type Mod(string name) => _native.LoadMod().GetType("Wildfire.Timberborn." + name)!;
    internal Type T(string name) => _native.LoadNative(name[..name.LastIndexOf('.')]).GetType(name)!;
    internal object New(string name, params object?[] args) => Activator.CreateInstance(T(name), Flags, null, args, null)!;
    internal object Amount(string good = "Water", int amount = 1) => New("Timberborn.Goods.GoodAmount", good, amount);
    internal static void Set(object owner, string name, object? value) => owner.GetType().GetField(name, Flags)!.SetValue(owner, value);
    internal object? Call(object owner, string name, params object?[] args) => owner.GetType().GetMethods(Flags)
        .Single(m => m.Name == name && !m.IsGenericMethod && m.GetParameters().Length == args.Length && m.GetParameters()
            .Select((p, i) => args[i] is null || p.ParameterType.IsInstanceOfType(args[i])).All(ok => ok)).Invoke(owner, args);
    internal object? Get(object owner, string name) => owner.GetType().GetProperty(name, Flags)!.GetValue(owner);
    internal void Give(object inventory, int amount = 1) => Call(inventory, "GiveExistingIgnoringCapacity", Amount(amount: amount));
    internal int Quantity(object inventory) => (int)Call(inventory, "AmountInStock", "Water")!;
    internal bool Poisoned => (bool)Get(Resources, "IsIndeterminate")!;
    internal void Transfer(Action action) => Call(Resources, "TransferInventory", action);
    internal void On(object inventory, string name, Action action)
    {
        var evt = inventory.GetType().GetEvent(name)!;
        var parameters = evt.EventHandlerType!.GetMethod("Invoke")!.GetParameters().Select(p => Expression.Parameter(p.ParameterType));
        evt.AddEventHandler(inventory, Expression.Lambda(evt.EventHandlerType, Expression.Invoke(Expression.Constant(action)), parameters).Compile());
    }
    internal bool Move(Action? afterDebit = null, Action? commit = null) => (bool)Mod("FireResponse.WardenEquipmentReturnStock")
        .GetMethod("TryMove", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [Source, Destination, afterDebit ?? (() => { }), commit ?? (() => { })])!;
    internal bool Exact() => (bool)Mod("FireResponse.WardenEquipmentReturnStock")
        .GetMethod("ExactCapacity", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [Destination, Reserver])!;
    internal void Reservation(object inventory, string good = "Water", int amount = 1, bool fixedAmount = true, bool consume = false, string kind = "Capacity")
        => Set(Reserver, "<" + kind + "Reservation>k__BackingField", New("Timberborn.InventorySystem.GoodReservation", inventory, Amount(good, amount), fixedAmount, consume));
    internal object WatchRelease() => Activator.CreateInstance(Mod("FireResponse.WardenCapacityRelease"), Flags, null, [Destination, Reserver], null)!;
    internal void ClearReservation() => Set(Reserver, "<CapacityReservation>k__BackingField", Activator.CreateInstance(T("Timberborn.InventorySystem.GoodReservation")));
    public void Dispose() => _native.Dispose();
}
