namespace Wildfire.Timberborn.Tests;

// Actual native padded map-index and column-ceiling lookup, with supplied column storage.
internal sealed class NativeAshGrowthSoilFixture
{
    internal readonly object Indices, Columns;
    internal readonly Array Values;
    internal readonly byte[] Counts = new byte[9];
    private readonly NativeAshGrowthFixture _f;
    internal NativeAshGrowthSoilFixture(NativeAshGrowthFixture f)
    {
        _f = f;
        Indices = f.New("Timberborn.MapIndexSystem", "MapIndexService");
        NativeAshGrowthFixture.Set(Indices, "<Stride>k__BackingField", 3);
        NativeAshGrowthFixture.Set(Indices, "<VerticalStride>k__BackingField", 9);
        Columns = f.New("Timberborn.TerrainSystem", "ThreadSafeColumnTerrainMap");
        Values = Array.CreateInstance(f.T("Timberborn.TerrainSystem", "ReadOnlyTerrainColumn"), 18);
        Counts[4] = 2;
        NativeAshGrowthFixture.Set(Columns, "_verticalStride", 9);
        NativeAshGrowthFixture.Set(Columns, "_columnCounts", Counts);
        NativeAshGrowthFixture.Set(Columns, "_terrainColumns", Values);
        SetColumn(4, 0, 1); SetColumn(13, 2, 3);
    }
    internal void SetColumn(int index, int floor, int ceiling) => Values.SetValue(
        Activator.CreateInstance(_f.T("Timberborn.TerrainSystem", "ReadOnlyTerrainColumn"), floor, ceiling), index);
}
