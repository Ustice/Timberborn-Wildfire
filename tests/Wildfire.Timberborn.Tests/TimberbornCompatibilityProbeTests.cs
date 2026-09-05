namespace Wildfire.Core.Tests;

public sealed class TimberbornCompatibilityProbeTests
{
    [Fact]
    public void RequiredFailureMarksReportFailedAndNamesDegradedFeature()
    {
        TimberbornCompatibilityReport report = TimberbornCompatibilityReport.Create(new[]
        {
            TimberbornCompatibilityProbeResult.Passed(
                "terrain_heights_in_cell",
                isRequired: true,
                "terrain"),
            TimberbornCompatibilityProbeResult.Failed(
                "compute_shader_bundle",
                isRequired: true,
                "compute",
                "missing:/tmp/wildfire_compute_mac"),
            TimberbornCompatibilityProbeResult.Degraded(
                "native_fire_prefab",
                isRequired: false,
                "visual effects",
                "preferred:CampfireFire"),
        });

        Assert.False(report.IsCompatible);
        Assert.True(report.IsDegraded);
        Assert.Equal("failed", report.StatusToken);
        Assert.Equal(1, report.PassedRequiredProbeCount);
        Assert.Equal(2, report.RequiredProbeCount);
        Assert.Equal(0, report.PassedOptionalProbeCount);
        Assert.Equal(1, report.OptionalProbeCount);
        Assert.Equal("compute,visual_effects", report.DegradedFeatureToken);
    }

    [Fact]
    public void OptionalDegradationKeepsRuntimeCompatibleButSearchablyDegraded()
    {
        TimberbornCompatibilityReport report = TimberbornCompatibilityReport.Create(new[]
        {
            TimberbornCompatibilityProbeResult.Passed(
                "terrain_heights_in_cell",
                isRequired: true,
                "terrain"),
            TimberbornCompatibilityProbeResult.Passed(
                "compute_shader_bundle",
                isRequired: true,
                "compute"),
            TimberbornCompatibilityProbeResult.Degraded(
                "diagnostic_asset_bundle",
                isRequired: false,
                "diagnostic assets",
                "missing:/tmp/wildfire_diagnostic_mac"),
        });

        Assert.True(report.IsCompatible);
        Assert.True(report.IsDegraded);
        Assert.Equal("degraded", report.StatusToken);
        Assert.Equal("diagnostic_assets", report.DegradedFeatureToken);
    }

    [Fact]
    public void OptionalBuildingBurnoutDegradationDoesNotFailComputeReadiness()
    {
        TimberbornCompatibilityReport report = TimberbornCompatibilityReport.Create(new[]
        {
            TimberbornCompatibilityProbeResult.Passed(
                "terrain_heights_in_cell",
                isRequired: true,
                "terrain"),
            TimberbornCompatibilityProbeResult.Passed(
                "compute_shader_bundle",
                isRequired: true,
                "compute"),
            TimberbornCompatibilityProbeResult.Degraded(
                "block_service_component_lookup",
                isRequired: false,
                "building_burnout",
                "missing_method:IBlockService.GetObjectsWithComponentAt"),
        });

        Assert.True(report.IsCompatible);
        Assert.False(report.HasRequiredFailure);
        Assert.Equal("degraded", report.StatusToken);
        Assert.Equal("building_burnout", report.DegradedFeatureToken);
        Assert.Equal("none", report.RequiredFailureFeatureToken);
    }

    [Fact]
    public void RuntimeGateRejectsRequiredProbeFailureBeforeInitialization()
    {
        RecordingFireLogSink logSink = new();
        TimberbornCompatibilityReport report = TimberbornCompatibilityReport.Create(new[]
        {
            TimberbornCompatibilityProbeResult.Passed(
                "terrain_heights_in_cell",
                isRequired: true,
                "terrain"),
            TimberbornCompatibilityProbeResult.Failed(
                "compute_shader_bundle",
                isRequired: true,
                "compute",
                "invalid_asset_bundle_header:not_a_bundle"),
        });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            TimberbornCompatibilityRuntimeGate.ThrowIfRequiredProbesFailed(report, logSink));

