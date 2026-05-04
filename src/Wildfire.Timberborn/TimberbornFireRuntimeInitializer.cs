using Timberborn.BlockSystem;
using Timberborn.Buildings;
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
    private readonly IBlockService _blockService;
    private readonly ITimberbornFireLogSink _logSink;

    public TimberbornFireRuntimeInitializer(
        TimberbornFireRuntime runtime,
        ITimberbornFireSimulatorFactory simulatorFactory,
        MapSize mapSize,
        ITerrainService terrainService,
        IBlockService blockService)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _simulatorFactory = simulatorFactory ?? throw new ArgumentNullException(nameof(simulatorFactory));
        _mapSize = mapSize ?? throw new ArgumentNullException(nameof(mapSize));
        _terrainService = terrainService ?? throw new ArgumentNullException(nameof(terrainService));
        _blockService = blockService ?? throw new ArgumentNullException(nameof(blockService));
        _logSink = new UnityTimberbornFireLogSink();
    }

    public void Load()
    {
        try
        {
            Vector3Int terrainSize = _mapSize.TerrainSize;
            FireGrid grid = new(terrainSize.x, terrainSize.y, terrainSize.z);
            TimberbornWorldCellImporter importer = new(CreateLiveWorldCellSourceProviders());
            TimberbornWorldCellImportResult importResult = importer.Import(grid);
            TimberbornCellSource[] sources = importResult.Sources.ToArray();

            _logSink.Info(
                $"wildfire_timberborn_runtime_initialize_started width={grid.Width} height={grid.Height} depth={grid.Depth} {importResult.Summary.StatusToken}");
            if (!TimberbornAutoDispatchPolicy.IsAllowedCellCount(grid.CellCount))
            {
                _runtime.SkipInitializeForOversizedGrid(grid, importResult.Summary);
                _logSink.Warning(
                    "wildfire_timberborn_runtime_initialize_completed " +
                    "status=skipped " +
                    "reason=map_too_large " +
                    $"width={grid.Width} " +
                    $"height={grid.Height} " +
                    $"depth={grid.Depth} " +
                    $"cell_count={grid.CellCount} " +
                    $"limit={TimberbornAutoDispatchPolicy.CellLimit} " +
                    $"{importResult.Summary.StatusToken}");
                return;
            }

            TimberbornPausableBuildingBurnoutConsequenceApi buildingBurnoutApi =
                new(grid, _blockService);
            _runtime.AttachBuildingBurnoutConsequenceApi(buildingBurnoutApi);
            _runtime.AttachBuildingBurnoutStimulusTargetProvider(buildingBurnoutApi);
            _runtime.Initialize(grid, sources, importResult.CompanionFields, importResult.Summary, _simulatorFactory);
            _logSink.Info(
                $"wildfire_timberborn_runtime_initialize_completed width={grid.Width} height={grid.Height} depth={grid.Depth} {importResult.Summary.StatusToken}");
        }
        catch (Exception exception)
        {
            _logSink.Warning($"wildfire_timberborn_runtime_initialize_failed message=\"{exception.Message}\"");
        }
    }

    private IEnumerable<ITimberbornWorldCellSourceProvider> CreateLiveWorldCellSourceProviders()
    {
        return new ITimberbornWorldCellSourceProvider[]
        {
            new TimberbornTerrainWorldCellSourceProvider(_mapSize, _terrainService),
            new TimberbornSafeUnavailableCellSourceProvider("trees", "safe_live_tree_enumeration_unavailable"),
            new TimberbornSafeUnavailableCellSourceProvider("crops", "safe_live_crop_enumeration_unavailable"),
            new TimberbornPausableBuildingCellSourceProvider(_blockService, _mapSize, _terrainService),
            new TimberbornSafeUnavailableCellSourceProvider("storage", "safe_live_storage_enumeration_unavailable"),
            new TimberbornSafeUnavailableCellSourceProvider("infrastructure", "safe_live_infrastructure_enumeration_unavailable"),
            new TimberbornSafeUnavailableCellSourceProvider("water", "safe_live_water_enumeration_unavailable"),
            new TimberbornSafeUnavailableCellSourceProvider("badwater", "safe_live_badwater_enumeration_unavailable"),
        };
    }
}

public sealed class TimberbornPausableBuildingCellSourceProvider : ITimberbornWorldCellSourceProvider
{
    private readonly IBlockService _blockService;
    private readonly MapSize _mapSize;
    private readonly ITerrainService _terrainService;
    private readonly TimberbornBuildingAdapter _buildingAdapter = new();

    public TimberbornPausableBuildingCellSourceProvider(
        IBlockService blockService,
        MapSize mapSize,
        ITerrainService terrainService)
    {
        _blockService = blockService ?? throw new ArgumentNullException(nameof(blockService));
        _mapSize = mapSize ?? throw new ArgumentNullException(nameof(mapSize));
        _terrainService = terrainService ?? throw new ArgumentNullException(nameof(terrainService));
    }

    public TimberbornWorldCellImportProviderResult Import(FireGrid grid)
    {
        TimberbornCellSource[] sources = EnumerateSurfaceCandidates(grid)
            .Where(cell => _blockService.GetObjectsWithComponentAt<PausableBuilding>(cell.Coordinates).Any())
            .Select(cell => _buildingAdapter.CreateWoodLikeSource(cell.X, cell.Y, cell.Z) with
            {
                CompanionTargetId = checked((uint)cell.Index + 1u),
            })
            .ToArray();

        return new TimberbornWorldCellImportProviderResult("buildings", sources);
    }

    private IEnumerable<BuildingImportCandidate> EnumerateSurfaceCandidates(FireGrid grid)
    {
        Vector2Int terrainSize2D = _mapSize.TerrainSize2D;
        Vector3Int terrainSize = _mapSize.TerrainSize;

        return Enumerable.Range(0, terrainSize2D.x)
            .SelectMany(x => Enumerable.Range(0, terrainSize2D.y)
                .SelectMany(y => _terrainService.GetAllHeightsInCell(new Vector2Int(x, y))))
            .Where(coordinates => IsInsideTerrain(coordinates, terrainSize))
            .SelectMany(coordinates => new[]
            {
                coordinates,
                new Vector3Int(coordinates.x, coordinates.y, coordinates.z + 1),
            })
            .Where(coordinates => IsInsideTerrain(coordinates, terrainSize))
            .Distinct()
            .Select(coordinates => new BuildingImportCandidate(
                grid.ToIndex(coordinates.x, coordinates.y, coordinates.z),
                coordinates,
                coordinates.x,
                coordinates.y,
                coordinates.z));
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

    private readonly record struct BuildingImportCandidate(
        int Index,
        Vector3Int Coordinates,
        int X,
        int Y,
        int Z);
}

public sealed class TimberbornTerrainWorldCellSourceProvider : ITimberbornWorldCellSourceProvider
{
    private readonly TimberbornTerrainAdapter _terrainAdapter = new();
    private readonly MapSize _mapSize;
    private readonly ITerrainService _terrainService;

    public TimberbornTerrainWorldCellSourceProvider(MapSize mapSize, ITerrainService terrainService)
    {
        _mapSize = mapSize ?? throw new ArgumentNullException(nameof(mapSize));
        _terrainService = terrainService ?? throw new ArgumentNullException(nameof(terrainService));
    }

    public TimberbornWorldCellImportProviderResult Import(FireGrid grid)
    {
        return new TimberbornWorldCellImportProviderResult(
            "terrain",
            CreateTerrainSources(_mapSize, _terrainService).ToArray());
    }

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
