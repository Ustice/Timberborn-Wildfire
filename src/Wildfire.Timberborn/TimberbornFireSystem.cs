using System.Diagnostics;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed record TimberbornQaDeltaStimulusSustainedHeatState(
    int CellIndex,
    int X,
    int Y,
    int Z,
    ushort SetCell,
    string TargetSource,
    int RequestedCycleCount,
    int CompletedCycleCount = 0,
    int? QueuedCycleNumber = null,
    uint? LastCompletedTick = null)
{
    public int RemainingCycleCount => Math.Max(0, RequestedCycleCount - CompletedCycleCount);

    public bool IsActive => CompletedCycleCount < RequestedCycleCount;
}

public sealed class TimberbornFireSystem : IDisposable
{
    internal const byte QaIgnitionHeat = 15;
    private const byte QaIgnitionFuel = 15;
    private const byte QaIgnitionFlammability = 3;
    private const byte QaIgnitionTerrain = 1;
    private const byte QaIgnitionWater = 0;
    private const int QaIgnitionPegDispatchTicks = 12;
    private const int QaDeltaStimulusSustainedHeatCycleCount = 12;
    private static readonly ushort QaDeltaStimulusCell = PackedCell.Pack(
        fuel: QaIgnitionFuel,
        heat: QaIgnitionHeat,
        flammability: QaIgnitionFlammability,
        water: QaIgnitionWater,
        terrain: QaIgnitionTerrain,
        burningLevel: 0);
    private const byte QaSpentFuel = 0;
    private const byte QaWaterSuppressionWater = 3;

    private readonly TimberbornFireCellMapper _cellMapper;
    private readonly ITimberbornFireSimulatorFactory? _simulatorFactory;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly TimberbornFireDeltaConsumer _deltaConsumer;
    private IGpuFireSimulator? _fireSimulator;
    private FireGrid? _grid;
    private TimberbornImportedFieldTarget[] _importedTargets = Array.Empty<TimberbornImportedFieldTarget>();
    private int _registeredChangeCountSinceLastDispatch;
    private FireSimChange[] _qaIgnitionPegChanges = Array.Empty<FireSimChange>();
    private int _qaIgnitionPegDispatchTicksRemaining;
    private string _qaIgnitionPegSource = "placeholder";
    private FireSimChange[] _qaBurnDamageSpendChanges = Array.Empty<FireSimChange>();
    private int _burnDurationSustainedHeatTicksPendingDispatch;
    private TimberbornQaDeltaStimulusSustainedHeatState? _qaDeltaStimulusSustainedHeatState;
    private FireSimParameters _fireSimParameters = FireSimParameters.Default;
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

    public TimberbornQaDeltaStimulusSustainedHeatState? QaDeltaStimulusSustainedHeatState =>
        _qaDeltaStimulusSustainedHeatState;

    public bool TryUpdateParameters(FireSimParameters parameters)
    {
        if (_fireSimulator is not ITimberbornConfigurableFireSimParameters configurable)
        {
            return false;
        }

        configurable.UpdateParameters(parameters);
        _fireSimParameters = parameters;
        return true;
    }

    public void Initialize(FireGrid grid, IEnumerable<TimberbornCellSource> sources)
    {
        TimberbornCellSource[] sourceValues = (sources ?? throw new ArgumentNullException(nameof(sources))).ToArray();
        Initialize(grid, sourceValues, _cellMapper.CreateCompanionFields(grid, sourceValues));
    }

    public void Initialize(
        FireGrid grid,
        IEnumerable<TimberbornCellSource> sources,
        ReadOnlySpan<WildfireCompanionField> companionFields)
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
        WildfireCompanionField[] companionFieldValues = companionFields.ToArray();
        DisposeSimulator();
        _fireSimulator = _simulatorFactory.Create(grid, initialCells, companionFieldValues);
        _grid = grid;
        _importedTargets = CreateImportedTargets(grid, initialCells, companionFieldValues);
        _registeredChangeCountSinceLastDispatch = 0;
        ClearQaIgnitionPeg();
        ClearQaBurnDamageSpendProbe();
        _qaDeltaStimulusSustainedHeatState = null;
        _burnDurationSustainedHeatTicksPendingDispatch = 0;
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
        RegisterPendingQaBurnDamageSpendChanges();
        RegisterPendingQaIgnitionPegChanges();
        QueueNextSustainedQaDeltaStimulusCycle();
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
            CompleteQueuedSustainedQaDeltaStimulusCycle(result.Tick);
            RecordBurnDurationSustainedHeatDispatch();
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

