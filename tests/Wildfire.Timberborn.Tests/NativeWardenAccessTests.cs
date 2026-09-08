using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeWardenAccessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AwakeUsesBuildingRoleWhenNativeCacheContainsMultipleAccessibleComponents(bool reverseAccessOrder)
    {
        using var native = new NativeManagedTestContext();
        var baseAssembly = native.LoadNative("Timberborn.BaseComponentSystem");
        var accessibleType = native.LoadNative("Timberborn.Navigation").GetType("Timberborn.Navigation.Accessible")!;
        var buildingType = native.LoadNative("Timberborn.Buildings").GetType("Timberborn.Buildings.BuildingAccessible")!;
        var workplaceType = native.LoadNative("Timberborn.WorkSystem").GetType("Timberborn.WorkSystem.Workplace")!;
        var stationType = native.LoadMod().GetType("Wildfire.Timberborn.FireResponse.WardenStation")!;
        var station = Activator.CreateInstance(stationType)!;
        var workplace = RuntimeHelpers.GetUninitializedObject(workplaceType);
        var building = Activator.CreateInstance(buildingType)!;
        var constructionType = native.LoadNative("Timberborn.BuildingsNavigation")
            .GetType("Timberborn.BuildingsNavigation.ConstructionSiteAccessible")!;
        var construction = RuntimeHelpers.GetUninitializedObject(constructionType);
        var intended = Activator.CreateInstance(accessibleType, new object?[] { null })!;
        var other = Activator.CreateInstance(accessibleType, new object?[] { null })!;
        var initializerType = native.LoadNative("Timberborn.AccessibleNavigation")
            .GetType("Timberborn.AccessibleNavigation.AccessibleInitializer")!;
        var initializer = Activator.CreateInstance(initializerType, true)!;
        var components = new List<object> { station, workplace, building, construction };
        components.AddRange(reverseAccessOrder ? [intended, other] : [other, intended]);
        var cacheType = baseAssembly.GetType("Timberborn.BaseComponentSystem.ComponentCache")!;
        var cache = RuntimeHelpers.GetUninitializedObject(cacheType);
        var mapType = baseAssembly.GetType("Timberborn.BaseComponentSystem.TypeIndexMap")!;
        var map = Activator.CreateInstance(mapType)!;
        var readOnlyType = native.LoadNative("Timberborn.Common").GetType("Timberborn.Common.ReadOnlyList`1")!.MakeGenericType(typeof(object));
        var readOnly = Activator.CreateInstance(readOnlyType, Private, null, [components], null)!;
        foreach (var type in new[] { accessibleType, buildingType, workplaceType, native.LoadNative("Timberborn.Navigation").GetType("Timberborn.Navigation.IBlockedAccessible")! })
            mapType.GetMethod("CacheType")!.MakeGenericMethod(type).Invoke(map, [readOnly]);
        cacheType.GetField("_components", Private)!.SetValue(cache, components);
        cacheType.GetField("_typeIndexMap", Private)!.SetValue(cache, map);
        cacheType.GetField("_name", Private)!.SetValue(cache, "WardenStation.IronTeeth(Clone)");
        var baseType = baseAssembly.GetType("Timberborn.BaseComponentSystem.BaseComponent")!;
        foreach (var component in components)
            baseType.GetField("_componentCache", Private)!.SetValue(component, cache);

        initializerType.GetMethod("Initialize")!.Invoke(initializer, [building, intended]);
        initializerType.GetMethod("Initialize")!.Invoke(initializer, [construction, other]);
        Assert.Equal("Building", accessibleType.GetProperty("ComponentName")!.GetValue(intended));
        Assert.Equal("ConstructionSite", accessibleType.GetProperty("ComponentName")!.GetValue(other));
        stationType.GetMethod("Awake")!.Invoke(station, null);

        Assert.Same(intended, stationType.GetProperty("Access")!.GetValue(station));
        Assert.Same(workplace, stationType.GetProperty("Workplace")!.GetValue(station));
    }

    [Fact]
    public void ActualNativeBuildingModulesCreateSeparateBuildingAndConstructionAccessesOnce()
    {
        using var native = new NativeManagedTestContext();
        Type T(string assembly, string name) => native.LoadNative(assembly).GetType(name)!;
        object Module(string assembly, string name)
        {
            var type = T(assembly, name);
            return type.GetMethod("ProvideTemplateModule", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)!
                .Invoke(RuntimeHelpers.GetUninitializedObject(type), null)!;
        }
        var initializerType = T("Timberborn.AccessibleNavigation", "Timberborn.AccessibleNavigation.AccessibleInitializer");
        var providerType = T("Timberborn.AccessibleNavigation", "Timberborn.AccessibleNavigation.AccessibleNavigationConfigurator+TemplateModuleProvider");
        var provider = Activator.CreateInstance(providerType, [Activator.CreateInstance(initializerType, true)])!;
        var modules = Array.CreateInstance(T("Timberborn.TemplateInstantiation", "Timberborn.TemplateInstantiation.TemplateModule"), 3);
        modules.SetValue(Module("Timberborn.Buildings", "Timberborn.Buildings.BuildingsConfigurator"), 0);
        modules.SetValue(Module("Timberborn.BuildingsNavigation", "Timberborn.BuildingsNavigation.BuildingsNavigationConfigurator"), 1);
        modules.SetValue(providerType.GetMethod("Get")!.Invoke(provider, null), 2);
        var factory = Activator.CreateInstance(T("Timberborn.TemplateInstantiation", "Timberborn.TemplateInstantiation.TemplateInstantiatorProvider"), null, null, modules)!;
        var instantiator = factory.GetType().GetMethod("Get")!.Invoke(factory, null)!;
        var source = FindStationBlueprint();
        using var definition = JsonDocument.Parse(File.ReadAllText(source));
        Assert.True(definition.RootElement.TryGetProperty("BuildingSpec", out _));
        Assert.True(definition.RootElement.TryGetProperty("BuildingAccessibleSpec", out _));
        Assert.True(definition.RootElement.TryGetProperty("EnterableSpec", out _));
        var specType = T("Timberborn.BlueprintSystem", "Timberborn.BlueprintSystem.ComponentSpec");
        var specs = Array.CreateInstance(specType, 2);
        specs.SetValue(Activator.CreateInstance(T("Timberborn.Buildings", "Timberborn.Buildings.BuildingSpec")), 0);
        specs.SetValue(Activator.CreateInstance(T("Timberborn.Buildings", "Timberborn.Buildings.BuildingAccessibleSpec")), 1);
        var blueprintType = T("Timberborn.BlueprintSystem", "Timberborn.BlueprintSystem.Blueprint");
        var children = typeof(System.Collections.Immutable.ImmutableArray<>).MakeGenericType(blueprintType).GetField("Empty")!.GetValue(null)!;
        var blueprint = Activator.CreateInstance(blueprintType, "Wildfire.WardenStation.IronTeeth", specs, children)!;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            object?[] args = [blueprint, null, null];
            instantiator.GetType().GetMethod("GetInstanceComponents", Private)!.Invoke(instantiator, args);
            var types = ((IEnumerable)args[2]!).Cast<Type>().ToArray();
            Assert.Single(types, type => type.FullName == "Timberborn.Buildings.BuildingAccessible");
            Assert.Single(types, type => type.FullName == "Timberborn.BuildingsNavigation.ConstructionSiteAccessible");
            Assert.Equal(2, types.Count(type => type.FullName == "Timberborn.Navigation.Accessible"));
        }
    }

    private static string FindStationBlueprint()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, "src/Wildfire.Timberborn/Data/Buildings/FireResponse/WardenStation.IronTeeth.blueprint.json");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Station blueprint not found.");
    }

    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
}
