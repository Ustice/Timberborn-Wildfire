using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornQaCommandBridgeTests
{
    [Fact]
    public void ExecuteStatusReturnsStateProviderValuesAndLogsResult()
    {
        TimberbornQaCommandState state = new(
            IsSimulatorIntegrated: true,
            Width: 12,
            Height: 8,
            Depth: 3,
            TickCount: 42,
            QueuedChangeCount: 5,
            LastDeltaCount: 7);
        RecordingStateProvider stateProvider = new(state);
        RecordingLogSink logSink = new();
        TimberbornQaCommandBridge bridge = new(stateProvider, logSink);

        TimberbornQaCommandResult result = bridge.Execute("status");

        Assert.True(result.Success);
        Assert.Equal("status", result.Command);
        Assert.Equal("success", result.Status);
        Assert.Equal(state, result.State);
        Assert.Equal(1, stateProvider.CallCount);
        Assert.Contains("wildfire_command_request command=status", logSink.InfoMessages);
        Assert.Contains(result.ResultToken, logSink.InfoMessages);
    }

    [Fact]
    public void EmptyCommandNormalizesToStatus()
    {
        RecordingStateProvider stateProvider = new(TimberbornQaCommandState.Placeholder);
        TimberbornQaCommandBridge bridge = new(stateProvider, new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("   ");

        Assert.True(result.Success);
        Assert.Equal("status", result.Command);
        Assert.Equal(1, stateProvider.CallCount);
    }

    [Fact]
    public void NullCommandFailsAndLogsResultWithoutQueryingStateProvider()
    {
        RecordingStateProvider stateProvider = new(TimberbornQaCommandState.Placeholder);
        RecordingLogSink logSink = new();
        TimberbornQaCommandBridge bridge = new(stateProvider, logSink);

        TimberbornQaCommandResult result = bridge.Execute(null);

        Assert.False(result.Success);
        Assert.Equal("null", result.Command);
        Assert.Equal("failure", result.Status);
        Assert.Equal(TimberbornQaCommandState.Placeholder, result.State);
        Assert.Equal(0, stateProvider.CallCount);
        Assert.Contains("Command text is required.", result.Message);
        Assert.Contains("wildfire_command_request command=null", logSink.InfoMessages);
        Assert.Contains(result.ResultToken, logSink.WarningMessages);
    }

    [Fact]
    public void CommandNormalizationUsesFirstTokenAndIgnoresCase()
    {
        TimberbornQaCommandState state = new(IsSimulatorIntegrated: true, TickCount: 3);
        RecordingStateProvider stateProvider = new(state);
        TimberbornQaCommandBridge bridge = new(stateProvider, new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("  STATUS   extra-ignored  ");

        Assert.True(result.Success);
        Assert.Equal("status", result.Command);
        Assert.Equal(state, result.State);
    }

    [Fact]
    public void ExecuteHelpReturnsReadOnlyCommandList()
    {
        TimberbornQaCommandBridge bridge = new(
            new RecordingStateProvider(TimberbornQaCommandState.Placeholder),
            new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("help");

        Assert.True(result.Success);
        Assert.Equal("help", result.Command);
        Assert.Equal(["help", "qa-readiness", "status"], result.KnownCommands);
        Assert.Contains("read-only", result.Message);
        Assert.Contains("qa-readiness", result.Message);
    }

    [Fact]
    public void DefaultConstructionKeepsQaDeltaStimulusRejectedAsUnknown()
    {
        RecordingStateProvider stateProvider = new(TimberbornQaCommandState.Placeholder);
        RecordingLogSink logSink = new();
        TimberbornQaCommandBridge bridge = new(stateProvider, logSink);

        TimberbornQaCommandResult result = bridge.Execute("qa-delta-stimulus");

        Assert.False(result.Success);
        Assert.Equal("qa-delta-stimulus", result.Command);
        Assert.Equal(["help", "qa-readiness", "status"], result.KnownCommands);
        Assert.Equal(0, stateProvider.CallCount);
        Assert.Contains("Unknown command 'qa-delta-stimulus'.", result.Message);
        Assert.Contains(result.ResultToken, logSink.WarningMessages);
    }

    [Fact]
    public void LiveStimulusBindingExpandsAllowlistAndHelpMessage()
    {
        TimberbornQaCommandBridge bridge = new(
            new RecordingStateProvider(TimberbornQaCommandState.Placeholder),
            new RecordingDeltaStimulus(new TimberbornQaDeltaStimulusResult(0, 0, 0, 0, 1)),
            new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("help");

        Assert.True(result.Success);
        Assert.Equal(["help", "qa-delta-stimulus", "qa-readiness", "status"], result.KnownCommands);
        Assert.Contains("qa-delta-stimulus", result.Message);
        Assert.Contains("QA-only simulator change", result.Message);
    }

    [Fact]
    public void BuildingBurnoutStimulusBindingExpandsAllowlistAndHelpMessage()
    {
        TimberbornQaCommandBridge bridge = new(
            new RecordingStateProvider(TimberbornQaCommandState.Placeholder),
            NullTimberbornQaDeltaStimulus.Instance,
            new RecordingBuildingBurnoutStimulus(
                new TimberbornQaBuildingBurnoutStimulusResult(0, 0, 0, 0, 1, 1, 2, 2)),
            new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("help");

        Assert.True(result.Success);
        Assert.Equal(["help", "qa-building-burnout-stimulus", "qa-readiness", "status"], result.KnownCommands);
        Assert.Contains("qa-building-burnout-stimulus", result.Message);
        Assert.Contains("QA-only simulator change", result.Message);
    }

    [Fact]
    public void ExecuteQaReadinessReturnsSafeLoadedGameReadinessState()
    {
        TimberbornQaCommandState state = new(
            IsSimulatorIntegrated: true,
            IsGameContextRuntimeLoaded: true,
            Width: 12,
            Height: 8,
            Depth: 3,
            TickCount: 42,
            QueuedChangeCount: 5,
            LastDeltaCount: 7);
        RecordingStateProvider stateProvider = new(state);
        RecordingLogSink logSink = new();
        TimberbornQaCommandBridge bridge = new(stateProvider, logSink);

        TimberbornQaCommandResult result = bridge.Execute("qa-readiness");

        Assert.True(result.Success);
        Assert.Equal("qa-readiness", result.Command);
        Assert.Equal("success", result.Status);
        Assert.Equal(state, result.State);
        Assert.Equal(["help", "qa-readiness", "status"], result.KnownCommands);
        Assert.Equal("loaded_game_ready", result.Message);
        Assert.Equal(1, stateProvider.CallCount);
        Assert.Contains("wildfire_command_request command=qa-readiness", logSink.InfoMessages);
        Assert.Contains(result.ResultToken, logSink.InfoMessages);
    }

    [Fact]
    public void QaReadinessTokenIncludesReadinessAndSimulatorFields()
    {
        TimberbornQaCommandState state = new(
            IsSimulatorIntegrated: true,
            IsGameContextRuntimeLoaded: true,
            Width: 4,
            Height: 5,
            Depth: 6,
            TickCount: 7,
            QueuedChangeCount: 8,
            LastDeltaCount: 9);
        TimberbornQaCommandBridge bridge = new(new RecordingStateProvider(state), new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("qa-readiness");

        Assert.Contains("wildfire_command_result", result.ResultToken);
        Assert.Contains("command=qa-readiness", result.ResultToken);
        Assert.Contains("success=true", result.ResultToken);
        Assert.Contains("bridge_alive=true", result.ResultToken);
        Assert.Contains("runtime_loaded=true", result.ResultToken);
        Assert.Contains("loaded_game_ready=true", result.ResultToken);
        Assert.Contains("simulator_integrated=true", result.ResultToken);
        Assert.Contains("width=4", result.ResultToken);
        Assert.Contains("height=5", result.ResultToken);
        Assert.Contains("depth=6", result.ResultToken);
        Assert.Contains("tick_count=7", result.ResultToken);
        Assert.Contains("queued_changes=8", result.ResultToken);
        Assert.Contains("last_delta_count=9", result.ResultToken);
    }

    [Fact]
    public void ExecuteQaDeltaStimulusQueuesBoundStimulusAndReportsTargetFields()
    {
        TimberbornQaCommandState state = new(
            IsSimulatorIntegrated: true,
            IsGameContextRuntimeLoaded: true,
            Width: 8,
            Height: 6,
            Depth: 4,
            TickCount: 12,
            QueuedChangeCount: 1,
            LastDeltaCount: 0);
        RecordingStateProvider stateProvider = new(state);
        RecordingDeltaStimulus deltaStimulus = new(new TimberbornQaDeltaStimulusResult(91, 3, 4, 2, 498));
        RecordingLogSink logSink = new();
        TimberbornQaCommandBridge bridge = new(stateProvider, deltaStimulus, logSink);

        TimberbornQaCommandResult result = bridge.Execute("qa-delta-stimulus");

        Assert.True(result.Success);
        Assert.Equal("qa-delta-stimulus", result.Command);
        Assert.Equal(["help", "qa-delta-stimulus", "qa-readiness", "status"], result.KnownCommands);
        Assert.Equal(1, deltaStimulus.CallCount);
        Assert.Equal(1, stateProvider.CallCount);
        Assert.Contains("target_index=91", result.Message);
        Assert.Contains("target_x=3", result.Message);
        Assert.Contains("target_y=4", result.Message);
        Assert.Contains("target_z=2", result.Message);
        Assert.Contains("set_cell=498", result.Message);
        Assert.Contains("queued_changes=1", result.ResultToken);
        Assert.Contains("tick_count=12", result.ResultToken);
        Assert.Contains("wildfire_command_request command=qa-delta-stimulus", logSink.InfoMessages);
        Assert.Contains(result.ResultToken, logSink.InfoMessages);
    }

    [Fact]
    public void ExecuteQaBuildingBurnoutStimulusQueuesBoundStimulusAndReportsTargetFields()
    {
        TimberbornQaCommandState state = new(
            IsSimulatorIntegrated: true,
            IsGameContextRuntimeLoaded: true,
            Width: 8,
            Height: 6,
            Depth: 4,
            TickCount: 12,
            QueuedChangeCount: 2,
            LastDeltaCount: 0);
        RecordingStateProvider stateProvider = new(state);
        RecordingBuildingBurnoutStimulus buildingBurnoutStimulus = new(
            new TimberbornQaBuildingBurnoutStimulusResult(91, 3, 4, 2, 57, 498, 496, 2));
        RecordingLogSink logSink = new();
        TimberbornQaCommandBridge bridge = new(
            stateProvider,
            NullTimberbornQaDeltaStimulus.Instance,
            buildingBurnoutStimulus,
            logSink);

        TimberbornQaCommandResult result = bridge.Execute("qa-building-burnout-stimulus");

        Assert.True(result.Success);
        Assert.Equal("qa-building-burnout-stimulus", result.Command);
        Assert.Equal(["help", "qa-building-burnout-stimulus", "qa-readiness", "status"], result.KnownCommands);
        Assert.Equal(1, buildingBurnoutStimulus.CallCount);
        Assert.Equal(1, stateProvider.CallCount);
        Assert.Contains("target_index=91", result.Message);
        Assert.Contains("target_x=3", result.Message);
        Assert.Contains("target_y=4", result.Message);
        Assert.Contains("target_z=2", result.Message);
        Assert.Contains("scanned_cells=57", result.Message);
        Assert.Contains("primed_cell=498", result.Message);
        Assert.Contains("set_cell=496", result.Message);
        Assert.Contains("queued_set_cell_changes=2", result.Message);
        Assert.Contains("queued_changes=2", result.ResultToken);
        Assert.Contains("tick_count=12", result.ResultToken);
        Assert.Contains("wildfire_command_request command=qa-building-burnout-stimulus", logSink.InfoMessages);
        Assert.Contains(result.ResultToken, logSink.InfoMessages);
    }

    [Fact]
    public void QueueFixedQaDeltaStimulusRegistersExactlyOneCenterSetCellChange()
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = new(simulator);

        TimberbornQaDeltaStimulusResult result = fireSystem.QueueFixedQaDeltaStimulus();

        Assert.Equal(new TimberbornQaDeltaStimulusResult(38, 2, 3, 1, PackedCell.Pack(15, 15, 3, 0, 1, 1)), result);
        FireSimChange change = Assert.Single(simulator.RegisteredChanges);
        Assert.Equal(38, change.CellIndex);
        Assert.Equal(PackedCell.Pack(15, 15, 3, 0, 1, 1), change.SetCell);
        Assert.Null(change.AddHeat);
        Assert.Null(change.AddFuel);
        Assert.Null(change.SetWater);
        Assert.Null(change.SetFuel);
        Assert.Null(change.SetHeat);
        Assert.Null(change.SetFlammability);
        Assert.Null(change.SetHeatLoss);
        Assert.Null(change.SetTerrain);
        Assert.Equal(1, fireSystem.RegisteredChangeCountSinceLastDispatch);
    }

    [Fact]
    public void QueueBuildingBurnoutQaStimulusRegistersOnlyTargetedSetCellChanges()
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = new(simulator);
        RecordingBuildingBurnoutTargetProvider targetProvider = new(
            new TimberbornQaBuildingBurnoutStimulusTarget(38, 2, 3, 1, 39));

        TimberbornQaBuildingBurnoutStimulusResult result =
            fireSystem.QueueBuildingBurnoutQaStimulus(targetProvider);

        Assert.Equal(
            new TimberbornQaBuildingBurnoutStimulusResult(
                38,
                2,
                3,
                1,
                39,
                PackedCell.Pack(12, 15, 2, 0, 1, 4),
                PackedCell.Pack(0, 15, 2, 0, 1, 4),
                2),
            result);
        Assert.Equal(1, targetProvider.CallCount);
        Assert.Equal([new FireGrid(4, 6, 2)], targetProvider.Grids);
        Assert.Equal(2, simulator.RegisteredChanges.Count);
        Assert.All(simulator.RegisteredChanges, change =>
        {
            Assert.Equal(38, change.CellIndex);
            Assert.Null(change.AddHeat);
            Assert.Null(change.AddFuel);
            Assert.Null(change.SetWater);
            Assert.Null(change.SetFuel);
            Assert.Null(change.SetHeat);
            Assert.Null(change.SetFlammability);
            Assert.Null(change.SetHeatLoss);
            Assert.Null(change.SetTerrain);
            Assert.NotNull(change.SetCell);
        });
        Assert.Equal(PackedCell.Pack(12, 15, 2, 0, 1, 4), simulator.RegisteredChanges[0].SetCell);
        Assert.Equal(PackedCell.Pack(0, 15, 2, 0, 1, 4), simulator.RegisteredChanges[1].SetCell);
        Assert.Equal(2, fireSystem.RegisteredChangeCountSinceLastDispatch);
    }

    [Fact]
    public void FindBuildingBurnoutQaTargetSkipsUnusableCellsAndReportsScannedCount()
    {
        FireGrid grid = new(4, 3, 2);

        TimberbornQaBuildingBurnoutStimulusTarget result =
            TimberbornQaBuildingBurnoutStimulusTargets.FindFirstUsableTarget(
                grid,
                target => target.CellIndex == 13);

        Assert.Equal(new TimberbornQaBuildingBurnoutStimulusTarget(13, 1, 0, 1, 14), result);
    }

    [Fact]
    public void FindBuildingBurnoutQaTargetThrowsWhenNoUsableCellExists()
    {
        FireGrid grid = new(2, 2, 1);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            TimberbornQaBuildingBurnoutStimulusTargets.FindFirstUsableTarget(
                grid,
                static _ => false));

        Assert.Contains("No unpaused pausable Timberborn building cell", exception.Message);
    }

    [Fact]
    public void UnknownCommandFailsWithoutQueryingStateProvider()
    {
        RecordingStateProvider stateProvider = new(
            new TimberbornQaCommandState(IsSimulatorIntegrated: true, TickCount: 99));
        RecordingLogSink logSink = new();
        TimberbornQaCommandBridge bridge = new(stateProvider, logSink);

        TimberbornQaCommandResult result = bridge.Execute("ignite");

        Assert.False(result.Success);
        Assert.Equal("ignite", result.Command);
        Assert.Equal("failure", result.Status);
        Assert.Equal(TimberbornQaCommandState.Placeholder, result.State);
        Assert.Equal(0, stateProvider.CallCount);
        Assert.Contains("Unknown command 'ignite'.", result.Message);
        Assert.Contains(result.ResultToken, logSink.WarningMessages);
    }

    [Theory]
    [InlineData("status;tick")]
    [InlineData("status()")]
    [InlineData("eval")]
    public void DynamicLookingInputIsRejectedAsUnknownCommand(string command)
    {
        RecordingStateProvider stateProvider = new(TimberbornQaCommandState.Placeholder);
        TimberbornQaCommandBridge bridge = new(stateProvider, new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute(command);

        Assert.False(result.Success);
        Assert.Equal(command, result.Command);
        Assert.Equal(0, stateProvider.CallCount);
    }

    [Fact]
    public void ResultTokenIncludesQaStateFields()
    {
        TimberbornQaCommandState state = new(
            IsSimulatorIntegrated: true,
            Width: 4,
            Height: 5,
            Depth: 6,
            TickCount: 7,
            QueuedChangeCount: 8,
            LastDeltaCount: 9,
            LastDeltaConsumerChangedCellCount: 10,
            LastDeltaConsumerDebugVisualUpdatedCellCount: 11,
            LastDeltaConsumerDebugVisualCellCount: 12,
            LastDeltaConsumerStartedBurningCount: 13,
            LastDeltaConsumerFuelDepletedCount: 14,
            LastDeltaConsumerVisualEffectEventCount: 15,
            LastDeltaConsumerGameplayConsequenceCount: 16,
            LastDeltaConsumerBuildingBurnoutConsideredDeltaCount: 17,
            LastDeltaConsumerBuildingBurnoutMatchedCellCount: 18,
            LastDeltaConsumerBuildingBurnoutAppliedConsequenceCount: 19,
            LastDeltaConsumerAlertCount: 20);
        TimberbornQaCommandBridge bridge = new(new RecordingStateProvider(state), new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("status");

        Assert.Contains("wildfire_command_result", result.ResultToken);
        Assert.Contains("command=status", result.ResultToken);
        Assert.Contains("success=true", result.ResultToken);
        Assert.Contains("simulator_integrated=true", result.ResultToken);
        Assert.Contains("width=4", result.ResultToken);
        Assert.Contains("height=5", result.ResultToken);
        Assert.Contains("depth=6", result.ResultToken);
        Assert.Contains("tick_count=7", result.ResultToken);
        Assert.Contains("queued_changes=8", result.ResultToken);
        Assert.Contains("last_delta_count=9", result.ResultToken);
        Assert.Contains("last_delta_consumer_changed_cells=10", result.ResultToken);
        Assert.Contains("last_delta_consumer_debug_visual_updated_cells=11", result.ResultToken);
        Assert.Contains("last_delta_consumer_debug_visual_cells=12", result.ResultToken);
        Assert.Contains("last_delta_consumer_started_burning=13", result.ResultToken);
        Assert.Contains("last_delta_consumer_fuel_depleted=14", result.ResultToken);
        Assert.Contains("last_delta_consumer_visual_effect_events=15", result.ResultToken);
        Assert.Contains("last_delta_consumer_gameplay_consequences=16", result.ResultToken);
        Assert.Contains("last_delta_consumer_building_burnout_considered_deltas=17", result.ResultToken);
        Assert.Contains("last_delta_consumer_building_burnout_matched_cells=18", result.ResultToken);
        Assert.Contains("last_delta_consumer_building_burnout_applied_consequences=19", result.ResultToken);
        Assert.Contains("last_delta_consumer_alerts=20", result.ResultToken);
    }

    [Fact]
    public void PlaceholderStatusTokenNamesMissingSimulatorValues()
    {
        TimberbornQaCommandBridge bridge = new(
            new RecordingStateProvider(TimberbornQaCommandState.Placeholder),
            new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("status");

        Assert.Contains("simulator_integrated=false", result.ResultToken);
        Assert.Contains("tick_count=placeholder", result.ResultToken);
        Assert.Contains("queued_changes=placeholder", result.ResultToken);
        Assert.Contains("last_delta_count=placeholder", result.ResultToken);
    }

    private sealed class RecordingStateProvider(TimberbornQaCommandState state) : ITimberbornQaCommandStateProvider
    {
        public int CallCount { get; private set; }

        public TimberbornQaCommandState GetState()
        {
            CallCount++;
            return state;
        }
    }

    private sealed class RecordingDeltaStimulus(TimberbornQaDeltaStimulusResult result) : ITimberbornQaDeltaStimulus
    {
        public int CallCount { get; private set; }

        public TimberbornQaDeltaStimulusResult QueueFixedDeltaStimulus()
        {
            CallCount++;
            return result;
        }
    }

    private sealed class RecordingBuildingBurnoutStimulus(TimberbornQaBuildingBurnoutStimulusResult result)
        : ITimberbornQaBuildingBurnoutStimulus
    {
        public int CallCount { get; private set; }

        public TimberbornQaBuildingBurnoutStimulusResult QueueBuildingBurnoutStimulus()
        {
            CallCount++;
            return result;
        }
    }

    private sealed class RecordingBuildingBurnoutTargetProvider(TimberbornQaBuildingBurnoutStimulusTarget target)
        : ITimberbornQaBuildingBurnoutStimulusTargetProvider
    {
        public int CallCount { get; private set; }

        public List<FireGrid> Grids { get; } = [];

        public TimberbornQaBuildingBurnoutStimulusTarget FindTarget(FireGrid grid)
        {
            CallCount++;
            Grids.Add(grid);
            return target;
        }
    }

    private sealed class RecordingLogSink : ITimberbornQaCommandLogSink
    {
        public List<string> InfoMessages { get; } = [];

        public List<string> WarningMessages { get; } = [];

        public void Info(string message)
        {
            InfoMessages.Add(message);
        }

        public void Warning(string message)
        {
            WarningMessages.Add(message);
        }
    }

    private sealed class RecordingFireSimulator(int width, int height, int depth) : IGpuFireSimulator
    {
        public int Width { get; } = width;

        public int Height { get; } = height;

        public int Depth { get; } = depth;

        public List<FireSimChange> RegisteredChanges { get; } = [];

        public void RegisterChange(FireSimChange change)
        {
            RegisteredChanges.Add(change);
        }

        public GpuFireStepResult Tick()
        {
            return new GpuFireStepResult([], Tick: 0);
        }

        public IDisposable Subscribe(IFireSimListener listener)
        {
            return NullDisposable.Instance;
        }
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        private NullDisposable()
        {
        }

        public void Dispose()
        {
        }
    }
}
