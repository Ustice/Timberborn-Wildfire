using Timberborn.MapIndexSystem;
using Timberborn.MapStateSystem;
using Timberborn.SoilMoistureSystem;
using Timberborn.TerrainSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornQaCommandBridge
{
    public const string StatusCommand = "status";
    public const string HelpCommand = "help";
    public const string QaReadinessCommand = "qa-readiness";
    public const string QaDeltaStimulusCommand = "qa-delta-stimulus";
    public const string QaBuildingBurnoutStimulusCommand = "qa-building-burnout-stimulus";
    public const string QaWaterSuppressionStimulusCommand = "qa-water-suppression-stimulus";
    public const string QaBurnDurationStimulusCommand = "qa-burn-duration-stimulus";
    public const string QaFirePresetCommand = "qa-fire-preset";
    public const string QaSoilMoistureRangeCommand = "qa-soil-moisture-range";

    private readonly ITimberbornQaCommandStateProvider _stateProvider;
    private readonly ITimberbornQaDeltaStimulus _deltaStimulus;
    private readonly ITimberbornQaBuildingBurnoutStimulus _buildingBurnoutStimulus;
    private readonly ITimberbornQaWaterSuppressionStimulus _waterSuppressionStimulus;
    private readonly ITimberbornQaBurnDurationStimulus _burnDurationStimulus;
    private readonly ITimberbornQaFireSimParameterPresetSelector _fireSimParameterPresetSelector;
    private readonly ITimberbornQaSoilMoistureMapProbe _soilMoistureMapProbe;
    private readonly ITimberbornQaCommandLogSink _logSink;
    private readonly IReadOnlyDictionary<string, Func<TimberbornQaCommandResult>> _commands;

    public TimberbornQaCommandBridge()
        : this(
            TimberbornQaCommandStateProvider.Placeholder,
            NullTimberbornQaDeltaStimulus.Instance,
            NullTimberbornQaBuildingBurnoutStimulus.Instance,
            NullTimberbornQaWaterSuppressionStimulus.Instance,
            NullTimberbornQaBurnDurationStimulus.Instance,
            NullTimberbornQaFireSimParameterPresetSelector.Instance,
            NullTimberbornQaSoilMoistureMapProbe.Instance,
            NullTimberbornQaCommandLogSink.Instance)
    {
    }

    public TimberbornQaCommandBridge(
        ITimberbornQaCommandStateProvider stateProvider,
        ITimberbornQaCommandLogSink logSink)
        : this(
            stateProvider,
            NullTimberbornQaDeltaStimulus.Instance,
            NullTimberbornQaBuildingBurnoutStimulus.Instance,
            NullTimberbornQaWaterSuppressionStimulus.Instance,
            NullTimberbornQaBurnDurationStimulus.Instance,
            NullTimberbornQaFireSimParameterPresetSelector.Instance,
            NullTimberbornQaSoilMoistureMapProbe.Instance,
            logSink)
    {
    }

    public TimberbornQaCommandBridge(
        ITimberbornQaCommandStateProvider stateProvider,
        ITimberbornQaDeltaStimulus deltaStimulus,
        ITimberbornQaCommandLogSink logSink)
        : this(
            stateProvider,
            deltaStimulus,
            NullTimberbornQaBuildingBurnoutStimulus.Instance,
            NullTimberbornQaWaterSuppressionStimulus.Instance,
            NullTimberbornQaBurnDurationStimulus.Instance,
            NullTimberbornQaFireSimParameterPresetSelector.Instance,
            NullTimberbornQaSoilMoistureMapProbe.Instance,
            logSink)
    {
    }

    public TimberbornQaCommandBridge(
        ITimberbornQaCommandStateProvider stateProvider,
        ITimberbornQaDeltaStimulus deltaStimulus,
        ITimberbornQaBuildingBurnoutStimulus buildingBurnoutStimulus,
        ITimberbornQaCommandLogSink logSink)
        : this(
            stateProvider,
            deltaStimulus,
            buildingBurnoutStimulus,
            NullTimberbornQaWaterSuppressionStimulus.Instance,
            NullTimberbornQaBurnDurationStimulus.Instance,
            NullTimberbornQaFireSimParameterPresetSelector.Instance,
            NullTimberbornQaSoilMoistureMapProbe.Instance,
            logSink)
    {
    }

    public TimberbornQaCommandBridge(
        ITimberbornQaCommandStateProvider stateProvider,
        ITimberbornQaDeltaStimulus deltaStimulus,
        ITimberbornQaBuildingBurnoutStimulus buildingBurnoutStimulus,
        ITimberbornQaWaterSuppressionStimulus waterSuppressionStimulus,
        ITimberbornQaCommandLogSink logSink)
        : this(
            stateProvider,
            deltaStimulus,
            buildingBurnoutStimulus,
            waterSuppressionStimulus,
            NullTimberbornQaBurnDurationStimulus.Instance,
            NullTimberbornQaFireSimParameterPresetSelector.Instance,
            NullTimberbornQaSoilMoistureMapProbe.Instance,
            logSink)
    {
    }

    public TimberbornQaCommandBridge(
        ITimberbornQaCommandStateProvider stateProvider,
        ITimberbornQaDeltaStimulus deltaStimulus,
        ITimberbornQaBuildingBurnoutStimulus buildingBurnoutStimulus,
        ITimberbornQaWaterSuppressionStimulus waterSuppressionStimulus,
        ITimberbornQaBurnDurationStimulus burnDurationStimulus,
        ITimberbornQaCommandLogSink logSink)
        : this(
            stateProvider,
            deltaStimulus,
            buildingBurnoutStimulus,
            waterSuppressionStimulus,
            burnDurationStimulus,
            NullTimberbornQaFireSimParameterPresetSelector.Instance,
            NullTimberbornQaSoilMoistureMapProbe.Instance,
            logSink)
    {
    }

    public TimberbornQaCommandBridge(
        ITimberbornQaCommandStateProvider stateProvider,
        ITimberbornQaDeltaStimulus deltaStimulus,
        ITimberbornQaBuildingBurnoutStimulus buildingBurnoutStimulus,
        ITimberbornQaWaterSuppressionStimulus waterSuppressionStimulus,
        ITimberbornQaBurnDurationStimulus burnDurationStimulus,
        ITimberbornQaFireSimParameterPresetSelector fireSimParameterPresetSelector,
        ITimberbornQaCommandLogSink logSink)
        : this(
            stateProvider,
            deltaStimulus,
            buildingBurnoutStimulus,
            waterSuppressionStimulus,
            burnDurationStimulus,
            fireSimParameterPresetSelector,
            NullTimberbornQaSoilMoistureMapProbe.Instance,
            logSink)
    {
    }

    public TimberbornQaCommandBridge(
        ITimberbornQaCommandStateProvider stateProvider,
        ITimberbornQaSoilMoistureMapProbe soilMoistureMapProbe,
        ITimberbornQaCommandLogSink logSink)
        : this(
            stateProvider,
            NullTimberbornQaDeltaStimulus.Instance,
            NullTimberbornQaBuildingBurnoutStimulus.Instance,
            NullTimberbornQaWaterSuppressionStimulus.Instance,
            NullTimberbornQaBurnDurationStimulus.Instance,
            NullTimberbornQaFireSimParameterPresetSelector.Instance,
            soilMoistureMapProbe,
            logSink)
    {
    }

    public TimberbornQaCommandBridge(
        ITimberbornQaCommandStateProvider stateProvider,
        ITimberbornQaDeltaStimulus deltaStimulus,
        ITimberbornQaBuildingBurnoutStimulus buildingBurnoutStimulus,
        ITimberbornQaWaterSuppressionStimulus waterSuppressionStimulus,
        ITimberbornQaBurnDurationStimulus burnDurationStimulus,
        ITimberbornQaFireSimParameterPresetSelector fireSimParameterPresetSelector,
        ITimberbornQaSoilMoistureMapProbe soilMoistureMapProbe,
        ITimberbornQaCommandLogSink logSink)
    {
        if (stateProvider is null)
        {
            throw new ArgumentNullException(nameof(stateProvider));
        }

        if (deltaStimulus is null)
        {
            throw new ArgumentNullException(nameof(deltaStimulus));
        }

        if (buildingBurnoutStimulus is null)
        {
            throw new ArgumentNullException(nameof(buildingBurnoutStimulus));
        }

        if (waterSuppressionStimulus is null)
        {
            throw new ArgumentNullException(nameof(waterSuppressionStimulus));
        }

        if (burnDurationStimulus is null)
        {
            throw new ArgumentNullException(nameof(burnDurationStimulus));
        }

        if (fireSimParameterPresetSelector is null)
        {
            throw new ArgumentNullException(nameof(fireSimParameterPresetSelector));
        }

        if (soilMoistureMapProbe is null)
        {
            throw new ArgumentNullException(nameof(soilMoistureMapProbe));
        }

        if (logSink is null)
        {
            throw new ArgumentNullException(nameof(logSink));
        }

        _stateProvider = stateProvider;
        _deltaStimulus = deltaStimulus;
        _buildingBurnoutStimulus = buildingBurnoutStimulus;
        _waterSuppressionStimulus = waterSuppressionStimulus;
        _burnDurationStimulus = burnDurationStimulus;
        _fireSimParameterPresetSelector = fireSimParameterPresetSelector;
        _soilMoistureMapProbe = soilMoistureMapProbe;
        _logSink = logSink;
        Dictionary<string, Func<TimberbornQaCommandResult>> commands = new(StringComparer.OrdinalIgnoreCase)
        {
            [StatusCommand] = ExecuteStatus,
            [HelpCommand] = ExecuteHelp,
            [QaReadinessCommand] = ExecuteQaReadiness,
        };

        if (!ReferenceEquals(deltaStimulus, NullTimberbornQaDeltaStimulus.Instance))
        {
            commands[QaDeltaStimulusCommand] = () => ExecuteQaDeltaStimulus(null);
        }

        if (!ReferenceEquals(buildingBurnoutStimulus, NullTimberbornQaBuildingBurnoutStimulus.Instance))
        {
            commands[QaBuildingBurnoutStimulusCommand] = ExecuteQaBuildingBurnoutStimulus;
        }

        if (!ReferenceEquals(waterSuppressionStimulus, NullTimberbornQaWaterSuppressionStimulus.Instance))
        {
            commands[QaWaterSuppressionStimulusCommand] = () => ExecuteQaWaterSuppressionStimulus(null);
        }

        if (!ReferenceEquals(burnDurationStimulus, NullTimberbornQaBurnDurationStimulus.Instance))
        {
            commands[QaBurnDurationStimulusCommand] = () => ExecuteQaBurnDurationStimulus(null);
        }

        if (!ReferenceEquals(fireSimParameterPresetSelector, NullTimberbornQaFireSimParameterPresetSelector.Instance))
        {
            commands[QaFirePresetCommand] = () => ExecuteQaFirePreset(null);
        }

        if (!ReferenceEquals(soilMoistureMapProbe, NullTimberbornQaSoilMoistureMapProbe.Instance))
        {
            commands[QaSoilMoistureRangeCommand] = ExecuteQaSoilMoistureRange;
        }

        _commands = commands;
    }

    public TimberbornQaCommandResult Execute(string? commandText)
    {
        if (commandText is null)
        {
            const string nullCommand = "null";
            _logSink.Info($"wildfire_command_request command={FormatToken(nullCommand)}");

            TimberbornQaCommandResult failure = TimberbornQaCommandResult.CreateFailure(
                nullCommand,
                "Command text is required.",
                TimberbornQaCommandState.Placeholder,
                KnownCommands);
            _logSink.Warning(failure.ResultToken);
            return failure;
        }

        string command = NormalizeCommand(commandText);
        _logSink.Info($"wildfire_command_request command={FormatToken(command)}");

        if (!_commands.TryGetValue(command, out Func<TimberbornQaCommandResult>? handler))
        {
            TimberbornQaCommandResult failure = TimberbornQaCommandResult.CreateFailure(
                command,
                $"Unknown command '{command}'.",
                TimberbornQaCommandState.Placeholder,
                KnownCommands);
            _logSink.Warning(failure.ResultToken);
            return failure;
        }

        if (IsSimulatorChangeCommand(command) && !CanAcceptArguments(command, commandText) && HasArguments(commandText))
        {
            TimberbornQaCommandResult failure = TimberbornQaCommandResult.CreateFailure(
                command,
                $"Command '{command}' does not accept arguments.",
                TimberbornQaCommandState.Placeholder,
                KnownCommands);
            _logSink.Warning(failure.ResultToken);
            return failure;
        }

        if (IsSimulatorChangeCommand(command))
        {
            TimberbornQaCommandState state = _stateProvider.GetState();
            if (!state.WildfireEnabled)
            {
                TimberbornQaCommandResult failure = TimberbornQaCommandResult.CreateFailure(
                    command,
                    "wildfire_disabled",
                    state,
                    KnownCommands);
                _logSink.Warning(failure.ResultToken);
                return failure;
            }
        }

        try
        {
            TimberbornQaCommandResult result =
                StringComparer.OrdinalIgnoreCase.Equals(command, QaBurnDurationStimulusCommand)
                    ? ExecuteQaBurnDurationStimulus(commandText)
                    : StringComparer.OrdinalIgnoreCase.Equals(command, QaFirePresetCommand)
                        ? ExecuteQaFirePreset(commandText)
                        : StringComparer.OrdinalIgnoreCase.Equals(command, QaDeltaStimulusCommand)
                            ? ExecuteQaDeltaStimulus(commandText)
                            : StringComparer.OrdinalIgnoreCase.Equals(command, QaWaterSuppressionStimulusCommand)
                                ? ExecuteQaWaterSuppressionStimulus(commandText)
                                : handler();
            _logSink.Info(result.ResultToken);
            return result;
        }
        catch (Exception exception)
        {
            TimberbornQaCommandState state;
            try
            {
                state = _stateProvider.GetState();
            }
            catch
            {
                state = TimberbornQaCommandState.Placeholder;
            }

            TimberbornQaCommandResult failure = TimberbornQaCommandResult.CreateFailure(
                command,
                exception.Message,
                state,
                KnownCommands);
            _logSink.Warning(failure.ResultToken);
            return failure;
        }
    }

    public IReadOnlyList<string> KnownCommands => _commands.Keys.OrderBy(command => command, StringComparer.OrdinalIgnoreCase).ToArray();

    private TimberbornQaCommandResult ExecuteStatus()
    {
        return TimberbornQaCommandResult.CreateSuccess(StatusCommand, _stateProvider.GetState(), KnownCommands);
    }

    private TimberbornQaCommandResult ExecuteHelp()
    {
        string writableCommands = string.Join(
            ",",
            KnownCommands.Where(IsSimulatorChangeCommand));
        string message = string.IsNullOrEmpty(writableCommands)
            ? "Supported commands are read-only: help, qa-readiness, status."
            : "Supported commands: " +
            $"{string.Join(",", KnownCommands)}. " +
            $"QA-only simulator change commands: {writableCommands}.";

        return TimberbornQaCommandResult.CreateSuccess(
            HelpCommand,
            _stateProvider.GetState(),
            KnownCommands,
            message);
    }

    private TimberbornQaCommandResult ExecuteQaReadiness()
    {
        TimberbornQaCommandState state = _stateProvider.GetState();

        return TimberbornQaCommandResult.CreateSuccess(
            QaReadinessCommand,
            state,
            KnownCommands,
            state.WildfireEnabled
                ? state.IsLoadedGameReady ? "loaded_game_ready" : "loaded_game_not_ready"
                : "wildfire_disabled");
    }

    private TimberbornQaCommandResult ExecuteQaSoilMoistureRange()
    {
        TimberbornQaSoilMoistureRangeResult range = _soilMoistureMapProbe.ScanSoilMoistureRange();

        return TimberbornQaCommandResult.CreateSuccess(
            QaSoilMoistureRangeCommand,
            _stateProvider.GetState(),
            KnownCommands,
            "soil_moisture_range_" +
            $"samples={range.SampleCount}_" +
            $"skipped={range.SkippedCount}_" +
            $"moist_cells={range.MoistCellCount}_" +
            $"min={FormatFloat(range.Min)}_" +
            $"max={FormatFloat(range.Max)}_" +
            $"avg={FormatFloat(range.Average)}_" +
            $"min_x={range.MinX}_min_y={range.MinY}_min_z={range.MinZ}_" +
            $"max_x={range.MaxX}_max_y={range.MaxY}_max_z={range.MaxZ}");
    }

    private TimberbornQaCommandResult ExecuteQaDeltaStimulus(string? commandText)
    {
        string targetSelector = ParseFieldTargetSelector(commandText) ?? TimberbornQaFieldTargetSelectors.Default;
        TimberbornQaDeltaStimulusResult stimulusResult = _deltaStimulus.QueueDeltaStimulus(targetSelector);
        TimberbornQaCommandState state = _stateProvider.GetState();

        return TimberbornQaCommandResult.CreateSuccess(
            QaDeltaStimulusCommand,
            state,
            KnownCommands,
            "queued_imported_field_stimulus_" +
            $"target_selector={stimulusResult.TargetSelector}_" +
            $"target_material={stimulusResult.MaterialClass}_" +
            $"companion_target_id={stimulusResult.CompanionTargetId}_" +
            $"target_index={stimulusResult.CellIndex}_" +
            $"target_x={stimulusResult.X}_" +
            $"target_y={stimulusResult.Y}_" +
            $"target_z={stimulusResult.Z}_" +
            $"initial_cell={stimulusResult.InitialCell}_" +
            $"set_heat={stimulusResult.SetHeat}_" +
            $"queued_heat_changes={stimulusResult.QueuedHeatChangeCount}_" +
            $"burn_damage_target_key={FormatToken(stimulusResult.BurnDamageTargetKey)}_" +
            $"burn_damage_spec_id={FormatToken(stimulusResult.BurnDamageSpecId)}_" +
            $"burn_damage_target_kind={FormatToken(stimulusResult.BurnDamageTargetKind?.ToString())}_" +
            $"burn_damage_remaining_capacity={FormatNumber(stimulusResult.BurnDamageRemainingCapacity)}_" +
            $"burn_damage_probe_fuel={FormatNumber(stimulusResult.BurnDamageProbeFuel)}_" +
            $"burn_damage_spend_fuel={FormatNumber(stimulusResult.BurnDamageSpendFuel)}_" +
            $"direct_target_kind={FormatToken(stimulusResult.DirectTargetKind)}_" +
            $"direct_target_stable_id={FormatToken(stimulusResult.DirectTargetStableId)}_" +
            $"direct_target_scanned_cells={FormatNumber(stimulusResult.DirectTargetScannedCellCount)}_" +
            $"target_source={FormatToken(stimulusResult.TargetSource)}_" +
            $"registered_burn_damage_targets={FormatNumber(stimulusResult.RegisteredBurnDamageTargetCount)}_" +
            $"registered_crop_burn_targets={FormatNumber(stimulusResult.RegisteredCropBurnTargetCount)}_" +
            $"registered_crop_burn_owned_cells={FormatNumber(stimulusResult.RegisteredCropBurnOwnedCellCount)}_" +
            $"sustained_heat_set_cell={FormatNumber(stimulusResult.SustainedHeatSetCell)}_" +
            $"sustained_heat_requested_cycles={FormatNumber(stimulusResult.SustainedHeatRequestedCycleCount)}_" +
            $"sustained_heat_completed_cycles={FormatNumber(stimulusResult.SustainedHeatCompletedCycleCount)}_" +
            $"sustained_heat_remaining_cycles={FormatNumber(stimulusResult.SustainedHeatRemainingCycleCount)}_" +
            $"sustained_heat_queued_cycle={FormatNumber(stimulusResult.SustainedHeatQueuedCycleNumber)}_" +
            $"beaver_exposure_target_beaver_id={FormatToken(stimulusResult.BeaverExposureTargetBeaverId)}_" +
            $"beaver_exposure_target_beaver_x={FormatNumber(stimulusResult.BeaverExposureTargetBeaverX)}_" +
            $"beaver_exposure_target_beaver_y={FormatNumber(stimulusResult.BeaverExposureTargetBeaverY)}_" +
            $"beaver_exposure_target_beaver_z={FormatNumber(stimulusResult.BeaverExposureTargetBeaverZ)}_" +
            $"beaver_exposure_target_candidate_cells={FormatNumber(stimulusResult.BeaverExposureTargetCandidateCells)}_" +
            $"beaver_exposure_target_sampled_beavers={FormatNumber(stimulusResult.BeaverExposureTargetSampledBeavers)}_" +
            $"beaver_exposure_target_skipped_no_position_api={FormatNumber(stimulusResult.BeaverExposureTargetSkippedNoPositionApi)}_" +
            $"beaver_exposure_target_skipped_bounded_sampling={FormatNumber(stimulusResult.BeaverExposureTargetSkippedBoundedSampling)}");
    }

    private TimberbornQaCommandResult ExecuteQaBuildingBurnoutStimulus()
    {
        TimberbornQaBuildingBurnoutStimulusResult stimulusResult =
            _buildingBurnoutStimulus.QueueBuildingBurnoutStimulus();
        TimberbornQaCommandState state = _stateProvider.GetState();

        return TimberbornQaCommandResult.CreateSuccess(
            QaBuildingBurnoutStimulusCommand,
            state,
            KnownCommands,
            "queued_building_burnout_stimulus_" +
            $"target_index={stimulusResult.CellIndex}_" +
            $"target_x={stimulusResult.X}_" +
            $"target_y={stimulusResult.Y}_" +
            $"target_z={stimulusResult.Z}_" +
            $"scanned_cells={stimulusResult.ScannedCellCount}_" +
            $"set_heat={stimulusResult.SetHeat}_" +
            $"set_fuel={stimulusResult.SetFuel}_" +
            $"queued_field_changes={stimulusResult.QueuedFieldChangeCount}");
    }

    private TimberbornQaCommandResult ExecuteQaWaterSuppressionStimulus(string? commandText)
    {
        string targetSelector = ParseFieldTargetSelector(commandText) ?? TimberbornQaFieldTargetSelectors.Default;
        TimberbornQaWaterSuppressionStimulusResult stimulusResult =
            _waterSuppressionStimulus.QueueWaterSuppressionStimulus(targetSelector);
        TimberbornQaCommandState state = _stateProvider.GetState();

        return TimberbornQaCommandResult.CreateSuccess(
            QaWaterSuppressionStimulusCommand,
            state,
            KnownCommands,
            "queued_water_fuel_lock_stimulus_" +
            $"target_selector={stimulusResult.TargetSelector}_" +
            $"target_index={stimulusResult.CellIndex}_" +
            $"target_x={stimulusResult.X}_" +
            $"target_y={stimulusResult.Y}_" +
            $"target_z={stimulusResult.Z}_" +
            $"target_material={stimulusResult.MaterialClass}_" +
            $"companion_target_id={stimulusResult.CompanionTargetId}_" +
            $"initial_cell={stimulusResult.InitialCell}_" +
            $"set_water={stimulusResult.SetWater}_" +
            $"queued_water_changes={stimulusResult.QueuedWaterChangeCount}");
    }

    private TimberbornQaCommandResult ExecuteQaBurnDurationStimulus(string? commandText)
    {
        string? target = ParseBurnDurationTarget(commandText);
        if (target is null)
        {
            return TimberbornQaCommandResult.CreateFailure(
                QaBurnDurationStimulusCommand,
                "Command 'qa-burn-duration-stimulus' requires one target: low, medium, or high.",
                _stateProvider.GetState(),
                KnownCommands);
        }

        TimberbornQaBurnDurationStimulusResult stimulusResult =
            _burnDurationStimulus.QueueBurnDurationStimulus(target);
        TimberbornQaCommandState state = _stateProvider.GetState();

        return TimberbornQaCommandResult.CreateSuccess(
            QaBurnDurationStimulusCommand,
            state,
            KnownCommands,
            "queued_burn_duration_stimulus_" +
            $"target={stimulusResult.Target}_" +
            $"target_index={stimulusResult.CellIndex}_" +
            $"target_x={stimulusResult.X}_" +
            $"target_y={stimulusResult.Y}_" +
            $"target_z={stimulusResult.Z}_" +
            $"target_material={stimulusResult.MaterialClass}_" +
            $"companion_target_id={stimulusResult.CompanionTargetId}_" +
            $"initial_cell={stimulusResult.InitialCell}_" +
            $"initial_fuel={stimulusResult.InitialFuel}_" +
            $"set_heat={stimulusResult.SetHeat}_" +
            $"timeout_ticks={stimulusResult.TimeoutTicks}_" +
            $"sustained_heat_ticks={stimulusResult.SustainedHeatTicks}_" +
            $"queued_heat_changes={stimulusResult.QueuedHeatChangeCount}");
    }

    private TimberbornQaCommandResult ExecuteQaFirePreset(string? commandText)
    {
        string? presetName = ParseFirePresetName(commandText);
        if (presetName is null)
        {
            return TimberbornQaCommandResult.CreateFailure(
                QaFirePresetCommand,
                $"Command 'qa-fire-preset' requires one preset: {string.Join(", ", TimberbornFireSimParameterPresets.All.Select(static preset => preset.Name))}.",
                _stateProvider.GetState(),
                KnownCommands);
        }

        TimberbornQaFireSimParameterPresetResult presetResult =
            _fireSimParameterPresetSelector.SelectFireSimParameterPreset(presetName);
        TimberbornQaCommandState state = _stateProvider.GetState();

        return TimberbornQaCommandResult.CreateSuccess(
            QaFirePresetCommand,
            state,
            KnownCommands,
            "selected_fire_sim_preset_" +
            $"name={presetResult.Name}_" +
            $"ignition={presetResult.Parameters.FireIgnitionBaseHeat}_" +
            $"water_fuel_lock={presetResult.Parameters.FireWaterFuelLock}");
    }

    private static string NormalizeCommand(string commandText)
    {
        string command = commandText.Trim();

        if (string.IsNullOrEmpty(command))
        {
            return StatusCommand;
        }

        return command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
    }

    private static bool HasArguments(string commandText)
    {
        return commandText.Trim()
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Any();
    }

    private static bool CanAcceptArguments(string command, string commandText)
    {
        string? fieldTargetSelector = ParseFieldTargetSelector(commandText);
        return StringComparer.OrdinalIgnoreCase.Equals(command, QaDeltaStimulusCommand) &&
            fieldTargetSelector is not null ||
            StringComparer.OrdinalIgnoreCase.Equals(command, QaWaterSuppressionStimulusCommand) &&
            fieldTargetSelector is not null &&
            fieldTargetSelector != TimberbornQaFieldTargetSelectors.BeaverExposure &&
            !TimberbornQaFieldTargetSelectors.IsDirectConsequenceTargetSelector(fieldTargetSelector) ||
            StringComparer.OrdinalIgnoreCase.Equals(command, QaBurnDurationStimulusCommand) &&
            ParseBurnDurationTarget(commandText) is not null ||
            StringComparer.OrdinalIgnoreCase.Equals(command, QaFirePresetCommand) &&
            ParseFirePresetName(commandText) is not null;
    }

    private static string? ParseFieldTargetSelector(string? commandText)
    {
        string[] tokens = (commandText ?? string.Empty)
            .Trim()
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 1)
        {
            return TimberbornQaFieldTargetSelectors.Default;
        }

        if (tokens.Length != 2)
        {
            return null;
        }

        string selector = tokens[1].Trim().ToLowerInvariant();
        return TimberbornQaFieldTargetSelectors.IsKnown(selector) ? selector : null;
    }

    private static string? ParseBurnDurationTarget(string? commandText)
    {
        string[] tokens = (commandText ?? string.Empty)
            .Trim()
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length != 2)
        {
            return null;
        }

        string target = tokens[1].Trim().ToLowerInvariant();
        return TimberbornQaBurnDurationStimulusTargets.IsKnownTarget(target) ? target : null;
    }

    private static string? ParseFirePresetName(string? commandText)
    {
        string[] tokens = (commandText ?? string.Empty)
            .Trim()
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length != 2)
        {
            return null;
        }

        string presetName = tokens[1].Trim().ToLowerInvariant();
        return TimberbornFireSimParameterPresets.TryGet(presetName, out _)
            ? presetName
            : null;
    }

    private static bool IsSimulatorChangeCommand(string command)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(command, QaDeltaStimulusCommand) ||
            StringComparer.OrdinalIgnoreCase.Equals(command, QaBuildingBurnoutStimulusCommand) ||
            StringComparer.OrdinalIgnoreCase.Equals(command, QaWaterSuppressionStimulusCommand) ||
            StringComparer.OrdinalIgnoreCase.Equals(command, QaBurnDurationStimulusCommand) ||
            StringComparer.OrdinalIgnoreCase.Equals(command, QaFirePresetCommand);
    }

    internal static string FormatToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "placeholder"
            : value.Replace(' ', '_').Replace('"', '\'');
    }

    private static string FormatFloat(float? value)
    {
        return value?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }

    private static string FormatNumber(int? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }

    private static string FormatNumber(byte? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }
}

