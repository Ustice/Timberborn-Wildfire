using System.Collections;
using System.Reflection;
using Wildfire.Core;
using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Tests;

// Existing real native map/column getters, with MapSize.Initialize establishing the actual two domains.
internal sealed class NativeOwnedWorldDomainFixture
{
    internal readonly NativeEnvironmentFixture Terrain = new();
    private readonly object _legacy, _owned, _map, _columns, _water;
    private static readonly NativeManagedTestContext Native = NativeEnvironmentFixture.Context;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    internal NativeOwnedWorldDomainFixture(int terrainHeight = 5, int airHeight = 4)
    {
        _legacy = Terrain.NativeProvider;
        _map = Field(Field(_legacy, "_indices"), "_mapSize");
        var spec = Activator.CreateInstance(T("Timberborn.MapStateSystem.MapSizeSpec"))!;
        Property(spec, "MaxGameTerrainHeight", terrainHeight); Property(spec, "MaxHeightAboveTerrain", airHeight);
        Set(_map, "_mapSizeSpec", spec);
        Call(_map, "Initialize", Activator.CreateInstance(T("UnityEngine.Vector2Int"), 1, 1)!);
        _columns = Field(_legacy, "_columns"); Set(_columns, "<MaxColumnCount>k__BackingField", 2); _water = Field(_legacy, "_water");
        _owned = _legacy.GetType().GetMethod("ForOwnedWorld")!.Invoke(null,
            [_map, Field(_legacy, "_terrain"), Field(_legacy, "_indices"), _columns, Field(_legacy, "_moisture"),
             Field(_legacy, "_contamination"), _water, Field(_legacy, "_ticks")])!;
    }
    internal object ReadDomain() => Native.LoadMod().GetType("Wildfire.Timberborn.Runtime.TimberbornNativeWorldDomain")!
        .GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [_map])!;
    internal FireGrid Grid(string name)
    {
        var value = Get(ReadDomain(), name);
        return new((int)Get(value, "Width"), (int)Get(value, "Height"), (int)Get(value, "Depth"));
    }
    internal object CaptureRaw(FireGrid grid) => Call(_owned, "Capture", grid)!;
    internal void RequireUnchanged(object capture) => Call(_owned, "RequireUnchanged", capture);
    internal TimberbornInitialEnvironmentCapture Capture(FireGrid grid)
    {
        var raw = CaptureRaw(grid);
        return TimberbornInitialEnvironmentCapture.ForOwnedDomain(new(Grid("WorldGrid"), Grid("TerrainGrid")),
            (IEnumerable<int>)Get(raw, "SolidVoxelIndices"),
            Values(raw, "SoilSurfaces").Select(v => new TimberbornSurfaceSoilSample((int)Get(v, "CellIndex"), (float)Get(v, "Moisture"),
                (float)Get(v, "Contamination"), (bool)Get(v, "IsMoist"), (bool)Get(v, "IsContaminated"))),
            Values(raw, "WaterColumns").Select(v => new TimberbornWaterColumnSample((int)Get(v, "X"), (int)Get(v, "Y"),
                (int)Get(v, "Floor"), (int)Get(v, "Ceiling"), (float)Get(v, "Depth"), (float)Get(v, "Contamination"), (float)Get(v, "Overflow"))));
    }
    internal void PutSoilAtTerrainTop()
    {
        Terrain.SolidVoxels[31] = false; Terrain.SolidVoxels[49] = true; // Move upper solid from z3 to z5.
        var values = (Array)Field(_columns, "_terrainColumns");
        values.SetValue(Activator.CreateInstance(T("Timberborn.TerrainSystem.ReadOnlyTerrainColumn"), 5, 6), 13);
        var water = (Array)Field(_water, "_threadSafeWaterColumns"); var column = water.GetValue(13)!;
        Set(column, "<Floor>k__BackingField", (byte)6); Set(column, "<WaterDepth>k__BackingField", 9f); water.SetValue(column, 13);
    }
    internal void AlterColumn(int floor, int ceiling) => ((Array)Field(_columns, "_terrainColumns"))
        .SetValue(Activator.CreateInstance(T("Timberborn.TerrainSystem.ReadOnlyTerrainColumn"), floor, ceiling), 13);
    internal void AlterColumnCount(byte count) => ((byte[])Field(_columns, "_columnCounts"))[4] = count;
    internal void TruncateColumns() => Set(_columns, "_terrainColumns", Array.CreateInstance(T("Timberborn.TerrainSystem.ReadOnlyTerrainColumn"), 5));
    internal void AlterTotal(int width, int depth) => Set(_map, "<TotalSize>k__BackingField", Activator.CreateInstance(T("UnityEngine.Vector3Int"), width, 1, depth)!);
    internal static Type T(string name)
    {
        string assembly = name.StartsWith("UnityEngine.", StringComparison.Ordinal) ? "UnityEngine.CoreModule" : name[..name.LastIndexOf('.')];
        return Native.LoadNative(assembly).GetType(name)!;
    }
    private static IEnumerable<object> Values(object value, string name) => ((IEnumerable)Get(value, name)).Cast<object>();
    private static object Call(object value, string name, params object[] args) => value.GetType().GetMethods(Flags)
        .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(value, args)!;
    private static object Get(object value, string name) => value.GetType().GetProperty(name, Flags)!.GetValue(value)!;
    private static object Field(object value, string name) => value.GetType().GetField(name, Flags)!.GetValue(value)!;
    private static void Set(object value, string name, object replacement) => value.GetType().GetField(name, Flags)!.SetValue(value, replacement);
    private static void Property(object value, string name, object replacement) => value.GetType().GetProperty(name, Flags)!.SetValue(value, replacement);
}
