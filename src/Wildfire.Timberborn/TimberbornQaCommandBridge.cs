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
            commands[QaDeltaStimulusCommand] = ExecuteQaDeltaStimulus;
        }

        if (!ReferenceEquals(buildingBurnoutStimulus, NullTimberbornQaBuildingBurnoutStimulus.Instance))
        {
            commands[QaBuildingBurnoutStimulusCommand] = ExecuteQaBuildingBurnoutStimulus;
        }

        if (!ReferenceEquals(waterSuppressionStimulus, NullTimberbornQaWaterSuppressionStimulus.Instance))
        {
            commands[QaWaterSuppressionStimulusCommand] = ExecuteQaWaterSuppressionStimulus;
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

    private TimberbornQaCommandResult ExecuteQaDeltaStimulus()
    {
        TimberbornQaDeltaStimulusResult stimulusResult = _deltaStimulus.QueueFixedDeltaStimulus();
        TimberbornQaCommandState state = _stateProvider.GetState();

        return TimberbornQaCommandResult.CreateSuccess(
            QaDeltaStimulusCommand,
            state,
            KnownCommands,
            "queued_fixed_center_stimulus_" +
            $"target_index={stimulusResult.CellIndex}_" +
            $"target_x={stimulusResult.X}_" +
            $"target_y={stimulusResult.Y}_" +
            $"target_z={stimulusResult.Z}_" +
            $"set_cell={stimulusResult.SetCell}");
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
            $"primed_cell={stimulusResult.PrimedCell}_" +
            $"set_cell={stimulusResult.SetCell}_" +
            $"queued_set_cell_changes={stimulusResult.QueuedSetCellChangeCount}");
    }

    private TimberbornQaCommandResult ExecuteQaWaterSuppressionStimulus()
    {
        TimberbornQaWaterSuppressionStimulusResult stimulusResult =
            _waterSuppressionStimulus.QueueWaterSuppressionStimulus();
        TimberbornQaCommandState state = _stateProvider.GetState();

        return TimberbornQaCommandResult.CreateSuccess(
            QaWaterSuppressionStimulusCommand,
            state,
            KnownCommands,
            "queued_water_suppression_stimulus_" +
            $"target_index={stimulusResult.CellIndex}_" +
            $"target_x={stimulusResult.X}_" +
            $"target_y={stimulusResult.Y}_" +
            $"target_z={stimulusResult.Z}_" +
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
            $"initial_fuel={stimulusResult.InitialFuel}_" +
            $"set_cell={stimulusResult.SetCell}_" +
            $"timeout_ticks={stimulusResult.TimeoutTicks}_" +
            $"queued_set_cell_changes={stimulusResult.QueuedSetCellChangeCount}");
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
        return StringComparer.OrdinalIgnoreCase.Equals(command, QaBurnDurationStimulusCommand) &&
            ParseBurnDurationTarget(commandText) is not null ||
            StringComparer.OrdinalIgnoreCase.Equals(command, QaFirePresetCommand) &&
            ParseFirePresetName(commandText) is not null;
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
    TimberbornQaDeltaStimulusResult QueueFixedDeltaStimulus();
}

public sealed record TimberbornQaDeltaStimulusResult(int CellIndex, int X, int Y, int Z, ushort SetCell);

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
    ushort PrimedCell,
    ushort SetCell,
    int QueuedSetCellChangeCount);

public interface ITimberbornQaWaterSuppressionStimulus
{
    TimberbornQaWaterSuppressionStimulusResult QueueWaterSuppressionStimulus();
}

public sealed record TimberbornQaWaterSuppressionStimulusResult(
    int CellIndex,
    int X,
    int Y,
    int Z,
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
    byte InitialFuel,
    ushort SetCell,
    uint TimeoutTicks,
    int QueuedSetCellChangeCount);

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

    public static TimberbornQaBurnDurationStimulusTarget SelectTarget(FireGrid grid, string target)
    {
        if (!IsKnownTarget(target))
        {
            throw new ArgumentException("Burn-duration QA target must be low, medium, or high.", nameof(target));
        }

        int centerX = grid.Width / 2;
        int x = target.ToLowerInvariant() switch
        {
            Low => Math.Max(0, centerX - 1),
            Medium => centerX,
            High => Math.Min(grid.Width - 1, centerX + 1),
            _ => centerX,
        };
        int y = grid.Height / 2;
        int z = grid.Depth / 2;

        return new TimberbornQaBurnDurationStimulusTarget(
            target.ToLowerInvariant(),
            grid.ToIndex(x, y, z),
            x,
            y,
            z,
            InitialFuel(target));
    }
}

public sealed record TimberbornQaBurnDurationStimulusTarget(
    string Target,
    int CellIndex,
    int X,
    int Y,
    int Z,
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

    public TimberbornQaDeltaStimulusResult QueueFixedDeltaStimulus()
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

    public TimberbornQaWaterSuppressionStimulusResult QueueWaterSuppressionStimulus()
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
    uint? LastPositiveBuildingBurnoutAppliedTick = null,
    int? LastPositiveBuildingBurnoutAppliedCount = null,
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
    int? WorldImportTreeSources = null,
    int? WorldImportCropSources = null,
    int? WorldImportBuildingSources = null,
    int? WorldImportStorageSources = null,
    int? WorldImportInfrastructureSources = null,
    int? WorldImportWaterSources = null,
    int? WorldImportBadwaterSources = null,
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
        $"last_positive_building_burnout_applied_tick={FormatNumber(State.LastPositiveBuildingBurnoutAppliedTick)} " +
        $"last_positive_building_burnout_applied_count={FormatNumber(State.LastPositiveBuildingBurnoutAppliedCount)} " +
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
        $"world_import_tree_sources={FormatNumber(State.WorldImportTreeSources)} " +
        $"world_import_crop_sources={FormatNumber(State.WorldImportCropSources)} " +
        $"world_import_building_sources={FormatNumber(State.WorldImportBuildingSources)} " +
        $"world_import_storage_sources={FormatNumber(State.WorldImportStorageSources)} " +
        $"world_import_infrastructure_sources={FormatNumber(State.WorldImportInfrastructureSources)} " +
        $"world_import_water_sources={FormatNumber(State.WorldImportWaterSources)} " +
        $"world_import_badwater_sources={FormatNumber(State.WorldImportBadwaterSources)} " +
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
