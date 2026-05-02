namespace Wildfire.Timberborn;

public sealed class TimberbornQaCommandBridge
{
    public const string StatusCommand = "status";
    public const string HelpCommand = "help";
    public const string QaReadinessCommand = "qa-readiness";
    public const string QaDeltaStimulusCommand = "qa-delta-stimulus";

    private readonly ITimberbornQaCommandStateProvider _stateProvider;
    private readonly ITimberbornQaDeltaStimulus _deltaStimulus;
    private readonly ITimberbornQaCommandLogSink _logSink;
    private readonly IReadOnlyDictionary<string, Func<TimberbornQaCommandResult>> _commands;

    public TimberbornQaCommandBridge()
        : this(
            TimberbornQaCommandStateProvider.Placeholder,
            NullTimberbornQaDeltaStimulus.Instance,
            NullTimberbornQaCommandLogSink.Instance)
    {
    }

    public TimberbornQaCommandBridge(
        ITimberbornQaCommandStateProvider stateProvider,
        ITimberbornQaCommandLogSink logSink)
        : this(stateProvider, NullTimberbornQaDeltaStimulus.Instance, logSink)
    {
    }

    public TimberbornQaCommandBridge(
        ITimberbornQaCommandStateProvider stateProvider,
        ITimberbornQaDeltaStimulus deltaStimulus,
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

        if (logSink is null)
        {
            throw new ArgumentNullException(nameof(logSink));
        }

        _stateProvider = stateProvider;
        _deltaStimulus = deltaStimulus;
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

        try
        {
            TimberbornQaCommandResult result = handler();
            _logSink.Info(result.ResultToken);
            return result;
        }
        catch (Exception exception)
        {
            TimberbornQaCommandResult failure = TimberbornQaCommandResult.CreateFailure(
                command,
                exception.Message,
                TimberbornQaCommandState.Placeholder,
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
        string message = _commands.ContainsKey(QaDeltaStimulusCommand)
            ? "Supported commands: help, qa-readiness, qa-delta-stimulus, status. Only qa-delta-stimulus queues a fixed QA-only simulator change."
            : "Supported commands are read-only: help, qa-readiness, status.";

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
            state.IsLoadedGameReady ? "loaded_game_ready" : "loaded_game_not_ready");
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

    private static string NormalizeCommand(string commandText)
    {
        string command = commandText.Trim();

        if (string.IsNullOrEmpty(command))
        {
            return StatusCommand;
        }

        return command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
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
    int? LastDeltaConsumerVisualEffectEventCount = null,
    int? LastDeltaConsumerGameplayConsequenceCount = null,
    int? LastDeltaConsumerAlertCount = null)
{
    public static readonly TimberbornQaCommandState Placeholder = new(IsSimulatorIntegrated: false);

    public bool IsLoadedGameReady =>
        IsGameContextRuntimeLoaded &&
        IsSimulatorIntegrated &&
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
        $"last_delta_consumer_visual_effect_events={FormatNumber(State.LastDeltaConsumerVisualEffectEventCount)} " +
        $"last_delta_consumer_gameplay_consequences={FormatNumber(State.LastDeltaConsumerGameplayConsequenceCount)} " +
        $"last_delta_consumer_alerts={FormatNumber(State.LastDeltaConsumerAlertCount)} " +
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