    public void RegisterChange(FireSimChange change, string source, bool shouldLog = true)
    {
        RequireSimulator().RegisterChange(change);
        _registeredChangeCountSinceLastDispatch++;
        if (shouldLog)
        {
            LogRegisteredChanges(source, 1);
        }
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

    public TimberbornQaDeltaStimulusResult QueueQaDeltaStimulus(
        string targetSelector = TimberbornQaFieldTargetSelectors.Default,
        IReadOnlyDictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState>? burnDamageTargets = null,
        ITimberbornExplosiveInfrastructureTargetApi? explosiveInfrastructureTargetApi = null,
        ITimberbornDetonatorFireSafetyTargetApi? detonatorFireSafetyTargetApi = null,
        ITimberbornTunnelFireTargetApi? tunnelFireTargetApi = null,
        TimberbornQaSelectedCropTarget? selectedCropTarget = null,
        TimberbornBeaverFieldExposureQaTarget? beaverExposureTarget = null)
    {
        FireGrid grid = RequireGrid();
        string normalizedSelector = TimberbornQaFieldTargetSelectors.Normalize(targetSelector);
        if (normalizedSelector == TimberbornQaFieldTargetSelectors.BeaverExposure)
        {
            return RegisterSustainedBeaverExposureDeltaStimulus(grid, beaverExposureTarget);
        }

        if (normalizedSelector is TimberbornQaFieldTargetSelectors.Crop or TimberbornQaFieldTargetSelectors.Bush &&
            burnDamageTargets is not null)
        {
            TimberbornQaBurnDamageProbeTarget target = FindCropBurnDamageProbeTarget(
                grid,
                normalizedSelector,
                burnDamageTargets,
                selectedCropTarget);
            int queuedChangeCount = RegisterBurnDamageProbe(target, "qa_crop_burn_damage_stimulus");
            TimberbornCropBurnTargetRegistrationSummary cropSummary =
                TimberbornCropBurnTargetClassifier.SummarizeRegisteredTargets(burnDamageTargets.Values);

            return new TimberbornQaDeltaStimulusResult(
                normalizedSelector,
                target.FieldTarget.CellIndex,
                target.FieldTarget.X,
                target.FieldTarget.Y,
                target.FieldTarget.Z,
                target.FieldTarget.MaterialClass,
                target.FieldTarget.CompanionTargetId,
                target.FieldTarget.InitialCell,
                QaIgnitionHeat,
                QueuedHeatChangeCount: queuedChangeCount,
                BurnDamageTargetKey: target.State.TargetKey.StableId,
                BurnDamageSpecId: target.State.SpecId,
                BurnDamageTargetKind: target.State.TargetKind,
                BurnDamageRemainingCapacity: target.State.RemainingCapacity,
                BurnDamageProbeFuel: target.ProbeFuel,
                BurnDamageSpendFuel: target.SpendFuel,
                TargetSource: selectedCropTarget?.TargetSource ?? "registered_crop_target",
                RegisteredBurnDamageTargetCount: burnDamageTargets.Count,
                RegisteredCropBurnTargetCount: cropSummary.TargetCount,
                RegisteredCropBurnOwnedCellCount: cropSummary.OwnedCellCount);
        }

        if (normalizedSelector is TimberbornQaFieldTargetSelectors.Default or TimberbornQaFieldTargetSelectors.Crop)
        {
            TimberbornQaDeltaStimulusTargetSelection? cropTarget =
                FindCropDeltaStimulusTarget(grid, burnDamageTargets, selectedCropTarget);
            if (cropTarget is not null)
            {
                return RegisterSustainedCropDeltaStimulus(normalizedSelector, cropTarget);
            }
        }

        if (TimberbornQaFieldTargetSelectors.IsDirectConsequenceTargetSelector(normalizedSelector))
        {
            TimberbornQaDirectConsequenceTarget target = FindDirectConsequenceTarget(
                grid,
                normalizedSelector,
                explosiveInfrastructureTargetApi,
                detonatorFireSafetyTargetApi,
                tunnelFireTargetApi);
            int queuedChangeCount = RegisterDirectConsequenceTargetProbe(target, "qa_direct_consequence_target_stimulus");

            return new TimberbornQaDeltaStimulusResult(
                normalizedSelector,
                target.CellIndex,
                target.X,
                target.Y,
                target.Z,
                WildfireMaterialClass.Infrastructure,
                CompanionTargetId: 0,
                target.InitialCell,
                QaIgnitionHeat,
                QueuedHeatChangeCount: queuedChangeCount,
                DirectTargetKind: target.Kind,
                DirectTargetStableId: target.StableId,
                DirectTargetScannedCellCount: target.ScannedCellCount);
        }

        if (TimberbornQaFieldTargetSelectors.IsBurnDamageProbeSelector(normalizedSelector))
        {
            TimberbornQaBurnDamageProbeTarget target = FindBurnDamageProbeTarget(
                grid,
                normalizedSelector,
                burnDamageTargets);
            int queuedChangeCount = RegisterBurnDamageProbe(target, "qa_burn_damage_stimulus");

            return new TimberbornQaDeltaStimulusResult(
                normalizedSelector,
                target.FieldTarget.CellIndex,
                target.FieldTarget.X,
                target.FieldTarget.Y,
                target.FieldTarget.Z,
                target.FieldTarget.MaterialClass,
                target.FieldTarget.CompanionTargetId,
                target.FieldTarget.InitialCell,
                QaIgnitionHeat,
                QueuedHeatChangeCount: queuedChangeCount,
                BurnDamageTargetKey: target.State.TargetKey.StableId,
                BurnDamageSpecId: target.State.SpecId,
                BurnDamageTargetKind: target.State.TargetKind,
                BurnDamageRemainingCapacity: target.State.RemainingCapacity,
                BurnDamageProbeFuel: target.ProbeFuel,
                BurnDamageSpendFuel: target.SpendFuel);
        }

        TimberbornImportedFieldTarget ignitionTarget = normalizedSelector == TimberbornQaFieldTargetSelectors.CenterTree
            ? FindImportedTarget(
                candidate => IsBurnableImportedTarget(candidate) &&
                    candidate.MaterialClass == WildfireMaterialClass.Tree,
                OrderByCenterDistance(grid),
                $"No imported center tree field target was found for QA delta stimulus selector '{normalizedSelector}'.")
            : FindImportedTarget(
                candidate => IsBurnableImportedTarget(candidate) &&
                    TimberbornQaFieldTargetSelectors.Matches(candidate.MaterialClass, normalizedSelector),
                $"No imported burnable field target was found for QA delta stimulus selector '{normalizedSelector}'.");
        int queuedHeatChangeCount = RegisterIgnitionCluster(grid, ignitionTarget.CellIndex, "qa_delta_stimulus");

        return new TimberbornQaDeltaStimulusResult(
            normalizedSelector,
            ignitionTarget.CellIndex,
            ignitionTarget.X,
            ignitionTarget.Y,
            ignitionTarget.Z,
            ignitionTarget.MaterialClass,
            ignitionTarget.CompanionTargetId,
            ignitionTarget.InitialCell,
            QaIgnitionHeat,
            QueuedHeatChangeCount: queuedHeatChangeCount);
    }

    public TimberbornQaDeltaStimulusResult QueueQaSelectedTreeDeltaStimulus(
        ITimberbornQaSelectedTreeTargetProvider targetProvider)
    {
        if (targetProvider is null)
        {
            throw new ArgumentNullException(nameof(targetProvider));
        }

        TimberbornImportedFieldTarget target =
            targetProvider.FindSelectedTreeTarget(RequireGrid(), _importedTargets);
        if (!IsBurnableImportedTarget(target) || target.MaterialClass != WildfireMaterialClass.Tree)
        {
            throw new InvalidOperationException(
                "The selected Timberborn entity did not resolve to a burnable imported tree field target.");
        }

        int queuedHeatChangeCount = RegisterIgnitionCluster(RequireGrid(), target.CellIndex, "qa_selected_tree_delta_stimulus");

        return new TimberbornQaDeltaStimulusResult(
            TimberbornQaFieldTargetSelectors.SelectedTree,
            target.CellIndex,
            target.X,
            target.Y,
            target.Z,
            target.MaterialClass,
            target.CompanionTargetId,
            target.InitialCell,
            QaIgnitionHeat,
            QueuedHeatChangeCount: queuedHeatChangeCount);
    }

    public TimberbornQaBuildingBurnoutStimulusResult QueueBuildingBurnoutQaStimulus(
        ITimberbornQaBuildingBurnoutStimulusTargetProvider targetProvider)
    {
        if (targetProvider is null)
        {
            throw new ArgumentNullException(nameof(targetProvider));
        }

        TimberbornQaBuildingBurnoutStimulusTarget target = targetProvider.FindTarget(RequireGrid());
        RegisterChange(new FireSimChange(CellIndex: target.CellIndex, SetHeat: QaIgnitionHeat), "qa_building_burnout_heat");
        RegisterChange(new FireSimChange(CellIndex: target.CellIndex, SetFuel: QaSpentFuel), "qa_building_burnout_stimulus");

        return new TimberbornQaBuildingBurnoutStimulusResult(
            target.CellIndex,
            target.X,
            target.Y,
            target.Z,
            target.ScannedCellCount,
            QaIgnitionHeat,
            QaSpentFuel,
            QueuedFieldChangeCount: 2);
    }

    public TimberbornQaWaterSuppressionStimulusResult QueueWaterSuppressionQaStimulus(
        string targetSelector = TimberbornQaFieldTargetSelectors.Default)
    {
        _ = RequireGrid();
        string normalizedSelector = TimberbornQaFieldTargetSelectors.Normalize(targetSelector);
        TimberbornImportedFieldTarget target = FindImportedTarget(
            candidate => IsBurnableImportedTarget(candidate) &&
                TimberbornQaFieldTargetSelectors.Matches(candidate.MaterialClass, normalizedSelector) &&
                PackedCell.Water(candidate.InitialCell) < QaWaterSuppressionWater,
            $"No imported burnable field target without maximum water was found for QA water suppression selector '{normalizedSelector}'.");
        RegisterChange(
            new FireSimChange(CellIndex: target.CellIndex, SetWater: QaWaterSuppressionWater),
            "qa_water_suppression");

        return new TimberbornQaWaterSuppressionStimulusResult(
            normalizedSelector,
            target.CellIndex,
            target.X,
            target.Y,
            target.Z,
            target.MaterialClass,
            target.CompanionTargetId,
            target.InitialCell,
            QaWaterSuppressionWater,
            QueuedWaterChangeCount: 1);
    }

    public TimberbornQaBurnDurationStimulusResult QueueBurnDurationQaStimulus(string target)
    {
        FireGrid grid = RequireGrid();
        TimberbornQaBurnDurationStimulusTarget selectedTarget =
            TimberbornQaBurnDurationStimulusTargets.SelectTarget(grid, _importedTargets, target);
        FireSimChange heatPegChange = new(CellIndex: selectedTarget.CellIndex, SetHeat: QaIgnitionHeat);
        RegisterChange(heatPegChange, "qa_burn_duration_stimulus");
        _burnDurationSustainedHeatTicksPendingDispatch = 1;
        StartQaIgnitionPeg(new[] { heatPegChange }, "qa_burn_duration_stimulus");

        int ignitionPegDispatchTicks = GetQaIgnitionPegDispatchTicks();
        _burnDurationProofState = new TimberbornQaBurnDurationProofState(
            selectedTarget.Target,
            selectedTarget.CellIndex,
            selectedTarget.X,
            selectedTarget.Y,
            selectedTarget.Z,
            selectedTarget.InitialFuel,
            LastTick ?? 0,
            TimberbornQaBurnDurationStimulusTargets.DefaultTimeoutTicks,
            SustainedHeatTicks: ignitionPegDispatchTicks);
        _logSink.Info(ToBurnDurationProofLogToken());

        return new TimberbornQaBurnDurationStimulusResult(
            selectedTarget.Target,
            selectedTarget.CellIndex,
            selectedTarget.X,
            selectedTarget.Y,
            selectedTarget.Z,
            selectedTarget.MaterialClass,
            selectedTarget.CompanionTargetId,
            selectedTarget.InitialCell,
            selectedTarget.InitialFuel,
            QaIgnitionHeat,
            TimberbornQaBurnDurationStimulusTargets.DefaultTimeoutTicks,
            ignitionPegDispatchTicks,
            QueuedHeatChangeCount: ignitionPegDispatchTicks);
    }

    public IDisposable Subscribe(IFireSimListener listener)
    {
        return RequireSimulator().Subscribe(listener);
    }

    private TimberbornQaDeltaStimulusResult RegisterSustainedCropDeltaStimulus(
        string normalizedSelector,
        TimberbornQaDeltaStimulusTargetSelection target)
    {
        int cellIndex = RequireGrid().ToIndex(target.Coordinates.X, target.Coordinates.Y, target.Coordinates.Z);
        _qaDeltaStimulusSustainedHeatState = new TimberbornQaDeltaStimulusSustainedHeatState(
            cellIndex,
            target.Coordinates.X,
            target.Coordinates.Y,
            target.Coordinates.Z,
            QaDeltaStimulusCell,
            target.TargetSource,
            QaDeltaStimulusSustainedHeatCycleCount);
        QueueNextSustainedQaDeltaStimulusCycle();

        return new TimberbornQaDeltaStimulusResult(
            normalizedSelector,
            cellIndex,
            target.Coordinates.X,
            target.Coordinates.Y,
            target.Coordinates.Z,
            WildfireMaterialClass.Crop,
            CompanionTargetId: 0,
            QaDeltaStimulusCell,
            QaIgnitionHeat,
            QueuedHeatChangeCount: _qaDeltaStimulusSustainedHeatState.QueuedCycleNumber.HasValue ? 1 : 0,
            TargetSource: target.TargetSource,
            RegisteredBurnDamageTargetCount: target.RegisteredBurnDamageTargetCount,
            RegisteredCropBurnTargetCount: target.RegisteredCropBurnTargetCount,
            RegisteredCropBurnOwnedCellCount: target.RegisteredCropBurnOwnedCellCount,
            SustainedHeatSetCell: QaDeltaStimulusCell,
            SustainedHeatRequestedCycleCount: QaDeltaStimulusSustainedHeatCycleCount,
            SustainedHeatCompletedCycleCount: _qaDeltaStimulusSustainedHeatState.CompletedCycleCount,
            SustainedHeatRemainingCycleCount: _qaDeltaStimulusSustainedHeatState.RemainingCycleCount,
            SustainedHeatQueuedCycleNumber: _qaDeltaStimulusSustainedHeatState.QueuedCycleNumber);
    }

    private TimberbornQaDeltaStimulusResult RegisterSustainedBeaverExposureDeltaStimulus(
        FireGrid grid,
        TimberbornBeaverFieldExposureQaTarget? target)
    {
        if (target is not { IsAvailable: true, CellIndex: int cellIndex, X: int x, Y: int y, Z: int z })
        {
            string reason = target?.UnavailableReason ?? "beaver_position_unavailable";
            throw new InvalidOperationException(
                $"QA beaver-exposure stimulus requires available beaver position sampling: {reason}.");
        }

        TimberbornImportedFieldTarget? importedTarget = _importedTargets
            .Where(imported => imported.CellIndex == cellIndex)
            .Select(imported => (TimberbornImportedFieldTarget?)imported)
            .FirstOrDefault();
        _qaDeltaStimulusSustainedHeatState = new TimberbornQaDeltaStimulusSustainedHeatState(
            cellIndex,
            x,
            y,
            z,
            QaDeltaStimulusCell,
            "beaver_candidate_cell",
            QaDeltaStimulusSustainedHeatCycleCount);
        QueueNextSustainedQaDeltaStimulusCycle();

        return new TimberbornQaDeltaStimulusResult(
            TimberbornQaFieldTargetSelectors.BeaverExposure,
            cellIndex,
            x,
            y,
            z,
            importedTarget?.MaterialClass ?? WildfireMaterialClass.Unknown,
            importedTarget?.CompanionTargetId ?? 0,
            importedTarget?.InitialCell ?? 0,
            QaIgnitionHeat,
            QueuedHeatChangeCount: _qaDeltaStimulusSustainedHeatState.QueuedCycleNumber.HasValue ? 1 : 0,
            TargetSource: "beaver_candidate_cell",
            SustainedHeatSetCell: QaDeltaStimulusCell,
            SustainedHeatRequestedCycleCount: QaDeltaStimulusSustainedHeatCycleCount,
            SustainedHeatCompletedCycleCount: _qaDeltaStimulusSustainedHeatState.CompletedCycleCount,
            SustainedHeatRemainingCycleCount: _qaDeltaStimulusSustainedHeatState.RemainingCycleCount,
            SustainedHeatQueuedCycleNumber: _qaDeltaStimulusSustainedHeatState.QueuedCycleNumber,
            BeaverExposureTargetBeaverId: target.BeaverId,
            BeaverExposureTargetBeaverX: target.BeaverX,
            BeaverExposureTargetBeaverY: target.BeaverY,
            BeaverExposureTargetBeaverZ: target.BeaverZ,
            BeaverExposureTargetCandidateCells: target.CandidateCellCount,
            BeaverExposureTargetSampledBeavers: target.SampledBeaverCount,
            BeaverExposureTargetSkippedNoPositionApi: target.SkippedNoPositionApiCount,
            BeaverExposureTargetSkippedBoundedSampling: target.SkippedBoundedSamplingCount);
    }

    private static TimberbornQaDeltaStimulusTargetSelection? FindCropDeltaStimulusTarget(
        FireGrid grid,
        IReadOnlyDictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState>? burnDamageTargets,
        TimberbornQaSelectedCropTarget? selectedCropTarget)
    {
        TimberbornBurnDamageTargetState[] states = burnDamageTargets?.Values.ToArray() ??
            Array.Empty<TimberbornBurnDamageTargetState>();
        TimberbornBurnDamageTargetState[] cropTargets = states
            .Where(TimberbornCropBurnTargetClassifier.IsCropOrHarvestable)
            .ToArray();
        int cropOwnedCellCount = cropTargets
            .SelectMany(static state => state.OwnedCellIndices)
            .Where(cellIndex => cellIndex >= 0 && cellIndex < grid.CellCount)
            .Distinct()
            .Count();

        if (selectedCropTarget is { } selectedTarget)
        {
            return new TimberbornQaDeltaStimulusTargetSelection(
                new TimberbornCellCoordinates(selectedTarget.X, selectedTarget.Y, selectedTarget.Z),
                selectedTarget.TargetSource,
                states.Length,
                cropTargets.Length,
                cropOwnedCellCount);
        }

        TimberbornCellCoordinates center = new(grid.Width / 2, grid.Height / 2, grid.Depth / 2);
        TimberbornCellCoordinates? cropTarget = cropTargets
            .SelectMany(static state => state.OwnedCellIndices)
            .Where(cellIndex => cellIndex >= 0 && cellIndex < grid.CellCount)
            .Distinct()
            .Select(cellIndex =>
            {
                (int x, int y, int z) = grid.FromIndex(cellIndex);
                return new TimberbornCellCoordinates(x, y, z);
            })
            .OrderBy(coordinates => Distance(coordinates, center))
            .ThenBy(static coordinates => coordinates.Z)
            .ThenBy(static coordinates => coordinates.Y)
            .ThenBy(static coordinates => coordinates.X)
            .Select(static coordinates => (TimberbornCellCoordinates?)coordinates)
            .FirstOrDefault();

        return cropTarget.HasValue
            ? new TimberbornQaDeltaStimulusTargetSelection(
                cropTarget.Value,
                "registered_crop_target",
                states.Length,
                cropTargets.Length,
                cropOwnedCellCount)
            : null;
    }

    private static int Distance(TimberbornCellCoordinates coordinates, TimberbornCellCoordinates target)
    {
        return Math.Abs(coordinates.X - target.X) +
            Math.Abs(coordinates.Y - target.Y) +
            Math.Abs(coordinates.Z - target.Z);
    }

    private TimberbornImportedFieldTarget FindImportedTarget(
        Func<TimberbornImportedFieldTarget, bool> predicate,
        string notFoundMessage)
    {
        return FindImportedTarget(
            predicate,
            static targets => targets.OrderBy(static target => target.CellIndex),
            notFoundMessage);
    }

    private TimberbornImportedFieldTarget FindImportedTarget(
        Func<TimberbornImportedFieldTarget, bool> predicate,
        Func<IEnumerable<TimberbornImportedFieldTarget>, IOrderedEnumerable<TimberbornImportedFieldTarget>> order,
        string notFoundMessage)
    {
        IEnumerable<TimberbornImportedFieldTarget> candidates = _importedTargets.Where(predicate);
        return order(candidates)
            .Select(static target => (TimberbornImportedFieldTarget?)target)
            .FirstOrDefault() ?? throw new InvalidOperationException(notFoundMessage);
    }

    private TimberbornQaDirectConsequenceTarget FindDirectConsequenceTarget(
        FireGrid grid,
        string selector,
        ITimberbornExplosiveInfrastructureTargetApi? explosiveInfrastructureTargetApi,
        ITimberbornDetonatorFireSafetyTargetApi? detonatorFireSafetyTargetApi,
        ITimberbornTunnelFireTargetApi? tunnelFireTargetApi)
    {
        if (selector == TimberbornQaFieldTargetSelectors.Dynamite && explosiveInfrastructureTargetApi is null)
        {
            throw new InvalidOperationException(
                "QA dynamite stimulus requires the explosive infrastructure target API.");
        }

        if (selector == TimberbornQaFieldTargetSelectors.Detonator && detonatorFireSafetyTargetApi is null)
        {
            throw new InvalidOperationException(
                "QA detonator stimulus requires the detonator fire-safety target API.");
        }

        if (selector == TimberbornQaFieldTargetSelectors.Tunnel && tunnelFireTargetApi is null)
        {
            throw new InvalidOperationException(
                "QA tunnel stimulus requires the tunnel fire target API.");
        }

        Func<int, TimberbornQaDirectConsequenceTarget?> resolveTarget = selector switch
        {
            TimberbornQaFieldTargetSelectors.Dynamite => cellIndex =>
                ResolveDynamiteQaTarget(grid, cellIndex, explosiveInfrastructureTargetApi),
            TimberbornQaFieldTargetSelectors.Detonator => cellIndex =>
                ResolveDetonatorQaTarget(grid, cellIndex, detonatorFireSafetyTargetApi),
            TimberbornQaFieldTargetSelectors.Tunnel => cellIndex =>
                ResolveTunnelQaTarget(grid, cellIndex, tunnelFireTargetApi),
            _ => throw new ArgumentException($"Unsupported direct consequence QA selector '{selector}'.", nameof(selector)),
        };

        int[] candidateCellIndices = _importedTargets
            .Where(static target => target.MaterialClass == WildfireMaterialClass.Infrastructure)
            .Select(static target => target.CellIndex)
            .Concat(Enumerable.Range(0, grid.CellCount))
            .Distinct()
            .OrderBy(static cellIndex => cellIndex)
            .ToArray();
        TimberbornQaDirectConsequenceTarget? target = candidateCellIndices
            .Select<int, TimberbornQaDirectConsequenceTarget?>((cellIndex, offset) =>
            {
                TimberbornQaDirectConsequenceTarget? resolvedTarget;
                try
                {
                    resolvedTarget = resolveTarget(cellIndex);
                }
                catch (Exception exception)
                {
                    _logSink.Warning(
                        "wildfire_timberborn_qa_direct_consequence_target_safe_unavailable " +
                        $"selector={TimberbornQaCommandBridge.FormatToken(selector)} " +
                        $"cell_index={cellIndex} " +
                        $"exception_type={exception.GetType().Name}");
                    resolvedTarget = null;
                }

                return resolvedTarget is null
                    ? (TimberbornQaDirectConsequenceTarget?)null
                    : resolvedTarget.Value with { ScannedCellCount = offset + 1 };
            })
            .Where(static resolvedTarget => resolvedTarget is not null)
            .FirstOrDefault();

        return target ?? throw new InvalidOperationException(
            $"No placed Timberborn target was found for QA selector '{selector}'.");
    }

    private static TimberbornQaDirectConsequenceTarget? ResolveDynamiteQaTarget(
        FireGrid grid,
        int cellIndex,
        ITimberbornExplosiveInfrastructureTargetApi? targetApi)
    {
        if (targetApi is null)
        {
            throw new InvalidOperationException(
                "QA dynamite stimulus requires the explosive infrastructure target API.");
        }

        TimberbornExplosiveInfrastructureTarget? target = targetApi.ResolveTarget(
            new TimberbornExplosiveInfrastructureConsequence(
                cellIndex,
                Tick: 0,
                Heat: QaIgnitionHeat,
                IsBurning: true));
        return target is null
            ? null
            : CreateDirectTarget(grid, cellIndex, "dynamite", target.StableId);
    }

    private static TimberbornQaDirectConsequenceTarget? ResolveDetonatorQaTarget(
        FireGrid grid,
        int cellIndex,
        ITimberbornDetonatorFireSafetyTargetApi? targetApi)
    {
        if (targetApi is null)
        {
            throw new InvalidOperationException(
                "QA detonator stimulus requires the detonator fire-safety target API.");
        }

        TimberbornDetonatorFireSafetyTarget? target = targetApi.ResolveTarget(
            new TimberbornDetonatorFireSafetyConsequence(
                cellIndex,
                Tick: 0,
                Heat: QaIgnitionHeat,
                IsBurning: true));
        return target is null ||
            IsUnavailablePseudoTarget(target.StableId, TimberbornDetonatorFireSafetyStableIds.UnavailablePrefix)
            ? null
            : CreateDirectTarget(grid, cellIndex, "detonator", target.StableId);
    }

    private static TimberbornQaDirectConsequenceTarget? ResolveTunnelQaTarget(
        FireGrid grid,
        int cellIndex,
        ITimberbornTunnelFireTargetApi? targetApi)
    {
        if (targetApi is null)
        {
            throw new InvalidOperationException(
                "QA tunnel stimulus requires the tunnel fire target API.");
        }

        TimberbornTunnelFireTarget? target = targetApi.ResolveTarget(
            new TimberbornTunnelFireConsequence(
                cellIndex,
                Tick: 0,
                Heat: QaIgnitionHeat,
                IsBurning: true));
        return target is null || IsUnavailablePseudoTarget(target.StableId, "tunnel-unavailable:")
            ? null
            : CreateDirectTarget(grid, cellIndex, "tunnel", target.StableId);
    }

    private static bool IsUnavailablePseudoTarget(string stableId, string prefix)
    {
        return stableId.StartsWith(prefix, StringComparison.Ordinal);
    }

    private static TimberbornQaDirectConsequenceTarget CreateDirectTarget(
        FireGrid grid,
        int cellIndex,
        string kind,
        string stableId)
    {
        (int x, int y, int z) = grid.FromIndex(cellIndex);
        ushort initialCell = PackedCell.Pack(
            fuel: QaIgnitionFuel,
            heat: 0,
            flammability: QaIgnitionFlammability,
            water: QaIgnitionWater,
            terrain: QaIgnitionTerrain,
            burningLevel: 0);

        return new TimberbornQaDirectConsequenceTarget(
            kind,
            stableId,
            cellIndex,
            x,
            y,
            z,
            initialCell,
            ScannedCellCount: 0);
    }

    private int RegisterIgnitionCluster(FireGrid grid, int centerCellIndex, string source)
    {
        int centerX = centerCellIndex % grid.Width;
        int centerY = (centerCellIndex / grid.Width) % grid.Height;
        int centerZ = centerCellIndex / (grid.Width * grid.Height);
        FireSimChange[] changes = _importedTargets
            .Where(IsBurnableImportedTarget)
            .Where(target => Math.Abs(target.X - centerX) <= 2 &&
                Math.Abs(target.Y - centerY) <= 2 &&
                Math.Abs(target.Z - centerZ) <= 1)
            .OrderBy(target => Math.Abs(target.X - centerX) +
                Math.Abs(target.Y - centerY) +
                Math.Abs(target.Z - centerZ))
            .ThenBy(static target => target.CellIndex)
            .Take(25)
            .Select(static target => new FireSimChange(
                CellIndex: target.CellIndex,
                SetHeat: QaIgnitionHeat))
            .ToArray();

        if (changes.Length == 0)
        {
            throw new InvalidOperationException("No imported burnable field targets were found for the QA ignition cluster.");
        }

        changes
            .ToList()
            .ForEach(change => RegisterChange(change, source, shouldLog: false));
        StartQaIgnitionPeg(changes, source);
        LogRegisteredChanges(source, changes.Length);
        return changes.Length;
    }

    private void StartQaIgnitionPeg(FireSimChange[] changes, string source)
    {
        _qaIgnitionPegChanges = changes.ToArray();
        _qaIgnitionPegSource = source;
        int ignitionPegDispatchTicks = GetQaIgnitionPegDispatchTicks();
        _qaIgnitionPegDispatchTicksRemaining = Math.Max(0, ignitionPegDispatchTicks - 1);
        if (_qaIgnitionPegDispatchTicksRemaining > 0)
        {
            _logSink.Info(
                "wildfire_timberborn_qa_ignition_heat_peg_started " +
                $"source={source} " +
                $"cell_count={_qaIgnitionPegChanges.Length} " +
                $"requested_dispatch_ticks={ignitionPegDispatchTicks} " +
                $"fire_step_interval_ticks={GetFireCellStepIntervalTicks()} " +
                $"remaining_dispatch_ticks={_qaIgnitionPegDispatchTicksRemaining}");
        }
    }

    private int GetQaIgnitionPegDispatchTicks()
    {
        return checked(QaIgnitionPegDispatchTicks * GetFireCellStepIntervalTicks());
    }

    private int GetFireCellStepIntervalTicks()
    {
        return checked((int)Math.Max(1u, _fireSimParameters.FireCellStepIntervalTicks));
    }

    private void RegisterPendingQaIgnitionPegChanges()
    {
        if (_qaIgnitionPegDispatchTicksRemaining <= 0 || _qaIgnitionPegChanges.Length == 0)
        {
            return;
        }

        if (_registeredChangeCountSinceLastDispatch > 0)
        {
            return;
        }

        _qaIgnitionPegChanges
            .ToList()
            .ForEach(change => RegisterChange(change, "qa_ignition_heat_peg", shouldLog: false));
        if (_qaIgnitionPegSource == "qa_burn_duration_stimulus")
        {
            _burnDurationSustainedHeatTicksPendingDispatch++;
        }

        _qaIgnitionPegDispatchTicksRemaining--;
        LogRegisteredChanges("qa_ignition_heat_peg", _qaIgnitionPegChanges.Length);
        if (_qaIgnitionPegDispatchTicksRemaining == 0)
        {
            ClearQaIgnitionPeg();
        }
    }

    private void QueueNextSustainedQaDeltaStimulusCycle()
    {
        if (_qaDeltaStimulusSustainedHeatState is not { IsActive: true, QueuedCycleNumber: null } state)
        {
            return;
        }

        int cycleNumber = state.CompletedCycleCount + 1;
        RegisterChange(
            new FireSimChange(CellIndex: state.CellIndex, SetHeat: QaIgnitionHeat),
            "qa_delta_stimulus_sustained_heat",
            shouldLog: false);
        _qaDeltaStimulusSustainedHeatState = state with
        {
            QueuedCycleNumber = cycleNumber,
        };
        _logSink.Info(
            "wildfire_timberborn_qa_delta_stimulus_sustained_heat_queued " +
            $"target_index={state.CellIndex} " +
            $"x={state.X} " +
            $"y={state.Y} " +
            $"z={state.Z} " +
            $"set_heat={QaIgnitionHeat} " +
            $"target_source={TimberbornQaCommandBridge.FormatToken(state.TargetSource)} " +
            $"cycle={cycleNumber} " +
            $"requested_cycles={state.RequestedCycleCount} " +
            $"completed_cycles={state.CompletedCycleCount} " +
            $"remaining_cycles={state.RemainingCycleCount}");
    }

    private void CompleteQueuedSustainedQaDeltaStimulusCycle(uint tick)
    {
        if (_qaDeltaStimulusSustainedHeatState is not { QueuedCycleNumber: int cycleNumber } state)
        {
            return;
        }

        int completedCycleCount = Math.Min(cycleNumber, state.RequestedCycleCount);
        _qaDeltaStimulusSustainedHeatState = state with
        {
            CompletedCycleCount = completedCycleCount,
            QueuedCycleNumber = null,
            LastCompletedTick = tick,
        };
        TimberbornQaDeltaStimulusSustainedHeatState completedState = _qaDeltaStimulusSustainedHeatState!;
        _logSink.Info(
            "wildfire_timberborn_qa_delta_stimulus_sustained_heat_completed " +
            $"target_index={completedState.CellIndex} " +
            $"tick={tick} " +
            $"cycle={completedCycleCount} " +
            $"requested_cycles={completedState.RequestedCycleCount} " +
            $"completed_cycles={completedState.CompletedCycleCount} " +
            $"remaining_cycles={completedState.RemainingCycleCount} " +
            $"active={completedState.IsActive.ToString().ToLowerInvariant()}");
    }

    private TimberbornQaBurnDamageProbeTarget FindBurnDamageProbeTarget(
        FireGrid grid,
        string selector,
        IReadOnlyDictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState>? burnDamageTargets)
    {
        if (burnDamageTargets is null || burnDamageTargets.Count == 0)
        {
            throw new InvalidOperationException(
                $"QA burn-damage selector '{selector}' requires registered TWF-075 burn-damage targets.");
        }

        TimberbornBurnDamageTargetState[] candidateStates = burnDamageTargets.Values
            .Where(state => MatchesBurnDamageProbeTargetKind(state.TargetKind, selector))
            .Where(state => MatchesBurnDamageProbeSelector(selector, state))
            .Where(state => state.RemainingCapacity > 0 || AllowsZeroCapacityBurnDamageProbe(selector, state))
            .Where(static state => state.OwnedCellIndices.Count > 0)
            .OrderBy(static state => state.TargetKey.StableId, StringComparer.Ordinal)
            .ThenBy(static state => state.SpecId, StringComparer.Ordinal)
            .ToArray();
        TimberbornQaBurnDamageProbeTarget? target = candidateStates
            .SelectMany(state => state.OwnedCellIndices
                .Where(cellIndex => cellIndex >= 0 && cellIndex < grid.CellCount)
                .OrderBy(static cellIndex => cellIndex)
                .Select(cellIndex => (TimberbornQaBurnDamageProbeTarget?)CreateBurnDamageProbeTarget(grid, state, cellIndex)))
            .FirstOrDefault();

        return target ?? throw new InvalidOperationException(
            $"No TWF-075 burn-damage owned target was found for QA selector '{selector}'.");
    }

    private TimberbornQaBurnDamageProbeTarget FindCropBurnDamageProbeTarget(
        FireGrid grid,
        string selector,
        IReadOnlyDictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState> burnDamageTargets,
        TimberbornQaSelectedCropTarget? selectedCropTarget)
    {
        TimberbornBurnDamageTargetState[] candidateStates = burnDamageTargets.Values
            .Where(TimberbornCropBurnTargetClassifier.IsCropOrHarvestable)
            .Where(state => MatchesCropBurnDamageProbeSelector(selector, state))
            .Where(static state => state.RemainingCapacity > 0)
            .Where(static state => state.OwnedCellIndices.Count > 0)
            .OrderBy(static state => state.TargetKey.StableId, StringComparer.Ordinal)
            .ThenBy(static state => state.SpecId, StringComparer.Ordinal)
            .ToArray();

        if (selectedCropTarget is { } selectedTarget)
        {
            int selectedCellIndex = grid.ToIndex(selectedTarget.X, selectedTarget.Y, selectedTarget.Z);
            TimberbornBurnDamageTargetState? selectedState = candidateStates
                .Where(state => state.OwnedCellIndices.Contains(selectedCellIndex))
                .Select(static state => (TimberbornBurnDamageTargetState?)state)
                .FirstOrDefault();
            if (selectedState is { } foundSelectedState)
            {
                return CreateBurnDamageProbeTarget(grid, foundSelectedState, selectedCellIndex);
            }
        }

        TimberbornCellCoordinates center = new(grid.Width / 2, grid.Height / 2, grid.Depth / 2);
        TimberbornQaBurnDamageProbeTarget? target = candidateStates
            .SelectMany(state => state.OwnedCellIndices
                .Where(cellIndex => cellIndex >= 0 && cellIndex < grid.CellCount)
                .Distinct()
                .Select(cellIndex =>
                {
                    (int x, int y, int z) = grid.FromIndex(cellIndex);
                    return new
                    {
                        State = state,
                        CellIndex = cellIndex,
                        Coordinates = new TimberbornCellCoordinates(x, y, z),
                    };
                }))
            .OrderBy(item => Distance(item.Coordinates, center))
            .ThenBy(static item => item.Coordinates.Z)
            .ThenBy(static item => item.Coordinates.Y)
            .ThenBy(static item => item.Coordinates.X)
            .Select(item => (TimberbornQaBurnDamageProbeTarget?)CreateBurnDamageProbeTarget(
                grid,
                item.State,
                item.CellIndex))
            .FirstOrDefault();

        return target ?? throw new InvalidOperationException(
            "No crop or harvestable burn-damage target was found for QA crop stimulus.");
    }

    private static bool MatchesCropBurnDamageProbeSelector(string selector, TimberbornBurnDamageTargetState state)
    {
        return TimberbornQaFieldTargetSelectors.Normalize(selector) switch
        {
            TimberbornQaFieldTargetSelectors.Bush => state.TargetKind == TimberbornBurnDamageTargetKind.Resource,
            _ => true,
        };
    }

    private TimberbornQaBurnDamageProbeTarget CreateBurnDamageProbeTarget(
        FireGrid grid,
        TimberbornBurnDamageTargetState state,
        int cellIndex)
    {
        TimberbornImportedFieldTarget fieldTarget = _importedTargets
            .Where(target => target.CellIndex == cellIndex)
            .Select(static target => (TimberbornImportedFieldTarget?)target)
            .FirstOrDefault() ??
            CreateSyntheticBurnDamageFieldTarget(grid, state, cellIndex);
        byte probeFuel = (byte)Math.Clamp(Math.Max(1, (int)state.FuelValue), 1, 15);
        byte spendFuel = (byte)Math.Max(0, probeFuel - 1);
        byte probeFlammability = (byte)Math.Clamp(Math.Max(1, (int)state.Flammability), 1, 3);

        return new TimberbornQaBurnDamageProbeTarget(
            state,
            fieldTarget,
            probeFuel,
            spendFuel,
            probeFlammability);
    }

    private static TimberbornImportedFieldTarget CreateSyntheticBurnDamageFieldTarget(
        FireGrid grid,
        TimberbornBurnDamageTargetState state,
        int cellIndex)
    {
        (int x, int y, int z) = grid.FromIndex(cellIndex);
        byte probeFuel = (byte)Math.Clamp(Math.Max(1, (int)state.FuelValue), 1, 15);
        byte probeFlammability = (byte)Math.Clamp(Math.Max(1, (int)state.Flammability), 1, 3);
        ushort initialCell = PackedCell.Pack(
            fuel: probeFuel,
            heat: 0,
            flammability: probeFlammability,
            water: 0,
            terrain: 1,
            burningLevel: 0);

        return new TimberbornImportedFieldTarget(
            cellIndex,
            x,
            y,
            z,
            MaterialClassForBurnDamageKind(state.TargetKind),
            CompanionTargetId: 0,
            initialCell);
    }

    private static bool MatchesBurnDamageProbeTargetKind(
        TimberbornBurnDamageTargetKind targetKind,
        string selector)
    {
        return TimberbornQaFieldTargetSelectors.Normalize(selector) switch
        {
            TimberbornQaFieldTargetSelectors.Building => targetKind == TimberbornBurnDamageTargetKind.Structure,
            TimberbornQaFieldTargetSelectors.Storage => targetKind == TimberbornBurnDamageTargetKind.Storage,
            TimberbornQaFieldTargetSelectors.Infrastructure => targetKind == TimberbornBurnDamageTargetKind.Infrastructure,
            TimberbornQaFieldTargetSelectors.PathInfrastructure => targetKind == TimberbornBurnDamageTargetKind.Infrastructure,
            TimberbornQaFieldTargetSelectors.PowerInfrastructure => targetKind == TimberbornBurnDamageTargetKind.Infrastructure,
            TimberbornQaFieldTargetSelectors.WaterInfrastructure => targetKind == TimberbornBurnDamageTargetKind.Infrastructure,
            _ => false,
        };
    }

    private static bool MatchesBurnDamageProbeSelector(
        string selector,
        TimberbornBurnDamageTargetState state)
    {
        if (state.TargetKind != TimberbornBurnDamageTargetKind.Infrastructure)
        {
            return true;
        }

        string stableId = state.TargetKey.StableId;
        return TimberbornQaFieldTargetSelectors.Normalize(selector) switch
        {
            TimberbornQaFieldTargetSelectors.Infrastructure => true,
            TimberbornQaFieldTargetSelectors.PathInfrastructure =>
                stableId.StartsWith("path_infrastructure:", StringComparison.Ordinal),
            TimberbornQaFieldTargetSelectors.PowerInfrastructure =>
                stableId.StartsWith("power_infrastructure:", StringComparison.Ordinal),
            TimberbornQaFieldTargetSelectors.WaterInfrastructure =>
                stableId.StartsWith("water_infrastructure:", StringComparison.Ordinal),
            _ => true,
        };
    }

    private static bool AllowsZeroCapacityBurnDamageProbe(
        string selector,
        TimberbornBurnDamageTargetState state)
    {
        if (state.TargetKind != TimberbornBurnDamageTargetKind.Infrastructure)
        {
            return false;
        }

        string stableId = state.TargetKey.StableId;
        return TimberbornQaFieldTargetSelectors.Normalize(selector) switch
        {
            TimberbornQaFieldTargetSelectors.Infrastructure => true,
            TimberbornQaFieldTargetSelectors.PathInfrastructure =>
                stableId.StartsWith("path_infrastructure:", StringComparison.Ordinal),
            TimberbornQaFieldTargetSelectors.PowerInfrastructure =>
                stableId.StartsWith("power_infrastructure:", StringComparison.Ordinal),
            TimberbornQaFieldTargetSelectors.WaterInfrastructure =>
                stableId.StartsWith("water_infrastructure:", StringComparison.Ordinal),
            _ => false,
        };
    }

    private static WildfireMaterialClass MaterialClassForBurnDamageKind(TimberbornBurnDamageTargetKind targetKind)
    {
        return targetKind switch
        {
            TimberbornBurnDamageTargetKind.Structure => WildfireMaterialClass.Building,
            TimberbornBurnDamageTargetKind.Storage => WildfireMaterialClass.Storage,
            TimberbornBurnDamageTargetKind.Infrastructure => WildfireMaterialClass.Infrastructure,
            TimberbornBurnDamageTargetKind.Tree => WildfireMaterialClass.Tree,
            TimberbornBurnDamageTargetKind.Crop => WildfireMaterialClass.Crop,
            _ => WildfireMaterialClass.Empty,
        };
    }

    private int RegisterBurnDamageProbe(TimberbornQaBurnDamageProbeTarget target, string source)
    {
        FireSimChange primeChange = new(
            CellIndex: target.FieldTarget.CellIndex,
            SetFuel: target.ProbeFuel,
            SetHeat: QaIgnitionHeat,
            SetFlammability: target.ProbeFlammability);
        RegisterChange(primeChange, source, shouldLog: false);
        _qaBurnDamageSpendChanges = new[]
        {
            new FireSimChange(
                CellIndex: target.FieldTarget.CellIndex,
                SetFuel: target.SpendFuel,
                SetHeat: QaIgnitionHeat,
                SetFlammability: target.ProbeFlammability),
        };
        LogRegisteredChanges(source, 1);
        _logSink.Info(
            "wildfire_timberborn_qa_burn_damage_spend_scheduled " +
            $"source={source} " +
            $"cell_index={target.FieldTarget.CellIndex} " +
            $"target_material={target.FieldTarget.MaterialClass} " +
            $"burn_damage_target_key={TimberbornQaCommandBridge.FormatToken(target.State.TargetKey.StableId)} " +
            $"burn_damage_spec_id={TimberbornQaCommandBridge.FormatToken(target.State.SpecId)} " +
            $"burn_damage_target_kind={target.State.TargetKind} " +
            $"remaining_capacity={target.State.RemainingCapacity} " +
            $"probe_fuel={target.ProbeFuel} " +
            $"spend_fuel={target.SpendFuel} " +
            $"scheduled_changes={_qaBurnDamageSpendChanges.Length}");

        return 1 + _qaBurnDamageSpendChanges.Length;
    }

    private int RegisterDirectConsequenceTargetProbe(TimberbornQaDirectConsequenceTarget target, string source)
    {
        FireSimChange primeChange = new(
            CellIndex: target.CellIndex,
            SetFuel: QaIgnitionFuel,
            SetHeat: QaIgnitionHeat,
            SetFlammability: QaIgnitionFlammability);
        RegisterChange(primeChange, source, shouldLog: false);
        _qaBurnDamageSpendChanges = new[]
        {
            new FireSimChange(
                CellIndex: target.CellIndex,
                SetFuel: (byte)Math.Max(0, QaIgnitionFuel - 1),
                SetHeat: QaIgnitionHeat,
                SetFlammability: QaIgnitionFlammability),
        };
        LogRegisteredChanges(source, 1);
        _logSink.Info(
            "wildfire_timberborn_qa_direct_consequence_target_stimulus_scheduled " +
            $"source={source} " +
            $"cell_index={target.CellIndex} " +
            $"target_kind={TimberbornQaCommandBridge.FormatToken(target.Kind)} " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(target.StableId)} " +
            $"scanned_cells={target.ScannedCellCount} " +
            $"scheduled_changes={_qaBurnDamageSpendChanges.Length}");

        return 1 + _qaBurnDamageSpendChanges.Length;
    }

