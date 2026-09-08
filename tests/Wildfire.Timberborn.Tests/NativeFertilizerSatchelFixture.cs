using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

internal sealed class NativeFertilizerSatchelFixture : IDisposable
{
    private readonly NativeManagedTestContext _native = new();
    internal object Resources { get; }
    internal object Satchel { get; }
    internal object Inventory { get; }
    internal object Source { get; }
    internal object Reserver { get; }
    internal object Balance { get; }
    internal object District { get; }
    private readonly object _goods;
    private readonly object _serializer;
    private readonly Type _stock;
    internal const string Good = "FertileAsh";

    internal NativeFertilizerSatchelFixture()
    {
        _stock = Mod("FertilizerSatchelStock");
        Resources = Activator.CreateInstance(_native.LoadMod().GetType("Wildfire.Timberborn.Resources.NativeResourceCoordinator")!)!;
        Satchel = Activator.CreateInstance(Mod("FertilizerSatchel"), Resources)!;
        var goodType = T("Timberborn.Goods", "GoodSpec");
        _goods = RuntimeHelpers.GetUninitializedObject(T("Timberborn.Goods", "GoodService"));
        var map = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), goodType))!;
        foreach (string id in new[] { Good, "Water" })
        {
            var good = Activator.CreateInstance(goodType)!;
            goodType.GetProperty("Id")!.SetValue(good, id);
            map.Add(id, good);
        }
        Set(_goods, "_goodSpecsById", map);
        var goodSerializer = Activator.CreateInstance(T("Timberborn.Goods", "SerializedGoodValueSerializer"), _goods)!;
        var amountSerializer = Activator.CreateInstance(T("Timberborn.Goods", "GoodAmountSerializer"), goodSerializer)!;
        _serializer = Activator.CreateInstance(T("Timberborn.Goods", "GoodRegistryValueSerializer"), amountSerializer)!;
        Inventory = NewInventory();
        var factory = Activator.CreateInstance(T("Timberborn.InventorySystem", "InventoryInitializerFactory"), _goods)!;
        var initializer = Activator.CreateInstance(Mod("FertilizerSatchelInventoryInitializer"), factory)!;
        Call(initializer, "Initialize", Satchel, Inventory);
        Call(Inventory, "Enable");
        Source = NewInventory();
        var sourceInitializer = Call(factory, "Create", Source, 10, "Fixture.Source")!;
        var storable = T("Timberborn.Goods", "StorableGood").GetMethod("CreateAsTakeable")!.Invoke(null, [Good]);
        Call(sourceInitializer, "AddAllowedGood", Activator.CreateInstance(T("Timberborn.Goods", "StorableGoodAmount"), storable, 10));
        Call(sourceInitializer, "Initialize");
        Call(Source, "Enable");
        Reserver = RuntimeHelpers.GetUninitializedObject(T("Timberborn.InventorySystem", "GoodReserver"));
        Balance = Activator.CreateInstance(T("Timberborn.ResourceCountingSystem", "DistrictGoodsBalance"), _serializer)!;
        District = Activator.CreateInstance(T("Timberborn.InventorySystem", "DistrictInventoryRegistry"), _goods)!;
        foreach (string suffix in new[] { "Registered", "Unregistered" })
        {
            var eventInfo = District.GetType().GetEvent("Inventory" + suffix)!;
            var handler = Delegate.CreateDelegate(eventInfo.EventHandlerType!, Balance,
                Balance.GetType().GetMethod("OnInventory" + suffix, Flags)!);
            eventInfo.AddEventHandler(District, handler);
        }
        Call(District, "Add", Inventory); // Actual private-inventory registration invokes native balance subscriber.
    }

    internal Type Mod(string name) => _native.LoadMod().GetType("Wildfire.Timberborn.Fertilizer." + name)!;
    internal Type T(string assembly, string name) => _native.LoadNative(assembly).GetType(assembly + "." + name)!;
    internal object Amount(string id = Good, int amount = 1) => Activator.CreateInstance(T("Timberborn.Goods", "GoodAmount"), id, amount)!;
    internal void Give(object inventory, int amount = 1) => Call(inventory, "GiveExistingIgnoringCapacity", Amount(amount: amount));
    internal int Quantity(object inventory) => (int)Call(inventory, "AmountInStock", Good)!;
    internal int Consumption => (int)Call(Balance, "GetConsumption", Good)!;
    internal int Production => (int)Call(Balance, "GetProduction", Good)!;
    internal bool Poisoned => (bool)Get(Resources, "IsIndeterminate")!;
    internal bool Move(object source, object destination) => (bool)_stock.GetMethod("TryMoveUnit", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [source, destination])!;
    internal void Consume() => _stock.GetMethod("ConsumeUnit", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [Inventory]);
    internal void Transfer(Action action) => Call(Resources, "TransferInventory", action);
    internal bool Reservation(string kind, object inventory) => (bool)Mod("FertilizerSatchel")
        .GetMethod("Exact" + kind + "Reservation", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [inventory, Reserver])!;
    internal void On(object inventory, string name, Action callback)
    {
        var info = inventory.GetType().GetEvent(name)!;
        var parameters = info.EventHandlerType!.GetMethod("Invoke")!.GetParameters().Select(p => Expression.Parameter(p.ParameterType));
        info.AddEventHandler(inventory, Expression.Lambda(info.EventHandlerType,
            Expression.Invoke(Expression.Constant(callback)), parameters).Compile());
    }
    internal object SaveLoadInventory()
    {
        var serialized = Activator.CreateInstance(T("Timberborn.WorldSerialization", "SerializedEntity"), Guid.NewGuid(), "Fixture.Adult")!;
        Call(Inventory, "Save", Activator.CreateInstance(T("Timberborn.WorldPersistence", "EntitySaver"), serialized));
        var restored = NewInventory();
        Set(restored, "<ComponentName>k__BackingField", Get(Inventory, "ComponentName"));
        Call(restored, "Load", Activator.CreateInstance(T("Timberborn.WorldPersistence", "EntityLoader"), serialized));
        return restored;
    }
    private object NewInventory()
    {
        var inventory = RuntimeHelpers.GetUninitializedObject(T("Timberborn.InventorySystem", "Inventory"));
        foreach (string field in new[] { "_storage", "_reservedStock", "_reservedCapacity" })
            Set(inventory, field, Activator.CreateInstance(T("Timberborn.Goods", "GoodRegistry")));
        Set(inventory, "_allowedGoods", Activator.CreateInstance(T("Timberborn.Goods", "StorableGoodRegistry")));
        Set(inventory, "_goodRegistryValueSerializer", _serializer);
        return inventory;
    }
    internal object? Call(object owner, string name, params object?[] args) => owner.GetType().GetMethods(Flags)
        .Single(m => m.Name == name && m.GetParameters().Length == args.Length && m.GetParameters()
            .Select((p, i) => args[i] is null || p.ParameterType.IsInstanceOfType(args[i])).All(value => value)).Invoke(owner, args);
    internal object? Get(object owner, string name) => owner.GetType().GetProperty(name, Flags)!.GetValue(owner);
    internal static void Set(object owner, string name, object? value) => owner.GetType().GetField(name, Flags)!.SetValue(owner, value);
    internal const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public void Dispose() => _native.Dispose();
}
