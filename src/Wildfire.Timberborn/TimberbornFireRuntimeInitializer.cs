using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.BaseComponentSystem;
using Timberborn.EntitySystem;
using Timberborn.MapIndexSystem;
using Timberborn.MapStateSystem;
using Timberborn.SingletonSystem;
using Timberborn.SoilContaminationSystem;
using Timberborn.SoilMoistureSystem;
using Timberborn.Stockpiles;
using Timberborn.TerrainSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornFireRuntimeInitializer : ILoadableSingleton, IUpdatableSingleton
{
    private const int TerrainOnlyFallbackAttempt = 120;
    private const int RequiredStableEntitySnapshotCount = 1;

    private readonly TimberbornFireRuntime _runtime;
    private readonly ITimberbornFireSimulatorFactory _simulatorFactory;
    private readonly MapSize _mapSize;
    private readonly ITerrainService _terrainService;
    private readonly ISoilMoistureService _soilMoistureService;
    private readonly ISoilContaminationService _soilContaminationService;
    private readonly MapIndexService _mapIndexService;
    private readonly IBlockService _blockService;
    private readonly EntityRegistry _entityRegistry;
    private readonly ITimberbornFireLogSink _logSink;
    private bool _initialized;
    private int _initializationAttempt;
    private int _lastEntityCount;
    private int _stableEntitySnapshotCount;

    public TimberbornFireRuntimeInitializer(
        TimberbornFireRuntime runtime,
        ITimberbornFireSimulatorFactory simulatorFactory,
        MapSize mapSize,
        ITerrainService terrainService,
        ISoilMoistureService soilMoistureService,
        ISoilContaminationService soilContaminationService,
        MapIndexService mapIndexService,
        IBlockService blockService,
        EntityRegistry entityRegistry)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _simulatorFactory = simulatorFactory ?? throw new ArgumentNullException(nameof(simulatorFactory));
        _mapSize = mapSize ?? throw new ArgumentNullException(nameof(mapSize));
        _terrainService = terrainService ?? throw new ArgumentNullException(nameof(terrainService));
        _soilMoistureService = soilMoistureService ?? throw new ArgumentNullException(nameof(soilMoistureService));
        _soilContaminationService = soilContaminationService ?? throw new ArgumentNullException(nameof(soilContaminationService));
        _mapIndexService = mapIndexService ?? throw new ArgumentNullException(nameof(mapIndexService));
        _blockService = blockService ?? throw new ArgumentNullException(nameof(blockService));
        _entityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
        _logSink = new UnityTimberbornFireLogSink();
    }

    public void Load()
    {
        _initializationAttempt = 0;
        _lastEntityCount = 0;
        _stableEntitySnapshotCount = 0;
        _logSink.Info("wildfire_timberborn_runtime_initialize_deferred_until_update");
    }

    public void UpdateSingleton()
    {
        if (_initialized)
        {
            return;
        }

        _initializationAttempt++;
        TryInitializeRuntime();
    }

    private void TryInitializeRuntime()
    {
        try
        {
            int entityCount = TimberbornEntityComponentCells.EntityCount(_entityRegistry);
            bool allowTerrainOnly = _initializationAttempt >= TerrainOnlyFallbackAttempt;
            if (entityCount == 0 && !allowTerrainOnly)
            {
                if (_initializationAttempt is 0 || _initializationAttempt % 30 == 0)
                {
                    _logSink.Info(
                        "wildfire_timberborn_runtime_initialize_waiting_for_entities " +
                        $"attempt={_initializationAttempt} " +
                        $"entity_count={entityCount}");
                }

                return;
            }
            if (entityCount > 0)
            {
                _stableEntitySnapshotCount = entityCount == _lastEntityCount
                    ? _stableEntitySnapshotCount + 1
                    : 1;
                _lastEntityCount = entityCount;
                if (_stableEntitySnapshotCount < RequiredStableEntitySnapshotCount)
                {
                    _logSink.Info(
                        "wildfire_timberborn_runtime_initialize_waiting_for_stable_entities " +
                        $"attempt={_initializationAttempt} " +
                        $"entity_count={entityCount} " +
                        $"stable_snapshots={_stableEntitySnapshotCount} " +
                        $"required_stable_snapshots={RequiredStableEntitySnapshotCount}");
                    return;
                }
            }

            Vector3Int terrainSize = _mapSize.TerrainSize;
            FireGrid grid = new(terrainSize.x, terrainSize.y, terrainSize.z);
            BlockObject[] blockObjects = TimberbornEntityComponentCells.BlockObjects(_entityRegistry).ToArray();
            _logSink.Info(
                "wildfire_timberborn_world_import_entity_snapshot " +
                $"attempt={_initializationAttempt} " +
                $"entity_count={entityCount} " +
                $"block_object_count={blockObjects.Length} " +
                $"sample_block_objects={TimberbornQaCommandBridge.FormatToken(TimberbornEntityComponentCells.FormatSampleBlockObjectNames(blockObjects))}");
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
            TimberbornLiveBurnDamageTargets burnDamageTargets =
                TimberbornLiveBurnDamageTargetCollector.Collect(_entityRegistry, grid);
            TimberbornLiveCropBurnDamageTargets cropBurnDamageTargets =
                TimberbornLiveCropBurnDamageTargetCollector.Collect(_entityRegistry, grid);
            TimberbornBurnDamageService burnDamageService =
                new(burnDamageTargets.DescriptorCatalog, logSink: _logSink);
            burnDamageService.RegisterTargets(
                grid,
                burnDamageTargets.Registrations.Concat(cropBurnDamageTargets.Registrations),
                cropBurnDamageTargets.Descriptors);
            _runtime.AttachBuildingBurnoutConsequenceApi(buildingBurnoutApi);
            _runtime.AttachBuildingBurnoutStimulusTargetProvider(buildingBurnoutApi);
            _runtime.AttachBurnDamageService(burnDamageService);
            _runtime.AttachTreeBurnConsequenceApi(
                new TimberbornTextureTreeBurnConsequenceApi(_entityRegistry, _logSink));
            _runtime.AttachCropBurnConsequenceApi(
                new TimberbornTextureCropBurnConsequenceApi(_entityRegistry, _logSink));
            _runtime.AttachStructureBurnDamageRollbackTargetApi(
                new TimberbornStructureBurnDamageRollbackTargetApi(grid, _blockService, _logSink));
            _runtime.AttachStoredGoodBurnInventoryApi(new TimberbornStockpileStoredGoodBurnInventoryApi(grid, _blockService));
            _runtime.AttachExplosiveInfrastructureTargetApi(
                new TimberbornDynamiteExplosiveInfrastructureTargetApi(grid, _blockService));
            _runtime.AttachDetonatorFireSafetyTargetApi(
                new TimberbornDetonatorFireSafetyTargetApi(grid, _blockService));
            _runtime.AttachTunnelFireTargetApi(new TimberbornTunnelFireTargetApi(grid, _blockService));
            _runtime.AttachPathInfrastructureFireTargetApi(
                new TimberbornPathInfrastructureFireTargetApi(grid, _blockService));
            _runtime.AttachPowerInfrastructureFireTargetApi(
                new TimberbornPowerInfrastructureFireTargetApi(grid, _blockService));
            _runtime.AttachWaterInfrastructureFireTargetApi(
                new TimberbornWaterInfrastructureFireTargetApi(grid, _blockService));
            _runtime.Initialize(grid, sources, importResult.CompanionFields, importResult.Summary, _simulatorFactory);
            _initialized = true;
            _logSink.Info(
                $"wildfire_timberborn_runtime_initialize_completed width={grid.Width} height={grid.Height} depth={grid.Depth} {importResult.Summary.StatusToken}");
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                "wildfire_timberborn_runtime_initialize_failed " +
                $"message={TimberbornQaCommandBridge.FormatToken(exception.Message)} " +
                $"details={TimberbornQaCommandBridge.FormatToken(exception.ToString())}");
        }
    }

    private IEnumerable<ITimberbornWorldCellSourceProvider> CreateLiveWorldCellSourceProviders()
    {
        return new ITimberbornWorldCellSourceProvider[]
        {
            new TimberbornTerrainWorldCellSourceProvider(
                _mapSize,
                _terrainService,
                _soilMoistureService,
                _soilContaminationService,
                _mapIndexService),
            new TimberbornNaturalResourceCellSourceProvider(_entityRegistry),
            new TimberbornInfrastructureCellSourceProvider(_entityRegistry),
            new TimberbornBuildingCellSourceProvider(_entityRegistry),
            new TimberbornStorageCellSourceProvider(_entityRegistry),
            new TimberbornWaterSourceCellSourceProvider(_entityRegistry),
        };
    }
}

