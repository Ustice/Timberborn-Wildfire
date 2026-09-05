using Wildfire.Core;
using static Wildfire.Timberborn.Qa.TimberbornQaStimulusValues;
using static Wildfire.Timberborn.Qa.TimberbornQaTargetSelector;

namespace Wildfire.Timberborn.Qa;

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
    uint? LastCompletedTick = null,
    bool QueueFullFieldState = false,
    IReadOnlyList<int>? CellIndices = null,
    byte? SetSmoke = null,
    byte? SetSmokeContamination = null)
{
    public int RemainingCycleCount => Math.Max(0, RequestedCycleCount - CompletedCycleCount);

    public bool IsActive => CompletedCycleCount < RequestedCycleCount;
}

/// <summary>Owns QA target selection, forced stimuli, and evidence accounting around simulator ticks.</summary>
public sealed class TimberbornQaController
{
    private FireSimChange[] _qaBurnDamageSpendChanges = Array.Empty<FireSimChange>();
    private int _burnDurationSustainedHeatTicksPendingDispatch;
    private TimberbornQaDeltaStimulusSustainedHeatState? _qaDeltaStimulusSustainedHeatState;
    private TimberbornQaBurnDurationProofState _burnDurationProofState =
        TimberbornQaBurnDurationProofState.Placeholder;

    private readonly ITimberbornQaWorld _world;
    private readonly TimberbornQaTargetSelector _targets;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly TimberbornSustainedIgnitionScheduler _ignition;

    internal TimberbornQaController(ITimberbornQaWorld world, TimberbornSustainedIgnitionScheduler ignition, ITimberbornFireLogSink logSink)
    {
        _world = world;
        _targets = new TimberbornQaTargetSelector(() => world.ImportedTargets, logSink);
        _ignition = ignition;
        _logSink = logSink;
    }

    public TimberbornQaBurnDurationProofState BurnDurationProofState => _burnDurationProofState;
    public TimberbornQaDeltaStimulusSustainedHeatState? QaDeltaStimulusSustainedHeatState => _qaDeltaStimulusSustainedHeatState;

    internal void Reset()
    {
        ClearQaBurnDamageSpendProbe();
        _qaDeltaStimulusSustainedHeatState = null;
        _burnDurationSustainedHeatTicksPendingDispatch = 0;
        _burnDurationProofState = TimberbornQaBurnDurationProofState.Placeholder;
    }

    internal void PrepareTick()
    {
        RegisterPendingQaBurnDamageSpendChanges();
    }

    internal void CompleteTickPreparation(string? sustainedInputSource)
    {
        if (sustainedInputSource == "qa_burn_duration_stimulus")
        {
            _burnDurationSustainedHeatTicksPendingDispatch++;
        }
        QueueNextSustainedQaDeltaStimulusCycle();
    }

    internal void AfterTick(GpuFireStepResult result)
    {
        CompleteQueuedSustainedQaDeltaStimulusCycle(result.Tick);
        RecordBurnDurationSustainedHeatDispatch();
        UpdateBurnDurationProof(result.Tick, result.Deltas);
    }

    private FireGrid RequireGrid() => _world.RequireInitializedGrid();
    private IReadOnlyList<TimberbornImportedFieldTarget> _importedTargets => _world.ImportedTargets;
    private int _registeredChangeCountSinceLastDispatch => _world.RegisteredChangeCountSinceLastDispatch;
    private uint? LastTick => _world.LastTick;
    private void RegisterChange(FireSimChange change, string source, bool shouldLog = true) => _world.RegisterChange(change, source, shouldLog);
    private void LogRegisteredChanges(string source, int count) => _world.LogRegisteredChanges(source, count);

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
        if (normalizedSelector is TimberbornQaFieldTargetSelectors.BeaverExposure or
            TimberbornQaFieldTargetSelectors.ToxicBeaverExposure)
        {
            return RegisterSustainedBeaverExposureDeltaStimulus(grid, normalizedSelector, beaverExposureTarget);
        }

