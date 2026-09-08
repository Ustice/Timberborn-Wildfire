using Timberborn.MapStateSystem;

namespace Wildfire.Timberborn.Runtime;

internal static class TimberbornNativeWorldDomain
{
    internal static TimberbornWorldDomain Read(MapSize mapSize)
    {
        if (mapSize is null) throw new ArgumentNullException(nameof(mapSize));
        var world = mapSize.TotalSize; var terrain = mapSize.TerrainSize; var horizontal = mapSize.TerrainSize2D;
        if (horizontal.x != terrain.x || horizontal.y != terrain.y)
            throw new InvalidOperationException("Native map dimensions are not settled.");
        return new(new(world.x, world.y, world.z), new(terrain.x, terrain.y, terrain.z));
    }
}
