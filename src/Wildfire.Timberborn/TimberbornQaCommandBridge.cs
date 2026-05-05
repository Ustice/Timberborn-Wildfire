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

    private readonly ITimberbornQaCommandStateProvider _stateProvider;
    private readonly ITimberbornQaDeltaStimulus _deltaStimulus;
    private readonly ITimberbornQaBuildingBurnoutStimulus _buildingBurnoutStimulus;
    private readonly ITimberbornQaWaterSuppressionStimulus _waterSuppressionStimulus;
    private readonly ITimberbornQaBurnDurationStimulus _burnDurationStimulus;
    private readonly ITimberbornQaFireSimParameterPresetSelector _fireSimParameterPresetSelector;
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
            $"queued_heat_changes={stimulusResult.QueuedHeatChangeCount}");
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
            "queued_water_suppression_stimulus_" +
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
            $"neighbor_bonus={presetResult.Parameters.FireBurningNeighborHeatBonus}_" +
            $"water_suppression={presetResult.Parameters.FireWaterSuppressionHeat}");
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
        return (StringComparer.OrdinalIgnoreCase.Equals(command, QaDeltaStimulusCommand) ||
            StringComparer.OrdinalIgnoreCase.Equals(command, QaWaterSuppressionStimulusCommand)) &&
            ParseFieldTargetSelector(commandText) is not null ||
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
    int QueuedHeatChangeCount);

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
    public const string Vegetation = "vegetation";
    public const string Crop = "crop";
    public const string Storage = "storage";
    public const string Building = "building";

    private static readonly HashSet<string> KnownSelectors = new(StringComparer.OrdinalIgnoreCase)
    {
        Default,
        Tree,
        Vegetation,
        Crop,
        Storage,
        Building,
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
            Vegetation => materialClass == WildfireMaterialClass.Vegetation,
            Crop => materialClass == WildfireMaterialClass.Crop,
            Storage => materialClass == WildfireMaterialClass.Storage,
            Building => materialClass == WildfireMaterialClass.Building,
            _ => false,
        };
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
    int? GpuFieldRendererMaterialFailureCount = null,
    uint? GpuFieldRendererLastUpdatedTick = null,
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
    uint? FireSimPresetBurningNeighborHeatBonus = null,
    uint? FireSimPresetWaterSuppressionHeat = null,
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
        $"gpu_field_renderer_material_failures={FormatNumber(State.GpuFieldRendererMaterialFailureCount)} " +
        $"gpu_field_renderer_updated_tick={FormatNumber(State.GpuFieldRendererLastUpdatedTick)} " +
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
        $"fire_burning_neighbor_heat_bonus={FormatNumber(State.FireSimPresetBurningNeighborHeatBonus)} " +
        $"fire_water_suppression_heat={FormatNumber(State.FireSimPresetWaterSuppressionHeat)} " +
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