public interface ITimberbornQaCommandStateProvider
{
    TimberbornQaCommandState GetState();
}

public interface ITimberbornQaDeltaStimulus
{
    TimberbornQaDeltaStimulusResult QueueDeltaStimulus(string targetSelector);
}

public sealed record TimberbornQaDeltaStimulusResult(
    string TargetSelector,
    int CellIndex,
    int X,
    int Y,
    int Z,
    WildfireMaterialClass MaterialClass,
    uint CompanionTargetId,
    ushort InitialCell,
    byte SetHeat,
    int QueuedHeatChangeCount,
    string? BurnDamageTargetKey = null,
    string? BurnDamageSpecId = null,
    TimberbornBurnDamageTargetKind? BurnDamageTargetKind = null,
    int? BurnDamageRemainingCapacity = null,
    byte? BurnDamageProbeFuel = null,
    byte? BurnDamageSpendFuel = null,
    string? DirectTargetKind = null,
    string? DirectTargetStableId = null,
    int? DirectTargetScannedCellCount = null,
    string? TargetSource = null,
    int? RegisteredBurnDamageTargetCount = null,
    int? RegisteredCropBurnTargetCount = null,
    int? RegisteredCropBurnOwnedCellCount = null,
    int? SustainedHeatSetCell = null,
    int? SustainedHeatRequestedCycleCount = null,
    int? SustainedHeatCompletedCycleCount = null,
    int? SustainedHeatRemainingCycleCount = null,
    int? SustainedHeatQueuedCycleNumber = null,
    string? BeaverExposureTargetBeaverId = null,
    int? BeaverExposureTargetBeaverX = null,
    int? BeaverExposureTargetBeaverY = null,
    int? BeaverExposureTargetBeaverZ = null,
    int? BeaverExposureTargetCandidateCells = null,
    int? BeaverExposureTargetSampledBeavers = null,
    int? BeaverExposureTargetSkippedNoPositionApi = null,
    int? BeaverExposureTargetSkippedBoundedSampling = null);

