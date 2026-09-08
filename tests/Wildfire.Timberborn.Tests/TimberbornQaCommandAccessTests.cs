namespace Wildfire.Core.Tests;

public sealed partial class TimberbornQaCommandBridgeTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Theory]
    [InlineData("qa-delta-stimulus")]
    [InlineData("qa-building-burnout-stimulus")]
    [InlineData("qa-water-suppression-stimulus selected")]
    [InlineData("qa-ash-water-stimulus")]
    [InlineData("qa-burn-duration-stimulus")]
    [InlineData("qa-fire-preset harsh")]
    [InlineData("qa-adjust-inventory all-consequences")]
    [InlineData("qa-stored-material-stimulus all")]
    [InlineData("  QA-ADJUST-INVENTORY   stored-materials  ")]
    public void ReleaseDefaultRejectsMutationsBeforeAnyRuntimeCall(string command)
    {
        RecordingStateProvider state = new(TimberbornQaCommandState.Placeholder);
        RecordingDeltaStimulus delta = new(default!);
        RecordingBuildingBurnoutStimulus burnout = new(default!);
        RecordingWaterSuppressionStimulus water = new(default!);
        RecordingBurnDurationStimulus duration = new(default!);
        RecordingFireSimParameterPresetSelector preset = new();
        RecordingSoilMoistureMapProbe soil = new(default!);
        RecordingLogSink log = new();
        RecordingAshCellProbe ash = new(default!);
        RecordingAshWaterStimulus ashWater = new(default!);
        RecordingInventoryAdjuster inventory = new(default!);
        RecordingStoredMaterialStimulus stored = new(default!);
        TimberbornQaCommandBridge bridge = new(
            state, delta, burnout, water, duration, preset, soil, log,
            ash, ashWater, inventory, stored);

        TimberbornQaCommandResult result = bridge.Execute(command);

        output.WriteLine(result.ResultToken);
        Assert.False(result.Success);
        Assert.Equal("qa_mutations_disabled", result.Message);
        Assert.Equal("diagnostics", bridge.AccessMode);
        Assert.Equal(["help", "qa-ash-cell", "qa-readiness", "qa-soil-moisture-range", "status"], result.KnownCommands);
        Assert.Contains(result.ResultToken, log.WarningMessages);
        Assert.Equal(0, state.CallCount);
        Assert.Equal(0, delta.CallCount);
        Assert.Equal(0, burnout.CallCount);
        Assert.Equal(0, water.CallCount);
        Assert.Empty(duration.Targets);
        Assert.Empty(preset.PresetNames);
        Assert.Equal(0, soil.CallCount);
        Assert.Empty(ash.CellIndices);
        Assert.Empty(ashWater.Targets);
        Assert.Empty(inventory.Profiles);
        Assert.Empty(stored.Targets);
    }

    [Theory]
    [InlineData("help")]
    [InlineData("status")]
    [InlineData("qa-readiness")]
    public void ReleaseDefaultKeepsIntentionalDiagnosticsAvailable(string command)
    {
        RecordingStateProvider state = new(TimberbornQaCommandState.Placeholder);
        TimberbornQaCommandBridge bridge = new(state, new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute(command);

        Assert.True(result.Success);
        Assert.Equal(1, state.CallCount);
        if (command == "help")
        {
            Assert.Contains("command_access=diagnostics", result.Message);
        }
    }

    [Theory]
    [InlineData("--wildfire-enable-qa-mutations", TimberbornQaCommandAccess.Development)]
    [InlineData("--wildfire-enable-qa-mutations=false", TimberbornQaCommandAccess.Diagnostics)]
    [InlineData("--WILDFIRE-ENABLE-QA-MUTATIONS", TimberbornQaCommandAccess.Diagnostics)]
    [InlineData("prefix--wildfire-enable-qa-mutations", TimberbornQaCommandAccess.Diagnostics)]
    [InlineData("--wildfire-enable-qa-mutations extra", TimberbornQaCommandAccess.Diagnostics)]
    public void DevelopmentAccessRequiresExactProcessArgument(string argument, TimberbornQaCommandAccess expected)
    {
        Assert.Equal(expected, TimberbornQaCommandPolicy.FromProcessArguments(["Timberborn", argument]));
    }

    [Fact]
    public void MissingOptInAndUnclassifiedFixtureCommandsFailClosed()
    {
        Assert.Equal(TimberbornQaCommandAccess.Diagnostics, TimberbornQaCommandPolicy.FromProcessArguments([]));
        Assert.False(TimberbornQaCommandPolicy.CanExecute(TimberbornQaCommandAccess.Diagnostics, "qa-fixture-readiness"));
        Assert.False(TimberbornQaCommandPolicy.CanExecute((TimberbornQaCommandAccess)42, "qa-adjust-inventory"));
    }
}