        if (normalizedSelector is TimberbornQaFieldTargetSelectors.Crop or TimberbornQaFieldTargetSelectors.Bush &&
            burnDamageTargets is not null)
        {
            TimberbornQaBurnDamageProbeTarget target = _targets.FindCropBurnDamageProbeTarget(
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
            TimberbornQaDirectConsequenceTarget target = _targets.FindDirectConsequenceTarget(
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
            TimberbornQaBurnDamageProbeTarget target = _targets.FindBurnDamageProbeTarget(
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

        if (normalizedSelector == TimberbornQaFieldTargetSelectors.TaintedAsh)
        {
            return QueueTaintedAshDeltaStimulus(normalizedSelector);
        }

        TimberbornImportedFieldTarget ignitionTarget = normalizedSelector switch
        {
            TimberbornQaFieldTargetSelectors.CenterTree => _targets.FindImportedTarget(
                candidate => IsBurnableImportedTarget(candidate) &&
                    candidate.MaterialClass == WildfireMaterialClass.Tree,
                OrderByCenterDistance(grid),
                $"No imported center tree field target was found for QA delta stimulus selector '{normalizedSelector}'."),
            TimberbornQaFieldTargetSelectors.ContaminatedTree => _targets.FindImportedTarget(
                candidate => IsBurnableImportedTarget(candidate) &&
                    candidate.MaterialClass == WildfireMaterialClass.Tree &&
                    candidate.SoilContamination > 0,
                OrderByDescendingSoilContamination(),
                $"No imported contaminated tree field target was found for QA delta stimulus selector '{normalizedSelector}'."),
            _ => _targets.FindImportedTarget(
                candidate => IsBurnableImportedTarget(candidate) &&
                    TimberbornQaFieldTargetSelectors.Matches(candidate.MaterialClass, normalizedSelector),
                $"No imported burnable field target was found for QA delta stimulus selector '{normalizedSelector}'."),
        };
        int queuedHeatChangeCount = normalizedSelector == TimberbornQaFieldTargetSelectors.ContaminatedTree
            ? RegisterForcedIgnitionCluster(grid, ignitionTarget.CellIndex, "qa_delta_stimulus")
            : RegisterIgnitionCluster(grid, ignitionTarget.CellIndex, "qa_delta_stimulus");

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

    private TimberbornQaDeltaStimulusResult QueueTaintedAshDeltaStimulus(string normalizedSelector)
    {
        TimberbornImportedFieldTarget target = _targets.FindImportedTarget(
            candidate => IsBurnableImportedTarget(candidate) &&
                TimberbornQaFieldTargetSelectors.Matches(candidate.MaterialClass, normalizedSelector),
            OrderByDescendingSoilContamination(),
            $"No imported burnable field target was found for QA tainted ash selector '{normalizedSelector}'.");
        RegisterChange(
            new FireSimChange(
                CellIndex: target.CellIndex,
                SetAsh: QaTaintedAshAmount,
                SetAshContamination: QaTaintedAshContamination),
            "qa_tainted_ash_stimulus");

        return new TimberbornQaDeltaStimulusResult(
            normalizedSelector,
            target.CellIndex,
            target.X,
            target.Y,
            target.Z,
            target.MaterialClass,
            target.CompanionTargetId,
            target.InitialCell,
            SetHeat: 0,
            QueuedHeatChangeCount: 0,
            SetAsh: QaTaintedAshAmount,
            SetAshContamination: QaTaintedAshContamination,
            QueuedAshChangeCount: 1,
            TargetSource: "qa_tainted_ash_field");
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
        TimberbornImportedFieldTarget target = _targets.FindImportedTarget(
            candidate => IsWaterSuppressionTarget(candidate, normalizedSelector) &&
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
            QueuedWaterChangeCount: 1,
            TargetSoilContamination: target.SoilContamination,
            IsAffectedCellContaminated: target.SoilContamination > 0,
            IsContaminatedSuppressionInput: target.MaterialClass == WildfireMaterialClass.Badwater,
            IsBadwaterSuppressionInput: target.MaterialClass == WildfireMaterialClass.Badwater);
    }

    public TimberbornQaAshWaterStimulusResult QueueAshWaterQaStimulus(string target)
    {
        _ = RequireGrid();
        string normalizedTarget = TimberbornQaAshWaterStimulusTargets.Normalize(target);
        TimberbornImportedFieldTarget selectedTarget = _targets.FindImportedTarget(
            candidate => IsBurnableImportedTarget(candidate) &&
                PackedCell.Water(candidate.InitialCell) < QaWaterSuppressionWater,
            normalizedTarget == TimberbornQaAshWaterStimulusTargets.Tainted
                ? OrderByDescendingSoilContamination()
                : static targets => targets.OrderBy(static target => target.CellIndex),
            $"No imported burnable field target without maximum water was found for QA ash-water target '{normalizedTarget}'.");
        byte ashContamination = normalizedTarget == TimberbornQaAshWaterStimulusTargets.Tainted
            ? QaTaintedAshContamination
            : (byte)0;

        RegisterChange(
            new FireSimChange(
                CellIndex: selectedTarget.CellIndex,
                SetAsh: QaTaintedAshAmount,
                SetAshContamination: ashContamination,
                SetWater: QaWaterSuppressionWater),
            "qa_ash_water_stimulus");

        return new TimberbornQaAshWaterStimulusResult(
            normalizedTarget,
            normalizedTarget,
            selectedTarget.CellIndex,
            selectedTarget.X,
            selectedTarget.Y,
            selectedTarget.Z,
            selectedTarget.MaterialClass,
            selectedTarget.CompanionTargetId,
            selectedTarget.InitialCell,
            QaTaintedAshAmount,
            ashContamination,
            QaWaterSuppressionWater,
            QueuedAshChangeCount: 1,
            QueuedWaterChangeCount: 1,
            ExpectedWaterTaintAttemptCount: normalizedTarget == TimberbornQaAshWaterStimulusTargets.Tainted ? 1 : 0,
            ExpectedWaterTaint: normalizedTarget == TimberbornQaAshWaterStimulusTargets.Tainted);
    }

    public TimberbornQaBurnDurationStimulusResult QueueBurnDurationQaStimulus(string target)
    {
        FireGrid grid = RequireGrid();
        TimberbornQaBurnDurationStimulusTarget selectedTarget =
            TimberbornQaBurnDurationStimulusTargets.SelectTarget(grid, _importedTargets, target);
        FireSimChange heatPegChange = new(CellIndex: selectedTarget.CellIndex, SetHeat: QaIgnitionHeat);
        RegisterChange(heatPegChange, "qa_burn_duration_stimulus");
        _burnDurationSustainedHeatTicksPendingDispatch = 1;
        _ignition.Start(new[] { heatPegChange }, "qa_burn_duration_stimulus");

        int ignitionPegDispatchTicks = _ignition.DurationTicks;
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
        string normalizedSelector,
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
        bool queueToxicSmoke = normalizedSelector == TimberbornQaFieldTargetSelectors.ToxicBeaverExposure;
        _qaDeltaStimulusSustainedHeatState = new TimberbornQaDeltaStimulusSustainedHeatState(
            cellIndex,
            x,
            y,
            z,
            QaBeaverSmokeExposureStimulusCell,
            queueToxicSmoke ? "beaver_candidate_toxic_smoke_cell" : "beaver_candidate_cell",
            QaDeltaStimulusSustainedHeatCycleCount,
            QueueFullFieldState: true,
            CellIndices: target.CellIndices,
            SetSmoke: queueToxicSmoke ? QaBeaverToxicSmokeExposureSmoke : null,
            SetSmokeContamination: queueToxicSmoke ? QaBeaverToxicSmokeExposureContamination : null);
        QueueNextSustainedQaDeltaStimulusCycle();
        int queuedCellCount = _qaDeltaStimulusSustainedHeatState.QueuedCycleNumber.HasValue
            ? target.CellIndices.Count
            : 0;

        return new TimberbornQaDeltaStimulusResult(
            normalizedSelector,
            cellIndex,
            x,
            y,
            z,
            importedTarget?.MaterialClass ?? WildfireMaterialClass.Unknown,
            importedTarget?.CompanionTargetId ?? 0,
            importedTarget?.InitialCell ?? 0,
            QaIgnitionHeat,
            QueuedHeatChangeCount: queuedCellCount,
            TargetSource: queueToxicSmoke ? "beaver_candidate_toxic_smoke_cell" : "beaver_candidate_cell",
            SustainedHeatSetCell: QaBeaverSmokeExposureStimulusCell,
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
            BeaverExposureTargetSkippedBoundedSampling: target.SkippedBoundedSamplingCount,
            SetSmoke: queueToxicSmoke ? QaBeaverToxicSmokeExposureSmoke : null,
            SetSmokeContamination: queueToxicSmoke ? QaBeaverToxicSmokeExposureContamination : null,
            QueuedSmokeChangeCount: queueToxicSmoke ? queuedCellCount : null);
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
        _ignition.Start(changes, source);
        LogRegisteredChanges(source, changes.Length);
        return changes.Length;
    }

    private int RegisterForcedIgnitionCluster(FireGrid grid, int centerCellIndex, string source)
    {
        int centerX = centerCellIndex % grid.Width;
        int centerY = (centerCellIndex / grid.Width) % grid.Height;
        int centerZ = centerCellIndex / (grid.Width * grid.Height);
        FireSimChange[] changes = _importedTargets
            .Where(IsBurnableImportedTarget)
            .Where(target => Math.Abs(target.X - centerX) <= 1 &&
                Math.Abs(target.Y - centerY) <= 1 &&
                Math.Abs(target.Z - centerZ) <= 1)
            .OrderBy(target => Math.Abs(target.X - centerX) +
                Math.Abs(target.Y - centerY) +
                Math.Abs(target.Z - centerZ))
            .ThenBy(static target => target.CellIndex)
            .Take(9)
            .Select(static target => new FireSimChange(
                CellIndex: target.CellIndex,
                SetFuel: QaIgnitionFuel,
                SetHeat: QaIgnitionHeat,
                SetFlammability: QaIgnitionFlammability,
                SetWater: QaIgnitionWater,
                SetBurningLevel: 7))
            .ToArray();

        if (changes.Length == 0)
        {
            throw new InvalidOperationException("No imported burnable field targets were found for the QA forced ignition cluster.");
        }

        changes
            .ToList()
            .ForEach(change => RegisterChange(change, source, shouldLog: false));
        _ignition.Start(changes, source);
        LogRegisteredChanges(source, changes.Length);
        return changes.Length;
    }

    private void QueueNextSustainedQaDeltaStimulusCycle()
    {
        if (_qaDeltaStimulusSustainedHeatState is not { IsActive: true, QueuedCycleNumber: null } state)
        {
            return;
        }

        int cycleNumber = state.CompletedCycleCount + 1;
        IReadOnlyList<int> cellIndices = state.CellIndices is { Count: > 0 }
            ? state.CellIndices
            : new[] { state.CellIndex };
        cellIndices
            .Select(cellIndex => state.QueueFullFieldState
                ? new FireSimChange(
                    CellIndex: cellIndex,
                    SetCell: state.SetCell,
                    SetSmoke: state.SetSmoke,
                    SetSmokeContamination: state.SetSmokeContamination)
                : new FireSimChange(CellIndex: cellIndex, SetHeat: QaIgnitionHeat))
            .ToList()
            .ForEach(change => RegisterChange(
                change,
                "qa_delta_stimulus_sustained_heat",
                shouldLog: false));
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
            $"set_heat={(state.QueueFullFieldState ? PackedCell.Heat(state.SetCell) : QaIgnitionHeat)} " +
            $"set_cell={state.SetCell} " +
            $"set_smoke={FormatNumber(state.SetSmoke)} " +
            $"set_smoke_contamination={FormatNumber(state.SetSmokeContamination)} " +
            $"queue_full_field_state={state.QueueFullFieldState.ToString().ToLowerInvariant()} " +
            $"queued_cells={cellIndices.Count} " +
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

    private static string FormatNumber(byte? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }


}