public interface ITimberbornQaBuildingBurnoutStimulus
{
    TimberbornQaBuildingBurnoutStimulusResult QueueBuildingBurnoutStimulus();
}

public sealed record TimberbornQaBuildingBurnoutStimulusTarget(
    int CellIndex,
    int X,
    int Y,
    int Z,
    int ScannedCellCount);

public sealed record TimberbornQaBuildingBurnoutStimulusResult(
    int CellIndex,
    int X,
    int Y,
    int Z,
    int ScannedCellCount,
    byte SetHeat,
    byte SetFuel,
    int QueuedFieldChangeCount);

public interface ITimberbornQaWaterSuppressionStimulus
{
    TimberbornQaWaterSuppressionStimulusResult QueueWaterSuppressionStimulus(string targetSelector);
}

public sealed record TimberbornQaWaterSuppressionStimulusResult(
    string TargetSelector,
    int CellIndex,
    int X,
    int Y,
    int Z,
    WildfireMaterialClass MaterialClass,
    uint CompanionTargetId,
    ushort InitialCell,
    byte SetWater,
    int QueuedWaterChangeCount);

public interface ITimberbornQaBurnDurationStimulus
{
    TimberbornQaBurnDurationStimulusResult QueueBurnDurationStimulus(string target);
}

public interface ITimberbornQaFireSimParameterPresetSelector
{
    TimberbornQaFireSimParameterPresetResult SelectFireSimParameterPreset(string presetName);
}

public sealed record TimberbornQaFireSimParameterPresetResult(string Name, FireSimParameters Parameters);

public sealed record TimberbornQaBurnDurationStimulusResult(
    string Target,
    int CellIndex,
    int X,
    int Y,
    int Z,
    WildfireMaterialClass MaterialClass,
    uint CompanionTargetId,
    ushort InitialCell,
    byte InitialFuel,
    byte SetHeat,
    uint TimeoutTicks,
    int SustainedHeatTicks,
    int QueuedHeatChangeCount);

public sealed record TimberbornQaBurnDurationProofState(
    string Target,
    int CellIndex,
    int X,
    int Y,
    int Z,
    byte InitialFuel,
    uint QueuedTick,
    uint TimeoutTicks,
    int SustainedHeatTicks = 0,
    int SustainedHeatAppliedTicks = 0,
    bool SustainedHeatComplete = false,
    uint? BurnStartTick = null,
    uint? DepletionTick = null,
    uint? ElapsedBurnTicks = null,
    bool TimedOut = false,
    string Status = "queued")
{
    public static readonly TimberbornQaBurnDurationProofState Placeholder = new(
        "placeholder",
        -1,
        -1,
        -1,
        -1,
        0,
        0,
        0,
        Status: "placeholder");
}

public static class TimberbornQaBurnDurationStimulusTargets
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public const uint DefaultTimeoutTicks = 64;

    private static readonly IReadOnlyDictionary<string, byte> InitialFuelByTarget =
        new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            [Low] = 4,
            [Medium] = 9,
            [High] = 15,
        };

    public static bool IsKnownTarget(string target)
    {
        return InitialFuelByTarget.ContainsKey(target);
    }

    public static byte InitialFuel(string target)
    {
        return InitialFuelByTarget.TryGetValue(target, out byte fuel)
            ? fuel
            : throw new ArgumentException("Burn-duration QA target must be low, medium, or high.", nameof(target));
    }

    public static TimberbornQaBurnDurationStimulusTarget SelectTarget(
        FireGrid grid,
        IReadOnlyList<TimberbornImportedFieldTarget> importedTargets,
        string target)
    {
        if (!IsKnownTarget(target))
        {
            throw new ArgumentException("Burn-duration QA target must be low, medium, or high.", nameof(target));
        }

        if (importedTargets is null)
        {
            throw new ArgumentNullException(nameof(importedTargets));
        }

        TimberbornImportedFieldTarget? selectedTarget = importedTargets
            .Where(static candidate => PackedCell.Terrain(candidate.InitialCell) == 1)
            .Where(static candidate => PackedCell.Flammability(candidate.InitialCell) > 0)
            .Where(static candidate => PackedCell.Fuel(candidate.InitialCell) > 0)
            .Where(candidate => IsTargetFuelBand(target, PackedCell.Fuel(candidate.InitialCell)))
            .OrderBy(static candidate => TargetPriority(candidate.MaterialClass))
            .ThenBy(static candidate => candidate.CellIndex)
            .Select(static candidate => (TimberbornImportedFieldTarget?)candidate)
            .FirstOrDefault();

        if (!selectedTarget.HasValue)
        {
            throw new InvalidOperationException(
                $"No imported burn-duration field target was found for fuel band '{target}'.");
        }
        TimberbornImportedFieldTarget selectedTargetValue = selectedTarget.Value;

        return new TimberbornQaBurnDurationStimulusTarget(
            target.ToLowerInvariant(),
            selectedTargetValue.CellIndex,
            selectedTargetValue.X,
            selectedTargetValue.Y,
            selectedTargetValue.Z,
            selectedTargetValue.MaterialClass,
            selectedTargetValue.CompanionTargetId,
            selectedTargetValue.InitialCell,
            (byte)PackedCell.Fuel(selectedTargetValue.InitialCell));
    }

    private static bool IsTargetFuelBand(string target, int fuel)
    {
        return target.ToLowerInvariant() switch
        {
            Low => fuel is > 0 and <= 4,
            Medium => fuel is >= 5 and <= 10,
            High => fuel >= 11,
            _ => false,
        };
    }

    private static int TargetPriority(WildfireMaterialClass materialClass)
    {
        return materialClass switch
        {
            WildfireMaterialClass.Tree => 0,
            WildfireMaterialClass.Vegetation => 1,
            WildfireMaterialClass.Crop => 2,
            WildfireMaterialClass.Storage => 3,
            WildfireMaterialClass.Building => 4,
            _ => 5,
        };
    }
}

public static class TimberbornQaFieldTargetSelectors
{
    public const string Default = "burnable";
    public const string Tree = "tree";
    public const string SelectedTree = "selected-tree";
    public const string CenterTree = "center-tree";
    public const string BeaverExposure = "beaver-exposure";
    public const string Vegetation = "vegetation";
    public const string Crop = "crop";
    public const string Storage = "storage";
    public const string Building = "building";
    public const string Infrastructure = "infrastructure";
    public const string Dynamite = "dynamite";
    public const string Detonator = "detonator";
    public const string Tunnel = "tunnel";
    public const string PathInfrastructure = "path-infrastructure";
    public const string PowerInfrastructure = "power-infrastructure";
    public const string WaterInfrastructure = "water-infrastructure";

    private static readonly HashSet<string> KnownSelectors = new(StringComparer.OrdinalIgnoreCase)
    {
        Default,
        Tree,
        SelectedTree,
        CenterTree,
        BeaverExposure,
        Vegetation,
        Crop,
        Storage,
        Building,
        Infrastructure,
        Dynamite,
        Detonator,
        Tunnel,
        PathInfrastructure,
        PowerInfrastructure,
        WaterInfrastructure,
    };

    public static bool IsKnown(string selector)
    {
        return KnownSelectors.Contains(selector);
    }

    public static string Normalize(string selector)
    {
        string normalized = selector.Trim().ToLowerInvariant();
        return IsKnown(normalized)
            ? normalized
            : throw new ArgumentException(
                $"QA field target selector must be one of: {string.Join(", ", KnownSelectors.OrderBy(static value => value))}.",
                nameof(selector));
    }

    public static bool Matches(WildfireMaterialClass materialClass, string selector)
    {
        return Normalize(selector) switch
        {
            Default => true,
            Tree => materialClass == WildfireMaterialClass.Tree,
            SelectedTree => materialClass == WildfireMaterialClass.Tree,
            CenterTree => materialClass == WildfireMaterialClass.Tree,
            BeaverExposure => false,
            Vegetation => materialClass == WildfireMaterialClass.Vegetation,
            Crop => materialClass == WildfireMaterialClass.Crop,
            Storage => materialClass == WildfireMaterialClass.Storage,
            Building => materialClass == WildfireMaterialClass.Building,
            Infrastructure => materialClass == WildfireMaterialClass.Infrastructure,
            Dynamite => false,
            Detonator => false,
            Tunnel => false,
            PathInfrastructure => materialClass == WildfireMaterialClass.Infrastructure,
            PowerInfrastructure => materialClass == WildfireMaterialClass.Infrastructure,
            WaterInfrastructure => materialClass == WildfireMaterialClass.Infrastructure,
            _ => false,
        };
    }

    public static bool IsBurnDamageProbeSelector(string selector)
    {
        return Normalize(selector) is Building or Storage or Infrastructure or PathInfrastructure or
            PowerInfrastructure or WaterInfrastructure;
    }

    public static bool IsDirectConsequenceTargetSelector(string selector)
    {
        return Normalize(selector) is Dynamite or Detonator or Tunnel;
    }
}