    private void RegisterPendingQaBurnDamageSpendChanges()
    {
        if (_qaBurnDamageSpendChanges.Length == 0 || _registeredChangeCountSinceLastDispatch > 0)
        {
            return;
        }

        _qaBurnDamageSpendChanges
            .ToList()
            .ForEach(change => RegisterChange(change, "qa_burn_damage_spend", shouldLog: false));
        LogRegisteredChanges("qa_burn_damage_spend", _qaBurnDamageSpendChanges.Length);
        ClearQaBurnDamageSpendProbe();
    }

    private void ClearQaBurnDamageSpendProbe()
    {
        _qaBurnDamageSpendChanges = Array.Empty<FireSimChange>();
    }

    private void ClearQaIgnitionPeg()
    {
        _qaIgnitionPegChanges = Array.Empty<FireSimChange>();
        _qaIgnitionPegDispatchTicksRemaining = 0;
        _qaIgnitionPegSource = "placeholder";
    }

    private static Func<IEnumerable<TimberbornImportedFieldTarget>, IOrderedEnumerable<TimberbornImportedFieldTarget>> OrderByCenterDistance(
        FireGrid grid)
    {
        float centerX = (grid.Width - 1) / 2f;
        float centerY = (grid.Height - 1) / 2f;
        float centerZ = (grid.Depth - 1) / 2f;
        return targets => targets
            .OrderBy(target => SquaredDistance(target, centerX, centerY, centerZ))
            .ThenBy(static target => target.CellIndex);
    }

