using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeExecutorTransformTests
{
    [Theory]
    [InlineData("FireResponse.WardenExecutor")]
    [InlineData("Ash.AshHarvestExecutor")]
    [InlineData("FireBell.BorrowedDutyExecutor")]
    [InlineData("Beavers.Emergency.WildfireCarryEmergencyExecutor")]
    public void ActualExecutorAwakeBindsNativeCachedTransformWithoutLookingForADecorator(string name)
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        Type T(string assembly, string type) => native.LoadNative(assembly).GetType(assembly + "." + type)!;
        var resources = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.Resources.NativeResourceCoordinator")!)!;
        var type = mod.GetType("Wildfire.Timberborn." + name)!;
        object executor = name switch
        {
            "FireResponse.WardenExecutor" => Activator.CreateInstance(type, null, null, resources, null, null)!,
            "Ash.AshHarvestExecutor" => Activator.CreateInstance(type, null, null, resources, null, null)!,
            "FireBell.BorrowedDutyExecutor" => Activator.CreateInstance(type,
                Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.FireBell.BorrowedDutyFixture")!), null, resources, null, null)!,
            _ => Activator.CreateInstance(type,
                Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.Beavers.Emergency.CarryEmergencySession")!), null, null)!
        };
        var components = new List<object> { executor };
        foreach (var (assembly, component) in new[]
        {
            ("Timberborn.EntitySystem", "EntityComponent"),
            ("Timberborn.WalkingSystem", "Walker"),
            ("Timberborn.WalkingSystem", "WalkerMover"),
            ("Timberborn.WalkingSystem", "WalkToPositionExecutor"),
            ("Timberborn.WalkingSystem", "WalkToAccessibleExecutor"),
            ("Timberborn.BehaviorSystem", "BehaviorManager"),
            ("Timberborn.CharacterNavigation", "Navigator"),
            ("Timberborn.InventorySystem", "GoodReserver"),
            ("Timberborn.Carrying", "GoodCarrier"),
            ("Timberborn.Carrying", "CarryRootBehavior"),
            ("Timberborn.WorkSystem", "Worker"),
            ("Timberborn.WorkSystem", "WorkerWorkingHours"),
            ("Timberborn.WorkSystem", "WorkRefuser"),
            ("Timberborn.NeedSystem", "NeedManager"),
            ("Timberborn.MortalSystem", "Mortal"),
            ("Timberborn.GameDistricts", "Citizen"),
            ("Timberborn.Characters", "Character"),
            ("Timberborn.CharacterControlSystem", "ControllableCharacter"),
            ("Timberborn.EnterableSystem", "Enterer")
        }) components.Add(RuntimeHelpers.GetUninitializedObject(T(assembly, component)));
        components.Add(Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.FireResponse.WardenEquipment")!, resources)!);

        var transformType = native.LoadNative("UnityEngine.CoreModule").GetType("UnityEngine.Transform")!;
        // A reference sentinel only: no Unity liveness or transform.position call is fabricated.
        var cachedTransform = RuntimeHelpers.GetUninitializedObject(transformType);
        var cacheType = T("Timberborn.BaseComponentSystem", "ComponentCache");
        var cache = RuntimeHelpers.GetUninitializedObject(cacheType);
        cacheType.GetField("<CachedTransform>k__BackingField", Flags)!.SetValue(cache, cachedTransform);
        cacheType.GetField("_components", Flags)!.SetValue(cache, components);
        var indexType = T("Timberborn.BaseComponentSystem", "TypeIndexMap");
        var index = Activator.CreateInstance(indexType)!;
        var list = Activator.CreateInstance(T("Timberborn.Common", "ReadOnlyList`1").MakeGenericType(typeof(object)),
            Flags, null, [components], null)!;
        foreach (var componentType in components.Select(c => c.GetType()).Append(transformType))
            indexType.GetMethod("CacheType")!.MakeGenericMethod(componentType).Invoke(index, [list]);
        cacheType.GetField("_typeIndexMap", Flags)!.SetValue(cache, index);
        var baseType = T("Timberborn.BaseComponentSystem", "BaseComponent");
        foreach (object component in components)
            baseType.GetField("_componentCache", Flags)!.SetValue(component, cache);

        Assert.Same(cachedTransform, baseType.GetProperty("Transform")!.GetValue(executor));
        Assert.Null(baseType.GetMethod("GetComponent")!.MakeGenericMethod(transformType).Invoke(executor, null));
        type.GetMethod("Awake")!.Invoke(executor, null); // Actual production wiring, including actual native path-event subscription.
        Assert.Same(cachedTransform, type.GetField("_transform", Flags)!.GetValue(executor));
    }

    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
}