public sealed record TimberbornQaBurnDurationStimulusTarget(
    string Target,
    int CellIndex,
    int X,
    int Y,
    int Z,
    WildfireMaterialClass MaterialClass,
    uint CompanionTargetId,
    ushort InitialCell,
    byte InitialFuel);

public interface ITimberbornQaBuildingBurnoutStimulusTargetProvider
{
    TimberbornQaBuildingBurnoutStimulusTarget FindTarget(FireGrid grid);
}

public sealed class NullTimberbornQaDeltaStimulus : ITimberbornQaDeltaStimulus
{
    public static readonly NullTimberbornQaDeltaStimulus Instance = new();

    private NullTimberbornQaDeltaStimulus()
    {
    }

    public TimberbornQaDeltaStimulusResult QueueDeltaStimulus(string targetSelector)
    {
        throw new InvalidOperationException("QA delta stimulus is unavailable until the Timberborn fire runtime is initialized.");
    }
}

public sealed class NullTimberbornQaBuildingBurnoutStimulus : ITimberbornQaBuildingBurnoutStimulus
{
    public static readonly NullTimberbornQaBuildingBurnoutStimulus Instance = new();

    private NullTimberbornQaBuildingBurnoutStimulus()
    {
    }

    public TimberbornQaBuildingBurnoutStimulusResult QueueBuildingBurnoutStimulus()
    {
        throw new InvalidOperationException(
            "QA building burnout stimulus is unavailable until the Timberborn fire runtime is initialized.");
    }
}

public sealed class NullTimberbornQaWaterSuppressionStimulus : ITimberbornQaWaterSuppressionStimulus
{
    public static readonly NullTimberbornQaWaterSuppressionStimulus Instance = new();

    private NullTimberbornQaWaterSuppressionStimulus()
    {
    }

    public TimberbornQaWaterSuppressionStimulusResult QueueWaterSuppressionStimulus(string targetSelector)
    {
        throw new InvalidOperationException(
            "QA water suppression stimulus is unavailable until the Timberborn fire runtime is initialized.");
    }
}

public sealed class NullTimberbornQaBurnDurationStimulus : ITimberbornQaBurnDurationStimulus
{
    public static readonly NullTimberbornQaBurnDurationStimulus Instance = new();

    private NullTimberbornQaBurnDurationStimulus()
    {
    }

    public TimberbornQaBurnDurationStimulusResult QueueBurnDurationStimulus(string target)
    {
        throw new InvalidOperationException(
            "QA burn-duration stimulus is unavailable until the Timberborn fire runtime is initialized.");
    }
}

public sealed class NullTimberbornQaFireSimParameterPresetSelector : ITimberbornQaFireSimParameterPresetSelector
{
    public static readonly NullTimberbornQaFireSimParameterPresetSelector Instance = new();

    private NullTimberbornQaFireSimParameterPresetSelector()
    {
    }

    public TimberbornQaFireSimParameterPresetResult SelectFireSimParameterPreset(string presetName)
    {
        throw new InvalidOperationException("QA fire sim parameter preset selection is unavailable.");
    }
}

public sealed class TimberbornQaCommandStateProvider : ITimberbornQaCommandStateProvider
{
    public static readonly TimberbornQaCommandStateProvider Placeholder = new(TimberbornQaCommandState.Placeholder);

    private readonly TimberbornQaCommandState _state;

    public TimberbornQaCommandStateProvider(TimberbornQaCommandState state)
    {
        _state = state;
    }

    public TimberbornQaCommandState GetState()
    {
        return _state;
    }
}

public sealed record TimberbornQaCommandState(
    bool IsSimulatorIntegrated,
    bool IsGameContextRuntimeLoaded = false,
    bool WildfireEnabled = true,
    int? Width = null,
    int? Height = null,
    int? Depth = null,
    uint? TickCount = null,
    int? QueuedChangeCount = null,
    int? LastDeltaCount = null,
    int? LastDeltaConsumerChangedCellCount = null,
    int? LastDeltaConsumerDebugVisualUpdatedCellCount = null,
    int? LastDeltaConsumerDebugVisualCellCount = null,
    int? LastDeltaConsumerStartedBurningCount = null,
    int? LastDeltaConsumerFuelDepletedCount = null,
    int? LastDeltaConsumerWaterChangedCount = null,
    uint? LastPositiveWaterChangedTick = null,
    int? LastPositiveWaterChangedCount = null,
    int? LastDeltaConsumerVisualEffectEventCount = null,
    int? LastDeltaConsumerVisualEffectFailureCount = null,
    int? LastDeltaConsumerGameplayConsequenceCount = null,
    int? LastDeltaConsumerBuildingBurnoutConsideredDeltaCount = null,
    int? LastDeltaConsumerBuildingBurnoutMatchedCellCount = null,
    int? LastDeltaConsumerBuildingBurnoutAppliedConsequenceCount = null,
    int? BurnDamageRegisteredTargetCount = null,
    int? BurnDamageRegisteredOwnedCellCount = null,
    int? BurnDamageRegisteredUnknownSpecCount = null,
    int? BurnDamageRegisteredMissingResourceCount = null,
    int? BurnDamageRegisteredTotalCapacity = null,
    int? BurnDamageRegisteredZeroCapacityTargetCount = null,
    int? BurnDamageRegisteredCropBurnTargetCount = null,
    int? BurnDamageRegisteredCropBurnOwnedCellCount = null,
    int? BurnDamageRegisteredTreeBurnTargetCount = null,
    int? BurnDamageRegisteredTreeBurnOwnedCellCount = null,
    int? QaDeltaStimulusTargetCellIndex = null,
    int? QaDeltaStimulusTargetX = null,
    int? QaDeltaStimulusTargetY = null,
    int? QaDeltaStimulusTargetZ = null,
    string? QaDeltaStimulusTargetSource = null,
    int? QaDeltaStimulusSustainedHeatSetCell = null,
    int? QaDeltaStimulusSustainedHeatRequestedCycleCount = null,
    int? QaDeltaStimulusSustainedHeatCompletedCycleCount = null,
    int? QaDeltaStimulusSustainedHeatRemainingCycleCount = null,
    int? QaDeltaStimulusSustainedHeatQueuedCycleNumber = null,
    uint? QaDeltaStimulusSustainedHeatLastCompletedTick = null,
    bool QaDeltaStimulusSustainedHeatActive = false,
    string? SelectedCropTargetSelectionState = null,
    string? SelectedCropTargetObjectType = null,
    string? SelectedCropTargetObjectName = null,
    string? SelectedCropTargetBlockObjectName = null,
    int? SelectedCropTargetComponentCount = null,
    string? SelectedCropTargetComponentTypes = null,
    int? SelectedCropTargetOccupiedCellCount = null,
    int? SelectedCropTargetOccupiedInGridCellCount = null,
    string? SelectedCropTargetYieldDebug = null,
    string? SelectedCropTargetFailureReason = null,
    int? LastDeltaConsumerCropBurnConsideredTargetCount = null,
    int? LastDeltaConsumerCropBurnBurnableTargetCount = null,
    int? LastDeltaConsumerCropBurnYieldLost = null,
    int? LastDeltaConsumerCropBurnKilledCropCount = null,
    int? LastDeltaConsumerCropBurnVisualStateUpdateCount = null,
    int? LastDeltaConsumerCropBurnDuplicateCellSuppressedCount = null,
    int? LastDeltaConsumerCropBurnUnmappedTargetCount = null,
    int? LastDeltaConsumerCropBurnUnknownHarvestResourceCount = null,
    int? LastDeltaConsumerCropBurnNonBurnableTargetCount = null,
    int? LastDeltaConsumerCropBurnSkippedUnsafeApiCount = null,
    int? LastDeltaConsumerTreeBurnConsideredTargetCount = null,
    int? LastDeltaConsumerTreeBurnBurnableTargetCount = null,
    int? LastDeltaConsumerTreeBurnYieldLost = null,
    int? LastDeltaConsumerTreeBurnKilledTreeCount = null,
    int? LastDeltaConsumerTreeBurnVisualStateUpdateCount = null,
    int? LastDeltaConsumerTreeBurnDuplicateCellSuppressedCount = null,
    int? LastDeltaConsumerTreeBurnUnmappedTargetCount = null,
    int? LastDeltaConsumerTreeBurnUnknownCuttableResourceCount = null,
    int? LastDeltaConsumerTreeBurnNonBurnableTargetCount = null,
    int? LastDeltaConsumerTreeBurnSkippedUnsafeApiCount = null,
    int? LastDeltaConsumerStructureBurnDamageRollbackConsideredDeltaCount = null,
    int? LastDeltaConsumerStructureBurnDamageRollbackMatchedStructureCellCount = null,
    int? LastDeltaConsumerStructureBurnDamageRollbackDuplicateStructureTargetSuppressedCount = null,
    int? LastDeltaConsumerStructureBurnDamageRollbackZeroBurnableCapacityTargetCount = null,
    int? LastDeltaConsumerStructureBurnDamageRollbackMaterialValueLost = null,
    int? LastDeltaConsumerStructureBurnDamageRollbackClosedStructureCount = null,
    int? LastDeltaConsumerStructureBurnDamageRollbackRepairBlockedCount = null,
    int? LastDeltaConsumerStructureBurnDamageRollbackRepairEligibleCount = null,
    int? LastDeltaConsumerStructureBurnDamageRollbackScorchedStageCount = null,
    int? LastDeltaConsumerStructureBurnDamageRollbackPartialConstructionStageCount = null,
    int? LastDeltaConsumerStructureBurnDamageRollbackUnfinishedStageCount = null,
    int? LastDeltaConsumerStructureBurnDamageRollbackVisualRollbackAppliedCount = null,
    int? LastDeltaConsumerStructureBurnDamageRollbackSkippedNoSafeApiCount = null,
    int? LastDeltaConsumerStructureBurnDamageRollbackTotalDamageApplied = null,
    uint? LastPositiveBuildingBurnoutAppliedTick = null,
    int? LastPositiveBuildingBurnoutAppliedCount = null,
    int? LastDeltaConsumerStoredGoodBurnConsideredDeltaCount = null,
    int? LastDeltaConsumerStoredGoodBurnMatchedStorageCellCount = null,
    int? LastDeltaConsumerStoredGoodBurnDuplicateStorageTargetSuppressedCount = null,
    int? LastDeltaConsumerStoredGoodBurnableStackCount = null,
    int? LastDeltaConsumerStoredGoodBurnDestroyedItemCount = null,
    int? LastDeltaConsumerStoredGoodBurnHazardousGoodCount = null,
    int? LastDeltaConsumerStoredGoodBurnSkippedNoInventoryApiCount = null,
    int? LastDeltaConsumerStoredGoodBurnSkippedUnknownResourceCount = null,
    int? LastDeltaConsumerStoredGoodBurnSkippedNonBurnableItemCount = null,
    int? LastDeltaConsumerExplosiveInfrastructureConsideredDeltaCount = null,
    int? LastDeltaConsumerExplosiveInfrastructureMatchedTargetCellCount = null,
    int? LastDeltaConsumerExplosiveInfrastructureDuplicateTargetSuppressedCount = null,
    int? LastDeltaConsumerExplosiveInfrastructureArmedTargetCount = null,
    int? LastDeltaConsumerExplosiveInfrastructureTriggeredTargetCount = null,
    int? LastDeltaConsumerExplosiveInfrastructureNativeTriggeredTargetCount = null,
    int? LastDeltaConsumerExplosiveInfrastructureHeatPulseCellCount = null,
    int? LastDeltaConsumerExplosiveInfrastructureSkippedSettingDisabledCount = null,
    int? LastDeltaConsumerExplosiveInfrastructureSkippedNoSafeApiCount = null,
    int? LastDeltaConsumerExplosiveInfrastructureSkippedAlreadyTriggeredCount = null,
    int? LastDeltaConsumerExplosiveInfrastructureLastTriggeredDepth = null,
    int? LastDeltaConsumerDetonatorFireSafetyConsideredDeltaCount = null,
    int? LastDeltaConsumerDetonatorFireSafetyMatchedTargetCellCount = null,
    int? LastDeltaConsumerDetonatorFireSafetyDuplicateTargetSuppressedCount = null,
    int? LastDeltaConsumerDetonatorFireSafetyDisabledTargetCount = null,
    int? LastDeltaConsumerDetonatorFireSafetyArmedTargetCount = null,
    int? LastDeltaConsumerDetonatorFireSafetySkippedSettingDisabledCount = null,
    int? LastDeltaConsumerDetonatorFireSafetySkippedNoSafeApiCount = null,
    int? LastDeltaConsumerDetonatorFireSafetyRecoverabilityPreservedCount = null,
    int? LastDeltaConsumerDetonatorFireSafetyRecoverabilityUnknownCount = null,
    int? LastDeltaConsumerTunnelFireConsideredDeltaCount = null,
    int? LastDeltaConsumerTunnelFireMatchedTargetCellCount = null,
    int? LastDeltaConsumerTunnelFireDuplicateTargetSuppressedCount = null,
    int? LastDeltaConsumerTunnelFireUnstableTargetCount = null,
    int? LastDeltaConsumerTunnelFireNativeExplodeAttemptedCount = null,
    int? LastDeltaConsumerTunnelFireNativeExplodeAppliedCount = null,
    int? LastDeltaConsumerTunnelFireDestructionDeferredCount = null,
    int? LastDeltaConsumerTunnelFireSkippedSettingDisabledCount = null,
    int? LastDeltaConsumerTunnelFireSkippedNoSafeApiCount = null,
    int? LastDeltaConsumerTunnelFireRecoverabilityPreservedCount = null,
    int? LastDeltaConsumerTunnelFireRecoverabilityUnknownCount = null,
    int? LastDeltaConsumerPathInfrastructureConsideredDeltaCount = null,
    int? LastDeltaConsumerPathInfrastructureMatchedTargetCellCount = null,
    int? LastDeltaConsumerPathInfrastructureDuplicateTargetSuppressedCount = null,
    int? LastDeltaConsumerPathInfrastructureZeroCostTargetCount = null,
    int? LastDeltaConsumerPathInfrastructureDamagedTargetCount = null,
    int? LastDeltaConsumerPathInfrastructureBlockedTargetCount = null,
    int? LastDeltaConsumerPathInfrastructureSkippedNoSafeApiCount = null,
    int? LastDeltaConsumerPathInfrastructureRepairEligibleTargetCount = null,
    int? LastDeltaConsumerPathInfrastructureTotalDamageApplied = null,
    int? LastDeltaConsumerPowerInfrastructureConsideredDeltaCount = null,
    int? LastDeltaConsumerPowerInfrastructureMatchedTargetCellCount = null,
    int? LastDeltaConsumerPowerInfrastructureDuplicateTargetSuppressedCount = null,
    int? LastDeltaConsumerPowerInfrastructureMetalOnlyNoOpTargetCount = null,
    int? LastDeltaConsumerPowerInfrastructureDamagedTargetCount = null,
    int? LastDeltaConsumerPowerInfrastructureDisabledOrDisconnectedTargetCount = null,
    int? LastDeltaConsumerPowerInfrastructureSkippedNoSafeApiCount = null,
    int? LastDeltaConsumerPowerInfrastructureRepairEligibleTargetCount = null,
    int? LastDeltaConsumerPowerInfrastructureTotalDamageApplied = null,
    int? LastDeltaConsumerWaterInfrastructureConsideredDeltaCount = null,
    int? LastDeltaConsumerWaterInfrastructureMatchedTargetCellCount = null,
    int? LastDeltaConsumerWaterInfrastructureDuplicateTargetSuppressedCount = null,
    int? LastDeltaConsumerWaterInfrastructureInertMaterialNoOpTargetCount = null,
    int? LastDeltaConsumerWaterInfrastructureDifficultToBurnNoOpTargetCount = null,
    int? LastDeltaConsumerWaterInfrastructureBurnableMaterialValue = null,
    int? LastDeltaConsumerWaterInfrastructureDamagedTargetCount = null,
    int? LastDeltaConsumerWaterInfrastructureWaterStateMutationAttemptCount = null,
    int? LastDeltaConsumerWaterInfrastructureSkippedNoSafeApiCount = null,
    int? LastDeltaConsumerWaterInfrastructureRepairEligibleTargetCount = null,
    int? LastDeltaConsumerWaterInfrastructureTotalDamageApplied = null,
    int? LastDeltaConsumerAlertCount = null,
    uint? LastPlayerFireAlertTick = null,
    int? LastPlayerFireAlertStartedFireCount = null,
    int? LastPlayerFireAlertFuelSpentCount = null,
    int? LastPlayerFireAlertMaxHeat = null,
    int? PlayerFireAlertNotificationCount = null,
    int? PlayerFireAlertPresentationFailureCount = null,
    bool PlayerFireAlertNotificationSent = false,
    string? LastPlayerFireAlertMessage = null,
    bool VisualFieldSurfaceBound = false,
    int? VisualFieldSurfaceCellCount = null,
    uint? VisualFieldSurfaceLastUpdatedTick = null,
    bool GpuFieldRendererEnabled = false,
    bool GpuFieldRendererMaterialReady = false,
    bool GpuFieldRendererSurfaceBound = false,
    int? GpuFieldRendererVisibleRegionCount = null,
    int? GpuFieldRendererUpdatedRegionCount = null,
    int? GpuFieldRendererLastNonZeroUpdatedRegionCount = null,
    uint? GpuFieldRendererLastNonZeroUpdatedRegionTick = null,
    int? GpuFieldRendererMaxUpdatedRegionCount = null,
    int? GpuFieldRendererDroppedRegionCount = null,
    int? GpuFieldRendererInvisibleRegionCount = null,
    int? GpuFieldRendererMaterialFailureCount = null,
    uint? GpuFieldRendererLastUpdatedTick = null,
    bool BeaverFieldExposureAvailable = false,
    int? BeaverFieldExposureSampledBeavers = null,
    int? BeaverFieldExposureExposedBeavers = null,
    int? BeaverFieldExposureRespiratoryCells = null,
    int? BeaverFieldExposureBurnCells = null,
    int? BeaverFieldExposureContaminatedSmokeCells = null,
    int? BeaverFieldExposureToxicCells = null,
    int? BeaverFieldExposureToxicSteamCells = null,
    int? BeaverFieldExposureTaintedAftermathCells = null,
    int? BeaverFieldExposureSkippedNoPositionApi = null,
    int? BeaverFieldExposureSkippedBoundedSampling = null,
    string? BeaverFieldExposureUnavailableReason = null,
    int? ActivePooledFireEffectCount = null,
    int? UpdatedVisualRegionCount = null,
    int? LastNonZeroUpdatedVisualRegionCount = null,
    uint? LastNonZeroUpdatedVisualRegionTick = null,
    int? MaxPooledFireEffectCount = null,
    int? MaxUpdatedVisualRegionCount = null,
    int? PooledFireEffectPresentationFailureCount = null,
    bool PooledFireEffectsVisibleEnabled = false,
    bool PooledFireEffectsNativePrefabResolved = false,
    string? PooledFireEffectsNativePrefabName = null,
    string? BurnDurationProofTarget = null,
    int? BurnDurationProofTargetIndex = null,
    int? BurnDurationProofTargetX = null,
    int? BurnDurationProofTargetY = null,
    int? BurnDurationProofTargetZ = null,
    int? BurnDurationProofInitialFuel = null,
    uint? BurnDurationProofQueuedTick = null,
    uint? BurnDurationProofBurnStartTick = null,
    uint? BurnDurationProofDepletionTick = null,
    uint? BurnDurationProofElapsedBurnTicks = null,
    uint? BurnDurationProofTimeoutTicks = null,
    int? BurnDurationProofSustainedHeatTicks = null,
    int? BurnDurationProofSustainedHeatAppliedTicks = null,
    bool BurnDurationProofSustainedHeatComplete = false,
    bool BurnDurationProofTimedOut = false,
    string? BurnDurationProofStatus = null,
    string CompatibilityProbeStatus = "placeholder",
    bool CompatibilityProbeDegraded = false,
    int? CompatibilityProbeRequiredPassed = null,
    int? CompatibilityProbeRequiredTotal = null,
    int? CompatibilityProbeOptionalPassed = null,
    int? CompatibilityProbeOptionalTotal = null,
    string CompatibilityProbeDegradedFeatures = "placeholder",
    string FireSimPresetName = "default",
    uint? FireSimPresetIgnitionBaseHeat = null,
    uint? FireSimPresetWaterFuelLock = null,
    uint? FireSimPresetFuelHeatWeight = null,
    uint? FireSimPresetFuelBurnDownNumerator = null,
    uint? FireSimPresetFuelBurnDownDenominator = null,
    int? WorldImportTotalSources = null,
    int? WorldImportTerrainSources = null,
    int? WorldImportVegetationSources = null,
    int? WorldImportTreeSources = null,
    int? WorldImportCropSources = null,
    int? WorldImportBuildingSources = null,
    int? WorldImportStorageSources = null,
    int? WorldImportInfrastructureSources = null,
    int? WorldImportWaterSources = null,
    int? WorldImportBadwaterSources = null,
    int? WorldImportResolvedEmptyCells = null,
    int? WorldImportResolvedTerrainCells = null,
    int? WorldImportResolvedVegetationCells = null,
    int? WorldImportResolvedTreeCells = null,
    int? WorldImportResolvedCropCells = null,
    int? WorldImportResolvedBuildingCells = null,
    int? WorldImportResolvedStorageCells = null,
    int? WorldImportResolvedInfrastructureCells = null,
    int? WorldImportResolvedWaterCells = null,
    int? WorldImportResolvedBadwaterCells = null,
    int? WorldImportSafeUnavailableCount = null)
{
    public static readonly TimberbornQaCommandState Placeholder = new(IsSimulatorIntegrated: false);

    public bool IsLoadedGameReady =>
        WildfireEnabled &&
        IsGameContextRuntimeLoaded &&
        IsSimulatorIntegrated &&
        !StringComparer.OrdinalIgnoreCase.Equals(CompatibilityProbeStatus, "failed") &&
        Width.HasValue &&
        Height.HasValue &&
        Depth.HasValue &&
        TickCount.HasValue;
}

