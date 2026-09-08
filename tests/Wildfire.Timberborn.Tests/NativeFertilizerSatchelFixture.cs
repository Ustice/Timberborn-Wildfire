using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Wildfire.Core;

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
    private object? _registeredCounter;
    internal object Character { get; private set; } = null!;
    internal object Citizen { get; private set; } = null!;
    internal readonly List<string> Warnings = new();
    internal Action<string>? OnWarning;
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
    internal void SetReservation(string kind, object inventory, string good = Good, int amount = 1,
        bool fixedAmount = true, bool consume = false)
    {
        var reservation = Activator.CreateInstance(T("Timberborn.InventorySystem", "GoodReservation"),
            inventory, Amount(good, amount), fixedAmount, consume);
        Set(Reserver, "<" + kind + "Reservation>k__BackingField", reservation);
    }
    internal object ModelActorAwake()
    {
        if (Character is not null) return Character;
        Character = Activator.CreateInstance(T("Timberborn.Characters", "Character"),
            Activator.CreateInstance(T("Timberborn.SingletonSystem", "EventBus")), null, null)!;
        var mortal = RuntimeHelpers.GetUninitializedObject(T("Timberborn.MortalSystem", "Mortal"));
        Set(mortal, "_character", Character);
        Citizen = RuntimeHelpers.GetUninitializedObject(T("Timberborn.GameDistricts", "Citizen"));
        Set(Citizen, "_unassignedCitizenRegistry", Activator.CreateInstance(T("Timberborn.GameDistricts", "UnassignedCitizenRegistry")));
        AttachCache(Satchel, Inventory, Character, mortal, Citizen);
        Call(Citizen, "Awake"); // Actual native subscriber order: district unassignment precedes satchel OnDied.
        Call(Satchel, "Awake");
        // Only the later diagnostic correction has a sink. No test calls Unity logging internals.
        Satchel.GetType().GetField("_warn", Flags)?.SetValue(Satchel, (Action<string>)(message =>
        {
            Warnings.Add(message);
            OnWarning?.Invoke(message);
        }));
        return Character;
    }
    internal void DeleteThroughNativeEntity(Action deletedEvent)
    {
        ModelActorAwake();
        var type = T("Timberborn.EntitySystem", "EntityComponent");
        var entity = RuntimeHelpers.GetUninitializedObject(type);
        var state = type.GetField("_entityState", Flags)!;
        state.SetValue(entity, Enum.Parse(state.FieldType, "Initialized"));
        Set(entity, "<RegisteredComponents>k__BackingField",
            Activator.CreateInstance(typeof(List<>).MakeGenericType(T("Timberborn.EntitySystem", "IRegisteredComponent"))));
        Set(entity, "_entityComponentRegistry", RuntimeHelpers.GetUninitializedObject(T("Timberborn.EntitySystem", "EntityComponentRegistry")));
        var bus = Activator.CreateInstance(T("Timberborn.SingletonSystem", "EventBus"))!;
        Set(bus, "_ready", true);
        var subscriptions = bus.GetType().GetField("_subscriptions", Flags)!.GetValue(bus)!;
        Call(subscriptions, "Add", T("Timberborn.EntitySystem", "EntityDeletedEvent"), this,
            (Action<object>)(_ => deletedEvent()));
        Set(entity, "_eventBus", bus);
        AttachCache(entity, Satchel, Inventory, Character, Citizen);
        Call(entity, "Delete");
        Assert.True((bool)Get(entity, "Deleted")!);
    }
    internal void ModelDistrictRegistration()
    {
        ModelActorAwake();
        var counter = Activator.CreateInstance(T("Timberborn.ResourceCountingSystem", "DistrictResourceCounter"))!;
        _registeredCounter = counter;
        Call(counter, "Add", Satchel.GetType().GetField("_satchelCounter", Flags)!.GetValue(Satchel));
        Set(Satchel, "_counter", counter);
        Set(Satchel, "_registry", District);
    }
    internal int RegisteredProcessors
    {
        get
        {
            var counter = _registeredCounter;
            if (counter is null) return 0;
            var processed = counter.GetType().GetField("_processedGoodCounter", Flags)!.GetValue(counter)!;
            return ((IList)processed.GetType().GetField("_goodProcessors", Flags)!.GetValue(processed)!).Count;
        }
    }
    internal void Capture(Action action) => Resources.GetType().GetMethod("CaptureAtRest")!.MakeGenericMethod(typeof(int))
        .Invoke(Resources, [(Func<int>)(() => { action(); return 1; })]);
    internal void Give(object inventory, int amount = 1) => Call(inventory, "GiveExistingIgnoringCapacity", Amount(amount: amount));
    internal int Quantity(object inventory) => (int)Call(inventory, "AmountInStock", Good)!;
    internal int Consumption => (int)Call(Balance, "GetConsumption", Good)!;
    internal int Production => (int)Call(Balance, "GetProduction", Good)!;
    internal bool Poisoned => (bool)Get(Resources, "IsIndeterminate")!;
    internal bool Move(object source, object destination) => (bool)_stock.GetMethod("TryMoveUnit", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [source, destination])!;
    internal void Consume(Action? commitPhase = null) => _stock.GetMethod("ConsumeCommittedUnit", BindingFlags.NonPublic | BindingFlags.Static)!
        .Invoke(null, [Inventory, Resources, commitPhase ?? (() => { })]);
    internal void Apply(Action? commitPhase = null, bool reject = false)
    {
        Call(Resources, "Attach", new ApplicationSimulator(reject));
        Call(Resources, "TryApplyCleanAsh", new FireSimAshApplicationInput(0, 2),
            (Action<FireSimAshApplicationReceipt>)(_ => Consume(commitPhase)));
    }
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
        var restoredSatchel = Activator.CreateInstance(Mod("FertilizerSatchel"), Resources)!;
        var factory = Activator.CreateInstance(T("Timberborn.InventorySystem", "InventoryInitializerFactory"), _goods)!;
        var initializer = Activator.CreateInstance(Mod("FertilizerSatchelInventoryInitializer"), factory)!;
        Call(initializer, "Initialize", restoredSatchel, restored);
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
        AttachCache(inventory, Activator.CreateInstance(T("Timberborn.InventorySystem", "Inventories"))!);
        return inventory;
    }
    private void AttachCache(params object[] components)
    {
        var list = components.ToList();
        var cache = RuntimeHelpers.GetUninitializedObject(T("Timberborn.BaseComponentSystem", "ComponentCache"));
        var mapType = T("Timberborn.BaseComponentSystem", "TypeIndexMap");
        var map = Activator.CreateInstance(mapType)!;
        var readOnlyType = T("Timberborn.Common", "ReadOnlyList`1").MakeGenericType(typeof(object));
        var readOnly = Activator.CreateInstance(readOnlyType, Flags, null, [list], null)!;
        foreach (var type in components.Select(component => component.GetType()).Append(T("Timberborn.EntitySystem", "IDeletableEntity")).Distinct())
            mapType.GetMethod("CacheType")!.MakeGenericMethod(type).Invoke(map, [readOnly]);
        Set(cache, "_components", list);
        Set(cache, "_typeIndexMap", map);
        foreach (object component in components)
            T("Timberborn.BaseComponentSystem", "BaseComponent").GetField("_componentCache", Flags)!.SetValue(component, cache);
    }
    internal object? Call(object owner, string name, params object?[] args) => owner.GetType().GetMethods(Flags)
        .Single(m => m.Name == name && m.GetParameters().Length == args.Length && m.GetParameters()
            .Select((p, i) => args[i] is null || p.ParameterType.IsInstanceOfType(args[i])).All(value => value)).Invoke(owner, args);
    internal object? Get(object owner, string name) => owner.GetType().GetProperty(name, Flags)!.GetValue(owner);
    internal static void Set(object owner, string name, object? value) => owner.GetType().GetField(name, Flags)!.SetValue(owner, value);
    private sealed class ApplicationSimulator(bool reject) : IFireSimAshApplicationSimulator, IFireSimAshCollectionSimulator
    {
        public int Width => 1;
        public int Height => 1;
        public int Depth => 1;
        public FireSimAshApplicationStepResult? TryApplyCleanAsh(FireSimAshApplicationInput input,
            Action<FireSimAshApplicationReceipt> commit)
        {
            var receipt = new FireSimAshApplicationReceipt(input.CellIndex, input.Limit,
                (byte)(reject ? 0 : 1), reject ? FireSimAshApplicationOutcome.Full : FireSimAshApplicationOutcome.Applied);
            if (!reject)
                try { commit(receipt); }
                catch (Exception exception) { throw new FireSimStepInputException(FireSimStepInputOutcome.Indeterminate, exception); }
            return new(new([], 1), receipt);
        }
        public GpuFireStepResult? TryCollectAsh(FireSimAshCollectionInput input, Action<FireSimAshCollectionReceipt> commit) => throw new NotSupportedException();
        public GpuFireStepResult? TryTickWithInput(FireSimChange input, Action commit) => throw new NotSupportedException();
        public GpuFireStepResult Tick() => throw new NotSupportedException();
        public IDisposable Subscribe(IFireSimListener listener) => throw new NotSupportedException();
        public void RegisterChange(FireSimChange change) => throw new NotSupportedException();
    }
    internal const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public void Dispose() => _native.Dispose();
}
