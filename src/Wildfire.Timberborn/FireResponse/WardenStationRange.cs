using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.BuildingRange;
using Timberborn.MapStateSystem;
using UnityEngine;

namespace Wildfire.Timberborn.FireResponse;

/// <summary>Potential geometric coverage rendered by the native range UI, independent of route safety.</summary>
public sealed class WardenStationRange : BaseComponent, IAwakableComponent, IBuildingWithRange
{
    private readonly StackableBlockService _surfaces;
    private readonly MapSize _mapSize;
    private WardenStation _station = null!;
    private BlockObject _blockObject = null!;
    private BuildingAccessible _buildingAccessible = null!;

    public WardenStationRange(StackableBlockService surfaces, MapSize mapSize)
    { _surfaces = surfaces; _mapSize = mapSize; }

    // Native UI unions the coverage of buildings sharing this name.
    public string RangeName => "Wildfire.WardenStation.Response";
    public void Awake()
    {
        _station = GetComponent<WardenStation>();
        _blockObject = GetComponent<BlockObject>();
        _buildingAccessible = GetComponent<BuildingAccessible>();
    }

    public IEnumerable<Vector3Int> GetBlocksInRange()
    {
        if (!TryGetAnchor(out var access)) yield break;
        foreach (var surface in _surfaces.GetGroundOrStackableBlocks(Columns(access, _mapSize.TotalSize), true))
            if (Contains(access, surface)) yield return surface;
    }

    public IEnumerable<BaseComponent> GetObjectsInRange() => Array.Empty<BaseComponent>();

    internal bool TryGetAnchor(out Vector3 access)
    {
        access = default;
        if (_blockObject.IsPreview)
        {
            if (!_blockObject.Positioned) return false;
            // Preview Accesses can still describe the previous placement. Native calculation
            // uses the current placement and is also what finished-state access publishes.
            access = _buildingAccessible.CalculateAccess();
            return true;
        }
        if (!_station.Finished || _station.Access.Accesses.Count == 0) return false;
        access = _station.Access.Accesses[0];
        return true;
    }

    internal static bool Contains(Vector3 access, Vector3Int surface) =>
        (new Vector3(surface.x + .5f, surface.z, surface.y + .5f) - access).sqrMagnitude <=
        WardenTargetSelector.ResponseRange * WardenTargetSelector.ResponseRange;

    internal static IEnumerable<Vector2Int> Columns(Vector3 access, Vector3Int size)
    {
        int radius = WardenTargetSelector.ResponseRange;
        int minX = Math.Max(0, (int)Math.Floor(access.x - radius));
        int maxX = Math.Min(size.x - 1, (int)Math.Floor(access.x + radius));
        int minY = Math.Max(0, (int)Math.Floor(access.z - radius));
        int maxY = Math.Min(size.y - 1, (int)Math.Floor(access.z + radius));
        for (int y = minY; y <= maxY; y++)
        for (int x = minX; x <= maxX; x++)
            yield return new(x, y);
    }
}