public sealed record TimberbornQaCommandResult(
    string Command,
    bool Success,
    string Status,
    TimberbornQaCommandState State,
    IReadOnlyList<string> KnownCommands,
    string Message)
{
    public string ResultToken =>
        "wildfire_command_result " +
        $"command={TimberbornQaCommandBridge.FormatToken(Command)} " +
        $"success={Success.ToString().ToLowerInvariant()} " +
        $"status={Status} " +
        "bridge_alive=true " +
        $"wildfire_enabled={State.WildfireEnabled.ToString().ToLowerInvariant()} " +
        $"runtime_loaded={State.IsGameContextRuntimeLoaded.ToString().ToLowerInvariant()} " +
        $"loaded_game_ready={State.IsLoadedGameReady.ToString().ToLowerInvariant()} " +
        $"simulator_integrated={State.IsSimulatorIntegrated.ToString().ToLowerInvariant()} " +
        $"width={FormatNumber(State.Width)} " +
        $"height={FormatNumber(State.Height)} " +
        $"depth={FormatNumber(State.Depth)} " +
        $"tick_count={FormatNumber(State.TickCount)} " +
        $"queued_changes={FormatNumber(State.QueuedChangeCount)} " +
        $"last_delta_count={FormatNumber(State.LastDeltaCount)} " +
        $"last_delta_consumer_changed_cells={FormatNumber(State.LastDeltaConsumerChangedCellCount)} " +
        $"last_delta_consumer_debug_visual_updated_cells={FormatNumber(State.LastDeltaConsumerDebugVisualUpdatedCellCount)} " +
        $"last_delta_consumer_debug_visual_cells={FormatNumber(State.LastDeltaConsumerDebugVisualCellCount)} " +
        $"last_delta_consumer_started_burning={FormatNumber(State.LastDeltaConsumerStartedBurningCount)} " +
        $"last_delta_consumer_fuel_depleted={FormatNumber(State.LastDeltaConsumerFuelDepletedCount)} " +
        $"last_delta_consumer_water_changed={FormatNumber(State.LastDeltaConsumerWaterChangedCount)} " +
        $"last_positive_water_changed_tick={FormatNumber(State.LastPositiveWaterChangedTick)} " +
        $"last_positive_water_changed_count={FormatNumber(State.LastPositiveWaterChangedCount)} " +
        $"last_delta_consumer_visual_effect_events={FormatNumber(State.LastDeltaConsumerVisualEffectEventCount)} " +
        $"last_delta_consumer_visual_effect_failures={FormatNumber(State.LastDeltaConsumerVisualEffectFailureCount)} " +
        $"last_delta_consumer_gameplay_consequences={FormatNumber(State.LastDeltaConsumerGameplayConsequenceCount)} " +
        $"last_delta_consumer_building_burnout_considered_deltas={FormatNumber(State.LastDeltaConsumerBuildingBurnoutConsideredDeltaCount)} " +
        $"last_delta_consumer_building_burnout_matched_cells={FormatNumber(State.LastDeltaConsumerBuildingBurnoutMatchedCellCount)} " +
        $"last_delta_consumer_building_burnout_applied_consequences={FormatNumber(State.LastDeltaConsumerBuildingBurnoutAppliedConsequenceCount)} " +
        $"burn_damage_registered_targets={FormatNumber(State.BurnDamageRegisteredTargetCount)} " +
        $"burn_damage_registered_owned_cells={FormatNumber(State.BurnDamageRegisteredOwnedCellCount)} " +
        $"burn_damage_registered_unknown_specs={FormatNumber(State.BurnDamageRegisteredUnknownSpecCount)} " +
        $"burn_damage_registered_missing_resources={FormatNumber(State.BurnDamageRegisteredMissingResourceCount)} " +
        $"burn_damage_registered_total_capacity={FormatNumber(State.BurnDamageRegisteredTotalCapacity)} " +
        $"burn_damage_registered_zero_capacity_targets={FormatNumber(State.BurnDamageRegisteredZeroCapacityTargetCount)} " +
        $"burn_damage_registered_crop_burn_targets={FormatNumber(State.BurnDamageRegisteredCropBurnTargetCount)} " +
        $"burn_damage_registered_crop_burn_owned_cells={FormatNumber(State.BurnDamageRegisteredCropBurnOwnedCellCount)} " +
        $"burn_damage_registered_tree_burn_targets={FormatNumber(State.BurnDamageRegisteredTreeBurnTargetCount)} " +
        $"burn_damage_registered_tree_burn_owned_cells={FormatNumber(State.BurnDamageRegisteredTreeBurnOwnedCellCount)} " +
        $"qa_delta_stimulus_target_index={FormatNumber(State.QaDeltaStimulusTargetCellIndex)} " +
        $"qa_delta_stimulus_target_x={FormatNumber(State.QaDeltaStimulusTargetX)} " +
        $"qa_delta_stimulus_target_y={FormatNumber(State.QaDeltaStimulusTargetY)} " +
        $"qa_delta_stimulus_target_z={FormatNumber(State.QaDeltaStimulusTargetZ)} " +
        $"qa_delta_stimulus_target_source={TimberbornQaCommandBridge.FormatToken(State.QaDeltaStimulusTargetSource)} " +
        $"qa_delta_stimulus_sustained_heat_set_cell={FormatNumber(State.QaDeltaStimulusSustainedHeatSetCell)} " +
        $"qa_delta_stimulus_sustained_heat_requested_cycles={FormatNumber(State.QaDeltaStimulusSustainedHeatRequestedCycleCount)} " +
        $"qa_delta_stimulus_sustained_heat_completed_cycles={FormatNumber(State.QaDeltaStimulusSustainedHeatCompletedCycleCount)} " +
        $"qa_delta_stimulus_sustained_heat_remaining_cycles={FormatNumber(State.QaDeltaStimulusSustainedHeatRemainingCycleCount)} " +
        $"qa_delta_stimulus_sustained_heat_queued_cycle={FormatNumber(State.QaDeltaStimulusSustainedHeatQueuedCycleNumber)} " +
        $"qa_delta_stimulus_sustained_heat_last_completed_tick={FormatNumber(State.QaDeltaStimulusSustainedHeatLastCompletedTick)} " +
        $"qa_delta_stimulus_sustained_heat_active={State.QaDeltaStimulusSustainedHeatActive.ToString().ToLowerInvariant()} " +
        $"selected_crop_target_state={TimberbornQaCommandBridge.FormatToken(State.SelectedCropTargetSelectionState)} " +
        $"selected_crop_target_object_type={TimberbornQaCommandBridge.FormatToken(State.SelectedCropTargetObjectType)} " +
        $"selected_crop_target_object_name={TimberbornQaCommandBridge.FormatToken(State.SelectedCropTargetObjectName)} " +
        $"selected_crop_target_block_object={TimberbornQaCommandBridge.FormatToken(State.SelectedCropTargetBlockObjectName)} " +
        $"selected_crop_target_component_count={FormatNumber(State.SelectedCropTargetComponentCount)} " +
        $"selected_crop_target_components={TimberbornQaCommandBridge.FormatToken(State.SelectedCropTargetComponentTypes)} " +
        $"selected_crop_target_occupied_cells={FormatNumber(State.SelectedCropTargetOccupiedCellCount)} " +
        $"selected_crop_target_occupied_in_grid_cells={FormatNumber(State.SelectedCropTargetOccupiedInGridCellCount)} " +
        $"selected_crop_target_yield_debug={TimberbornQaCommandBridge.FormatToken(State.SelectedCropTargetYieldDebug)} " +
        $"selected_crop_target_failure={TimberbornQaCommandBridge.FormatToken(State.SelectedCropTargetFailureReason)} " +
        $"last_delta_consumer_crop_burn_considered_targets={FormatNumber(State.LastDeltaConsumerCropBurnConsideredTargetCount)} " +
        $"last_delta_consumer_crop_burn_burnable_targets={FormatNumber(State.LastDeltaConsumerCropBurnBurnableTargetCount)} " +
        $"last_delta_consumer_crop_burn_yield_lost={FormatNumber(State.LastDeltaConsumerCropBurnYieldLost)} " +
        $"last_delta_consumer_crop_burn_killed_crops={FormatNumber(State.LastDeltaConsumerCropBurnKilledCropCount)} " +
        $"last_delta_consumer_crop_burn_visual_state_updates={FormatNumber(State.LastDeltaConsumerCropBurnVisualStateUpdateCount)} " +
        $"last_delta_consumer_crop_burn_duplicate_cells_suppressed={FormatNumber(State.LastDeltaConsumerCropBurnDuplicateCellSuppressedCount)} " +
        $"last_delta_consumer_crop_burn_unmapped_targets={FormatNumber(State.LastDeltaConsumerCropBurnUnmappedTargetCount)} " +
        $"last_delta_consumer_crop_burn_unknown_harvest_resources={FormatNumber(State.LastDeltaConsumerCropBurnUnknownHarvestResourceCount)} " +
        $"last_delta_consumer_crop_burn_non_burnable_targets={FormatNumber(State.LastDeltaConsumerCropBurnNonBurnableTargetCount)} " +
        $"last_delta_consumer_crop_burn_skipped_unsafe_apis={FormatNumber(State.LastDeltaConsumerCropBurnSkippedUnsafeApiCount)} " +
        $"last_delta_consumer_tree_burn_considered_targets={FormatNumber(State.LastDeltaConsumerTreeBurnConsideredTargetCount)} " +
        $"last_delta_consumer_tree_burn_burnable_targets={FormatNumber(State.LastDeltaConsumerTreeBurnBurnableTargetCount)} " +
        $"last_delta_consumer_tree_burn_yield_lost={FormatNumber(State.LastDeltaConsumerTreeBurnYieldLost)} " +
        $"last_delta_consumer_tree_burn_killed_trees={FormatNumber(State.LastDeltaConsumerTreeBurnKilledTreeCount)} " +
        $"last_delta_consumer_tree_burn_visual_state_updates={FormatNumber(State.LastDeltaConsumerTreeBurnVisualStateUpdateCount)} " +
        $"last_delta_consumer_tree_burn_duplicate_cells_suppressed={FormatNumber(State.LastDeltaConsumerTreeBurnDuplicateCellSuppressedCount)} " +
        $"last_delta_consumer_tree_burn_unmapped_targets={FormatNumber(State.LastDeltaConsumerTreeBurnUnmappedTargetCount)} " +
        $"last_delta_consumer_tree_burn_unknown_cuttable_resources={FormatNumber(State.LastDeltaConsumerTreeBurnUnknownCuttableResourceCount)} " +
        $"last_delta_consumer_tree_burn_non_burnable_targets={FormatNumber(State.LastDeltaConsumerTreeBurnNonBurnableTargetCount)} " +
        $"last_delta_consumer_tree_burn_skipped_unsafe_apis={FormatNumber(State.LastDeltaConsumerTreeBurnSkippedUnsafeApiCount)} " +
        $"last_delta_consumer_structure_burn_damage_rollback_considered_deltas={FormatNumber(State.LastDeltaConsumerStructureBurnDamageRollbackConsideredDeltaCount)} " +
        $"last_delta_consumer_structure_burn_damage_rollback_matched_structure_cells={FormatNumber(State.LastDeltaConsumerStructureBurnDamageRollbackMatchedStructureCellCount)} " +
        $"last_delta_consumer_structure_burn_damage_rollback_duplicate_structure_targets_suppressed={FormatNumber(State.LastDeltaConsumerStructureBurnDamageRollbackDuplicateStructureTargetSuppressedCount)} " +
        $"last_delta_consumer_structure_burn_damage_rollback_zero_burnable_capacity_targets={FormatNumber(State.LastDeltaConsumerStructureBurnDamageRollbackZeroBurnableCapacityTargetCount)} " +
        $"last_delta_consumer_structure_burn_damage_rollback_material_value_lost={FormatNumber(State.LastDeltaConsumerStructureBurnDamageRollbackMaterialValueLost)} " +
        $"last_delta_consumer_structure_burn_damage_rollback_closed_structures={FormatNumber(State.LastDeltaConsumerStructureBurnDamageRollbackClosedStructureCount)} " +
        $"last_delta_consumer_structure_burn_damage_rollback_repair_blocked={FormatNumber(State.LastDeltaConsumerStructureBurnDamageRollbackRepairBlockedCount)} " +
        $"last_delta_consumer_structure_burn_damage_rollback_repair_eligible={FormatNumber(State.LastDeltaConsumerStructureBurnDamageRollbackRepairEligibleCount)} " +
        $"last_delta_consumer_structure_burn_damage_rollback_stage_scorched={FormatNumber(State.LastDeltaConsumerStructureBurnDamageRollbackScorchedStageCount)} " +
        $"last_delta_consumer_structure_burn_damage_rollback_stage_partial_construction={FormatNumber(State.LastDeltaConsumerStructureBurnDamageRollbackPartialConstructionStageCount)} " +
        $"last_delta_consumer_structure_burn_damage_rollback_stage_unfinished={FormatNumber(State.LastDeltaConsumerStructureBurnDamageRollbackUnfinishedStageCount)} " +
        $"last_delta_consumer_structure_burn_damage_rollback_visual_applied={FormatNumber(State.LastDeltaConsumerStructureBurnDamageRollbackVisualRollbackAppliedCount)} " +
        $"last_delta_consumer_structure_burn_damage_rollback_skipped_no_safe_api={FormatNumber(State.LastDeltaConsumerStructureBurnDamageRollbackSkippedNoSafeApiCount)} " +
        $"last_delta_consumer_structure_burn_damage_rollback_total_damage_applied={FormatNumber(State.LastDeltaConsumerStructureBurnDamageRollbackTotalDamageApplied)} " +
        $"last_positive_building_burnout_applied_tick={FormatNumber(State.LastPositiveBuildingBurnoutAppliedTick)} " +
        $"last_positive_building_burnout_applied_count={FormatNumber(State.LastPositiveBuildingBurnoutAppliedCount)} " +
        $"last_delta_consumer_stored_good_burn_considered_deltas={FormatNumber(State.LastDeltaConsumerStoredGoodBurnConsideredDeltaCount)} " +
        $"last_delta_consumer_stored_good_burn_matched_storage_cells={FormatNumber(State.LastDeltaConsumerStoredGoodBurnMatchedStorageCellCount)} " +
        $"last_delta_consumer_stored_good_burn_duplicate_storage_targets_suppressed={FormatNumber(State.LastDeltaConsumerStoredGoodBurnDuplicateStorageTargetSuppressedCount)} " +
        $"last_delta_consumer_stored_good_burnable_stacks={FormatNumber(State.LastDeltaConsumerStoredGoodBurnableStackCount)} " +
        $"last_delta_consumer_stored_good_burn_destroyed_items={FormatNumber(State.LastDeltaConsumerStoredGoodBurnDestroyedItemCount)} " +
        $"last_delta_consumer_stored_good_burn_hazardous_goods={FormatNumber(State.LastDeltaConsumerStoredGoodBurnHazardousGoodCount)} " +
        $"last_delta_consumer_stored_good_burn_skipped_no_inventory_api={FormatNumber(State.LastDeltaConsumerStoredGoodBurnSkippedNoInventoryApiCount)} " +
        $"last_delta_consumer_stored_good_burn_skipped_unknown_resources={FormatNumber(State.LastDeltaConsumerStoredGoodBurnSkippedUnknownResourceCount)} " +
        $"last_delta_consumer_stored_good_burn_skipped_non_burnable_items={FormatNumber(State.LastDeltaConsumerStoredGoodBurnSkippedNonBurnableItemCount)} " +
        $"last_delta_consumer_explosive_infrastructure_considered_deltas={FormatNumber(State.LastDeltaConsumerExplosiveInfrastructureConsideredDeltaCount)} " +
        $"last_delta_consumer_explosive_infrastructure_matched_target_cells={FormatNumber(State.LastDeltaConsumerExplosiveInfrastructureMatchedTargetCellCount)} " +
        $"last_delta_consumer_explosive_infrastructure_duplicate_targets_suppressed={FormatNumber(State.LastDeltaConsumerExplosiveInfrastructureDuplicateTargetSuppressedCount)} " +
        $"last_delta_consumer_explosive_infrastructure_armed_targets={FormatNumber(State.LastDeltaConsumerExplosiveInfrastructureArmedTargetCount)} " +
        $"last_delta_consumer_explosive_infrastructure_triggered_targets={FormatNumber(State.LastDeltaConsumerExplosiveInfrastructureTriggeredTargetCount)} " +
        $"last_delta_consumer_explosive_infrastructure_native_triggered_targets={FormatNumber(State.LastDeltaConsumerExplosiveInfrastructureNativeTriggeredTargetCount)} " +
        $"last_delta_consumer_explosive_infrastructure_heat_pulse_cells={FormatNumber(State.LastDeltaConsumerExplosiveInfrastructureHeatPulseCellCount)} " +
        $"last_delta_consumer_explosive_infrastructure_skipped_setting_disabled={FormatNumber(State.LastDeltaConsumerExplosiveInfrastructureSkippedSettingDisabledCount)} " +
        $"last_delta_consumer_explosive_infrastructure_skipped_no_safe_api={FormatNumber(State.LastDeltaConsumerExplosiveInfrastructureSkippedNoSafeApiCount)} " +
        $"last_delta_consumer_explosive_infrastructure_skipped_already_triggered={FormatNumber(State.LastDeltaConsumerExplosiveInfrastructureSkippedAlreadyTriggeredCount)} " +
        $"last_delta_consumer_explosive_infrastructure_last_triggered_depth={FormatNumber(State.LastDeltaConsumerExplosiveInfrastructureLastTriggeredDepth)} " +
        $"last_delta_consumer_detonator_fire_safety_considered_deltas={FormatNumber(State.LastDeltaConsumerDetonatorFireSafetyConsideredDeltaCount)} " +
        $"last_delta_consumer_detonator_fire_safety_matched_target_cells={FormatNumber(State.LastDeltaConsumerDetonatorFireSafetyMatchedTargetCellCount)} " +
        $"last_delta_consumer_detonator_fire_safety_duplicate_targets_suppressed={FormatNumber(State.LastDeltaConsumerDetonatorFireSafetyDuplicateTargetSuppressedCount)} " +
        $"last_delta_consumer_detonator_fire_safety_disabled_targets={FormatNumber(State.LastDeltaConsumerDetonatorFireSafetyDisabledTargetCount)} " +
        $"last_delta_consumer_detonator_fire_safety_armed_targets={FormatNumber(State.LastDeltaConsumerDetonatorFireSafetyArmedTargetCount)} " +
        $"last_delta_consumer_detonator_fire_safety_skipped_setting_disabled={FormatNumber(State.LastDeltaConsumerDetonatorFireSafetySkippedSettingDisabledCount)} " +
        $"last_delta_consumer_detonator_fire_safety_skipped_no_safe_api={FormatNumber(State.LastDeltaConsumerDetonatorFireSafetySkippedNoSafeApiCount)} " +
        $"last_delta_consumer_detonator_fire_safety_recoverability_preserved={FormatNumber(State.LastDeltaConsumerDetonatorFireSafetyRecoverabilityPreservedCount)} " +
        $"last_delta_consumer_detonator_fire_safety_recoverability_unknown={FormatNumber(State.LastDeltaConsumerDetonatorFireSafetyRecoverabilityUnknownCount)} " +
        $"last_delta_consumer_tunnel_fire_considered_deltas={FormatNumber(State.LastDeltaConsumerTunnelFireConsideredDeltaCount)} " +
        $"last_delta_consumer_tunnel_fire_matched_target_cells={FormatNumber(State.LastDeltaConsumerTunnelFireMatchedTargetCellCount)} " +
        $"last_delta_consumer_tunnel_fire_duplicate_targets_suppressed={FormatNumber(State.LastDeltaConsumerTunnelFireDuplicateTargetSuppressedCount)} " +
        $"last_delta_consumer_tunnel_fire_unstable_targets={FormatNumber(State.LastDeltaConsumerTunnelFireUnstableTargetCount)} " +
        $"last_delta_consumer_tunnel_fire_native_explode_attempted={FormatNumber(State.LastDeltaConsumerTunnelFireNativeExplodeAttemptedCount)} " +
        $"last_delta_consumer_tunnel_fire_native_explode_applied={FormatNumber(State.LastDeltaConsumerTunnelFireNativeExplodeAppliedCount)} " +
        $"last_delta_consumer_tunnel_fire_destruction_deferred={FormatNumber(State.LastDeltaConsumerTunnelFireDestructionDeferredCount)} " +
        $"last_delta_consumer_tunnel_fire_skipped_setting_disabled={FormatNumber(State.LastDeltaConsumerTunnelFireSkippedSettingDisabledCount)} " +
        $"last_delta_consumer_tunnel_fire_skipped_no_safe_api={FormatNumber(State.LastDeltaConsumerTunnelFireSkippedNoSafeApiCount)} " +
        $"last_delta_consumer_tunnel_fire_recoverability_preserved={FormatNumber(State.LastDeltaConsumerTunnelFireRecoverabilityPreservedCount)} " +
        $"last_delta_consumer_tunnel_fire_recoverability_unknown={FormatNumber(State.LastDeltaConsumerTunnelFireRecoverabilityUnknownCount)} " +
        $"last_delta_consumer_path_infrastructure_considered_deltas={FormatNumber(State.LastDeltaConsumerPathInfrastructureConsideredDeltaCount)} " +
        $"last_delta_consumer_path_infrastructure_matched_target_cells={FormatNumber(State.LastDeltaConsumerPathInfrastructureMatchedTargetCellCount)} " +
        $"last_delta_consumer_path_infrastructure_duplicate_targets_suppressed={FormatNumber(State.LastDeltaConsumerPathInfrastructureDuplicateTargetSuppressedCount)} " +
        $"last_delta_consumer_path_infrastructure_zero_cost_targets={FormatNumber(State.LastDeltaConsumerPathInfrastructureZeroCostTargetCount)} " +
        $"last_delta_consumer_path_infrastructure_damaged_targets={FormatNumber(State.LastDeltaConsumerPathInfrastructureDamagedTargetCount)} " +
        $"last_delta_consumer_path_infrastructure_blocked_targets={FormatNumber(State.LastDeltaConsumerPathInfrastructureBlockedTargetCount)} " +
        $"last_delta_consumer_path_infrastructure_skipped_no_safe_api={FormatNumber(State.LastDeltaConsumerPathInfrastructureSkippedNoSafeApiCount)} " +
        $"last_delta_consumer_path_infrastructure_repair_eligible_targets={FormatNumber(State.LastDeltaConsumerPathInfrastructureRepairEligibleTargetCount)} " +
        $"last_delta_consumer_path_infrastructure_total_damage_applied={FormatNumber(State.LastDeltaConsumerPathInfrastructureTotalDamageApplied)} " +
        $"last_delta_consumer_power_infrastructure_considered_deltas={FormatNumber(State.LastDeltaConsumerPowerInfrastructureConsideredDeltaCount)} " +
        $"last_delta_consumer_power_infrastructure_matched_target_cells={FormatNumber(State.LastDeltaConsumerPowerInfrastructureMatchedTargetCellCount)} " +
        $"last_delta_consumer_power_infrastructure_duplicate_targets_suppressed={FormatNumber(State.LastDeltaConsumerPowerInfrastructureDuplicateTargetSuppressedCount)} " +
        $"last_delta_consumer_power_infrastructure_metal_only_noop_targets={FormatNumber(State.LastDeltaConsumerPowerInfrastructureMetalOnlyNoOpTargetCount)} " +
        $"last_delta_consumer_power_infrastructure_damaged_targets={FormatNumber(State.LastDeltaConsumerPowerInfrastructureDamagedTargetCount)} " +
        $"last_delta_consumer_power_infrastructure_disabled_or_disconnected_targets={FormatNumber(State.LastDeltaConsumerPowerInfrastructureDisabledOrDisconnectedTargetCount)} " +
        $"last_delta_consumer_power_infrastructure_skipped_no_safe_api={FormatNumber(State.LastDeltaConsumerPowerInfrastructureSkippedNoSafeApiCount)} " +
        $"last_delta_consumer_power_infrastructure_repair_eligible_targets={FormatNumber(State.LastDeltaConsumerPowerInfrastructureRepairEligibleTargetCount)} " +
        $"last_delta_consumer_power_infrastructure_total_damage_applied={FormatNumber(State.LastDeltaConsumerPowerInfrastructureTotalDamageApplied)} " +
        $"last_delta_consumer_water_infrastructure_considered_deltas={FormatNumber(State.LastDeltaConsumerWaterInfrastructureConsideredDeltaCount)} " +
        $"last_delta_consumer_water_infrastructure_matched_target_cells={FormatNumber(State.LastDeltaConsumerWaterInfrastructureMatchedTargetCellCount)} " +
        $"last_delta_consumer_water_infrastructure_duplicate_targets_suppressed={FormatNumber(State.LastDeltaConsumerWaterInfrastructureDuplicateTargetSuppressedCount)} " +
        $"last_delta_consumer_water_infrastructure_inert_material_noop_targets={FormatNumber(State.LastDeltaConsumerWaterInfrastructureInertMaterialNoOpTargetCount)} " +
        $"last_delta_consumer_water_infrastructure_difficult_to_burn_noop_targets={FormatNumber(State.LastDeltaConsumerWaterInfrastructureDifficultToBurnNoOpTargetCount)} " +
        $"last_delta_consumer_water_infrastructure_burnable_material_value={FormatNumber(State.LastDeltaConsumerWaterInfrastructureBurnableMaterialValue)} " +
        $"last_delta_consumer_water_infrastructure_damaged_targets={FormatNumber(State.LastDeltaConsumerWaterInfrastructureDamagedTargetCount)} " +
        $"last_delta_consumer_water_infrastructure_water_state_mutation_attempts={FormatNumber(State.LastDeltaConsumerWaterInfrastructureWaterStateMutationAttemptCount)} " +
        $"last_delta_consumer_water_infrastructure_skipped_no_safe_api={FormatNumber(State.LastDeltaConsumerWaterInfrastructureSkippedNoSafeApiCount)} " +
        $"last_delta_consumer_water_infrastructure_repair_eligible_targets={FormatNumber(State.LastDeltaConsumerWaterInfrastructureRepairEligibleTargetCount)} " +
        $"last_delta_consumer_water_infrastructure_total_damage_applied={FormatNumber(State.LastDeltaConsumerWaterInfrastructureTotalDamageApplied)} " +
        $"last_delta_consumer_alerts={FormatNumber(State.LastDeltaConsumerAlertCount)} " +
        $"last_player_fire_alert_tick={FormatNumber(State.LastPlayerFireAlertTick)} " +
        $"last_player_fire_alert_started_fires={FormatNumber(State.LastPlayerFireAlertStartedFireCount)} " +
        $"last_player_fire_alert_fuel_spent={FormatNumber(State.LastPlayerFireAlertFuelSpentCount)} " +
        $"last_player_fire_alert_max_heat={FormatNumber(State.LastPlayerFireAlertMaxHeat)} " +
        $"player_fire_alert_notifications={FormatNumber(State.PlayerFireAlertNotificationCount)} " +
        $"player_fire_alert_presentation_failures={FormatNumber(State.PlayerFireAlertPresentationFailureCount)} " +
        $"player_fire_alert_notification_sent={State.PlayerFireAlertNotificationSent.ToString().ToLowerInvariant()} " +
        $"last_player_fire_alert_message={TimberbornQaCommandBridge.FormatToken(State.LastPlayerFireAlertMessage)} " +
        $"visual_field_surface_bound={State.VisualFieldSurfaceBound.ToString().ToLowerInvariant()} " +
        $"visual_field_surface_cells={FormatNumber(State.VisualFieldSurfaceCellCount)} " +
        $"visual_field_surface_updated_tick={FormatNumber(State.VisualFieldSurfaceLastUpdatedTick)} " +
        $"gpu_field_renderer_enabled={State.GpuFieldRendererEnabled.ToString().ToLowerInvariant()} " +
        $"gpu_field_renderer_material_ready={State.GpuFieldRendererMaterialReady.ToString().ToLowerInvariant()} " +
        $"gpu_field_renderer_surface_bound={State.GpuFieldRendererSurfaceBound.ToString().ToLowerInvariant()} " +
        $"gpu_field_renderer_visible_regions={FormatNumber(State.GpuFieldRendererVisibleRegionCount)} " +
        $"gpu_field_renderer_updated_regions={FormatNumber(State.GpuFieldRendererUpdatedRegionCount)} " +
        $"gpu_field_renderer_last_nonzero_updated_regions={FormatNumber(State.GpuFieldRendererLastNonZeroUpdatedRegionCount)} " +
        $"gpu_field_renderer_last_nonzero_updated_regions_tick={FormatNumber(State.GpuFieldRendererLastNonZeroUpdatedRegionTick)} " +
        $"gpu_field_renderer_max_updated_regions={FormatNumber(State.GpuFieldRendererMaxUpdatedRegionCount)} " +
        $"gpu_field_renderer_dropped_regions={FormatNumber(State.GpuFieldRendererDroppedRegionCount)} " +
        $"gpu_field_renderer_invisible_regions={FormatNumber(State.GpuFieldRendererInvisibleRegionCount)} " +
        $"gpu_field_renderer_material_failures={FormatNumber(State.GpuFieldRendererMaterialFailureCount)} " +
        $"gpu_field_renderer_updated_tick={FormatNumber(State.GpuFieldRendererLastUpdatedTick)} " +
        $"beaver_field_exposure_available={State.BeaverFieldExposureAvailable.ToString().ToLowerInvariant()} " +
        $"beaver_field_exposure_sampled_beavers={FormatNumber(State.BeaverFieldExposureSampledBeavers)} " +
        $"beaver_field_exposure_exposed_beavers={FormatNumber(State.BeaverFieldExposureExposedBeavers)} " +
        $"beaver_field_exposure_respiratory_cells={FormatNumber(State.BeaverFieldExposureRespiratoryCells)} " +
        $"beaver_field_exposure_burn_cells={FormatNumber(State.BeaverFieldExposureBurnCells)} " +
        $"beaver_field_exposure_contaminated_smoke_cells={FormatNumber(State.BeaverFieldExposureContaminatedSmokeCells)} " +
        $"beaver_field_exposure_toxic_cells={FormatNumber(State.BeaverFieldExposureToxicCells)} " +
        $"beaver_field_exposure_toxic_steam_cells={FormatNumber(State.BeaverFieldExposureToxicSteamCells)} " +
        $"beaver_field_exposure_tainted_aftermath_cells={FormatNumber(State.BeaverFieldExposureTaintedAftermathCells)} " +
        $"beaver_field_exposure_skipped_no_position_api={FormatNumber(State.BeaverFieldExposureSkippedNoPositionApi)} " +
        $"beaver_field_exposure_skipped_bounded_sampling={FormatNumber(State.BeaverFieldExposureSkippedBoundedSampling)} " +
        $"beaver_field_exposure_unavailable_reason={TimberbornQaCommandBridge.FormatToken(State.BeaverFieldExposureUnavailableReason)} " +
        $"active_pooled_fire_effects={FormatNumber(State.ActivePooledFireEffectCount)} " +
        $"updated_visual_regions={FormatNumber(State.UpdatedVisualRegionCount)} " +
        $"last_nonzero_updated_visual_regions={FormatNumber(State.LastNonZeroUpdatedVisualRegionCount)} " +
        $"last_nonzero_updated_visual_regions_tick={FormatNumber(State.LastNonZeroUpdatedVisualRegionTick)} " +
        $"max_pooled_fire_effects={FormatNumber(State.MaxPooledFireEffectCount)} " +
        $"max_updated_visual_regions={FormatNumber(State.MaxUpdatedVisualRegionCount)} " +
        $"pooled_fire_effect_presentation_failures={FormatNumber(State.PooledFireEffectPresentationFailureCount)} " +
        $"pooled_fire_effects_visible_enabled={State.PooledFireEffectsVisibleEnabled.ToString().ToLowerInvariant()} " +
        $"pooled_fire_effects_native_prefab_resolved={State.PooledFireEffectsNativePrefabResolved.ToString().ToLowerInvariant()} " +
        $"pooled_fire_effects_native_prefab={TimberbornQaCommandBridge.FormatToken(State.PooledFireEffectsNativePrefabName)} " +
        $"burn_duration_proof_target={TimberbornQaCommandBridge.FormatToken(State.BurnDurationProofTarget)} " +
        $"burn_duration_proof_target_index={FormatNumber(State.BurnDurationProofTargetIndex)} " +
        $"burn_duration_proof_target_x={FormatNumber(State.BurnDurationProofTargetX)} " +
        $"burn_duration_proof_target_y={FormatNumber(State.BurnDurationProofTargetY)} " +
        $"burn_duration_proof_target_z={FormatNumber(State.BurnDurationProofTargetZ)} " +
        $"burn_duration_proof_initial_fuel={FormatNumber(State.BurnDurationProofInitialFuel)} " +
        $"burn_duration_proof_queued_tick={FormatNumber(State.BurnDurationProofQueuedTick)} " +
        $"burn_duration_proof_burn_start_tick={FormatNumber(State.BurnDurationProofBurnStartTick)} " +
        $"burn_duration_proof_depletion_tick={FormatNumber(State.BurnDurationProofDepletionTick)} " +
        $"burn_duration_proof_elapsed_burn_ticks={FormatNumber(State.BurnDurationProofElapsedBurnTicks)} " +
        $"burn_duration_proof_timeout_ticks={FormatNumber(State.BurnDurationProofTimeoutTicks)} " +
        $"burn_duration_proof_sustained_heat_ticks={FormatNumber(State.BurnDurationProofSustainedHeatTicks)} " +
        $"burn_duration_proof_sustained_heat_applied_ticks={FormatNumber(State.BurnDurationProofSustainedHeatAppliedTicks)} " +
        $"burn_duration_proof_sustained_heat_complete={State.BurnDurationProofSustainedHeatComplete.ToString().ToLowerInvariant()} " +
        $"burn_duration_proof_timed_out={State.BurnDurationProofTimedOut.ToString().ToLowerInvariant()} " +
        $"burn_duration_proof_status={TimberbornQaCommandBridge.FormatToken(State.BurnDurationProofStatus)} " +
        $"compatibility_probe_status={TimberbornQaCommandBridge.FormatToken(State.CompatibilityProbeStatus)} " +
        $"compatibility_probe_degraded={State.CompatibilityProbeDegraded.ToString().ToLowerInvariant()} " +
        $"compatibility_probe_required_passed={FormatNumber(State.CompatibilityProbeRequiredPassed)} " +
        $"compatibility_probe_required_total={FormatNumber(State.CompatibilityProbeRequiredTotal)} " +
        $"compatibility_probe_optional_passed={FormatNumber(State.CompatibilityProbeOptionalPassed)} " +
        $"compatibility_probe_optional_total={FormatNumber(State.CompatibilityProbeOptionalTotal)} " +
        $"compatibility_probe_degraded_features={TimberbornQaCommandBridge.FormatToken(State.CompatibilityProbeDegradedFeatures)} " +
        $"fire_sim_preset={TimberbornQaCommandBridge.FormatToken(State.FireSimPresetName)} " +
        $"fire_ignition_base_heat={FormatNumber(State.FireSimPresetIgnitionBaseHeat)} " +
        $"fire_water_fuel_lock={FormatNumber(State.FireSimPresetWaterFuelLock)} " +
        $"fire_fuel_heat_weight={FormatNumber(State.FireSimPresetFuelHeatWeight)} " +
        $"fire_fuel_burn_down={FormatFraction(State.FireSimPresetFuelBurnDownNumerator, State.FireSimPresetFuelBurnDownDenominator)} " +
        $"world_import_total_sources={FormatNumber(State.WorldImportTotalSources)} " +
        $"world_import_terrain_sources={FormatNumber(State.WorldImportTerrainSources)} " +
        $"world_import_vegetation_sources={FormatNumber(State.WorldImportVegetationSources)} " +
        $"world_import_tree_sources={FormatNumber(State.WorldImportTreeSources)} " +
        $"world_import_crop_sources={FormatNumber(State.WorldImportCropSources)} " +
        $"world_import_building_sources={FormatNumber(State.WorldImportBuildingSources)} " +
        $"world_import_storage_sources={FormatNumber(State.WorldImportStorageSources)} " +
        $"world_import_infrastructure_sources={FormatNumber(State.WorldImportInfrastructureSources)} " +
        $"world_import_water_sources={FormatNumber(State.WorldImportWaterSources)} " +
        $"world_import_badwater_sources={FormatNumber(State.WorldImportBadwaterSources)} " +
        $"world_import_resolved_empty_cells={FormatNumber(State.WorldImportResolvedEmptyCells)} " +
        $"world_import_resolved_terrain_cells={FormatNumber(State.WorldImportResolvedTerrainCells)} " +
        $"world_import_resolved_vegetation_cells={FormatNumber(State.WorldImportResolvedVegetationCells)} " +
        $"world_import_resolved_tree_cells={FormatNumber(State.WorldImportResolvedTreeCells)} " +
        $"world_import_resolved_crop_cells={FormatNumber(State.WorldImportResolvedCropCells)} " +
        $"world_import_resolved_building_cells={FormatNumber(State.WorldImportResolvedBuildingCells)} " +
        $"world_import_resolved_storage_cells={FormatNumber(State.WorldImportResolvedStorageCells)} " +
        $"world_import_resolved_infrastructure_cells={FormatNumber(State.WorldImportResolvedInfrastructureCells)} " +
        $"world_import_resolved_water_cells={FormatNumber(State.WorldImportResolvedWaterCells)} " +
        $"world_import_resolved_badwater_cells={FormatNumber(State.WorldImportResolvedBadwaterCells)} " +
        $"world_import_safe_unavailable={FormatNumber(State.WorldImportSafeUnavailableCount)} " +
        $"message={TimberbornQaCommandBridge.FormatToken(Message)}";

    public static TimberbornQaCommandResult CreateSuccess(
        string command,
        TimberbornQaCommandState state,
        IReadOnlyList<string> knownCommands,
        string message = "ok")
    {
        return new TimberbornQaCommandResult(command, true, "success", state, knownCommands, message);
    }

    public static TimberbornQaCommandResult CreateFailure(
        string command,
        string message,
        TimberbornQaCommandState state,
        IReadOnlyList<string> knownCommands)
    {
        return new TimberbornQaCommandResult(command, false, "failure", state, knownCommands, message);
    }

    private static string FormatNumber(int? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }

    private static string FormatNumber(uint? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }

    private static string FormatFraction(uint? numerator, uint? denominator)
    {
        return numerator.HasValue && denominator.HasValue
            ? $"{FormatNumber(numerator)}/{FormatNumber(denominator)}"
            : "placeholder";
    }

    private static string FormatFloat(float? value)
    {
        return value?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }
}

