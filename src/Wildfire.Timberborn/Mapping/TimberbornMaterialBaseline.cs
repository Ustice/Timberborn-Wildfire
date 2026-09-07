using Wildfire.Core;

namespace Wildfire.Timberborn.Mapping;

/// <summary>
/// Desired unowned material underneath native contributors. This contains no initial or current
/// wetness, heat, ash, stock or burn history. Environmental field updates have a separate boundary.
/// </summary>
public sealed class TimberbornMaterialBaseline
{
    private readonly IReadOnlyDictionary<int, FireSimBaselineDefinition> _cells;

    public TimberbornMaterialBaseline(FireGrid grid, IEnumerable<KeyValuePair<int, FireSimBaselineDefinition>> cells)
    {
        if (grid.Width <= 0 || grid.Height <= 0 || grid.Depth <= 0)
            throw new ArgumentOutOfRangeException(nameof(grid));
        _ = checked(grid.Width * grid.Height * grid.Depth);
        if (cells is null) throw new ArgumentNullException(nameof(cells));
        var copied = new Dictionary<int, FireSimBaselineDefinition>();
        foreach (var entry in cells)
        {
            grid.FromIndex(entry.Key);
            if (!copied.TryAdd(entry.Key, entry.Value))
                throw new ArgumentException("Each baseline cell must have one definition.", nameof(cells));
        }
        Grid = grid;
        _cells = copied;
    }

    public FireGrid Grid { get; }

    public FireSimBaselineDefinition GetCell(int cellIndex)
    {
        Grid.FromIndex(cellIndex);
        return _cells.TryGetValue(cellIndex, out var definition) ? definition : FireSimBaselineDefinition.Empty;
    }
}
