using Timberborn.Navigation;
using Timberborn.MapStateSystem;
using Timberborn.TerrainSystem;
using Timberborn.WaterSystem;
using UnityEngine;

namespace Wildfire.Timberborn.FireBell;

// A deliberately narrow, fixed ground-level cardinal shoreline; the executor owns route/arrival.
internal sealed class NaturalWaterSourceShore
{
    private readonly ITerrainService _terrain;
    private readonly MapSize _map;
    private readonly IThreadSafeWaterMap _water;
    private readonly INavigationService _navigation;
    internal NaturalWaterSourceShore(ITerrainService terrain, IThreadSafeWaterMap water, INavigationService navigation, MapSize map)
    { _terrain = terrain; _water = water; _navigation = navigation; _map = map; }

    internal bool IsUsable(Vector3Int input, Vector3 point)
    {
        if (!TryGetShore(input, point, _map.TotalSize, out var shore) ||
            !_terrain.OnGround(input) || !_terrain.OnGround(shore) ||
            !_water.TryGetColumnFloor(input, out int floor) || floor != input.z ||
            !_water.CellIsUnderwater(input) || _water.CellIsUnderwater(shore)) return false;
        float contamination = _water.ColumnContamination(input);
        return float.IsFinite(contamination) && contamination == 0 && _navigation.IsOnNavMesh(point);
    }

    internal static bool TryGetShore(Vector3Int input, Vector3 point, Vector3Int size, out Vector3Int shore)
    {
        shore = default;
        if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z) ||
            input.x < 0 || input.y < 0 || input.z < 0 || input.x >= size.x || input.y >= size.y || input.z >= size.z ||
            point.x < 0 || point.z < 0 || point.x >= size.x || point.z >= size.y || point.y != input.z) return false;
        shore = new((int)point.x, (int)point.z, input.z);
        return point.x == shore.x + .5f && point.z == shore.y + .5f &&
            Math.Abs((long)shore.x - input.x) + Math.Abs((long)shore.y - input.y) == 1;
    }
}
