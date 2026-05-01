using Timberborn.MapStateSystem;
using Timberborn.SingletonSystem;
using Timberborn.TerrainSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornFireRuntimeInitializer : ILoadableSingleton
{
    private readonly TimberbornFireRuntime _runtime;
    private readonly ITimberbornFireSimulatorFactory _simulatorFactory;
    private readonly MapSize _mapSize;
    private readonly ITerrainService _terrainService;
    private readonly TimberbornTerrainCellSourceProvider _cellSourceProvider;
    private readonly ITimberbornFireLogSink _logSink;

    public TimberbornFireRuntimeInitializer(
        TimberbornFireRuntime runtime,
        ITimberbornFireSimulatorFactory simulatorFactory,
        MapSize mapSize,
        ITerrainService terrainService)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _simulatorFactory = simulatorFactory ?? throw new ArgumentNullException(nameof(simulatorFactory));
        _mapSize = mapSize ?? throw new ArgumentNullException(nameof(mapSize));
        _terrainService = terrainService ?? throw new ArgumentNullException(nameof(terrainService));
        _cellSourceProvider = new TimberbornTerrainCellSourceProvider();
        _logSink = new UnityTimberbornFireLogSink();
    }

    public void Load()
    {
        try
        {
            Vector3Int terrainSize = _mapSize.TerrainSize;
            FireGrid grid = new(terrainSize.x, terrainSize.y, terrainSize.z);
            TimberbornCellSource[] sources = _cellSourceProvider.CreateTerrainSources(_mapSize, _terrainService).ToArray();

            _logSink.Info(
                $"wildfire_timberborn_runtime_initialize_started width={grid.Width} height={grid.Height} depth={grid.Depth} terrain_sources={sources.Length}");
            _runtime.Initialize(grid, sources, _simulatorFactory);
            _logSink.Info(
                $"wildfire_timberborn_runtime_initialize_completed width={grid.Width} height={grid.Height} depth={grid.Depth} terrain_sources={sources.Length}");
        }
        catch (Exception exception)
        {
            _logSink.Warning($"wildfire_timberborn_runtime_initialize_failed message=\"{exception.Message}\"");
        }
    }
}

public sealed class TimberbornTerrainCellSourceProvider
{
    private readonly TimberbornTerrainAdapter _terrainAdapter = new();

    public IEnumerable<TimberbornCellSource> CreateTerrainSources(MapSize mapSize, ITerrainService terrainService)
    {
        if (mapSize is null)
        {
            throw new ArgumentNullException(nameof(mapSize));
        }

        if (terrainService is null)
        {
            throw new ArgumentNullException(nameof(terrainService));
        }

        Vector2Int terrainSize2D = mapSize.TerrainSize2D;
        Vector3Int terrainSize = mapSize.TerrainSize;

        return Enumerable.Range(0, terrainSize2D.x)
            .SelectMany(x => Enumerable.Range(0, terrainSize2D.y)
                .SelectMany(y => terrainService.GetAllHeightsInCell(new Vector2Int(x, y))))
            .Where(coordinates => IsInsideTerrain(coordinates, terrainSize))
            .Select(coordinates => _terrainAdapter.CreateSource(
                coordinates.x,
                coordinates.y,
                coordinates.z,
                isSolid: true));
    }

    private static bool IsInsideTerrain(Vector3Int coordinates, Vector3Int terrainSize)
    {
        return coordinates.x >= 0 &&
            coordinates.y >= 0 &&
            coordinates.z >= 0 &&
            coordinates.x < terrainSize.x &&
            coordinates.y < terrainSize.y &&
            coordinates.z < terrainSize.z;
    }
}
