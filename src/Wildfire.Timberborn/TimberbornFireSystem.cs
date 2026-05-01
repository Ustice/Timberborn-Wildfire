using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornFireSystem
{
    private readonly TimberbornFireCellMapper _cellMapper;
    private readonly ITimberbornFireSimulatorFactory? _simulatorFactory;
    private readonly ITimberbornFireLogSink _logSink;
    private IGpuFireSimulator? _fireSimulator;
    private FireGrid? _grid;
    private int _registeredChangeCountSinceLastDispatch;

    public TimberbornFireSystem(IGpuFireSimulator fireSimulator)
        : this(fireSimulator, new TimberbornFireCellMapper(), NullTimberbornFireLogSink.Instance)
    {
    }

    public TimberbornFireSystem(IGpuFireSimulator fireSimulator, TimberbornFireCellMapper cellMapper)
        : this(fireSimulator, cellMapper, NullTimberbornFireLogSink.Instance)
    {
    }

    public TimberbornFireSystem(
        IGpuFireSimulator fireSimulator,
        TimberbornFireCellMapper cellMapper,
        ITimberbornFireLogSink logSink)
    {
        if (fireSimulator is null)
        {
            throw new ArgumentNullException(nameof(fireSimulator));
        }

        if (cellMapper is null)
        {
            throw new ArgumentNullException(nameof(cellMapper));
        }

        if (logSink is null)
        {
            throw new ArgumentNullException(nameof(logSink));
        }

        _fireSimulator = fireSimulator;
        _grid = new FireGrid(fireSimulator.Width, fireSimulator.Height, fireSimulator.Depth);
        _cellMapper = cellMapper;
        _logSink = logSink;
        _logSink.Info(
            $"wildfire_timberborn_simulator_attached width={fireSimulator.Width} height={fireSimulator.Height} depth={fireSimulator.Depth}");
    }

    public TimberbornFireSystem(ITimberbornFireSimulatorFactory simulatorFactory)
        : this(simulatorFactory, new TimberbornFireCellMapper(), NullTimberbornFireLogSink.Instance)
    {
    }

    public TimberbornFireSystem(
        ITimberbornFireSimulatorFactory simulatorFactory,
        TimberbornFireCellMapper cellMapper,
        ITimberbornFireLogSink logSink)
    {
        if (simulatorFactory is null)
        {
            throw new ArgumentNullException(nameof(simulatorFactory));
        }

        if (cellMapper is null)
        {
            throw new ArgumentNullException(nameof(cellMapper));
        }

        if (logSink is null)
        {
            throw new ArgumentNullException(nameof(logSink));
        }

        _simulatorFactory = simulatorFactory;
        _cellMapper = cellMapper;
        _logSink = logSink;
    }

    public bool IsInitialized => _fireSimulator is not null;

    public int? Width => _fireSimulator?.Width;

    public int? Height => _fireSimulator?.Height;

    public int? Depth => _fireSimulator?.Depth;

    public int RegisteredChangeCountSinceLastDispatch => _registeredChangeCountSinceLastDispatch;

    public uint? LastTick { get; private set; }

    public int? LastDeltaCount { get; private set; }

    public void Initialize(FireGrid grid, IEnumerable<TimberbornCellSource> sources)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        if (_simulatorFactory is null)
        {
            throw new InvalidOperationException("Timberborn fire system was constructed with an existing simulator and cannot initialize another one.");
        }

        ushort[] initialCells = _cellMapper.CreateInitialCells(grid, sources);
        _fireSimulator = _simulatorFactory.Create(grid, initialCells);
        _grid = grid;
        _registeredChangeCountSinceLastDispatch = 0;
        LastTick = null;
        LastDeltaCount = null;
        _logSink.Info(
            $"wildfire_timberborn_initialized width={grid.Width} height={grid.Height} depth={grid.Depth} cell_count={grid.CellCount}");
    }

    public GpuFireStepResult Tick()
    {
        IGpuFireSimulator fireSimulator = RequireSimulator();
        int pendingChangeCount = _registeredChangeCountSinceLastDispatch;

        _logSink.Info($"wildfire_timberborn_dispatch_started pending_changes={pendingChangeCount}");
        try
        {
            GpuFireStepResult result = fireSimulator.Tick();
            _registeredChangeCountSinceLastDispatch = 0;
            LastTick = result.Tick;
            LastDeltaCount = result.Deltas.Count;
            _logSink.Info(
                $"wildfire_timberborn_dispatch_completed tick={result.Tick} delta_count={result.Deltas.Count}");

            return result;
        }
        catch (Exception exception)
        {
            _logSink.Warning($"wildfire_timberborn_dispatch_failed message=\"{exception.Message}\"");
            throw;
        }
    }

    public void RegisterHeat(int cellIndex, byte heat)
    {
        RegisterChange(new FireSimChange(CellIndex: cellIndex, AddHeat: heat), "heat");
    }

    public void RegisterChange(FireSimChange change)
    {
        RegisterChange(change, "external");
    }

    public void RegisterMappedCellChanges(FireGrid grid, IEnumerable<TimberbornCellSource> sources)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }
        RequireMatchingGrid(grid);

        FireSimChange[] changes = _cellMapper.CreateSetCellChanges(grid, sources).ToArray();

        changes
            .ToList()
            .ForEach(change => RegisterChange(change, "mapped_cell"));
        _logSink.Info($"wildfire_timberborn_mapped_changes_registered count={changes.Length}");
    }

    public void RegisterMappedCellChanges(IEnumerable<TimberbornCellSource> sources)
    {
        RegisterMappedCellChanges(RequireGrid(), sources);
    }

    public IDisposable Subscribe(IFireSimListener listener)
    {
        return RequireSimulator().Subscribe(listener);
    }

    private void RegisterChange(FireSimChange change, string source)
    {
        RequireSimulator().RegisterChange(change);
        _registeredChangeCountSinceLastDispatch++;
        _logSink.Info($"wildfire_timberborn_change_registered source={source} cell_index={change.CellIndex}");
    }

    private IGpuFireSimulator RequireSimulator()
    {
        return _fireSimulator ??
            throw new InvalidOperationException("Timberborn fire system must be initialized before dispatching or registering changes.");
    }

    private FireGrid RequireGrid()
    {
        return _grid ??
            throw new InvalidOperationException("Timberborn fire system must be initialized before mapping cell changes without an explicit grid.");
    }

    private void RequireMatchingGrid(FireGrid grid)
    {
        IGpuFireSimulator fireSimulator = RequireSimulator();

        if (grid.Width != fireSimulator.Width || grid.Height != fireSimulator.Height || grid.Depth != fireSimulator.Depth)
        {
            throw new ArgumentException(
                $"Mapped cell grid {grid.Width}x{grid.Height}x{grid.Depth} must match simulator grid " +
                $"{fireSimulator.Width}x{fireSimulator.Height}x{fireSimulator.Depth}.",
                nameof(grid));
        }
    }
}

