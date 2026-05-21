using Wildfire.Core;
using Wildfire.Timberborn;
using Wildfire.Unity;

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
            new RecordingDeltaStimulus(
                new TimberbornQaDeltaStimulusResult(TimberbornQaFieldTargetSelectors.Default, 0, 0, 0, 0, WildfireMaterialClass.Tree, 1u, 1, 15, 1)),
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
                new TimberbornQaBuildingBurnoutStimulusResult(0, 0, 0, 0, 1, 15, 0, 2)),
            new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("help");

        Assert.True(result.Success);
        Assert.Equal(["help", "qa-building-burnout-stimulus", "qa-readiness", "status"], result.KnownCommands);
        Assert.Contains("qa-building-burnout-stimulus", result.Message);
        Assert.Contains("QA-only simulator change", result.Message);
    }

    [Fact]
    public void WaterSuppressionStimulusBindingExpandsAllowlistAndHelpMessage()
    {
        TimberbornQaCommandBridge bridge = new(
            new RecordingStateProvider(TimberbornQaCommandState.Placeholder),
            NullTimberbornQaDeltaStimulus.Instance,
            NullTimberbornQaBuildingBurnoutStimulus.Instance,
            new RecordingWaterSuppressionStimulus(
                new TimberbornQaWaterSuppressionStimulusResult(TimberbornQaFieldTargetSelectors.Default, 0, 0, 0, 0, WildfireMaterialClass.Tree, 1u, 1, 3, 1)),
            new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("help");

        Assert.True(result.Success);
        Assert.Equal(["help", "qa-readiness", "qa-water-suppression-stimulus", "status"], result.KnownCommands);
        Assert.Contains("qa-water-suppression-stimulus", result.Message);
        Assert.Contains("QA-only simulator change", result.Message);
    }

    [Fact]
    public void BurnDurationStimulusBindingExpandsAllowlistAndHelpMessage()
    {
        TimberbornQaCommandBridge bridge = new(
            new RecordingStateProvider(TimberbornQaCommandState.Placeholder),
            NullTimberbornQaDeltaStimulus.Instance,
            NullTimberbornQaBuildingBurnoutStimulus.Instance,
            NullTimberbornQaWaterSuppressionStimulus.Instance,
            new RecordingBurnDurationStimulus(
                new TimberbornQaBurnDurationStimulusResult(
                    "medium",
                    0,
                    0,
                    0,
                    0,
                    WildfireMaterialClass.Tree,
                    1u,
                    1,
                    9,
                    15,
                    64,
                    12,
                    12)),
            new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("help");

        Assert.True(result.Success);
        Assert.Equal(["help", "qa-burn-duration-stimulus", "qa-readiness", "status"], result.KnownCommands);
        Assert.Contains("qa-burn-duration-stimulus", result.Message);
        Assert.Contains("QA-only simulator change", result.Message);
    }

    [Fact]
    public void FirePresetBindingExpandsAllowlistAndHelpMessage()
    {
        TimberbornQaCommandBridge bridge = new(
            new RecordingStateProvider(TimberbornQaCommandState.Placeholder),
            NullTimberbornQaDeltaStimulus.Instance,
            NullTimberbornQaBuildingBurnoutStimulus.Instance,
            NullTimberbornQaWaterSuppressionStimulus.Instance,
            NullTimberbornQaBurnDurationStimulus.Instance,
            new RecordingFireSimParameterPresetSelector(),
            new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("help");

        Assert.True(result.Success);
        Assert.Equal(["help", "qa-fire-preset", "qa-readiness", "status"], result.KnownCommands);
        Assert.Contains("qa-fire-preset", result.Message);
        Assert.Contains("QA-only simulator change", result.Message);
    }

    [Fact]
    public void SoilMoistureRangeBindingExpandsAllowlistAsReadOnlyAndReportsRange()
    {
        TimberbornQaSoilMoistureRangeResult range = new(
            SampleCount: 12,
            SkippedCount: 2,
            MoistCellCount: 5,
            Min: 0f,
            Max: 7.25f,
            Average: 2.5f,
            MinX: 1,
            MinY: 2,
            MinZ: 3,
            MaxX: 4,
            MaxY: 5,
            MaxZ: 6);
        RecordingSoilMoistureMapProbe probe = new(range);
        RecordingStateProvider stateProvider = new(
            new TimberbornQaCommandState(IsSimulatorIntegrated: true, WildfireEnabled: false));
        TimberbornQaCommandBridge bridge = new(
            stateProvider,
            probe,
            new RecordingLogSink());

        TimberbornQaCommandResult help = bridge.Execute("help");
        TimberbornQaCommandResult result = bridge.Execute("qa-soil-moisture-range");

        Assert.True(help.Success);
        Assert.Equal(["help", "qa-readiness", "qa-soil-moisture-range", "status"], help.KnownCommands);
        Assert.Contains("read-only", help.Message);
        Assert.True(result.Success);
        Assert.Equal("qa-soil-moisture-range", result.Command);
        Assert.Equal(1, probe.CallCount);
        Assert.Contains("soil_moisture_range_samples=12", result.Message);
        Assert.Contains("skipped=2", result.Message);
        Assert.Contains("moist_cells=5", result.Message);
        Assert.Contains("min=0", result.Message);
        Assert.Contains("max=7.25", result.Message);
        Assert.Contains("avg=2.5", result.Message);
        Assert.Contains("min_x=1_min_y=2_min_z=3", result.Message);
        Assert.Contains("max_x=4_max_y=5_max_z=6", result.Message);
        Assert.Equal(2, stateProvider.CallCount);
    }

    [Fact]
    public void ExecuteQaFirePresetSelectsNamedPresetWithoutRawParameters()
    {
        RecordingFireSimParameterPresetSelector selector = new();
        RecordingStateProvider stateProvider = new(
            new TimberbornQaCommandState(IsSimulatorIntegrated: true, WildfireEnabled: true));
        TimberbornQaCommandBridge bridge = new(
            stateProvider,
            NullTimberbornQaDeltaStimulus.Instance,
            NullTimberbornQaBuildingBurnoutStimulus.Instance,
            NullTimberbornQaWaterSuppressionStimulus.Instance,
            NullTimberbornQaBurnDurationStimulus.Instance,
            selector,
            new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("qa-fire-preset slow-reactable");

        Assert.True(result.Success);
        Assert.Equal("qa-fire-preset", result.Command);
        Assert.Equal(["help", "qa-fire-preset", "qa-readiness", "status"], result.KnownCommands);
        Assert.Equal(["slow-reactable"], selector.PresetNames);
        Assert.Contains("selected_fire_sim_preset_name=slow-reactable", result.Message);
        Assert.Contains("ignition=5", result.Message);
        Assert.Equal(2, stateProvider.CallCount);
    }

    [Fact]
    public void ExecuteQaFirePresetRejectsInvalidNamesBeforeSelectorRuns()
    {
        RecordingFireSimParameterPresetSelector selector = new();
        RecordingStateProvider stateProvider = new(TimberbornQaCommandState.Placeholder);
        TimberbornQaCommandBridge bridge = new(
            stateProvider,
            NullTimberbornQaDeltaStimulus.Instance,
            NullTimberbornQaBuildingBurnoutStimulus.Instance,
            NullTimberbornQaWaterSuppressionStimulus.Instance,
            NullTimberbornQaBurnDurationStimulus.Instance,
            selector,
            new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("qa-fire-preset IgnitionPoint=1");

        Assert.False(result.Success);
        Assert.Equal("failure", result.Status);
        Assert.Contains("does not accept arguments", result.Message);
        Assert.Empty(selector.PresetNames);
        Assert.Equal(0, stateProvider.CallCount);
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
        Assert.Contains("wildfire_enabled=true", result.ResultToken);
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
    public void QaReadinessReportsDisabledStateAsNotReady()
    {
        TimberbornQaCommandState state = new(
            IsSimulatorIntegrated: true,
            IsGameContextRuntimeLoaded: true,
            WildfireEnabled: false,
            Width: 4,
            Height: 5,
            Depth: 6,
            TickCount: 7,
            QueuedChangeCount: 0,
            LastDeltaCount: 0);
        TimberbornQaCommandBridge bridge = new(new RecordingStateProvider(state), new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("qa-readiness");

        Assert.True(result.Success);
        Assert.Equal("wildfire_disabled", result.Message);
        Assert.False(result.State.IsLoadedGameReady);
        Assert.Contains("wildfire_enabled=false", result.ResultToken);
        Assert.Contains("loaded_game_ready=false", result.ResultToken);
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
        RecordingDeltaStimulus deltaStimulus = new(
            new TimberbornQaDeltaStimulusResult(
                TimberbornQaFieldTargetSelectors.Default,
                91,
                3,
                4,
                2,
                WildfireMaterialClass.Crop,
                77u,
                498,
                15,
                1,
                TargetSource: "selected_crop_target",
                RegisteredBurnDamageTargetCount: 1,
                RegisteredCropBurnTargetCount: 1,
                RegisteredCropBurnOwnedCellCount: 1,
                SustainedHeatSetCell: 13311,
                SustainedHeatRequestedCycleCount: 12,
                SustainedHeatCompletedCycleCount: 0,
                SustainedHeatRemainingCycleCount: 12,
                SustainedHeatQueuedCycleNumber: 1));
        RecordingLogSink logSink = new();
        TimberbornQaCommandBridge bridge = new(stateProvider, deltaStimulus, logSink);

        TimberbornQaCommandResult result = bridge.Execute("qa-delta-stimulus");

        Assert.True(result.Success);
        Assert.Equal("qa-delta-stimulus", result.Command);
        Assert.Equal(["help", "qa-delta-stimulus", "qa-readiness", "status"], result.KnownCommands);
        Assert.Equal(1, deltaStimulus.CallCount);
        Assert.Equal([TimberbornQaFieldTargetSelectors.Default], deltaStimulus.TargetSelectors);
        Assert.Equal(2, stateProvider.CallCount);
        Assert.Contains("target_selector=burnable", result.Message);
        Assert.Contains("target_index=91", result.Message);
        Assert.Contains("target_x=3", result.Message);
        Assert.Contains("target_y=4", result.Message);
        Assert.Contains("target_z=2", result.Message);
        Assert.Contains("target_material=Crop", result.Message);
        Assert.Contains("companion_target_id=77", result.Message);
        Assert.Contains("initial_cell=498", result.Message);
        Assert.Contains("set_heat=15", result.Message);
        Assert.Contains("target_source=selected_crop_target", result.Message);
        Assert.Contains("registered_crop_burn_targets=1", result.Message);
        Assert.Contains("sustained_heat_requested_cycles=12", result.Message);
        Assert.Contains("queued_changes=1", result.ResultToken);
        Assert.Contains("tick_count=12", result.ResultToken);
        Assert.Contains("wildfire_command_request command=qa-delta-stimulus", logSink.InfoMessages);
        Assert.Contains(result.ResultToken, logSink.InfoMessages);
    }

    [Fact]
    public void ExecuteQaDeltaStimulusReportsBeaverExposureTargetFields()
    {
        RecordingDeltaStimulus deltaStimulus = new(
            new TimberbornQaDeltaStimulusResult(
                TimberbornQaFieldTargetSelectors.BeaverExposure,
                91,
                3,
                4,
                2,
                WildfireMaterialClass.Unknown,
                0u,
                0,
                15,
                1,
                TargetSource: "beaver_candidate_cell",
                SustainedHeatSetCell: 13311,
                SustainedHeatRequestedCycleCount: 12,
                SustainedHeatCompletedCycleCount: 0,
                SustainedHeatRemainingCycleCount: 12,
                SustainedHeatQueuedCycleNumber: 1,
                BeaverExposureTargetBeaverId: "beaver-1",
                BeaverExposureTargetBeaverX: 3,
                BeaverExposureTargetBeaverY: 4,
                BeaverExposureTargetBeaverZ: 2,
                BeaverExposureTargetCandidateCells: 9,
                BeaverExposureTargetSampledBeavers: 27,
                BeaverExposureTargetSkippedNoPositionApi: 0,
                BeaverExposureTargetSkippedBoundedSampling: 0));
        TimberbornQaCommandBridge bridge = new(
            new RecordingStateProvider(new TimberbornQaCommandState(IsSimulatorIntegrated: true, WildfireEnabled: true)),
            deltaStimulus,
            new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("qa-delta-stimulus beaver-exposure");

        Assert.True(result.Success);
        Assert.Equal([TimberbornQaFieldTargetSelectors.BeaverExposure], deltaStimulus.TargetSelectors);
        Assert.Contains("target_selector=beaver-exposure", result.Message);
        Assert.Contains("target_source=beaver_candidate_cell", result.Message);
        Assert.Contains("beaver_exposure_target_beaver_id=beaver-1", result.Message);
        Assert.Contains("beaver_exposure_target_beaver_x=3", result.Message);
        Assert.Contains("beaver_exposure_target_beaver_y=4", result.Message);
        Assert.Contains("beaver_exposure_target_beaver_z=2", result.Message);
        Assert.Contains("beaver_exposure_target_candidate_cells=9", result.Message);
        Assert.Contains("beaver_exposure_target_sampled_beavers=27", result.Message);
        Assert.Contains("beaver_exposure_target_skipped_no_position_api=0", result.Message);
        Assert.Contains("beaver_exposure_target_skipped_bounded_sampling=0", result.Message);
    }

    [Fact]
    public void ExecuteQaDeltaStimulusReportsTaintedAshStimulusFields()
    {
        RecordingDeltaStimulus deltaStimulus = new(
            new TimberbornQaDeltaStimulusResult(
                TimberbornQaFieldTargetSelectors.TaintedAsh,
                91,
                3,
                4,
                2,
                WildfireMaterialClass.Tree,
                77u,
                498,
                SetHeat: 0,
                QueuedHeatChangeCount: 0,
                SetAsh: 3,
                SetAshContamination: 7,
                QueuedAshChangeCount: 1,
                TargetSource: "qa_tainted_ash_field"));
        TimberbornQaCommandBridge bridge = new(
            new RecordingStateProvider(new TimberbornQaCommandState(IsSimulatorIntegrated: true, WildfireEnabled: true)),
            deltaStimulus,
            new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("qa-delta-stimulus tainted-ash");

        Assert.True(result.Success);
        Assert.Equal([TimberbornQaFieldTargetSelectors.TaintedAsh], deltaStimulus.TargetSelectors);
        Assert.Contains("target_selector=tainted-ash", result.Message);
        Assert.Contains("set_heat=0", result.Message);
        Assert.Contains("set_ash=3", result.Message);
        Assert.Contains("set_ash_contamination=7", result.Message);
        Assert.Contains("queued_heat_changes=0", result.Message);
        Assert.Contains("queued_ash_changes=1", result.Message);
        Assert.Contains("target_source=qa_tainted_ash_field", result.Message);
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
            new TimberbornQaBuildingBurnoutStimulusResult(91, 3, 4, 2, 57, 15, 0, 2));
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
        Assert.Equal(2, stateProvider.CallCount);
        Assert.Contains("target_index=91", result.Message);
        Assert.Contains("target_x=3", result.Message);
        Assert.Contains("target_y=4", result.Message);
        Assert.Contains("target_z=2", result.Message);
        Assert.Contains("scanned_cells=57", result.Message);
        Assert.Contains("set_heat=15", result.Message);
        Assert.Contains("set_fuel=0", result.Message);
        Assert.Contains("queued_field_changes=2", result.Message);
        Assert.Contains("queued_changes=2", result.ResultToken);
        Assert.Contains("tick_count=12", result.ResultToken);
        Assert.Contains("wildfire_command_request command=qa-building-burnout-stimulus", logSink.InfoMessages);
        Assert.Contains(result.ResultToken, logSink.InfoMessages);
    }

    [Fact]
    public void ExecuteQaWaterSuppressionStimulusQueuesBoundStimulusAndReportsTargetFields()
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
        RecordingWaterSuppressionStimulus waterSuppressionStimulus = new(
            new TimberbornQaWaterSuppressionStimulusResult(
                TimberbornQaFieldTargetSelectors.Default,
                91,
                3,
                4,
                2,
                WildfireMaterialClass.Tree,
                77u,
                498,
                3,
                1,
                TargetSoilContamination: 6,
                IsAffectedCellContaminated: true,
                IsContaminatedSuppressionInput: false,
                IsBadwaterSuppressionInput: false,
                WaterSuppressionInputSafeUnavailableCount: 1,
                NativeDecontaminationAttemptCount: 0));
        RecordingLogSink logSink = new();
        TimberbornQaCommandBridge bridge = new(
            stateProvider,
            NullTimberbornQaDeltaStimulus.Instance,
            NullTimberbornQaBuildingBurnoutStimulus.Instance,
            waterSuppressionStimulus,
            logSink);

        TimberbornQaCommandResult result = bridge.Execute("qa-water-suppression-stimulus");

        Assert.True(result.Success);
        Assert.Equal("qa-water-suppression-stimulus", result.Command);
        Assert.Equal(["help", "qa-readiness", "qa-water-suppression-stimulus", "status"], result.KnownCommands);
        Assert.Equal(1, waterSuppressionStimulus.CallCount);
        Assert.Equal([TimberbornQaFieldTargetSelectors.Default], waterSuppressionStimulus.TargetSelectors);
        Assert.Equal(2, stateProvider.CallCount);
        Assert.Contains("target_selector=burnable", result.Message);
        Assert.Contains("target_index=91", result.Message);
        Assert.Contains("target_x=3", result.Message);
        Assert.Contains("target_y=4", result.Message);
        Assert.Contains("target_z=2", result.Message);
        Assert.Contains("target_material=Tree", result.Message);
        Assert.Contains("companion_target_id=77", result.Message);
        Assert.Contains("target_soil_contamination=6", result.Message);
        Assert.Contains("affected_cell_contaminated=true", result.Message);
        Assert.Contains("contaminated_suppression_input=false", result.Message);
        Assert.Contains("badwater_suppression_input=false", result.Message);
        Assert.Contains("water_suppression_input_safe_unavailable=1", result.Message);
        Assert.Contains("native_decontamination_attempts=0", result.Message);
        Assert.Contains("initial_cell=498", result.Message);
        Assert.Contains("set_water=3", result.Message);
        Assert.Contains("queued_water_changes=1", result.Message);
        Assert.Contains("queued_changes=1", result.ResultToken);
        Assert.Contains("tick_count=12", result.ResultToken);
        Assert.Contains("wildfire_command_request command=qa-water-suppression-stimulus", logSink.InfoMessages);
        Assert.Contains(result.ResultToken, logSink.InfoMessages);
    }

    [Theory]
    [InlineData("qa-delta-stimulus tree", "tree")]
    [InlineData("qa-delta-stimulus contaminated-tree", "contaminated-tree")]
    [InlineData("qa-delta-stimulus tainted-ash", "tainted-ash")]
    [InlineData("qa-delta-stimulus selected-tree", "selected-tree")]
    [InlineData("qa-delta-stimulus beaver-exposure", "beaver-exposure")]
    [InlineData("qa-delta-stimulus infrastructure", "infrastructure")]
    [InlineData("qa-delta-stimulus dynamite", "dynamite")]
    [InlineData("qa-delta-stimulus detonator", "detonator")]
    [InlineData("qa-delta-stimulus tunnel", "tunnel")]
    [InlineData("qa-delta-stimulus path-infrastructure", "path-infrastructure")]
    [InlineData("qa-delta-stimulus power-infrastructure", "power-infrastructure")]
    [InlineData("qa-delta-stimulus water-infrastructure", "water-infrastructure")]
    [InlineData("qa-water-suppression-stimulus storage", "storage")]
    [InlineData("qa-water-suppression-stimulus contaminated-tree", "contaminated-tree")]
    public void ImportedFieldStimulusCommandsAcceptAllowlistedTargetSelectors(string command, string selector)
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
        RecordingDeltaStimulus deltaStimulus = new(
            new TimberbornQaDeltaStimulusResult(selector, 91, 3, 4, 2, WildfireMaterialClass.Tree, 77u, 498, 15, 1));
        RecordingWaterSuppressionStimulus waterSuppressionStimulus = new(
            new TimberbornQaWaterSuppressionStimulusResult(selector, 91, 3, 4, 2, WildfireMaterialClass.Storage, 77u, 498, 3, 1));
        TimberbornQaCommandBridge bridge = new(
            stateProvider,
            deltaStimulus,
            NullTimberbornQaBuildingBurnoutStimulus.Instance,
            waterSuppressionStimulus,
            new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute(command);

        Assert.True(result.Success);
        Assert.Contains($"target_selector={selector}", result.Message);
        Assert.Equal(
            StringComparer.OrdinalIgnoreCase.Equals(result.Command, "qa-delta-stimulus") ? [selector] : [],
            deltaStimulus.TargetSelectors);
        Assert.Equal(
            StringComparer.OrdinalIgnoreCase.Equals(result.Command, "qa-water-suppression-stimulus") ? [selector] : [],
            waterSuppressionStimulus.TargetSelectors);
    }

    [Fact]
    public void WaterSuppressionStimulusRejectsDirectConsequenceTargetSelectors()
    {
        RecordingWaterSuppressionStimulus waterSuppressionStimulus = new(
            new TimberbornQaWaterSuppressionStimulusResult(
                TimberbornQaFieldTargetSelectors.Dynamite,
                91,
                3,
                4,
                2,
                WildfireMaterialClass.Infrastructure,
                77u,
                498,
                3,
                1));
        TimberbornQaCommandBridge bridge = new(
            new RecordingStateProvider(new TimberbornQaCommandState(IsSimulatorIntegrated: true, WildfireEnabled: true)),
            NullTimberbornQaDeltaStimulus.Instance,
            NullTimberbornQaBuildingBurnoutStimulus.Instance,
            waterSuppressionStimulus,
            new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("qa-water-suppression-stimulus dynamite");

        Assert.False(result.Success);
        Assert.Contains("does not accept arguments", result.Message);
        Assert.Equal(0, waterSuppressionStimulus.CallCount);
    }

    [Fact]
    public void ExecuteQaBurnDurationStimulusQueuesNamedTargetAndReportsProofFields()
    {
        TimberbornQaCommandState state = new(
            IsSimulatorIntegrated: true,
            IsGameContextRuntimeLoaded: true,
            Width: 8,
            Height: 6,
            Depth: 4,
            TickCount: 12,
            QueuedChangeCount: 1,
            LastDeltaCount: 0,
            BurnDurationProofTarget: "high",
            BurnDurationProofTargetIndex: 91,
            BurnDurationProofTargetX: 3,
            BurnDurationProofTargetY: 4,
            BurnDurationProofTargetZ: 2,
            BurnDurationProofInitialFuel: 15,
            BurnDurationProofQueuedTick: 11,
            BurnDurationProofTimeoutTicks: 64,
            BurnDurationProofSustainedHeatTicks: 12,
            BurnDurationProofSustainedHeatAppliedTicks: 0,
            BurnDurationProofSustainedHeatComplete: false,
            BurnDurationProofStatus: "queued");
        RecordingStateProvider stateProvider = new(state);
        RecordingBurnDurationStimulus burnDurationStimulus = new(
            new TimberbornQaBurnDurationStimulusResult(
                "high",
                91,
                3,
                4,
                2,
                WildfireMaterialClass.Tree,
                77u,
                13311,
                15,
                15,
                64,
                12,
                12));
        RecordingLogSink logSink = new();
        TimberbornQaCommandBridge bridge = new(
            stateProvider,
            NullTimberbornQaDeltaStimulus.Instance,
            NullTimberbornQaBuildingBurnoutStimulus.Instance,
            NullTimberbornQaWaterSuppressionStimulus.Instance,
            burnDurationStimulus,
            logSink);

        TimberbornQaCommandResult result = bridge.Execute("qa-burn-duration-stimulus high");

        Assert.True(result.Success);
        Assert.Equal("qa-burn-duration-stimulus", result.Command);
        Assert.Equal(["help", "qa-burn-duration-stimulus", "qa-readiness", "status"], result.KnownCommands);
        Assert.Equal(["high"], burnDurationStimulus.Targets);
        Assert.Equal(2, stateProvider.CallCount);
        Assert.Contains("target=high", result.Message);
        Assert.Contains("target_index=91", result.Message);
        Assert.Contains("target_x=3", result.Message);
        Assert.Contains("target_y=4", result.Message);
        Assert.Contains("target_z=2", result.Message);
        Assert.Contains("target_material=Tree", result.Message);
        Assert.Contains("companion_target_id=77", result.Message);
        Assert.Contains("initial_cell=13311", result.Message);
        Assert.Contains("initial_fuel=15", result.Message);
        Assert.Contains("set_heat=15", result.Message);
        Assert.Contains("timeout_ticks=64", result.Message);
        Assert.Contains("sustained_heat_ticks=12", result.Message);
        Assert.Contains("queued_heat_changes=12", result.Message);
        Assert.Contains("burn_duration_proof_target=high", result.ResultToken);
        Assert.Contains("burn_duration_proof_initial_fuel=15", result.ResultToken);
        Assert.Contains("burn_duration_proof_sustained_heat_ticks=12", result.ResultToken);
        Assert.Contains("burn_duration_proof_sustained_heat_applied_ticks=0", result.ResultToken);
        Assert.Contains("burn_duration_proof_sustained_heat_complete=false", result.ResultToken);
        Assert.Contains("burn_duration_proof_status=queued", result.ResultToken);
        Assert.Contains("wildfire_command_request command=qa-burn-duration-stimulus", logSink.InfoMessages);
        Assert.Contains(result.ResultToken, logSink.InfoMessages);
    }

    [Fact]
    public void QueueQaDeltaStimulusRegistersExactlyOneImportedTargetIgnitionChange()
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            new TimberbornResourceAdapter().CreateTreeSource(2, 3, 1, materialTargetId: 77u));

        TimberbornQaDeltaStimulusResult result = fireSystem.QueueQaDeltaStimulus();

        Assert.Equal(38, result.CellIndex);
        Assert.Equal(WildfireMaterialClass.Tree, result.MaterialClass);
        Assert.Equal(77u, result.CompanionTargetId);
        Assert.Equal((byte)15, result.SetHeat);
        FireSimChange change = Assert.Single(simulator.RegisteredChanges);
        Assert.Equal(38, change.CellIndex);
        Assert.Null(change.SetCell);
        Assert.Null(change.AddHeat);
        Assert.Null(change.AddFuel);
        Assert.Null(change.SetWater);
        Assert.Null(change.SetFuel);
        Assert.Equal((byte)15, change.SetHeat);
        Assert.Null(change.SetFlammability);
        Assert.Null(change.SetBurningLevel);
        Assert.Null(change.SetTerrain);
        Assert.Equal(1, fireSystem.RegisteredChangeCountSinceLastDispatch);
    }

    [Fact]
    public void QueueQaDeltaStimulusTaintedAshQueuesSimulatorAshContaminationOnly()
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            new TimberbornResourceAdapter().CreateTreeSource(2, 3, 1, materialTargetId: 77u));

        TimberbornQaDeltaStimulusResult result =
            fireSystem.QueueQaDeltaStimulus(TimberbornQaFieldTargetSelectors.TaintedAsh);

        Assert.Equal(TimberbornQaFieldTargetSelectors.TaintedAsh, result.TargetSelector);
        Assert.Equal(38, result.CellIndex);
        Assert.Equal(WildfireMaterialClass.Tree, result.MaterialClass);
        Assert.Equal("qa_tainted_ash_field", result.TargetSource);
        Assert.Equal(0, result.SetHeat);
        Assert.Equal(0, result.QueuedHeatChangeCount);
        Assert.Equal((byte)3, result.SetAsh);
        Assert.Equal((byte)7, result.SetAshContamination);
        Assert.Equal(1, result.QueuedAshChangeCount);
        FireSimChange change = Assert.Single(simulator.RegisteredChanges);
        Assert.Equal(38, change.CellIndex);
        Assert.Null(change.SetCell);
        Assert.Null(change.AddHeat);
        Assert.Null(change.AddFuel);
        Assert.Null(change.SetWater);
        Assert.Null(change.SetFuel);
        Assert.Null(change.SetHeat);
        Assert.Null(change.SetFlammability);
        Assert.Null(change.SetBurningLevel);
        Assert.Null(change.SetTerrain);
        Assert.Equal((byte)3, change.SetAsh);
        Assert.Equal((byte)7, change.SetAshContamination);
        Assert.Equal(1, fireSystem.RegisteredChangeCountSinceLastDispatch);
    }

    [Fact]
    public void QueueQaDeltaStimulusBeaverExposureQueuesFullFieldStateAtSampledCandidateCells()
    {
        RecordingFireSimulator simulator = new(width: 5, height: 5, depth: 2);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(simulator);
        FireGrid grid = new(simulator.Width, simulator.Height, simulator.Depth);
        int beaverCellIndex = grid.ToIndex(2, 3, 1);
        int[] candidateCellIndices =
        [
            grid.ToIndex(1, 2, 1),
            beaverCellIndex,
            grid.ToIndex(3, 4, 1),
        ];

        TimberbornQaDeltaStimulusResult result = fireSystem.QueueQaDeltaStimulus(
            TimberbornQaFieldTargetSelectors.BeaverExposure,
            beaverExposureTarget: TimberbornBeaverFieldExposureQaTarget.Available(
                "beaver-1",
                beaverX: 2,
                beaverY: 3,
                beaverZ: 1,
                cellIndex: beaverCellIndex,
                x: 2,
                y: 3,
                z: 1,
                candidateCellCount: candidateCellIndices.Length,
                sampledBeaverCount: 1,
                skippedBoundedSamplingCount: 0,
                cellIndices: candidateCellIndices));

        Assert.Equal(TimberbornQaFieldTargetSelectors.BeaverExposure, result.TargetSelector);
        Assert.Equal(beaverCellIndex, result.CellIndex);
        Assert.Equal(2, result.X);
        Assert.Equal(3, result.Y);
        Assert.Equal(1, result.Z);
        Assert.Equal("beaver_candidate_cell", result.TargetSource);
        Assert.Equal("beaver-1", result.BeaverExposureTargetBeaverId);
        Assert.Equal(candidateCellIndices.Length, result.BeaverExposureTargetCandidateCells);
        Assert.Equal(1, result.BeaverExposureTargetSampledBeavers);
        Assert.Equal(PackedCell.Heat((ushort)result.SustainedHeatSetCell!.Value), result.SetHeat);
        Assert.Equal(12, result.SustainedHeatRequestedCycleCount);
        Assert.Equal(12, result.SustainedHeatRemainingCycleCount);
        Assert.Equal(candidateCellIndices.Length, simulator.RegisteredChanges.Count);
        Assert.Equal(candidateCellIndices, simulator.RegisteredChanges.Select(static change => change.CellIndex).ToArray());
        FireSimChange change = simulator.RegisteredChanges[1];
        ushort queuedCell = change.SetCell ?? throw new InvalidOperationException("Expected a queued full field state.");
        Assert.Equal((ushort)result.SustainedHeatSetCell.Value, queuedCell);
        Assert.All(simulator.RegisteredChanges, registeredChange => Assert.Null(registeredChange.SetHeat));
        FireVisualSample visualSample = FireVisualField.FromPackedCell(queuedCell);
        Assert.True(visualSample.Smoke >= TimberbornBeaverFieldExposureTelemetry.RespiratorySmokeThreshold);
        Assert.True(visualSample.Smoke < TimberbornBeaverFieldExposureTelemetry.ToxicSmokeThreshold);
        Assert.True(visualSample.Fire < TimberbornBeaverFieldExposureTelemetry.BurnFireThreshold);
    }

    [Fact]
    public void QueueQaDeltaStimulusBeaverExposureDoesNotQueueWhenPositionSamplingIsUnavailable()
    {
        RecordingFireSimulator simulator = new(width: 5, height: 5, depth: 2);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(simulator);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => fireSystem.QueueQaDeltaStimulus(
                TimberbornQaFieldTargetSelectors.BeaverExposure,
                beaverExposureTarget: TimberbornBeaverFieldExposureQaTarget.Unavailable(
                    "position_api_unavailable",
                    skippedNoPositionApiCount: 1)));

        Assert.Contains("position_api_unavailable", exception.Message);
        Assert.Empty(simulator.RegisteredChanges);
    }

    [Fact]
    public void QueueQaDeltaStimulusTargetsRegisteredCropDepthBeforeCenterFallback()
    {
        RecordingFireSimulator simulator = new(width: 50, height: 50, depth: 23);
        TimberbornFireSystem fireSystem = new(simulator);
        FireGrid grid = new(50, 50, 23);
        int cropCellIndex = grid.ToIndex(25, 25, 3);
        TimberbornBurnDamageTargetState cropTarget = CreateOrganicBurnDamageState(
            "crop-kohlrabi-25-25-3",
            "Crop.Kohlrabi",
            TimberbornBurnDamageTargetKind.Crop,
            [cropCellIndex, grid.ToIndex(25, 26, 3)],
            ["Kohlrabi"]);
        TimberbornBurnDamageTargetState treeTarget = CreateOrganicBurnDamageState(
            "tree-center",
            "Tree.Pine",
            TimberbornBurnDamageTargetKind.Tree,
            [grid.ToIndex(25, 25, 4)],
            ["Log"]);

        TimberbornQaDeltaStimulusResult result = fireSystem.QueueQaDeltaStimulus(
            targetSelector: TimberbornQaFieldTargetSelectors.Crop,
            burnDamageTargets: new Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState>
            {
                [treeTarget.TargetKey] = treeTarget,
                [cropTarget.TargetKey] = cropTarget,
            });

        Assert.Equal(cropCellIndex, result.CellIndex);
        Assert.Equal(25, result.X);
        Assert.Equal(25, result.Y);
        Assert.Equal(3, result.Z);
        Assert.Equal("registered_crop_target", result.TargetSource);
        Assert.Equal(2, result.RegisteredBurnDamageTargetCount);
        Assert.Equal(1, result.RegisteredCropBurnTargetCount);
        Assert.Equal(2, result.RegisteredCropBurnOwnedCellCount);
        Assert.Equal("crop-kohlrabi-25-25-3", result.BurnDamageTargetKey);
        Assert.Equal("Crop.Kohlrabi", result.BurnDamageSpecId);
        Assert.Equal(TimberbornBurnDamageTargetKind.Crop, result.BurnDamageTargetKind);
        Assert.Equal(2, result.BurnDamageRemainingCapacity);
        Assert.Equal((byte?)1, result.BurnDamageProbeFuel);
        Assert.Equal((byte?)0, result.BurnDamageSpendFuel);
        Assert.Equal(2, result.QueuedHeatChangeCount);
        Assert.Null(result.SustainedHeatRequestedCycleCount);
        Assert.Null(result.SustainedHeatRemainingCycleCount);
        FireSimChange change = Assert.Single(simulator.RegisteredChanges);
        Assert.Equal(cropCellIndex, change.CellIndex);
        Assert.Null(change.SetWater);
        Assert.Equal((byte)1, change.SetFuel);
        Assert.Equal((byte)15, change.SetHeat);
        Assert.Equal((byte)3, change.SetFlammability);
        Assert.Null(change.SetBurningLevel);
        Assert.Null(change.SetTerrain);
    }

    [Fact]
    public void QueueQaDeltaStimulusTargetsSelectedCropBeforeAutomaticRegisteredCrop()
    {
        RecordingFireSimulator simulator = new(width: 50, height: 50, depth: 23);
        TimberbornFireSystem fireSystem = new(simulator);
        FireGrid grid = new(50, 50, 23);
        int automaticCropCellIndex = grid.ToIndex(25, 25, 3);
        int selectedCropCellIndex = grid.ToIndex(7, 8, 2);
        TimberbornBurnDamageTargetState automaticCropTarget = CreateOrganicBurnDamageState(
            "crop-kohlrabi-auto",
            "Crop.Kohlrabi",
            TimberbornBurnDamageTargetKind.Crop,
            [automaticCropCellIndex],
            ["Kohlrabi"]);
        TimberbornBurnDamageTargetState selectedCropTarget = CreateOrganicBurnDamageState(
            "crop-kohlrabi-selected",
            "Crop.Kohlrabi",
            TimberbornBurnDamageTargetKind.Crop,
            [selectedCropCellIndex],
            ["Kohlrabi"]);

        TimberbornQaDeltaStimulusResult result = fireSystem.QueueQaDeltaStimulus(
            targetSelector: TimberbornQaFieldTargetSelectors.Crop,
            burnDamageTargets: new Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState>
            {
                [automaticCropTarget.TargetKey] = automaticCropTarget,
                [selectedCropTarget.TargetKey] = selectedCropTarget,
            },
            selectedCropTarget: new TimberbornQaSelectedCropTarget(
                selectedCropCellIndex,
                7,
                8,
                2,
                "selected_crop_target"));

        Assert.Equal(selectedCropCellIndex, result.CellIndex);
        Assert.Equal("selected_crop_target", result.TargetSource);
        Assert.Equal("crop-kohlrabi-selected", result.BurnDamageTargetKey);
        Assert.Equal(TimberbornBurnDamageTargetKind.Crop, result.BurnDamageTargetKind);
        Assert.Equal(2, result.QueuedHeatChangeCount);
        Assert.Equal(2, result.RegisteredCropBurnTargetCount);
        FireSimChange change = Assert.Single(simulator.RegisteredChanges);
        Assert.Equal(selectedCropCellIndex, change.CellIndex);
        Assert.Null(change.SetWater);
        Assert.Equal((byte)1, change.SetFuel);
        Assert.Equal((byte)15, change.SetHeat);
        Assert.Equal((byte)3, change.SetFlammability);
        Assert.Null(change.SetBurningLevel);
        Assert.Null(change.SetTerrain);
    }

    [Fact]
    public void QueueQaDeltaStimulusBushTargetsRegisteredHarvestableResource()
    {
        RecordingFireSimulator simulator = new(width: 50, height: 50, depth: 23);
        TimberbornFireSystem fireSystem = new(simulator);
        FireGrid grid = new(50, 50, 23);
        int cropCellIndex = grid.ToIndex(25, 25, 3);
        int bushCellIndex = grid.ToIndex(8, 9, 2);
        TimberbornBurnDamageTargetState cropTarget = CreateOrganicBurnDamageState(
            "crop-kohlrabi",
            "Crop.Kohlrabi",
            TimberbornBurnDamageTargetKind.Crop,
            [cropCellIndex],
            ["Kohlrabi"]);
        TimberbornBurnDamageTargetState bushTarget = CreateOrganicBurnDamageState(
            "resource-blueberry-bush",
            "BlueberryBush",
            TimberbornBurnDamageTargetKind.Resource,
            [bushCellIndex],
            ["Blueberry"]);

        TimberbornQaDeltaStimulusResult result = fireSystem.QueueQaDeltaStimulus(
            targetSelector: TimberbornQaFieldTargetSelectors.Bush,
            burnDamageTargets: new Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState>
            {
                [cropTarget.TargetKey] = cropTarget,
                [bushTarget.TargetKey] = bushTarget,
            });

        Assert.Equal(TimberbornQaFieldTargetSelectors.Bush, result.TargetSelector);
        Assert.Equal(bushCellIndex, result.CellIndex);
        Assert.Equal("resource-blueberry-bush", result.BurnDamageTargetKey);
        Assert.Equal("BlueberryBush", result.BurnDamageSpecId);
        Assert.Equal(TimberbornBurnDamageTargetKind.Resource, result.BurnDamageTargetKind);
        Assert.Equal(2, result.RegisteredCropBurnTargetCount);
        FireSimChange change = Assert.Single(simulator.RegisteredChanges);
        Assert.Equal(bushCellIndex, change.CellIndex);
        Assert.Equal((byte)1, change.SetFuel);
        Assert.Equal((byte)15, change.SetHeat);
    }

    [Fact]
    public void QaDeltaStimulusSustainsMaxHeatSetCellAcrossTwelveSuccessfulDispatches()
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = new(simulator);
        FireGrid grid = new(4, 6, 2);
        int selectedCropCellIndex = grid.ToIndex(1, 3, 1);
        TimberbornBurnDamageTargetState selectedCropTarget = CreateOrganicBurnDamageState(
            "crop-kohlrabi-selected",
            "Crop.Kohlrabi",
            TimberbornBurnDamageTargetKind.Crop,
            [selectedCropCellIndex],
            ["Kohlrabi"]);
        TimberbornQaDeltaStimulusResult queued = fireSystem.QueueQaDeltaStimulus(
            burnDamageTargets: new Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState>
            {
                [selectedCropTarget.TargetKey] = selectedCropTarget,
            });

        Enumerable.Range(0, 12)
            .ToList()
            .ForEach(_ => fireSystem.Tick());

        TimberbornQaDeltaStimulusSustainedHeatState state =
            Assert.IsType<TimberbornQaDeltaStimulusSustainedHeatState>(
                fireSystem.QaDeltaStimulusSustainedHeatState);
        Assert.Equal(12, simulator.TickCallCount);
        Assert.Equal(12, simulator.RegisteredChanges.Count);
        Assert.All(simulator.RegisteredChanges, change =>
        {
            Assert.Equal(queued.CellIndex, change.CellIndex);
            Assert.Null(change.SetCell);
            Assert.Equal((byte)15, change.SetHeat);
        });
        Assert.Equal(12, state.RequestedCycleCount);
        Assert.Equal(12, state.CompletedCycleCount);
        Assert.Equal(0, state.RemainingCycleCount);
        Assert.Null(state.QueuedCycleNumber);
        Assert.Equal(12u, state.LastCompletedTick);
        Assert.False(state.IsActive);
    }

    [Fact]
    public void SelectedBlueberryHarvestableResolvesToRegisteredOrganicResourceTarget()
    {
        FireGrid grid = new(50, 50, 23);
        int selectedBlueberryCellIndex = grid.ToIndex(11, 12, 2);
        TimberbornBurnDamageTargetState blueberryTarget = CreateOrganicBurnDamageState(
            "harvestable-blueberry-selected",
            "BlueberryBush",
            TimberbornBurnDamageTargetKind.Resource,
            [selectedBlueberryCellIndex],
            ["Berries"]);

        TimberbornQaSelectedCropTarget target = TimberbornSelectedCropTargetProvider.ResolveSelectedTarget(
            grid,
            new Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState>
            {
                [blueberryTarget.TargetKey] = blueberryTarget,
            },
            [selectedBlueberryCellIndex]);

        Assert.Equal(new TimberbornQaSelectedCropTarget(
            selectedBlueberryCellIndex,
            11,
            12,
            2,
            "selected_crop_target"), target);
    }

    [Fact]
    public void SelectedCuttableLogResourceDoesNotResolveAsCropTarget()
    {
        FireGrid grid = new(50, 50, 23);
        int selectedLogCellIndex = grid.ToIndex(11, 12, 2);
        TimberbornBurnDamageTargetState logTarget = CreateOrganicBurnDamageState(
            "cuttable-oak-log-selected",
            "Tree.Oak",
            TimberbornBurnDamageTargetKind.Resource,
            [selectedLogCellIndex],
            ["Log"]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            TimberbornSelectedCropTargetProvider.ResolveSelectedTarget(
                grid,
                new Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState>
                {
                    [logTarget.TargetKey] = logTarget,
                },
                [selectedLogCellIndex]));

        Assert.Equal(
            "Selected Timberborn object did not resolve to a registered crop or harvestable burn-damage target.",
            exception.Message);
    }

    [Fact]
    public void QueueQaDeltaStimulusInfrastructureProbeRequiresTwf075OwnedTarget()
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            new TimberbornBuildingAdapter().CreateNonBurnableSource(2, 3, 1) with
            {
                CompanionTargetId = 77u,
            });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => fireSystem.QueueQaDeltaStimulus(TimberbornQaFieldTargetSelectors.Infrastructure));

        Assert.Contains("requires registered TWF-075 burn-damage targets", exception.Message);
        Assert.Empty(simulator.RegisteredChanges);
        Assert.Equal(0, fireSystem.RegisteredChangeCountSinceLastDispatch);
    }

    [Fact]
    public void QueueQaDeltaStimulusInfrastructureStagesBurnDamageFuelSpendOnTwf075OwnedCell()
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            new TimberbornBuildingAdapter().CreateNonBurnableSource(2, 3, 1) with
            {
                CompanionTargetId = 77u,
            });
        TimberbornBurnDamageTargetState state = CreateBurnDamageState(
            "infrastructure-target",
            "PowerWheel",
            TimberbornBurnDamageTargetKind.Infrastructure,
            fuelValue: 6,
            flammability: 2,
            damageCapacity: 10,
            damageTaken: 3,
            ownedCellIndices: [38]);

        TimberbornQaDeltaStimulusResult result = fireSystem.QueueQaDeltaStimulus(
            TimberbornQaFieldTargetSelectors.Infrastructure,
            new Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState>
            {
                [state.TargetKey] = state,
            });

        Assert.Equal(38, result.CellIndex);
        Assert.Equal(WildfireMaterialClass.Infrastructure, result.MaterialClass);
        Assert.Equal(77u, result.CompanionTargetId);
        Assert.Equal("infrastructure-target", result.BurnDamageTargetKey);
        Assert.Equal("PowerWheel", result.BurnDamageSpecId);
        Assert.Equal(TimberbornBurnDamageTargetKind.Infrastructure, result.BurnDamageTargetKind);
        Assert.Equal(7, result.BurnDamageRemainingCapacity);
        Assert.Equal((byte)6, result.BurnDamageProbeFuel);
        Assert.Equal((byte)5, result.BurnDamageSpendFuel);
        Assert.Equal((byte)15, result.SetHeat);
        Assert.Equal(2, result.QueuedHeatChangeCount);
        FireSimChange primeChange = Assert.Single(simulator.RegisteredChanges);
        Assert.Equal(38, primeChange.CellIndex);
        Assert.Null(primeChange.SetWater);
        Assert.Equal((byte)6, primeChange.SetFuel);
        Assert.Equal((byte)15, primeChange.SetHeat);
        Assert.Equal((byte)2, primeChange.SetFlammability);
        Assert.Null(primeChange.SetBurningLevel);
        Assert.Null(primeChange.SetTerrain);
        Assert.Equal(1, fireSystem.RegisteredChangeCountSinceLastDispatch);

        fireSystem.Tick();

        Assert.Single(simulator.RegisteredChanges);
        Assert.Equal(0, fireSystem.RegisteredChangeCountSinceLastDispatch);

        fireSystem.Tick();

        Assert.Equal(2, simulator.RegisteredChanges.Count);
        FireSimChange spendChange = simulator.RegisteredChanges[1];
        Assert.Equal(38, spendChange.CellIndex);
        Assert.Null(spendChange.SetWater);
        Assert.Equal((byte)5, spendChange.SetFuel);
        Assert.Equal((byte)15, spendChange.SetHeat);
        Assert.Equal((byte)2, spendChange.SetFlammability);
        Assert.Null(spendChange.SetBurningLevel);
        Assert.Null(spendChange.SetTerrain);
        Assert.Equal(0, fireSystem.RegisteredChangeCountSinceLastDispatch);
    }

    [Theory]
    [InlineData(TimberbornQaFieldTargetSelectors.PowerInfrastructure, "power_infrastructure:owned", "PowerWheel")]
    [InlineData(TimberbornQaFieldTargetSelectors.WaterInfrastructure, "water_infrastructure:owned", "WaterPump")]
    public void QueueQaDeltaStimulusInfrastructureSelectorsRequireMatchingOwnedPrefix(
        string selector,
        string expectedTargetKey,
        string expectedSpecId)
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            new TimberbornBuildingAdapter().CreateNonBurnableSource(2, 3, 1) with
            {
                CompanionTargetId = 77u,
            },
            new TimberbornBuildingAdapter().CreateNonBurnableSource(3, 3, 1) with
            {
                CompanionTargetId = 88u,
            });
        TimberbornBurnDamageTargetState powerState = CreateBurnDamageState(
            "power_infrastructure:owned",
            "PowerWheel",
            TimberbornBurnDamageTargetKind.Infrastructure,
            fuelValue: 6,
            flammability: 2,
            damageCapacity: 10,
            damageTaken: 3,
            ownedCellIndices: [38]);
        TimberbornBurnDamageTargetState waterState = CreateBurnDamageState(
            "water_infrastructure:owned",
            "WaterPump",
            TimberbornBurnDamageTargetKind.Infrastructure,
            fuelValue: 5,
            flammability: 1,
            damageCapacity: 9,
            damageTaken: 2,
            ownedCellIndices: [39]);

        TimberbornQaDeltaStimulusResult result = fireSystem.QueueQaDeltaStimulus(
            selector,
            new Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState>
            {
                [powerState.TargetKey] = powerState,
                [waterState.TargetKey] = waterState,
            });

        Assert.Equal(expectedTargetKey, result.BurnDamageTargetKey);
        Assert.Equal(expectedSpecId, result.BurnDamageSpecId);
    }

    [Fact]
    public void QueueQaDeltaStimulusPathInfrastructureAllowsZeroCapacitySafeNoOpProbe()
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            new TimberbornBuildingAdapter().CreateNonBurnableSource(2, 3, 1) with
            {
                CompanionTargetId = 77u,
            });
        TimberbornBurnDamageTargetState state = CreateBurnDamageState(
            "path_infrastructure:zero",
            "Path.Folktails(Clone)",
            TimberbornBurnDamageTargetKind.Infrastructure,
            fuelValue: 0,
            flammability: 0,
            damageCapacity: 0,
            damageTaken: 0,
            ownedCellIndices: [38]);

        TimberbornQaDeltaStimulusResult result = fireSystem.QueueQaDeltaStimulus(
            TimberbornQaFieldTargetSelectors.PathInfrastructure,
            new Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState>
            {
                [state.TargetKey] = state,
            });

        Assert.Equal(38, result.CellIndex);
        Assert.Equal(WildfireMaterialClass.Infrastructure, result.MaterialClass);
        Assert.Equal("path_infrastructure:zero", result.BurnDamageTargetKey);
        Assert.Equal(0, result.BurnDamageRemainingCapacity);
        Assert.Equal((byte)1, result.BurnDamageProbeFuel);
        Assert.Equal((byte)0, result.BurnDamageSpendFuel);
        Assert.Equal(2, result.QueuedHeatChangeCount);
    }

    [Theory]
    [InlineData(TimberbornQaFieldTargetSelectors.Dynamite, 38, "dynamite:owned", "dynamite")]
    [InlineData(TimberbornQaFieldTargetSelectors.Detonator, 39, "detonator:owned", "detonator")]
    [InlineData(TimberbornQaFieldTargetSelectors.Tunnel, 40, "tunnel:owned", "tunnel")]
    public void QueueQaDeltaStimulusDirectConsequenceSelectorsTargetPlacedCells(
        string selector,
        int expectedCellIndex,
        string expectedStableId,
        string expectedKind)
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            new TimberbornBuildingAdapter().CreateNonBurnableSource(2, 3, 1) with
            {
                CompanionTargetId = 77u,
            },
            new TimberbornBuildingAdapter().CreateNonBurnableSource(3, 3, 1) with
            {
                CompanionTargetId = 88u,
            },
            new TimberbornBuildingAdapter().CreateNonBurnableSource(0, 4, 1) with
            {
                CompanionTargetId = 99u,
            });
        RecordingExplosiveInfrastructureTargetApi explosiveApi = new(
            new TimberbornExplosiveInfrastructureTarget(
                "dynamite:owned",
                TimberbornExplosiveInfrastructureKind.Dynamite,
                38,
                Depth: 3,
                CanTriggerNative: true));
        RecordingDetonatorFireSafetyTargetApi detonatorApi = new(
            new TimberbornDetonatorFireSafetyTarget(
                "detonator:owned",
                39,
                CanDisable: true,
                CanPreserveAutomationState: true));
        RecordingTunnelFireTargetApi tunnelApi = new(
            new TimberbornTunnelFireTarget(
                "tunnel:owned",
                40,
                BottomLevel: 1,
                CanMarkUnstable: true,
                CanExplodeNative: true,
                CanRecover: false));

        TimberbornQaDeltaStimulusResult result = fireSystem.QueueQaDeltaStimulus(
            selector,
            explosiveInfrastructureTargetApi: explosiveApi,
            detonatorFireSafetyTargetApi: detonatorApi,
            tunnelFireTargetApi: tunnelApi);

        Assert.Equal(expectedCellIndex, result.CellIndex);
        Assert.Equal(WildfireMaterialClass.Infrastructure, result.MaterialClass);
        Assert.Equal(expectedKind, result.DirectTargetKind);
        Assert.Equal(expectedStableId, result.DirectTargetStableId);
        Assert.True(result.DirectTargetScannedCellCount > 0);
        Assert.Equal((byte)15, result.SetHeat);
        Assert.Equal(2, result.QueuedHeatChangeCount);
        FireSimChange primeChange = Assert.Single(simulator.RegisteredChanges);
        Assert.Equal(expectedCellIndex, primeChange.CellIndex);
        Assert.Null(primeChange.SetWater);
        Assert.Equal((byte)15, primeChange.SetFuel);
        Assert.Equal((byte)15, primeChange.SetHeat);
        Assert.Equal((byte)3, primeChange.SetFlammability);
        Assert.Null(primeChange.SetBurningLevel);
        Assert.Null(primeChange.SetTerrain);
        Assert.Equal(0, explosiveApi.NativeTriggerCallCount);
        Assert.Equal(0, detonatorApi.DisableCallCount);
        Assert.Equal(0, tunnelApi.ExplodeCallCount);

        fireSystem.Tick();
        fireSystem.Tick();

        Assert.Equal(2, simulator.RegisteredChanges.Count);
        Assert.Equal(expectedCellIndex, simulator.RegisteredChanges[1].CellIndex);
        Assert.Equal((byte)14, simulator.RegisteredChanges[1].SetFuel);
        Assert.Equal((byte)15, simulator.RegisteredChanges[1].SetHeat);
    }

    [Fact]
    public void QueueQaDeltaStimulusDetonatorAcceptsDynamiteControlFallbackTarget()
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            new TimberbornBuildingAdapter().CreateNonBurnableSource(3, 3, 1) with
            {
                CompanionTargetId = 88u,
            });
        string stableId = $"{TimberbornDetonatorFireSafetyStableIds.DynamiteControlPrefix}owned";
        RecordingDetonatorFireSafetyTargetApi detonatorApi = new(
            new TimberbornDetonatorFireSafetyTarget(
                stableId,
                39,
                CanDisable: true,
                CanPreserveAutomationState: true));

        TimberbornQaDeltaStimulusResult result = fireSystem.QueueQaDeltaStimulus(
            TimberbornQaFieldTargetSelectors.Detonator,
            explosiveInfrastructureTargetApi: new RecordingExplosiveInfrastructureTargetApi(),
            detonatorFireSafetyTargetApi: detonatorApi,
            tunnelFireTargetApi: new RecordingTunnelFireTargetApi());

        Assert.Equal(39, result.CellIndex);
        Assert.Equal("detonator", result.DirectTargetKind);
        Assert.Equal(stableId, result.DirectTargetStableId);
        Assert.Equal(2, result.QueuedHeatChangeCount);
        Assert.Single(simulator.RegisteredChanges);
        Assert.Equal(0, detonatorApi.DisableCallCount);
    }

    [Theory]
    [InlineData(TimberbornQaFieldTargetSelectors.Dynamite)]
    [InlineData(TimberbornQaFieldTargetSelectors.Detonator)]
    [InlineData(TimberbornQaFieldTargetSelectors.Tunnel)]
    public void QueueQaDeltaStimulusDirectConsequenceSelectorsRequirePlacedTargets(string selector)
    {
        RecordingFireSimulator simulator = new(width: 2, height: 2, depth: 1);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            new TimberbornBuildingAdapter().CreateNonBurnableSource(0, 0, 0));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            fireSystem.QueueQaDeltaStimulus(
                selector,
                explosiveInfrastructureTargetApi: new RecordingExplosiveInfrastructureTargetApi(),
                detonatorFireSafetyTargetApi: new RecordingDetonatorFireSafetyTargetApi(),
                tunnelFireTargetApi: new RecordingTunnelFireTargetApi()));

        Assert.Contains($"No placed Timberborn target was found for QA selector '{selector}'", exception.Message);
        Assert.Empty(simulator.RegisteredChanges);
    }

    [Theory]
    [InlineData(
        TimberbornQaFieldTargetSelectors.Detonator,
        TimberbornDetonatorFireSafetyStableIds.UnavailablePrefix + "0")]
    [InlineData(TimberbornQaFieldTargetSelectors.Tunnel, "tunnel-unavailable:0")]
    public void QueueQaDeltaStimulusDirectConsequenceSelectorsRejectUnavailablePseudoTargets(
        string selector,
        string stableId)
    {
        RecordingFireSimulator simulator = new(width: 2, height: 2, depth: 1);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            new TimberbornBuildingAdapter().CreateNonBurnableSource(0, 0, 0));
        RecordingDetonatorFireSafetyTargetApi detonatorApi = new(
            new TimberbornDetonatorFireSafetyTarget(
                stableId,
                CellIndex: 0,
                CanDisable: false,
                CanPreserveAutomationState: false));
        RecordingTunnelFireTargetApi tunnelApi = new(
            new TimberbornTunnelFireTarget(
                stableId,
                CellIndex: 0,
                BottomLevel: 0,
                CanMarkUnstable: false,
                CanExplodeNative: false,
                CanRecover: false));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            fireSystem.QueueQaDeltaStimulus(
                selector,
                explosiveInfrastructureTargetApi: new RecordingExplosiveInfrastructureTargetApi(),
                detonatorFireSafetyTargetApi: detonatorApi,
                tunnelFireTargetApi: tunnelApi));

        Assert.Contains($"No placed Timberborn target was found for QA selector '{selector}'", exception.Message);
        Assert.Empty(simulator.RegisteredChanges);
        Assert.Equal(0, detonatorApi.DisableCallCount);
        Assert.Equal(0, tunnelApi.ExplodeCallCount);
    }

    [Fact]
    public void QueueQaSelectedTreeDeltaStimulusRegistersSelectedImportedTreeHeatChange()
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            new TimberbornResourceAdapter().CreateTreeSource(1, 1, 0, materialTargetId: 11u),
            new TimberbornResourceAdapter().CreateTreeSource(2, 3, 1, materialTargetId: 77u));

        TimberbornQaDeltaStimulusResult result = fireSystem.QueueQaSelectedTreeDeltaStimulus(
            new RecordingSelectedTreeTargetProvider(38));

        Assert.Equal(TimberbornQaFieldTargetSelectors.SelectedTree, result.TargetSelector);
        Assert.Equal(38, result.CellIndex);
        Assert.Equal(WildfireMaterialClass.Tree, result.MaterialClass);
        Assert.Equal(77u, result.CompanionTargetId);
        Assert.Contains(simulator.RegisteredChanges, static change => change.CellIndex == 38);
        Assert.All(simulator.RegisteredChanges, static change =>
        {
            Assert.Equal((byte)15, change.SetHeat);
            Assert.Null(change.SetFuel);
            Assert.Null(change.SetFlammability);
            Assert.Null(change.SetBurningLevel);
            Assert.Null(change.SetTerrain);
            Assert.Null(change.SetWater);
        });
        Assert.Equal(simulator.RegisteredChanges.Count, fireSystem.RegisteredChangeCountSinceLastDispatch);
    }

    [Fact]
    public void QueueQaSelectedTreeDeltaStimulusPegsHeatAcrossIgnitionBuildupTicks()
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            new TimberbornResourceAdapter().CreateTreeSource(2, 3, 1, materialTargetId: 77u));

        fireSystem.QueueQaSelectedTreeDeltaStimulus(new RecordingSelectedTreeTargetProvider(38));

        Assert.Single(simulator.RegisteredChanges);

        fireSystem.Tick();
        Assert.Single(simulator.RegisteredChanges);

        Enumerable.Range(0, 11).ToList().ForEach(_ => fireSystem.Tick());
        Assert.Equal(12, simulator.RegisteredChanges.Count);

        fireSystem.Tick();
        Assert.Equal(12, simulator.RegisteredChanges.Count);
        Assert.All(simulator.RegisteredChanges, static change =>
        {
            Assert.Equal(38, change.CellIndex);
            Assert.Equal((byte)15, change.SetHeat);
        });
    }

    [Fact]
    public void QueueQaSelectedTreeDeltaStimulusScalesHeatPegWithFireStepInterval()
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            new TimberbornResourceAdapter().CreateTreeSource(2, 3, 1, materialTargetId: 77u));
        Assert.True(fireSystem.TryUpdateParameters(FireSimParameters.Default with
        {
            FireCellStepIntervalTicks = 6u,
        }));

        fireSystem.QueueQaSelectedTreeDeltaStimulus(new RecordingSelectedTreeTargetProvider(38));

        Enumerable.Range(0, 72).ToList().ForEach(_ => fireSystem.Tick());
        Assert.Equal(72, simulator.RegisteredChanges.Count);

        fireSystem.Tick();
        Assert.Equal(72, simulator.RegisteredChanges.Count);
        Assert.All(simulator.RegisteredChanges, static change =>
        {
            Assert.Equal(38, change.CellIndex);
            Assert.Equal((byte)15, change.SetHeat);
        });
    }

    [Fact]
    public void DisabledQaDeltaStimulusFailsWithoutQueuingExternalChange()
    {
        TimberbornQaCommandState state = new(
            IsSimulatorIntegrated: true,
            IsGameContextRuntimeLoaded: true,
            WildfireEnabled: false,
            Width: 8,
            Height: 6,
            Depth: 4,
            TickCount: 12,
            QueuedChangeCount: 0,
            LastDeltaCount: 0);
        RecordingStateProvider stateProvider = new(state);
        RecordingDeltaStimulus deltaStimulus = new(
            new TimberbornQaDeltaStimulusResult(TimberbornQaFieldTargetSelectors.Default, 91, 3, 4, 2, WildfireMaterialClass.Tree, 77u, 498, 15, 1));
        RecordingLogSink logSink = new();
        TimberbornQaCommandBridge bridge = new(stateProvider, deltaStimulus, logSink);

        TimberbornQaCommandResult result = bridge.Execute("qa-delta-stimulus");

        Assert.False(result.Success);
        Assert.Equal("failure", result.Status);
        Assert.Equal("wildfire_disabled", result.Message);
        Assert.Equal(0, deltaStimulus.CallCount);
        Assert.Equal(1, stateProvider.CallCount);
        Assert.Contains("wildfire_enabled=false", result.ResultToken);
        Assert.Contains("loaded_game_ready=false", result.ResultToken);
        Assert.Contains(result.ResultToken, logSink.WarningMessages);
    }

    [Fact]
    public void QueueBuildingBurnoutQaStimulusRegistersOnlyTargetedFieldChanges()
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            CreateBurnDurationSource(1, 3, 1, 4));
        RecordingBuildingBurnoutTargetProvider targetProvider = new(
            new TimberbornQaBuildingBurnoutStimulusTarget(38, 2, 3, 1, 39));

        TimberbornQaBuildingBurnoutStimulusResult result =
            fireSystem.QueueBuildingBurnoutQaStimulus(targetProvider);

        Assert.Equal(new TimberbornQaBuildingBurnoutStimulusResult(38, 2, 3, 1, 39, 15, 0, 2), result);
        Assert.Equal(1, targetProvider.CallCount);
        Assert.Equal([new FireGrid(4, 6, 2)], targetProvider.Grids);
        Assert.Equal(2, simulator.RegisteredChanges.Count);
        Assert.All(simulator.RegisteredChanges, change =>
        {
            Assert.Equal(38, change.CellIndex);
            Assert.Null(change.AddHeat);
            Assert.Null(change.AddFuel);
            Assert.Null(change.SetWater);
            Assert.Null(change.SetFlammability);
            Assert.Null(change.SetBurningLevel);
            Assert.Null(change.SetTerrain);
            Assert.Null(change.SetCell);
        });
        Assert.Equal((byte)15, simulator.RegisteredChanges[0].SetHeat);
        Assert.Equal((byte)0, simulator.RegisteredChanges[1].SetFuel);
        Assert.Equal(2, fireSystem.RegisteredChangeCountSinceLastDispatch);
    }

    [Fact]
    public void QueueWaterSuppressionQaStimulusRegistersExactlyOneBoundSetWaterChange()
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            new TimberbornResourceAdapter().CreateTreeSource(2, 3, 1, materialTargetId: 77u));

        TimberbornQaWaterSuppressionStimulusResult result =
            fireSystem.QueueWaterSuppressionQaStimulus();

        Assert.Equal(38, result.CellIndex);
        Assert.Equal(WildfireMaterialClass.Tree, result.MaterialClass);
        Assert.Equal(77u, result.CompanionTargetId);
        Assert.Equal((byte)3, result.SetWater);
        FireSimChange change = Assert.Single(simulator.RegisteredChanges);
        Assert.Equal(38, change.CellIndex);
        Assert.Null(change.SetCell);
        Assert.Null(change.AddHeat);
        Assert.Null(change.AddFuel);
        Assert.Equal((byte)3, change.SetWater);
        Assert.Null(change.SetFuel);
        Assert.Null(change.SetHeat);
        Assert.Null(change.SetFlammability);
        Assert.Null(change.SetBurningLevel);
        Assert.Null(change.SetTerrain);
        Assert.Equal(1, fireSystem.RegisteredChangeCountSinceLastDispatch);
    }

    [Fact]
    public void QueueWaterSuppressionQaStimulusTargetsContaminatedTreeWithoutDecontamination()
    {
        RecordingFireSimulator simulator = new(width: 2, height: 1, depth: 1);
        TimberbornTerrainAdapter terrainAdapter = new();
        TimberbornResourceAdapter resourceAdapter = new();
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            terrainAdapter.CreateSource(0, 0, 0, isSolid: true),
            resourceAdapter.CreateTreeSource(0, 0, 0, materialTargetId: 1u),
            terrainAdapter.CreateSource(1, 0, 0, isSolid: true, soilContamination: 7),
            resourceAdapter.CreateTreeSource(1, 0, 0, materialTargetId: 2u));

        TimberbornQaWaterSuppressionStimulusResult result =
            fireSystem.QueueWaterSuppressionQaStimulus(TimberbornQaFieldTargetSelectors.ContaminatedTree);

        Assert.Equal(1, result.CellIndex);
        Assert.Equal((byte)7, result.TargetSoilContamination);
        Assert.True(result.IsAffectedCellContaminated);
        Assert.False(result.IsContaminatedSuppressionInput);
        Assert.False(result.IsBadwaterSuppressionInput);
        Assert.Equal(1, result.WaterSuppressionInputSafeUnavailableCount);
        Assert.Equal(0, result.NativeDecontaminationAttemptCount);
        Assert.Equal(1, fireSystem.ContaminationFireSummary.ContaminatedAffectedMapCellCount);
        Assert.Equal(0, fireSystem.ContaminationFireSummary.ContaminatedWaterSuppressionInputCellCount);
        FireSimChange change = Assert.Single(simulator.RegisteredChanges);
        Assert.Equal(1, change.CellIndex);
        Assert.Equal((byte)3, change.SetWater);
    }

    [Theory]
    [InlineData("low", 37, 1, 3, 1, 4)]
    [InlineData("medium", 38, 2, 3, 1, 9)]
    [InlineData("high", 39, 3, 3, 1, 15)]
    public void QueueBurnDurationQaStimulusSchedulesSustainedNamedBoundHeatChanges(
        string target,
        int expectedCellIndex,
        int expectedX,
        int expectedY,
        int expectedZ,
        byte expectedFuel)
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            CreateBurnDurationSource(1, 3, 1, 4),
            CreateBurnDurationSource(2, 3, 1, 9),
            CreateBurnDurationSource(3, 3, 1, 15));

        TimberbornQaBurnDurationStimulusResult result =
            fireSystem.QueueBurnDurationQaStimulus(target);

        Assert.Equal(expectedCellIndex, result.CellIndex);
        Assert.Equal(expectedX, result.X);
        Assert.Equal(expectedY, result.Y);
        Assert.Equal(expectedZ, result.Z);
        Assert.Equal(expectedFuel, result.InitialFuel);
        Assert.Equal((byte)15, result.SetHeat);
        Assert.Equal(12, result.SustainedHeatTicks);
        Assert.Equal(12, result.QueuedHeatChangeCount);
        FireSimChange change = Assert.Single(simulator.RegisteredChanges);
        Assert.Equal(expectedCellIndex, change.CellIndex);
        Assert.Null(change.SetCell);
        Assert.Null(change.AddHeat);
        Assert.Null(change.AddFuel);
        Assert.Null(change.SetWater);
        Assert.Null(change.SetFuel);
        Assert.Equal((byte)15, change.SetHeat);
        Assert.Null(change.SetFlammability);
        Assert.Null(change.SetBurningLevel);
        Assert.Null(change.SetTerrain);
        Assert.Equal(1, fireSystem.RegisteredChangeCountSinceLastDispatch);
        Assert.Equal(target, fireSystem.BurnDurationProofState.Target);
        Assert.Equal(expectedFuel, fireSystem.BurnDurationProofState.InitialFuel);
        Assert.Equal(12, fireSystem.BurnDurationProofState.SustainedHeatTicks);
        Assert.Equal(0, fireSystem.BurnDurationProofState.SustainedHeatAppliedTicks);
        Assert.False(fireSystem.BurnDurationProofState.SustainedHeatComplete);
        Assert.Equal("queued", fireSystem.BurnDurationProofState.Status);
    }

    [Fact]
    public void BurnDurationQaStimulusPegsMaxHeatForTwelveSuccessfulDispatches()
    {
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            CreateBurnDurationSource(1, 3, 1, 4));

        fireSystem.QueueBurnDurationQaStimulus("low");
        Enumerable.Range(0, 12)
            .ToList()
            .ForEach(_ => fireSystem.Tick());

        Assert.Equal(12, simulator.RegisteredChanges.Count);
        simulator.RegisteredChanges
            .ForEach(change =>
            {
                Assert.Equal(37, change.CellIndex);
                Assert.Equal((byte)15, change.SetHeat);
                Assert.Null(change.SetFuel);
                Assert.Null(change.SetWater);
            });
        Assert.Equal(12, fireSystem.BurnDurationProofState.SustainedHeatAppliedTicks);
        Assert.True(fireSystem.BurnDurationProofState.SustainedHeatComplete);
    }

    [Fact]
    public void BurnDurationQaProofRecordsStartDepletionAndElapsedTicksFromSimulatorDeltas()
    {
        ushort coldCell = PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 1, burningLevel: 0);
        ushort burningFuel4 = PackedCell.Pack(fuel: 4, heat: 15, flammability: 3, water: 0, terrain: 1, burningLevel: 1);
        ushort burningFuel3 = PackedCell.Pack(fuel: 3, heat: 15, flammability: 3, water: 0, terrain: 1, burningLevel: 1);
        ushort depletedCell = PackedCell.Pack(fuel: 0, heat: 15, flammability: 3, water: 0, terrain: 1, burningLevel: 0);
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        simulator.TickResults.Enqueue(new GpuFireStepResult([new CellDelta(37, coldCell, burningFuel4)], Tick: 5));
        simulator.TickResults.Enqueue(new GpuFireStepResult([new CellDelta(37, burningFuel4, burningFuel3)], Tick: 6));
        simulator.TickResults.Enqueue(new GpuFireStepResult([new CellDelta(37, burningFuel3, depletedCell)], Tick: 7));
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            CreateBurnDurationSource(1, 3, 1, 4));

        fireSystem.QueueBurnDurationQaStimulus("low");
        fireSystem.Tick();
        fireSystem.Tick();
        fireSystem.Tick();

        TimberbornQaBurnDurationProofState proof = fireSystem.BurnDurationProofState;
        Assert.Equal("low", proof.Target);
        Assert.Equal(37, proof.CellIndex);
        Assert.Equal(4, proof.InitialFuel);
        Assert.Equal(5u, proof.BurnStartTick);
        Assert.Equal(7u, proof.DepletionTick);
        Assert.Equal(3u, proof.ElapsedBurnTicks);
        Assert.False(proof.TimedOut);
        Assert.Equal("depleted", proof.Status);
    }

    [Fact]
    public void BurnDurationQaProofRecordsLowFuelDepletionBehindSustainedHeatDelta()
    {
        ushort lowFuelWetCrop = PackedCell.Pack(fuel: 4, heat: 12, flammability: 2, water: 1, terrain: 1, burningLevel: 0);
        ushort heatPeggedLowFuelCrop = PackedCell.SetBurningLevel(PackedCell.SetHeat(lowFuelWetCrop, 15), 1);
        ushort depletedCrop = PackedCell.Pack(fuel: 0, heat: 11, flammability: 2, water: 0, terrain: 1, burningLevel: 0);
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        simulator.TickResults.Enqueue(new GpuFireStepResult(
            [
                new CellDelta(37, lowFuelWetCrop, heatPeggedLowFuelCrop),
                new CellDelta(37, heatPeggedLowFuelCrop, depletedCrop),
            ],
            Tick: 33));
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            CreateBurnDurationSource(1, 3, 1, 4));

        fireSystem.QueueBurnDurationQaStimulus("low");
        fireSystem.Tick();

        TimberbornQaBurnDurationProofState proof = fireSystem.BurnDurationProofState;
        Assert.Equal("low", proof.Target);
        Assert.Equal(37, proof.CellIndex);
        Assert.Equal(33u, proof.BurnStartTick);
        Assert.Equal(33u, proof.DepletionTick);
        Assert.Equal(1u, proof.ElapsedBurnTicks);
        Assert.False(proof.TimedOut);
        Assert.Equal("depleted", proof.Status);
    }

    [Fact]
    public void BurnDurationQaProofRecordsLowFuelDepletionBeforeSustainedHeatDelta()
    {
        ushort lowFuelWetCrop = PackedCell.Pack(fuel: 4, heat: 12, flammability: 2, water: 1, terrain: 1, burningLevel: 0);
        ushort heatPeggedLowFuelCrop = PackedCell.SetBurningLevel(PackedCell.SetHeat(lowFuelWetCrop, 15), 1);
        ushort depletedCrop = PackedCell.Pack(fuel: 0, heat: 11, flammability: 2, water: 0, terrain: 1, burningLevel: 0);
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        simulator.TickResults.Enqueue(new GpuFireStepResult(
            [
                new CellDelta(37, heatPeggedLowFuelCrop, depletedCrop),
                new CellDelta(37, lowFuelWetCrop, heatPeggedLowFuelCrop),
            ],
            Tick: 33));
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            CreateBurnDurationSource(1, 3, 1, 4));

        fireSystem.QueueBurnDurationQaStimulus("low");
        fireSystem.Tick();

        TimberbornQaBurnDurationProofState proof = fireSystem.BurnDurationProofState;
        Assert.Equal("low", proof.Target);
        Assert.Equal(37, proof.CellIndex);
        Assert.Equal(33u, proof.BurnStartTick);
        Assert.Equal(33u, proof.DepletionTick);
        Assert.Equal(1u, proof.ElapsedBurnTicks);
        Assert.False(proof.TimedOut);
        Assert.Equal("depleted", proof.Status);
    }

    [Fact]
    public void BurnDurationQaProofReportsTimeoutWhenTargetDoesNotDeplete()
    {
        ushort coldCell = PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 1, burningLevel: 0);
        ushort burningFuel9 = PackedCell.Pack(fuel: 9, heat: 15, flammability: 3, water: 0, terrain: 1, burningLevel: 1);
        RecordingFireSimulator simulator = new(width: 4, height: 6, depth: 2);
        simulator.TickResults.Enqueue(new GpuFireStepResult([new CellDelta(38, coldCell, burningFuel9)], Tick: 10));
        simulator.TickResults.Enqueue(new GpuFireStepResult([], Tick: 74));
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            CreateBurnDurationSource(2, 3, 1, 9));

        fireSystem.QueueBurnDurationQaStimulus("medium");
        fireSystem.Tick();
        fireSystem.Tick();

        TimberbornQaBurnDurationProofState proof = fireSystem.BurnDurationProofState;
        Assert.Equal("medium", proof.Target);
        Assert.Equal(10u, proof.BurnStartTick);
        Assert.Null(proof.DepletionTick);
        Assert.Equal(65u, proof.ElapsedBurnTicks);
        Assert.True(proof.TimedOut);
        Assert.Equal("no_depletion_timeout", proof.Status);
    }

    [Fact]
    public void WaterSuppressionQaStimulusAppliesOnNextCadenceDispatchTick()
    {
        ushort burningCell = PackedCell.Pack(fuel: 15, heat: 15, flammability: 1, water: 0, terrain: 1, burningLevel: 1);
        ushort wetCell = PackedCell.SetWater(burningCell, 3);
        RecordingFireSimulator simulator = new(width: 1, height: 1, depth: 1);
        simulator.TickResults.Enqueue(new GpuFireStepResult([new CellDelta(0, burningCell, wetCell)], Tick: 1));
        TimberbornFireSystem fireSystem = CreateInitializedFireSystem(
            simulator,
            new TimberbornResourceAdapter().CreateTreeSource(0, 0, 0, materialTargetId: 77u));
        TimberbornFixedCadenceFireDispatcher dispatcher = new(
            fireSystem,
            TimberbornFireCadence.FromSeconds(1),
            NullTimberbornFireLogSink.Instance);

        fireSystem.QueueWaterSuppressionQaStimulus();
        TimberbornFireDispatchResult waiting = dispatcher.Update(
            new TimberbornFireUpdate(1, TimeSpan.FromMilliseconds(500)));

        Assert.False(waiting.DidDispatch);
        Assert.Equal(1, fireSystem.RegisteredChangeCountSinceLastDispatch);
        Assert.Equal(0, simulator.TickCallCount);
        Assert.Equal(0u, fireSystem.LastTick);
        Assert.Equal(0, fireSystem.LastDeltaCount);

        TimberbornFireDispatchResult dispatched = dispatcher.Update(
            new TimberbornFireUpdate(2, TimeSpan.FromMilliseconds(500)));

        Assert.True(dispatched.DidDispatch);
        Assert.Equal(1, simulator.TickCallCount);
        Assert.Equal(0, fireSystem.RegisteredChangeCountSinceLastDispatch);
        Assert.Equal(1u, fireSystem.LastTick);
        Assert.Equal(1, fireSystem.LastDeltaCount);
        Assert.True(dispatched.Step.HasValue);
        Assert.Equal([new CellDelta(0, burningCell, wetCell)], dispatched.Step.Value.Deltas);
    }

    [Fact]
    public void DisabledDispatcherSkipsTicksAndDoesNotAccumulateCatchUpElapsed()
    {
        RecordingFireSimulator simulator = new(width: 1, height: 1, depth: 1);
        RecordingFireLogSink logSink = new();
        bool isEnabled = false;
        TimberbornFireSystem fireSystem = new(
            simulator,
            new TimberbornFireCellMapper(),
            logSink);
        TimberbornFixedCadenceFireDispatcher dispatcher = new(
            fireSystem,
            TimberbornFireCadence.FromSeconds(1),
            logSink,
            () => isEnabled);

        TimberbornFireDispatchResult disabled = dispatcher.Update(
            new TimberbornFireUpdate(1, TimeSpan.FromSeconds(5)));
        isEnabled = true;
        TimberbornFireDispatchResult waiting = dispatcher.Update(
            new TimberbornFireUpdate(2, TimeSpan.FromMilliseconds(500)));

        Assert.False(disabled.DidDispatch);
        Assert.Equal("wildfire-disabled", disabled.Reason);
        Assert.False(waiting.DidDispatch);
        Assert.Equal("cadence-not-reached", waiting.Reason);
        Assert.Equal(0, simulator.TickCallCount);
        Assert.Contains(
            "wildfire_timberborn_dispatch_skipped_disabled game_update_id=1",
            logSink.InfoMessages);
    }

    [Fact]
    public void TimberbornRuntimeAutoDispatchGuardDisablesLargeLiveMaps()
    {
        FireGrid largeValidationMap = new(256, 256, 23);

        Assert.True(TimberbornAutoDispatchPolicy.IsAllowedCellCount(largeValidationMap.CellCount));
        Assert.True(TimberbornAutoDispatchPolicy.IsAllowedCellCount(TimberbornAutoDispatchPolicy.CellLimit));
        Assert.False(TimberbornAutoDispatchPolicy.IsAllowedCellCount(TimberbornAutoDispatchPolicy.CellLimit + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TimberbornAutoDispatchPolicy.IsAllowedCellCount(-1));
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

    [Theory]
    [InlineData("qa-delta-stimulus unknown")]
    [InlineData("qa-delta-stimulus 1 2 3")]
    [InlineData("qa-building-burnout-stimulus target=1")]
    [InlineData("qa-water-suppression-stimulus unknown")]
    [InlineData("qa-water-suppression-stimulus x=1 y=2")]
    [InlineData("qa-burn-duration-stimulus x=1 y=2")]
    [InlineData("qa-burn-duration-stimulus extreme")]
    public void SimulatorChangeCommandsRejectArguments(string command)
    {
        RecordingStateProvider stateProvider = new(TimberbornQaCommandState.Placeholder);
        TimberbornQaCommandBridge bridge = new(
            stateProvider,
            new RecordingDeltaStimulus(
                new TimberbornQaDeltaStimulusResult(TimberbornQaFieldTargetSelectors.Default, 0, 0, 0, 0, WildfireMaterialClass.Tree, 1u, 1, 15, 1)),
            new RecordingBuildingBurnoutStimulus(
                new TimberbornQaBuildingBurnoutStimulusResult(0, 0, 0, 0, 1, 15, 0, 2)),
            new RecordingWaterSuppressionStimulus(
                new TimberbornQaWaterSuppressionStimulusResult(TimberbornQaFieldTargetSelectors.Default, 0, 0, 0, 0, WildfireMaterialClass.Tree, 1u, 1, 3, 1)),
            new RecordingBurnDurationStimulus(
                new TimberbornQaBurnDurationStimulusResult(
                    "low",
                    0,
                    0,
                    0,
                    0,
                    WildfireMaterialClass.Tree,
                    1u,
                    1,
                    4,
                    15,
                    64,
                    12,
                    12)),
            new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute(command);

        Assert.False(result.Success);
        Assert.Equal("failure", result.Status);
        Assert.Contains("does not accept arguments", result.Message);
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
            LastDeltaConsumerWaterChangedCount: 15,
            LastPositiveWaterChangedTick: 16,
            LastPositiveWaterChangedCount: 17,
            LastDeltaConsumerVisualEffectEventCount: 18,
            LastDeltaConsumerVisualEffectFailureCount: 30,
            LastDeltaConsumerGameplayConsequenceCount: 19,
            LastDeltaConsumerBuildingBurnoutConsideredDeltaCount: 20,
            LastDeltaConsumerBuildingBurnoutMatchedCellCount: 21,
            LastDeltaConsumerBuildingBurnoutAppliedConsequenceCount: 22,
            LastPositiveBuildingBurnoutAppliedTick: 40,
            LastPositiveBuildingBurnoutAppliedCount: 41,
            LastDeltaConsumerAlertCount: 23,
            LastPlayerFireAlertTick: 34,
            LastPlayerFireAlertStartedFireCount: 35,
            LastPlayerFireAlertFuelSpentCount: 36,
            LastPlayerFireAlertMaxHeat: 37,
            PlayerFireAlertNotificationCount: 38,
            PlayerFireAlertPresentationFailureCount: 39,
            PlayerFireAlertNotificationSent: true,
            LastPlayerFireAlertMessage: "Wildfire alert",
            VisualFieldSurfaceBound: true,
            VisualFieldSurfaceCellCount: 24,
            VisualFieldSurfaceLastUpdatedTick: 25,
            SmokeHeightSourceSmokeCellCount: 76,
            SmokeHeightNonSourceSmokeCellCount: 77,
            SmokeHeightNonSourceGroundContactSmokeCellCount: 78,
            SmokeHeightMaxNonSourceSmokeDistanceFromSource: 79,
            BeaverFieldExposureAvailable: true,
            BeaverFieldExposureSampledBeavers: 43,
            BeaverFieldExposureExposedBeavers: 44,
            BeaverFieldExposureRespiratoryCells: 45,
            BeaverFieldExposureBurnCells: 46,
            BeaverFieldExposureContaminatedSmokeCells: 47,
            BeaverFieldExposureToxicCells: 48,
            BeaverFieldExposureSteamCells: 49,
            BeaverFieldExposureTaintedAftermathCells: 50,
            BeaverFieldExposureSkippedNoPositionApi: 51,
            BeaverFieldExposureSkippedBoundedSampling: 52,
            BeaverFieldExposureUnavailableReason: "none",
            BeaverFieldBehaviorDispatcherEnabled: true,
            BeaverFieldBehaviorTrackedBeavers: 53,
            BeaverFieldBehaviorDecisionsEvaluated: 54,
            BeaverFieldBehaviorSmokeDecisionsApplied: 55,
            BeaverFieldBehaviorToxicSmokeDecisionsApplied: 56,
            BeaverFieldBehaviorFireHeatDecisionsApplied: 57,
            BeaverFieldBehaviorNoOpDecisionsApplied: 58,
            BeaverFieldBehaviorDecisionsSkippedCooldown: 59,
            BeaverFieldBehaviorDecisionsSkippedBatch: 60,
            BeaverFieldBehaviorSkippedNoSafeApi: 61,
            BeaverFieldBehaviorFailedDecisions: 62,
            BeaverFieldBehaviorRecoveryActions: 63,
            BeaverFieldBehaviorSmokeExposedSamples: 64,
            BeaverFieldBehaviorSmokeExposureAccumulatedSamples: 65,
            BeaverFieldBehaviorSmokeCoughingEntered: 66,
            BeaverFieldBehaviorSmokeCoughingRecovered: 67,
            BeaverFieldBehaviorSmokeRecoveryDecays: 68,
            BeaverFieldBehaviorSmokeChokingCandidates: 69,
            BeaverFieldBehaviorSmokeChokingSkippedUnsafeApi: 70,
            BeaverFieldBehaviorSmokeDeathCandidates: 71,
            BeaverFieldBehaviorSmokeDeathSkippedUnsafeApi: 72,
            BeaverFieldBehaviorPersistenceSaves: 73,
            BeaverFieldBehaviorPersistenceLoads: 74,
            BeaverFieldBehaviorLastDecisionTick: 75,
            BurnDurationProofTarget: "medium",
            BurnDurationProofTargetIndex: 42,
            BurnDurationProofTargetX: 5,
            BurnDurationProofTargetY: 6,
            BurnDurationProofTargetZ: 7,
            BurnDurationProofInitialFuel: 9,
            BurnDurationProofQueuedTick: 44,
            BurnDurationProofBurnStartTick: 45,
            BurnDurationProofDepletionTick: 60,
            BurnDurationProofElapsedBurnTicks: 16,
            BurnDurationProofTimeoutTicks: 64,
            BurnDurationProofSustainedHeatTicks: 12,
            BurnDurationProofSustainedHeatAppliedTicks: 12,
            BurnDurationProofSustainedHeatComplete: true,
            BurnDurationProofTimedOut: false,
            BurnDurationProofStatus: "depleted",
            CompatibilityProbeStatus: "degraded",
            CompatibilityProbeDegraded: true,
            CompatibilityProbeRequiredPassed: 6,
            CompatibilityProbeRequiredTotal: 6,
            CompatibilityProbeOptionalPassed: 2,
            CompatibilityProbeOptionalTotal: 4,
            CompatibilityProbeDegradedFeatures: "visual_effects,diagnostic_assets",
            FireSimPresetName: "slow-reactable",
            FireSimPresetIgnitionPoint: 5,
            FireSimPresetWaterIgnitionPenalty: 2,
            FireSimPresetFuelHeatWeight: 2,
            FireSimPresetFuelBurnDownNumerator: 1,
            FireSimPresetFuelBurnDownDenominator: 2,
            FireSimPresetCellStepIntervalTicks: 6,
            WorldImportTotalSources: 50,
            WorldImportTerrainSources: 44,
            WorldImportVegetationSources: 3,
            WorldImportTreeSources: 2,
            WorldImportCropSources: 1,
            WorldImportBuildingSources: 1,
            WorldImportStorageSources: 1,
            WorldImportInfrastructureSources: 1,
            WorldImportWaterSources: 0,
            WorldImportBadwaterSources: 0,
            WorldImportResolvedEmptyCells: 45,
            WorldImportResolvedTerrainCells: 1,
            WorldImportResolvedVegetationCells: 3,
            WorldImportResolvedTreeCells: 2,
            WorldImportResolvedCropCells: 1,
            WorldImportResolvedBuildingCells: 1,
            WorldImportResolvedStorageCells: 0,
            WorldImportResolvedInfrastructureCells: 0,
            WorldImportResolvedWaterCells: 0,
            WorldImportResolvedBadwaterCells: 0,
            WorldImportSafeUnavailableCount: 3,
            AshFieldEntries: 4,
            AshFieldFertileCells: 2,
            AshFieldSpentCells: 1,
            AshFieldTaintedCells: 1,
            AshFieldContaminatedBurnSources: 4,
            AshFieldContaminatedAffectedCells: 5,
            AshFieldGrowthCandidateCells: 2,
            AshFieldGrowthAppliedGrowables: 1,
            AshFieldGrowthSkippedTaintedCells: 1,
            AshFieldGrowthSkippedUnsafeApis: 0,
            ContaminationFireContaminatedBurnSources: 6,
            ContaminationFireContaminatedAffectedCells: 7,
            ContaminationFireContaminatedAffectedMapCells: 8,
            ContaminationFireBadwaterWaterLikeMapCells: 9,
            ContaminationFireContaminatedWaterLikeMapCells: 10,
            ContaminationFireBadwaterSuppressionInputs: 0,
            ContaminationFireContaminatedWaterSuppressionInputs: 0,
            ContaminationFireWaterSuppressionInputSafeUnavailable: 11,
            ContaminationFireToxicSmokeCells: 48,
            ContaminationFireNativeDecontaminationAttempts: 0,
            ContaminationFireSkippedUnsafeContaminationApis: 11,
            TaintedAshPoisonCandidateCells: 1,
            TaintedAshPoisonAppliedCells: 1,
            TaintedAshPoisonSkippedNoSafeApi: 0,
            FertileAshGathererPosts: 2,
            FertileAshCollectionCandidateCells: 2,
            FertileAshCollectionReachableCells: 1,
            FertileAshCollectedGoods: 1,
            FertileAshCollectionDepletedCells: 1,
            FertileAshCollectionSkippedTaintedOrSpentCells: 2,
            FertileAshCollectionSkippedInventoryApi: 0);
        TimberbornQaCommandBridge bridge = new(new RecordingStateProvider(state), new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("status");

        Assert.Contains("wildfire_command_result", result.ResultToken);
        Assert.Contains("command=status", result.ResultToken);
        Assert.Contains("success=true", result.ResultToken);
        Assert.Contains("wildfire_enabled=true", result.ResultToken);
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
        Assert.Contains("last_delta_consumer_water_changed=15", result.ResultToken);
        Assert.Contains("last_positive_water_changed_tick=16", result.ResultToken);
        Assert.Contains("last_positive_water_changed_count=17", result.ResultToken);
        Assert.Contains("last_delta_consumer_visual_effect_events=18", result.ResultToken);
        Assert.Contains("last_delta_consumer_visual_effect_failures=30", result.ResultToken);
        Assert.Contains("last_delta_consumer_gameplay_consequences=19", result.ResultToken);
        Assert.Contains("last_delta_consumer_building_burnout_considered_deltas=20", result.ResultToken);
        Assert.Contains("last_delta_consumer_building_burnout_matched_cells=21", result.ResultToken);
        Assert.Contains("last_delta_consumer_building_burnout_applied_consequences=22", result.ResultToken);
        Assert.Contains("last_positive_building_burnout_applied_tick=40", result.ResultToken);
        Assert.Contains("last_positive_building_burnout_applied_count=41", result.ResultToken);
        Assert.Contains("last_delta_consumer_alerts=23", result.ResultToken);
        Assert.Contains("last_player_fire_alert_tick=34", result.ResultToken);
        Assert.Contains("last_player_fire_alert_started_fires=35", result.ResultToken);
        Assert.Contains("last_player_fire_alert_fuel_spent=36", result.ResultToken);
        Assert.Contains("last_player_fire_alert_max_heat=37", result.ResultToken);
        Assert.Contains("player_fire_alert_notifications=38", result.ResultToken);
        Assert.Contains("player_fire_alert_presentation_failures=39", result.ResultToken);
        Assert.Contains("player_fire_alert_notification_sent=true", result.ResultToken);
        Assert.Contains("last_player_fire_alert_message=Wildfire_alert", result.ResultToken);
        Assert.Contains("visual_field_surface_bound=true", result.ResultToken);
        Assert.Contains("visual_field_surface_cells=24", result.ResultToken);
        Assert.Contains("visual_field_surface_updated_tick=25", result.ResultToken);
        Assert.Contains("smoke_height_source_smoke_cells=76", result.ResultToken);
        Assert.Contains("smoke_height_non_source_smoke_cells=77", result.ResultToken);
        Assert.Contains("smoke_height_non_source_ground_contact_smoke_cells=78", result.ResultToken);
        Assert.Contains("smoke_height_max_non_source_smoke_distance_from_source=79", result.ResultToken);
        Assert.Contains("beaver_field_exposure_available=true", result.ResultToken);
        Assert.Contains("beaver_field_exposure_sampled_beavers=43", result.ResultToken);
        Assert.Contains("beaver_field_exposure_exposed_beavers=44", result.ResultToken);
        Assert.Contains("beaver_field_exposure_respiratory_cells=45", result.ResultToken);
        Assert.Contains("beaver_field_exposure_burn_cells=46", result.ResultToken);
        Assert.Contains("beaver_field_exposure_contaminated_smoke_cells=47", result.ResultToken);
        Assert.Contains("beaver_field_exposure_toxic_cells=48", result.ResultToken);
        Assert.Contains("beaver_field_exposure_steam_cells=49", result.ResultToken);
        Assert.Contains("beaver_field_exposure_tainted_aftermath_cells=50", result.ResultToken);
        Assert.Contains("beaver_field_exposure_skipped_no_position_api=51", result.ResultToken);
        Assert.Contains("beaver_field_exposure_skipped_bounded_sampling=52", result.ResultToken);
        Assert.Contains("beaver_field_exposure_unavailable_reason=none", result.ResultToken);
        Assert.Contains("beaver_field_behavior_dispatcher_enabled=true", result.ResultToken);
        Assert.Contains("beaver_field_behavior_tracked_beavers=53", result.ResultToken);
        Assert.Contains("beaver_field_behavior_decisions_evaluated=54", result.ResultToken);
        Assert.Contains("beaver_field_behavior_smoke_decisions_applied=55", result.ResultToken);
        Assert.Contains("beaver_field_behavior_toxic_smoke_decisions_applied=56", result.ResultToken);
        Assert.Contains("beaver_field_behavior_fire_heat_decisions_applied=57", result.ResultToken);
        Assert.Contains("beaver_field_behavior_noop_decisions_applied=58", result.ResultToken);
        Assert.Contains("beaver_field_behavior_decisions_skipped_cooldown=59", result.ResultToken);
        Assert.Contains("beaver_field_behavior_decisions_skipped_batch=60", result.ResultToken);
        Assert.Contains("beaver_field_behavior_skipped_no_safe_api=61", result.ResultToken);
        Assert.Contains("beaver_field_behavior_failed_decisions=62", result.ResultToken);
        Assert.Contains("beaver_field_behavior_recovery_actions=63", result.ResultToken);
        Assert.Contains("beaver_field_behavior_smoke_exposed_samples=64", result.ResultToken);
        Assert.Contains("beaver_field_behavior_smoke_exposure_accumulated_samples=65", result.ResultToken);
        Assert.Contains("beaver_field_behavior_smoke_coughing_entered=66", result.ResultToken);
        Assert.Contains("beaver_field_behavior_smoke_coughing_recovered=67", result.ResultToken);
        Assert.Contains("beaver_field_behavior_smoke_recovery_decays=68", result.ResultToken);
        Assert.Contains("beaver_field_behavior_smoke_choking_candidates=69", result.ResultToken);
        Assert.Contains("beaver_field_behavior_smoke_choking_skipped_unsafe_api=70", result.ResultToken);
        Assert.Contains("beaver_field_behavior_smoke_death_candidates=71", result.ResultToken);
        Assert.Contains("beaver_field_behavior_smoke_death_skipped_unsafe_api=72", result.ResultToken);
        Assert.Contains("beaver_field_behavior_persistence_saves=73", result.ResultToken);
        Assert.Contains("beaver_field_behavior_persistence_loads=74", result.ResultToken);
        Assert.Contains("beaver_field_behavior_last_decision_tick=75", result.ResultToken);
        Assert.Contains("burn_duration_proof_target=medium", result.ResultToken);
        Assert.Contains("burn_duration_proof_target_index=42", result.ResultToken);
        Assert.Contains("burn_duration_proof_target_x=5", result.ResultToken);
        Assert.Contains("burn_duration_proof_target_y=6", result.ResultToken);
        Assert.Contains("burn_duration_proof_target_z=7", result.ResultToken);
        Assert.Contains("burn_duration_proof_initial_fuel=9", result.ResultToken);
        Assert.Contains("burn_duration_proof_queued_tick=44", result.ResultToken);
        Assert.Contains("burn_duration_proof_burn_start_tick=45", result.ResultToken);
        Assert.Contains("burn_duration_proof_depletion_tick=60", result.ResultToken);
        Assert.Contains("burn_duration_proof_elapsed_burn_ticks=16", result.ResultToken);
        Assert.Contains("burn_duration_proof_timeout_ticks=64", result.ResultToken);
        Assert.Contains("burn_duration_proof_sustained_heat_ticks=12", result.ResultToken);
        Assert.Contains("burn_duration_proof_sustained_heat_applied_ticks=12", result.ResultToken);
        Assert.Contains("burn_duration_proof_sustained_heat_complete=true", result.ResultToken);
        Assert.Contains("burn_duration_proof_timed_out=false", result.ResultToken);
        Assert.Contains("burn_duration_proof_status=depleted", result.ResultToken);
        Assert.Contains("compatibility_probe_status=degraded", result.ResultToken);
        Assert.Contains("compatibility_probe_degraded=true", result.ResultToken);
        Assert.Contains("compatibility_probe_required_passed=6", result.ResultToken);
        Assert.Contains("compatibility_probe_required_total=6", result.ResultToken);
        Assert.Contains("compatibility_probe_optional_passed=2", result.ResultToken);
        Assert.Contains("compatibility_probe_optional_total=4", result.ResultToken);
        Assert.Contains("compatibility_probe_degraded_features=visual_effects,diagnostic_assets", result.ResultToken);
        Assert.Contains("fire_sim_preset=slow-reactable", result.ResultToken);
        Assert.Contains("fire_ignition_point=5", result.ResultToken);
        Assert.DoesNotContain("fire_burning_neighbor_heat_bonus", result.ResultToken);
        Assert.Contains("fire_water_ignition_penalty=2", result.ResultToken);
        Assert.Contains("fire_fuel_heat_weight=2", result.ResultToken);
        Assert.Contains("fire_fuel_burn_down=1/2", result.ResultToken);
        Assert.Contains("fire_step_interval_ticks=6", result.ResultToken);
        Assert.Contains("world_import_total_sources=50", result.ResultToken);
        Assert.Contains("world_import_terrain_sources=44", result.ResultToken);
        Assert.Contains("world_import_vegetation_sources=3", result.ResultToken);
        Assert.Contains("world_import_tree_sources=2", result.ResultToken);
        Assert.Contains("world_import_resolved_empty_cells=45", result.ResultToken);
        Assert.Contains("world_import_resolved_vegetation_cells=3", result.ResultToken);
        Assert.Contains("world_import_resolved_tree_cells=2", result.ResultToken);
        Assert.Contains("world_import_safe_unavailable=3", result.ResultToken);
        Assert.Contains("ash_field_entries=4", result.ResultToken);
        Assert.Contains("ash_field_fertile_cells=2", result.ResultToken);
        Assert.Contains("ash_field_spent_cells=1", result.ResultToken);
        Assert.Contains("ash_field_tainted_cells=1", result.ResultToken);
        Assert.Contains("ash_field_contaminated_burn_sources=4", result.ResultToken);
        Assert.Contains("ash_field_contaminated_affected_cells=5", result.ResultToken);
        Assert.Contains("ash_field_growth_candidate_cells=2", result.ResultToken);
        Assert.Contains("ash_field_growth_applied_growables=1", result.ResultToken);
        Assert.Contains("ash_field_growth_skipped_tainted_cells=1", result.ResultToken);
        Assert.Contains("ash_field_growth_skipped_unsafe_apis=0", result.ResultToken);
        Assert.Contains("contamination_fire_contaminated_burn_sources=6", result.ResultToken);
        Assert.Contains("contamination_fire_contaminated_affected_cells=7", result.ResultToken);
        Assert.Contains("contamination_fire_contaminated_affected_map_cells=8", result.ResultToken);
        Assert.Contains("contamination_fire_badwater_water_like_map_cells=9", result.ResultToken);
        Assert.Contains("contamination_fire_contaminated_water_like_map_cells=10", result.ResultToken);
        Assert.Contains("contamination_fire_badwater_suppression_inputs=0", result.ResultToken);
        Assert.Contains("contamination_fire_contaminated_water_suppression_inputs=0", result.ResultToken);
        Assert.Contains("contamination_fire_water_suppression_input_safe_unavailable=11", result.ResultToken);
        Assert.Contains("contamination_fire_toxic_smoke_cells=48", result.ResultToken);
        Assert.Contains("contamination_fire_native_decontamination_attempts=0", result.ResultToken);
        Assert.Contains("contamination_fire_skipped_unsafe_contamination_apis=11", result.ResultToken);
        Assert.Contains("tainted_ash_poison_candidate_cells=1", result.ResultToken);
        Assert.Contains("tainted_ash_poison_applied_cells=1", result.ResultToken);
        Assert.Contains("tainted_ash_poison_skipped_no_safe_api=0", result.ResultToken);
        Assert.Contains("fertile_ash_gatherer_posts=2", result.ResultToken);
        Assert.Contains("fertile_ash_collection_candidate_cells=2", result.ResultToken);
        Assert.Contains("fertile_ash_collection_reachable_cells=1", result.ResultToken);
        Assert.Contains("fertile_ash_collected_goods=1", result.ResultToken);
        Assert.Contains("fertile_ash_collection_depleted_cells=1", result.ResultToken);
        Assert.Contains("fertile_ash_collection_skipped_tainted_or_spent_cells=2", result.ResultToken);
        Assert.Contains("fertile_ash_collection_skipped_inventory_api=0", result.ResultToken);
    }

    [Fact]
    public void FailedRequiredCompatibilityProbeBlocksLoadedGameReadyToken()
    {
        TimberbornQaCommandState state = new(
            IsSimulatorIntegrated: true,
            IsGameContextRuntimeLoaded: true,
            Width: 4,
            Height: 5,
            Depth: 6,
            TickCount: 7,
            CompatibilityProbeStatus: "failed",
            CompatibilityProbeDegraded: true,
            CompatibilityProbeRequiredPassed: 4,
            CompatibilityProbeRequiredTotal: 5,
            CompatibilityProbeDegradedFeatures: "compute");
        TimberbornQaCommandBridge bridge = new(new RecordingStateProvider(state), new RecordingLogSink());

        TimberbornQaCommandResult result = bridge.Execute("qa-readiness");

        Assert.False(result.State.IsLoadedGameReady);
        Assert.Contains("loaded_game_ready=false", result.ResultToken);
        Assert.Contains("compatibility_probe_status=failed", result.ResultToken);
        Assert.Contains("compatibility_probe_degraded_features=compute", result.ResultToken);
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
        Assert.Contains("compatibility_probe_status=placeholder", result.ResultToken);
        Assert.Contains("compatibility_probe_required_passed=placeholder", result.ResultToken);
    }

    [Fact]
    public void FireSimParameterPresetsExposeExpectedNamedProfiles()
    {
        string[] names = TimberbornFireSimParameterPresets.All
            .Select(static preset => preset.Name)
            .ToArray();

        Assert.Equal(["default", "slow-reactable", "harsh", "wildfire", "conservative", "high-threshold-high-bonus"], names);
        Assert.True(TimberbornFireSimParameterPresets.TryGet("slow-reactable", out TimberbornFireSimParameterPreset? preset));
        Assert.Equal(5u, preset.Parameters.IgnitionPoint);
        Assert.Equal(1u, preset.Parameters.FireFuelBurnDownPressureNumerator);
        Assert.Equal(2u, preset.Parameters.FireFuelBurnDownPressureDenominator);
        Assert.Equal(6u, preset.Parameters.FireCellStepIntervalTicks);
        Assert.True(TimberbornFireSimParameterPresets.TryGet("high-threshold-high-bonus", out TimberbornFireSimParameterPreset? highPreset));
        Assert.Equal(5u, highPreset.Parameters.IgnitionPoint);
        Assert.Equal(6u, highPreset.Parameters.FireFuelHeatWeight);
        Assert.Equal(0u, highPreset.Parameters.FireWaterIgnitionPenalty);
        Assert.Equal(2u, highPreset.Parameters.FireFuelBurnDownPressureNumerator);
        Assert.Equal(1u, highPreset.Parameters.FireFuelBurnDownPressureDenominator);
        Assert.False(TimberbornFireSimParameterPresets.TryGet("IgnitionPoint=1", out _));
    }

    [Fact]
    public void FireSimPresetStateRejectsUnknownNamesAndKeepsCurrentPreset()
    {
        TimberbornFireSimParameterPresetState state = new();

        TimberbornQaFireSimParameterPresetResult selected =
            state.SelectFireSimParameterPreset("harsh");
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => state.SelectFireSimParameterPreset("raw=1"));

        Assert.Equal("harsh", selected.Name);
        Assert.Equal("harsh", state.CurrentPreset.Name);
        Assert.Contains("Known presets", exception.Message);
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

        public TimberbornQaDeltaStimulusResult QueueDeltaStimulus(string targetSelector)
        {
            CallCount++;
            TargetSelectors.Add(targetSelector);
            return result;
        }

        public List<string> TargetSelectors { get; } = [];
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

    private sealed class RecordingWaterSuppressionStimulus(TimberbornQaWaterSuppressionStimulusResult result)
        : ITimberbornQaWaterSuppressionStimulus
    {
        public int CallCount { get; private set; }

        public TimberbornQaWaterSuppressionStimulusResult QueueWaterSuppressionStimulus(string targetSelector)
        {
            CallCount++;
            TargetSelectors.Add(targetSelector);
            return result;
        }

        public List<string> TargetSelectors { get; } = [];
    }

    private sealed class RecordingBurnDurationStimulus(TimberbornQaBurnDurationStimulusResult result)
        : ITimberbornQaBurnDurationStimulus
    {
        public List<string> Targets { get; } = [];

        public TimberbornQaBurnDurationStimulusResult QueueBurnDurationStimulus(string target)
        {
            Targets.Add(target);
            return result;
        }
    }

    private sealed class RecordingFireSimParameterPresetSelector : ITimberbornQaFireSimParameterPresetSelector
    {
        public List<string> PresetNames { get; } = [];

        public TimberbornQaFireSimParameterPresetResult SelectFireSimParameterPreset(string presetName)
        {
            PresetNames.Add(presetName);
            return new TimberbornQaFireSimParameterPresetResult(
                presetName,
                TimberbornFireSimParameterPresets.All
                    .Single(preset => string.Equals(preset.Name, presetName, StringComparison.OrdinalIgnoreCase))
                    .Parameters);
        }
    }

    private sealed class RecordingSoilMoistureMapProbe(TimberbornQaSoilMoistureRangeResult range)
        : ITimberbornQaSoilMoistureMapProbe
    {
        public int CallCount { get; private set; }

        public TimberbornQaSoilMoistureRangeResult ScanSoilMoistureRange()
        {
            CallCount++;
            return range;
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

    private sealed class RecordingExplosiveInfrastructureTargetApi(
        TimberbornExplosiveInfrastructureTarget? target = null) : ITimberbornExplosiveInfrastructureTargetApi
    {
        public int NativeTriggerCallCount { get; private set; }

        public TimberbornExplosiveInfrastructureTarget? ResolveTarget(
            TimberbornExplosiveInfrastructureConsequence consequence)
        {
            return target is not null && consequence.CellIndex == target.CellIndex
                ? target
                : null;
        }

        public TimberbornExplosiveInfrastructureNativeTriggerResult TriggerNative(
            TimberbornExplosiveInfrastructureTarget target,
            int delayTicks)
        {
            NativeTriggerCallCount++;
            return new TimberbornExplosiveInfrastructureNativeTriggerResult(
                TimberbornExplosiveInfrastructureNativeTriggerStatus.Triggered);
        }
    }

    private sealed class RecordingDetonatorFireSafetyTargetApi(
        TimberbornDetonatorFireSafetyTarget? target = null) : ITimberbornDetonatorFireSafetyTargetApi
    {
        public int DisableCallCount { get; private set; }

        public TimberbornDetonatorFireSafetyTarget? ResolveTarget(
            TimberbornDetonatorFireSafetyConsequence consequence)
        {
            return target is not null && consequence.CellIndex == target.CellIndex
                ? target
                : null;
        }

        public TimberbornDetonatorFireSafetyDisableResult DisableTarget(
            TimberbornDetonatorFireSafetyTarget target)
        {
            DisableCallCount++;
            return new TimberbornDetonatorFireSafetyDisableResult(
                TimberbornDetonatorFireSafetyDisableStatus.Disabled,
                RecoverabilityPreserved: true);
        }
    }

    private sealed class RecordingTunnelFireTargetApi(TimberbornTunnelFireTarget? target = null)
        : ITimberbornTunnelFireTargetApi
    {
        public int ExplodeCallCount { get; private set; }

        public TimberbornTunnelFireTarget? ResolveTarget(TimberbornTunnelFireConsequence consequence)
        {
            return target is not null && consequence.CellIndex == target.CellIndex
                ? target
                : null;
        }

        public TimberbornTunnelNativeExplodeResult ExplodeNative(TimberbornTunnelFireTarget target)
        {
            ExplodeCallCount++;
            return new TimberbornTunnelNativeExplodeResult(
                TimberbornTunnelNativeExplodeStatus.Applied,
                RecoverabilityPreserved: false);
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

    private sealed class RecordingSelectedTreeTargetProvider(int selectedCellIndex)
        : ITimberbornQaSelectedTreeTargetProvider
    {
        public TimberbornImportedFieldTarget FindSelectedTreeTarget(
            FireGrid grid,
            IReadOnlyList<TimberbornImportedFieldTarget> importedTargets)
        {
            return importedTargets
                .Where(target => target.CellIndex == selectedCellIndex)
                .Select(static target => (TimberbornImportedFieldTarget?)target)
                .FirstOrDefault() ??
                throw new InvalidOperationException("Expected selected imported target was not found.");
        }
    }

    private sealed class RecordingFireLogSink : ITimberbornFireLogSink
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

    private static TimberbornFireSystem CreateInitializedFireSystem(
        RecordingFireSimulator simulator,
        params TimberbornCellSource[] sources)
    {
        TimberbornFireSystem fireSystem = new(new RecordingFireSimulatorFactory(simulator));
        fireSystem.Initialize(new FireGrid(simulator.Width, simulator.Height, simulator.Depth), sources);
        simulator.RegisteredChanges.Clear();
        return fireSystem;
    }

    private static TimberbornCellSource CreateBurnDurationSource(int x, int y, int z, byte fuel)
    {
        return new TimberbornResourceAdapter().CreateSource(
            x,
            y,
            z,
            fuel,
            flammability: 3,
            TimberbornResourceKind.Vegetation,
            WildfireMaterialClass.Tree,
            materialTargetId: (uint)fuel);
    }

    private static TimberbornBurnDamageTargetState CreateBurnDamageState(
        string stableId,
        string specId,
        TimberbornBurnDamageTargetKind targetKind,
        byte fuelValue,
        byte flammability,
        int damageCapacity,
        int damageTaken,
        IReadOnlyList<int> ownedCellIndices)
    {
        return new TimberbornBurnDamageTargetState(
            new TimberbornBurnDamageTargetKey(stableId),
            specId,
            targetKind,
            TimberbornBurnMaterialKind.Constructed,
            damageCapacity,
            fuelValue,
            flammability,
            damageTaken,
            LastDamagedTick: 0,
            OwnedCellIndices: ownedCellIndices.ToArray(),
            MissingResourceIds: [],
            AccountedResourceIds: ["Log"]);
    }

    private static TimberbornBurnDamageTargetState CreateOrganicBurnDamageState(
        string stableId,
        string specId,
        TimberbornBurnDamageTargetKind targetKind,
        IReadOnlyList<int> ownedCellIndices,
        IReadOnlyList<string> accountedResourceIds)
    {
        return new TimberbornBurnDamageTargetState(
            new TimberbornBurnDamageTargetKey(stableId),
            specId,
            targetKind,
            TimberbornBurnMaterialKind.Organic,
            DamageCapacity: 2,
            FuelValue: 1,
            Flammability: 3,
            DamageTaken: 0,
            LastDamagedTick: 0,
            ownedCellIndices,
            MissingResourceIds: [],
            accountedResourceIds);
    }

    private sealed class RecordingFireSimulatorFactory(RecordingFireSimulator simulator) : ITimberbornFireSimulatorFactory
    {
        public IGpuFireSimulator Create(
            FireGrid grid,
            ReadOnlySpan<ushort> initialCells,
            ReadOnlySpan<WildfireMaterialField> materialFields)
        {
            Assert.Equal(simulator.Width, grid.Width);
            Assert.Equal(simulator.Height, grid.Height);
            Assert.Equal(simulator.Depth, grid.Depth);
            return simulator;
        }
    }

    private sealed class RecordingFireSimulator(int width, int height, int depth) :
        IGpuFireSimulator,
        ITimberbornConfigurableFireSimParameters
    {
        public int Width { get; } = width;

        public int Height { get; } = height;

        public int Depth { get; } = depth;

        public List<FireSimChange> RegisteredChanges { get; } = [];

        public Queue<GpuFireStepResult> TickResults { get; } = [];

        public int TickCallCount { get; private set; }

        public FireSimParameters Parameters { get; private set; } = FireSimParameters.Default;

        public void RegisterChange(FireSimChange change)
        {
            RegisteredChanges.Add(change);
        }

        public GpuFireStepResult Tick()
        {
            TickCallCount++;
            return TickResults.Count > 0
                ? TickResults.Dequeue()
                : new GpuFireStepResult([], Tick: (uint)TickCallCount);
        }

        public void UpdateParameters(FireSimParameters parameters)
        {
            Parameters = parameters;
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