public sealed class TimberbornBuildingCellSourceProvider : ITimberbornWorldCellSourceProvider
{
    private readonly EntityRegistry _entityRegistry;
    private readonly TimberbornBuildingAdapter _buildingAdapter = new();

    public TimberbornBuildingCellSourceProvider(EntityRegistry entityRegistry)
    {
        _entityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
    }

    public TimberbornWorldCellImportProviderResult Import(FireGrid grid)
    {
        TimberbornCellSource[] sources = TimberbornEntityComponentCells.ComponentBlockObjects<Building>(_entityRegistry)
            .Where(static building => !TimberbornEntityComponentCells.IsInfrastructureName(building.BlockObject.Name))
            .Where(static building => !building.Component.TryGetComponent(out Stockpile _))
            .SelectMany((building, buildingIndex) => TimberbornEntityComponentCells.OccupiedCoordinates(building.Component)
                .Where(coordinates => TimberbornEntityComponentCells.IsInsideGrid(coordinates, grid))
                .Select(coordinates => _buildingAdapter.CreateBuildingSource(
                    coordinates.x,
                    coordinates.y,
                    coordinates.z,
                    building.BlockObject.Name,
                    checked((uint)buildingIndex + 1u))))
            .ToArray();

        return new TimberbornWorldCellImportProviderResult("buildings", sources);
    }
}