public interface ITimberbornQaSoilMoistureMapProbe
{
    TimberbornQaSoilMoistureRangeResult ScanSoilMoistureRange();
}

public sealed record TimberbornQaSoilMoistureRangeResult(
    int SampleCount,
    int SkippedCount,
    int MoistCellCount,
    float? Min,
    float? Max,
    float? Average,
    int MinX,
    int MinY,
    int MinZ,
    int MaxX,
    int MaxY,
    int MaxZ);

public sealed class NullTimberbornQaSoilMoistureMapProbe : ITimberbornQaSoilMoistureMapProbe
{
    public static readonly NullTimberbornQaSoilMoistureMapProbe Instance = new();

    private NullTimberbornQaSoilMoistureMapProbe()
    {
    }

    public TimberbornQaSoilMoistureRangeResult ScanSoilMoistureRange()
    {
        throw new InvalidOperationException("QA soil moisture range command is unavailable.");
    }
}

public sealed class TimberbornQaSoilMoistureMapProbe : ITimberbornQaSoilMoistureMapProbe
{
    private readonly MapSize _mapSize;
    private readonly ITerrainService _terrainService;
    private readonly ISoilMoistureService _soilMoistureService;
    private readonly MapIndexService _mapIndexService;

    public TimberbornQaSoilMoistureMapProbe(
        MapSize mapSize,
        ITerrainService terrainService,
        ISoilMoistureService soilMoistureService,
        MapIndexService mapIndexService)
    {
        _mapSize = mapSize ?? throw new ArgumentNullException(nameof(mapSize));
        _terrainService = terrainService ?? throw new ArgumentNullException(nameof(terrainService));
        _soilMoistureService = soilMoistureService ?? throw new ArgumentNullException(nameof(soilMoistureService));
        _mapIndexService = mapIndexService ?? throw new ArgumentNullException(nameof(mapIndexService));
    }

