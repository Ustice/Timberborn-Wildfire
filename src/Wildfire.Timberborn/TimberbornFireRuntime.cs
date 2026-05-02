using Timberborn.SingletonSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornFireRuntime :
    ILoadableSingleton,
    IUnloadableSingleton,
    IUpdatableSingleton,
    ITimberbornQaCommandStateProvider,
    ITimberbornQaDeltaStimulus
{
    private readonly ITimberbornFireLogSink _logSink;
    private readonly TimberbornFireDebugVisualStateSink _debugVisualSink;
    private TimberbornFireSystem? _fireSystem;
    private TimberbornFixedCadenceFireDispatcher? _dispatcher;
    private long _gameUpdateId;
    private bool _isLoaded;

    public TimberbornFireRuntime()
    {
        _logSink = new UnityTimberbornFireLogSink();
        _debugVisualSink = new TimberbornFireDebugVisualStateSink();
    }

    public void Load()
    {
        _isLoaded = true;
        _logSink.Info(
            $"wildfire_timberborn_adapter_started cadence_interval_ms={TimberbornFireCadence.Default.Interval.TotalMilliseconds:F0}");
        _logSink.Info(
            $"wildfire_timberborn_runtime_ready cadence_interval_ms={TimberbornFireCadence.Default.Interval.TotalMilliseconds:F0}");
    }

    public void Unload()
    {
        _logSink.Info(
            $"wildfire_timberborn_adapter_stopping game_update_id={_gameUpdateId} simulator_integrated={(_fireSystem is { IsInitialized: true }).ToString().ToLowerInvariant()}");
        _fireSystem?.Dispose();
        _dispatcher = null;
        _fireSystem = null;
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

        TimberbornFireSystem fireSystem = new(
            simulatorFactory,
            new TimberbornFireCellMapper(),
            _logSink,
            CreateDeltaConsumerSinks());
        fireSystem.Initialize(grid, sources);
        Configure(fireSystem, cadence);
        _logSink.Info(
            $"wildfire_timberborn_runtime_simulator_initialized width={fireSystem.Width} height={fireSystem.Height} depth={fireSystem.Depth}");
    }

    public void RegisterHeat(int cellIndex, byte heat)
    {
        RequireFireSystem().RegisterHeat(cellIndex, heat);
    }

    public void RegisterChange(FireSimChange change)
    {
        RequireFireSystem().RegisterChange(change);
    }

    public void RegisterMappedCellChanges(IEnumerable<TimberbornCellSource> sources)
    {
        RequireFireSystem().RegisterMappedCellChanges(sources);
    }

    public TimberbornQaDeltaStimulusResult QueueFixedDeltaStimulus()
    {
        TimberbornQaDeltaStimulusResult result = RequireFireSystem().QueueFixedQaDeltaStimulus();
        _logSink.Info(
            "wildfire_timberborn_qa_delta_stimulus_queued " +
            $"cell_index={result.CellIndex} " +
            $"x={result.X} " +
            $"y={result.Y} " +
            $"z={result.Z} " +
            $"set_cell={result.SetCell}");

        return result;
    }

    public TimberbornQaCommandState GetState()
    {
        if (_fireSystem is not { IsInitialized: true } fireSystem)
        {
            return new TimberbornQaCommandState(
                IsSimulatorIntegrated: false,
                IsGameContextRuntimeLoaded: _isLoaded);
        }

        TimberbornFireDeltaConsumerSummary deltaConsumerSummary = fireSystem.LastDeltaConsumerSummary;

        return new TimberbornQaCommandState(
            IsSimulatorIntegrated: true,
            IsGameContextRuntimeLoaded: _isLoaded,
            Width: fireSystem.Width,
            Height: fireSystem.Height,
            Depth: fireSystem.Depth,
            TickCount: fireSystem.LastTick,
            QueuedChangeCount: fireSystem.RegisteredChangeCountSinceLastDispatch,
            LastDeltaCount: fireSystem.LastDeltaCount,
            LastDeltaConsumerChangedCellCount: deltaConsumerSummary.ChangedCellCount,
            LastDeltaConsumerDebugVisualCellCount: _debugVisualSink.States.Count,
            LastDeltaConsumerStartedBurningCount: deltaConsumerSummary.StartedBurningCount,
            LastDeltaConsumerFuelDepletedCount: deltaConsumerSummary.FuelDepletedCount,
            LastDeltaConsumerVisualEffectEventCount: deltaConsumerSummary.VisualEffectEventCount,
            LastDeltaConsumerGameplayConsequenceCount: deltaConsumerSummary.GameplayConsequenceCount,
            LastDeltaConsumerAlertCount: deltaConsumerSummary.AlertCount);
    }

    private void Configure(TimberbornFireSystem fireSystem, TimberbornFireCadence? cadence)
    {
        _fireSystem?.Dispose();
        _debugVisualSink.Clear();
        _fireSystem = fireSystem;
        _dispatcher = new TimberbornFixedCadenceFireDispatcher(
            fireSystem,
            cadence ?? TimberbornFireCadence.Default,
            _logSink);
        _gameUpdateId = 0;
        _logSink.Info(
            $"wildfire_timberborn_runtime_configured cadence_interval_ms={(cadence ?? TimberbornFireCadence.Default).Interval.TotalMilliseconds:F0}");
        _logSink.Info("wildfire_timberborn_delta_consequence_sink_bound lane=debug_visual_state");
    }

    private TimberbornFireDeltaConsumerSinks CreateDeltaConsumerSinks()
    {
        return new TimberbornFireDeltaConsumerSinks(debugVisualSink: _debugVisualSink);
    }

    private TimberbornFireSystem RequireFireSystem()
    {
        return _fireSystem ??
            throw new InvalidOperationException("Timberborn fire runtime must be initialized before registering simulator changes.");
    }
}