public sealed class TimberbornNaturalResourceCellSourceProvider : ITimberbornWorldCellSourceProvider
{
    private readonly EntityRegistry _entityRegistry;
    private readonly TimberbornResourceAdapter _resourceAdapter = new();

    public TimberbornNaturalResourceCellSourceProvider(EntityRegistry entityRegistry)
    {
        _entityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
    }

    public TimberbornWorldCellImportProviderResult Import(FireGrid grid)
    {
        BlockObject[] blockObjects = TimberbornEntityComponentCells.BlockObjects(_entityRegistry).ToArray();
        TimberbornCellSource[] treeSources = blockObjects
            .Where(static resource => TimberbornEntityComponentCells.IsTreeName(resource.Name))
            .SelectMany((resource, resourceIndex) => TimberbornEntityComponentCells.OccupiedCoordinates(resource)
                .Where(coordinates => TimberbornEntityComponentCells.IsInsideGrid(coordinates, grid))
                .Select(coordinates => _resourceAdapter.CreateTreeSource(
                    coordinates.x,
                    coordinates.y,
                    coordinates.z,
                    resource.Name,
                    checked((uint)resourceIndex + 1u))))
            .ToArray();
        TimberbornCellSource[] cropSources = blockObjects
            .Where(static resource => TimberbornEntityComponentCells.IsCropName(resource.Name))
            .SelectMany((resource, resourceIndex) => TimberbornEntityComponentCells.OccupiedCoordinates(resource)
                .Where(coordinates => TimberbornEntityComponentCells.IsInsideGrid(coordinates, grid))
                .Select(coordinates => _resourceAdapter.CreateCropSource(
                    coordinates.x,
                    coordinates.y,
                    coordinates.z,
                    resource.Name,
                    checked((uint)resourceIndex + 1u))))
            .ToArray();
        TimberbornCellSource[] vegetationSources = blockObjects
            .Where(static resource => TimberbornEntityComponentCells.IsVegetationName(resource.Name))
            .SelectMany((resource, resourceIndex) => TimberbornEntityComponentCells.OccupiedCoordinates(resource)
                .Where(coordinates => TimberbornEntityComponentCells.IsInsideGrid(coordinates, grid))
                .Select(coordinates => _resourceAdapter.CreateVegetationSource(
                    coordinates.x,
                    coordinates.y,
                    coordinates.z) with
                {
                    CompanionTargetId = checked((uint)resourceIndex + 1u),
                }))
            .ToArray();

        return new TimberbornWorldCellImportProviderResult("natural_resources", treeSources.Concat(cropSources).Concat(vegetationSources).ToArray());
    }
}