    public TimberbornQaSoilMoistureRangeResult ScanSoilMoistureRange()
    {
        Vector2Int terrainSize2D = _mapSize.TerrainSize2D;
        Vector3Int terrainSize = _mapSize.TerrainSize;
        TimberbornQaSoilMoistureSample[] samples = Enumerable.Range(0, terrainSize2D.x)
            .SelectMany(x => Enumerable.Range(0, terrainSize2D.y)
                .SelectMany(y => _terrainService.GetAllHeightsInCell(new Vector2Int(x, y))))
            .Where(coordinates => IsInsideTerrain(coordinates, terrainSize))
            .Select(ReadSample)
            .ToArray();
        TimberbornQaSoilMoistureSample[] validSamples = samples
            .Where(static sample => sample.IsValid)
            .ToArray();

        if (validSamples.Length == 0)
        {
            return new TimberbornQaSoilMoistureRangeResult(
                SampleCount: 0,
                SkippedCount: samples.Length,
                MoistCellCount: 0,
                Min: null,
                Max: null,
                Average: null,
                MinX: -1,
                MinY: -1,
                MinZ: -1,
                MaxX: -1,
                MaxY: -1,
                MaxZ: -1);
        }

        TimberbornQaSoilMoistureSample min = validSamples
            .OrderBy(static sample => sample.Moisture)
            .First();
        TimberbornQaSoilMoistureSample max = validSamples
            .OrderByDescending(static sample => sample.Moisture)
            .First();

        return new TimberbornQaSoilMoistureRangeResult(
            SampleCount: validSamples.Length,
            SkippedCount: samples.Length - validSamples.Length,
            MoistCellCount: validSamples.Count(static sample => sample.IsMoist),
            Min: min.Moisture,
            Max: max.Moisture,
            Average: validSamples.Average(static sample => sample.Moisture),
            MinX: min.Coordinates.x,
            MinY: min.Coordinates.y,
            MinZ: min.Coordinates.z,
            MaxX: max.Coordinates.x,
            MaxY: max.Coordinates.y,
            MaxZ: max.Coordinates.z);
    }

