using Timberborn.SingletonSystem;
using Timberborn.QuickNotificationSystem;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.SelectionSystem;
using Timberborn.MapIndexSystem;
using Timberborn.MapStateSystem;
using Timberborn.TerrainSystem;
using Timberborn.SoilContaminationSystem;
using Timberborn.TimeSystem;
using Timberborn.WorldPersistence;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Runtime;

public sealed class TimberbornFireRuntime :
    ILoadableSingleton,
    IUnloadableSingleton,
    IUpdatableSingleton,
    ISaveableSingleton,
    ITimberbornQaCommandStateProvider,
    ITimberbornQaDeltaStimulus,
    ITimberbornQaBuildingBurnoutStimulus,
    ITimberbornQaWaterSuppressionStimulus,
    ITimberbornQaAshWaterStimulus,
    ITimberbornQaBurnDurationStimulus,
    ITimberbornQaFireSimParameterPresetSelector,
    ITimberbornQaAshCellProbe,
    ITimberbornQaInventoryAdjuster,
    ITimberbornQaStoredMaterialStimulus
{
    public const string FertileAshFieldGatherableTemplateName = "FertileAshField";

    private readonly ITimberbornFireLogSink _logSink;
    private readonly EntityRegistry _entityRegistry;
    private readonly TimberbornFireDebugVisualStateSink _debugVisualSink;
    private readonly TimberbornGpuFieldRendererSink _gpuFieldRenderer;
    private readonly TimberbornPlayerFireAlertSink _playerFireAlerts;
    private readonly TimberbornPlayerFireAlertCameraFocus _playerFireAlertCameraFocus;
    private readonly TimberbornBeaverFieldExposureTelemetry _beaverFieldExposureTelemetry;
    private readonly TimberbornBeaverFieldBehaviorDispatcher _beaverFieldBehaviorDispatcher;
    private readonly TimberbornSelectedTreeTargetProvider _selectedTreeTargetProvider;
    private readonly TimberbornSelectedCropTargetProvider _selectedCropTargetProvider;
    private readonly TimberbornAshFieldService _ashFieldService;
    private readonly TimberbornTaintedAshSoilPoisoningService _taintedAshSoilPoisoningService;
    private readonly TimberbornAshWaterWashoutService _ashWaterWashoutService;
    private readonly TimberbornFertileAshCollectionService _fertileAshCollectionService;
    private readonly ISingletonLoader _singletonLoader;
    private readonly ISoilContaminationService _soilContaminationService;
    private readonly IDayNightCycle _dayNightCycle;
    private readonly WildfireReleaseSettings _releaseSettings;
    private readonly TimberbornFireSimParameterPresetState _fireSimParameterPresetState;
    private readonly ITimberbornWindProvider _windProvider;
    private TimberbornRuntimeBindings? _bindings;
    private TimberbornQueuedFireSimHeatPulseSink? _explosiveInfrastructureHeatPulseSink;
    private TimberbornQueuedStoredGoodContaminationPulseSink? _storedGoodContaminationPulseSink;
    private TimberbornFireSystem? _fireSystem;
    private TimberbornFixedCadenceFireDispatcher? _dispatcher;
    private TimberbornGpuIndirectFireRenderer? _gpuIndirectRenderer;
    private TimberbornWorldCellImportSummary? _lastWorldImportSummary;
    private readonly TimberbornAshFieldSynchronizer _ashFieldSynchronizer;
    private TimberbornCompatibilityReport _compatibilityReport = TimberbornCompatibilityReport.Placeholder;
    private bool _compatibilityProbesRan;
    private FireGrid? _initializingGrid;
    private long _gameUpdateId;
    private bool _isLoaded;
    private readonly TimberbornRuntimePersistence _persistence = new();
    private readonly Dictionary<int, uint> _fertileAshHarvestWalkFailures = new();

    public TimberbornFireRuntime(
        ITimberbornGpuVisualFieldSurface visualFieldSurface,
        EntityRegistry entityRegistry,
        EntitySelectionService entitySelectionService,
        QuickNotificationService quickNotificationService,
        TimberbornPlayerFireAlertCameraFocus playerFireAlertCameraFocus,
        WildfireReleaseSettings releaseSettings,
        TimberbornFireSimParameterPresetState fireSimParameterPresetState,
        ITimberbornWindProvider windProvider,
        MapSize mapSize,
        ITerrainService terrainService,
        IBlockService blockService,
        ISoilContaminationService soilContaminationService,
        MapIndexService mapIndexService,
        IDayNightCycle dayNightCycle,
        ISingletonLoader singletonLoader)
    {
        _releaseSettings = releaseSettings ?? throw new ArgumentNullException(nameof(releaseSettings));
        _fireSimParameterPresetState = fireSimParameterPresetState ??
            throw new ArgumentNullException(nameof(fireSimParameterPresetState));
        _windProvider = windProvider ?? throw new ArgumentNullException(nameof(windProvider));
        _singletonLoader = singletonLoader ?? throw new ArgumentNullException(nameof(singletonLoader));
        _entityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
        _soilContaminationService = soilContaminationService ??
            throw new ArgumentNullException(nameof(soilContaminationService));
        _dayNightCycle = dayNightCycle ?? throw new ArgumentNullException(nameof(dayNightCycle));
        _playerFireAlertCameraFocus = playerFireAlertCameraFocus ??
            throw new ArgumentNullException(nameof(playerFireAlertCameraFocus));
        _logSink = new UnityTimberbornFireLogSink();
        _debugVisualSink = new TimberbornFireDebugVisualStateSink();
        WildfireReleaseVisualSettings visualSettings =
            WildfireReleaseVisualSettings.FromSnapshot(_releaseSettings.GetSnapshot());
        _gpuFieldRenderer = new TimberbornGpuFieldRendererSink(
            visualFieldSurface ?? throw new ArgumentNullException(nameof(visualFieldSurface)),
            _logSink,
            visualSettings.ToGpuFieldRendererOptions());
        _playerFireAlerts = new TimberbornPlayerFireAlertSink(
            new TimberbornQuickNotificationSink(quickNotificationService, _playerFireAlertCameraFocus),
            _logSink);
        _beaverFieldExposureTelemetry = new TimberbornBeaverFieldExposureTelemetry(
            new TimberbornEntityRegistryBeaverPositionProvider(_entityRegistry),
            visualFieldSurface,
            _logSink);
        _beaverFieldBehaviorDispatcher = new TimberbornBeaverFieldBehaviorDispatcher(
            new TimberbornWorkerSpeedBeaverFieldBehaviorActuator(
                new TimberbornEntityRegistryBeaverWorkerSpeedAdapter(_entityRegistry)),
            _logSink);
        _ashFieldService = new TimberbornAshFieldService(
            new TimberbornGrowableAshGrowthAdapter(
                blockService ?? throw new ArgumentNullException(nameof(blockService)),
                CurrentGrid,
                _logSink),
            _logSink);
        _ashFieldSynchronizer = new TimberbornAshFieldSynchronizer(_ashFieldService);
        _taintedAshSoilPoisoningService = new TimberbornTaintedAshSoilPoisoningService(
            new TimberbornSoilContaminationAshPoisoningAdapter(
                _soilContaminationService,
                CurrentGrid,
                mapIndexService ?? throw new ArgumentNullException(nameof(mapIndexService)),
                _logSink),
            _logSink);
        _ashWaterWashoutService = new TimberbornAshWaterWashoutService(
            UnavailableTimberbornAshWaterTaintAdapter.Instance,
            _logSink);
        _fertileAshCollectionService = new TimberbornFertileAshCollectionService(
            new TimberbornGathererPostFertileAshCollectionAdapter(_entityRegistry, CurrentGrid, _logSink),
            _logSink);
        EntitySelectionService selectionService =
            entitySelectionService ?? throw new ArgumentNullException(nameof(entitySelectionService));
        _selectedTreeTargetProvider = new TimberbornSelectedTreeTargetProvider(
            selectionService);
        _selectedCropTargetProvider = new TimberbornSelectedCropTargetProvider(selectionService);
    }

    internal TimberbornRuntimeInitialization Initialization { get; } = new();

    public TimberbornRuntimeInitializationState InitializationState => Initialization.State;

    public Exception? InitializationFailure => Initialization.Failure;

    public void Load()
    {
        Initialization.Unload();
        ResetRuntimeSession();
        Initialization.Load();
        _isLoaded = true;
        LoadPersistentState();
        RunCompatibilityProbesIfNeeded();
        _logSink.Info(
            $"wildfire_timberborn_adapter_started cadence_interval_ms={TimberbornFireCadence.Default.Interval.TotalMilliseconds:F0}");
        TimberbornCompatibilityRuntimeGate.LogLoadState(
            _compatibilityReport,
            _logSink,
            TimberbornFireCadence.Default);
    }

    public void Unload()
    {
        _logSink.Info(
            $"wildfire_timberborn_adapter_stopping game_update_id={_gameUpdateId} simulator_integrated={(_fireSystem is { IsInitialized: true }).ToString().ToLowerInvariant()}");
        Initialization.Unload();
        ResetRuntimeSession();
        _logSink.Info("wildfire_timberborn_adapter_stopped");
        _logSink.Info("wildfire_timberborn_runtime_unloaded");
    }

    private void ResetRuntimeSession()
    {
        _fireSystem?.Dispose();
        _gpuIndirectRenderer?.Dispose();
        _gpuIndirectRenderer = null;
        _explosiveInfrastructureHeatPulseSink?.Detach();
        _storedGoodContaminationPulseSink?.Detach();
        _gpuFieldRenderer.Clear();
        _playerFireAlerts.Clear();
        _playerFireAlertCameraFocus.Clear();
        _beaverFieldBehaviorDispatcher.Clear();
        _ashFieldService.Clear();
        _taintedAshSoilPoisoningService.Clear();
        _ashWaterWashoutService.Clear();
        _fertileAshCollectionService.Clear();
        _dispatcher = null;
        _fireSystem = null;
        _lastWorldImportSummary = null;
        _ashFieldSynchronizer.Clear();
        _initializingGrid = null;
        _bindings = null;
        _explosiveInfrastructureHeatPulseSink = null;
        _storedGoodContaminationPulseSink = null;
        _persistence.Reset();
        _fertileAshHarvestWalkFailures.Clear();
        _debugVisualSink.Clear();
        _compatibilityReport = TimberbornCompatibilityReport.Placeholder;
        _compatibilityProbesRan = false;
        _gameUpdateId = 0;
        _isLoaded = false;
    }

    public void Save(ISingletonSaver singletonSaver)
    {
        if (singletonSaver is null)
        {
            throw new ArgumentNullException(nameof(singletonSaver));
        }

        string? encoded = _persistence.EncodeForSave(InitializationState, CapturePersistentState);
        if (encoded is null)
        {
            _logSink.Info($"wildfire_timberborn_persistence_save_skipped initialization_state={InitializationState}");
            return;
        }

        singletonSaver
            .GetSingleton(TimberbornWildfirePersistenceKeys.Singleton)
            .Set(TimberbornWildfirePersistenceKeys.Snapshot, encoded);
        _logSink.Info(
            "wildfire_timberborn_persistence_saved " +
            $"initialization_state={InitializationState} " +
            $"preserved_original={(InitializationState != TimberbornRuntimeInitializationState.Ready).ToString().ToLowerInvariant()}");
    }

    public void UpdateSingleton()
    {
        if (InitializationState != TimberbornRuntimeInitializationState.Ready)
        {
            return;
        }

        if (_dispatcher is null)
        {
            _gpuIndirectRenderer?.OnUpdate();
            return;
        }

        _gameUpdateId++;
        TimeSpan elapsed = TimeSpan.FromSeconds(Math.Max(0d, Time.deltaTime));
        try
        {
            TimberbornFireDispatchResult result = _dispatcher.Update(new TimberbornFireUpdate(_gameUpdateId, elapsed));

            if (result.DidDispatch)
            {
                uint tick = result.Step?.Tick ?? _fireSystem?.LastTick ?? 0;
                SyncAshReadModelFromSimulator(tick);
                TryApplyAshWorldEffects(tick);
                TryDispatchBeaverFieldBehavior(tick);
                _logSink.Info(
                    $"wildfire_timberborn_runtime_dispatched game_update_id={_gameUpdateId} tick={result.Step?.Tick} delta_count={result.Step?.Deltas.Count}");
            }
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                $"wildfire_timberborn_runtime_dispatch_failed game_update_id={_gameUpdateId} message=\"{exception.Message}\"");
            throw;
        }

        _gpuIndirectRenderer?.OnUpdate();
    }

    private void TryDispatchBeaverFieldBehavior(uint tick)
    {
        if (_fireSystem is not { IsInitialized: true } fireSystem)
        {
            return;
        }

        try
        {
            TimberbornBeaverFieldExposureSnapshot beaverExposure = _beaverFieldExposureTelemetry.Sample(
                new FireGrid(fireSystem.Width!.Value, fireSystem.Height!.Value, fireSystem.Depth!.Value),
                tick);
            _beaverFieldBehaviorDispatcher.Dispatch(beaverExposure, tick);
            _playerFireAlerts.PublishBeaverBehavior(tick, _beaverFieldBehaviorDispatcher.Counters);
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                $"wildfire_timberborn_beaver_field_behavior_dispatch_failed tick={tick} message={TimberbornQaCommandBridge.FormatToken(exception.Message)}");
        }
    }

    private void TryApplyAshWorldEffects(uint tick)
    {
        try
        {
            IReadOnlyDictionary<int, TimberbornAshWaterContact> waterContacts = CurrentAshWaterContacts();
            _ashWaterWashoutService.Apply(tick, _ashFieldService.Entries, waterContacts, QueueWashedAshRemoval);
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                $"wildfire_timberborn_ash_water_washout_failed tick={tick} message={TimberbornQaCommandBridge.FormatToken(exception.Message)}");
        }

        try
        {
            _taintedAshSoilPoisoningService.Apply(tick, _ashFieldService.Entries);
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                $"wildfire_timberborn_tainted_ash_apply_failed tick={tick} message={TimberbornQaCommandBridge.FormatToken(exception.Message)}");
        }

        try
        {
            _fertileAshCollectionService.Apply(tick, _ashFieldService, QueueCollectedAshRemoval);
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                $"wildfire_timberborn_fertile_ash_apply_failed tick={tick} message={TimberbornQaCommandBridge.FormatToken(exception.Message)}");
        }

        try
        {
            _ashFieldService.ApplyDayDecay(tick, CurrentDayNumber(), QueueDecayedAshRemoval);
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                $"wildfire_timberborn_ash_decay_failed tick={tick} day={CurrentDayNumber()} message={TimberbornQaCommandBridge.FormatToken(exception.Message)}");
        }
    }

    private void SyncAshReadModelFromSimulator(uint tick)
    {
        _ashFieldSynchronizer.Sync(_fireSystem, tick, CurrentDayNumber());
    }

    internal void Initialize(
        FireGrid grid,
        IEnumerable<TimberbornCellSource> sources,
        ReadOnlySpan<WildfireMaterialField> companionFields,
        TimberbornWorldCellImportSummary worldImportSummary,
        ITimberbornFireSimulatorFactory simulatorFactory,
        TimberbornRuntimeBindings bindings,
        TimberbornFireCadence? cadence = null)
    {
        if (InitializationState != TimberbornRuntimeInitializationState.Initializing || _fireSystem is not null)
        {
            throw new InvalidOperationException("Runtime initialization must prepare a new session before readiness is published.");
        }

        RunCompatibilityProbesIfNeeded();
        TimberbornCompatibilityRuntimeGate.ThrowIfRequiredProbesFailed(_compatibilityReport, _logSink);
        TimberbornQueuedFireSimHeatPulseSink heatPulseSink = new(grid);
        TimberbornQueuedStoredGoodContaminationPulseSink contaminationPulseSink = new(grid);
        TimberbornFireSystem fireSystem = new(
            simulatorFactory,
            new TimberbornFireCellMapper(),
            _logSink,
            CreateDeltaConsumerSinks(bindings, grid, heatPulseSink, contaminationPulseSink));
        TimberbornGpuIndirectFireRenderer? renderer = null;
        _initializingGrid = grid;
        try
        {
            if (_persistence.LoadedSnapshot?.FireSim is { } fireSimSnapshot)
            {
                fireSystem.InitializeFromPersistentFireSimState(grid, sources, companionFields, fireSimSnapshot);
            }
            else
            {
                fireSystem.Initialize(grid, sources, companionFields);
            }

            if (!fireSystem.TryApplyPreset(_fireSimParameterPresetState.CurrentPreset))
            {
                throw new InvalidOperationException("The simulator cannot apply the selected fire preset.");
            }

            RestorePersistentConsequenceAndAshState(_persistence.LoadedSnapshot, bindings);
            renderer = PrepareRenderer(fireSystem, grid);
            heatPulseSink.Attach(fireSystem);
            contaminationPulseSink.Attach(fireSystem);
            _playerFireAlertCameraFocus.ConfigureGrid(grid);
            _gpuFieldRenderer.CompleteVisualEffectDispatch(fireSystem.LastTick ?? 0);
            TimberbornFixedCadenceFireDispatcher dispatcher = new(
                fireSystem, cadence ?? TimberbornFireCadence.Default, _logSink, IsAutoDispatchEnabled);
            LogPreparedBindings(cadence ?? TimberbornFireCadence.Default);
            _logSink.Info(
                $"wildfire_timberborn_runtime_simulator_initialized width={fireSystem.Width} height={fireSystem.Height} depth={fireSystem.Depth} {worldImportSummary.StatusToken}");

            // Commit only after every setup/restore operation has completed. All
            // access and dispatch remain gated by lifecycle Ready until we return.
            _bindings = bindings;
            _fireSystem = fireSystem;
            _gpuIndirectRenderer = renderer;
            _explosiveInfrastructureHeatPulseSink = heatPulseSink;
            _storedGoodContaminationPulseSink = contaminationPulseSink;
            _lastWorldImportSummary = worldImportSummary;
            _dispatcher = dispatcher;
            _gameUpdateId = 0;
            _persistence.ReleaseRestoredSnapshot();
        }
        catch (Exception initializationFailure)
        {
            CleanupFailedPreparation(initializationFailure,
                () => renderer?.Dispose(),
                fireSystem.Dispose,
                heatPulseSink.Detach,
                contaminationPulseSink.Detach,
                ClearPreparedWorldState);
            throw;
        }
        finally
        {
            _initializingGrid = null;
        }
    }

    private TimberbornGpuIndirectFireRenderer? PrepareRenderer(TimberbornFireSystem fireSystem, FireGrid grid)
    {
        if (fireSystem.Simulator is not TimberbornComputeFireSimulator computeSim)
        {
            return null;
        }

        WildfireReleaseVisualSettings visualSettings = WildfireReleaseVisualSettings.FromSnapshot(_releaseSettings.GetSnapshot());
        TimberbornGpuIndirectFireRenderer renderer = new(
            computeSim, grid, _logSink, _windProvider, visualSettings.ToGpuIndirectFireRendererOptions());
        try
        {
            renderer.Initialize();
            if (_persistence.LoadedSnapshot?.FireSim is not null)
            {
                renderer.SeedSmoothedFieldsFromRestoredBuffers(fireSystem.LastTick ?? 0);
            }

            return renderer;
        }
        catch (Exception preparationFailure)
        {
            CleanupFailedPreparation(preparationFailure, renderer.Dispose);
            throw;
        }
    }

    private static void CleanupFailedPreparation(Exception originalFailure, params Action[] cleanupSteps)
    {
        List<Exception> failures = new() { originalFailure };
        foreach (Action cleanup in cleanupSteps)
        {
            try
            {
                cleanup();
            }
            catch (Exception cleanupFailure)
            {
                failures.Add(cleanupFailure);
            }
        }

        if (failures.Count > 1)
        {
            throw new AggregateException("Runtime preparation and resource cleanup both failed.", failures);
        }
    }

    private void ClearPreparedWorldState()
    {
        _debugVisualSink.Clear();
        _gpuFieldRenderer.Clear();
        _playerFireAlerts.Clear();
        _playerFireAlertCameraFocus.Clear();
        _beaverFieldBehaviorDispatcher.Clear();
        _ashFieldService.Clear();
        _ashFieldSynchronizer.Clear();
    }

    public void RegisterHeat(int cellIndex, byte heat)
    {
        if (!TryAllowExternalChange("heat", 1))
        {
            return;
        }

        RequireFireSystem().RegisterHeat(cellIndex, heat);
    }

    public bool TryFindFertileAshFieldHarvestTarget(
        Vector3Int gathererCenter,
        int liftingCapacity,
        out TimberbornFertileAshFieldHarvestTarget target)
    {
        target = default;
        FireGrid? grid = CurrentGrid();
        if (!grid.HasValue)
        {
            return false;
        }

        int goodsToCollect = Math.Max(1, Math.Min(liftingCapacity, 1));
        uint tick = _fireSystem?.LastTick ?? 0;
        (int X, int Y, int Z) gridCenter = ToFireGridCoordinates(gathererCenter);
        TimberbornAshFieldEntry? entry = _ashFieldService.Entries.Values
            .Where(static candidate => candidate.Quality == WildfireAshQuality.Fertile && candidate.Strength > 0)
            .Where(candidate => IsFertileAshHarvestTargetEligible(candidate.CellIndex, tick))
            .Select(candidate => new
            {
                Entry = candidate,
                Coordinates = grid.Value.FromIndex(candidate.CellIndex),
            })
            .Where(candidate => TimberbornBoundedCellRange.IsWithinRange(
                gridCenter.X,
                gridCenter.Y,
                gridCenter.Z,
                candidate.Coordinates.X,
                candidate.Coordinates.Y,
                candidate.Coordinates.Z,
                grid.Value,
                TimberbornGathererPostFertileAshCollectionAdapter.MaxCollectionRangeCells))
            .OrderBy(candidate => TimberbornBoundedCellRange.DistanceSquared(
                gridCenter.X,
                gridCenter.Y,
                gridCenter.Z,
                candidate.Coordinates.X,
                candidate.Coordinates.Y,
                candidate.Coordinates.Z))
            .ThenBy(candidate => candidate.Entry.CellIndex)
            .Select(candidate => (TimberbornAshFieldEntry?)candidate.Entry)
            .FirstOrDefault();

        if (!entry.HasValue)
        {
            return false;
        }

        (int X, int Y, int Z) coordinates = grid.Value.FromIndex(entry.Value.CellIndex);
        int goodsAmount = Math.Min(goodsToCollect, entry.Value.Strength);
        target = new TimberbornFertileAshFieldHarvestTarget(
            entry.Value.CellIndex,
            StrengthToRemove: TimberbornFertileAshCollectionService.StrengthPerGood * goodsAmount,
            new GoodAmount(TimberbornAshFieldService.FertileAshGoodId, goodsAmount),
            new Vector3Int(coordinates.X, coordinates.Y, coordinates.Z),
            new Vector3(coordinates.X + 0.5f, coordinates.Z, coordinates.Y + 0.5f),
            TimberbornFertileAshFieldHarvestSource.SimulatorAshField);
        return true;
    }

    public void RecordFertileAshFieldHarvestWalkFailure(int cellIndex)
    {
        _fertileAshHarvestWalkFailures[cellIndex] = _fireSystem?.LastTick ?? 0;
    }

    private bool IsFertileAshHarvestTargetEligible(int cellIndex, uint tick)
    {
        if (!_fertileAshHarvestWalkFailures.TryGetValue(cellIndex, out uint failedTick))
        {
            return true;
        }

        if (tick <= failedTick + 60)
        {
            return false;
        }

        _fertileAshHarvestWalkFailures.Remove(cellIndex);
        return true;
    }

    public bool TryCompleteFertileAshFieldHarvest(
        TimberbornFertileAshFieldHarvestTarget target,
        out TimberbornAshFieldCollectionRemoval removal)
    {
        removal = _ashFieldService.CalculateCollectedFertileStrengthRemoval(target.CellIndex, target.StrengthToRemove);
        if (removal.StrengthRemoved <= 0)
        {
            return false;
        }

        QueueCollectedAshRemoval(new TimberbornFertileAshCollectedCell(
            target.CellIndex,
            removal.StrengthRemoved,
            target.GoodAmount.Amount));
        _fertileAshCollectionService.RecordWorkerHarvest(
            _fireSystem?.LastTick ?? 0,
            removal,
            target.GoodAmount.Amount);
        return true;
    }

    private static (int X, int Y, int Z) ToFireGridCoordinates(Vector3Int timberbornCoordinates)
    {
        return (timberbornCoordinates.x, timberbornCoordinates.y, timberbornCoordinates.z);
    }

    public void RegisterChange(FireSimChange change)
    {
        if (!TryAllowExternalChange("external", 1))
        {
            return;
        }

        RequireFireSystem().RegisterChange(change);
    }

    public int RegisterSustainedIgnitionChanges(IEnumerable<FireSimChange> changes, string source)
    {
        FireSimChange[] ignitionChanges = (changes ?? throw new ArgumentNullException(nameof(changes))).ToArray();
        if (!TryAllowExternalChange(source, ignitionChanges.Length))
        {
            return 0;
        }

        return RequireFireSystem().RegisterSustainedIgnitionChanges(ignitionChanges, source);
    }

    public void RegisterMappedCellChanges(IEnumerable<TimberbornCellSource> sources)
    {
        if (!TryAllowExternalChange("mapped_cell", null))
        {
            return;
        }

        RequireFireSystem().RegisterMappedCellChanges(sources);
    }

    public TimberbornQaDeltaStimulusResult QueueDeltaStimulus(string targetSelector)
    {
        string normalizedSelector = TimberbornQaFieldTargetSelectors.Normalize(targetSelector);
        TimberbornBeaverFieldExposureQaTarget? beaverExposureTarget =
            normalizedSelector is TimberbornQaFieldTargetSelectors.BeaverExposure or
                TimberbornQaFieldTargetSelectors.ToxicBeaverExposure
                ? _beaverFieldExposureTelemetry.SelectQaStimulusTarget(RequireFireSystem().RequireInitializedGrid())
                : null;
        TimberbornQaDeltaStimulusResult result = normalizedSelector == TimberbornQaFieldTargetSelectors.SelectedTree
            ? RequireFireSystem().Qa.QueueQaSelectedTreeDeltaStimulus(_selectedTreeTargetProvider)
            : RequireFireSystem().Qa.QueueQaDeltaStimulus(
                normalizedSelector,
                _bindings?.BurnDamageService?.States,
                _bindings?.ExplosiveInfrastructure,
                _bindings?.DetonatorSafety,
                _bindings?.TunnelFire,
                normalizedSelector is TimberbornQaFieldTargetSelectors.Default or
                    TimberbornQaFieldTargetSelectors.Crop or
                    TimberbornQaFieldTargetSelectors.Bush
                    ? FindOrRegisterSelectedCropTarget(RequireFireSystem().RequireInitializedGrid())
                    : null,
                beaverExposureTarget);
        if (normalizedSelector == TimberbornQaFieldTargetSelectors.ContaminatedTree)
        {
            _playerFireAlertCameraFocus.SetLatestFocusCell(result.CellIndex);
            _playerFireAlertCameraFocus.FocusLatestFireCell();
        }

        _logSink.Info(
            "wildfire_timberborn_qa_delta_stimulus_queued " +
            $"target_selector={result.TargetSelector} " +
            $"cell_index={result.CellIndex} " +
            $"x={result.X} " +
            $"y={result.Y} " +
            $"z={result.Z} " +
            $"target_material={result.MaterialClass} " +
            $"companion_target_id={result.CompanionTargetId} " +
            $"initial_cell={result.InitialCell} " +
            $"set_heat={result.SetHeat} " +
            $"queued_heat_changes={result.QueuedHeatChangeCount} " +
            $"set_smoke={FormatNumber(result.SetSmoke)} " +
            $"set_smoke_contamination={FormatNumber(result.SetSmokeContamination)} " +
            $"queued_smoke_changes={FormatNumber(result.QueuedSmokeChangeCount)} " +
            $"burn_damage_target_key={TimberbornQaCommandBridge.FormatToken(result.BurnDamageTargetKey)} " +
            $"burn_damage_spec_id={TimberbornQaCommandBridge.FormatToken(result.BurnDamageSpecId)} " +
            $"burn_damage_target_kind={TimberbornQaCommandBridge.FormatToken(result.BurnDamageTargetKind?.ToString())} " +
            $"burn_damage_remaining_capacity={FormatNumber(result.BurnDamageRemainingCapacity)} " +
            $"burn_damage_probe_fuel={FormatNumber(result.BurnDamageProbeFuel)} " +
            $"burn_damage_spend_fuel={FormatNumber(result.BurnDamageSpendFuel)} " +
            $"direct_target_kind={TimberbornQaCommandBridge.FormatToken(result.DirectTargetKind)} " +
            $"direct_target_stable_id={TimberbornQaCommandBridge.FormatToken(result.DirectTargetStableId)} " +
            $"direct_target_scanned_cells={FormatNumber(result.DirectTargetScannedCellCount)} " +
            $"target_source={TimberbornQaCommandBridge.FormatToken(result.TargetSource)} " +
            $"registered_burn_damage_targets={FormatNumber(result.RegisteredBurnDamageTargetCount)} " +
            $"registered_crop_burn_targets={FormatNumber(result.RegisteredCropBurnTargetCount)} " +
            $"registered_crop_burn_owned_cells={FormatNumber(result.RegisteredCropBurnOwnedCellCount)} " +
            $"sustained_heat_requested_cycles={FormatNumber(result.SustainedHeatRequestedCycleCount)} " +
            $"sustained_heat_completed_cycles={FormatNumber(result.SustainedHeatCompletedCycleCount)} " +
            $"sustained_heat_remaining_cycles={FormatNumber(result.SustainedHeatRemainingCycleCount)} " +
            $"sustained_heat_queued_cycle={FormatNumber(result.SustainedHeatQueuedCycleNumber)} " +
            $"beaver_exposure_target_beaver_id={TimberbornQaCommandBridge.FormatToken(result.BeaverExposureTargetBeaverId)} " +
            $"beaver_exposure_target_beaver_x={FormatNumber(result.BeaverExposureTargetBeaverX)} " +
            $"beaver_exposure_target_beaver_y={FormatNumber(result.BeaverExposureTargetBeaverY)} " +
            $"beaver_exposure_target_beaver_z={FormatNumber(result.BeaverExposureTargetBeaverZ)} " +
            $"beaver_exposure_target_candidate_cells={FormatNumber(result.BeaverExposureTargetCandidateCells)} " +
            $"beaver_exposure_target_sampled_beavers={FormatNumber(result.BeaverExposureTargetSampledBeavers)} " +
            $"beaver_exposure_target_skipped_no_position_api={FormatNumber(result.BeaverExposureTargetSkippedNoPositionApi)} " +
            $"beaver_exposure_target_skipped_bounded_sampling={FormatNumber(result.BeaverExposureTargetSkippedBoundedSampling)}");

        return result;
    }

    private TimberbornQaSelectedCropTarget? FindOrRegisterSelectedCropTarget(FireGrid grid)
    {
        if (_bindings?.BurnDamageService is not { } burnDamageService)
        {
            return null;
        }

        try
        {
            return _selectedCropTargetProvider.FindSelectedTarget(grid, burnDamageService.States);
        }
        catch (InvalidOperationException) when (burnDamageService.States.Count == 0)
        {
            TimberbornLiveCropBurnDamageTargets selectedTargets =
                _selectedCropTargetProvider.CollectSelectedTargets(grid);
            if (selectedTargets.Registrations.Count == 0)
            {
                throw;
            }

            TimberbornBurnDamageRegistrationSummary registrationSummary = burnDamageService.RegisterTargets(
                grid,
                selectedTargets.Registrations,
                selectedTargets.Descriptors);
            _logSink.Info(
                "wildfire_timberborn_selected_crop_target_registered " +
                $"targets={registrationSummary.TargetCount} " +
                $"owned_cells={registrationSummary.OwnedCellCount}");

            return _selectedCropTargetProvider.FindSelectedTarget(grid, burnDamageService.States);
        }
    }

    public TimberbornQaBuildingBurnoutStimulusResult QueueBuildingBurnoutStimulus()
    {
        ITimberbornQaBuildingBurnoutStimulusTargetProvider targetProvider =
            _bindings?.BuildingBurnoutStimulus ??
            throw new InvalidOperationException(
                "QA building burnout stimulus requires a Timberborn pausable building target provider.");
        TimberbornQaBuildingBurnoutStimulusResult result =
            RequireFireSystem().Qa.QueueBuildingBurnoutQaStimulus(targetProvider);
        _logSink.Info(
            "wildfire_timberborn_qa_building_burnout_stimulus_queued " +
            $"cell_index={result.CellIndex} " +
            $"x={result.X} " +
            $"y={result.Y} " +
            $"z={result.Z} " +
            $"scanned_cells={result.ScannedCellCount} " +
            $"set_heat={result.SetHeat} " +
            $"set_fuel={result.SetFuel} " +
            $"queued_field_changes={result.QueuedFieldChangeCount}");

        return result;
    }

    public TimberbornQaWaterSuppressionStimulusResult QueueWaterSuppressionStimulus(string targetSelector)
    {
        TimberbornQaWaterSuppressionStimulusResult result =
            RequireFireSystem().Qa.QueueWaterSuppressionQaStimulus(targetSelector);
        _logSink.Info(
            "wildfire_timberborn_qa_water_suppression_queued " +
            $"target_selector={result.TargetSelector} " +
            $"cell_index={result.CellIndex} " +
            $"x={result.X} " +
            $"y={result.Y} " +
            $"z={result.Z} " +
            $"target_material={result.MaterialClass} " +
            $"companion_target_id={result.CompanionTargetId} " +
            $"target_soil_contamination={result.TargetSoilContamination} " +
            $"affected_cell_contaminated={result.IsAffectedCellContaminated.ToString().ToLowerInvariant()} " +
            $"contaminated_suppression_input={result.IsContaminatedSuppressionInput.ToString().ToLowerInvariant()} " +
            $"badwater_suppression_input={result.IsBadwaterSuppressionInput.ToString().ToLowerInvariant()} " +
            $"initial_cell={result.InitialCell} " +
            $"set_water={result.SetWater} " +
            $"queued_water_changes={result.QueuedWaterChangeCount}");

        return result;
    }

    public TimberbornQaAshWaterStimulusResult QueueAshWaterStimulus(string target)
    {
        TimberbornQaAshWaterStimulusResult result =
            RequireFireSystem().Qa.QueueAshWaterQaStimulus(target);
        _logSink.Info(
            "wildfire_timberborn_qa_ash_water_stimulus_queued " +
            $"target={result.Target} " +
            $"ash_quality={result.AshQuality} " +
            $"cell_index={result.CellIndex} " +
            $"x={result.X} " +
            $"y={result.Y} " +
            $"z={result.Z} " +
            $"target_material={result.MaterialClass} " +
            $"companion_target_id={result.CompanionTargetId} " +
            $"initial_cell={result.InitialCell} " +
            $"set_ash={result.SetAsh} " +
            $"set_ash_contamination={result.SetAshContamination} " +
            $"set_water={result.SetWater} " +
            $"queued_ash_changes={result.QueuedAshChangeCount} " +
            $"queued_water_changes={result.QueuedWaterChangeCount} " +
            $"expected_water_taint_attempts={result.ExpectedWaterTaintAttemptCount}");

        return result;
    }

    public TimberbornQaBurnDurationStimulusResult QueueBurnDurationStimulus(string target)
    {
        TimberbornQaBurnDurationStimulusResult result =
            RequireFireSystem().Qa.QueueBurnDurationQaStimulus(target);
        _logSink.Info(
            "wildfire_timberborn_qa_burn_duration_stimulus_queued " +
            $"target={result.Target} " +
            $"cell_index={result.CellIndex} " +
            $"x={result.X} " +
            $"y={result.Y} " +
            $"z={result.Z} " +
            $"target_material={result.MaterialClass} " +
            $"companion_target_id={result.CompanionTargetId} " +
            $"initial_cell={result.InitialCell} " +
            $"initial_fuel={result.InitialFuel} " +
            $"set_heat={result.SetHeat} " +
            $"timeout_ticks={result.TimeoutTicks} " +
            $"sustained_heat_ticks={result.SustainedHeatTicks} " +
            $"queued_heat_changes={result.QueuedHeatChangeCount}");

        return result;
    }

    public TimberbornQaFireSimParameterPresetResult SelectFireSimParameterPreset(string presetName)
    {
        TimberbornQaFireSimParameterPresetResult result =
            _fireSimParameterPresetState.SelectFireSimParameterPreset(presetName);
        _logSink.Info(
            "wildfire_timberborn_fire_sim_preset_selected " +
            $"preset={TimberbornQaCommandBridge.FormatToken(result.Name)} " +
            $"ignition={result.Parameters.IgnitionPoint} " +
            $"water_ignition_penalty={result.Parameters.FireWaterIgnitionPenalty}");
        if (_fireSystem is not null)
        {
            bool applied = _fireSystem.TryApplyPreset(_fireSimParameterPresetState.CurrentPreset);
            _logSink.Info(
                "wildfire_timberborn_fire_sim_preset_live_update " +
                $"preset={TimberbornQaCommandBridge.FormatToken(result.Name)} " +
                $"applied={applied.ToString().ToLowerInvariant()}");
        }

        return result;
    }

    public TimberbornQaInventoryAdjustmentResult AdjustInventory(string profile)
    {
        if (_bindings?.InventoryAdjuster is not { } inventoryAdjuster)
        {
            throw new InvalidOperationException(
                "QA inventory adjustment is unavailable until the Timberborn fire runtime is initialized.");
        }

        TimberbornQaInventoryAdjustmentResult result = inventoryAdjuster.AdjustInventory(profile);
        _logSink.Info(
            "wildfire_timberborn_qa_inventory_adjusted " +
            $"profile={TimberbornQaCommandBridge.FormatToken(result.Profile)} " +
            $"targets_scanned={result.TargetsScanned} " +
            $"targets_adjusted={result.TargetsAdjusted} " +
            $"explosives_added={result.ExplosivesAdded} " +
            $"badwater_added={result.BadwaterAdded} " +
            $"fertile_ash_added={result.FertileAshAdded} " +
            $"logs_added={result.LogsAdded}");

        return result;
    }

    public TimberbornQaStoredMaterialStimulusResult QueueStoredMaterialStimulus(string target)
    {
        string normalizedTarget = TimberbornQaStoredMaterialStimulusTargets.Normalize(target);
        TimberbornFireSystem fireSystem = RequireFireSystem();
        FireGrid grid = fireSystem.RequireInitializedGrid();
        TimberbornQaStoredMaterialInventoryTarget[] targets =
            TimberbornQaInventoryAdjustmentApi.FindStoredMaterialTargets(_entityRegistry, grid, normalizedTarget);
        if (targets.Length == 0)
        {
            throw new InvalidOperationException(
                $"No stocked stored-material inventory targets were found for QA target '{normalizedTarget}'. Run qa-adjust-inventory stored-materials first and keep the loaded save command-responsive.");
        }

        int[] queuedCellIndices = targets
            .SelectMany(static storedTarget => storedTarget.CellIndices)
            .Distinct()
            .OrderBy(static cellIndex => cellIndex)
            .ToArray();
        if (!TryAllowExternalChange("qa_stored_material_stimulus", queuedCellIndices.Length))
        {
            return new TimberbornQaStoredMaterialStimulusResult(
                normalizedTarget,
                SetHeat: 15,
                QueuedHeatChangeCount: 0,
                Targets: Array.Empty<TimberbornQaStoredMaterialStimulusQueuedTarget>());
        }

        queuedCellIndices
            .Select(static cellIndex => new FireSimChange(CellIndex: cellIndex, SetHeat: 15))
            .ToList()
            .ForEach(change => fireSystem.RegisterChange(change));

        TimberbornQaStoredMaterialStimulusQueuedTarget[] queuedTargets = targets
            .Select(storedTarget =>
            {
                int cellIndex = storedTarget.CellIndices.First();
                (int x, int y, int z) = grid.FromIndex(cellIndex);
                return new TimberbornQaStoredMaterialStimulusQueuedTarget(
                    storedTarget.TargetKey,
                    storedTarget.TargetSpecId,
                    storedTarget.GoodId,
                    storedTarget.StockBefore,
                    cellIndex,
                    x,
                    y,
                    z,
                    storedTarget.CellIndices.Count);
            })
            .ToArray();

        TimberbornQaStoredMaterialStimulusResult result = new(
            normalizedTarget,
            SetHeat: 15,
            QueuedHeatChangeCount: queuedCellIndices.Length,
            queuedTargets);
        TimberbornQaStoredMaterialStimulusQueuedTarget primaryTarget = queuedTargets[0];
        _logSink.Info(
            "wildfire_timberborn_qa_stored_material_stimulus_queued " +
            $"target={TimberbornQaCommandBridge.FormatToken(result.Target)} " +
            $"target_count={result.Targets.Count} " +
            $"target_key={TimberbornQaCommandBridge.FormatToken(primaryTarget.TargetKey)} " +
            $"target_spec_id={TimberbornQaCommandBridge.FormatToken(primaryTarget.TargetSpecId)} " +
            $"target_good_id={TimberbornQaCommandBridge.FormatToken(primaryTarget.GoodId)} " +
            $"target_stock_before={primaryTarget.StockBefore} " +
            $"cell_index={primaryTarget.CellIndex} " +
            $"x={primaryTarget.X} " +
            $"y={primaryTarget.Y} " +
            $"z={primaryTarget.Z} " +
            $"set_heat={result.SetHeat} " +
            $"queued_heat_changes={result.QueuedHeatChangeCount}");

        return result;
    }

    public TimberbornQaCommandState GetState()
    {
        TimberbornFireSimParameterPreset currentPreset = _fireSimParameterPresetState.CurrentPreset;
        WildfireReleaseSettingsSnapshot releaseSnapshot = _releaseSettings.GetSnapshot();

        if (InitializationState != TimberbornRuntimeInitializationState.Ready ||
            _fireSystem is not { IsInitialized: true } fireSystem)
        {
            return new TimberbornQaCommandState(
                IsSimulatorIntegrated: false,
                IsGameContextRuntimeLoaded: _isLoaded,
                WildfireEnabled: releaseSnapshot.IsWildfireEnabled,
                VisualIntensityPercent: releaseSnapshot.VisualIntensityPercent,
                VisualDebugVisibility: releaseSnapshot.VisualDebugVisibility.ToString().ToLowerInvariant(),
                VisualDebugOverlayEnabled: releaseSnapshot.IsVisualDebugOverlayEnabled,
                CompatibilityProbeStatus: _compatibilityReport.StatusToken,
                CompatibilityProbeDegraded: _compatibilityReport.IsDegraded,
                CompatibilityProbeRequiredPassed: _compatibilityReport.PassedRequiredProbeCount,
                CompatibilityProbeRequiredTotal: _compatibilityReport.RequiredProbeCount,
                CompatibilityProbeOptionalPassed: _compatibilityReport.PassedOptionalProbeCount,
                CompatibilityProbeOptionalTotal: _compatibilityReport.OptionalProbeCount,
                CompatibilityProbeDegradedFeatures: _compatibilityReport.DegradedFeatureToken,
                FireSimPresetName: currentPreset.Name,
                FireSimPresetIgnitionPoint: currentPreset.Parameters.IgnitionPoint,
                FireSimPresetWaterIgnitionPenalty: currentPreset.Parameters.FireWaterIgnitionPenalty,
                FireSimPresetFuelHeatWeight: currentPreset.Parameters.FireFuelHeatWeight,
                FireSimPresetFuelBurnDownNumerator: currentPreset.Parameters.FireFuelBurnDownPressureNumerator,
                FireSimPresetFuelBurnDownDenominator: currentPreset.Parameters.FireFuelBurnDownPressureDenominator,
                SustainedIgnitionDispatchTicks: currentPreset.SustainedIgnitionDispatchTicks,
                WorldImportTotalSources: _lastWorldImportSummary?.TotalSources,
                WorldImportTerrainSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Terrain),
                WorldImportVegetationSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Vegetation),
                WorldImportTreeSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Tree),
                WorldImportCropSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Crop),
                WorldImportBuildingSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Building),
                WorldImportStorageSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Storage),
                WorldImportInfrastructureSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Infrastructure),
                WorldImportWaterSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Water),
                WorldImportBadwaterSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Badwater),
                WorldImportResolvedEmptyCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Empty),
                WorldImportResolvedTerrainCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Terrain),
                WorldImportResolvedVegetationCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Vegetation),
                WorldImportResolvedTreeCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Tree),
                WorldImportResolvedCropCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Crop),
                WorldImportResolvedBuildingCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Building),
                WorldImportResolvedStorageCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Storage),
                WorldImportResolvedInfrastructureCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Infrastructure),
                WorldImportResolvedWaterCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Water),
                WorldImportResolvedBadwaterCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Badwater),
                PersistentRestoreNoLiveFuelCellsCleared: 0);
        }

        TimberbornFireDeltaConsumerSummary deltaConsumerSummary = fireSystem.LastDeltaConsumerSummary;
        TimberbornContaminationFireConsequenceSummary contaminationFireSummary =
            fireSystem.ContaminationFireSummary;
        TimberbornGpuVisualFieldSurfaceState visualFieldSurfaceState = fireSystem.VisualFieldSurfaceState;
        TimberbornSmokeHeightTelemetry smokeHeightTelemetry =
            visualFieldSurfaceState.SmokeHeightTelemetry ?? TimberbornSmokeHeightTelemetry.Empty;
        TimberbornGpuFieldRendererCounters gpuFieldRendererCounters = _gpuFieldRenderer.Counters;
        TimberbornPlayerFireAlertCounters alertCounters = _playerFireAlerts.Counters;
        TimberbornQaBurnDurationProofState burnDurationProof = fireSystem.Qa.BurnDurationProofState;
        TimberbornBeaverFieldExposureSnapshot beaverExposure = _beaverFieldExposureTelemetry.LastSnapshot;
        TimberbornBeaverFieldBehaviorCounters beaverFieldBehaviorCounters = _beaverFieldBehaviorDispatcher.Counters;
        TimberbornBurnDamageRegistrationSummary burnDamageRegistrationSummary =
            _bindings?.BurnDamageService?.LastRegistrationSummary ?? TimberbornBurnDamageRegistrationSummary.Empty;
        TimberbornCropBurnTargetRegistrationSummary cropBurnSummary =
            TimberbornCropBurnTargetClassifier.SummarizeRegisteredTargets(
                _bindings?.BurnDamageService?.States.Values ?? Array.Empty<TimberbornBurnDamageTargetState>());
        TimberbornTreeBurnTargetRegistrationSummary treeBurnSummary =
            TimberbornTreeBurnTargetClassifier.SummarizeRegisteredTargets(
                _bindings?.BurnDamageService?.States.Values ?? Array.Empty<TimberbornBurnDamageTargetState>());
        TimberbornQaDeltaStimulusSustainedHeatState? sustainedHeatState =
            fireSystem.Qa.QaDeltaStimulusSustainedHeatState;
        TimberbornSelectedCropTargetDiagnostics selectedCropDiagnostics =
            _selectedCropTargetProvider.LastDiagnostics;
        SyncAshReadModelFromSimulator(fireSystem.LastTick ?? 0);
        TimberbornAshFieldSummary ashFieldSummary = _ashFieldService.LastSummary;
        TimberbornTaintedAshSoilPoisoningSummary taintedAshSummary =
            _taintedAshSoilPoisoningService.LastSummary;
        TimberbornAshWaterWashoutSummary ashWaterWashoutSummary =
            _ashWaterWashoutService.LastSummary;
        TimberbornFertileAshCollectionSummary fertileAshCollectionSummary =
            _fertileAshCollectionService.LastSummary;

        return new TimberbornQaCommandState(
            IsSimulatorIntegrated: true,
            IsGameContextRuntimeLoaded: _isLoaded,
            WildfireEnabled: releaseSnapshot.IsWildfireEnabled,
            VisualIntensityPercent: releaseSnapshot.VisualIntensityPercent,
            VisualDebugVisibility: releaseSnapshot.VisualDebugVisibility.ToString().ToLowerInvariant(),
            VisualDebugOverlayEnabled: releaseSnapshot.IsVisualDebugOverlayEnabled,
            Width: fireSystem.Width,
            Height: fireSystem.Height,
            Depth: fireSystem.Depth,
            TickCount: fireSystem.LastTick,
            QueuedChangeCount: fireSystem.RegisteredChangeCountSinceLastDispatch,
            LastDeltaCount: fireSystem.LastDeltaCount,
            LastDeltaConsumerChangedCellCount: deltaConsumerSummary.ChangedCellCount,
            LastDeltaConsumerDebugVisualUpdatedCellCount: deltaConsumerSummary.DebugVisualUpdatedCellCount,
            LastDeltaConsumerDebugVisualCellCount: _debugVisualSink.States.Count,
            LastDeltaConsumerStartedBurningCount: deltaConsumerSummary.StartedBurningCount,
            LastDeltaConsumerFuelDepletedCount: deltaConsumerSummary.FuelDepletedCount,
            LastDeltaConsumerWaterChangedCount: deltaConsumerSummary.WaterChangedCount,
            LastPositiveWaterChangedTick: fireSystem.LastPositiveWaterChangedTick,
            LastPositiveWaterChangedCount: fireSystem.LastPositiveWaterChangedCount,
            LastDeltaConsumerVisualEffectEventCount: deltaConsumerSummary.VisualEffectEventCount,
            LastDeltaConsumerVisualEffectFailureCount: deltaConsumerSummary.VisualEffectFailureCount,
            LastDeltaConsumerGameplayConsequenceCount: deltaConsumerSummary.GameplayConsequenceCount,
            LastDeltaConsumerBuildingBurnoutConsideredDeltaCount: deltaConsumerSummary.BuildingBurnoutConsideredDeltaCount,
            LastDeltaConsumerBuildingBurnoutMatchedCellCount: deltaConsumerSummary.BuildingBurnoutMatchedCellCount,
            LastDeltaConsumerBuildingBurnoutAppliedConsequenceCount: deltaConsumerSummary.BuildingBurnoutAppliedConsequenceCount,
            BurnDamageRegisteredTargetCount: burnDamageRegistrationSummary.TargetCount,
            BurnDamageRegisteredOwnedCellCount: burnDamageRegistrationSummary.OwnedCellCount,
            BurnDamageRegisteredUnknownSpecCount: burnDamageRegistrationSummary.UnknownSpecCount,
            BurnDamageRegisteredMissingResourceCount: burnDamageRegistrationSummary.MissingResourceCount,
            BurnDamageRegisteredTotalCapacity: burnDamageRegistrationSummary.TotalDamageCapacity,
            BurnDamageRegisteredZeroCapacityTargetCount: burnDamageRegistrationSummary.ZeroCapacityTargetCount,
            BurnDamageRegisteredCropBurnTargetCount: cropBurnSummary.TargetCount,
            BurnDamageRegisteredCropBurnOwnedCellCount: cropBurnSummary.OwnedCellCount,
            BurnDamageRegisteredTreeBurnTargetCount: treeBurnSummary.TargetCount,
            BurnDamageRegisteredTreeBurnOwnedCellCount: treeBurnSummary.OwnedCellCount,
            QaDeltaStimulusTargetCellIndex: sustainedHeatState?.CellIndex,
            QaDeltaStimulusTargetX: sustainedHeatState?.X,
            QaDeltaStimulusTargetY: sustainedHeatState?.Y,
            QaDeltaStimulusTargetZ: sustainedHeatState?.Z,
            QaDeltaStimulusTargetSource: sustainedHeatState?.TargetSource,
            QaDeltaStimulusSustainedHeatSetCell: sustainedHeatState?.SetCell,
            QaDeltaStimulusSustainedHeatRequestedCycleCount: sustainedHeatState?.RequestedCycleCount,
            QaDeltaStimulusSustainedHeatCompletedCycleCount: sustainedHeatState?.CompletedCycleCount,
            QaDeltaStimulusSustainedHeatRemainingCycleCount: sustainedHeatState?.RemainingCycleCount,
            QaDeltaStimulusSustainedHeatQueuedCycleNumber: sustainedHeatState?.QueuedCycleNumber,
            QaDeltaStimulusSustainedHeatLastCompletedTick: sustainedHeatState?.LastCompletedTick,
            QaDeltaStimulusSustainedHeatActive: sustainedHeatState?.IsActive ?? false,
            SelectedCropTargetSelectionState: selectedCropDiagnostics.SelectionState,
            SelectedCropTargetObjectType: selectedCropDiagnostics.SelectedObjectType,
            SelectedCropTargetObjectName: selectedCropDiagnostics.SelectedObjectName,
            SelectedCropTargetBlockObjectName: selectedCropDiagnostics.BlockObjectName,
            SelectedCropTargetComponentCount: selectedCropDiagnostics.ComponentCount,
            SelectedCropTargetComponentTypes: selectedCropDiagnostics.ComponentTypes,
            SelectedCropTargetOccupiedCellCount: selectedCropDiagnostics.OccupiedCellCount,
            SelectedCropTargetOccupiedInGridCellCount: selectedCropDiagnostics.OccupiedInGridCellCount,
            SelectedCropTargetYieldDebug: selectedCropDiagnostics.YieldDebug,
            SelectedCropTargetFailureReason: selectedCropDiagnostics.FailureReason,
            LastDeltaConsumerCropBurnConsideredTargetCount: deltaConsumerSummary.CropBurnConsideredTargetCount,
            LastDeltaConsumerCropBurnBurnableTargetCount: deltaConsumerSummary.CropBurnBurnableTargetCount,
            LastDeltaConsumerCropBurnYieldLost: deltaConsumerSummary.CropBurnYieldLost,
            LastDeltaConsumerCropBurnKilledCropCount: deltaConsumerSummary.CropBurnKilledCropCount,
            LastDeltaConsumerCropBurnVisualStateUpdateCount: deltaConsumerSummary.CropBurnVisualStateUpdateCount,
            LastDeltaConsumerCropBurnDuplicateCellSuppressedCount: deltaConsumerSummary.CropBurnDuplicateCellSuppressedCount,
            LastDeltaConsumerCropBurnUnmappedTargetCount: deltaConsumerSummary.CropBurnUnmappedTargetCount,
            LastDeltaConsumerCropBurnUnknownHarvestResourceCount: deltaConsumerSummary.CropBurnUnknownHarvestResourceCount,
            LastDeltaConsumerCropBurnNonBurnableTargetCount: deltaConsumerSummary.CropBurnNonBurnableTargetCount,
            LastDeltaConsumerCropBurnFailedConsequenceCount: deltaConsumerSummary.CropBurnFailedConsequenceCount,
            LastDeltaConsumerTreeBurnConsideredTargetCount: deltaConsumerSummary.TreeBurnConsideredTargetCount,
            LastDeltaConsumerTreeBurnBurnableTargetCount: deltaConsumerSummary.TreeBurnBurnableTargetCount,
            LastDeltaConsumerTreeBurnYieldLost: deltaConsumerSummary.TreeBurnYieldLost,
            LastDeltaConsumerTreeBurnKilledTreeCount: deltaConsumerSummary.TreeBurnKilledTreeCount,
            LastDeltaConsumerTreeBurnVisualStateUpdateCount: deltaConsumerSummary.TreeBurnVisualStateUpdateCount,
            LastDeltaConsumerTreeBurnDuplicateCellSuppressedCount: deltaConsumerSummary.TreeBurnDuplicateCellSuppressedCount,
            LastDeltaConsumerTreeBurnUnmappedTargetCount: deltaConsumerSummary.TreeBurnUnmappedTargetCount,
            LastDeltaConsumerTreeBurnUnknownCuttableResourceCount: deltaConsumerSummary.TreeBurnUnknownCuttableResourceCount,
            LastDeltaConsumerTreeBurnNonBurnableTargetCount: deltaConsumerSummary.TreeBurnNonBurnableTargetCount,
            LastDeltaConsumerTreeBurnFailedConsequenceCount: deltaConsumerSummary.TreeBurnFailedConsequenceCount,
            LastDeltaConsumerStructureBurnDamageRollbackConsideredDeltaCount: deltaConsumerSummary.StructureBurnDamageRollbackConsideredDeltaCount,
            LastDeltaConsumerStructureBurnDamageRollbackMatchedStructureCellCount: deltaConsumerSummary.StructureBurnDamageRollbackMatchedStructureCellCount,
            LastDeltaConsumerStructureBurnDamageRollbackDuplicateStructureTargetSuppressedCount: deltaConsumerSummary.StructureBurnDamageRollbackDuplicateStructureTargetSuppressedCount,
            LastDeltaConsumerStructureBurnDamageRollbackZeroBurnableCapacityTargetCount: deltaConsumerSummary.StructureBurnDamageRollbackZeroBurnableCapacityTargetCount,
            LastDeltaConsumerStructureBurnDamageRollbackMaterialValueLost: deltaConsumerSummary.StructureBurnDamageRollbackMaterialValueLost,
            LastDeltaConsumerStructureBurnDamageRollbackClosedStructureCount: deltaConsumerSummary.StructureBurnDamageRollbackClosedStructureCount,
            LastDeltaConsumerStructureBurnDamageRollbackRepairBlockedCount: deltaConsumerSummary.StructureBurnDamageRollbackRepairBlockedCount,
            LastDeltaConsumerStructureBurnDamageRollbackRepairEligibleCount: deltaConsumerSummary.StructureBurnDamageRollbackRepairEligibleCount,
            LastDeltaConsumerStructureBurnDamageRollbackScorchedStageCount: deltaConsumerSummary.StructureBurnDamageRollbackScorchedStageCount,
            LastDeltaConsumerStructureBurnDamageRollbackPartialConstructionStageCount: deltaConsumerSummary.StructureBurnDamageRollbackPartialConstructionStageCount,
            LastDeltaConsumerStructureBurnDamageRollbackUnfinishedStageCount: deltaConsumerSummary.StructureBurnDamageRollbackUnfinishedStageCount,
            LastDeltaConsumerStructureBurnDamageRollbackVisualRollbackAppliedCount: deltaConsumerSummary.StructureBurnDamageRollbackVisualRollbackAppliedCount,
            LastDeltaConsumerStructureBurnDamageRollbackConstructionPhaseEnteredCount: deltaConsumerSummary.StructureBurnDamageRollbackConstructionPhaseEnteredCount,
            LastDeltaConsumerStructureBurnDamageRollbackTotalDamageApplied: deltaConsumerSummary.StructureBurnDamageRollbackTotalDamageApplied,
            LastDeltaConsumerBurnDamageConsideredCellCount: deltaConsumerSummary.BurnDamageConsideredCellCount,
            LastDeltaConsumerBurnDamageDamageCandidateCellCount: deltaConsumerSummary.BurnDamageDamageCandidateCellCount,
            LastDeltaConsumerBurnDamageResolvedTargetCellCount: deltaConsumerSummary.BurnDamageResolvedTargetCellCount,
            LastDeltaConsumerBurnDamageUnresolvedCellCount: deltaConsumerSummary.BurnDamageUnresolvedCellCount,
            LastDeltaConsumerBurnDamageDuplicateCellSuppressedCount: deltaConsumerSummary.BurnDamageDuplicateCellSuppressedCount,
            LastDeltaConsumerBurnDamageAppliedTargetCount: deltaConsumerSummary.BurnDamageAppliedTargetCount,
            LastDeltaConsumerBurnDamageTotalDamageApplied: deltaConsumerSummary.BurnDamageTotalDamageApplied,
            LastDeltaConsumerBurnDamagePersistenceWriteCount: deltaConsumerSummary.BurnDamagePersistenceWriteCount,
            LastPositiveBuildingBurnoutAppliedTick: fireSystem.LastPositiveBuildingBurnoutAppliedTick,
            LastPositiveBuildingBurnoutAppliedCount: fireSystem.LastPositiveBuildingBurnoutAppliedCount,
            LastPositiveBurnDamageAppliedTick: fireSystem.LastPositiveBurnDamageAppliedTick,
            LastPositiveBurnDamageAppliedTargetCount: fireSystem.LastPositiveBurnDamageAppliedTargetCount,
            LastPositiveBurnDamageTotalDamageApplied: fireSystem.LastPositiveBurnDamageTotalDamageApplied,
            LastPositiveStructureBurnDamageRollbackTick: fireSystem.LastPositiveStructureBurnDamageRollbackTick,
            LastPositiveStructureBurnDamageRollbackUnfinishedStageCount: fireSystem.LastPositiveStructureBurnDamageRollbackUnfinishedStageCount,
            LastPositiveStructureBurnDamageRollbackConstructionPhaseEnteredCount: fireSystem.LastPositiveStructureBurnDamageRollbackConstructionPhaseEnteredCount,
            LastPositiveStructureBurnDamageRollbackTotalDamageApplied: fireSystem.LastPositiveStructureBurnDamageRollbackTotalDamageApplied,
            LastDeltaConsumerStoredGoodBurnConsideredDeltaCount: deltaConsumerSummary.StoredGoodBurnConsideredDeltaCount,
            LastDeltaConsumerStoredGoodBurnMatchedStorageCellCount: deltaConsumerSummary.StoredGoodBurnMatchedStorageCellCount,
            LastDeltaConsumerStoredGoodBurnDuplicateStorageTargetSuppressedCount: deltaConsumerSummary.StoredGoodBurnDuplicateStorageTargetSuppressedCount,
            LastDeltaConsumerStoredGoodBurnableStackCount: deltaConsumerSummary.StoredGoodBurnableStackCount,
            LastDeltaConsumerStoredGoodBurnDestroyedItemCount: deltaConsumerSummary.StoredGoodBurnDestroyedItemCount,
            LastDeltaConsumerStoredGoodBurnHazardousGoodCount: deltaConsumerSummary.StoredGoodBurnHazardousGoodCount,
            LastDeltaConsumerStoredGoodBurnUnknownResourceCount: deltaConsumerSummary.StoredGoodBurnUnknownResourceCount,
            LastDeltaConsumerStoredGoodBurnSkippedNonBurnableItemCount: deltaConsumerSummary.StoredGoodBurnSkippedNonBurnableItemCount,
            LastDeltaConsumerExplosiveInfrastructureConsideredDeltaCount: deltaConsumerSummary.ExplosiveInfrastructureConsideredDeltaCount,
            LastDeltaConsumerExplosiveInfrastructureMatchedTargetCellCount: deltaConsumerSummary.ExplosiveInfrastructureMatchedTargetCellCount,
            LastDeltaConsumerExplosiveInfrastructureDuplicateTargetSuppressedCount: deltaConsumerSummary.ExplosiveInfrastructureDuplicateTargetSuppressedCount,
            LastDeltaConsumerExplosiveInfrastructureArmedTargetCount: deltaConsumerSummary.ExplosiveInfrastructureArmedTargetCount,
            LastDeltaConsumerExplosiveInfrastructureTriggeredTargetCount: deltaConsumerSummary.ExplosiveInfrastructureTriggeredTargetCount,
            LastDeltaConsumerExplosiveInfrastructureNativeTriggeredTargetCount: deltaConsumerSummary.ExplosiveInfrastructureNativeTriggeredTargetCount,
            LastDeltaConsumerExplosiveInfrastructureHeatPulseCellCount: deltaConsumerSummary.ExplosiveInfrastructureHeatPulseCellCount,
            LastDeltaConsumerExplosiveInfrastructureSkippedSettingDisabledCount: deltaConsumerSummary.ExplosiveInfrastructureSkippedSettingDisabledCount,
            LastDeltaConsumerExplosiveInfrastructureSkippedAlreadyTriggeredCount: deltaConsumerSummary.ExplosiveInfrastructureSkippedAlreadyTriggeredCount,
            LastDeltaConsumerExplosiveInfrastructureLastTriggeredDepth: deltaConsumerSummary.ExplosiveInfrastructureLastTriggeredDepth,
            LastDeltaConsumerDetonatorFireSafetyConsideredDeltaCount: deltaConsumerSummary.DetonatorFireSafetyConsideredDeltaCount,
            LastDeltaConsumerDetonatorFireSafetyMatchedTargetCellCount: deltaConsumerSummary.DetonatorFireSafetyMatchedTargetCellCount,
            LastDeltaConsumerDetonatorFireSafetyDuplicateTargetSuppressedCount: deltaConsumerSummary.DetonatorFireSafetyDuplicateTargetSuppressedCount,
            LastDeltaConsumerDetonatorFireSafetyDisabledTargetCount: deltaConsumerSummary.DetonatorFireSafetyDisabledTargetCount,
            LastDeltaConsumerDetonatorFireSafetyArmedTargetCount: deltaConsumerSummary.DetonatorFireSafetyArmedTargetCount,
            LastDeltaConsumerDetonatorFireSafetySkippedSettingDisabledCount: deltaConsumerSummary.DetonatorFireSafetySkippedSettingDisabledCount,
            LastDeltaConsumerDetonatorFireSafetyRecoverabilityPreservedCount: deltaConsumerSummary.DetonatorFireSafetyRecoverabilityPreservedCount,
            LastDeltaConsumerDetonatorFireSafetyRecoverabilityUnknownCount: deltaConsumerSummary.DetonatorFireSafetyRecoverabilityUnknownCount,
            LastDeltaConsumerTunnelFireConsideredDeltaCount: deltaConsumerSummary.TunnelFireConsideredDeltaCount,
            LastDeltaConsumerTunnelFireMatchedTargetCellCount: deltaConsumerSummary.TunnelFireMatchedTargetCellCount,
            LastDeltaConsumerTunnelFireDuplicateTargetSuppressedCount: deltaConsumerSummary.TunnelFireDuplicateTargetSuppressedCount,
            LastDeltaConsumerTunnelFireUnstableTargetCount: deltaConsumerSummary.TunnelFireUnstableTargetCount,
            LastDeltaConsumerTunnelFireNativeExplodeAttemptedCount: deltaConsumerSummary.TunnelFireNativeExplodeAttemptedCount,
            LastDeltaConsumerTunnelFireNativeExplodeAppliedCount: deltaConsumerSummary.TunnelFireNativeExplodeAppliedCount,
            LastDeltaConsumerTunnelFireDestructionDeferredCount: deltaConsumerSummary.TunnelFireDestructionDeferredCount,
            LastDeltaConsumerTunnelFireSkippedSettingDisabledCount: deltaConsumerSummary.TunnelFireSkippedSettingDisabledCount,
            LastDeltaConsumerTunnelFireRecoverabilityPreservedCount: deltaConsumerSummary.TunnelFireRecoverabilityPreservedCount,
            LastDeltaConsumerTunnelFireRecoverabilityUnknownCount: deltaConsumerSummary.TunnelFireRecoverabilityUnknownCount,
            LastDeltaConsumerPathInfrastructureConsideredDeltaCount: deltaConsumerSummary.PathInfrastructureConsideredDeltaCount,
            LastDeltaConsumerPathInfrastructureMatchedTargetCellCount: deltaConsumerSummary.PathInfrastructureMatchedTargetCellCount,
            LastDeltaConsumerPathInfrastructureDuplicateTargetSuppressedCount: deltaConsumerSummary.PathInfrastructureDuplicateTargetSuppressedCount,
            LastDeltaConsumerPathInfrastructureZeroCostTargetCount: deltaConsumerSummary.PathInfrastructureZeroCostTargetCount,
            LastDeltaConsumerPathInfrastructureDamagedTargetCount: deltaConsumerSummary.PathInfrastructureDamagedTargetCount,
            LastDeltaConsumerPathInfrastructureBlockedTargetCount: deltaConsumerSummary.PathInfrastructureBlockedTargetCount,
            LastDeltaConsumerPathInfrastructureRepairEligibleTargetCount: deltaConsumerSummary.PathInfrastructureRepairEligibleTargetCount,
            LastDeltaConsumerPathInfrastructureTotalDamageApplied: deltaConsumerSummary.PathInfrastructureTotalDamageApplied,
            LastDeltaConsumerPowerInfrastructureConsideredDeltaCount: deltaConsumerSummary.PowerInfrastructureConsideredDeltaCount,
            LastDeltaConsumerPowerInfrastructureMatchedTargetCellCount: deltaConsumerSummary.PowerInfrastructureMatchedTargetCellCount,
            LastDeltaConsumerPowerInfrastructureDuplicateTargetSuppressedCount: deltaConsumerSummary.PowerInfrastructureDuplicateTargetSuppressedCount,
            LastDeltaConsumerPowerInfrastructureMetalOnlyNoOpTargetCount: deltaConsumerSummary.PowerInfrastructureMetalOnlyNoOpTargetCount,
            LastDeltaConsumerPowerInfrastructureDamagedTargetCount: deltaConsumerSummary.PowerInfrastructureDamagedTargetCount,
            LastDeltaConsumerPowerInfrastructureDisabledOrDisconnectedTargetCount: deltaConsumerSummary.PowerInfrastructureDisabledOrDisconnectedTargetCount,
            LastDeltaConsumerPowerInfrastructureRepairEligibleTargetCount: deltaConsumerSummary.PowerInfrastructureRepairEligibleTargetCount,
            LastDeltaConsumerPowerInfrastructureTotalDamageApplied: deltaConsumerSummary.PowerInfrastructureTotalDamageApplied,
            LastDeltaConsumerWaterInfrastructureConsideredDeltaCount: deltaConsumerSummary.WaterInfrastructureConsideredDeltaCount,
            LastDeltaConsumerWaterInfrastructureMatchedTargetCellCount: deltaConsumerSummary.WaterInfrastructureMatchedTargetCellCount,
            LastDeltaConsumerWaterInfrastructureDuplicateTargetSuppressedCount: deltaConsumerSummary.WaterInfrastructureDuplicateTargetSuppressedCount,
            LastDeltaConsumerWaterInfrastructureInertMaterialNoOpTargetCount: deltaConsumerSummary.WaterInfrastructureInertMaterialNoOpTargetCount,
            LastDeltaConsumerWaterInfrastructureDifficultToBurnNoOpTargetCount: deltaConsumerSummary.WaterInfrastructureDifficultToBurnNoOpTargetCount,
            LastDeltaConsumerWaterInfrastructureBurnableMaterialValue: deltaConsumerSummary.WaterInfrastructureBurnableMaterialValue,
            LastDeltaConsumerWaterInfrastructureDamagedTargetCount: deltaConsumerSummary.WaterInfrastructureDamagedTargetCount,
            LastDeltaConsumerWaterInfrastructureWaterStateMutationAttemptCount: deltaConsumerSummary.WaterInfrastructureWaterStateMutationAttemptCount,
            LastDeltaConsumerWaterInfrastructureRepairEligibleTargetCount: deltaConsumerSummary.WaterInfrastructureRepairEligibleTargetCount,
            LastDeltaConsumerWaterInfrastructureTotalDamageApplied: deltaConsumerSummary.WaterInfrastructureTotalDamageApplied,
            AshFieldEntries: _ashFieldService.Entries.Count,
            AshFieldFertileCells: ashFieldSummary.FertileAshCellCount,
            AshFieldSpentCells: ashFieldSummary.SpentAshCellCount,
            AshFieldTaintedCells: ashFieldSummary.TaintedAshCellCount,
            AshFieldContaminatedBurnSources: ashFieldSummary.ContaminatedBurnSourceCellCount,
            AshFieldContaminatedAffectedCells: ashFieldSummary.ContaminatedAffectedCellCount,
            AshFieldGrowthCandidateCells: ashFieldSummary.GrowthCandidateCellCount,
            AshFieldGrowthAppliedGrowables: ashFieldSummary.GrowthAppliedGrowableCount,
            AshFieldGrowthSkippedTaintedCells: ashFieldSummary.GrowthSkippedTaintedCellCount,
            AshFieldGrowthFailedApplications: ashFieldSummary.GrowthFailedConsequenceCount,
            ContaminationFireContaminatedBurnSources: ashFieldSummary.ContaminatedBurnSourceCellCount,
            ContaminationFireContaminatedAffectedCells: ashFieldSummary.ContaminatedAffectedCellCount,
            ContaminationFireContaminatedAffectedMapCells: contaminationFireSummary.ContaminatedAffectedMapCellCount,
            ContaminationFireBadwaterWaterLikeMapCells: contaminationFireSummary.BadwaterWaterLikeMapCellCount,
            ContaminationFireContaminatedWaterLikeMapCells: contaminationFireSummary.ContaminatedWaterLikeMapCellCount,
            ContaminationFireBadwaterSuppressionInputs: contaminationFireSummary.BadwaterSuppressionInputCellCount,
            ContaminationFireContaminatedWaterSuppressionInputs: contaminationFireSummary.ContaminatedWaterSuppressionInputCellCount,
            ContaminationFireToxicSmokeCells: beaverExposure.ToxicExposureCells,
            ContaminationFireNativeDecontaminationAttempts: contaminationFireSummary.NativeDecontaminationAttemptCount,
            TaintedAshPoisonCandidateCells: taintedAshSummary.CandidateCellCount,
            TaintedAshPoisonAppliedCells: taintedAshSummary.AppliedCellCount,
            AshWaterWashoutCandidateAshCells: ashWaterWashoutSummary.CandidateAshCellCount,
            AshWaterWashoutCleanAshWashed: ashWaterWashoutSummary.CleanAshWashedCellCount,
            AshWaterWashoutTaintedAshWashed: ashWaterWashoutSummary.TaintedAshWashedCellCount,
            AshWaterWashoutWaterTaintAttempts: ashWaterWashoutSummary.WaterTaintAttemptCount,
            AshWaterWashoutWaterTaintSuccesses: ashWaterWashoutSummary.WaterTaintSuccessCount,
            AshWaterWashoutNoOpCells: ashWaterWashoutSummary.NoOpCellCount,
            FertileAshGathererPosts: fertileAshCollectionSummary.GathererPostCount,
            FertileAshCollectionCandidateCells: fertileAshCollectionSummary.CandidateCellCount,
            FertileAshCollectionReachableCells: fertileAshCollectionSummary.ReachableCellCount,
            FertileAshCollectedGoods: fertileAshCollectionSummary.CollectedGoodCount,
            FertileAshCollectionDepletedCells: fertileAshCollectionSummary.DepletedAshCellCount,
            FertileAshCollectionSkippedTaintedOrSpentCells: fertileAshCollectionSummary.SkippedTaintedOrSpentCellCount,
            LastDeltaConsumerAlertCount: deltaConsumerSummary.AlertCount,
            LastPlayerFireAlertTick: alertCounters.LastAlertTick,
            LastPlayerFireAlertStartedFireCount: alertCounters.LastFireStartedCount,
            LastPlayerFireAlertFuelSpentCount: alertCounters.LastFuelSpentCount,
            LastPlayerFireAlertMaxHeat: alertCounters.LastMaxHeat,
            PlayerFireAlertNotificationCount: alertCounters.TotalNotificationCount,
            PlayerFireAlertPresentationFailureCount: alertCounters.PresentationFailureCount,
            PlayerFireAlertNotificationSent: alertCounters.LastNotificationSent,
            LastPlayerFireAlertMessage: alertCounters.LastMessage,
            WorldConsequenceFeedbackSourceEvents: alertCounters.TotalSourceEventCount,
            WorldConsequenceFeedbackCoalescedEvents: alertCounters.TotalCoalescedEventCount,
            WorldConsequenceFeedbackActiveFireEvents: alertCounters.ActiveFireEventCount,
            StructureOnFireEventsReceived: alertCounters.StructureOnFireEventCount,
            StructureOnFireEventsCoalesced: alertCounters.StructureOnFireCoalescedEventCount,
            StructureOnFireNotificationsSent: alertCounters.StructureOnFireNotificationCount,
            StructureOnFireNotificationsThrottled: alertCounters.StructureOnFireNotificationSuppressedThrottleCount,
            StructureOnFirePresentationFailures: alertCounters.StructureOnFirePresentationFailureCount,
            WorldConsequenceFeedbackStructureOnFireEvents: alertCounters.StructureOnFireEventCount,
            WorldConsequenceFeedbackBuildingDamageClosureEvents: alertCounters.BuildingDamageClosureEventCount,
            WorldConsequenceFeedbackPlantCropResourceLossEvents: alertCounters.PlantCropResourceLossEventCount,
            WorldConsequenceFeedbackBeaverDangerDeathEvents: alertCounters.BeaverDangerDeathEventCount,
            WorldConsequenceFeedbackAshAftermathEvents: alertCounters.AshAftermathEventCount,
            WorldConsequenceFeedbackNotifications: alertCounters.TotalNotificationCount,
            WorldConsequenceFeedbackActiveFireNotifications: alertCounters.ActiveFireNotificationCount,
            WorldConsequenceFeedbackStructureOnFireNotifications: alertCounters.StructureOnFireNotificationCount,
            WorldConsequenceFeedbackBuildingDamageClosureNotifications: alertCounters.BuildingDamageClosureNotificationCount,
            WorldConsequenceFeedbackPlantCropResourceLossNotifications: alertCounters.PlantCropResourceLossNotificationCount,
            WorldConsequenceFeedbackBeaverDangerDeathNotifications: alertCounters.BeaverDangerDeathNotificationCount,
            WorldConsequenceFeedbackAshAftermathNotifications: alertCounters.AshAftermathNotificationCount,
            WorldConsequenceFeedbackSuppressedThrottle: alertCounters.NotificationSuppressedThrottleCount,
            WorldConsequenceFeedbackPresentationFailures: alertCounters.PresentationFailureCount,
            WorldConsequenceFeedbackLogOnlyFallbacks: alertCounters.LogOnlyFallbackCount,
            WorldConsequenceFeedbackNotificationSent: alertCounters.LastNotificationSent,
            WorldConsequenceFeedbackNotificationSuppressed: alertCounters.LastNotificationSuppressed,
            WorldConsequenceFeedbackPrimaryClass: alertCounters.LastPrimaryClass.ToString().ToLowerInvariant(),
            VisualFieldSurfaceBound: visualFieldSurfaceState.IsBound,
            VisualFieldSurfaceCellCount: visualFieldSurfaceState.CellCount,
            VisualFieldSurfaceLastUpdatedTick: visualFieldSurfaceState.LastUpdatedTick,
            SmokeHeightSmokeCellCount: smokeHeightTelemetry.SmokeCellCount,
            SmokeHeightGroundContactSmokeCellCount: smokeHeightTelemetry.GroundContactSmokeCellCount,
            SmokeHeightAbsoluteGroundSmokeCellCount: smokeHeightTelemetry.AbsoluteGroundSmokeCellCount,
            SmokeHeightNearBottomSmokeCellCount: smokeHeightTelemetry.NearBottomSmokeCellCount,
            SmokeHeightLowestSmokeZ: smokeHeightTelemetry.LowestSmokeZ,
            SmokeHeightHighestSmokeZ: smokeHeightTelemetry.HighestSmokeZ,
            SmokeHeightPeakSmoke: smokeHeightTelemetry.PeakSmoke,
            SmokeHeightSmokeCellCountAtLowestZ: smokeHeightTelemetry.SmokeCellCountAtLowestZ,
            SmokeHeightContaminatedSmokeCellCount: smokeHeightTelemetry.ContaminatedSmokeCellCount,
            SmokeHeightSourceSmokeCellCount: smokeHeightTelemetry.SourceSmokeCellCount,
            SmokeHeightNonSourceSmokeCellCount: smokeHeightTelemetry.NonSourceSmokeCellCount,
            SmokeHeightNonSourceGroundContactSmokeCellCount: smokeHeightTelemetry.NonSourceGroundContactSmokeCellCount,
            SmokeHeightMaxNonSourceSmokeDistanceFromSource: smokeHeightTelemetry.MaxNonSourceSmokeDistanceFromSource,
            GpuFieldRendererEnabled: gpuFieldRendererCounters.RendererEnabled,
            GpuFieldRendererMaterialReady: gpuFieldRendererCounters.MaterialReady,
            GpuFieldRendererSurfaceBound: gpuFieldRendererCounters.VisualFieldSurfaceBound,
            GpuFieldRendererVisibleRegionCount: gpuFieldRendererCounters.VisibleRegionCount,
            GpuFieldRendererUpdatedRegionCount: gpuFieldRendererCounters.UpdatedRegionCount,
            GpuFieldRendererLastNonZeroUpdatedRegionCount: gpuFieldRendererCounters.LastNonZeroUpdatedRegionCount,
            GpuFieldRendererLastNonZeroUpdatedRegionTick: gpuFieldRendererCounters.LastNonZeroUpdatedRegionTick,
            GpuFieldRendererMaxUpdatedRegionCount: gpuFieldRendererCounters.MaxUpdatedRegionCount,
            GpuFieldRendererDroppedRegionCount: gpuFieldRendererCounters.DroppedRegionCount,
            GpuFieldRendererInvisibleRegionCount: gpuFieldRendererCounters.InvisibleRegionCount,
            GpuFieldRendererMaterialFailureCount: gpuFieldRendererCounters.MaterialFailureCount,
            GpuFieldRendererLastUpdatedTick: gpuFieldRendererCounters.LastUpdatedTick,
            BeaverFieldExposureAvailable: beaverExposure.IsAvailable,
            BeaverFieldExposureSampledBeavers: beaverExposure.SampledBeavers,
            BeaverFieldExposureExposedBeavers: beaverExposure.ExposedBeavers,
            BeaverFieldExposureRespiratoryCells: beaverExposure.RespiratoryExposureCells,
            BeaverFieldExposureBurnCells: beaverExposure.BurnExposureCells,
            BeaverFieldExposureContaminatedSmokeCells: beaverExposure.ContaminatedSmokeCells,
            BeaverFieldExposureToxicCells: beaverExposure.ToxicExposureCells,
            BeaverFieldExposureSteamCells: beaverExposure.SteamCells,
            BeaverFieldExposureTaintedAftermathCells: beaverExposure.TaintedAftermathCells,
            BeaverFieldExposureSkippedNoPositionApi: beaverExposure.SkippedNoPositionApi,
            BeaverFieldExposureSkippedBoundedSampling: beaverExposure.SkippedBoundedSampling,
            BeaverFieldExposureUnavailableReason: beaverExposure.UnavailableReason,
            BeaverFieldBehaviorDispatcherEnabled: beaverFieldBehaviorCounters.DispatcherEnabled,
            BeaverFieldBehaviorTrackedBeavers: beaverFieldBehaviorCounters.TrackedBeaverCount,
            BeaverFieldBehaviorDecisionsEvaluated: beaverFieldBehaviorCounters.DecisionsEvaluated,
            BeaverFieldBehaviorSmokeDecisionsApplied: beaverFieldBehaviorCounters.SmokeDecisionsApplied,
            BeaverFieldBehaviorToxicSmokeDecisionsApplied: beaverFieldBehaviorCounters.ToxicSmokeDecisionsApplied,
            BeaverFieldBehaviorFireHeatDecisionsApplied: beaverFieldBehaviorCounters.FireHeatDecisionsApplied,
            BeaverFieldBehaviorNoOpDecisionsApplied: beaverFieldBehaviorCounters.NoOpDecisionsApplied,
            BeaverFieldBehaviorDecisionsSkippedCooldown: beaverFieldBehaviorCounters.DecisionsSkippedCooldown,
            BeaverFieldBehaviorDecisionsSkippedBatch: beaverFieldBehaviorCounters.DecisionsSkippedBatch,
            BeaverFieldBehaviorFailedDecisions: beaverFieldBehaviorCounters.FailedDecisions,
            BeaverFieldBehaviorRecoveryActions: beaverFieldBehaviorCounters.RecoveryActions,
            BeaverFieldBehaviorSmokeExposedSamples: beaverFieldBehaviorCounters.SmokeExposedSamples,
            BeaverFieldBehaviorSmokeExposureAccumulatedSamples: beaverFieldBehaviorCounters.SmokeExposureAccumulatedSamples,
            BeaverFieldBehaviorSmokeCoughingEntered: beaverFieldBehaviorCounters.SmokeCoughingEntered,
            BeaverFieldBehaviorSmokeCoughingRecovered: beaverFieldBehaviorCounters.SmokeCoughingRecovered,
            BeaverFieldBehaviorSmokeCoughingSlowdownsApplied: beaverFieldBehaviorCounters.SmokeCoughingSlowdownsApplied,
            BeaverFieldBehaviorSmokeCoughingSlowdownsRecovered: beaverFieldBehaviorCounters.SmokeCoughingSlowdownsRecovered,
            BeaverFieldBehaviorSmokeRecoveryDecays: beaverFieldBehaviorCounters.SmokeRecoveryDecays,
            BeaverFieldBehaviorSmokeChokingSlowdownsApplied: beaverFieldBehaviorCounters.SmokeChokingSlowdownsApplied,
            BeaverFieldBehaviorSmokeChokingSlowdownsRecovered: beaverFieldBehaviorCounters.SmokeChokingSlowdownsRecovered,
            BeaverFieldBehaviorToxicSmokeExposedBeavers: beaverFieldBehaviorCounters.ToxicSmokeExposedBeavers,
            BeaverFieldBehaviorToxicSmokeExposureAccumulatedSamples:
                beaverFieldBehaviorCounters.ToxicSmokeExposureAccumulatedSamples,
            BeaverFieldBehaviorToxicSmokeRecoveryDecays: beaverFieldBehaviorCounters.ToxicSmokeRecoveryDecays,
            BeaverFieldBehaviorFireHeatExposedBeavers: beaverFieldBehaviorCounters.FireHeatExposedBeavers,
            BeaverFieldBehaviorFireHeatActiveFlameContacts: beaverFieldBehaviorCounters.FireHeatActiveFlameContacts,
            BeaverFieldBehaviorFireHeatRecoveryDecays: beaverFieldBehaviorCounters.FireHeatRecoveryDecays,
            BeaverFieldBehaviorPersistenceSaves: beaverFieldBehaviorCounters.PersistenceSaveCount,
            BeaverFieldBehaviorPersistenceLoads: beaverFieldBehaviorCounters.PersistenceLoadCount,
            BeaverFieldBehaviorLastDecisionTick: beaverFieldBehaviorCounters.LastDecisionTick,
            BurnDurationProofTarget: burnDurationProof.Target,
            BurnDurationProofTargetIndex: burnDurationProof.CellIndex,
            BurnDurationProofTargetX: burnDurationProof.X,
            BurnDurationProofTargetY: burnDurationProof.Y,
            BurnDurationProofTargetZ: burnDurationProof.Z,
            BurnDurationProofInitialFuel: burnDurationProof.InitialFuel,
            BurnDurationProofQueuedTick: burnDurationProof.QueuedTick,
            BurnDurationProofBurnStartTick: burnDurationProof.BurnStartTick,
            BurnDurationProofDepletionTick: burnDurationProof.DepletionTick,
            BurnDurationProofElapsedBurnTicks: burnDurationProof.ElapsedBurnTicks,
            BurnDurationProofTimeoutTicks: burnDurationProof.TimeoutTicks,
            BurnDurationProofSustainedHeatTicks: burnDurationProof.SustainedHeatTicks,
            BurnDurationProofSustainedHeatAppliedTicks: burnDurationProof.SustainedHeatAppliedTicks,
            BurnDurationProofSustainedHeatComplete: burnDurationProof.SustainedHeatComplete,
            BurnDurationProofTimedOut: burnDurationProof.TimedOut,
            BurnDurationProofStatus: burnDurationProof.Status,
            CompatibilityProbeStatus: _compatibilityReport.StatusToken,
            CompatibilityProbeDegraded: _compatibilityReport.IsDegraded,
            CompatibilityProbeRequiredPassed: _compatibilityReport.PassedRequiredProbeCount,
            CompatibilityProbeRequiredTotal: _compatibilityReport.RequiredProbeCount,
            CompatibilityProbeOptionalPassed: _compatibilityReport.PassedOptionalProbeCount,
            CompatibilityProbeOptionalTotal: _compatibilityReport.OptionalProbeCount,
            CompatibilityProbeDegradedFeatures: _compatibilityReport.DegradedFeatureToken,
            FireSimPresetName: currentPreset.Name,
            FireSimPresetIgnitionPoint: currentPreset.Parameters.IgnitionPoint,
            FireSimPresetWaterIgnitionPenalty: currentPreset.Parameters.FireWaterIgnitionPenalty,
            FireSimPresetFuelHeatWeight: currentPreset.Parameters.FireFuelHeatWeight,
            FireSimPresetFuelBurnDownNumerator: currentPreset.Parameters.FireFuelBurnDownPressureNumerator,
            FireSimPresetFuelBurnDownDenominator: currentPreset.Parameters.FireFuelBurnDownPressureDenominator,
            SustainedIgnitionDispatchTicks: currentPreset.SustainedIgnitionDispatchTicks,
            WorldImportTotalSources: _lastWorldImportSummary?.TotalSources,
            WorldImportTerrainSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Terrain),
            WorldImportVegetationSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Vegetation),
            WorldImportTreeSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Tree),
            WorldImportCropSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Crop),
            WorldImportBuildingSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Building),
            WorldImportStorageSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Storage),
            WorldImportInfrastructureSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Infrastructure),
            WorldImportWaterSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Water),
            WorldImportBadwaterSources: _lastWorldImportSummary?.Count(WildfireMaterialClass.Badwater),
            WorldImportResolvedEmptyCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Empty),
            WorldImportResolvedTerrainCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Terrain),
            WorldImportResolvedVegetationCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Vegetation),
            WorldImportResolvedTreeCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Tree),
            WorldImportResolvedCropCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Crop),
            WorldImportResolvedBuildingCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Building),
            WorldImportResolvedStorageCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Storage),
            WorldImportResolvedInfrastructureCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Infrastructure),
            WorldImportResolvedWaterCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Water),
            WorldImportResolvedBadwaterCells: _lastWorldImportSummary?.ResolvedCount(WildfireMaterialClass.Badwater),
            PersistentRestoreNoLiveFuelCellsCleared: fireSystem.LastPersistentRestoreNoLiveFuelCellsCleared);
    }

    public TimberbornQaAshCellProbeResult InspectAshCell(int cellIndex)
    {
        if (_fireSystem is not { IsInitialized: true } fireSystem ||
            fireSystem.Width is null ||
            fireSystem.Height is null ||
            fireSystem.Depth is null)
        {
            return InvalidAshCellProbeResult(cellIndex);
        }

        FireGrid grid = new(fireSystem.Width.Value, fireSystem.Height.Value, fireSystem.Depth.Value);
        if (cellIndex < 0 || cellIndex >= grid.CellCount)
        {
            return InvalidAshCellProbeResult(cellIndex);
        }

        TimberbornFireSimPersistenceSnapshot? snapshot = fireSystem.CapturePersistentFireSimState();
        if (snapshot?.TransportFields is not { Count: > 0 } transportFields ||
            cellIndex >= transportFields.Count)
        {
            return InvalidAshCellProbeResult(cellIndex);
        }

        uint packedTransport = transportFields[cellIndex];
        WildfireTransportFieldState transport = WildfireTransportFieldState.Unpack(packedTransport);
        (int x, int y, int z) = grid.FromIndex(cellIndex);
        bool hasEntry = _ashFieldService.TryGetEntry(cellIndex, out TimberbornAshFieldEntry entry);

        return new TimberbornQaAshCellProbeResult(
            cellIndex,
            IsValid: true,
            X: x,
            Y: y,
            Z: z,
            PackedTransport: packedTransport,
            Steam: transport.Steam,
            Smoke: transport.Smoke,
            SmokeContamination: transport.SmokeContamination,
            Ash: transport.Ash,
            AshContamination: transport.AshContamination,
            Source: transport.Source,
            ReadModelPresent: hasEntry,
            ReadModelStrength: hasEntry ? entry.Strength : null,
            ReadModelQuality: hasEntry ? entry.Quality.ToString() : null);
    }

    private static TimberbornQaAshCellProbeResult InvalidAshCellProbeResult(int cellIndex)
    {
        return new TimberbornQaAshCellProbeResult(
            cellIndex,
            IsValid: false,
            X: null,
            Y: null,
            Z: null,
            PackedTransport: null,
            Steam: null,
            Smoke: null,
            SmokeContamination: null,
            Ash: null,
            AshContamination: null,
            Source: false,
            ReadModelPresent: false,
            ReadModelStrength: null,
            ReadModelQuality: null);
    }

    public bool ApplyPlayerFertileAshDesignation(int cellIndex, int strength)
    {
        byte ashAmount = StrengthToAshUnits(strength);
        if (ashAmount == 0)
        {
            return false;
        }

        _fireSystem?.RegisterChange(
            new FireSimChange(
                CellIndex: cellIndex,
                AddAsh: ashAmount,
                SetAshContamination: 0),
            "fertile_ash_application");
        _logSink.Info(
            "wildfire_timberborn_fertile_ash_application_queued " +
            $"cell_index={cellIndex} " +
            $"ash_amount={ashAmount}");
        return true;
    }

    public bool TryResolveFertileAshApplicationCell(int cellIndex, out int applicationCellIndex)
    {
        applicationCellIndex = cellIndex;
        if (_fireSystem is not { IsInitialized: true } fireSystem ||
            fireSystem.Width is null ||
            fireSystem.Height is null ||
            fireSystem.Depth is null)
        {
            return false;
        }

        FireGrid grid = new(fireSystem.Width.Value, fireSystem.Height.Value, fireSystem.Depth.Value);
        if (cellIndex < 0 || cellIndex >= grid.CellCount)
        {
            return false;
        }

        TimberbornFireSimPersistenceSnapshot? snapshot = fireSystem.CapturePersistentFireSimState();
        if (snapshot?.Cells is not { Count: > 0 } cells || cells.Count != grid.CellCount)
        {
            return false;
        }

        (int x, int y, int z) = grid.FromIndex(cellIndex);
        int? landingCellIndex = Enumerable.Range(0, z + 1)
            .Select(offset => grid.ToIndex(x, y, z - offset))
            .Where(candidate => PackedCell.Terrain(cells[candidate]) == 1)
            .Select(static candidate => (int?)candidate)
            .FirstOrDefault();
        if (landingCellIndex is null)
        {
            return false;
        }

        applicationCellIndex = landingCellIndex.Value;
        return true;
    }

    private void QueueCollectedAshRemoval(TimberbornFertileAshCollectedCell cell)
    {
        byte ashAmount = StrengthToAshUnits(cell.StrengthToRemove);
        if (ashAmount == 0)
        {
            return;
        }

        _fireSystem?.RegisterChange(
            new FireSimChange(
                CellIndex: cell.CellIndex,
                RemoveAsh: ashAmount),
            "fertile_ash_collection");
    }

    private void QueueDecayedAshRemoval(TimberbornAshFieldCollectionRemoval removal)
    {
        byte ashAmount = StrengthToAshUnits(removal.StrengthRemoved);
        if (ashAmount == 0)
        {
            return;
        }

        _fireSystem?.RegisterChange(
            new FireSimChange(
                CellIndex: removal.CellIndex,
                RemoveAsh: ashAmount),
            "ash_day_decay");
    }

    private void QueueWashedAshRemoval(TimberbornAshWaterWashoutRemoval removal)
    {
        byte ashAmount = StrengthToAshUnits(removal.StrengthRemoved);
        if (ashAmount == 0)
        {
            return;
        }

        _fireSystem?.RegisterChange(
            new FireSimChange(
                CellIndex: removal.CellIndex,
                RemoveAsh: ashAmount),
            "ash_water_washout");
    }

    private IReadOnlyDictionary<int, TimberbornAshWaterContact> CurrentAshWaterContacts()
    {
        TimberbornFireSystem? fireSystem = _fireSystem;
        return TimberbornAshWaterContactClassifier.FromFireSimState(
            fireSystem?.CapturePersistentFireSimState(),
            fireSystem?.ImportedTargets ?? Array.Empty<TimberbornImportedFieldTarget>());
    }

    private static byte StrengthToAshUnits(int strength)
    {
        if (strength <= 0)
        {
            return 0;
        }

        return checked((byte)Math.Clamp(
            (strength + TimberbornFertileAshCollectionService.StrengthPerGood - 1) /
                TimberbornFertileAshCollectionService.StrengthPerGood,
            1,
            3));
    }

    private int CurrentDayNumber()
    {
        return Math.Max(0, _dayNightCycle.DayNumber);
    }

    public bool IsCellTaintedAsh(int cellIndex)
    {
        return _ashFieldService.TryGetEntry(cellIndex, out TimberbornAshFieldEntry entry) &&
            entry.Quality == WildfireAshQuality.Tainted;
    }

    private void LogPreparedBindings(TimberbornFireCadence cadence)
    {
        _logSink.Info($"wildfire_timberborn_runtime_configured cadence_interval_ms={cadence.Interval.TotalMilliseconds:F0}");
        foreach (string lane in new[]
        {
            "debug_visual_state",
            "gpu_field_renderer",
            "player_fire_alert",
            "building_burnout_access_block",
            "crop_burn_consequences",
            "tree_burn_consequences",
            "structure_burn_damage_rollback",
            "stored_goods_burn",
            "explosive_infrastructure",
            "detonator_fire_safety",
            "tunnel_fire",
            "path_infrastructure_fire",
            "power_infrastructure_fire",
            "water_infrastructure_fire",
            "ash_field",
        })
        {
            _logSink.Info($"wildfire_timberborn_delta_consequence_sink_bound lane={lane}");
        }
    }

    private FireGrid? CurrentGrid()
    {
        return _fireSystem is { IsInitialized: true } fireSystem
            ? new FireGrid(
                fireSystem.Width ?? throw new InvalidOperationException("Fire system width is unavailable."),
                fireSystem.Height ?? throw new InvalidOperationException("Fire system height is unavailable."),
                fireSystem.Depth ?? throw new InvalidOperationException("Fire system depth is unavailable."))
            : _initializingGrid;
    }

    private TimberbornWildfirePersistenceSnapshot CapturePersistentState()
    {
        return new TimberbornWildfirePersistenceSnapshot(
            TimberbornWildfirePersistenceSnapshot.CurrentPersistenceVersion,
            _fireSystem?.CapturePersistentFireSimState(),
            _ashFieldService.SaveSnapshot(),
            _beaverFieldBehaviorDispatcher.CaptureState(),
            TimberbornWildfirePersistenceCodec.CaptureConsequences(_bindings?.BurnDamageService));
    }

    private void LoadPersistentState()
    {
        _persistence.Load(() =>
            _singletonLoader.TryGetSingleton(TimberbornWildfirePersistenceKeys.Singleton, out var loader) &&
                loader.Has(TimberbornWildfirePersistenceKeys.Snapshot)
                ? loader.Get(TimberbornWildfirePersistenceKeys.Snapshot)
                : null);

        if (_persistence.LoadFailure is { } exception)
        {
            Initialization.Fail(exception);
            _logSink.Warning(
                "wildfire_timberborn_persistence_load_failed " +
                $"message={TimberbornQaCommandBridge.FormatToken(exception.Message)} " +
                $"details={TimberbornQaCommandBridge.FormatToken(exception.ToString())}");
            return;
        }

        if (_persistence.LoadedSnapshot is { } snapshot)
        {
            _logSink.Info(
                "wildfire_timberborn_persistence_loaded " +
                $"version={snapshot.PersistenceVersion} " +
                $"firesim_saved={(snapshot.FireSim is not null).ToString().ToLowerInvariant()} " +
                $"ash_entries={snapshot.AshField.Entries.Count} " +
                $"beaver_behavior_entries={snapshot.BeaverBehavior.Entries.Count} " +
                $"consequence_burn_damage_entries={snapshot.Consequences.BurnDamageStates.Count}");
            return;
        }

        _logSink.Info("wildfire_timberborn_persistence_load_skipped reason=no_saved_state");
    }

    private void RestorePersistentConsequenceAndAshState(
        TimberbornWildfirePersistenceSnapshot? snapshot, TimberbornRuntimeBindings bindings)
    {
        if (snapshot is null)
        {
            return;
        }

        TimberbornWildfirePersistenceCodec.RestoreConsequences(bindings.BurnDamageService, snapshot.Consequences);
        if (snapshot.FireSim?.TransportFields is { Count: > 0 } atmosphericFields &&
            atmosphericFields.Any(static packed => WildfireTransportFieldState.Unpack(packed).Ash > 0))
        {
            _ashFieldService.SyncFromTransportFields(snapshot.FireSim.Tick, atmosphericFields, CurrentDayNumber());
        }
        else
        {
            _ashFieldService.RestoreSnapshot(snapshot.FireSim?.Tick ?? 0, snapshot.AshField, CurrentDayNumber());
        }
        _beaverFieldBehaviorDispatcher.RestoreState(snapshot.BeaverBehavior);
        _logSink.Info(
            "wildfire_timberborn_persistence_runtime_state_restored " +
            $"ash_entries={snapshot.AshField.Entries.Count} " +
            $"beaver_behavior_entries={snapshot.BeaverBehavior.Entries.Count} " +
            $"consequence_burn_damage_entries={snapshot.Consequences.BurnDamageStates.Count}");
    }

    private TimberbornFireDeltaConsumerSinks CreateDeltaConsumerSinks(
        TimberbornRuntimeBindings bindings,
        FireGrid grid,
        TimberbornQueuedFireSimHeatPulseSink heatPulseSink,
        TimberbornQueuedStoredGoodContaminationPulseSink contaminationPulseSink)
    {
        TimberbornStoredGoodHazardConsequenceSink storedHazards = new(
            grid,
            () => TimberbornExplosiveInfrastructureConsequenceSettings.FromSnapshot(_releaseSettings.GetSnapshot()),
            bindings.BlastRadius,
            heatPulseSink,
            contaminationPulseSink);
        return new TimberbornFireDeltaConsumerSinks(
            debugVisualSink: _debugVisualSink,
            visualEffectSink: new TimberbornCompositeFireVisualEffectSink(_gpuFieldRenderer),
            alertSink: _playerFireAlerts,
            buildingBurnoutConsequenceSink: new TimberbornBuildingBurnoutConsequenceSink(bindings.BuildingBurnout),
            structureBurnDamageRollbackSink: new TimberbornStructureBurnDamageRollbackSink(
                bindings.StructureRollback, logSink: _logSink, burnDamageTargets: bindings.BurnDamageService),
            burnDamageSink: bindings.BurnDamageService,
            cropBurnConsequenceSink: new TimberbornCropBurnConsequenceSink(bindings.BurnDamageService, bindings.CropBurn),
            treeBurnConsequenceSink: new TimberbornTreeBurnConsequenceSink(bindings.BurnDamageService, bindings.TreeBurn, _logSink),
            storedGoodBurnConsequenceSink: new TimberbornStoredGoodBurnConsequenceSink(
                bindings.StoredInventory, storedHazards, logSink: _logSink, burnDamageTargets: bindings.BurnDamageService),
            explosiveInfrastructureConsequenceSink: new TimberbornExplosiveInfrastructureConsequenceSink(
                () => TimberbornExplosiveInfrastructureConsequenceSettings.FromSnapshot(_releaseSettings.GetSnapshot()),
                bindings.ExplosiveInfrastructure, heatPulseSink, _logSink),
            detonatorFireSafetySink: new TimberbornDetonatorFireSafetySink(
                () => _releaseSettings.GetSnapshot().IsDetonatorFireSafetyEnabled, bindings.DetonatorSafety, _logSink),
            tunnelFireSink: new TimberbornTunnelFireSink(
                () => TimberbornTunnelFireSettings.FromSnapshot(_releaseSettings.GetSnapshot()), bindings.TunnelFire, _logSink),
            pathInfrastructureFireSink: new TimberbornPathInfrastructureFireSink(
                bindings.PathFire, logSink: _logSink, burnDamageTargets: bindings.BurnDamageService),
            powerInfrastructureFireSink: new TimberbornPowerInfrastructureFireSink(
                bindings.PowerFire, logSink: _logSink, burnDamageTargets: bindings.BurnDamageService),
            waterInfrastructureFireSink: new TimberbornWaterInfrastructureFireSink(
                bindings.WaterFire, logSink: _logSink, burnDamageTargets: bindings.BurnDamageService),
            ashFieldSink: new TimberbornAshFieldSink(
                bindings.BurnDamageService, _ashFieldService, affectedCellContaminationProvider: IsAffectedCellContaminated));
    }

    private bool IsAffectedCellContaminated(int cellIndex)
    {
        FireGrid grid = _fireSystem?.RequireInitializedGrid() ??
            throw new InvalidOperationException("Fire system grid is unavailable.");
        (int x, int y, int z) = grid.FromIndex(cellIndex);
        return _soilContaminationService.SoilIsContaminated(new Vector3Int(x, y, z));
    }

    private bool TryAllowExternalChange(string source, int? count)
    {
        if (IsWildfireEnabled())
        {
            return true;
        }

        _logSink.Info(
            "wildfire_timberborn_external_change_skipped_disabled " +
            $"source={source} " +
            $"count={count?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"}");
        return false;
    }

    private bool IsWildfireEnabled()
    {
        return _releaseSettings.GetSnapshot().IsWildfireEnabled;
    }

    private bool IsAutoDispatchEnabled()
    {
        return Initialization.State == TimberbornRuntimeInitializationState.Ready &&
            IsWildfireEnabled();
    }

    private static string FormatNumber(int? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }

    private static string FormatNumber(byte? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }

    private TimberbornFireSystem RequireFireSystem()
    {
        return _fireSystem ??
            throw new InvalidOperationException("Timberborn fire runtime must be initialized before registering simulator changes.");
    }

    private void RunCompatibilityProbesIfNeeded()
    {
        if (_compatibilityProbesRan)
        {
            return;
        }

        _compatibilityReport = TimberbornCompatibilityProbeCatalog.RunReleasePathProbes();
        _compatibilityProbesRan = true;
        TimberbornCompatibilityProbeLogger.Log(_compatibilityReport, _logSink);
    }

}
