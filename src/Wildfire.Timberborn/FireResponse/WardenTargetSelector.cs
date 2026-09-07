using UnityEngine;
using Wildfire.Core;
using Wildfire.Timberborn.FireSafety;

namespace Wildfire.Timberborn.FireResponse;

public readonly record struct WardenTarget(int CellIndex, Vector3 Approach);

/// <summary>Warden coverage and approach selection over the shared safety observation.</summary>
public sealed class WardenTargetSelector
{
    public const int ResponseRange = 20;
    private readonly FireSafetyField _field;
    private IReadOnlyList<ushort>? _observedCells;
    private readonly List<(int Index, Vector3 Position)> _burning = new();
    public WardenTargetSelector(FireSafetyField field) => _field = field;

    public bool TryFindTarget(Vector3 start, Vector3 station, out WardenTarget target)
    {
        target = default;
        if (!_field.TryObserve(out var field)) return false;
        RefreshFireTargets(field);
        var burning = _burning
            .Where(item => (item.Position - station).sqrMagnitude <= ResponseRange * ResponseRange)
            .OrderBy(item => (item.Position - start).sqrMagnitude).Take(32);
        foreach (var fire in burning)
        foreach (var offset in ApproachOffsets)
        {
            var approach = fire.Position + offset;
            if (_field.SafeRoute(start, approach))
            { target = new WardenTarget(fire.Index, approach); return true; }
        }
        return false;
    }

    private void RefreshFireTargets(FireFieldObservation field)
    {
        if (ReferenceEquals(_observedCells, field.Cells)) return;
        _observedCells = field.Cells;
        _burning.Clear();
        var grid = new FireGrid(field.Width, field.Height, field.Depth);
        for (var index = 0; index < field.Cells.Count; index++)
        {
            if (PackedCell.BurningLevel(field.Cells[index]) == 0) continue;
            var coordinates = grid.FromIndex(index);
            _burning.Add((index, new Vector3(coordinates.X + .5f, coordinates.Z, coordinates.Y + .5f)));
        }
    }

    private static readonly Vector3[] ApproachOffsets = { new(2, 0, 0), new(-2, 0, 0), new(0, 0, 2), new(0, 0, -2) };
}