public sealed class TimberbornInfrastructureCellSourceProvider : ITimberbornWorldCellSourceProvider
{
    private readonly EntityRegistry _entityRegistry;
    private readonly TimberbornBuildingAdapter _buildingAdapter = new();

    public TimberbornInfrastructureCellSourceProvider(EntityRegistry entityRegistry)
    {
        _entityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
    }

    public TimberbornWorldCellImportProviderResult Import(FireGrid grid)
    {
        TimberbornCellSource[] sources = TimberbornEntityComponentCells.BlockObjects(_entityRegistry)
            .Where(static blockObject => TimberbornEntityComponentCells.IsInfrastructureName(blockObject.Name))
            .SelectMany((blockObject, blockObjectIndex) => TimberbornEntityComponentCells.OccupiedCoordinates(blockObject)
                .Where(coordinates => TimberbornEntityComponentCells.IsInsideGrid(coordinates, grid))
                .Select(coordinates => _buildingAdapter.CreateNonBurnableSource(coordinates.x, coordinates.y, coordinates.z) with
                {
                    CompanionTargetId = checked((uint)blockObjectIndex + 1u),
                }))
            .ToArray();

        return new TimberbornWorldCellImportProviderResult("infrastructure", sources);
    }
}

public sealed class TimberbornStorageCellSourceProvider : ITimberbornWorldCellSourceProvider
{
    private readonly EntityRegistry _entityRegistry;
    private readonly TimberbornResourceAdapter _resourceAdapter = new();

    public TimberbornStorageCellSourceProvider(EntityRegistry entityRegistry)
    {
        _entityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
    }

    public TimberbornWorldCellImportProviderResult Import(FireGrid grid)
    {
        TimberbornCellSource[] sources = TimberbornEntityComponentCells.ComponentsWithBlockObject<Stockpile>(_entityRegistry)
            .SelectMany((stockpile, stockpileIndex) => TimberbornEntityComponentCells.OccupiedCoordinates(stockpile)
                .Where(coordinates => TimberbornEntityComponentCells.IsInsideGrid(coordinates, grid))
                .Select(coordinates => _resourceAdapter.CreateStockpileResourceSource(
                    coordinates.x,
                    coordinates.y,
                    coordinates.z,
                    stockpile.WhitelistedGoodType,
                    checked((uint)stockpileIndex + 1u))))
            .ToArray();

        return new TimberbornWorldCellImportProviderResult("storage", sources);
    }
}

public sealed class TimberbornWaterSourceCellSourceProvider : ITimberbornWorldCellSourceProvider
{
    private readonly EntityRegistry _entityRegistry;
    private readonly TimberbornWaterAdapter _waterAdapter = new();

    public TimberbornWaterSourceCellSourceProvider(EntityRegistry entityRegistry)
    {
        _entityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
    }

