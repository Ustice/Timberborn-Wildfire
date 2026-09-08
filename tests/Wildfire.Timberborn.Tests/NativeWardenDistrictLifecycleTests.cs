using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeWardenDistrictLifecycleTests
{
    [Fact]
    public void ActualCharacterConstructorIsAliveBeforeEquipmentInitializationAndPostLoad()
    {
        using var f = new Fixture();
        // Character.Awake/PreInitialize have not run; native construction already establishes Alive.
        Assert.True((bool)f.Get(f.Character, "Alive")!);
        f.Call(f.Equipment, "InitializeEntity");
        f.Call(f.Inventory, "Disable");
        int enabled = 0;
        f.On(f.Inventory, "InventoryEnabled", () => enabled++);
        f.Call(f.Equipment, "PostLoadEntity");
        Assert.Equal(1, enabled); // A false initial death inference would have permanently exited instead.
        Assert.True((bool)f.Get(f.Inventory, "Enabled")!);
        Assert.Equal(1, f.Quantity);
        Assert.False(f.Poisoned);
    }

    [Fact]
    public void NativeUnregisterFailureDoesNotAbortDeletionOrAllowSave()
    {
        using var f = new Fixture();
        int callbacks = 0;
        f.On(f.Registry, "InventoryUnregistered", () =>
        {
            callbacks++;
            Assert.Equal(0, f.RegisteredProcessors); // Real native counter was already changed.
            throw new IOException("district observer failed");
        });
        bool nativeDeletedEvent = false;
        f.DeleteThroughNativeEntity(() => nativeDeletedEvent = true);
        Assert.True(nativeDeletedEvent);
        Assert.Equal(1, callbacks);
        Assert.True(f.Poisoned);
        Assert.Throws<TargetInvocationException>(() => f.Call(f.Resources, "ThrowIfSaveUnsafe"));
        f.Call(f.Equipment, "DeleteEntity"); // Never replay uncertain removal.
        Assert.Equal(1, callbacks);
        Assert.Equal(1, f.Quantity);
    }

    [Fact]
    public void DeletionDuringCaptureCannotMutateNativeCountersOrReportSuccessfulCapture()
    {
        using var f = new Fixture();
        int callbacks = 0;
        bool continued = false;
        f.On(f.Registry, "InventoryUnregistered", () => callbacks++);
        Assert.Throws<TargetInvocationException>(() => f.Capture(() =>
        {
            f.DeleteThroughNativeEntity(() => continued = true);
        }));
        Assert.True(continued);
        Assert.True(f.Poisoned);
        Assert.Equal(0, callbacks);
        Assert.Equal(1, f.RegisteredProcessors);
        f.Call(f.Equipment, "DeleteEntity");
        Assert.Equal(0, callbacks);
        Assert.Equal(1, f.Quantity);
    }

    [Fact]
    public void NativeDeathDistrictEventBeforeEquipmentDeathStillAllowsLaterNativeSubscribers()
    {
        using var f = new Fixture();
        int callbacks = 0;
        bool laterDeathSubscriber = false;
        f.On(f.Registry, "InventoryUnregistered", () =>
        {
            callbacks++;
            throw new IOException("district observer failed during Citizen.OnDied");
        });
        f.On(f.Character, "Died", () => laterDeathSubscriber = true);
        f.Call(f.Character, "KillCharacter");
        Assert.True(laterDeathSubscriber);
        Assert.False((bool)f.Get(f.Character, "Alive")!);
        Assert.True(f.Poisoned);
        Assert.Equal(1, callbacks);
        Assert.Equal(0, f.RegisteredProcessors);
        f.Call(f.Equipment, "DeleteEntity");
        Assert.Equal(1, callbacks);
        Assert.Equal(1, f.Quantity);
    }

    [Fact]
    public void SuccessfulUnassignmentUsesOneGuardAndDoesNotSpendOrReplayWater()
    {
        using var f = new Fixture();
        int callbacks = 0;
        f.On(f.Registry, "InventoryUnregistered", () =>
        {
            callbacks++;
            Assert.Throws<TargetInvocationException>(() => f.Call(f.Resources, "ThrowIfSaveUnsafe"));
        });
        f.Call(f.Citizen, "UnassignDistrict");
        Assert.Equal(0, f.RegisteredProcessors);
        f.Call(f.Citizen, "UnassignDistrict");
        f.Call(f.Equipment, "DeleteEntity");
        Assert.Equal(1, callbacks);
        Assert.Equal(1, f.Quantity);
        Assert.False(f.Poisoned);
        f.Call(f.Resources, "ThrowIfSaveUnsafe");
    }

    [Fact]
    public void LivingDistrictFailurePreservesOriginalCauseAndPoisonsBeforeLaterTeardown()
    {
        using var f = new Fixture();
        var failure = new IOException("native district callback");
        int callbacks = 0;
        f.On(f.Registry, "InventoryUnregistered", () => { callbacks++; throw failure; });
        var error = Assert.Throws<TargetInvocationException>(() => f.Call(f.Citizen, "UnassignDistrict"));
        Assert.Same(failure, error.InnerException);
        Assert.True(f.Poisoned);
        f.Call(f.Character, "KillCharacter");
        f.Call(f.Equipment, "DeleteEntity");
        Assert.Equal(1, callbacks);
        Assert.Equal(1, f.Quantity);
    }

    [Fact]
    public void CaughtBusyDistrictChangeInvalidatesCaptureWithoutUnguardedRemoval()
    {
        using var f = new Fixture();
        int callbacks = 0;
        f.On(f.Registry, "InventoryUnregistered", () => callbacks++);
        Assert.Throws<TargetInvocationException>(() => f.Capture(() =>
        {
            Assert.Throws<TargetInvocationException>(() => f.Call(f.Citizen, "UnassignDistrict"));
        }));
        Assert.True(f.Poisoned);
        Assert.Equal(0, callbacks);
        Assert.Equal(1, f.RegisteredProcessors);
        Assert.Equal(1, f.Quantity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BusyOrPreviouslyPoisonedDeathContinuesWithoutRetryingNativeCleanup(bool alreadyPoisoned)
    {
        using var f = new Fixture();
        int callbacks = 0;
        bool laterDeathSubscriber = false;
        f.On(f.Registry, "InventoryUnregistered", () => callbacks++);
        f.On(f.Character, "Died", () => laterDeathSubscriber = true);
        if (alreadyPoisoned)
        {
            Assert.Throws<TargetInvocationException>(() => f.Transfer(() => throw new IOException("previous mutation")));
            f.Call(f.Character, "KillCharacter");
        }
        else
        {
            Assert.Throws<TargetInvocationException>(() => f.Transfer(() => f.Call(f.Character, "KillCharacter")));
        }
        Assert.True(laterDeathSubscriber);
        Assert.True(f.Poisoned);
        f.Call(f.Equipment, "DeleteEntity");
        Assert.Equal(0, callbacks);
        Assert.Equal(1, f.RegisteredProcessors);
        Assert.Equal(1, f.Quantity);
    }

    [Theory]
    [InlineData("InitializeEntity")]
    [InlineData("PostLoadEntity")]
    public void NativeEnableCallbacksBelongToTheRegistrationGuard(string lifecycle)
    {
        using var f = new Fixture();
        f.Call(f.Inventory, "Disable");
        var failure = new IOException("enabled observer failed after flag changed");
        f.On(f.Inventory, "InventoryEnabled", () =>
        {
            Assert.Throws<TargetInvocationException>(() => f.Call(f.Resources, "ThrowIfSaveUnsafe"));
            throw failure;
        });
        var error = Assert.Throws<TargetInvocationException>(() => f.Call(f.Equipment, lifecycle));
        Assert.Same(failure, error.InnerException);
        Assert.True((bool)f.Get(f.Inventory, "Enabled")!);
        Assert.True(f.Poisoned);
        f.Call(f.Equipment, "DeleteEntity");
        Assert.Equal(1, f.RegisteredProcessors);
        Assert.Equal(1, f.Quantity);
    }

    [Fact]
    public void DeathInsideNativeEnableDoesNotContinueDistrictWritesOrReportSuccessfulLoad()
    {
        using var f = new Fixture();
        f.Call(f.Inventory, "Disable");
        int unregistrations = 0;
        f.On(f.Registry, "InventoryUnregistered", () => unregistrations++);
        f.On(f.Inventory, "InventoryEnabled", () => f.Call(f.Character, "KillCharacter"));
        Assert.Throws<TargetInvocationException>(() => f.Call(f.Equipment, "PostLoadEntity"));
        Assert.True(f.Poisoned);
        Assert.Equal(0, unregistrations);
        Assert.Equal(1, f.RegisteredProcessors);
        Assert.Equal(1, f.Quantity);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly NativeManagedTestContext _native = new();
        internal object Resources { get; }
        internal object Equipment { get; }
        internal object Inventory { get; }
        internal object Character { get; }
        internal object Citizen { get; }
        internal object Registry { get; }
        private readonly object _counter;
        internal bool Poisoned => (bool)Get(Resources, "IsIndeterminate")!;
        internal int Quantity => (int)Call(Inventory, "AmountInStock", "Water")!;
        internal int RegisteredProcessors
        {
            get
            {
                var processed = _counter.GetType().GetField("_processedGoodCounter", Flags)!.GetValue(_counter)!;
                return ((IList)processed.GetType().GetField("_goodProcessors", Flags)!.GetValue(processed)!).Count;
            }
        }

        internal Fixture()
        {
            var mod = _native.LoadMod();
            Resources = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.Resources.NativeResourceCoordinator")!)!;
            Equipment = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.FireResponse.WardenEquipment")!, Resources)!;
            var goods = RuntimeHelpers.GetUninitializedObject(T("Timberborn.Goods", "GoodService"));
            var goodType = T("Timberborn.Goods", "GoodSpec");
            var good = Activator.CreateInstance(goodType)!;
            goodType.GetProperty("Id")!.SetValue(good, "Water");
            var map = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), goodType))!;
            map.Add("Water", good);
            Set(goods, "_goodSpecsById", map);
            Inventory = RuntimeHelpers.GetUninitializedObject(T("Timberborn.InventorySystem", "Inventory"));
            foreach (string field in new[] { "_storage", "_reservedStock", "_reservedCapacity" })
                Set(Inventory, field, Activator.CreateInstance(T("Timberborn.Goods", "GoodRegistry")));
            Set(Inventory, "_allowedGoods", Activator.CreateInstance(T("Timberborn.Goods", "StorableGoodRegistry")));
            AttachCache(Inventory, Activator.CreateInstance(T("Timberborn.InventorySystem", "Inventories"))!);
            var factory = Activator.CreateInstance(T("Timberborn.InventorySystem", "InventoryInitializerFactory"), goods)!;
            var initializer = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.FireResponse.WardenEquipmentInventoryInitializer")!, factory)!;
            Call(initializer, "Initialize", Equipment, Inventory);
            Call(Inventory, "Enable");
            Call(Inventory, "GiveExistingIgnoringCapacity", Activator.CreateInstance(T("Timberborn.Goods", "GoodAmount"), "Water", 1));

            Character = Activator.CreateInstance(T("Timberborn.Characters", "Character"),
                Activator.CreateInstance(T("Timberborn.SingletonSystem", "EventBus")), null, null)!;
            Citizen = RuntimeHelpers.GetUninitializedObject(T("Timberborn.GameDistricts", "Citizen"));
            Set(Citizen, "_unassignedCitizenRegistry", Activator.CreateInstance(T("Timberborn.GameDistricts", "UnassignedCitizenRegistry")));
            AttachCache(Equipment, Inventory, Character, Citizen);
            Call(Citizen, "Awake"); // Native Citizen subscribes first: its OnDied unassigns before our handler.
            Call(Equipment, "Awake");
            Registry = Activator.CreateInstance(T("Timberborn.InventorySystem", "DistrictInventoryRegistry"), goods)!;
            Call(Registry, "Add", Inventory); // Native private registration still raises events.
            _counter = Activator.CreateInstance(T("Timberborn.ResourceCountingSystem", "DistrictResourceCounter"))!;
            Call(_counter, "Add", Equipment.GetType().GetField("_equipmentCounter", Flags)!.GetValue(Equipment));
            Set(Equipment, "_counter", _counter);
            Set(Equipment, "_registry", Registry);
        }

        internal void DeleteThroughNativeEntity(Action deletedEvent)
        {
            var type = T("Timberborn.EntitySystem", "EntityComponent");
            var entity = RuntimeHelpers.GetUninitializedObject(type);
            var state = type.GetField("_entityState", Flags)!;
            state.SetValue(entity, Enum.Parse(state.FieldType, "Initialized"));
            Set(entity, "<RegisteredComponents>k__BackingField",
                Activator.CreateInstance(typeof(List<>).MakeGenericType(T("Timberborn.EntitySystem", "IRegisteredComponent"))));
            Set(entity, "_entityComponentRegistry",
                RuntimeHelpers.GetUninitializedObject(T("Timberborn.EntitySystem", "EntityComponentRegistry")));
            var bus = Activator.CreateInstance(T("Timberborn.SingletonSystem", "EventBus"))!;
            Set(bus, "_ready", true);
            var subscriptions = bus.GetType().GetField("_subscriptions", Flags)!.GetValue(bus)!;
            Call(subscriptions, "Add", T("Timberborn.EntitySystem", "EntityDeletedEvent"), this,
                (Action<object>)(_ => deletedEvent()));
            Set(entity, "_eventBus", bus);
            AttachCache(entity, Equipment, Inventory, Character, Citizen);
            Call(entity, "Delete"); // Actual native loop runs equipment, Citizen teardown, then EntityDeletedEvent.
            Assert.True((bool)Get(entity, "Deleted")!);
        }

        internal void Transfer(Action action) => Call(Resources, "TransferInventory", action);
        internal void Capture(Action action) => Resources.GetType().GetMethod("CaptureAtRest")!.MakeGenericMethod(typeof(int))
            .Invoke(Resources, [(Func<int>)(() => { action(); return 1; })]);
        internal void On(object owner, string name, Action action)
        {
            var ev = owner.GetType().GetEvent(name)!;
            var parameters = ev.EventHandlerType!.GetMethod("Invoke")!.GetParameters().Select(p => Expression.Parameter(p.ParameterType));
            ev.AddEventHandler(owner, Expression.Lambda(ev.EventHandlerType,
                Expression.Invoke(Expression.Constant(action)), parameters).Compile());
        }
        internal object? Call(object owner, string name, params object?[] args) => owner.GetType().GetMethods(Flags)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length && m.GetParameters()
                .Select((p, i) => args[i] is null || p.ParameterType.IsInstanceOfType(args[i])).All(value => value)).Invoke(owner, args);
        internal object? Get(object owner, string name) => owner.GetType().GetProperty(name, Flags)!.GetValue(owner);
        private Type T(string assembly, string name) => _native.LoadNative(assembly).GetType(assembly + "." + name)!;
        private static void Set(object owner, string name, object? value) => owner.GetType().GetField(name, Flags)!.SetValue(owner, value);
        private void AttachCache(params object[] components)
        {
            var cache = RuntimeHelpers.GetUninitializedObject(T("Timberborn.BaseComponentSystem", "ComponentCache"));
            var mapType = T("Timberborn.BaseComponentSystem", "TypeIndexMap");
            var map = Activator.CreateInstance(mapType)!;
            var list = components.ToList();
            var readOnly = Activator.CreateInstance(T("Timberborn.Common", "ReadOnlyList`1").MakeGenericType(typeof(object)),
                Flags, null, [list], null)!;
            foreach (var type in components.Select(c => c.GetType())
                .Append(T("Timberborn.EntitySystem", "IDeletableEntity")).Distinct())
                mapType.GetMethod("CacheType")!.MakeGenericMethod(type).Invoke(map, [readOnly]);
            Set(cache, "_components", list);
            Set(cache, "_typeIndexMap", map);
            foreach (var component in components)
                T("Timberborn.BaseComponentSystem", "BaseComponent").GetField("_componentCache", Flags)!.SetValue(component, cache);
        }
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        public void Dispose() => _native.Dispose();
    }
}
