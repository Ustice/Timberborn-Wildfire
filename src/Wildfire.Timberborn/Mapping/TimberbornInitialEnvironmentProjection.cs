using Wildfire.Core;

namespace Wildfire.Timberborn.Mapping;

public readonly record struct TimberbornInitialEnvironmentFields(byte Wetness, byte SoilContamination);
public readonly record struct TimberbornInitialLiquidContact(int CellIndex, int ColumnFloor, int ColumnCeiling,
    double OverlapDepth, float Contamination);

/// <summary>
/// Fixed initial mapping of copied native facts. Baseline material survives contributor changes;
/// initial fields are applied once at formation, never replayed when a contributor is removed.
/// </summary>
public sealed class TimberbornInitialEnvironmentProjection
{
    private const uint SoilMask = 7u << 25;

    private TimberbornInitialEnvironmentProjection(TimberbornInitialEnvironmentCapture capture,
        TimberbornMaterialBaseline baseline, TimberbornInitialEnvironmentFields[] fields,
        TimberbornInitialLiquidContact[] contacts)
    {
        SourceCapture = capture;
        MaterialBaseline = baseline;
        InitialFields = Array.AsReadOnly(fields);
        LiquidContacts = Array.AsReadOnly(contacts);
    }

    public TimberbornInitialEnvironmentCapture SourceCapture { get; }
    public TimberbornMaterialBaseline MaterialBaseline { get; }
    public IReadOnlyList<TimberbornInitialEnvironmentFields> InitialFields { get; }
    public IReadOnlyList<TimberbornInitialLiquidContact> LiquidContacts { get; }

    public static TimberbornInitialEnvironmentProjection Project(TimberbornInitialEnvironmentCapture capture)
    {
        if (capture is null) throw new ArgumentNullException(nameof(capture));
        var grid = capture.Grid;
        var solid = capture.SolidVoxelIndices.ToHashSet();
        var soil = capture.SoilSurfaces.Select(sample => sample.CellIndex).ToHashSet();
        var baseline = solid.ToDictionary(cell => cell, _ => FireSimBaselineDefinition.SolidTerrain);
        var fields = new TimberbornInitialEnvironmentFields[grid.CellCount];
        var contacts = new List<TimberbornInitialLiquidContact>();

        foreach (var sample in capture.SoilSurfaces)
        {
            baseline.Add(sample.CellIndex, FireSimBaselineDefinition.OpenSoil);
            fields[sample.CellIndex] = new(TimberbornTerrainAdapter.QuantizeSoilMoisture(sample.Moisture),
                TimberbornTerrainAdapter.QuantizeSoilContamination(sample.Contamination, sample.IsContaminated));
        }

        foreach (var column in capture.WaterColumns)
        {
            int end = Math.Min(column.Ceiling, grid.Depth);
            // Work relative to the floor: adding float.Epsilon to an elevated floor loses real liquid.
            double depth = Math.Min((double)column.Depth, end - column.Floor);
            for (int z = column.Floor; z < end; z++)
            {
                double overlap = Math.Min(1d, depth - (z - column.Floor));
                if (overlap <= 0) break;
                int cell = grid.ToIndex(column.X, column.Y, z);
                if (solid.Contains(cell))
                    throw new ArgumentException("Positive native liquid overlaps solid geometry.", nameof(capture));
                contacts.Add(new(cell, column.Floor, column.Ceiling, overlap, column.Contamination));
                fields[cell] = fields[cell] with { Wetness = 3 };
                if (!soil.Contains(cell))
                    baseline[cell] = column.Contamination > 0
                        ? FireSimBaselineDefinition.Badwater : FireSimBaselineDefinition.Water;
            }
        }

        return new(capture, new TimberbornMaterialBaseline(grid, baseline), fields,
            contacts.OrderBy(contact => contact.CellIndex).ToArray());
    }

    /// <summary>Copy initial ambient fields onto resolved material without changing material or ownership.</summary>
    public (ushort[] Cells, uint[] CompanionFields) OverlayInitialFields(FireGrid grid,
        ReadOnlySpan<ushort> materialCells, ReadOnlySpan<uint> companionFields)
    {
        if (grid != MaterialBaseline.Grid || materialCells.Length != grid.CellCount || companionFields.Length != grid.CellCount)
            throw new ArgumentException("Initial environment and both material arrays must describe the exact same grid.");
        var cells = materialCells.ToArray();
        var companions = companionFields.ToArray();
        for (int cell = 0; cell < cells.Length; cell++)
        {
            cells[cell] = PackedCell.SetWater(cells[cell], InitialFields[cell].Wetness);
            companions[cell] = (companions[cell] & ~SoilMask) | ((uint)InitialFields[cell].SoilContamination << 25);
        }
        return (cells, companions);
    }
}