        Assert.Contains("compute", exception.Message);
        Assert.Contains(
            logSink.WarningMessages,
            message => message.Contains("wildfire_timberborn_runtime_initialize_rejected") &&
                message.Contains("reason=compatibility_probe_failed") &&
                message.Contains("required_failed_features=compute"));
    }

    [Fact]
    public void RuntimeGateLogsReadyOnlyWhenRequiredProbesPass()
    {
        RecordingFireLogSink logSink = new();
        TimberbornCompatibilityReport report = TimberbornCompatibilityReport.Create(new[]
        {
            TimberbornCompatibilityProbeResult.Passed(
                "terrain_heights_in_cell",
                isRequired: true,
                "terrain"),
            TimberbornCompatibilityProbeResult.Passed(
                "compute_shader_bundle",
                isRequired: true,
                "compute"),
            TimberbornCompatibilityProbeResult.Degraded(
                "native_fire_prefab",
                isRequired: false,
                "visual_effects",
                "preferred:CampfireFire"),
        });

        TimberbornCompatibilityRuntimeGate.LogLoadState(
            report,
            logSink,
            TimberbornFireCadence.FromSeconds(1));

        Assert.Contains(
            logSink.InfoMessages,
            message => message.Contains("wildfire_timberborn_runtime_ready") &&
                message.Contains("compatibility_probe_status=degraded") &&
                message.Contains("degraded_features=visual_effects"));
        Assert.Empty(logSink.WarningMessages);
    }

    [Fact]
    public void RuntimeGateLogsBlockedWhenRequiredProbesFail()
    {
        RecordingFireLogSink logSink = new();
        TimberbornCompatibilityReport report = TimberbornCompatibilityReport.Create(new[]
        {
            TimberbornCompatibilityProbeResult.Failed(
                "compute_shader_bundle",
                isRequired: true,
                "compute",
                "missing:/tmp/wildfire_compute_mac"),
        });

        TimberbornCompatibilityRuntimeGate.LogLoadState(
            report,
            logSink,
            TimberbornFireCadence.FromSeconds(1));

        Assert.Empty(logSink.InfoMessages);
        Assert.Contains(
            logSink.WarningMessages,
            message => message.Contains("wildfire_timberborn_runtime_initialization_blocked") &&
                message.Contains("compatibility_probe_status=failed") &&
                message.Contains("required_failed_features=compute"));
    }

    [Fact]
    public void LoggerEmitsSummaryAndPerProbeResultTokens()
    {
        RecordingFireLogSink logSink = new();
        TimberbornCompatibilityReport report = TimberbornCompatibilityReport.Create(new[]
        {
            TimberbornCompatibilityProbeResult.Passed(
                "terrain_heights_in_cell",
                isRequired: true,
                "terrain"),
            TimberbornCompatibilityProbeResult.Degraded(
                "native_fire_prefab",
                isRequired: false,
                "visual effects",
                "preferred:CampfireFire"),
        });

        TimberbornCompatibilityProbeLogger.Log(report, logSink);

        Assert.Contains(
            logSink.InfoMessages,
            message => message.Contains("wildfire_timberborn_compatibility_probe_summary") &&
                message.Contains("status=degraded") &&
                message.Contains("required_passed=1/1") &&
                message.Contains("optional_passed=0/1") &&
                message.Contains("degraded_features=visual_effects"));
        Assert.Contains(
            logSink.InfoMessages,
            message => message.Contains("wildfire_timberborn_compatibility_probe_result") &&
                message.Contains("probe=terrain_heights_in_cell") &&
                message.Contains("status=passed") &&
                message.Contains("required=true"));
        Assert.Contains(
            logSink.WarningMessages,
            message => message.Contains("wildfire_timberborn_compatibility_probe_result") &&
                message.Contains("probe=native_fire_prefab") &&
                message.Contains("status=degraded") &&
                message.Contains("degraded=true") &&
                message.Contains("feature=visual_effects"));
    }

    [Fact]
    public void FileProbeTreatsMissingRequiredFileAsFailedAndMissingOptionalFileAsDegraded()
    {
        TimberbornCompatibilityProbeResult requiredResult = TimberbornCompatibilityProbeCatalog.ProbeFile(
            "compute_shader_bundle",
            isRequired: true,
            "compute",
            "/missing/wildfire_compute_mac",
            exists: false);
        TimberbornCompatibilityProbeResult optionalResult = TimberbornCompatibilityProbeCatalog.ProbeFile(
            "diagnostic_asset_bundle",
            isRequired: false,
            "diagnostic_assets",
            "/missing/wildfire_diagnostic_mac",
            exists: false);

        Assert.Equal(TimberbornCompatibilityProbeStatus.Failed, requiredResult.Status);
        Assert.Equal(TimberbornCompatibilityProbeStatus.Degraded, optionalResult.Status);
    }

    [Fact]
    public void UnityAssetBundleProbeRejectsWrongContentAndNamesTwf050LimitationOnPass()
    {
        TimberbornCompatibilityProbeResult invalidResult = TimberbornCompatibilityProbeCatalog.ProbeUnityAssetBundleFile(
            "compute_shader_bundle",
            isRequired: true,
            "compute",
            "/tmp/wildfire_compute_mac",
            exists: true,
            sizeBytes: 128,
            header: "not-a-bundle",
            readError: null);
        TimberbornCompatibilityProbeResult validResult = TimberbornCompatibilityProbeCatalog.ProbeUnityAssetBundleFile(
            "compute_shader_bundle",
            isRequired: true,
            "compute",
            "/tmp/wildfire_compute_mac",
            exists: true,
            sizeBytes: 128,
            header: "UnityFS",
            readError: null);

        Assert.Equal(TimberbornCompatibilityProbeStatus.Failed, invalidResult.Status);
        Assert.Contains("invalid_asset_bundle_header", invalidResult.Message);
        Assert.Equal(TimberbornCompatibilityProbeStatus.Passed, validResult.Status);
        Assert.Contains("asset_and_kernel_validation_deferred_to_TWF-050", validResult.Message);
    }

    [Fact]
    public void UnityAssetBundleProbeDefersExistingHeaderReadErrorsToUnityLoader()
    {
        TimberbornCompatibilityProbeResult result = TimberbornCompatibilityProbeCatalog.ProbeUnityAssetBundleFile(
            "compute_shader_bundle",
            isRequired: true,
            "compute",
            "/tmp/wildfire_compute_mac",
            exists: true,
            sizeBytes: null,
            header: null,
            readError: "Win32 IO returned ERROR_NOT_SUPPORTED");

        Assert.Equal(TimberbornCompatibilityProbeStatus.Passed, result.Status);
        Assert.Contains("asset_bundle_exists_header_unreadable", result.Message);
        Assert.Contains("asset_and_kernel_validation_deferred_to_unity_loader", result.Message);
    }

    private sealed class RecordingFireLogSink : ITimberbornFireLogSink
    {
        public List<string> InfoMessages { get; } = new();

        public List<string> WarningMessages { get; } = new();

        public void Info(string message)
        {
            InfoMessages.Add(message);
        }

        public void Warning(string message)
        {
            WarningMessages.Add(message);
        }
    }
}
