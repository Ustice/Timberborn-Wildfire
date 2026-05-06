using System.Reflection;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.MapStateSystem;
using Timberborn.QuickNotificationSystem;
using Timberborn.TerrainSystem;
using UnityEngine;

namespace Wildfire.Timberborn;

public enum TimberbornCompatibilityProbeStatus
{
    Passed,
    Degraded,
    Failed,
}

public sealed record TimberbornCompatibilityProbeResult(
    string Name,
    bool IsRequired,
    TimberbornCompatibilityProbeStatus Status,
    string Feature,
    string Message)
{
    public static TimberbornCompatibilityProbeResult Passed(
        string name,
        bool isRequired,
        string feature,
        string message = "ok")
    {
        return new TimberbornCompatibilityProbeResult(
            name,
            isRequired,
            TimberbornCompatibilityProbeStatus.Passed,
            feature,
            message);
    }

    public static TimberbornCompatibilityProbeResult Degraded(
        string name,
        bool isRequired,
        string feature,
        string message)
    {
        return new TimberbornCompatibilityProbeResult(
            name,
            isRequired,
            TimberbornCompatibilityProbeStatus.Degraded,
            feature,
            message);
    }

    public static TimberbornCompatibilityProbeResult Failed(
        string name,
        bool isRequired,
        string feature,
        string message)
    {
        return new TimberbornCompatibilityProbeResult(
            name,
            isRequired,
            TimberbornCompatibilityProbeStatus.Failed,
            feature,
            message);
    }
}

public sealed record TimberbornCompatibilityReport(IReadOnlyList<TimberbornCompatibilityProbeResult> Results)
{
    public static readonly TimberbornCompatibilityReport Placeholder =
        new(Array.Empty<TimberbornCompatibilityProbeResult>());

    public int RequiredProbeCount => Results.Count(static result => result.IsRequired);

    public int PassedRequiredProbeCount => Results.Count(static result =>
        result.IsRequired && result.Status == TimberbornCompatibilityProbeStatus.Passed);

    public int OptionalProbeCount => Results.Count(static result => !result.IsRequired);

    public int PassedOptionalProbeCount => Results.Count(static result =>
        !result.IsRequired && result.Status == TimberbornCompatibilityProbeStatus.Passed);

    public bool IsCompatible => RequiredProbeCount > 0 && PassedRequiredProbeCount == RequiredProbeCount;

    public bool IsDegraded => Results.Any(static result => result.Status != TimberbornCompatibilityProbeStatus.Passed);

    public bool HasRequiredFailure => Results.Any(static result =>
        result.IsRequired && result.Status != TimberbornCompatibilityProbeStatus.Passed);

    public string StatusToken => RequiredProbeCount == 0
        ? "placeholder"
        : IsCompatible
            ? IsDegraded ? "degraded" : "compatible"
            : "failed";

    public string DegradedFeatureToken
    {
        get
        {
            string[] features = Results
                .Where(static result => result.Status != TimberbornCompatibilityProbeStatus.Passed)
                .Select(static result => FormatToken(result.Feature))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static feature => feature, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return features.Length == 0 ? "none" : string.Join(",", features);
        }
    }

    public string RequiredFailureFeatureToken
    {
        get
        {
            string[] features = Results
                .Where(static result =>
                    result.IsRequired &&
                    result.Status != TimberbornCompatibilityProbeStatus.Passed)
                .Select(static result => FormatToken(result.Feature))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static feature => feature, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return features.Length == 0 ? "none" : string.Join(",", features);
        }
    }

    public static TimberbornCompatibilityReport Create(
        IEnumerable<TimberbornCompatibilityProbeResult> results)
    {
        if (results is null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        return new TimberbornCompatibilityReport(results.ToArray());
    }

    public static string FormatToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "placeholder"
            : value.Replace(' ', '_').Replace('"', '\'').Replace('\\', '/');
    }
}