    public TimberbornWorldCellImportProviderResult Import(FireGrid grid)
    {
        BlockObject[] blockObjects = TimberbornEntityComponentCells.BlockObjects(_entityRegistry).ToArray();
        TimberbornCellSource[] waterSources = blockObjects
            .Where(static source => TimberbornEntityComponentCells.IsWaterSourceName(source.Name))
            .SelectMany(TimberbornEntityComponentCells.OccupiedCoordinates)
            .Where(coordinates => TimberbornEntityComponentCells.IsInsideGrid(coordinates, grid))
            .Select(coordinates => _waterAdapter.CreateSource(coordinates.x, coordinates.y, coordinates.z, 3))
            .ToArray();
        TimberbornCellSource[] badwaterSources = blockObjects
            .Where(static source => TimberbornEntityComponentCells.IsBadwaterSourceName(source.Name))
            .SelectMany(TimberbornEntityComponentCells.OccupiedCoordinates)
            .Where(coordinates => TimberbornEntityComponentCells.IsInsideGrid(coordinates, grid))
            .Select(coordinates => _waterAdapter.CreateSource(
                coordinates.x,
                coordinates.y,
                coordinates.z,
                water: 3,
                isContaminated: true,
                isBadwater: true))
            .ToArray();

        return new TimberbornWorldCellImportProviderResult("water_sources", waterSources.Concat(badwaterSources).ToArray());
    }
}

public static class TimberbornEntityComponentCells
{
    private static readonly string[] TreeNameTokens =
    {
        "Birch",
        "Chestnut",
        "Mangrove",
        "Maple",
        "Oak",
        "Pine",
    };

    private static readonly string[] CropNameTokens =
    {
        "Canola",
        "Carrot",
        "Cassava",
        "Cattail",
        "Coffee",
        "Corn",
        "Dandelion",
        "Eggplant",
        "Kohlrabi",
        "Potato",
        "Soybean",
        "Spadderdock",
        "Sunflower",
        "Wheat",
    };

