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

    private readonly ITimberbornQaCommandStateProvider _stateProvider;
    private readonly ITimberbornQaDeltaStimulus _deltaStimulus;
    private readonly ITimberbornQaBuildingBurnoutStimulus _buildingBurnoutStimulus;
    private readonly ITimberbornQaWaterSuppressionStimulus _waterSuppressionStimulus;
    private readonly ITimberbornQaCommandLogSink _logSink;
    private readonly IReadOnlyDictionary<string, Func<TimberbornQaCommandResult>> _commands;

    public TimberbornQaCommandBridge()
        : this(
            TimberbornQaCommandStateProvider.Placeholder,
            NullTimberbornQaDeltaStimulus.Instance,
            NullTimberbornQaBuildingBurnoutStimulus.Instance,
            NullTimberbornQaWaterSuppressionStimulus.Instance,
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
            logSink)
    {
    }

    public TimberbornQaCommandBridge(
        ITimberbornQaCommandStateProvider stateProvider,
        ITimberbornQaDeltaStimulus deltaStimulus,
        ITimberbornQaBuildingBurnoutStimulus buildingBurnoutStimulus,
        ITimberbornQaWaterSuppressionStimulus waterSuppressionStimulus,
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

        if (logSink is null)
        {
            throw new ArgumentNullException(nameof(logSink));
        }

        _stateProvider = stateProvider;
        _deltaStimulus = deltaStimulus;
        _buildingBurnoutStimulus = buildingBurnoutStimulus;
        _waterSuppressionStimulus = waterSuppressionStimulus;
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

        if (IsSimulatorChangeCommand(command) && HasArguments(commandText))
        {
            TimberbornQaCommandResult failure = TimberbornQaCommandResult.CreateFailure(
                command,
                $"Command '{command}' does not accept arguments.",
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

    private static bool IsSimulatorChangeCommand(string command)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(command, QaDeltaStimulusCommand) ||
            StringComparer.OrdinalIgnoreCase.Equals(command, QaBuildingBurnoutStimulusCommand) ||
            StringComparer.OrdinalIgnoreCase.Equals(command, QaWaterSuppressionStimulusCommand);
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
    int? LastDeltaConsumerWaterChangedCount = null,
    uint? LastPositiveWaterChangedTick = null,
    int? LastPositiveWaterChangedCount = null,
    int? LastDeltaConsumerVisualEffectEventCount = null,
    int? LastDeltaConsumerVisualEffectFailureCount = null,
    int? LastDeltaConsumerGameplayConsequenceCount = null,
    int? LastDeltaConsumerBuildingBurnoutConsideredDeltaCount = null,
    int? LastDeltaConsumerBuildingBurnoutMatchedCellCount = null,
    int? LastDeltaConsumerBuildingBurnoutAppliedConsequenceCount = null,
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
    string? PooledFireEffectsNativePrefabName = null)
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
        $"last_delta_consumer_water_changed={FormatNumber(State.LastDeltaConsumerWaterChangedCount)} " +
        $"last_positive_water_changed_tick={FormatNumber(State.LastPositiveWaterChangedTick)} " +
        $"last_positive_water_changed_count={FormatNumber(State.LastPositiveWaterChangedCount)} " +
        $"last_delta_consumer_visual_effect_events={FormatNumber(State.LastDeltaConsumerVisualEffectEventCount)} " +
        $"last_delta_consumer_visual_effect_failures={FormatNumber(State.LastDeltaConsumerVisualEffectFailureCount)} " +
        $"last_delta_consumer_gameplay_consequences={FormatNumber(State.LastDeltaConsumerGameplayConsequenceCount)} " +
        $"last_delta_consumer_building_burnout_considered_deltas={FormatNumber(State.LastDeltaConsumerBuildingBurnoutConsideredDeltaCount)} " +
        $"last_delta_consumer_building_burnout_matched_cells={FormatNumber(State.LastDeltaConsumerBuildingBurnoutMatchedCellCount)} " +
        $"last_delta_consumer_building_burnout_applied_consequences={FormatNumber(State.LastDeltaConsumerBuildingBurnoutAppliedConsequenceCount)} " +
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
