using System.Diagnostics;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornFireSystem : IDisposable
{
    private static readonly ushort QaDeltaStimulusCell = PackedCell.Pack(
        fuel: 15,
        heat: 15,
        flammability: 3,
        water: 0,
        terrain: 1,
        heatLoss: 1);
    private static readonly ushort QaBuildingBurnoutPrimedCell = PackedCell.Pack(
        fuel: TimberbornBuildingAdapter.WoodLikeFuel,
        heat: 15,
        flammability: TimberbornBuildingAdapter.WoodLikeFlammability,
        water: 0,
        terrain: 1,
        heatLoss: TimberbornBuildingAdapter.WoodLikeHeatLoss);
    private static readonly ushort QaBuildingBurnoutSpentCell = PackedCell.Pack(
        fuel: 0,
        heat: 15,
        flammability: TimberbornBuildingAdapter.WoodLikeFlammability,
        water: 0,
        terrain: 1,
        heatLoss: TimberbornBuildingAdapter.WoodLikeHeatLoss);
    private const byte QaWaterSuppressionWater = 3;

    private readonly TimberbornFireCellMapper _cellMapper;
    private readonly ITimberbornFireSimulatorFactory? _simulatorFactory;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly TimberbornFireDeltaConsumer _deltaConsumer;
    private IGpuFireSimulator? _fireSimulator;
    private FireGrid? _grid;
    private int _registeredChangeCountSinceLastDispatch;
    private TimberbornQaBurnDurationProofState _burnDurationProofState =
        TimberbornQaBurnDurationProofState.Placeholder;

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
        ITimberbornFireLogSink logSink,
        TimberbornFireDeltaConsumerSinks? deltaConsumerSinks = null)
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
        _deltaConsumer = new TimberbornFireDeltaConsumer(logSink, deltaConsumerSinks ?? TimberbornFireDeltaConsumerSinks.Null);
        LastTick = 0;
        LastDeltaCount = 0;
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
        ITimberbornFireLogSink logSink,
        TimberbornFireDeltaConsumerSinks? deltaConsumerSinks = null)
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
        _deltaConsumer = new TimberbornFireDeltaConsumer(logSink, deltaConsumerSinks ?? TimberbornFireDeltaConsumerSinks.Null);
    }

    public bool IsInitialized => _fireSimulator is not null;

    public int? Width => _fireSimulator?.Width;

    public int? Height => _fireSimulator?.Height;

    public int? Depth => _fireSimulator?.Depth;

    public int RegisteredChangeCountSinceLastDispatch => _registeredChangeCountSinceLastDispatch;

    public uint? LastTick { get; private set; }

    public int? LastDeltaCount { get; private set; }

    public TimberbornFireDeltaConsumerSummary LastDeltaConsumerSummary => _deltaConsumer.LastSummary;

    public uint LastPositiveWaterChangedTick => _deltaConsumer.LastPositiveWaterChangedTick;

    public int LastPositiveWaterChangedCount => _deltaConsumer.LastPositiveWaterChangedCount;

    public uint LastPositiveBuildingBurnoutAppliedTick => _deltaConsumer.LastPositiveBuildingBurnoutAppliedTick;

    public int LastPositiveBuildingBurnoutAppliedCount => _deltaConsumer.LastPositiveBuildingBurnoutAppliedCount;

    public TimberbornGpuVisualFieldSurfaceState VisualFieldSurfaceState =>
        (_fireSimulator as ITimberbornGpuVisualFieldStateProvider)?.VisualFieldSurfaceState ??
        TimberbornGpuVisualFieldSurfaceState.Unbound;

    public TimberbornQaBurnDurationProofState BurnDurationProofState => _burnDurationProofState;

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
        DisposeSimulator();
        _fireSimulator = _simulatorFactory.Create(grid, initialCells);
        _grid = grid;
        _registeredChangeCountSinceLastDispatch = 0;
        LastTick = 0;
        LastDeltaCount = 0;
        _burnDurationProofState = TimberbornQaBurnDurationProofState.Placeholder;
        _deltaConsumer.Reset();
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
            Stopwatch stopwatch = Stopwatch.StartNew();
            GpuFireStepResult result = fireSimulator.Tick();
            stopwatch.Stop();
            _registeredChangeCountSinceLastDispatch = 0;
            LastTick = result.Tick;
            LastDeltaCount = result.Deltas.Count;
            _deltaConsumer.Consume(result.Tick, result.Deltas.ToArray());
            UpdateBurnDurationProof(result.Tick, result.Deltas);
            _logSink.Info(
                $"wildfire_timberborn_dispatch_completed tick={result.Tick} delta_count={result.Deltas.Count} elapsed_ms={stopwatch.Elapsed.TotalMilliseconds:F3}");

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
            .ForEach(change => RegisterChange(change, "mapped_cell", shouldLog: false));
        _logSink.Info(
            $"wildfire_timberborn_changes_registered source=mapped_cell count={changes.Length} pending_changes={_registeredChangeCountSinceLastDispatch}");
    }

    public void RegisterMappedCellChanges(IEnumerable<TimberbornCellSource> sources)
    {
        RegisterMappedCellChanges(RequireGrid(), sources);
    }

    public TimberbornQaDeltaStimulusResult QueueFixedQaDeltaStimulus()
    {
        FireGrid grid = RequireGrid();
        int x = grid.Width / 2;
        int y = grid.Height / 2;
        int z = grid.Depth / 2;
        int cellIndex = grid.ToIndex(x, y, z);
        RegisterChange(new FireSimChange(CellIndex: cellIndex, SetCell: QaDeltaStimulusCell), "qa_delta_stimulus");

        return new TimberbornQaDeltaStimulusResult(cellIndex, x, y, z, QaDeltaStimulusCell);
    }

    public TimberbornQaBuildingBurnoutStimulusResult QueueBuildingBurnoutQaStimulus(
        ITimberbornQaBuildingBurnoutStimulusTargetProvider targetProvider)
    {
        if (targetProvider is null)
        {
            throw new ArgumentNullException(nameof(targetProvider));
        }

        TimberbornQaBuildingBurnoutStimulusTarget target = targetProvider.FindTarget(RequireGrid());
        RegisterChange(
            new FireSimChange(CellIndex: target.CellIndex, SetCell: QaBuildingBurnoutPrimedCell),
            "qa_building_burnout_prime");
        RegisterChange(
            new FireSimChange(CellIndex: target.CellIndex, SetCell: QaBuildingBurnoutSpentCell),
            "qa_building_burnout_stimulus");

        return new TimberbornQaBuildingBurnoutStimulusResult(
            target.CellIndex,
            target.X,
            target.Y,
            target.Z,
            target.ScannedCellCount,
            QaBuildingBurnoutPrimedCell,
            QaBuildingBurnoutSpentCell,
            QueuedSetCellChangeCount: 2);
    }

    public TimberbornQaWaterSuppressionStimulusResult QueueWaterSuppressionQaStimulus()
    {
        FireGrid grid = RequireGrid();
        int x = grid.Width / 2;
        int y = grid.Height / 2;
        int z = grid.Depth / 2;
        int cellIndex = grid.ToIndex(x, y, z);
        RegisterChange(
            new FireSimChange(CellIndex: cellIndex, SetWater: QaWaterSuppressionWater),
            "qa_water_suppression");

        return new TimberbornQaWaterSuppressionStimulusResult(
            cellIndex,
            x,
            y,
            z,
            QaWaterSuppressionWater,
            QueuedWaterChangeCount: 1);
    }

    public TimberbornQaBurnDurationStimulusResult QueueBurnDurationQaStimulus(string target)
    {
        FireGrid grid = RequireGrid();
        TimberbornQaBurnDurationStimulusTarget selectedTarget =
            TimberbornQaBurnDurationStimulusTargets.SelectTarget(grid, target);
        ushort setCell = PackedCell.Pack(
            fuel: selectedTarget.InitialFuel,
            heat: 15,
            flammability: 3,
            water: 0,
            terrain: 1,
            heatLoss: 1);
        RegisterChange(
            new FireSimChange(CellIndex: selectedTarget.CellIndex, SetCell: setCell),
            "qa_burn_duration_stimulus");

        _burnDurationProofState = new TimberbornQaBurnDurationProofState(
            selectedTarget.Target,
            selectedTarget.CellIndex,
            selectedTarget.X,
            selectedTarget.Y,
            selectedTarget.Z,
            selectedTarget.InitialFuel,
            LastTick ?? 0,
            TimberbornQaBurnDurationStimulusTargets.DefaultTimeoutTicks);
        _logSink.Info(ToBurnDurationProofLogToken());

        return new TimberbornQaBurnDurationStimulusResult(
            selectedTarget.Target,
            selectedTarget.CellIndex,
            selectedTarget.X,
            selectedTarget.Y,
            selectedTarget.Z,
            selectedTarget.InitialFuel,
            setCell,
            TimberbornQaBurnDurationStimulusTargets.DefaultTimeoutTicks,
            QueuedSetCellChangeCount: 1);
    }

    public IDisposable Subscribe(IFireSimListener listener)
    {
        return RequireSimulator().Subscribe(listener);
    }

    private void RegisterChange(FireSimChange change, string source, bool shouldLog = true)
    {
        RequireSimulator().RegisterChange(change);
        _registeredChangeCountSinceLastDispatch++;
        if (shouldLog)
        {
            _logSink.Info(
                $"wildfire_timberborn_changes_registered source={source} count=1 pending_changes={_registeredChangeCountSinceLastDispatch}");
        }
    }

    private void UpdateBurnDurationProof(uint tick, IReadOnlyList<CellDelta> deltas)
    {
        if (_burnDurationProofState.Status == "placeholder" ||
            _burnDurationProofState.DepletionTick.HasValue ||
            _burnDurationProofState.TimedOut)
        {
            return;
        }

        CellDelta? targetDelta = deltas
            .Select(delta => (CellDelta?)delta)
            .FirstOrDefault(delta => delta?.CellIndex == _burnDurationProofState.CellIndex);
        TimberbornQaBurnDurationProofState nextState = _burnDurationProofState;

        if (targetDelta is { } delta)
        {
            bool isBurning = PackedCell.IsBurning(delta.NewCell);
            int oldFuel = PackedCell.Fuel(delta.OldCell);
            int newFuel = PackedCell.Fuel(delta.NewCell);
            uint? burnStartTick = nextState.BurnStartTick;
            if (!burnStartTick.HasValue && isBurning)
            {
                burnStartTick = tick;
            }

            uint? depletionTick = nextState.DepletionTick;
            uint? elapsedBurnTicks = nextState.ElapsedBurnTicks;
            if (burnStartTick.HasValue && oldFuel > 0 && newFuel == 0)
            {
                depletionTick = tick;
                elapsedBurnTicks = checked((tick - burnStartTick.Value) + 1);
            }

            nextState = nextState with
            {
                BurnStartTick = burnStartTick,
                DepletionTick = depletionTick,
                ElapsedBurnTicks = elapsedBurnTicks,
                Status = depletionTick.HasValue
                    ? "depleted"
                    : burnStartTick.HasValue ? "burning" : "queued",
            };
        }

        if (nextState is { BurnStartTick: not null, DepletionTick: null } &&
            tick - nextState.BurnStartTick.Value >= nextState.TimeoutTicks)
        {
            nextState = nextState with
            {
                TimedOut = true,
                ElapsedBurnTicks = checked((tick - nextState.BurnStartTick.Value) + 1),
                Status = "no_depletion_timeout",
            };
        }

        if (!Equals(nextState, _burnDurationProofState))
        {
            _burnDurationProofState = nextState;
            _logSink.Info(ToBurnDurationProofLogToken());
        }
    }

    private string ToBurnDurationProofLogToken()
    {
        return "wildfire_timberborn_qa_burn_duration_status " +
            $"target={_burnDurationProofState.Target} " +
            $"cell_index={_burnDurationProofState.CellIndex} " +
            $"x={_burnDurationProofState.X} " +
            $"y={_burnDurationProofState.Y} " +
            $"z={_burnDurationProofState.Z} " +
            $"initial_fuel={_burnDurationProofState.InitialFuel} " +
            $"queued_tick={_burnDurationProofState.QueuedTick} " +
            $"burn_start_tick={FormatNumber(_burnDurationProofState.BurnStartTick)} " +
            $"depletion_tick={FormatNumber(_burnDurationProofState.DepletionTick)} " +
            $"elapsed_burn_ticks={FormatNumber(_burnDurationProofState.ElapsedBurnTicks)} " +
            $"timeout_ticks={_burnDurationProofState.TimeoutTicks} " +
            $"timed_out={_burnDurationProofState.TimedOut.ToString().ToLowerInvariant()} " +
            $"status={_burnDurationProofState.Status}";
    }

    private static string FormatNumber(uint? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }

    private IGpuFireSimulator RequireSimulator()
    {
        return _fireSimulator ??
            throw new InvalidOperationException("Timberborn fire system must be initialized before dispatching or registering changes.");
    }

    public void Dispose()
    {
        DisposeSimulator();
    }

    private void DisposeSimulator()
    {
        if (_fireSimulator is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _fireSimulator = null;
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
    private readonly Func<bool> _isDispatchEnabled;
    private TimeSpan _accumulatedElapsed = TimeSpan.Zero;
    private long? _lastProcessedGameUpdateId;
    private bool _loggedWaitingForCurrentInterval;
    private bool _loggedDisabled;

    public TimberbornFixedCadenceFireDispatcher(TimberbornFireSystem fireSystem)
        : this(fireSystem, TimberbornFireCadence.Default, NullTimberbornFireLogSink.Instance)
    {
    }

    public TimberbornFixedCadenceFireDispatcher(
        TimberbornFireSystem fireSystem,
        TimberbornFireCadence cadence,
        ITimberbornFireLogSink logSink,
        Func<bool>? isDispatchEnabled = null)
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
        _isDispatchEnabled = isDispatchEnabled ?? (() => true);
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

        if (!_isDispatchEnabled())
        {
            _accumulatedElapsed = TimeSpan.Zero;
            _loggedWaitingForCurrentInterval = false;
            if (!_loggedDisabled)
            {
                _logSink.Info(
                    $"wildfire_timberborn_dispatch_skipped_disabled game_update_id={update.GameUpdateId}");
                _loggedDisabled = true;
            }

            return TimberbornFireDispatchResult.Skipped("wildfire-disabled", _accumulatedElapsed);
        }

        _loggedDisabled = false;
        _accumulatedElapsed += update.Elapsed;

        if (_accumulatedElapsed < _cadence.Interval)
        {
            if (!_loggedWaitingForCurrentInterval)
            {
                _logSink.Info(
                    $"wildfire_timberborn_dispatch_waiting game_update_id={update.GameUpdateId} accumulated_ms={_accumulatedElapsed.TotalMilliseconds:F0}");
                _loggedWaitingForCurrentInterval = true;
            }

            return TimberbornFireDispatchResult.Skipped("cadence-not-reached", _accumulatedElapsed);
        }

        _accumulatedElapsed -= _cadence.Interval;
        _loggedWaitingForCurrentInterval = false;
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
