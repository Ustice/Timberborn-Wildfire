using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeFertilizerDestinationTests
{
    private static readonly NativeManagedTestContext Native = NativeManagedTestContext.ProxyContracts;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type T(string assembly, string name) => Native.LoadNative(assembly).GetType(assembly + "." + name)!;
    private static object Position(float x, float z = 0) => Activator.CreateInstance(Native.LoadNative("UnityEngine.CoreModule").GetType("UnityEngine.Vector3")!, x, 0f, z)!;

    [Fact]
    public void NativePositionDestinationOffsetsActualPathCornerAndKeepsItsNativeSaveRepresentation()
    {
        var cornerType = T("Timberborn.Navigation", "PathCorner");
        var path = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(cornerType))!;
        object Corner(float x) => Activator.CreateInstance(cornerType, Position(x), 2.5f, 17)!;
        var navigation = NativePersistenceProxy.Create(T("Timberborn.Navigation", "INavigationService"), (method, args) =>
        {
            if (method.Name == "FindPathUnlimitedRange")
            {
                Assert.Equal(Position(3), args[1]);
                var result = (IList)args[2]!;
                result.Add(Corner(0)); result.Add(Corner(3));
                args[3] = 3f;
                return true;
            }
            if (method.Name == "IsOnNavMesh") return true;
            throw new NotSupportedException(method.Name);
        });
        var factory = Activator.CreateInstance(T("Timberborn.WalkingSystem", "PositionDestinationFactory"), navigation, null)!;
        var destination = factory.GetType().GetMethod("Create")!.Invoke(factory, [Position(3), .4f])!;
        object?[] find = [Position(0), path, 0f];
        Assert.Equal(true, destination.GetType().GetMethod("FindPath")!.Invoke(destination, find));
        Assert.Equal(3f, find[2]);
        var last = path[1]!;
        var endpoint = cornerType.GetProperty("Position")!.GetValue(last)!;
        Assert.Equal(2.6f, (float)endpoint.GetType().GetField("x")!.GetValue(endpoint)!, 5);
        Assert.Equal(2.5f, cornerType.GetProperty("Speed")!.GetValue(last));
        Assert.Equal(17, cornerType.GetProperty("GroupId")!.GetValue(last));
        Assert.NotEqual(destination.GetType().GetProperty("Destination")!.GetValue(destination), endpoint);

        var values = new Dictionary<object, object?>();
        object ObjectProxy(string name) => NativePersistenceProxy.Create(T("Timberborn.Persistence", name), (method, args) =>
        {
            if (method.Name == "Set") { values[args[0]!] = args[1]; return null; }
            if (method.Name == "Get") return values[args[0]!];
            if (method.Name == "Has") return values.ContainsKey(args[0]!);
            throw new NotSupportedException(method.Name);
        });
        var serializer = Activator.CreateInstance(T("Timberborn.WalkingSystem", "DestinationValueSerializer"), null, factory)!;
        var saver = NativePersistenceProxy.Create(T("Timberborn.Persistence", "IValueSaver"), (_, _) => ObjectProxy("IObjectSaver"));
        serializer.GetType().GetMethod("Serialize")!.Invoke(serializer, [destination, saver]);
        Assert.Contains("PositionDestination", values.Values);
        Assert.Contains(.4f, values.Values);
        var loader = NativePersistenceProxy.Create(T("Timberborn.Persistence", "IValueLoader"), (_, _) => ObjectProxy("IObjectLoader"));
        var decoded = serializer.GetType().GetMethod("Deserialize")!.Invoke(serializer, [loader])!;
        Assert.Equal(false, decoded.GetType().GetProperty("Obsolete")!.GetValue(decoded));
        var restored = decoded.GetType().GetProperty("Value")!.GetValue(decoded)!;
        Assert.IsType(destination.GetType(), restored);
        Assert.Equal(destination, restored);
        Assert.NotSame(destination, restored);
        // Real native path math + serializer, supplied navigation path/mesh answer. No engine movement.
    }

    [Fact]
    public void ActualForestryModuleAndAwakeSelectTreeReacherWithoutUsingOtherYieldRoles()
    {
        var config = T("Timberborn.Forestry", "ForestryConfigurator");
        var module = config.GetMethod("ProvideTemplateModule", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
        var modules = Array.CreateInstance(T("Timberborn.TemplateInstantiation", "TemplateModule"), 1);
        modules.SetValue(module, 0);
        var provider = Activator.CreateInstance(T("Timberborn.TemplateInstantiation", "TemplateInstantiatorProvider"), null, null, modules)!;
        var instantiator = provider.GetType().GetMethod("Get")!.Invoke(provider, null)!;
        var specs = Array.CreateInstance(T("Timberborn.BlueprintSystem", "ComponentSpec"), 1);
        specs.SetValue(Activator.CreateInstance(T("Timberborn.Forestry", "TreeComponentSpec")), 0);
        var blueprintType = T("Timberborn.BlueprintSystem", "Blueprint");
        var children = typeof(System.Collections.Immutable.ImmutableArray<>).MakeGenericType(blueprintType).GetField("Empty")!.GetValue(null)!;
        var blueprint = Activator.CreateInstance(blueprintType, "NativeTreeRoleFixture", specs, children)!;
        object?[] args = [blueprint, null, null];
        instantiator.GetType().GetMethod("GetInstanceComponents", Flags)!.Invoke(instantiator, args);
        var types = ((IEnumerable)args[2]!).Cast<Type>().ToArray();
        Assert.Single(types, t => t == T("Timberborn.Forestry", "TreeReacher"));
        Assert.Single(types, t => t == T("Timberborn.Forestry", "TreeRemoveYieldStrategy"));

        var strategy = Activator.CreateInstance(T("Timberborn.Forestry", "TreeRemoveYieldStrategy"), new object?[] { null })!;
        var reacher = Activator.CreateInstance(T("Timberborn.Forestry", "TreeReacher"), new object?[] { null })!;
        var block = RuntimeHelpers.GetUninitializedObject(T("Timberborn.BlockSystem", "BlockObject"));
        var otherType = T("Timberborn.UncuttableYielding", "UncuttableReacher");
        var other = RuntimeHelpers.GetUninitializedObject(otherType);
        var components = new List<object> { strategy, other, reacher, block };
        var cacheType = T("Timberborn.BaseComponentSystem", "ComponentCache");
        var cache = RuntimeHelpers.GetUninitializedObject(cacheType);
        var map = Activator.CreateInstance(T("Timberborn.BaseComponentSystem", "TypeIndexMap"))!;
        var readOnlyType = T("Timberborn.Common", "ReadOnlyList`1").MakeGenericType(typeof(object));
        var readOnly = Activator.CreateInstance(readOnlyType, Flags, null, [components], null)!;
        foreach (var type in new[] { reacher.GetType(), block.GetType(), T("Timberborn.Yielding", "Yielder") })
            map.GetType().GetMethod("CacheType")!.MakeGenericMethod(type).Invoke(map, [readOnly]);
        cacheType.GetField("_components", Flags)!.SetValue(cache, components);
        cacheType.GetField("_typeIndexMap", Flags)!.SetValue(cache, map);
        var baseType = T("Timberborn.BaseComponentSystem", "BaseComponent");
        baseType.GetField("_componentCache", Flags)!.SetValue(strategy, cache);
        strategy.GetType().GetMethod("Awake")!.Invoke(strategy, null);
        Assert.Same(reacher, strategy.GetType().GetProperty("Reacher")!.GetValue(strategy));
        Assert.NotSame(other, strategy.GetType().GetProperty("Reacher")!.GetValue(strategy));
        var destination = Activator.CreateInstance(T("Timberborn.WalkingSystem", "PositionDestination"), null, null, Position(8), .4f)!;
        reacher.GetType().GetField("_destination", Flags)!.SetValue(reacher, destination);
        Assert.Same(destination, reacher.GetType().GetProperty("Destination")!.GetValue(reacher));
        // Actual module/cache/Awake/getter. Reacher model-derived InitializeEntity remains engine-only.
    }
}
