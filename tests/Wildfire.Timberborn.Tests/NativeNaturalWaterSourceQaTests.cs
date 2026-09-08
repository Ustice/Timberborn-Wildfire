using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeNaturalWaterSourceQaTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(5.5f, 4f, 5.5f, true)]
    [InlineData(3.5f, 4f, 5.5f, true)]
    [InlineData(4.5f, 4f, 6.5f, true)]
    [InlineData(4.5f, 4f, 4.5f, true)]
    [InlineData(4.5f, 4f, 5.5f, false)] // Water cell itself.
    [InlineData(5.5f, 4f, 6.5f, false)] // Diagonal cannot silently widen reach.
    [InlineData(6.5f, 4f, 5.5f, false)]
    [InlineData(5.5f, 5f, 5.5f, true)] // Adjacent one-level bank.
    [InlineData(5.5f, 6f, 5.5f, false)]
    [InlineData(5.5f, 3f, 5.5f, false)]
    [InlineData(5.25f, 4f, 5.5f, false)]
    [InlineData(float.NaN, 4f, 5.5f, false)]
    [InlineData(float.PositiveInfinity, 4f, 5.5f, false)]
    [InlineData(float.MaxValue, 4f, 5.5f, false)]
    public void ExplicitNativeToUnityShoreCoordinatesAreNarrowAndChecked(float x, float y, float z, bool expected)
    {
        using var native = new NativeManagedTestContext();
        var vector = native.LoadNative("UnityEngine.CoreModule").GetType("UnityEngine.Vector3")!;
        var grid = native.LoadNative("UnityEngine.CoreModule").GetType("UnityEngine.Vector3Int")!;
        var type = native.LoadMod().GetType("Wildfire.Timberborn.FireBell.NaturalWaterSourceShore")!;
        object?[] args = [Activator.CreateInstance(grid, 4, 5, 4), Activator.CreateInstance(vector, x, y, z),
            Activator.CreateInstance(grid, 12, 10, 33), null];
        Assert.Equal(expected, type.GetMethod("TryGetShore", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args));
        if (expected)
        {
            Assert.Equal((int)x, grid.GetProperty("x")!.GetValue(args[3]));
            Assert.Equal((int)z, grid.GetProperty("y")!.GetValue(args[3]));
            Assert.Equal((int)y, grid.GetProperty("z")!.GetValue(args[3]));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ActualNativeTemplateClosureAddsOwnedSourceOnlyForExplicitMarker(bool marked)
    {
        using var native = new NativeManagedTestContext();
        Type T(string assembly, string name) => native.LoadNative(assembly).GetType(assembly + "." + name)!;
        var mod = native.LoadMod();
        var providerType = mod.GetType("Wildfire.Timberborn.FireBell.NaturalWaterSourceQaConfigurator+Module")!;
        var modules = Array.CreateInstance(T("Timberborn.TemplateInstantiation", "TemplateModule"), 4);
        modules.SetValue(providerType.GetMethod("Get")!.Invoke(Activator.CreateInstance(providerType, true), null), 0);
        var configs = new[] { ("Timberborn.BlockSystem", "BlockSystemConfigurator"),
            ("Timberborn.WaterBuildings", "WaterBuildingsConfigurator"), ("Timberborn.TemplateSystem", "TemplateSystemConfigurator") };
        for (int i = 0; i < configs.Length; i++)
        {
            var type = T(configs[i].Item1, configs[i].Item2);
            modules.SetValue(type.GetMethod("ProvideTemplateModule", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!
                .Invoke(RuntimeHelpers.GetUninitializedObject(type), null), i + 1);
        }
        var json = JsonDocument.Parse(File.ReadAllText(BlueprintPath()));
        var names = new[] { ("Timberborn.BlockSystem", "BlockObjectSpec"), ("Timberborn.TemplateSystem", "TemplateSpec"),
            ("Timberborn.WaterBuildings", "WaterInputSpec"), ("Timberborn.WaterBuildings", "WaterInputFixedSpec") };
        var specs = Array.CreateInstance(T("Timberborn.BlueprintSystem", "ComponentSpec"), marked ? 5 : 4);
        var deserialize = native.LoadNative("Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert")!
            .GetMethod("DeserializeObject", [typeof(string), typeof(Type)])!;
        for (int i = 0; i < names.Length; i++) specs.SetValue(deserialize.Invoke(null,
            [json.RootElement.GetProperty(names[i].Item2).GetRawText(), T(names[i].Item1, names[i].Item2)]), i);
        if (marked) specs.SetValue(Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.FireBell.NaturalWaterSourceQaSpec")!), 4);
        var blueprintType = T("Timberborn.BlueprintSystem", "Blueprint");
        var children = typeof(System.Collections.Immutable.ImmutableArray<>).MakeGenericType(blueprintType).GetField("Empty")!.GetValue(null)!;
        var blueprint = Activator.CreateInstance(blueprintType, "Fixture.Source", specs, children)!;
        var provider = Activator.CreateInstance(T("Timberborn.TemplateInstantiation", "TemplateInstantiatorProvider"), null, null, modules)!;
        var instantiator = provider.GetType().GetMethod("Get")!.Invoke(provider, null)!;
        object?[] args = [blueprint, null, null];
        instantiator.GetType().GetMethod("GetInstanceComponents", Private)!.Invoke(instantiator, args);
        var types = ((IEnumerable)args[2]!).Cast<Type>().Select(t => t.FullName).ToArray();
        Assert.Single(types, t => t == "Timberborn.WaterBuildings.WaterInput");
        Assert.Single(types, t => t == "Timberborn.WaterBuildings.WaterInputFixedCoordinates");
        Assert.Equal(marked ? 1 : 0, types.Count(t => t == "Wildfire.Timberborn.FireBell.TimberbornNaturalWaterSource"));
        Assert.DoesNotContain("Timberborn.WaterBuildings.WaterInputPipeCoordinates", types);
        Assert.True(json.RootElement.GetProperty("BlockObjectSpec").GetProperty("Entrance").GetProperty("HasEntrance").ValueKind == JsonValueKind.False);
        Assert.False(json.RootElement.TryGetProperty("PlaceableBlockObjectSpec", out _));
    }

    [Fact]
    public void MissingProcessOptInRejectsBeforeAnyNativeFactoryOrTickAccess()
    {
        using var native = new NativeManagedTestContext();
        var type = native.LoadMod().GetType("Wildfire.Timberborn.FireBell.NaturalWaterSourceQaFactory")!;
        var grid = native.LoadNative("UnityEngine.CoreModule").GetType("UnityEngine.Vector3Int")!;
        var vector = native.LoadNative("UnityEngine.CoreModule").GetType("UnityEngine.Vector3")!;
        var exception = Assert.Throws<TargetInvocationException>(() => type.GetMethod("Create", Private)!.Invoke(
            RuntimeHelpers.GetUninitializedObject(type), [Activator.CreateInstance(grid), Activator.CreateInstance(vector)]));
        Assert.Equal("qa_mutations_disabled", Assert.IsType<InvalidOperationException>(exception.InnerException).Message);
    }

    [Fact]
    public void ActualBinditoSelectsEachSoleInternalConstructorWithoutProviderShim()
    {
        using var native = new NativeManagedTestContext();
        var retrieverType = native.LoadNative("Bindito.Core").GetType("Bindito.Core.Internal.ConstructorRetriever")!;
        var retriever = Activator.CreateInstance(retrieverType)!;
        var mod = native.LoadMod();
        foreach (string name in new[] { "NaturalWaterSourceQaFactory", "NaturalWaterSourceShore", "TimberbornNaturalWaterSource", "TimberbornWaterCreditBoundary" })
        {
            var type = mod.GetType("Wildfire.Timberborn.FireBell." + name)!;
            var selected = Assert.IsAssignableFrom<ConstructorInfo>(retrieverType.GetMethod("GetEligibleConstructor")!.Invoke(retriever, [type]));
            Assert.Equal(Assert.Single(type.GetConstructors(Private)), selected);
            Assert.True(selected.IsAssembly);
        }
    }

    [Fact]
    public void ActualNativeBuilderRetainsGeneratedIdentityAcrossFactoryPreparation()
    {
        using var native = new NativeManagedTestContext();
        var type = native.LoadNative("Timberborn.EntitySystem").GetType("Timberborn.EntitySystem.EntitySetup+Builder")!;
        var builder = Activator.CreateInstance(type, new object?[] { null })!;
        object Build() => type.GetMethod("Build")!.Invoke(builder, null)!;
        Guid Id(object setup) => (Guid)setup.GetType().GetProperty("Id")!.GetValue(setup)!;
        var first = Build();
        type.GetMethod("AddInitComponent")!.Invoke(builder, [new object()]);
        var second = Build();
        Assert.NotEqual(Guid.Empty, Id(first));
        Assert.Equal(Id(first), Id(second));
    }

    private static string BlueprintPath()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var path = Path.Combine(d.FullName, "src/Wildfire.Timberborn/Data/NaturalResources/WildfireNaturalWaterSource/NaturalWaterSource.QA.blueprint.json");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("QA source blueprint missing.");
    }
}