    public static bool IsTreeName(string name)
    {
        return TreeNameTokens.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsCropName(string name)
    {
        return CropNameTokens.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsVegetationName(string name)
    {
        return name.Contains("Blueberry", StringComparison.OrdinalIgnoreCase) &&
            !IsTreeName(name) &&
            !IsCropName(name);
    }

    public static bool IsInfrastructureName(string name)
    {
        return TimberbornInfrastructureNameClassifier.IsAnyInfrastructureName(name);
    }

    public static bool IsWaterSourceName(string name)
    {
        return (name.Contains("WaterSource", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Water Source", StringComparison.OrdinalIgnoreCase)) &&
            !IsBadwaterSourceName(name);
    }

    public static bool IsBadwaterSourceName(string name)
    {
        return name.Contains("BadwaterSource", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Badwater Source", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Badtide", StringComparison.OrdinalIgnoreCase);
    }

    public static int EntityCount(EntityRegistry entityRegistry)
    {
        return entityRegistry.Entities.Count;
    }

    public static IEnumerable<BlockObject> BlockObjects(EntityRegistry entityRegistry)
    {
        return entityRegistry.Entities
            .Select(entity => TryGetComponent<BlockObject>(entity, out BlockObject blockObject)
                ? blockObject
                : null)
            .Where(static blockObject => blockObject is not null)!;
    }

    public static IEnumerable<T> ComponentsWithBlockObject<T>(EntityRegistry entityRegistry)
        where T : BaseComponent
    {
        return entityRegistry.Entities
            .Select(entity => TryGetComponent<T>(entity, out T component) &&
                TryGetComponent<BlockObject>(entity, out _)
                    ? component
                    : null)
            .Where(static component => component is not null)!;
    }

    public static IEnumerable<TimberbornEntityComponentBlockObject<T>> ComponentBlockObjects<T>(EntityRegistry entityRegistry)
        where T : BaseComponent
    {
        return entityRegistry.Entities
            .Select(entity => TryGetComponent<T>(entity, out T component) &&
                TryGetComponent<BlockObject>(entity, out BlockObject blockObject)
                    ? new TimberbornEntityComponentBlockObject<T>(component, blockObject)
                    : (TimberbornEntityComponentBlockObject<T>?)null)
            .Where(static component => component.HasValue)
            .Select(static component => component!.Value);
    }

    public static string FormatSampleBlockObjectNames(IEnumerable<BlockObject> blockObjects)
    {
        string[] names = blockObjects
            .Select(static blockObject => blockObject.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToArray();

        return names.Length == 0
            ? "none"
            : string.Join("|", names);
    }

    public static IEnumerable<Vector3Int> OccupiedCoordinates(BaseComponent component)
    {
        BlockObject blockObject = component.GetComponent<BlockObject>();
        return blockObject.PositionedBlocks.GetOccupiedCoordinates();
    }

    private static bool TryGetComponent<T>(BaseComponent component, out T result)
    {
        return component.TryGetComponent(out result);
    }

    public static bool IsInsideGrid(Vector3Int coordinates, FireGrid grid)
    {
        return coordinates.x >= 0 &&
            coordinates.y >= 0 &&
            coordinates.z >= 0 &&
            coordinates.x < grid.Width &&
            coordinates.y < grid.Height &&
            coordinates.z < grid.Depth;
    }

    public readonly record struct TimberbornEntityComponentBlockObject<T>(T Component, BlockObject BlockObject)
        where T : BaseComponent;
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
            .Select(cell => _blockService.GetObjectsWithComponentAt<PausableBuilding>(cell.Coordinates)
                .Select(building => _buildingAdapter.CreateBuildingSource(
                    cell.X,
                    cell.Y,
                    cell.Z,
                    building.GetComponent<BlockObject>().Name,
                    checked((uint)cell.Index + 1u)))
                .First())
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
    private readonly ISoilMoistureService? _soilMoistureService;
    private readonly ISoilContaminationService? _soilContaminationService;
    private readonly MapIndexService? _mapIndexService;

    public TimberbornTerrainWorldCellSourceProvider(MapSize mapSize, ITerrainService terrainService)
        : this(mapSize, terrainService, soilMoistureService: null, soilContaminationService: null, mapIndexService: null)
    {
    }

    public TimberbornTerrainWorldCellSourceProvider(
        MapSize mapSize,
        ITerrainService terrainService,
        ISoilMoistureService? soilMoistureService,
        ISoilContaminationService? soilContaminationService,
        MapIndexService? mapIndexService)
    {
        _mapSize = mapSize ?? throw new ArgumentNullException(nameof(mapSize));
        _terrainService = terrainService ?? throw new ArgumentNullException(nameof(terrainService));
        _soilMoistureService = soilMoistureService;
        _soilContaminationService = soilContaminationService;
        _mapIndexService = mapIndexService;
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
                isSolid: true,
                wetness: ReadTerrainWetness(coordinates),
                soilContamination: ReadSoilContamination(coordinates)));
    }

    private byte ReadTerrainWetness(Vector3Int coordinates)
    {
        if (_soilMoistureService is null || _mapIndexService is null)
        {
            return 0;
        }

        try
        {
            int index = _mapIndexService.CellToIndex(new Vector2Int(coordinates.x, coordinates.y));
            return TimberbornTerrainAdapter.QuantizeSoilMoisture(_soilMoistureService.SoilMoisture(index));
        }
        catch
        {
            return 0;
        }
    }

    private byte ReadSoilContamination(Vector3Int coordinates)
    {
        if (_soilContaminationService is null || _mapIndexService is null)
        {
            return 0;
        }

        try
        {
            int index = _mapIndexService.CellToIndex(new Vector2Int(coordinates.x, coordinates.y));
            return TimberbornTerrainAdapter.QuantizeSoilContamination(
                _soilContaminationService.Contamination(index),
                _soilContaminationService.SoilIsContaminated(coordinates));
        }
        catch
        {
            return 0;
        }
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
