using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeWardenPreviewAccessTests
{
    [Fact]
    public void PreviewMatchesNativeEntranceAndFinishedAccessAcrossPlacementChanges()
    {
        using var f = new Fixture();
        int cases = 0, staleLists = 0;
        foreach (var origin in new[] { f.Grid(10, 20, 3), f.Grid(21, 7, 12) })
        foreach (var rotation in Enum.GetValues(f.Orientation))
        foreach (bool mirrored in new[] { false, true })
        {
            f.PlacePreview(origin, rotation, mirrored);
            var calculated = Call(f.Building, "CalculateAccess");
            Call(f.Building, "OnPostPlacementChanged");
            Assert.Equal(calculated, f.RangeAnchor());
            Assert.Equal(calculated, f.EntranceCenter());
            var prior = f.Accesses();
            if (prior.Length == 1 && !prior[0].Equals(calculated)) staleLists++;

            Call(f.Building, "OnEnterFinishedState");
            var actual = Assert.Single(f.Accesses());
            Assert.Equal(calculated, actual);
            f.MarkFinished();
            Assert.Equal(actual, f.RangeAnchor());
            // Selection must use published access, even if a later calculation would differ.
            var original = Get(f.Spec, "LocalAccess");
            Set(f.Spec, "<LocalAccess>k__BackingField", f.World(99, 0, 99));
            Assert.Equal(actual, f.RangeAnchor());
            Set(f.Spec, "<LocalAccess>k__BackingField", original);
            cases++;
        }
        Assert.Equal(16, cases);
        Assert.Equal(7, staleLists);
        Assert.False(f.ShippingFlippable); // Mirrored cases exercise native math, not a newly enabled feature.
    }

    /// <summary>Real native transforms/access publication with supplied managed backing state; no GameObjects.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly NativeManagedTestContext _native = new();
        private readonly object _blocks, _block, _entrance, _state, _range, _station, _accessible;
        internal readonly object Building, Spec;
        internal readonly Type Orientation;
        internal readonly bool ShippingFlippable;
        private readonly MethodInfo _centered;

        internal Fixture()
        {
            using var document = JsonDocument.Parse(File.ReadAllText(FindStationBlueprint()));
            var blockData = document.RootElement.GetProperty("BlockObjectSpec");
            var accessData = document.RootElement.GetProperty("BuildingAccessibleSpec");
            ShippingFlippable = blockData.GetProperty("Flippable").GetBoolean();
            _blocks = Blank("Timberborn.BlockSystem", "Blocks");
            Set(_blocks, "<Size>k__BackingField", GridFrom(blockData.GetProperty("Size")));
            Set(_blocks, "_all", typeof(System.Collections.Immutable.ImmutableArray<>)
                .MakeGenericType(T("Timberborn.BlockSystem", "Block")).GetField("Empty")!.GetValue(null)!);
            _block = Blank("Timberborn.BlockSystem", "BlockObject");
            Set(_block, "_blocks", _blocks);
            Spec = New("Timberborn.Buildings", "BuildingAccessibleSpec");
            var local = accessData.GetProperty("LocalAccess");
            Set(Spec, "<LocalAccess>k__BackingField", World(local.GetProperty("X").GetSingle(),
                local.GetProperty("Y").GetSingle(), local.GetProperty("Z").GetSingle()));
            Set(Spec, "<ForceOneFinalAccess>k__BackingField", accessData.GetProperty("ForceOneFinalAccess").GetBoolean());
            Building = New("Timberborn.Buildings", "BuildingAccessible");
            Set(Building, "_blockObject", _block);
            Set(Building, "_buildingAccessibleSpec", Spec);
            _accessible = Activator.CreateInstance(T("Timberborn.Navigation", "Accessible"), new object?[] { null })!;
            AttachAccessibleCache();
            Call(Building, "SetAccessible", _accessible);
            _entrance = New("Timberborn.BlockSystem", "EntranceBlockSpec");
            var entrance = blockData.GetProperty("Entrance");
            Set(_entrance, "<HasEntrance>k__BackingField", entrance.GetProperty("HasEntrance").GetBoolean());
            Set(_entrance, "<Coordinates>k__BackingField", GridFrom(entrance.GetProperty("Coordinates")));
            Orientation = T("Timberborn.Coordinates", "Orientation");
            _centered = Orientation.Assembly.GetTypes().SelectMany(t => t.GetMethods(Flags)).Single(m =>
                m.Name == "GridToWorldCentered" && m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType == Grid(0, 0, 0).GetType());
            _state = Blank("Timberborn.BlockSystem", "BlockObjectState");
            Set(_block, "_blockObjectState", _state);
            var mod = _native.LoadMod();
            _range = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.FireResponse.WardenStationRange")!, new object?[] { null, null })!;
            _station = RuntimeHelpers.GetUninitializedObject(mod.GetType("Wildfire.Timberborn.FireResponse.WardenStation")!);
            Set(_station, "<Access>k__BackingField", _accessible);
            Set(_range, "_blockObject", _block);
            Set(_range, "_buildingAccessible", Building);
            Set(_range, "_station", _station);
        }

        internal void PlacePreview(object origin, object rotation, bool mirrored)
        {
            Set(_block, "<Coordinates>k__BackingField", origin);
            Set(_block, "<Orientation>k__BackingField", rotation);
            Set(_block, "<FlipMode>k__BackingField", Activator.CreateInstance(T("Timberborn.Coordinates", "FlipMode"), mirrored)!);
            SetState("Preview");
            Set(_block, "<PositionedBlocks>k__BackingField", T("Timberborn.BlockSystem", "PositionedBlocks")
                .GetMethod("From")!.Invoke(null, [_blocks, Get(_block, "Placement")])!);
        }
        internal void MarkFinished() { SetState("Finished"); Set(_station, "_finished", true); }
        internal object[] Accesses() => ((IEnumerable)Get(_accessible, "Accesses")).Cast<object>().ToArray();
        internal object RangeAnchor()
        {
            object?[] args = [null];
            Assert.True((bool)_range.GetType().GetMethod("TryGetAnchor", Flags)!.Invoke(_range, args)!);
            return args[0]!;
        }
        internal object EntranceCenter()
        {
            var placed = T("Timberborn.BlockSystem", "PositionedEntrance").GetMethod("From")!
                .Invoke(null, [_blocks, _entrance, Get(_block, "Placement")])!;
            return _centered.Invoke(null, [Get(placed, "Coordinates")])!;
        }
        private void AttachAccessibleCache()
        {
            var cache = Blank("Timberborn.BaseComponentSystem", "ComponentCache");
            var map = New("Timberborn.BaseComponentSystem", "TypeIndexMap");
            var components = new List<object> { _accessible };
            var readOnly = Activator.CreateInstance(T("Timberborn.Common", "ReadOnlyList`1").MakeGenericType(typeof(object)),
                Flags, null, [components], null)!;
            map.GetType().GetMethod("CacheType")!.MakeGenericMethod(T("Timberborn.Navigation", "IAccessibleValidator"))
                .Invoke(map, [readOnly]);
            Set(cache, "_components", components);
            Set(cache, "_typeIndexMap", map);
            var baseType = T("Timberborn.BaseComponentSystem", "BaseComponent");
            baseType.GetField("_componentCache", Flags)!.SetValue(_accessible, cache);
            baseType.GetField("<Enabled>k__BackingField", Flags)!.SetValue(_accessible, true);
        }
        private void SetState(string state) => Set(_state, "_state", Enum.Parse(_state.GetType().GetField("_state", Flags)!.FieldType, state));
        private Type T(string assembly, string type) => _native.LoadNative(assembly).GetType(assembly + "." + type)!;
        private object Blank(string assembly, string type) => RuntimeHelpers.GetUninitializedObject(T(assembly, type));
        private object New(string assembly, string type) => Activator.CreateInstance(T(assembly, type))!;
        private object GridFrom(JsonElement value) => Grid(value.GetProperty("X").GetInt32(), value.GetProperty("Y").GetInt32(), value.GetProperty("Z").GetInt32());
        internal object Grid(int x, int y, int z) => Activator.CreateInstance(_native.LoadNative("UnityEngine.CoreModule").GetType("UnityEngine.Vector3Int")!, x, y, z)!;
        internal object World(float x, float y, float z) => Activator.CreateInstance(_native.LoadNative("UnityEngine.CoreModule").GetType("UnityEngine.Vector3")!, x, y, z)!;
        public void Dispose() => _native.Dispose();
    }

    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Flags)!.SetValue(owner, value);
    private static object Get(object owner, string property) => owner.GetType().GetProperty(property, Flags)!.GetValue(owner)!;
    private static object Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Flags)!.Invoke(owner, args)!;
    private static string FindStationBlueprint()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, "src/Wildfire.Timberborn/Data/Buildings/FireResponse/WardenStation.IronTeeth.blueprint.json");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Station blueprint not found.");
    }
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
}
