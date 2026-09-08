using Wildfire.Core;

namespace Wildfire.Timberborn.Mapping;

/// <summary>One immutable native dimension reading; terrain storage is smaller than the supported block world.</summary>
public sealed record TimberbornWorldDomain
{
    // Internal for explicit synthetic fixtures; production obtains this only from the native MapSize reader.
    internal TimberbornWorldDomain(FireGrid worldGrid, FireGrid terrainGrid)
    {
        if (worldGrid.Width <= 0 || worldGrid.Height <= 0 || worldGrid.Depth <= 0 || terrainGrid.Depth <= 0 ||
            worldGrid.Width != terrainGrid.Width || worldGrid.Height != terrainGrid.Height || worldGrid.Depth < terrainGrid.Depth)
            throw new ArgumentException("Native world and terrain domains must share positive horizontal dimensions and bounded terrain depth.");
        _ = checked(worldGrid.Width * worldGrid.Height * worldGrid.Depth);
        _ = checked(terrainGrid.Width * terrainGrid.Height * terrainGrid.Depth);
        WorldGrid = worldGrid; TerrainGrid = terrainGrid;
    }
    public FireGrid WorldGrid { get; }
    public FireGrid TerrainGrid { get; }
}
