using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeWardenPublicInventoryTests
{
    [Fact]
    public void PublicStationInputUsesNativeEmptyingAndBlockingAdmission()
    {
        using var f = new Fixture();
        var components = f.TemplateTypes();
        foreach (string name in new[] { "Emptiable", "EmptyInventoriesWorkplaceBehavior", "RemoveUnwantedStockWorkplaceBehavior", "EmptiableHaulBehaviorProvider", "UnwantedStockHaulBehaviorProvider" })
            Assert.Single(components, t => t == f.T("Timberborn.Emptying", name));
        Assert.Contains(f.T("Timberborn.BlockingSystem", "BlockableObject"), components);
        var inventory = f.Inventory(station: true, includeValidator: true);
        f.VerifyHaulProviders();
        Assert.Equal("Wildfire.WardenStation", f.Get(inventory, "ComponentName"));
        Assert.True((bool)f.Get(inventory, "PublicInput")!);
        Assert.False((bool)f.Get(inventory, "PublicOutput")!);
        f.Call(f.District, "Add", inventory);
        Assert.Contains(inventory, f.Capacity());
        Assert.True(f.IsTaking(inventory));

        f.Call(f.Emptiable!, "MarkForEmptyingWithoutStatus");
        Assert.False(f.IsTaking(inventory));
        f.Set(f.Emptiable!, "<IsMarkedForEmptying>k__BackingField", false);
        var blocker = new object();
        f.Call(f.Blockable!, "Block", blocker);
        Assert.False(f.IsTaking(inventory));
        f.Call(f.Blockable!, "Unblock", blocker);
        Assert.True(f.IsTaking(inventory));
        f.Call(inventory, "ReserveCapacity", f.Amount(20));
        Assert.False(f.IsTaking(inventory));
        Assert.Empty(f.Capacity());
        Assert.Equal(0, f.Call(inventory, "AmountInStock", "Water"));
    }

    [Fact]
    public void MissingValidatorReproducesActualPickerFailureWhilePrivateEquipmentIsNeverCandidate()
    {
        using var f = new Fixture();
        var station = f.Inventory(station: true, includeValidator: false);
        f.Call(f.District, "Add", station);
        Assert.Contains(station, f.Capacity());
        var failure = Assert.Throws<TargetInvocationException>(() => f.IsTaking(station));
        Assert.IsType<NullReferenceException>(failure.InnerException);
        var equipment = f.Inventory(station: false, includeValidator: false);
        Assert.False((bool)f.Get(equipment, "PublicInput")!);
        Assert.False((bool)f.Get(equipment, "PublicOutput")!);
        f.Call(f.District, "Add", equipment);
        Assert.DoesNotContain(equipment, f.Capacity());
        Assert.Same(station, Assert.Single(f.Capacity()));
    }

    // Actual native initializer, component cache, registry and picker methods over supplied managed
    // component state. No GameObject, navigation path, complete lifecycle or live hauling is claimed.
    private sealed class Fixture : IDisposable
    {
        private readonly NativeManagedTestContext _native = new();
        private readonly Assembly _mod;
        private readonly object _factory;
        private readonly List<object> _emptying = new();
        internal object District { get; }
        internal object? Emptiable { get; private set; }
        internal object? Blockable { get; private set; }
        internal Fixture()
        {
            _mod = _native.LoadMod();
            _factory = Activator.CreateInstance(T("Timberborn.InventorySystem", "InventoryInitializerFactory"), new object?[] { null })!;
            District = RuntimeHelpers.GetUninitializedObject(T("Timberborn.InventorySystem", "DistrictInventoryRegistry"));
            var registry = Activator.CreateInstance(T("Timberborn.InventorySystem", "InventoryRegistry"),
                ReadOnly(typeof(string), new List<string> { "Water" }))!;
            Set(District, "_publicInventoryRegistry", registry);
        }
        internal Type T(string assembly, string name) => _native.LoadNative(assembly).GetType(assembly + "." + name)!;
        private Type Mod(string name) => _mod.GetType(name)!;
        internal object Amount(int amount) => Activator.CreateInstance(T("Timberborn.Goods", "GoodAmount"), "Water", amount)!;
        internal object Inventory(bool station, bool includeValidator)
        {
            var inventory = RuntimeHelpers.GetUninitializedObject(T("Timberborn.InventorySystem", "Inventory"));
            foreach (string field in new[] { "_storage", "_reservedStock", "_reservedCapacity" })
                Set(inventory, field, Activator.CreateInstance(T("Timberborn.Goods", "GoodRegistry"))!);
            Set(inventory, "_allowedGoods", Activator.CreateInstance(T("Timberborn.Goods", "StorableGoodRegistry"))!);
            var owner = RuntimeHelpers.GetUninitializedObject(Mod("Wildfire.Timberborn.FireResponse." + (station ? "WardenStation" : "WardenEquipment")));
            var components = new List<object> { inventory, owner, Activator.CreateInstance(T("Timberborn.InventorySystem", "Inventories"))! };
            if (station)
            {
                Blockable = Activator.CreateInstance(T("Timberborn.BlockingSystem", "BlockableObject"))!;
                components.Add(Blockable);
            }
            if (includeValidator)
            {
                Emptiable = RuntimeHelpers.GetUninitializedObject(T("Timberborn.Emptying", "Emptiable"));
                components.Add(Emptiable);
                foreach (string name in new[] { "EmptyInventoriesWorkplaceBehavior", "RemoveUnwantedStockWorkplaceBehavior", "EmptiableHaulBehaviorProvider", "UnwantedStockHaulBehaviorProvider" })
                    _emptying.Add(RuntimeHelpers.GetUninitializedObject(T("Timberborn.Emptying", name)));
                components.AddRange(_emptying);
            }
            Cache(components);
            var initializer = Activator.CreateInstance(Mod("Wildfire.Timberborn.FireResponse." +
                (station ? "WardenStationInventoryInitializer" : "WardenEquipmentInventoryInitializer")), _factory)!;
            Call(initializer, "Initialize", owner, inventory);
            SetBase(inventory, "<Enabled>k__BackingField", true);
            if (Emptiable is not null) SetBase(Emptiable, "<Enabled>k__BackingField", true);
            return inventory;
        }
        internal Type[] TemplateTypes()
        {
            var provider = Activator.CreateInstance(Mod("Wildfire.Timberborn.Runtime.WildfireConfigurator+WardenTemplateModuleProvider"),
                Flags, null, new object?[] { null, null }, null)!;
            var building = RuntimeHelpers.GetUninitializedObject(T("Timberborn.Buildings", "BuildingsConfigurator"));
            var modules = Array.CreateInstance(T("Timberborn.TemplateInstantiation", "TemplateModule"), 3);
            modules.SetValue(Call(provider, "Get"), 0);
            modules.SetValue(Call(building, "ProvideTemplateModule"), 1);
            modules.SetValue(Call(RuntimeHelpers.GetUninitializedObject(T("Timberborn.Emptying", "EmptyingConfigurator")), "ProvideTemplateModule"), 2);
            var factory = Activator.CreateInstance(T("Timberborn.TemplateInstantiation", "TemplateInstantiatorProvider"), null, null, modules)!;
            var instantiator = Call(factory, "Get")!;
            var specs = Array.CreateInstance(T("Timberborn.BlueprintSystem", "ComponentSpec"), 2);
            specs.SetValue(Activator.CreateInstance(Mod("Wildfire.Timberborn.FireResponse.WildfireWardenStationSpec")), 0);
            specs.SetValue(Activator.CreateInstance(T("Timberborn.Buildings", "BuildingSpec")), 1);
            var blueprintType = T("Timberborn.BlueprintSystem", "Blueprint");
            var children = typeof(System.Collections.Immutable.ImmutableArray<>).MakeGenericType(blueprintType).GetField("Empty")!.GetValue(null);
            var blueprint = Activator.CreateInstance(blueprintType, "Wildfire.WardenStation.IronTeeth", specs, children)!;
            object?[] args = [blueprint, null, null];
            instantiator.GetType().GetMethod("GetInstanceComponents", Flags)!.Invoke(instantiator, args);
            return ((IEnumerable)args[2]!).Cast<Type>().ToArray();
        }
        internal void VerifyHaulProviders()
        {
            foreach (var component in _emptying) Call(component, "Awake");
            Assert.Same(_emptying[0], _emptying[2].GetType().GetField("_emptyInventoriesWorkplaceBehavior", Flags)!.GetValue(_emptying[2]));
            Assert.Same(_emptying[1], _emptying[3].GetType().GetField("_removeUnwantedStockWorkplaceBehavior", Flags)!.GetValue(_emptying[3]));
        }
        internal object[] Capacity()
        {
            var values = Call(District, "ActiveInventoriesWithCapacity", "Water")!;
            var iterator = Call(values, "GetEnumerator")!;
            var result = new List<object>();
            while ((bool)Call(iterator, "MoveNext")!) result.Add(Get(iterator, "Current")!);
            return result.ToArray();
        }
        internal bool IsTaking(object inventory) => (bool)T("Timberborn.InventorySystem", "DistrictInventoryPicker")
            .GetMethod("InventoryIsTaking", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [inventory, Amount(1)])!;
        private void Cache(List<object> components)
        {
            var cache = RuntimeHelpers.GetUninitializedObject(T("Timberborn.BaseComponentSystem", "ComponentCache"));
            var map = Activator.CreateInstance(T("Timberborn.BaseComponentSystem", "TypeIndexMap"))!;
            foreach (var type in components.Select(c => c.GetType()).Concat(new[] { T("Timberborn.InventorySystem", "IInventoryValidator"), T("Timberborn.BlockingSystem", "BlockableObject") }).Distinct())
                map.GetType().GetMethod("CacheType")!.MakeGenericMethod(type).Invoke(map, [ReadOnly(typeof(object), components)]);
            Set(cache, "_components", components); Set(cache, "_typeIndexMap", map); Set(cache, "_name", "Supplied station components");
            foreach (var component in components) SetBase(component, "_componentCache", cache);
        }
        private object ReadOnly(Type element, object list) => Activator.CreateInstance(T("Timberborn.Common", "ReadOnlyList`1").MakeGenericType(element), Flags, null, [list], null)!;
        private void SetBase(object owner, string field, object value) => T("Timberborn.BaseComponentSystem", "BaseComponent").GetField(field, Flags)!.SetValue(owner, value);
        internal void Set(object owner, string field, object? value) => owner.GetType().GetField(field, Flags)!.SetValue(owner, value);
        internal object? Get(object owner, string property) => owner.GetType().GetProperty(property, Flags)!.GetValue(owner);
        internal object? Call(object owner, string method, params object?[] args) => owner.GetType().GetMethods(Flags | BindingFlags.Static)
            .Single(m => m.Name == method && m.GetParameters().Length == args.Length && m.GetParameters().Select((p, i) => args[i] is null || p.ParameterType.IsInstanceOfType(args[i])).All(x => x)).Invoke(owner, args);
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        public void Dispose() => _native.Dispose();
    }
}
