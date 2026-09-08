using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class WardenBehaviorOwnershipTests
{
    private static readonly NativeManagedTestContext Native = NativeManagedTestContext.ProxyContracts;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public void AdultTemplateProvidesOneBeaverOwnedWardenBehavior()
    {
        var providerType = Mod("Runtime.WildfireConfigurator+WardenTemplateModuleProvider");
        var provider = Activator.CreateInstance(providerType,
            RuntimeHelpers.GetUninitializedObject(Mod("FireResponse.WardenStationInventoryInitializer")),
            RuntimeHelpers.GetUninitializedObject(Mod("FireResponse.WardenEquipmentInventoryInitializer")))!;
        var modules = Array.CreateInstance(T("Timberborn.TemplateInstantiation", "TemplateModule"), 1);
        modules.SetValue(providerType.GetMethod("Get")!.Invoke(provider, null), 0);
        var factoryType = T("Timberborn.TemplateInstantiation", "TemplateInstantiatorProvider");
        var factory = Activator.CreateInstance(factoryType, null, null, modules)!;
        var instantiator = factoryType.GetMethod("Get")!.Invoke(factory, null)!;
        var specs = Array.CreateInstance(T("Timberborn.BlueprintSystem", "ComponentSpec"), 1);
        specs.SetValue(Activator.CreateInstance(T("Timberborn.Beavers", "AdultSpec")), 0);
        var blueprintType = T("Timberborn.BlueprintSystem", "Blueprint");
        var children = typeof(System.Collections.Immutable.ImmutableArray<>).MakeGenericType(blueprintType).GetField("Empty")!.GetValue(null)!;
        var blueprint = Activator.CreateInstance(blueprintType, "BeaverAdult", specs, children)!;
        object?[] args = [blueprint, null, null];
        instantiator.GetType().GetMethod("GetInstanceComponents", Flags)!.Invoke(instantiator, args);
        var types = ((IEnumerable)args[2]!).Cast<Type>().ToArray();
        Assert.Single(types, type => type.FullName == "Wildfire.Timberborn.FireResponse.WardenBehavior");
    }

    [Fact]
    public void NestedNativeWorkerTransferPreservesBeaverOwnerAndNativeJobFlag()
    {
        var behavior = Activator.CreateInstance(Mod("FireResponse.WardenBehavior"))!;
        var station = RuntimeHelpers.GetUninitializedObject(Mod("FireResponse.WardenStation"));
        var executor = Executor();
        var decision = behavior.GetType().GetMethod("Own", Flags)!.Invoke(behavior, [executor])!;
        var wrapped = decision.GetType().GetMethod("TransferNow")!.Invoke(null, [station, decision])!;
        Assert.Same(behavior, wrapped.GetType().GetProperty("Behavior")!.GetValue(wrapped));
        Assert.Same(executor, wrapped.GetType().GetProperty("Executor")!.GetValue(wrapped));
        Assert.Equal(false, wrapped.GetType().GetProperty("ShouldReturnToBehavior")!.GetValue(wrapped));
        var manager = Blank("Timberborn.BehaviorSystem", "BehaviorManager");
        Set(manager, "_runningBehavior", behavior);
        var worker = Blank("Timberborn.WorkSystem", "Worker");
        Set(worker, "_behaviorManager", manager);
        Assert.Equal(true, worker.GetType().GetProperty("JobRunning")!.GetValue(worker));
        Assert.True(T("Timberborn.WorkSystem", "IJobBehavior").IsAssignableFrom(behavior.GetType()));
        var release = behavior.GetType().GetMethod("Decide")!.Invoke(behavior, [null])!;
        Assert.Equal(true, release.GetType().GetProperty("ShouldReleaseNow")!.GetValue(release));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("WardenStation")]
    [InlineData("WardenBehavior")]
    public void NativeExecutorSaveIsIndependentButLoadRequiresResolvedBehavior(string? behaviorName)
    {
        bool behaviorSurvives = behaviorName is not null;
        var source = Executor();
        var sortie = Get(source, "_sortie")!;
        sortie.GetType().GetMethod("Restore")!.Invoke(sortie, [5, .75f]); // Returning, no station reference.
        var sourceManager = Blank("Timberborn.BehaviorSystem", "BehaviorManager");
        Set(sourceManager, "_runningExecutor", source);
        Set(sourceManager, "_runningExecutorElapsedTime", .25f);
        var values = new Dictionary<object, object?>();
        object Proxy(string contract) => NativePersistenceProxy.Create(T("Timberborn.Persistence", contract), (method, args) =>
        {
            if (method.Name == "Set") { values[args[0]!] = args[1]; return null; }
            if (method.Name == "Has") return values.ContainsKey(args[0]!);
            if (method.Name == "Get") return values[args[0]!];
            throw new NotSupportedException(method.Name);
        });
        var saver = NativePersistenceProxy.Create(T("Timberborn.WorldPersistence", "IEntitySaver"), (_, _) => Proxy("IObjectSaver"));
        sourceManager.GetType().GetMethod("SaveRunningExecutor", Flags)!.Invoke(sourceManager, [saver, Proxy("IObjectSaver")]);
        Assert.NotEmpty(values); // Actual native manager called actual WardenExecutor.Save even without a behavior.
        var restored = Executor();
        var manager = Blank("Timberborn.BehaviorSystem", "BehaviorManager");
        BindComponents([manager, restored], T("Timberborn.BehaviorSystem", "IExecutor"));
        if (behaviorSurvives) Set(manager, "_runningBehavior", RuntimeHelpers.GetUninitializedObject(Mod("FireResponse." + behaviorName)));
        int executorLoads = 0;
        var loader = NativePersistenceProxy.Create(T("Timberborn.WorldPersistence", "IEntityLoader"), (_, _) =>
        { executorLoads++; return Proxy("IObjectLoader"); });
        manager.GetType().GetMethod("LoadRunningExecutor", Flags)!.Invoke(manager, [loader, Proxy("IObjectLoader")]);
        Assert.Equal(behaviorSurvives ? 1 : 0, executorLoads);
        Assert.Equal(behaviorSurvives ? 5 : 0, Convert.ToInt32(restored.GetType().GetProperty("Phase")!.GetValue(restored)));
        Assert.Equal(behaviorSurvives, Get(restored, "_restoreWalk"));
        Assert.Equal(behaviorSurvives, Get(restored, "_needsReturnRoute"));
        Assert.Same(behaviorSurvives ? restored : null, Get(manager, "_runningExecutor"));
        // No Unity GameObject liveness, native deletion or movement is simulated here.
        // The null behavior is the native ReferenceSerializer missing-entity result established separately below.
    }

    [Fact]
    public void NativeReferencesRetainWorkerMarkerWhenStationGuidHasDisappeared()
    {
        var registry = Activator.CreateInstance(T("Timberborn.EntitySystem", "EntityRegistry"))!;
        var references = Activator.CreateInstance(T("Timberborn.WorldPersistence", "ReferenceSerializer"), registry)!;
        var behaviorType = T("Timberborn.BehaviorSystem", "Behavior");
        var entityType = T("Timberborn.EntitySystem", "EntityComponent");
        var serializer = references.GetType().GetMethod("Of")!.MakeGenericMethod(behaviorType).Invoke(references, null)!;
        var station = RuntimeHelpers.GetUninitializedObject(Mod("FireResponse.WardenStation"));
        var behavior = Activator.CreateInstance(Mod("FireResponse.WardenBehavior"))!;
        var stationEntity = RuntimeHelpers.GetUninitializedObject(entityType);
        var workerEntity = RuntimeHelpers.GetUninitializedObject(entityType);
        var stationId = Guid.NewGuid();
        var workerId = Guid.NewGuid();
        Set(stationEntity, "<EntityId>k__BackingField", stationId);
        Set(workerEntity, "<EntityId>k__BackingField", workerId);
        BindComponents([stationEntity, station], entityType, behaviorType);
        BindComponents([workerEntity, behavior], entityType, behaviorType);
        // Supply surviving registry membership, rather than pretending to destroy Unity GameObjects.
        ((IDictionary)Get(registry, "_entities")!).Add(workerId, workerEntity);
        string Serialize(object value)
        {
            string? text = null;
            var saver = NativePersistenceProxy.Create(T("Timberborn.Persistence", "IValueSaver"), (method, args) =>
            {
                Assert.Equal("AsString", method.Name);
                text = (string)args[0]!; return null;
            });
            serializer.GetType().GetMethod("Serialize")!.Invoke(serializer, [value, saver]);
            return Assert.IsType<string>(text);
        }
        object Deserialize(string text)
        {
            var loader = NativePersistenceProxy.Create(T("Timberborn.Persistence", "IValueLoader"), (method, _) =>
                method.Name == "AsString" ? text : throw new NotSupportedException(method.Name));
            return serializer.GetType().GetMethod("Deserialize")!.Invoke(serializer, [loader])!;
        }
        string oldReference = Serialize(station), newReference = Serialize(behavior);
        Assert.StartsWith(stationId + ":", oldReference);
        Assert.StartsWith(workerId + ":", newReference);
        var missing = Deserialize(oldReference);
        Assert.Equal(true, missing.GetType().GetProperty("Obsolete")!.GetValue(missing));
        var retained = Deserialize(newReference);
        Assert.Equal(false, retained.GetType().GetProperty("Obsolete")!.GetValue(retained));
        Assert.Same(behavior, retained.GetType().GetProperty("Value")!.GetValue(retained));
    }

    private static object Executor()
    {
        var resources = Activator.CreateInstance(Mod("Resources.NativeResourceCoordinator"))!;
        return Activator.CreateInstance(Mod("FireResponse.WardenExecutor"), null, null, resources, null, null)!;
    }
    private static void BindComponents(List<object> components, params Type[] indexedTypes)
    {
        var cache = Blank("Timberborn.BaseComponentSystem", "ComponentCache");
        Set(cache, "_components", components);
        var index = Activator.CreateInstance(T("Timberborn.BaseComponentSystem", "TypeIndexMap"))!;
        var list = Activator.CreateInstance(T("Timberborn.Common", "ReadOnlyList`1").MakeGenericType(typeof(object)), Flags, null, [components], null)!;
        foreach (var indexedType in indexedTypes)
            index.GetType().GetMethod("CacheType")!.MakeGenericMethod(indexedType).Invoke(index, [list]);
        Set(cache, "_typeIndexMap", index);
        foreach (var component in components)
            T("Timberborn.BaseComponentSystem", "BaseComponent").GetField("_componentCache", Flags)!.SetValue(component, cache);
    }
    private static Type T(string assembly, string name) => Native.LoadNative(assembly).GetType(assembly + "." + name)!;
    private static Type Mod(string name) => Native.LoadMod().GetType("Wildfire.Timberborn." + name)!;
    private static object Blank(string assembly, string name) => RuntimeHelpers.GetUninitializedObject(T(assembly, name));
    private static object? Get(object target, string field) => target.GetType().GetField(field, Flags)!.GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags)!.SetValue(target, value);
}