    private TimberbornQaSoilMoistureSample ReadSample(Vector3Int coordinates)
    {
        try
        {
            Vector3Int timberbornCoordinates = new(coordinates.x, coordinates.z, coordinates.y);
            int index = _mapIndexService.CellToIndex(new Vector2Int(coordinates.x, coordinates.y));
            return new TimberbornQaSoilMoistureSample(
                coordinates,
                _soilMoistureService.SoilMoisture(index),
                TryReadIsMoist(timberbornCoordinates),
                IsValid: true);
        }
        catch
        {
            return new TimberbornQaSoilMoistureSample(coordinates, 0f, IsMoist: false, IsValid: false);
        }
    }

    private bool TryReadIsMoist(Vector3Int timberbornCoordinates)
    {
        try
        {
            return _soilMoistureService.SoilIsMoist(timberbornCoordinates);
        }
        catch
        {
            return false;
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

    private readonly record struct TimberbornQaSoilMoistureSample(
        Vector3Int Coordinates,
        float Moisture,
        bool IsMoist,
        bool IsValid);
}

public interface ITimberbornQaCommandLogSink
{
    void Info(string message);

    void Warning(string message);
}

public sealed class NullTimberbornQaCommandLogSink : ITimberbornQaCommandLogSink
{
    public static readonly NullTimberbornQaCommandLogSink Instance = new();

    private NullTimberbornQaCommandLogSink()
    {
    }

    public void Info(string message)
    {
    }

    public void Warning(string message)
    {
    }
}
