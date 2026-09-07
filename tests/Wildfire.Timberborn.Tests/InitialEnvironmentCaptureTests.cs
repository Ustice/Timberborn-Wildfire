using System.Reflection;
using System.Runtime.CompilerServices;
using Wildfire.Core;
using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Tests;

public sealed class InitialEnvironmentCaptureTests
{
    [Fact]
    public void ActualNativeStackedColumnMethodsPreserveDifferentSurfaceSoilAndSolidGeometry()
    {
        var fixture = new NativeEnvironmentFixture();
        var capture = fixture.Capture();
        Assert.Equal(new[] { 0, 3 }, capture.SolidVoxelIndices);
        Assert.Equal(new[] { 1, 4 }, capture.SoilSurfaces.Select(sample => sample.CellIndex));
        Assert.Equal(new[] { 4f, 12f }, capture.SoilSurfaces.Select(sample => sample.Moisture));
        Assert.Equal(new[] { 0f, .6f }, capture.SoilSurfaces.Select(sample => sample.Contamination));
        Assert.False(capture.SoilSurfaces[0].IsContaminated);
        Assert.True(capture.SoilSurfaces[1].IsContaminated);
        Assert.Equal(new[] { .25f, 1.5f }, capture.WaterColumns.Select(column => column.Depth));
        Assert.Equal(new[] { 0f, .8f }, capture.WaterColumns.Select(column => column.Contamination));
        Assert.Equal(new[] { 1, 4 }, capture.WaterColumns.Select(column => column.Floor));
        Assert.Equal(255, capture.WaterColumns[1].Ceiling); // Open native column ceiling is not a FireGrid cell.
    }

    [Fact]
    public void MissingElevatedColumnDoesNotFallBackToFirstColumnOrDrySoil()
    {
        var fixture = new NativeEnvironmentFixture();
        fixture.TerrainColumnCounts[4] = 1;
        var failure = Assert.Throws<TargetInvocationException>(() => fixture.Capture());
        Assert.IsType<InvalidOperationException>(failure.InnerException);
        Assert.Contains("no settled terrain-column index", failure.InnerException!.Message);
    }

    [Fact]
    public void CapturedViewsAreCopiedAndRevalidationRejectsAnyChangedEnvironment()
    {
        var fixture = new NativeEnvironmentFixture();
        var capture = fixture.Capture();
        fixture.Moisture[13] = 9;
        Assert.Equal(12, capture.SoilSurfaces[1].Moisture);
        var failure = Assert.Throws<TargetInvocationException>(() => fixture.RequireUnchanged(capture));
        Assert.Contains("changed during", failure.InnerException!.Message);
        fixture.Moisture[13] = 12;
        fixture.RequireUnchanged(capture);
        fixture.SolidVoxels[31] = false;
        Assert.Throws<TargetInvocationException>(() => fixture.RequireUnchanged(capture));
    }

    [Fact]
    public void UnsettledNativeTickAndInvalidQuantitiesRejectCapture()
    {
        var fixture = new NativeEnvironmentFixture { TickFinished = false };
        Assert.Throws<TargetInvocationException>(() => fixture.Capture());
        fixture.TickFinished = true;
        fixture.Moisture[13] = float.NaN;
        Assert.Throws<TargetInvocationException>(() => fixture.Capture());
    }

    [Fact]
    public void NativeWaterDoesNotRequireSourceObjectsAndZeroDepthIsNotMissingData()
    {
        var fixture = new NativeEnvironmentFixture();
        fixture.SetWaterDepth(13, 0);
        var capture = fixture.Capture();
        var world = new TimberbornInitialWorldCapture(capture.Grid, [], [], [], capture);
        Assert.Empty(world.WaterSources);
        Assert.Equal(2, world.Environment.WaterColumns.Count);
        Assert.Equal(0, world.Environment.WaterColumns[1].Depth);
        fixture.SetWaterDepth(13, 2);
        Assert.Throws<TargetInvocationException>(() => fixture.RequireUnchanged(capture));
    }

