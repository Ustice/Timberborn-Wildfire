using Timberborn.Navigation;
using UnityEngine;
using Wildfire.Core;
using Wildfire.Timberborn.Runtime;

namespace Wildfire.Timberborn.FireSafety;

public sealed record FireFieldObservation(int Width, int Height, int Depth,
    IReadOnlyList<ushort> Cells, IReadOnlyList<uint> TransportFields);

/// <summary>Shared field observation and movement safety. The simulator remains authoritative.</summary>
public sealed class FireSafetyField
{
    public bool ResponseEnabled => _runtime.FireResponseEnabled;
    private readonly TimberbornFireRuntime _runtime;
    private readonly INavigationService _navigation;
    private readonly List<PathCorner> _path = new();
    public FireSafetyField(TimberbornFireRuntime runtime, INavigationService navigation)
    { _runtime = runtime; _navigation = navigation; }

    public long Revision => _runtime.FireObservationRevision;
    public bool Ready => _runtime.FireResponseEnabled && ObservationAvailable;
    public bool ObservationAvailable => TryObserve(out _);
    internal bool TryObserve(out FireFieldObservation field) => _runtime.TryObserveFireField(out field);

    public bool IsBurning(int cellIndex) => TryObserve(out var field) &&
        cellIndex >= 0 && cellIndex < field.Cells.Count && PackedCell.BurningLevel(field.Cells[cellIndex]) > 0;

    public bool SafeRoute(Vector3 start, Vector3 end, bool escaping = false)
    {
        if (!SafePosition(end)) return false;
        _path.Clear();
        if (!_navigation.FindPath(start, end, _path)) return false;
        return SafeInstalledPath(start, _path, escaping);
    }

    public bool SafeInstalledPath(Vector3 start, IEnumerable<PathCorner> path, bool escaping)
    {
        var samples = new List<FireRouteSample> { new(0, RiskAt(start)) };
        float distance = 0;
        foreach (var corner in path)
        {
            float segment = Vector3.Distance(start, corner.Position);
            var steps = Math.Max(1, (int)Math.Ceiling(segment * 4));
            for (var step = 1; step <= steps; step++)
            {
                float fraction = (float)step / steps;
                samples.Add(new FireRouteSample(distance + segment * fraction,
                    RiskAt(Vector3.Lerp(start, corner.Position, fraction))));
            }
            distance += segment;
            start = corner.Position;
        }
        return FireRouteSafety.CanTraverse(samples, escaping);
    }

    public bool SafePosition(Vector3 position) => RiskAt(position) is >= 0 and < 2;

    private int RiskAt(Vector3 position)
    {
        if (!TryObserve(out var field)) return -1;
        int x = (int)Math.Floor(position.x), y = (int)Math.Floor(position.z), z = (int)Math.Floor(position.y);
        if (x < 0 || y < 0 || z < 0 || x >= field.Width || y >= field.Height || z >= field.Depth) return -1;
        var grid = new FireGrid(field.Width, field.Height, field.Depth);
        int risk = 0;
        for (var height = z; height <= Math.Min(z + 1, field.Depth - 1); height++)
        {
            var index = grid.ToIndex(x, y, height);
            var cell = field.Cells[index];
            var transport = WildfireTransportFieldState.Unpack(field.TransportFields[index]);
            risk = Math.Max(risk, PackedCell.BurningLevel(cell) * 4 + PackedCell.Heat(cell));
            risk = Math.Max(risk, transport.Smoke >= 3 ? 2 : 0);
            risk = Math.Max(risk, transport.Smoke > 0 && transport.SmokeContamination > 0 ? 3 : 0);
        }
        return risk;
    }

    public bool TryRetreat(Vector3 start, IEnumerable<Vector3> preferredAccesses, out Vector3 end)
    {
        foreach (var access in preferredAccesses)
            if (SafeRoute(start, access, escaping: true)) { end = access; return true; }
        foreach (var offset in RetreatOffsets)
        {
            var candidate = start + offset;
            if (SafeRoute(start, candidate, escaping: true)) { end = candidate; return true; }
        }
        end = default;
        return false;
    }

    private static readonly Vector3[] RetreatOffsets = { new(1,0,0), new(-1,0,0), new(0,0,1), new(0,0,-1), new(2,0,0), new(-2,0,0), new(0,0,2), new(0,0,-2) };
}
