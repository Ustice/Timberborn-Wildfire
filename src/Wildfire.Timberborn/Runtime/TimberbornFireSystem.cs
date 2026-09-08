using System.Diagnostics;
using Wildfire.Core;

namespace Wildfire.Timberborn.Runtime;

public sealed class TimberbornFireSystem : IDisposable, ITimberbornQaWorld
{
    private readonly TimberbornFireCellMapper _cellMapper;
    private readonly ITimberbornFireSimulatorFactory? _simulatorFactory;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly TimberbornFireDeltaConsumer _deltaConsumer;
    private IGpuFireSimulator? _fireSimulator;
    private FireGrid? _grid;
    private TimberbornImportedFieldTarget[] _importedTargets = Array.Empty<TimberbornImportedFieldTarget>();
    private int _registeredChangeCountSinceLastDispatch;
    internal ITimberbornFireDispatchHost? HostDispatch { get; set; }
    public TimberbornQaController Qa { get; }

    public TimberbornSustainedIgnitionScheduler SustainedIgnition { get; }
    private TimberbornContaminationFireConsequenceSummary _contaminationFireSummary =
        TimberbornContaminationFireConsequenceSummary.Empty;
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
        SustainedIgnition = new TimberbornSustainedIgnitionScheduler(this, logSink);
        Qa = new TimberbornQaController(this, SustainedIgnition, logSink);
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
        SustainedIgnition = new TimberbornSustainedIgnitionScheduler(this, logSink);
        Qa = new TimberbornQaController(this, SustainedIgnition, logSink);
    }

    public bool IsInitialized => _fireSimulator is not null;

    public IGpuFireSimulator? Simulator => _fireSimulator;

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

    public uint LastPositiveBurnDamageAppliedTick => _deltaConsumer.LastPositiveBurnDamageAppliedTick;

    public int LastPositiveBurnDamageAppliedTargetCount => _deltaConsumer.LastPositiveBurnDamageAppliedTargetCount;

    public int LastPositiveBurnDamageTotalDamageApplied => _deltaConsumer.LastPositiveBurnDamageTotalDamageApplied;

    public uint LastPositiveStructureBurnDamageRollbackTick =>
        _deltaConsumer.LastPositiveStructureBurnDamageRollbackTick;

    public int LastPositiveStructureBurnDamageRollbackUnfinishedStageCount =>
        _deltaConsumer.LastPositiveStructureBurnDamageRollbackUnfinishedStageCount;

    public int LastPositiveStructureBurnDamageRollbackConstructionPhaseEnteredCount =>
        _deltaConsumer.LastPositiveStructureBurnDamageRollbackConstructionPhaseEnteredCount;


    public int LastPositiveStructureBurnDamageRollbackTotalDamageApplied =>
        _deltaConsumer.LastPositiveStructureBurnDamageRollbackTotalDamageApplied;

    public TimberbornGpuVisualFieldSurfaceState VisualFieldSurfaceState =>
        (_fireSimulator as ITimberbornGpuVisualFieldStateProvider)?.VisualFieldSurfaceState ??
        TimberbornGpuVisualFieldSurfaceState.Unbound;

    public TimberbornContaminationFireConsequenceSummary ContaminationFireSummary => _contaminationFireSummary;

    public IReadOnlyList<TimberbornImportedFieldTarget> ImportedTargets => _importedTargets;

    public int LastPersistentRestoreNoLiveFuelCellsCleared { get; private set; }

    public bool TryApplyPreset(TimberbornFireSimParameterPreset preset)
    {
        if (preset.SustainedIgnitionDispatchTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(preset), "Sustained ignition duration must be positive.");
        }

        if (_fireSimulator is not ITimberbornConfigurableFireSimParameters configurable)
        {
            return false;
        }

        configurable.UpdateParameters(preset.Parameters);
        SustainedIgnition.DurationTicks = preset.SustainedIgnitionDispatchTicks;
        return true;
    }

    public void Initialize(FireGrid grid, IEnumerable<TimberbornCellSource> sources)
    {
        TimberbornCellSource[] sourceValues = (sources ?? throw new ArgumentNullException(nameof(sources))).ToArray();
        Initialize(grid, sourceValues, _cellMapper.CreateMaterialFields(grid, sourceValues));
    }

    public void Initialize(
        FireGrid grid,
        IEnumerable<TimberbornCellSource> sources,
        ReadOnlySpan<WildfireMaterialField> materialFields)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        if (_simulatorFactory is null)
        {
            throw new InvalidOperationException("Timberborn fire system was constructed with an existing simulator and cannot initialize another one.");
        }

        TimberbornCellSource[] sourceValues = sources.ToArray();
        ushort[] initialCells = _cellMapper.CreateInitialCells(grid, sourceValues);
        WildfireMaterialField[] materialFieldValues = materialFields.ToArray();
        DisposeSimulator();
        _fireSimulator = _simulatorFactory.Create(grid, initialCells, materialFieldValues);
        _grid = grid;
        _importedTargets = CreateImportedTargets(grid, initialCells, materialFieldValues);
        _contaminationFireSummary = TimberbornContaminationFireConsequenceTelemetry.Summarize(
            grid,
            sourceValues,
            _importedTargets);
        _registeredChangeCountSinceLastDispatch = 0;
        Qa.Reset();
        LastTick = 0;
        LastDeltaCount = 0;
        _deltaConsumer.Reset();
        LastPersistentRestoreNoLiveFuelCellsCleared = 0;
        _logSink.Info(
            $"wildfire_timberborn_initialized width={grid.Width} height={grid.Height} depth={grid.Depth} cell_count={grid.CellCount}");
        if (!Equals(_contaminationFireSummary, TimberbornContaminationFireConsequenceSummary.Empty))
        {
            _logSink.Info(_contaminationFireSummary.ToLogToken());
        }
    }

    public void InitializeFromPersistentFireSimState(
        FireGrid grid,
        IEnumerable<TimberbornCellSource> sources,
        ReadOnlySpan<WildfireMaterialField> materialFields,
        TimberbornFireSimPersistenceSnapshot snapshot)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        TimberbornCellSource[] sourceValues = sources.ToArray();
        if (snapshot.Width != grid.Width ||
            snapshot.Height != grid.Height ||
            snapshot.Depth != grid.Depth ||
            snapshot.Cells.Count != grid.CellCount)
        {
            Initialize(grid, sourceValues, materialFields);
            _logSink.Warning(
                "wildfire_timberborn_firesim_persistence_skipped " +
                "reason=dimension_mismatch " +
                $"saved={snapshot.Width}x{snapshot.Height}x{snapshot.Depth} " +
                $"current={grid.Width}x{grid.Height}x{grid.Depth}");
            return;
        }

        Initialize(grid, sourceValues, materialFields);
        if (_fireSimulator is ITimberbornFireSimPersistenceState persistenceState)
        {
            TimberbornFireSimPersistenceSnapshot sanitizedSnapshot = SanitizePersistentFireSimState(
                snapshot,
                _cellMapper.CreateInitialCells(grid, sourceValues),
                materialFields);
            persistenceState.RestoreFireSimState(sanitizedSnapshot);
            LastTick = sanitizedSnapshot.Tick;
            LastDeltaCount = 0;
            int clearedCellCount = snapshot.Cells
                .Zip(sanitizedSnapshot.Cells, static (saved, restored) => saved == restored)
                .Count(static unchanged => !unchanged);
            LastPersistentRestoreNoLiveFuelCellsCleared = clearedCellCount;
            _logSink.Info(
                "wildfire_timberborn_firesim_persistence_restored " +
                $"tick={sanitizedSnapshot.Tick} " +
                $"cell_count={sanitizedSnapshot.Cells.Count} " +
                $"no_live_fuel_cells_cleared={clearedCellCount}");
        }
        else
        {
            _logSink.Warning(
                "wildfire_timberborn_firesim_persistence_skipped " +
                "reason=snapshot_missing");
        }
    }

    private static TimberbornFireSimPersistenceSnapshot SanitizePersistentFireSimState(
        TimberbornFireSimPersistenceSnapshot snapshot,
        IReadOnlyList<ushort> currentImportCells,
        ReadOnlySpan<WildfireMaterialField> currentMaterialFields)
    {
        WildfireMaterialField[] currentMaterialFieldValues = currentMaterialFields.ToArray();
        ushort[] sanitizedCells = snapshot.Cells
            .Select((savedCell, index) => ShouldRestoreSavedCell(
                savedCell,
                currentImportCells[index],
                currentMaterialFieldValues[index])
                ? savedCell
                : currentImportCells[index])
            .ToArray();

        return snapshot with { Cells = sanitizedCells };
    }

    private static bool ShouldRestoreSavedCell(
        ushort savedCell,
        ushort currentImportCell,
        WildfireMaterialField currentMaterialField)
    {
        if (!IsLiveFuelMaterial(currentMaterialField.State.MaterialClass) ||
            PackedCell.Fuel(currentImportCell) == 0 ||
            PackedCell.Flammability(currentImportCell) == 0 ||
            PackedCell.Terrain(currentImportCell) == 0)
        {
            return !HasActiveFireState(savedCell);
        }

        return true;
    }

    private static bool HasActiveFireState(ushort cell)
    {
        return PackedCell.Fuel(cell) > 0 ||
            PackedCell.Heat(cell) > 0 ||
            PackedCell.Flammability(cell) > 0 ||
            PackedCell.BurningLevel(cell) > 0;
    }

    private static bool IsLiveFuelMaterial(WildfireMaterialClass materialClass)
    {
        return materialClass is WildfireMaterialClass.Tree or
            WildfireMaterialClass.Vegetation or
            WildfireMaterialClass.Crop or
            WildfireMaterialClass.Building or
            WildfireMaterialClass.Storage;
    }

    public IReadOnlyList<uint>? ReadTransportFields()
    {
        if (_fireSimulator is null)
        {
            return null;
        }

        if (_fireSimulator is not ITimberbornTransportFieldReader transportReader)
        {
            throw new InvalidOperationException("The initialized fire simulator does not support transport field observations.");
        }

        return transportReader.ReadTransportFields();
    }

    public TimberbornFireSimPersistenceSnapshot? CapturePersistentFireSimState()
    {
        return _fireSimulator is ITimberbornFireSimPersistenceState persistenceState
            ? persistenceState.CaptureFireSimState()
            : null;
    }

    public GpuFireStepResult Tick()
    {
        ITimberbornFireDispatchHost? host = HostDispatch;
        host?.ThrowIfSaveUnsafe(); // Before QA/input preparation, not only the eventual GPU call.
        IGpuFireSimulator fireSimulator = RequireSimulator();
        Qa.PrepareTick();
        string? sustainedInputSource = SustainedIgnition.BeforeTick();
        Qa.CompleteTickPreparation(sustainedInputSource);
        int pendingChangeCount = _registeredChangeCountSinceLastDispatch;

        _logSink.Info($"wildfire_timberborn_dispatch_started pending_changes={pendingChangeCount}");
        bool stepReturned = false;
        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            GpuFireStepResult result = host is null ? fireSimulator.Tick() : host.Tick();
            stepReturned = true;
            stopwatch.Stop();
            _registeredChangeCountSinceLastDispatch = 0;
            LastTick = result.Tick;
            LastDeltaCount = result.Deltas.Count;
            _deltaConsumer.Consume(result.Tick, result.Deltas.ToArray());
            Qa.AfterTick(result);
            _logSink.Info(
                $"wildfire_timberborn_dispatch_completed tick={result.Tick} delta_count={result.Deltas.Count} elapsed_ms={stopwatch.Elapsed.TotalMilliseconds:F3}");

            return result;
        }
        catch (Exception exception)
        {
            // Even a later observational failure prevents Runtime's remaining native followups.
            if (stepReturned || exception is FireSimStepInputException
                { Outcome: FireSimStepInputOutcome.Committed or FireSimStepInputOutcome.Indeterminate })
                host?.InvalidateIncompleteDispatch();
            try
            {
                _logSink.Warning($"wildfire_timberborn_dispatch_failed message=\"{exception.Message}\"");
            }
            catch
            {
                // Diagnostics cannot replace the original step/delivery failure.
            }
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

    public void RegisterChange(FireSimChange change, string source, bool shouldLog = true)
    {
        RequireSimulator().RegisterChange(change);
        _registeredChangeCountSinceLastDispatch++;
        if (shouldLog)
        {
            LogRegisteredChanges(source, 1);
        }
    }

    public int RegisterSustainedIgnitionChanges(IEnumerable<FireSimChange> changes, string source)
    {
        return SustainedIgnition.Queue(changes, source);
    }

    public void LogRegisteredChanges(string source, int count)
    {
        _logSink.Info(
            $"wildfire_timberborn_changes_registered source={source} count={count} pending_changes={_registeredChangeCountSinceLastDispatch}");
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

    public FireGrid RequireInitializedGrid()
    {
        return RequireGrid();
    }

    public IDisposable Subscribe(IFireSimListener listener)
    {
        return RequireSimulator().Subscribe(listener);
    }

    private static TimberbornImportedFieldTarget[] CreateImportedTargets(
        FireGrid grid,
        IReadOnlyList<ushort> initialCells,
        IReadOnlyList<WildfireMaterialField> materialFields)
    {
        return Enumerable.Range(0, grid.CellCount)
            .Select(index =>
            {
                (int x, int y, int z) = grid.FromIndex(index);
                return new TimberbornImportedFieldTarget(
                    index,
                    x,
                    y,
                    z,
                    materialFields[index].State.MaterialClass,
                    materialFields[index].TargetId,
                    initialCells[index],
                    materialFields[index].State.SoilContamination);
            })
            .Where(static target => target.MaterialClass != WildfireMaterialClass.Empty)
            .ToArray();
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
        SustainedIgnition.Reset();
        Qa.Reset();
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
    IGpuFireSimulator Create(
        FireGrid grid,
        ReadOnlySpan<ushort> initialCells,
        ReadOnlySpan<WildfireMaterialField> materialFields);
}

public interface ITimberbornQaSelectedTreeTargetProvider
{
    TimberbornImportedFieldTarget FindSelectedTreeTarget(
        FireGrid grid,
        IReadOnlyList<TimberbornImportedFieldTarget> importedTargets);
}

public readonly record struct TimberbornImportedFieldTarget(
    int CellIndex,
    int X,
    int Y,
    int Z,
    WildfireMaterialClass MaterialClass,
    uint CompanionTargetId,
    ushort InitialCell,
    byte SoilContamination = 0);

public readonly record struct TimberbornQaBurnDamageProbeTarget(
    TimberbornBurnDamageTargetState State,
    TimberbornImportedFieldTarget FieldTarget,
    byte ProbeFuel,
    byte SpendFuel,
    byte ProbeFlammability);

public readonly record struct TimberbornQaDirectConsequenceTarget(
    string Kind,
    string StableId,
    int CellIndex,
    int X,
    int Y,
    int Z,
    ushort InitialCell,
    int ScannedCellCount);

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