    [Fact]
    public void MalformedEnvironmentCannotBeCombinedWithBodyCapture()
    {
        var grid = new FireGrid(1, 1, 6);
        Assert.Throws<ArgumentException>(() => new TimberbornInitialEnvironmentCapture(grid, [0, 0], [], []));
        Assert.Throws<ArgumentException>(() => new TimberbornInitialEnvironmentCapture(grid, [0], [new(0, 1, 0, true, false)], []));
        Assert.Throws<ArgumentException>(() => new TimberbornInitialEnvironmentCapture(grid, [], [], [new(0, 0, 1, 3, 0, 0, 0), new(0, 0, 2, 4, 0, 0, 0)]));
        Assert.Throws<ArgumentException>(() => new TimberbornInitialWorldCapture(new(2, 1, 6), [], [], [], new(grid, [], [], [])));
    }
}

/// <summary>Actual installed map/column/soil/water getters; only the tick boundary is a proxy. No GameObjects.</summary>
internal sealed class NativeEnvironmentFixture
{
    private static readonly NativeManagedTestContext Native = new(collectible: false);
    private readonly object _provider, _water;
    private readonly Type _providerType;
    private readonly Dictionary<TimberbornInitialEnvironmentCapture, object> _captures = new();
    internal readonly byte[] TerrainColumnCounts = new byte[9];
    internal readonly float[] Moisture = new float[18];
    internal readonly bool[] SolidVoxels = new bool[54];
    internal bool TickFinished = true;