    private static float SquaredDistance(TimberbornImportedFieldTarget target, float centerX, float centerY, float centerZ)
    {
        float dx = target.X - centerX;
        float dy = target.Y - centerY;
        float dz = target.Z - centerZ;
        return dx * dx + dy * dy + dz * dz;
    }

    private static TimberbornImportedFieldTarget[] CreateImportedTargets(
        FireGrid grid,
        IReadOnlyList<ushort> initialCells,
        IReadOnlyList<WildfireCompanionField> companionFields)
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
                    companionFields[index].State.MaterialClass,
                    companionFields[index].TargetId,
                    initialCells[index]);
            })
            .Where(static target => target.MaterialClass != WildfireMaterialClass.Empty)
            .ToArray();
    }

    private static bool IsBurnableImportedTarget(TimberbornImportedFieldTarget target)
    {
        return PackedCell.Terrain(target.InitialCell) == 1 &&
            PackedCell.Fuel(target.InitialCell) > 0 &&
            PackedCell.Flammability(target.InitialCell) > 0 &&
            target.MaterialClass is WildfireMaterialClass.Tree or
                WildfireMaterialClass.Vegetation or
                WildfireMaterialClass.Crop or
                WildfireMaterialClass.Building or
                WildfireMaterialClass.Storage;
    }

    private void UpdateBurnDurationProof(uint tick, IReadOnlyList<CellDelta> deltas)
    {
        if (_burnDurationProofState.Status == "placeholder" ||
            _burnDurationProofState.DepletionTick.HasValue ||
            _burnDurationProofState.TimedOut)
        {
            return;
        }

        CellDelta[] targetDeltas = deltas
            .Where(delta => delta.CellIndex == _burnDurationProofState.CellIndex)
            .ToArray();
        bool hasBurnEvidence = targetDeltas
            .Any(static delta => PackedCell.BurningLevel(delta.OldCell) > 0 || PackedCell.BurningLevel(delta.NewCell) > 0);
        bool hasFuelDepletion = targetDeltas
            .Any(static delta => PackedCell.Fuel(delta.OldCell) > 0 && PackedCell.Fuel(delta.NewCell) == 0);
        uint? burnStartTick = _burnDurationProofState.BurnStartTick;
        if (!burnStartTick.HasValue && hasBurnEvidence)
        {
            burnStartTick = tick;
        }

        uint? depletionTick = _burnDurationProofState.DepletionTick;
        uint? elapsedBurnTicks = _burnDurationProofState.ElapsedBurnTicks;
        if (burnStartTick.HasValue && !depletionTick.HasValue && hasFuelDepletion)
        {
            depletionTick = tick;
            elapsedBurnTicks = checked((tick - burnStartTick.Value) + 1);
        }

        TimberbornQaBurnDurationProofState nextState = _burnDurationProofState with
        {
            BurnStartTick = burnStartTick,
            DepletionTick = depletionTick,
            ElapsedBurnTicks = elapsedBurnTicks,
            Status = depletionTick.HasValue
                ? "depleted"
                : burnStartTick.HasValue ? "burning" : "queued",
        };

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

    private void RecordBurnDurationSustainedHeatDispatch()
    {
        if (_burnDurationSustainedHeatTicksPendingDispatch <= 0)
        {
            return;
        }

        int pendingTicks = _burnDurationSustainedHeatTicksPendingDispatch;
        _burnDurationSustainedHeatTicksPendingDispatch = 0;
        if (_burnDurationProofState.Status == "placeholder")
        {
            return;
        }

        int appliedTicks = Math.Min(
            _burnDurationProofState.SustainedHeatTicks,
            _burnDurationProofState.SustainedHeatAppliedTicks + pendingTicks);
        TimberbornQaBurnDurationProofState nextState = _burnDurationProofState with
        {
            SustainedHeatAppliedTicks = appliedTicks,
            SustainedHeatComplete = appliedTicks >= _burnDurationProofState.SustainedHeatTicks,
        };

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
            $"sustained_heat_ticks={_burnDurationProofState.SustainedHeatTicks} " +
            $"sustained_heat_applied_ticks={_burnDurationProofState.SustainedHeatAppliedTicks} " +
            $"sustained_heat_complete={_burnDurationProofState.SustainedHeatComplete.ToString().ToLowerInvariant()} " +
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

    private sealed record TimberbornQaDeltaStimulusTargetSelection(
        TimberbornCellCoordinates Coordinates,
        string TargetSource,
        int RegisteredBurnDamageTargetCount,
        int RegisteredCropBurnTargetCount,
        int RegisteredCropBurnOwnedCellCount);
}

public interface ITimberbornFireSimulatorFactory
{
    IGpuFireSimulator Create(
        FireGrid grid,
        ReadOnlySpan<ushort> initialCells,
        ReadOnlySpan<WildfireCompanionField> companionFields);
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
    ushort InitialCell);

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
