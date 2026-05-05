using Timberborn.SingletonSystem;
using Timberborn.QuickNotificationSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornFireRuntime :
    ILoadableSingleton,
    IUnloadableSingleton,
    IUpdatableSingleton,
    ITimberbornQaCommandStateProvider,
    ITimberbornQaDeltaStimulus,
    ITimberbornQaBuildingBurnoutStimulus,
    ITimberbornQaWaterSuppressionStimulus,
    ITimberbornQaBurnDurationStimulus,
    ITimberbornQaFireSimParameterPresetSelector
{
    private readonly ITimberbornFireLogSink _logSink;
    private readonly TimberbornFireDebugVisualStateSink _debugVisualSink;
    private readonly TimberbornGpuFieldRendererSink _gpuFieldRenderer;
    private readonly TimberbornPooledFireSmokeAshEffectSink _pooledFireEffects;
    private readonly TimberbornPlayerFireAlertSink _playerFireAlerts;
    private readonly TimberbornPlayerFireAlertCameraFocus _playerFireAlertCameraFocus;
    private readonly WildfireReleaseSettings _releaseSettings;
    private readonly TimberbornFireSimParameterPresetState _fireSimParameterPresetState;
    private ITimberbornBuildingBurnoutConsequenceApi? _buildingBurnoutConsequenceApi;
    private ITimberbornQaBuildingBurnoutStimulusTargetProvider? _buildingBurnoutStimulusTargetProvider;
    private ITimberbornStructureBurnDamageRollbackTargetApi? _structureBurnDamageRollbackTargetApi;
    private ITimberbornStoredGoodBurnInventoryApi? _storedGoodBurnInventoryApi;
    private ITimberbornExplosiveInfrastructureTargetApi? _explosiveInfrastructureTargetApi;
    private TimberbornQueuedFireSimHeatPulseSink? _explosiveInfrastructureHeatPulseSink;
    private ITimberbornDetonatorFireSafetyTargetApi? _detonatorFireSafetyTargetApi;
    private ITimberbornTunnelFireTargetApi? _tunnelFireTargetApi;
    private ITimberbornPathInfrastructureFireTargetApi? _pathInfrastructureFireTargetApi;
    private ITimberbornPowerInfrastructureFireTargetApi? _powerInfrastructureFireTargetApi;
    private ITimberbornWaterInfrastructureFireTargetApi? _waterInfrastructureFireTargetApi;
    private TimberbornFireSystem? _fireSystem;
    private TimberbornFixedCadenceFireDispatcher? _dispatcher;
    private TimberbornWorldCellImportSummary? _lastWorldImportSummary;
    private TimberbornCompatibilityReport _compatibilityReport = TimberbornCompatibilityReport.Placeholder;
    private bool _compatibilityProbesRan;
    private string? _autoDispatchDisabledReason;
    private long _gameUpdateId;
    private bool _isLoaded;

    public TimberbornFireRuntime(
        ITimberbornGpuVisualFieldSurface visualFieldSurface,
        QuickNotificationService quickNotificationService,
        TimberbornPlayerFireAlertCameraFocus playerFireAlertCameraFocus,
        WildfireReleaseSettings releaseSettings,
        TimberbornFireSimParameterPresetState fireSimParameterPresetState)
    {
        _releaseSettings = releaseSettings ?? throw new ArgumentNullException(nameof(releaseSettings));
        _fireSimParameterPresetState = fireSimParameterPresetState ??
            throw new ArgumentNullException(nameof(fireSimParameterPresetState));
        _playerFireAlertCameraFocus = playerFireAlertCameraFocus ??
            throw new ArgumentNullException(nameof(playerFireAlertCameraFocus));
        _logSink = new UnityTimberbornFireLogSink();
        _debugVisualSink = new TimberbornFireDebugVisualStateSink();
        _gpuFieldRenderer = new TimberbornGpuFieldRendererSink(
            visualFieldSurface ?? throw new ArgumentNullException(nameof(visualFieldSurface)),
            _logSink);
        _pooledFireEffects = new TimberbornPooledFireSmokeAshEffectSink(
            visualFieldSurface,
            _logSink);
        _playerFireAlerts = new TimberbornPlayerFireAlertSink(
            new TimberbornQuickNotificationSink(quickNotificationService, _playerFireAlertCameraFocus),
            _logSink);
    }

    public void Load()
    {
        _isLoaded = true;
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
        _fireSystem?.Dispose();
        _explosiveInfrastructureHeatPulseSink?.Detach();
        _gpuFieldRenderer.Clear();
        _pooledFireEffects.Clear();
        _playerFireAlerts.Clear();
        _playerFireAlertCameraFocus.Clear();
        _dispatcher = null;
        _fireSystem = null;
        _lastWorldImportSummary = null;
        _autoDispatchDisabledReason = null;
        _compatibilityReport = TimberbornCompatibilityReport.Placeholder;
        _compatibilityProbesRan = false;
        _gameUpdateId = 0;
        _isLoaded = false;
        _logSink.Info("wildfire_timberborn_adapter_stopped");
        _logSink.Info("wildfire_timberborn_runtime_unloaded");
    }

    public void UpdateSingleton()
    {
        if (_dispatcher is null)
        {
            return;
        }

        _gameUpdateId++;
        TimeSpan elapsed = TimeSpan.FromSeconds(Math.Max(0d, Time.deltaTime));
        try
        {
            TimberbornFireDispatchResult result = _dispatcher.Update(new TimberbornFireUpdate(_gameUpdateId, elapsed));

            if (result.DidDispatch)
            {
                _logSink.Info(
                    $"wildfire_timberborn_runtime_dispatched game_update_id={_gameUpdateId} tick={result.Step?.Tick} delta_count={result.Step?.Deltas.Count}");
            }
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                $"wildfire_timberborn_runtime_dispatch_failed game_update_id={_gameUpdateId} message=\"{exception.Message}\"");
        }
    }

    public void AttachSimulator(IGpuFireSimulator fireSimulator, TimberbornFireCadence? cadence = null)
    {
        if (fireSimulator is null)
        {
            throw new ArgumentNullException(nameof(fireSimulator));
        }

        Configure(
            new TimberbornFireSystem(
                fireSimulator,
                new TimberbornFireCellMapper(),
                _logSink,
                CreateDeltaConsumerSinks()),
            cadence);
    }

    public void Initialize(
        FireGrid grid,
        IEnumerable<TimberbornCellSource> sources,
        ReadOnlySpan<WildfireCompanionField> companionFields,
        TimberbornWorldCellImportSummary worldImportSummary,
        ITimberbornFireSimulatorFactory simulatorFactory,
        TimberbornFireCadence? cadence = null)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        if (simulatorFactory is null)
        {
            throw new ArgumentNullException(nameof(simulatorFactory));
        }

        RunCompatibilityProbesIfNeeded();
        TimberbornCompatibilityRuntimeGate.ThrowIfRequiredProbesFailed(_compatibilityReport, _logSink);
        _explosiveInfrastructureHeatPulseSink = new TimberbornQueuedFireSimHeatPulseSink(grid);
        TimberbornFireSystem fireSystem = new(
            simulatorFactory,
            new TimberbornFireCellMapper(),
            _logSink,
            CreateDeltaConsumerSinks());
        fireSystem.Initialize(grid, sources, companionFields);
        _lastWorldImportSummary = worldImportSummary ?? throw new ArgumentNullException(nameof(worldImportSummary));
        Configure(fireSystem, cadence);
        _logSink.Info(
            $"wildfire_timberborn_runtime_simulator_initialized width={fireSystem.Width} height={fireSystem.Height} depth={fireSystem.Depth} {_lastWorldImportSummary.StatusToken}");
    }

    public void RegisterHeat(int cellIndex, byte heat)
    {
        if (!TryAllowExternalChange("heat", 1))
        {
            return;
        }

        RequireFireSystem().RegisterHeat(cellIndex, heat);
    }

    public void RegisterChange(FireSimChange change)
    {
        if (!TryAllowExternalChange("external", 1))
        {
            return;
        }

        RequireFireSystem().RegisterChange(change);
    }

    public void SkipInitializeForOversizedGrid(FireGrid grid, TimberbornWorldCellImportSummary worldImportSummary)
    {
        if (grid.CellCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(grid), grid, "Fire grid cell count cannot be negative.");
        }

        _fireSystem?.Dispose();
        _dispatcher = null;
        _fireSystem = null;
        _lastWorldImportSummary = worldImportSummary ?? throw new ArgumentNullException(nameof(worldImportSummary));
        _autoDispatchDisabledReason =
            $"map_too_large:cell_count={grid.CellCount}:limit={TimberbornAutoDispatchPolicy.CellLimit}";
        _logSink.Warning(
            "wildfire_timberborn_runtime_initialize_skipped " +
            "reason=map_too_large " +
            $"width={grid.Width} " +
            $"height={grid.Height} " +
            $"depth={grid.Depth} " +
            $"cell_count={grid.CellCount} " +
            $"limit={TimberbornAutoDispatchPolicy.CellLimit} " +
            $"{_lastWorldImportSummary.StatusToken}");
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
        TimberbornQaDeltaStimulusResult result = RequireFireSystem().QueueQaDeltaStimulus(targetSelector);
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
            $"queued_heat_changes={result.QueuedHeatChangeCount}");

        return result;
    }

    public TimberbornQaBuildingBurnoutStimulusResult QueueBuildingBurnoutStimulus()
    {
        ITimberbornQaBuildingBurnoutStimulusTargetProvider targetProvider =
            _buildingBurnoutStimulusTargetProvider ??
            throw new InvalidOperationException(
                "QA building burnout stimulus requires a Timberborn pausable building target provider.");
        TimberbornQaBuildingBurnoutStimulusResult result =
            RequireFireSystem().QueueBuildingBurnoutQaStimulus(targetProvider);
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
            RequireFireSystem().QueueWaterSuppressionQaStimulus(targetSelector);
        _logSink.Info(
            "wildfire_timberborn_qa_water_suppression_queued " +
            $"target_selector={result.TargetSelector} " +
            $"cell_index={result.CellIndex} " +
            $"x={result.X} " +
            $"y={result.Y} " +
            $"z={result.Z} " +
            $"target_material={result.MaterialClass} " +
            $"companion_target_id={result.CompanionTargetId} " +
            $"initial_cell={result.InitialCell} " +
            $"set_water={result.SetWater} " +
            $"queued_water_changes={result.QueuedWaterChangeCount}");

        return result;
    }

    public TimberbornQaBurnDurationStimulusResult QueueBurnDurationStimulus(string target)
    {
        TimberbornQaBurnDurationStimulusResult result =
            RequireFireSystem().QueueBurnDurationQaStimulus(target);
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
            $"ignition={result.Parameters.FireIgnitionBaseHeat} " +
            $"neighbor_bonus={result.Parameters.FireBurningNeighborHeatBonus} " +
            $"water_suppression={result.Parameters.FireWaterSuppressionHeat}");

        return result;
    }

    public TimberbornQaCommandState GetState()
    {
        TimberbornFireSimParameterPreset currentPreset = _fireSimParameterPresetState.CurrentPreset;

        if (_fireSystem is not { IsInitialized: true } fireSystem)
        {
            return new TimberbornQaCommandState(
                IsSimulatorIntegrated: false,
                IsGameContextRuntimeLoaded: _isLoaded,
                WildfireEnabled: IsWildfireEnabled(),
                CompatibilityProbeStatus: _compatibilityReport.StatusToken,
                CompatibilityProbeDegraded: _compatibilityReport.IsDegraded,
                CompatibilityProbeRequiredPassed: _compatibilityReport.PassedRequiredProbeCount,
                CompatibilityProbeRequiredTotal: _compatibilityReport.RequiredProbeCount,
                CompatibilityProbeOptionalPassed: _compatibilityReport.PassedOptionalProbeCount,
                CompatibilityProbeOptionalTotal: _compatibilityReport.OptionalProbeCount,
                CompatibilityProbeDegradedFeatures: _compatibilityReport.DegradedFeatureToken,
                FireSimPresetName: currentPreset.Name,
                FireSimPresetIgnitionBaseHeat: currentPreset.Parameters.FireIgnitionBaseHeat,
                FireSimPresetBurningNeighborHeatBonus: currentPreset.Parameters.FireBurningNeighborHeatBonus,
                FireSimPresetWaterSuppressionHeat: currentPreset.Parameters.FireWaterSuppressionHeat,
                FireSimPresetFuelBurnDownNumerator: currentPreset.Parameters.FireFuelBurnDownPressureNumerator,
                FireSimPresetFuelBurnDownDenominator: currentPreset.Parameters.FireFuelBurnDownPressureDenominator,
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
                WorldImportSafeUnavailableCount: _lastWorldImportSummary?.ProviderSafeUnavailableCounts.Values.Sum());
        }

        TimberbornFireDeltaConsumerSummary deltaConsumerSummary = fireSystem.LastDeltaConsumerSummary;
        TimberbornGpuVisualFieldSurfaceState visualFieldSurfaceState = fireSystem.VisualFieldSurfaceState;
        TimberbornGpuFieldRendererCounters gpuFieldRendererCounters = _gpuFieldRenderer.Counters;
        TimberbornPooledFireEffectCounters pooledEffectCounters = _pooledFireEffects.Counters;
        TimberbornPlayerFireAlertCounters alertCounters = _playerFireAlerts.Counters;
        TimberbornQaBurnDurationProofState burnDurationProof = fireSystem.BurnDurationProofState;

        return new TimberbornQaCommandState(
            IsSimulatorIntegrated: true,
            IsGameContextRuntimeLoaded: _isLoaded,
            WildfireEnabled: IsWildfireEnabled(),
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
            LastDeltaConsumerStructureBurnDamageRollbackSkippedNoSafeApiCount: deltaConsumerSummary.StructureBurnDamageRollbackSkippedNoSafeApiCount,
            LastDeltaConsumerStructureBurnDamageRollbackTotalDamageApplied: deltaConsumerSummary.StructureBurnDamageRollbackTotalDamageApplied,
            LastPositiveBuildingBurnoutAppliedTick: fireSystem.LastPositiveBuildingBurnoutAppliedTick,
            LastPositiveBuildingBurnoutAppliedCount: fireSystem.LastPositiveBuildingBurnoutAppliedCount,
            LastDeltaConsumerStoredGoodBurnConsideredDeltaCount: deltaConsumerSummary.StoredGoodBurnConsideredDeltaCount,
            LastDeltaConsumerStoredGoodBurnMatchedStorageCellCount: deltaConsumerSummary.StoredGoodBurnMatchedStorageCellCount,
            LastDeltaConsumerStoredGoodBurnDuplicateStorageTargetSuppressedCount: deltaConsumerSummary.StoredGoodBurnDuplicateStorageTargetSuppressedCount,
            LastDeltaConsumerStoredGoodBurnableStackCount: deltaConsumerSummary.StoredGoodBurnableStackCount,
            LastDeltaConsumerStoredGoodBurnDestroyedItemCount: deltaConsumerSummary.StoredGoodBurnDestroyedItemCount,
            LastDeltaConsumerStoredGoodBurnHazardousGoodCount: deltaConsumerSummary.StoredGoodBurnHazardousGoodCount,
            LastDeltaConsumerStoredGoodBurnSkippedNoInventoryApiCount: deltaConsumerSummary.StoredGoodBurnSkippedNoInventoryApiCount,
            LastDeltaConsumerStoredGoodBurnSkippedUnknownResourceCount: deltaConsumerSummary.StoredGoodBurnSkippedUnknownResourceCount,
            LastDeltaConsumerStoredGoodBurnSkippedNonBurnableItemCount: deltaConsumerSummary.StoredGoodBurnSkippedNonBurnableItemCount,
            LastDeltaConsumerExplosiveInfrastructureConsideredDeltaCount: deltaConsumerSummary.ExplosiveInfrastructureConsideredDeltaCount,
            LastDeltaConsumerExplosiveInfrastructureMatchedTargetCellCount: deltaConsumerSummary.ExplosiveInfrastructureMatchedTargetCellCount,
            LastDeltaConsumerExplosiveInfrastructureDuplicateTargetSuppressedCount: deltaConsumerSummary.ExplosiveInfrastructureDuplicateTargetSuppressedCount,
            LastDeltaConsumerExplosiveInfrastructureArmedTargetCount: deltaConsumerSummary.ExplosiveInfrastructureArmedTargetCount,
            LastDeltaConsumerExplosiveInfrastructureTriggeredTargetCount: deltaConsumerSummary.ExplosiveInfrastructureTriggeredTargetCount,
            LastDeltaConsumerExplosiveInfrastructureNativeTriggeredTargetCount: deltaConsumerSummary.ExplosiveInfrastructureNativeTriggeredTargetCount,
            LastDeltaConsumerExplosiveInfrastructureHeatPulseCellCount: deltaConsumerSummary.ExplosiveInfrastructureHeatPulseCellCount,
            LastDeltaConsumerExplosiveInfrastructureSkippedSettingDisabledCount: deltaConsumerSummary.ExplosiveInfrastructureSkippedSettingDisabledCount,
            LastDeltaConsumerExplosiveInfrastructureSkippedNoSafeApiCount: deltaConsumerSummary.ExplosiveInfrastructureSkippedNoSafeApiCount,
            LastDeltaConsumerExplosiveInfrastructureSkippedAlreadyTriggeredCount: deltaConsumerSummary.ExplosiveInfrastructureSkippedAlreadyTriggeredCount,
            LastDeltaConsumerExplosiveInfrastructureLastTriggeredDepth: deltaConsumerSummary.ExplosiveInfrastructureLastTriggeredDepth,
            LastDeltaConsumerDetonatorFireSafetyConsideredDeltaCount: deltaConsumerSummary.DetonatorFireSafetyConsideredDeltaCount,
            LastDeltaConsumerDetonatorFireSafetyMatchedTargetCellCount: deltaConsumerSummary.DetonatorFireSafetyMatchedTargetCellCount,
            LastDeltaConsumerDetonatorFireSafetyDuplicateTargetSuppressedCount: deltaConsumerSummary.DetonatorFireSafetyDuplicateTargetSuppressedCount,
            LastDeltaConsumerDetonatorFireSafetyDisabledTargetCount: deltaConsumerSummary.DetonatorFireSafetyDisabledTargetCount,
            LastDeltaConsumerDetonatorFireSafetyArmedTargetCount: deltaConsumerSummary.DetonatorFireSafetyArmedTargetCount,
            LastDeltaConsumerDetonatorFireSafetySkippedSettingDisabledCount: deltaConsumerSummary.DetonatorFireSafetySkippedSettingDisabledCount,
            LastDeltaConsumerDetonatorFireSafetySkippedNoSafeApiCount: deltaConsumerSummary.DetonatorFireSafetySkippedNoSafeApiCount,
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
            LastDeltaConsumerTunnelFireSkippedNoSafeApiCount: deltaConsumerSummary.TunnelFireSkippedNoSafeApiCount,
            LastDeltaConsumerTunnelFireRecoverabilityPreservedCount: deltaConsumerSummary.TunnelFireRecoverabilityPreservedCount,
            LastDeltaConsumerTunnelFireRecoverabilityUnknownCount: deltaConsumerSummary.TunnelFireRecoverabilityUnknownCount,
            LastDeltaConsumerPathInfrastructureConsideredDeltaCount: deltaConsumerSummary.PathInfrastructureConsideredDeltaCount,
            LastDeltaConsumerPathInfrastructureMatchedTargetCellCount: deltaConsumerSummary.PathInfrastructureMatchedTargetCellCount,
            LastDeltaConsumerPathInfrastructureDuplicateTargetSuppressedCount: deltaConsumerSummary.PathInfrastructureDuplicateTargetSuppressedCount,
            LastDeltaConsumerPathInfrastructureZeroCostTargetCount: deltaConsumerSummary.PathInfrastructureZeroCostTargetCount,
            LastDeltaConsumerPathInfrastructureDamagedTargetCount: deltaConsumerSummary.PathInfrastructureDamagedTargetCount,
            LastDeltaConsumerPathInfrastructureBlockedTargetCount: deltaConsumerSummary.PathInfrastructureBlockedTargetCount,
            LastDeltaConsumerPathInfrastructureSkippedNoSafeApiCount: deltaConsumerSummary.PathInfrastructureSkippedNoSafeApiCount,
            LastDeltaConsumerPathInfrastructureRepairEligibleTargetCount: deltaConsumerSummary.PathInfrastructureRepairEligibleTargetCount,
            LastDeltaConsumerPathInfrastructureTotalDamageApplied: deltaConsumerSummary.PathInfrastructureTotalDamageApplied,
            LastDeltaConsumerPowerInfrastructureConsideredDeltaCount: deltaConsumerSummary.PowerInfrastructureConsideredDeltaCount,
            LastDeltaConsumerPowerInfrastructureMatchedTargetCellCount: deltaConsumerSummary.PowerInfrastructureMatchedTargetCellCount,
            LastDeltaConsumerPowerInfrastructureDuplicateTargetSuppressedCount: deltaConsumerSummary.PowerInfrastructureDuplicateTargetSuppressedCount,
            LastDeltaConsumerPowerInfrastructureMetalOnlyNoOpTargetCount: deltaConsumerSummary.PowerInfrastructureMetalOnlyNoOpTargetCount,
            LastDeltaConsumerPowerInfrastructureDamagedTargetCount: deltaConsumerSummary.PowerInfrastructureDamagedTargetCount,
            LastDeltaConsumerPowerInfrastructureDisabledOrDisconnectedTargetCount: deltaConsumerSummary.PowerInfrastructureDisabledOrDisconnectedTargetCount,
            LastDeltaConsumerPowerInfrastructureSkippedNoSafeApiCount: deltaConsumerSummary.PowerInfrastructureSkippedNoSafeApiCount,
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
            LastDeltaConsumerWaterInfrastructureSkippedNoSafeApiCount: deltaConsumerSummary.WaterInfrastructureSkippedNoSafeApiCount,
            LastDeltaConsumerWaterInfrastructureRepairEligibleTargetCount: deltaConsumerSummary.WaterInfrastructureRepairEligibleTargetCount,
            LastDeltaConsumerWaterInfrastructureTotalDamageApplied: deltaConsumerSummary.WaterInfrastructureTotalDamageApplied,
            LastDeltaConsumerAlertCount: deltaConsumerSummary.AlertCount,
            LastPlayerFireAlertTick: alertCounters.LastAlertTick,
            LastPlayerFireAlertStartedFireCount: alertCounters.LastFireStartedCount,
            LastPlayerFireAlertFuelSpentCount: alertCounters.LastFuelSpentCount,
            LastPlayerFireAlertMaxHeat: alertCounters.LastMaxHeat,
            PlayerFireAlertNotificationCount: alertCounters.TotalNotificationCount,
            PlayerFireAlertPresentationFailureCount: alertCounters.PresentationFailureCount,
            PlayerFireAlertNotificationSent: alertCounters.LastNotificationSent,
            LastPlayerFireAlertMessage: alertCounters.LastMessage,
            VisualFieldSurfaceBound: visualFieldSurfaceState.IsBound,
            VisualFieldSurfaceCellCount: visualFieldSurfaceState.CellCount,
            VisualFieldSurfaceLastUpdatedTick: visualFieldSurfaceState.LastUpdatedTick,
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
            ActivePooledFireEffectCount: pooledEffectCounters.ActivePooledEffectCount,
            UpdatedVisualRegionCount: pooledEffectCounters.UpdatedVisualRegionCount,
            LastNonZeroUpdatedVisualRegionCount: pooledEffectCounters.LastNonZeroUpdatedVisualRegionCount,
            LastNonZeroUpdatedVisualRegionTick: pooledEffectCounters.LastNonZeroUpdatedVisualRegionTick,
            MaxPooledFireEffectCount: pooledEffectCounters.MaxActivePooledEffectCount,
            MaxUpdatedVisualRegionCount: pooledEffectCounters.MaxUpdatedVisualRegionCount,
            PooledFireEffectPresentationFailureCount: pooledEffectCounters.PresentationFailureCount,
            PooledFireEffectsVisibleEnabled: pooledEffectCounters.VisibleEffectsEnabled,
            PooledFireEffectsNativePrefabResolved: pooledEffectCounters.NativeEffectPrefabResolved,
            PooledFireEffectsNativePrefabName: pooledEffectCounters.LastNativeEffectPrefabName,
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
            FireSimPresetIgnitionBaseHeat: currentPreset.Parameters.FireIgnitionBaseHeat,
            FireSimPresetBurningNeighborHeatBonus: currentPreset.Parameters.FireBurningNeighborHeatBonus,
            FireSimPresetWaterSuppressionHeat: currentPreset.Parameters.FireWaterSuppressionHeat,
            FireSimPresetFuelBurnDownNumerator: currentPreset.Parameters.FireFuelBurnDownPressureNumerator,
            FireSimPresetFuelBurnDownDenominator: currentPreset.Parameters.FireFuelBurnDownPressureDenominator,
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
            WorldImportSafeUnavailableCount: _lastWorldImportSummary?.ProviderSafeUnavailableCounts.Values.Sum());
    }

    public void AttachBuildingBurnoutConsequenceApi(ITimberbornBuildingBurnoutConsequenceApi consequenceApi)
    {
        _buildingBurnoutConsequenceApi = consequenceApi ?? throw new ArgumentNullException(nameof(consequenceApi));
    }

    public void AttachBuildingBurnoutStimulusTargetProvider(
        ITimberbornQaBuildingBurnoutStimulusTargetProvider targetProvider)
    {
        _buildingBurnoutStimulusTargetProvider =
            targetProvider ?? throw new ArgumentNullException(nameof(targetProvider));
    }

    public void AttachStructureBurnDamageRollbackTargetApi(ITimberbornStructureBurnDamageRollbackTargetApi targetApi)
    {
        _structureBurnDamageRollbackTargetApi = targetApi ?? throw new ArgumentNullException(nameof(targetApi));
    }

    public void AttachStoredGoodBurnInventoryApi(ITimberbornStoredGoodBurnInventoryApi inventoryApi)
    {
        _storedGoodBurnInventoryApi = inventoryApi ?? throw new ArgumentNullException(nameof(inventoryApi));
    }

    public void AttachExplosiveInfrastructureTargetApi(ITimberbornExplosiveInfrastructureTargetApi targetApi)
    {
        _explosiveInfrastructureTargetApi = targetApi ?? throw new ArgumentNullException(nameof(targetApi));
    }

    public void AttachDetonatorFireSafetyTargetApi(ITimberbornDetonatorFireSafetyTargetApi targetApi)
    {
        _detonatorFireSafetyTargetApi = targetApi ?? throw new ArgumentNullException(nameof(targetApi));
    }

    public void AttachTunnelFireTargetApi(ITimberbornTunnelFireTargetApi targetApi)
    {
        _tunnelFireTargetApi = targetApi ?? throw new ArgumentNullException(nameof(targetApi));
    }

    public void AttachPathInfrastructureFireTargetApi(ITimberbornPathInfrastructureFireTargetApi targetApi)
    {
        _pathInfrastructureFireTargetApi = targetApi ?? throw new ArgumentNullException(nameof(targetApi));
    }

    public void AttachPowerInfrastructureFireTargetApi(ITimberbornPowerInfrastructureFireTargetApi targetApi)
    {
        _powerInfrastructureFireTargetApi = targetApi ?? throw new ArgumentNullException(nameof(targetApi));
    }

    public void AttachWaterInfrastructureFireTargetApi(ITimberbornWaterInfrastructureFireTargetApi targetApi)
    {
        _waterInfrastructureFireTargetApi = targetApi ?? throw new ArgumentNullException(nameof(targetApi));
    }

    private void Configure(TimberbornFireSystem fireSystem, TimberbornFireCadence? cadence)
    {
        _fireSystem?.Dispose();
        _explosiveInfrastructureHeatPulseSink?.Attach(fireSystem);
        _debugVisualSink.Clear();
        _gpuFieldRenderer.Clear();
        _pooledFireEffects.Clear();
        _playerFireAlerts.Clear();
        _playerFireAlertCameraFocus.ConfigureGrid(
            new FireGrid(
                fireSystem.Width ?? throw new InvalidOperationException("Fire system width is unavailable."),
                fireSystem.Height ?? throw new InvalidOperationException("Fire system height is unavailable."),
                fireSystem.Depth ?? throw new InvalidOperationException("Fire system depth is unavailable.")));
        _fireSystem = fireSystem;
        int cellCount = checked(
            (fireSystem.Width ?? throw new InvalidOperationException("Fire system width is unavailable.")) *
            (fireSystem.Height ?? throw new InvalidOperationException("Fire system height is unavailable.")) *
            (fireSystem.Depth ?? throw new InvalidOperationException("Fire system depth is unavailable.")));
        _autoDispatchDisabledReason = TimberbornAutoDispatchPolicy.IsAllowedCellCount(cellCount)
            ? null
            : $"map_too_large:cell_count={cellCount}:limit={TimberbornAutoDispatchPolicy.CellLimit}";
        if (_autoDispatchDisabledReason is not null)
        {
            _logSink.Warning(
                "wildfire_timberborn_auto_dispatch_disabled " +
                "reason=map_too_large " +
                $"cell_count={cellCount} " +
                $"limit={TimberbornAutoDispatchPolicy.CellLimit}");
        }

        _dispatcher = new TimberbornFixedCadenceFireDispatcher(
            fireSystem,
            cadence ?? TimberbornFireCadence.Default,
            _logSink,
            IsAutoDispatchEnabled);
        _gameUpdateId = 0;
        _logSink.Info(
            $"wildfire_timberborn_runtime_configured cadence_interval_ms={(cadence ?? TimberbornFireCadence.Default).Interval.TotalMilliseconds:F0}");
        _logSink.Info("wildfire_timberborn_delta_consequence_sink_bound lane=debug_visual_state");
        _logSink.Info("wildfire_timberborn_delta_consequence_sink_bound lane=gpu_field_renderer");
        _logSink.Info("wildfire_timberborn_delta_consequence_sink_bound lane=pooled_fire_smoke_ash_effects");
        _logSink.Info("wildfire_timberborn_delta_consequence_sink_bound lane=player_fire_alert");
        if (_buildingBurnoutConsequenceApi is not null)
        {
            _logSink.Info("wildfire_timberborn_delta_consequence_sink_bound lane=building_burnout_pause");
        }
        if (_structureBurnDamageRollbackTargetApi is not null)
        {
            _logSink.Info("wildfire_timberborn_delta_consequence_sink_bound lane=structure_burn_damage_rollback");
        }
        if (_storedGoodBurnInventoryApi is not null)
        {
            _logSink.Info("wildfire_timberborn_delta_consequence_sink_bound lane=stored_goods_burn");
        }
        if (_explosiveInfrastructureTargetApi is not null && _explosiveInfrastructureHeatPulseSink is not null)
        {
            _logSink.Info("wildfire_timberborn_delta_consequence_sink_bound lane=explosive_infrastructure");
        }
        if (_detonatorFireSafetyTargetApi is not null)
        {
            _logSink.Info("wildfire_timberborn_delta_consequence_sink_bound lane=detonator_fire_safety");
        }
        if (_tunnelFireTargetApi is not null)
        {
            _logSink.Info("wildfire_timberborn_delta_consequence_sink_bound lane=tunnel_fire");
        }
        if (_pathInfrastructureFireTargetApi is not null)
        {
            _logSink.Info("wildfire_timberborn_delta_consequence_sink_bound lane=path_infrastructure_fire");
        }
        if (_powerInfrastructureFireTargetApi is not null)
        {
            _logSink.Info("wildfire_timberborn_delta_consequence_sink_bound lane=power_infrastructure_fire");
        }
        if (_waterInfrastructureFireTargetApi is not null)
        {
            _logSink.Info("wildfire_timberborn_delta_consequence_sink_bound lane=water_infrastructure_fire");
        }
    }

    private TimberbornFireDeltaConsumerSinks CreateDeltaConsumerSinks()
    {
        return new TimberbornFireDeltaConsumerSinks(
            debugVisualSink: _debugVisualSink,
            visualEffectSink: new TimberbornCompositeFireVisualEffectSink(_gpuFieldRenderer, _pooledFireEffects),
            alertSink: _playerFireAlerts,
            buildingBurnoutConsequenceSink: _buildingBurnoutConsequenceApi is null
                ? null
                : new TimberbornBuildingBurnoutConsequenceSink(_buildingBurnoutConsequenceApi),
            structureBurnDamageRollbackSink: _structureBurnDamageRollbackTargetApi is null
                ? null
                : new TimberbornStructureBurnDamageRollbackSink(
                    _structureBurnDamageRollbackTargetApi,
                    logSink: _logSink),
            storedGoodBurnConsequenceSink: _storedGoodBurnInventoryApi is null
                ? null
                : new TimberbornStoredGoodBurnConsequenceSink(_storedGoodBurnInventoryApi, logSink: _logSink),
            explosiveInfrastructureConsequenceSink:
                _explosiveInfrastructureTargetApi is null || _explosiveInfrastructureHeatPulseSink is null
                    ? null
                    : new TimberbornExplosiveInfrastructureConsequenceSink(
                        () => TimberbornExplosiveInfrastructureConsequenceSettings.FromSnapshot(
                            _releaseSettings.GetSnapshot()),
                        _explosiveInfrastructureTargetApi,
                        _explosiveInfrastructureHeatPulseSink,
                        _logSink),
            detonatorFireSafetySink: _detonatorFireSafetyTargetApi is null
                ? null
                : new TimberbornDetonatorFireSafetySink(
                    () => _releaseSettings.GetSnapshot().IsDetonatorFireSafetyEnabled,
                    _detonatorFireSafetyTargetApi,
                    _logSink),
            tunnelFireSink: _tunnelFireTargetApi is null
                ? null
                : new TimberbornTunnelFireSink(
                    () => TimberbornTunnelFireSettings.FromSnapshot(_releaseSettings.GetSnapshot()),
                    _tunnelFireTargetApi,
                    _logSink),
            pathInfrastructureFireSink: _pathInfrastructureFireTargetApi is null
                ? null
                : new TimberbornPathInfrastructureFireSink(
                    _pathInfrastructureFireTargetApi,
                    logSink: _logSink),
            powerInfrastructureFireSink: _powerInfrastructureFireTargetApi is null
                ? null
                : new TimberbornPowerInfrastructureFireSink(
                    _powerInfrastructureFireTargetApi,
                    logSink: _logSink),
            waterInfrastructureFireSink: _waterInfrastructureFireTargetApi is null
                ? null
                : new TimberbornWaterInfrastructureFireSink(
                    _waterInfrastructureFireTargetApi,
                    logSink: _logSink));
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
        return IsWildfireEnabled() && _autoDispatchDisabledReason is null;
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