    internal NativeEnvironmentFixture()
    {
        var vector = Type("UnityEngine.CoreModule", "UnityEngine.Vector3Int");
        var size = New("Timberborn.MapStateSystem", "Timberborn.MapStateSystem.MapSize");
        Set(size, "<TerrainSize>k__BackingField", Activator.CreateInstance(vector, 1, 1, 6)!);
        Set(size, "<TerrainSize2D>k__BackingField", Activator.CreateInstance(Type("UnityEngine.CoreModule", "UnityEngine.Vector2Int"), 1, 1)!);
        var indices = New("Timberborn.MapIndexSystem", "Timberborn.MapIndexSystem.MapIndexService");
        Set(indices, "_mapSize", size); Set(indices, "<Stride>k__BackingField", 3); Set(indices, "<VerticalStride>k__BackingField", 9);
        var terrainMap = New("Timberborn.TerrainSystem", "Timberborn.TerrainSystem.TerrainMap");
        Set(terrainMap, "_mapSize", size); Set(terrainMap, "_mapIndexService", indices); Set(terrainMap, "_terrainVoxels", SolidVoxels);
        SolidVoxels[4] = true; SolidVoxels[31] = true; // Native margin index4; z3 adds3*9. FireGrid indices0/3.
        var terrain = New("Timberborn.TerrainSystem", "Timberborn.TerrainSystem.TerrainService");
        Set(terrain, "_mapSize", size); Set(terrain, "_terrainMap", terrainMap);
        var columns = New("Timberborn.TerrainSystem", "Timberborn.TerrainSystem.ThreadSafeColumnTerrainMap");
        Set(columns, "_verticalStride", 9); Set(columns, "_columnCounts", TerrainColumnCounts); TerrainColumnCounts[4] = 2;
        var columnType = Type("Timberborn.TerrainSystem", "Timberborn.TerrainSystem.ReadOnlyTerrainColumn");
        var terrainColumns = Array.CreateInstance(columnType, 18);
        terrainColumns.SetValue(Activator.CreateInstance(columnType, 0, 1), 4);
        terrainColumns.SetValue(Activator.CreateInstance(columnType, 3, 4), 13);
        Set(columns, "_terrainColumns", terrainColumns);
        var moisture = New("Timberborn.SoilMoistureSystem", "Timberborn.SoilMoistureSystem.SoilMoistureService");
        Moisture[4] = 4; Moisture[13] = 12;
        Set(moisture, "_threadSafeMoistureLevels", Moisture); Set(moisture, "_mapIndexService", indices); Set(moisture, "_threadSafeColumnTerrainMap", columns);
        var contamination = New("Timberborn.SoilContaminationSystem", "Timberborn.SoilContaminationSystem.SoilContaminationService");
        var contaminationLevels = new float[18]; contaminationLevels[13] = .6f;
        Set(contamination, "_threadSafeContaminationLevels", contaminationLevels); Set(contamination, "_mapIndexService", indices); Set(contamination, "_threadSafeColumnTerrainMap", columns);
        _water = New("Timberborn.WaterSystem", "Timberborn.WaterSystem.ThreadSafeWaterMap");
        var counts = new byte[9]; counts[4] = 2;
        Set(_water, "_threadSafeColumnCounts", counts); Set(_water, "<MaxColumnCount>k__BackingField", 2);
        var waterType = Type("Timberborn.WaterSystem", "Timberborn.WaterSystem.ReadOnlyWaterColumn");
        var waters = Array.CreateInstance(waterType, 18);
        foreach (var (index, floor, ceiling, depth, taint) in new[] { (4, 1, 3, .25f, 0f), (13, 4, 255, 1.5f, .8f) })
        {
            var value = Activator.CreateInstance(waterType)!;
            Set(value, "<Floor>k__BackingField", (byte)floor); Set(value, "<Ceiling>k__BackingField", (byte)ceiling);
            Set(value, "<WaterDepth>k__BackingField", depth); Set(value, "<Contamination>k__BackingField", taint);
            waters.SetValue(value, index);
        }
        Set(_water, "_threadSafeWaterColumns", waters);
        var ticks = NativePersistenceProxy.Create(Type("Timberborn.TickSystem", "Timberborn.TickSystem.ITickableSingletonService"),
            (method, _) => method.Name switch { "get_ParalleTicklIsFinished" => TickFinished, "get_IsStartingParallelTick" => false, _ => throw new NotSupportedException(method.Name) });
        _providerType = Native.LoadMod().GetType("Wildfire.Timberborn.Runtime.TimberbornInitialEnvironmentCaptureProvider")!;
        _provider = Activator.CreateInstance(_providerType, terrain, indices, columns, moisture, contamination, _water, ticks)!;
    }
    internal TimberbornInitialEnvironmentCapture Capture()
    {
        var raw = _providerType.GetMethod("Capture")!.Invoke(_provider, [new FireGrid(1, 1, 6)])!;
        var soil = Values(raw, "SoilSurfaces").Select(v => new TimberbornSurfaceSoilSample(Get<int>(v, "CellIndex"), Get<float>(v, "Moisture"),
            Get<float>(v, "Contamination"), Get<bool>(v, "IsMoist"), Get<bool>(v, "IsContaminated")));
        var water = Values(raw, "WaterColumns").Select(v => new TimberbornWaterColumnSample(Get<int>(v, "X"), Get<int>(v, "Y"),
            Get<int>(v, "Floor"), Get<int>(v, "Ceiling"), Get<float>(v, "Depth"), Get<float>(v, "Contamination"), Get<float>(v, "Overflow")));
        var result = new TimberbornInitialEnvironmentCapture(new(1, 1, 6), Get<IEnumerable<int>>(raw, "SolidVoxelIndices"), soil, water);
        _captures.Add(result, raw); return result;
    }
    internal void RequireUnchanged(TimberbornInitialEnvironmentCapture capture) => _providerType.GetMethod("RequireUnchanged")!.Invoke(_provider, [_captures[capture]]);
    private static IEnumerable<object> Values(object target, string name) => Get<System.Collections.IEnumerable>(target, name).Cast<object>();
    private static T Get<T>(object target, string name) => (T)target.GetType().GetProperty(name)!.GetValue(target)!;
    internal void SetWaterDepth(int index, float depth)
    {
        var array = (Array)_water.GetType().GetField("_threadSafeWaterColumns", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_water)!;
        var value = array.GetValue(index)!; Set(value, "<WaterDepth>k__BackingField", depth); array.SetValue(value, index);
    }
    private static Type Type(string assembly, string name) => Native.LoadNative(assembly).GetType(name)!;
    private static object New(string assembly, string name) => RuntimeHelpers.GetUninitializedObject(Type(assembly, name));
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.SetValue(target, value);
}
