using Timberborn.SingletonSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornFireRuntime : ILoadableSingleton, IUnloadableSingleton, IUpdatableSingleton, ITimberbornQaCommandStateProvider
{
    private readonly ITimberbornFireLogSink _logSink;
    private TimberbornFireSystem? _fireSystem;
    private TimberbornFixedCadenceFireDispatcher? _dispatcher;
    private long _gameUpdateId;

    public TimberbornFireRuntime()
        : this(new UnityTimberbornFireLogSink())
    {
    }

    internal TimberbornFireRuntime(ITimberbornFireLogSink logSink)
    {
        if (logSink is null)
        {
            throw new ArgumentNullException(nameof(logSink));
        }

        _logSink = logSink;
    }

    public void Load()
    {
        _logSink.Info(
            $"wildfire_timberborn_runtime_ready cadence_interval_ms={TimberbornFireCadence.Default.Interval.TotalMilliseconds:F0}");
    }

    public void Unload()
    {
        _dispatcher = null;
        _fireSystem = null;
        _gameUpdateId = 0;
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

        Configure(new TimberbornFireSystem(fireSimulator, new TimberbornFireCellMapper(), _logSink), cadence);
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

        TimberbornFireSystem fireSystem = new(simulatorFactory, new TimberbornFireCellMapper(), _logSink);
        fireSystem.Initialize(grid, sources);
        Configure(fireSystem, cadence);
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

    public TimberbornQaCommandState GetState()
    {
        if (_fireSystem is not { IsInitialized: true } fireSystem)
        {
            return TimberbornQaCommandState.Placeholder;
        }

        return new TimberbornQaCommandState(
            IsSimulatorIntegrated: true,
            Width: fireSystem.Width,
            Height: fireSystem.Height,
            Depth: fireSystem.Depth,
            TickCount: fireSystem.LastTick,
            QueuedChangeCount: fireSystem.RegisteredChangeCountSinceLastDispatch,
            LastDeltaCount: fireSystem.LastDeltaCount);
    }

    private void Configure(TimberbornFireSystem fireSystem, TimberbornFireCadence? cadence)
    {
        _fireSystem = fireSystem;
        _dispatcher = new TimberbornFixedCadenceFireDispatcher(
            fireSystem,
            cadence ?? TimberbornFireCadence.Default,
            _logSink);
        _gameUpdateId = 0;
    }

    private TimberbornFireSystem RequireFireSystem()
    {
        return _fireSystem ??
            throw new InvalidOperationException("Timberborn fire runtime must be initialized before registering simulator changes.");
    }
}
