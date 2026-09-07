using Timberborn.Navigation;
using UnityEngine;
using Wildfire.Core;
using Wildfire.Timberborn.Runtime;

namespace Wildfire.Timberborn.FireResponse;

public readonly record struct WardenTarget(int CellIndex, Vector3 Approach);

/// <summary>Derived targeting and safety observations. The simulator remains authoritative.</summary>
public sealed class WardenFireField
{
    private readonly TimberbornFireRuntime _runtime;
    private readonly INavigationService _navigation;
    private readonly List<PathCorner> _path = new();
    private IReadOnlyList<ushort>? _observedCells;
    private readonly List<(int Index, Vector3 Position)> _burning = new();
    public WardenFireField(TimberbornFireRuntime runtime, INavigationService navigation)
    { _runtime = runtime; _navigation = navigation; }

    public bool Ready => _runtime.TryObserveWardenField(out _);

    public bool TryFindTarget(Vector3 start, Vector3 station, out WardenTarget target)
    {
        target = default;
        if (!_runtime.TryObserveWardenField(out var field)) return false;
        RefreshFireTargets(field);
        var burning = _burning
            .Where(item => (item.Position - station).sqrMagnitude <= 400)
            .OrderBy(item => (item.Position - start).sqrMagnitude).Take(32);
        foreach (var fire in burning)
        foreach (var offset in ApproachOffsets)
        {
            var approach = fire.Position + offset;
            if (SafeRoute(start, approach))
            { target = new WardenTarget(fire.Index, approach); return true; }
        }
        return false;
    }

    private void RefreshFireTargets(TimberbornFireSimPersistenceSnapshot field)
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

    public bool IsBurning(int cellIndex) => _runtime.TryObserveWardenField(out var field) &&
        cellIndex >= 0 && cellIndex < field.Cells.Count && PackedCell.BurningLevel(field.Cells[cellIndex]) > 0;

    public bool SafeRoute(Vector3 start, Vector3 end)
    {
        if (!SafePosition(end)) return false;
        _path.Clear();
        return _navigation.FindPath(start, end, _path) && SafePath(start, _path);
    }

    public bool SafePath(Vector3 start, IEnumerable<PathCorner> corners)
    {
        foreach (var corner in corners)
        {
            if (!SafeSegment(start, corner.Position)) return false;
            start = corner.Position;
        }
        return true;
    }

    public bool SafeSegment(Vector3 start, Vector3 end)
    {
        var steps = Math.Max(1, (int)Math.Ceiling(Vector3.Distance(start, end) * 4));
        for (var step = 1; step <= steps; step++)
            if (!SafePosition(Vector3.Lerp(start, end, (float)step / steps))) return false;
        return true;
    }

    public bool SafePosition(Vector3 position)
    {
        if (!_runtime.TryObserveWardenField(out var field)) return false;
        int x = (int)Math.Floor(position.x), y = (int)Math.Floor(position.z), z = (int)Math.Floor(position.y);
        if (x < 0 || y < 0 || z < 0 || x >= field.Width || y >= field.Height || z >= field.Depth) return false;
        var grid = new FireGrid(field.Width, field.Height, field.Depth);
        for (var height = z; height <= Math.Min(z + 1, field.Depth - 1); height++)
        {
            var cell = field.Cells[grid.ToIndex(x, y, height)];
            if (PackedCell.BurningLevel(cell) > 0 || PackedCell.Heat(cell) >= 2) return false;
        }
        return true;
    }

    public bool TryRetreat(Vector3 start, IEnumerable<Vector3> stationAccesses, out Vector3 end)
    {
        foreach (var access in stationAccesses)
            if (SafeRoute(start, access)) { end = access; return true; }
        foreach (var offset in RetreatOffsets)
        {
            var candidate = start + offset;
            if (SafeRoute(start, candidate)) { end = candidate; return true; }
        }
        end = default;
        return false;
    }

    private static readonly Vector3[] ApproachOffsets = { new(2, 0, 0), new(-2, 0, 0), new(0, 0, 2), new(0, 0, -2) };
    private static readonly Vector3[] RetreatOffsets = { new(1,0,0), new(-1,0,0), new(0,0,1), new(0,0,-1), new(2,0,0), new(-2,0,0), new(0,0,2), new(0,0,-2) };
}
