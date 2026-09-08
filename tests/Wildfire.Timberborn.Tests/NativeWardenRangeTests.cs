using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeWardenRangeTests
{
    [Theory]
    [InlineData(20, 0, 3, true)]
    [InlineData(21, 0, 3, false)]
    [InlineData(0, 0, 23, true)]
    [InlineData(0, 0, 24, false)]
    [InlineData(12, 16, 3, true)]
    [InlineData(12, 16, 4, false)]
    public void SurfaceCentersMatchSelectorSphereWithoutHalfTileOrHeightShift(int x, int y, int z, bool expected)
    {
        using var f = new Fixture();
        var actual = f.Range.GetMethod("Contains", Static)!.Invoke(null, [f.Vector(.5f, 3, .5f), f.Grid(x, y, z)]);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CandidateColumnsStayInMapAndDoNotOmitAnyInRangeSurface()
    {
        using var f = new Fixture();
        var access = f.Vector(1.5f, 7, 3.5f);
        var columns = ((IEnumerable)f.Range.GetMethod("Columns", Static)!.Invoke(null, [access, f.Grid(32, 27, 33)])!)
            .Cast<object>().Select(v => (f.Int(v, "x"), f.Int(v, "y"))).ToHashSet();
        Assert.All(columns, v => { Assert.InRange(v.Item1, 0, 31); Assert.InRange(v.Item2, 0, 26); });
        for (int y = 0; y < 27; y++)
        for (int x = 0; x < 32; x++)
            if ((bool)f.Range.GetMethod("Contains", Static)!.Invoke(null, [access, f.Grid(x, y, 7)])!)
                Assert.Contains((x, y), columns);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void UnpositionedPreviewUnfinishedAndMissingAccessNeverQuerySurfaceServices(bool preview, bool finished)
    {
        using var f = new Fixture();
        var state = RuntimeHelpers.GetUninitializedObject(f.T("Timberborn.BlockSystem", "BlockObjectState"));
        var stateField = state.GetType().GetField("_state", Instance)!;
        stateField.SetValue(state, Enum.Parse(stateField.FieldType, preview ? "Preview" : "Unfinished"));
        var block = RuntimeHelpers.GetUninitializedObject(f.T("Timberborn.BlockSystem", "BlockObject"));
        block.GetType().GetField("_blockObjectState", Instance)!.SetValue(block, state);
        var stationType = f.Mod.GetType("Wildfire.Timberborn.FireResponse.WardenStation")!;
        var station = RuntimeHelpers.GetUninitializedObject(stationType);
        stationType.GetField("_finished", Instance)!.SetValue(station, finished);
        if (finished)
            stationType.GetProperty("Access")!.SetValue(station,
                Activator.CreateInstance(f.T("Timberborn.Navigation", "Accessible"), new object?[] { null }));
        var range = Activator.CreateInstance(f.Range, new object?[] { null, null })!;
        f.Range.GetField("_blockObject", Instance)!.SetValue(range, block);
        f.Range.GetField("_station", Instance)!.SetValue(range, station);
        Assert.Empty(((IEnumerable)f.Range.GetMethod("GetBlocksInRange")!.Invoke(range, null)!).Cast<object>());
    }

    [Fact]
    public void ActualNativeGroundCoordinatesAreAboveSolidVoxels()
    {
        var f = new NativeEnvironmentFixture();
        var terrain = f.NativeProvider.GetType().GetField("_terrain", Instance)!.GetValue(f.NativeProvider)!;
        var vector = NativeEnvironmentFixture.Context.LoadNative("UnityEngine.CoreModule").GetType("UnityEngine.Vector3Int")!;
        foreach (var (z, expected) in new[] { (0, false), (1, true), (3, false), (4, true) })
            Assert.Equal(expected, terrain.GetType().GetMethod("OnGround")!.Invoke(terrain,
                [Activator.CreateInstance(vector, 0, 0, z)]));
    }

    [Fact]
    public void NativeRangeModuleFindsOneStationRangeAndSuppliesItsOwnPreviewUpdater()
    {
        using var f = new Fixture();
        var providerType = f.Mod.GetType("Wildfire.Timberborn.Runtime.WildfireConfigurator+WardenTemplateModuleProvider")!;
        var provider = Activator.CreateInstance(providerType, Instance, null, new object?[] { null, null }, null)!;
        var nativeUi = RuntimeHelpers.GetUninitializedObject(f.T("Timberborn.RangedEffectBuildingUI", "RangedEffectBuildingUIConfigurator"));
        var modules = Array.CreateInstance(f.T("Timberborn.TemplateInstantiation", "TemplateModule"), 2);
        modules.SetValue(providerType.GetMethod("Get")!.Invoke(provider, null), 0);
        modules.SetValue(nativeUi.GetType().GetMethod("ProvideTemplateModule", Static)!.Invoke(null, null), 1);
        var factory = Activator.CreateInstance(f.T("Timberborn.TemplateInstantiation", "TemplateInstantiatorProvider"), null, null, modules)!;
        var instantiator = factory.GetType().GetMethod("Get")!.Invoke(factory, null)!;
        var specs = Array.CreateInstance(f.T("Timberborn.BlueprintSystem", "ComponentSpec"), 1);
        specs.SetValue(Activator.CreateInstance(f.Mod.GetType("Wildfire.Timberborn.FireResponse.WildfireWardenStationSpec")!), 0);
        var blueprintType = f.T("Timberborn.BlueprintSystem", "Blueprint");
        var children = typeof(System.Collections.Immutable.ImmutableArray<>).MakeGenericType(blueprintType).GetField("Empty")!.GetValue(null)!;
        var blueprint = Activator.CreateInstance(blueprintType, "Wildfire.WardenStation.IronTeeth", specs, children)!;
        object?[] args = [blueprint, null, null];
        instantiator.GetType().GetMethod("GetInstanceComponents", Instance)!.Invoke(instantiator, args);
        var types = ((IEnumerable)args[2]!).Cast<Type>().ToArray();
        var contract = f.T("Timberborn.BuildingRange", "IBuildingWithRange");
        Assert.Single(types, contract.IsAssignableFrom);
        Assert.Contains(f.Range, types);
        Assert.Single(types, t => t == f.T("Timberborn.RangedEffectBuildingUI", "BuildingWithRangePreviewUpdater"));
        var range = Activator.CreateInstance(f.Range, new object?[] { null, null })!;
        Assert.Empty(((IEnumerable)f.Range.GetMethod("GetObjectsInRange")!.Invoke(range, null)!).Cast<object>());
        Assert.Equal("Wildfire.WardenStation.Response", f.Range.GetProperty("RangeName")!.GetValue(range));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly NativeManagedTestContext _native = new();
        internal readonly Assembly Mod;
        internal readonly Type Range;
        internal Fixture() { Mod = _native.LoadMod(); Range = Mod.GetType("Wildfire.Timberborn.FireResponse.WardenStationRange")!; }
        internal Type T(string assembly, string name) => _native.LoadNative(assembly).GetType(assembly + "." + name)!;
        internal object Vector(float x, float y, float z) => Activator.CreateInstance(_native.LoadNative("UnityEngine.CoreModule").GetType("UnityEngine.Vector3")!, x, y, z)!;
        internal object Grid(int x, int y, int z) => Activator.CreateInstance(_native.LoadNative("UnityEngine.CoreModule").GetType("UnityEngine.Vector3Int")!, x, y, z)!;
        internal int Int(object v, string property) => (int)v.GetType().GetProperty(property)!.GetValue(v)!;
        public void Dispose() => _native.Dispose();
    }
    private const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;
}