public interface ITimberbornFireSimulatorFactory
{
    IGpuFireSimulator Create(FireGrid grid, ReadOnlySpan<ushort> initialCells);
}

public interface ITimberbornFireLogSink
{
    void Info(string message);

    void Warning(string message);
}

public sealed class TimberbornFixedCadenceFireDispatcher
{
    private readonly TimberbornFireSystem _fireSystem;
    private readonly TimberbornFireCadence _cadence;
    private readonly ITimberbornFireLogSink _logSink;
    private TimeSpan _accumulatedElapsed = TimeSpan.Zero;
    private long? _lastProcessedGameUpdateId;

    public TimberbornFixedCadenceFireDispatcher(TimberbornFireSystem fireSystem)
        : this(fireSystem, TimberbornFireCadence.Default, NullTimberbornFireLogSink.Instance)
    {
    }

    public TimberbornFixedCadenceFireDispatcher(
        TimberbornFireSystem fireSystem,
        TimberbornFireCadence cadence,
        ITimberbornFireLogSink logSink)
    {
        if (fireSystem is null)
        {
            throw new ArgumentNullException(nameof(fireSystem));
        }

        if (logSink is null)
        {
            throw new ArgumentNullException(nameof(logSink));
        }

        _fireSystem = fireSystem;
        _cadence = cadence;
        _logSink = logSink;
        _logSink.Info($"wildfire_timberborn_cadence_configured interval_ms={_cadence.Interval.TotalMilliseconds:F0}");
    }

    public TimberbornFireDispatchResult Update(TimberbornFireUpdate update)
    {
        if (_lastProcessedGameUpdateId == update.GameUpdateId)
        {
            _logSink.Warning($"wildfire_timberborn_dispatch_skipped_duplicate game_update_id={update.GameUpdateId}");
            return TimberbornFireDispatchResult.Skipped("duplicate-game-update", _accumulatedElapsed);
        }

        _lastProcessedGameUpdateId = update.GameUpdateId;
        _accumulatedElapsed += update.Elapsed;

        if (_accumulatedElapsed < _cadence.Interval)
        {
            _logSink.Info(
                $"wildfire_timberborn_dispatch_waiting game_update_id={update.GameUpdateId} accumulated_ms={_accumulatedElapsed.TotalMilliseconds:F0}");
            return TimberbornFireDispatchResult.Skipped("cadence-not-reached", _accumulatedElapsed);
        }

        _accumulatedElapsed -= _cadence.Interval;
        GpuFireStepResult step = _fireSystem.Tick();

        return TimberbornFireDispatchResult.Dispatched(step, _accumulatedElapsed);
    }
}

public readonly record struct TimberbornFireCadence
{
    public static readonly TimberbornFireCadence Default = FromSeconds(1);

    public TimberbornFireCadence(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "Fire dispatch cadence must be positive.");
        }

        Interval = interval;
    }

    public TimeSpan Interval { get; }

    public static TimberbornFireCadence FromSeconds(double seconds)
    {
        return new TimberbornFireCadence(TimeSpan.FromSeconds(seconds));
    }
}

public readonly record struct TimberbornFireUpdate
{
    public TimberbornFireUpdate(long gameUpdateId, TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed), elapsed, "Game update elapsed time cannot be negative.");
        }

        GameUpdateId = gameUpdateId;
        Elapsed = elapsed;
    }

    public long GameUpdateId { get; }

    public TimeSpan Elapsed { get; }
}

public sealed record TimberbornFireDispatchResult(
    bool DidDispatch,
    GpuFireStepResult? Step,
    string Reason,
    TimeSpan RemainingElapsed)
{
    public static TimberbornFireDispatchResult Dispatched(GpuFireStepResult step, TimeSpan remainingElapsed)
    {
        return new TimberbornFireDispatchResult(true, step, "dispatched", remainingElapsed);
    }

    public static TimberbornFireDispatchResult Skipped(string reason, TimeSpan remainingElapsed)
    {
        return new TimberbornFireDispatchResult(false, null, reason, remainingElapsed);
    }
}

public sealed class NullTimberbornFireLogSink : ITimberbornFireLogSink
{
    public static readonly NullTimberbornFireLogSink Instance = new();

    private NullTimberbornFireLogSink()
    {
    }

    public void Info(string message)
    {
    }

    public void Warning(string message)
    {
    }
}