public static class TimberbornCompatibilityRuntimeGate
{
    public static void LogLoadState(
        TimberbornCompatibilityReport report,
        ITimberbornFireLogSink logSink,
        TimberbornFireCadence cadence)
    {
        if (report is null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (logSink is null)
        {
            throw new ArgumentNullException(nameof(logSink));
        }

        if (report.IsCompatible)
        {
            logSink.Info(
                "wildfire_timberborn_runtime_ready " +
                $"cadence_interval_ms={cadence.Interval.TotalMilliseconds:F0} " +
                $"compatibility_probe_status={report.StatusToken} " +
                $"degraded_features={report.DegradedFeatureToken}");
            return;
        }

        logSink.Warning(
            "wildfire_timberborn_runtime_initialization_blocked " +
            "reason=compatibility_probe_failed " +
            $"compatibility_probe_status={report.StatusToken} " +
            $"required_failed_features={report.RequiredFailureFeatureToken}");
    }

    public static void ThrowIfRequiredProbesFailed(
        TimberbornCompatibilityReport report,
        ITimberbornFireLogSink logSink)
    {
        if (report is null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (logSink is null)
        {
            throw new ArgumentNullException(nameof(logSink));
        }

        if (report.IsCompatible)
        {
            return;
        }

        string requiredFailedFeatures = report.RequiredFailureFeatureToken;
        logSink.Warning(
            "wildfire_timberborn_runtime_initialize_rejected " +
            "reason=compatibility_probe_failed " +
            $"required_failed_features={requiredFailedFeatures}");
        throw new InvalidOperationException(
            $"Required Timberborn compatibility probes failed: {requiredFailedFeatures}.");
    }
}

public static class TimberbornCompatibilityProbeLogger
{
    public static void Log(TimberbornCompatibilityReport report, ITimberbornFireLogSink logSink)
    {
        if (report is null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (logSink is null)
        {
            throw new ArgumentNullException(nameof(logSink));
        }

        logSink.Info(
            "wildfire_timberborn_compatibility_probe_summary " +
            $"status={report.StatusToken} " +
            $"degraded={report.IsDegraded.ToString().ToLowerInvariant()} " +
            $"required_passed={report.PassedRequiredProbeCount}/{report.RequiredProbeCount} " +
            $"optional_passed={report.PassedOptionalProbeCount}/{report.OptionalProbeCount} " +
            $"degraded_features={report.DegradedFeatureToken}");

        report.Results
            .ToList()
            .ForEach(result => LogResult(result, logSink));
    }

    private static void LogResult(TimberbornCompatibilityProbeResult result, ITimberbornFireLogSink logSink)
    {
        string message =
            "wildfire_timberborn_compatibility_probe_result " +
            $"probe={TimberbornCompatibilityReport.FormatToken(result.Name)} " +
            $"required={result.IsRequired.ToString().ToLowerInvariant()} " +
            $"status={result.Status.ToString().ToLowerInvariant()} " +
            $"degraded={(result.Status != TimberbornCompatibilityProbeStatus.Passed).ToString().ToLowerInvariant()} " +
            $"feature={TimberbornCompatibilityReport.FormatToken(result.Feature)} " +
            $"message={TimberbornCompatibilityReport.FormatToken(result.Message)}";

        if (result.Status == TimberbornCompatibilityProbeStatus.Passed)
        {
            logSink.Info(message);
            return;
        }

        logSink.Warning(message);
    }
}

public static class TimberbornCompatibilityProbeCatalog
{
    public static TimberbornCompatibilityReport RunReleasePathProbes()
    {
        List<TimberbornCompatibilityProbeResult> results = new()
        {
            ProbeProperty<MapSize>(
                "map_size_terrain_size",
                isRequired: true,
                feature: "terrain",
                "TerrainSize",
                typeof(Vector3Int)),
            ProbeProperty<MapSize>(
                "map_size_terrain_size_2d",
                isRequired: true,
                feature: "terrain",
                "TerrainSize2D",
                typeof(Vector2Int)),
            ProbeMethod<ITerrainService>(
                "terrain_heights_in_cell",
                isRequired: true,
                feature: "terrain",
                "GetAllHeightsInCell",
                typeof(Vector2Int)),
            ProbeGenericVector3IntMethod<IBlockService>(
                "block_service_component_lookup",
                isRequired: false,
                feature: "building_burnout"),
            ProbeProperty<PausableBuilding>(
                "pausable_building_paused",
                isRequired: false,
                feature: "building_burnout",
                "Paused",
                typeof(bool)),
            ProbeMethod<PausableBuilding>(
                "pausable_building_pause",
                isRequired: false,
                feature: "building_burnout",
                "Pause"),
            ProbeMethod<QuickNotificationService>(
                "quick_notification_warning",
                isRequired: false,
                feature: "player_alerts",
                "SendWarningNotification",
                typeof(string)),
            ProbeComputeShaderSupport(),
        };

        results.AddRange(ProbeAssetBundles());

        return TimberbornCompatibilityReport.Create(results);
    }

    public static TimberbornCompatibilityProbeResult ProbeFile(
        string name,
        bool isRequired,
        string feature,
        string path,
        bool exists)
    {
        return exists
            ? TimberbornCompatibilityProbeResult.Passed(name, isRequired, feature, path)
            : CreateUnavailableResult(name, isRequired, feature, $"missing:{path}");
    }

    public static TimberbornCompatibilityProbeResult CreateUnavailableResult(
        string name,
        bool isRequired,
        string feature,
        string message)
    {
        return isRequired
            ? TimberbornCompatibilityProbeResult.Failed(name, isRequired, feature, message)
            : TimberbornCompatibilityProbeResult.Degraded(name, isRequired, feature, message);
    }

    private static TimberbornCompatibilityProbeResult ProbeProperty<T>(
        string name,
        bool isRequired,
        string feature,
        string propertyName,
        Type expectedType)
    {
        PropertyInfo? property = typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);

        return property?.PropertyType == expectedType
            ? TimberbornCompatibilityProbeResult.Passed(name, isRequired, feature)
            : CreateUnavailableResult(name, isRequired, feature, $"missing_property:{typeof(T).Name}.{propertyName}");
    }

    private static TimberbornCompatibilityProbeResult ProbeMethod<T>(
        string name,
        bool isRequired,
        string feature,
        string methodName,
        params Type[] parameterTypes)
    {
        MethodInfo? method = typeof(T).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: parameterTypes,
            modifiers: null);

        return method is not null
            ? TimberbornCompatibilityProbeResult.Passed(name, isRequired, feature)
            : CreateUnavailableResult(name, isRequired, feature, $"missing_method:{typeof(T).Name}.{methodName}");
    }

    private static TimberbornCompatibilityProbeResult ProbeGenericVector3IntMethod<T>(
        string name,
        bool isRequired,
        string feature)
    {
        bool foundMethod = typeof(T)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Any(static method =>
                method.Name == "GetObjectsWithComponentAt" &&
                method.IsGenericMethodDefinition &&
                method.GetParameters() is { Length: 1 } parameters &&
                parameters[0].ParameterType == typeof(Vector3Int));

        return foundMethod
            ? TimberbornCompatibilityProbeResult.Passed(name, isRequired, feature)
            : CreateUnavailableResult(name, isRequired, feature, $"missing_method:{typeof(T).Name}.GetObjectsWithComponentAt");
    }

    private static TimberbornCompatibilityProbeResult ProbeComputeShaderSupport()
    {
        try
        {
            return SystemInfo.supportsComputeShaders
                ? TimberbornCompatibilityProbeResult.Passed(
                    "unity_compute_shader_support",
                    isRequired: true,
                    "compute")
                : TimberbornCompatibilityProbeResult.Failed(
                    "unity_compute_shader_support",
                    isRequired: true,
                    "compute",
                    "compute_shaders_unsupported");
        }
        catch (Exception exception)
        {
            return TimberbornCompatibilityProbeResult.Failed(
                "unity_compute_shader_support",
                isRequired: true,
                "compute",
                $"message:{exception.Message}");
        }
    }

    private static IEnumerable<TimberbornCompatibilityProbeResult> ProbeAssetBundles()
    {
        try
        {
            TimberbornComputeShaderBundleProbe bundleProbe = TimberbornComputeShaderLoader.ProbeDeployedBundles();

            return new[]
            {
                ProbeComputeBundle(bundleProbe),
                ProbeDiagnosticBundle(bundleProbe),
            };
        }
        catch (Exception exception)
        {
            return new[]
            {
                TimberbornCompatibilityProbeResult.Failed(
                    "compute_shader_bundle",
                    isRequired: true,
                    "compute",
                    $"message:{exception.Message}"),
                TimberbornCompatibilityProbeResult.Degraded(
                    "diagnostic_asset_bundle",
                    isRequired: false,
                    "diagnostic_assets",
                    $"message:{exception.Message}"),
            };
        }
    }

    private static TimberbornCompatibilityProbeResult ProbeComputeBundle(TimberbornComputeShaderBundleProbe bundleProbe)
    {
        return ProbeUnityAssetBundleFile(
            "compute_shader_bundle",
            isRequired: true,
            "compute",
            bundleProbe.ComputeBundlePath,
            bundleProbe.ComputeBundleExists,
            bundleProbe.ComputeBundleSizeBytes,
            bundleProbe.ComputeBundleHeader,
            bundleProbe.ComputeBundleReadError);
    }

    private static TimberbornCompatibilityProbeResult ProbeDiagnosticBundle(TimberbornComputeShaderBundleProbe bundleProbe)
    {
        return ProbeUnityAssetBundleFile(
            "diagnostic_asset_bundle",
            isRequired: false,
            "diagnostic_assets",
            bundleProbe.DiagnosticBundlePath,
            bundleProbe.DiagnosticBundleExists,
            bundleProbe.DiagnosticBundleSizeBytes,
            bundleProbe.DiagnosticBundleHeader,
            bundleProbe.DiagnosticBundleReadError);
    }

    public static TimberbornCompatibilityProbeResult ProbeUnityAssetBundleFile(
        string name,
        bool isRequired,
        string feature,
        string path,
        bool exists,
        long? sizeBytes,
        string? header,
        string? readError)
    {
        if (!exists)
        {
            return CreateUnavailableResult(name, isRequired, feature, $"missing:{path}");
        }

        if (!string.IsNullOrWhiteSpace(readError))
        {
            return CreateUnavailableResult(name, isRequired, feature, $"read_error:{readError}");
        }

        if (!sizeBytes.HasValue || sizeBytes <= 0)
        {
            return CreateUnavailableResult(name, isRequired, feature, $"empty:{path}");
        }

        if (!IsUnityAssetBundleHeader(header))
        {
            return CreateUnavailableResult(name, isRequired, feature, $"invalid_asset_bundle_header:{header ?? "missing"}");
        }

        return TimberbornCompatibilityProbeResult.Passed(
            name,
            isRequired,
            feature,
            $"asset_bundle_header_valid:path={path}:bytes={sizeBytes}:header={header}:limitation=asset_and_kernel_validation_deferred_to_TWF-050");
    }

    private static bool IsUnityAssetBundleHeader(string? header)
    {
        return !string.IsNullOrWhiteSpace(header) &&
            (header.StartsWith("UnityFS", StringComparison.Ordinal) ||
                header.StartsWith("UnityRaw", StringComparison.Ordinal) ||
                header.StartsWith("UnityWeb", StringComparison.Ordinal));
    }

}
