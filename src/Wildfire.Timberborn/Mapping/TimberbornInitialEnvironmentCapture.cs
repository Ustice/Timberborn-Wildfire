using Wildfire.Core;

namespace Wildfire.Timberborn.Mapping;

public sealed record TimberbornSurfaceSoilSample(int CellIndex, float Moisture, float Contamination,
    bool IsMoist, bool IsContaminated);
public sealed record TimberbornWaterColumnSample(int X, int Y, int Floor, int Ceiling,
    float Depth, float Contamination, float Overflow);

/// <summary>Copied native facts only. Solid voxels are NOT the simulator's packed Terrain domain.</summary>
public sealed class TimberbornInitialEnvironmentCapture
{
    public TimberbornInitialEnvironmentCapture(FireGrid grid, IEnumerable<int> solidVoxelIndices,
        IEnumerable<TimberbornSurfaceSoilSample> soilSurfaces, IEnumerable<TimberbornWaterColumnSample> waterColumns)
    {
        if (grid.Width <= 0 || grid.Height <= 0 || grid.Depth <= 0) throw new ArgumentOutOfRangeException(nameof(grid));
        _ = checked(grid.Width * grid.Height * grid.Depth);
        var solid = solidVoxelIndices.OrderBy(cell => cell).ToArray();
        var soil = soilSurfaces.OrderBy(sample => sample.CellIndex).ToArray();
        var water = waterColumns.OrderBy(column => column.Y).ThenBy(column => column.X).ThenBy(column => column.Floor).ToArray();
        if (solid.Distinct().Count() != solid.Length || soil.Select(sample => sample.CellIndex).Distinct().Count() != soil.Length)
            throw new ArgumentException("Duplicate native geometry or surface identity.");
        foreach (int cell in solid.Concat(soil.Select(sample => sample.CellIndex))) grid.FromIndex(cell);
        var solidSet = solid.ToHashSet();
        if (soil.Any(sample => solidSet.Contains(sample.CellIndex) || sample.CellIndex < grid.Width * grid.Height ||
                !solidSet.Contains(sample.CellIndex - grid.Width * grid.Height) || !NonnegativeFinite(sample.Moisture) ||
                !NonnegativeFinite(sample.Contamination) || sample.IsMoist != (sample.Moisture > 0) ||
                sample.IsContaminated != (sample.Contamination > 0)))
            throw new ArgumentException("Surface soil must be finite and located above solid native terrain, not in it.");
        foreach (var group in water.GroupBy(column => (column.X, column.Y)))
        {
            int previousCeiling = -1;
            foreach (var column in group)
            {
                if (column.X < 0 || column.X >= grid.Width || column.Y < 0 || column.Y >= grid.Height ||
                    column.Floor < 0 || column.Ceiling <= column.Floor || column.Ceiling > byte.MaxValue || column.Floor < previousCeiling ||
                    !NonnegativeFinite(column.Depth) || !NonnegativeFinite(column.Contamination) || !NonnegativeFinite(column.Overflow))
                    throw new ArgumentException("Malformed native water column.");
                previousCeiling = column.Ceiling;
            }
        }
        Grid = grid; SolidVoxelIndices = Array.AsReadOnly(solid); SoilSurfaces = Array.AsReadOnly(soil); WaterColumns = Array.AsReadOnly(water);
    }

    public FireGrid Grid { get; }
    public IReadOnlyList<int> SolidVoxelIndices { get; }
    public IReadOnlyList<TimberbornSurfaceSoilSample> SoilSurfaces { get; }
    public IReadOnlyList<TimberbornWaterColumnSample> WaterColumns { get; }
    internal bool SameReadings(TimberbornInitialEnvironmentCapture other) => Grid == other.Grid &&
        SolidVoxelIndices.SequenceEqual(other.SolidVoxelIndices) && SoilSurfaces.SequenceEqual(other.SoilSurfaces) && WaterColumns.SequenceEqual(other.WaterColumns);
    private static bool NonnegativeFinite(float value) => value >= 0 && !float.IsInfinity(value) && !float.IsNaN(value);
}
