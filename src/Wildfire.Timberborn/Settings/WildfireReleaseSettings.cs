using System.Globalization;
using Timberborn.SettingsSystem;
using Timberborn.SingletonSystem;

namespace Wildfire.Timberborn.Settings;

public sealed class WildfireReleaseSettings
{
    public const string ModId = "JasonKleinberg.Wildfire";
    public const string KeyPrefix = ModId + ".release.";
    public const string SettingsSchemaVersionKey = KeyPrefix + "settings_schema_version";
    public const string WildfireEnabledKey = KeyPrefix + "wildfire_enabled";
    public const string ExplosiveInfrastructureEnabledKey = KeyPrefix + "explosive_infrastructure_enabled";
    public const string NativeDynamiteTriggerEnabledKey = KeyPrefix + "native_dynamite_trigger_enabled";
    public const string ExplosiveInfrastructureArmedThresholdTicksKey =
        KeyPrefix + "explosive_infrastructure_armed_threshold_ticks";
    public const string ExplosiveInfrastructurePulseHeatKey = KeyPrefix + "explosive_infrastructure_pulse_heat";
    public const string ExplosiveInfrastructurePulseRadiusKey = KeyPrefix + "explosive_infrastructure_pulse_radius";
    public const string DetonatorFireSafetyEnabledKey = KeyPrefix + "detonator_fire_safety_enabled";
    public const string TunnelFireBehaviorEnabledKey = KeyPrefix + "tunnel_fire_behavior_enabled";
    public const string TunnelTerrainDestructionEnabledKey = KeyPrefix + "tunnel_terrain_destruction_enabled";
    public const string VisualIntensityPercentKey = KeyPrefix + "visual_intensity_percent";
    public const string VisualDebugVisibilityKey = KeyPrefix + "visual_debug_visibility";
    public const int CurrentSettingsSchemaVersion = 1;
    public const bool DefaultWildfireEnabled = true;
    public const bool InvalidWildfireEnabled = false;
    public const bool DefaultExplosiveInfrastructureEnabled = true;
    public const bool DefaultNativeDynamiteTriggerEnabled = false;
    public const int DefaultExplosiveInfrastructureArmedThresholdTicks = 2;
    public const int DefaultExplosiveInfrastructurePulseHeat = 15;
    public const int DefaultExplosiveInfrastructurePulseRadius = 1;
    public const bool DefaultDetonatorFireSafetyEnabled = true;
    public const bool DefaultTunnelFireBehaviorEnabled = true;
    public const bool DefaultTunnelTerrainDestructionEnabled = false;
    public const int DefaultVisualIntensityPercent = 100;
    public const int MinimumVisualIntensityPercent = 25;
    public const int MaximumVisualIntensityPercent = 150;
    public const WildfireReleaseVisualDebugVisibility DefaultVisualDebugVisibility =
        WildfireReleaseVisualDebugVisibility.Hidden;

    public static readonly IReadOnlyList<string> StableKeys = new[]
    {
        SettingsSchemaVersionKey,
        WildfireEnabledKey,
        ExplosiveInfrastructureEnabledKey,
        NativeDynamiteTriggerEnabledKey,
        ExplosiveInfrastructureArmedThresholdTicksKey,
        ExplosiveInfrastructurePulseHeatKey,
        ExplosiveInfrastructurePulseRadiusKey,
        DetonatorFireSafetyEnabledKey,
        TunnelFireBehaviorEnabledKey,
        TunnelTerrainDestructionEnabledKey,
        VisualIntensityPercentKey,
        VisualDebugVisibilityKey,
    };

    private readonly IWildfireReleaseSettingsStore _store;

    public WildfireReleaseSettings(IWildfireReleaseSettingsStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static WildfireReleaseSettings FromStore(IWildfireReleaseSettingsStore store)
    {
        return new WildfireReleaseSettings(store);
    }

    public WildfireReleaseSettingsSnapshot GetSnapshot()
    {
        WildfireReleaseSettingsIntValue schemaVersion = ReadSchemaVersion();
        WildfireReleaseSettingsBoolValue wildfireEnabled = ReadWildfireEnabled();
        WildfireReleaseSettingsBoolValue explosiveInfrastructureEnabled = ReadBoolSetting(
            ExplosiveInfrastructureEnabledKey,
            DefaultExplosiveInfrastructureEnabled,
            invalidDefault: false);
        WildfireReleaseSettingsBoolValue nativeDynamiteTriggerEnabled = ReadBoolSetting(
            NativeDynamiteTriggerEnabledKey,
            DefaultNativeDynamiteTriggerEnabled,
            invalidDefault: false);
        WildfireReleaseSettingsIntValue armedThresholdTicks = ReadBoundedIntSetting(
            ExplosiveInfrastructureArmedThresholdTicksKey,
            DefaultExplosiveInfrastructureArmedThresholdTicks,
            minimum: 1,
            maximum: 120);
        WildfireReleaseSettingsIntValue pulseHeat = ReadBoundedIntSetting(
            ExplosiveInfrastructurePulseHeatKey,
            DefaultExplosiveInfrastructurePulseHeat,
            minimum: 0,
            maximum: byte.MaxValue);
        WildfireReleaseSettingsIntValue pulseRadius = ReadBoundedIntSetting(
            ExplosiveInfrastructurePulseRadiusKey,
            DefaultExplosiveInfrastructurePulseRadius,
            minimum: 0,
            maximum: 8);
        WildfireReleaseSettingsBoolValue detonatorFireSafetyEnabled = ReadBoolSetting(
            DetonatorFireSafetyEnabledKey,
            DefaultDetonatorFireSafetyEnabled,
            invalidDefault: false);
        WildfireReleaseSettingsBoolValue tunnelFireBehaviorEnabled = ReadBoolSetting(
            TunnelFireBehaviorEnabledKey,
            DefaultTunnelFireBehaviorEnabled,
            invalidDefault: false);
        WildfireReleaseSettingsBoolValue tunnelTerrainDestructionEnabled = ReadBoolSetting(
            TunnelTerrainDestructionEnabledKey,
            DefaultTunnelTerrainDestructionEnabled,
            invalidDefault: false);
        WildfireReleaseSettingsIntValue visualIntensityPercent = ReadBoundedIntSetting(
            VisualIntensityPercentKey,
            DefaultVisualIntensityPercent,
            MinimumVisualIntensityPercent,
            MaximumVisualIntensityPercent);
        WildfireReleaseSettingsEnumValue<WildfireReleaseVisualDebugVisibility> visualDebugVisibility =
            ReadEnumSetting(
                VisualDebugVisibilityKey,
                DefaultVisualDebugVisibility,
                DefaultVisualDebugVisibility);

        return new WildfireReleaseSettingsSnapshot(
            schemaVersion.Value,
            wildfireEnabled.Value,
            explosiveInfrastructureEnabled.Value,
            nativeDynamiteTriggerEnabled.Value,
            armedThresholdTicks.Value,
            pulseHeat.Value,
            pulseRadius.Value,
            detonatorFireSafetyEnabled.Value,
            tunnelFireBehaviorEnabled.Value,
            tunnelTerrainDestructionEnabled.Value,
            visualIntensityPercent.Value,
            visualDebugVisibility.Value,
            _store.SourceName,
            schemaVersion.InvalidValues
                .Concat(wildfireEnabled.InvalidValues)
                .Concat(explosiveInfrastructureEnabled.InvalidValues)
                .Concat(nativeDynamiteTriggerEnabled.InvalidValues)
                .Concat(armedThresholdTicks.InvalidValues)
                .Concat(pulseHeat.InvalidValues)
                .Concat(pulseRadius.InvalidValues)
                .Concat(detonatorFireSafetyEnabled.InvalidValues)
                .Concat(tunnelFireBehaviorEnabled.InvalidValues)
                .Concat(tunnelTerrainDestructionEnabled.InvalidValues)
                .Concat(visualIntensityPercent.InvalidValues)
                .Concat(visualDebugVisibility.InvalidValues)
                .ToArray());
    }

    private WildfireReleaseSettingsIntValue ReadSchemaVersion()
    {
        WildfireReleaseSettingReadResult result = _store.ReadInt32(SettingsSchemaVersionKey);

        if (result.Status == WildfireReleaseSettingReadStatus.Missing)
        {
            return WildfireReleaseSettingsIntValue.Valid(CurrentSettingsSchemaVersion);
        }

        if (result.Status == WildfireReleaseSettingReadStatus.Invalid)
        {
            return WildfireReleaseSettingsIntValue.Defaulted(
                CurrentSettingsSchemaVersion,
                SettingsSchemaVersionKey,
                result.RawValue,
                CurrentSettingsSchemaVersion.ToString(CultureInfo.InvariantCulture),
                "not_an_integer");
        }

        if (result.Value == CurrentSettingsSchemaVersion)
        {
            return WildfireReleaseSettingsIntValue.Valid(result.Value);
        }

        return WildfireReleaseSettingsIntValue.Defaulted(
            CurrentSettingsSchemaVersion,
            SettingsSchemaVersionKey,
            result.RawValue,
            CurrentSettingsSchemaVersion.ToString(CultureInfo.InvariantCulture),
            $"outside_supported_range_1_to_{CurrentSettingsSchemaVersion}");
    }

    private WildfireReleaseSettingsBoolValue ReadWildfireEnabled()
    {
        return ReadBoolSetting(WildfireEnabledKey, DefaultWildfireEnabled, InvalidWildfireEnabled);
    }

    private WildfireReleaseSettingsBoolValue ReadBoolSetting(string key, bool defaultValue, bool invalidDefault)
    {
        WildfireReleaseSettingReadResult result = _store.ReadInt32(key);

        if (result.Status == WildfireReleaseSettingReadStatus.Missing)
        {
            return WildfireReleaseSettingsBoolValue.Valid(defaultValue);
        }

        if (result.Status == WildfireReleaseSettingReadStatus.Invalid)
        {
            return WildfireReleaseSettingsBoolValue.Defaulted(
                invalidDefault,
                key,
                result.RawValue,
                FormatBoolSetting(invalidDefault),
                "not_an_integer");
        }

        if (result.Value is 0 or 1)
        {
            return WildfireReleaseSettingsBoolValue.Valid(result.Value == 1);
        }

        return WildfireReleaseSettingsBoolValue.Defaulted(
            invalidDefault,
            key,
            result.RawValue,
            FormatBoolSetting(invalidDefault),
            "outside_supported_range_0_to_1");
    }

    private WildfireReleaseSettingsIntValue ReadBoundedIntSetting(
        string key,
        int defaultValue,
        int minimum,
        int maximum)
    {
        WildfireReleaseSettingReadResult result = _store.ReadInt32(key);

        if (result.Status == WildfireReleaseSettingReadStatus.Missing)
        {
            return WildfireReleaseSettingsIntValue.Valid(defaultValue);
        }

        if (result.Status == WildfireReleaseSettingReadStatus.Invalid)
        {
            return WildfireReleaseSettingsIntValue.Defaulted(
                defaultValue,
                key,
                result.RawValue,
                defaultValue.ToString(CultureInfo.InvariantCulture),
                "not_an_integer");
        }

        if (result.Value >= minimum && result.Value <= maximum)
        {
            return WildfireReleaseSettingsIntValue.Valid(result.Value);
        }

        return WildfireReleaseSettingsIntValue.Defaulted(
            defaultValue,
            key,
            result.RawValue,
            defaultValue.ToString(CultureInfo.InvariantCulture),
            $"outside_supported_range_{minimum}_to_{maximum}");
    }

    private WildfireReleaseSettingsEnumValue<TEnum> ReadEnumSetting<TEnum>(
        string key,
        TEnum defaultValue,
        TEnum invalidDefault)
        where TEnum : struct, Enum
    {
        WildfireReleaseSettingReadResult result = _store.ReadInt32(key);

        if (result.Status == WildfireReleaseSettingReadStatus.Missing)
        {
            return WildfireReleaseSettingsEnumValue<TEnum>.Valid(defaultValue);
        }

        if (result.Status == WildfireReleaseSettingReadStatus.Invalid)
        {
            return WildfireReleaseSettingsEnumValue<TEnum>.Defaulted(
                invalidDefault,
                key,
                result.RawValue,
                Convert.ToInt32(invalidDefault, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                "not_an_integer");
        }

        if (Enum.IsDefined(typeof(TEnum), result.Value))
        {
            return WildfireReleaseSettingsEnumValue<TEnum>.Valid((TEnum)Enum.ToObject(typeof(TEnum), result.Value));
        }

        string supportedValues = string.Join(
            "_",
            Enum.GetValues(typeof(TEnum))
                .Cast<TEnum>()
                .Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)));
        return WildfireReleaseSettingsEnumValue<TEnum>.Defaulted(
            invalidDefault,
            key,
            result.RawValue,
            Convert.ToInt32(invalidDefault, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            $"outside_supported_values_{supportedValues}");
    }

    private static string FormatBoolSetting(bool value)
    {
        return value ? "1" : "0";
    }

}

public sealed class TimberbornSettingsSystemWildfireReleaseSettingsStore : IWildfireReleaseSettingsStore
{
    private const int InvalidIntegerSentinel = int.MinValue;

    private readonly ISettings _settings;

    public TimberbornSettingsSystemWildfireReleaseSettingsStore(ISettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public string SourceName => "Timberborn.SettingsSystem.ISettings";

    public WildfireReleaseSettingReadResult ReadInt32(string key)
    {
        if (!_settings.Has(key))
        {
            return WildfireReleaseSettingReadResult.Missing();
        }

        int value = _settings.GetSafeInt(key, InvalidIntegerSentinel);

        return value == InvalidIntegerSentinel
            ? WildfireReleaseSettingReadResult.Invalid("<unreadable_integer>")
            : WildfireReleaseSettingReadResult.Valid(value);
    }
}

public sealed class WildfireReleaseSettingsInitializer : ILoadableSingleton
{
    private readonly WildfireReleaseSettingsLogReporter _logReporter;

    public WildfireReleaseSettingsInitializer(WildfireReleaseSettings settings)
    {
        _logReporter = new WildfireReleaseSettingsLogReporter(
            settings ?? throw new ArgumentNullException(nameof(settings)),
            new UnityTimberbornFireLogSink());
    }

    public void Load()
    {
        _logReporter.Report();
    }
}

public sealed class WildfireReleaseSettingsLogReporter
{
    private readonly WildfireReleaseSettings _settings;
    private readonly ITimberbornFireLogSink _logSink;

    public WildfireReleaseSettingsLogReporter(
        WildfireReleaseSettings settings,
        ITimberbornFireLogSink logSink)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
    }

    public void Report()
    {
        WildfireReleaseSettingsSnapshot snapshot = _settings.GetSnapshot();

        if (snapshot.HasInvalidValues)
        {
            _logSink.Warning(snapshot.StatusToken);
            foreach (WildfireReleaseSettingInvalidValue invalidValue in snapshot.InvalidValues)
            {
                _logSink.Warning(invalidValue.StatusToken);
            }

            return;
        }

        _logSink.Info(snapshot.StatusToken);
    }
}

public interface IWildfireReleaseSettingsStore
{
    string SourceName { get; }

    WildfireReleaseSettingReadResult ReadInt32(string key);
}

public readonly record struct WildfireReleaseSettingReadResult(
    WildfireReleaseSettingReadStatus Status,
    int Value,
    string RawValue)
{
    public static WildfireReleaseSettingReadResult Missing()
    {
        return new WildfireReleaseSettingReadResult(
            WildfireReleaseSettingReadStatus.Missing,
            0,
            string.Empty);
    }

    public static WildfireReleaseSettingReadResult Valid(int value, string? rawValue = null)
    {
        return new WildfireReleaseSettingReadResult(
            WildfireReleaseSettingReadStatus.Valid,
            value,
            rawValue ?? value.ToString(CultureInfo.InvariantCulture));
    }

    public static WildfireReleaseSettingReadResult Invalid(string rawValue)
    {
        return new WildfireReleaseSettingReadResult(
            WildfireReleaseSettingReadStatus.Invalid,
            0,
            rawValue ?? string.Empty);
    }
}

public enum WildfireReleaseSettingReadStatus
{
    Missing,
    Valid,
    Invalid,
}

public enum WildfireReleaseVisualDebugVisibility
{
    Hidden = 0,
    SafeOverlay = 1,
}

public sealed record WildfireReleaseSettingsSnapshot(
    int SettingsSchemaVersion,
    bool IsWildfireEnabled,
    bool IsExplosiveInfrastructureEnabled,
    bool IsNativeDynamiteTriggerEnabled,
    int ExplosiveInfrastructureArmedThresholdTicks,
    int ExplosiveInfrastructurePulseHeat,
    int ExplosiveInfrastructurePulseRadius,
    bool IsDetonatorFireSafetyEnabled,
    bool IsTunnelFireBehaviorEnabled,
    bool IsTunnelTerrainDestructionEnabled,
    int VisualIntensityPercent,
    WildfireReleaseVisualDebugVisibility VisualDebugVisibility,
    string SourceName,
    IReadOnlyList<WildfireReleaseSettingInvalidValue> InvalidValues)
{
    public bool HasInvalidValues => InvalidValues.Count > 0;

    public bool IsVisualDebugOverlayEnabled => VisualDebugVisibility == WildfireReleaseVisualDebugVisibility.SafeOverlay;

    public float VisualIntensityScale => VisualIntensityPercent / 100f;

    public string StatusToken =>
        "wildfire_release_settings " +
        $"source={FormatToken(SourceName)} " +
        $"key_prefix={WildfireReleaseSettings.KeyPrefix} " +
        $"schema_key={WildfireReleaseSettings.SettingsSchemaVersionKey} " +
        $"wildfire_enabled_key={WildfireReleaseSettings.WildfireEnabledKey} " +
        $"settings_schema_version={SettingsSchemaVersion} " +
        $"wildfire_enabled={IsWildfireEnabled.ToString().ToLowerInvariant()} " +
        $"explosive_infrastructure_enabled={IsExplosiveInfrastructureEnabled.ToString().ToLowerInvariant()} " +
        $"native_dynamite_trigger_enabled={IsNativeDynamiteTriggerEnabled.ToString().ToLowerInvariant()} " +
        $"explosive_infrastructure_armed_threshold_ticks={ExplosiveInfrastructureArmedThresholdTicks} " +
        $"explosive_infrastructure_pulse_heat={ExplosiveInfrastructurePulseHeat} " +
        $"explosive_infrastructure_pulse_radius={ExplosiveInfrastructurePulseRadius} " +
        $"detonator_fire_safety_enabled={IsDetonatorFireSafetyEnabled.ToString().ToLowerInvariant()} " +
        $"tunnel_fire_behavior_enabled={IsTunnelFireBehaviorEnabled.ToString().ToLowerInvariant()} " +
        $"tunnel_terrain_destruction_enabled={IsTunnelTerrainDestructionEnabled.ToString().ToLowerInvariant()} " +
        $"visual_intensity_percent={VisualIntensityPercent} " +
        $"visual_debug_visibility={FormatVisualDebugVisibility(VisualDebugVisibility)} " +
        $"visual_debug_overlay_enabled={IsVisualDebugOverlayEnabled.ToString().ToLowerInvariant()} " +
        $"invalid_values={InvalidValues.Count} " +
        $"invalid_keys={FormatInvalidKeys(InvalidValues)}";

    private static string FormatInvalidKeys(IReadOnlyList<WildfireReleaseSettingInvalidValue> invalidValues)
    {
        if (invalidValues.Count == 0)
        {
            return "none";
        }

        return string.Join(",", invalidValues.Select(static invalidValue => invalidValue.Key));
    }

    private static string FormatToken(string value)
    {
        return value.Replace(' ', '_');
    }

    private static string FormatVisualDebugVisibility(WildfireReleaseVisualDebugVisibility visibility)
    {
        return visibility.ToString().ToLowerInvariant();
    }
}

public sealed record WildfireReleaseVisualSettings(
    int VisualIntensityPercent,
    float VisualIntensityScale,
    WildfireReleaseVisualDebugVisibility DebugVisibility,
    bool DebugOverlayEnabled)
{
    public static WildfireReleaseVisualSettings FromSnapshot(WildfireReleaseSettingsSnapshot snapshot)
    {
        return new WildfireReleaseVisualSettings(
            snapshot.VisualIntensityPercent,
            snapshot.VisualIntensityScale,
            snapshot.VisualDebugVisibility,
            snapshot.IsVisualDebugOverlayEnabled);
    }

    public TimberbornGpuFieldRendererOptions ToGpuFieldRendererOptions()
    {
        return new TimberbornGpuFieldRendererOptions(
            AshOverlayEnabled: true,
            DebugOverlayEnabled: DebugOverlayEnabled,
            VisualIntensityScale: VisualIntensityScale,
            IndirectFireRendererActive: true);
    }

    public TimberbornGpuIndirectFireRendererOptions ToGpuIndirectFireRendererOptions()
    {
        return new TimberbornGpuIndirectFireRendererOptions(VisualIntensityScale);
    }
}

public sealed record WildfireReleaseSettingInvalidValue(
    string Key,
    string RawValue,
    string DefaultValue,
    string Reason)
{
    public string StatusToken =>
        "wildfire_release_setting_invalid " +
        $"key={Key} " +
        $"raw={FormatToken(RawValue)} " +
        $"default={FormatToken(DefaultValue)} " +
        $"reason={FormatToken(Reason)}";

    private static string FormatToken(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "blank"
            : value.Replace(' ', '_');
    }
}

internal sealed record WildfireReleaseSettingsIntValue(
    int Value,
    IReadOnlyList<WildfireReleaseSettingInvalidValue> InvalidValues)
{
    public static WildfireReleaseSettingsIntValue Valid(int value)
    {
        return new WildfireReleaseSettingsIntValue(
            value,
            Array.Empty<WildfireReleaseSettingInvalidValue>());
    }

    public static WildfireReleaseSettingsIntValue Defaulted(
        int value,
        string key,
        string rawValue,
        string defaultValue,
        string reason)
    {
        return new WildfireReleaseSettingsIntValue(
            value,
            new[]
            {
                new WildfireReleaseSettingInvalidValue(key, rawValue, defaultValue, reason),
            });
    }
}

internal sealed record WildfireReleaseSettingsBoolValue(
    bool Value,
    IReadOnlyList<WildfireReleaseSettingInvalidValue> InvalidValues)
{
    public static WildfireReleaseSettingsBoolValue Valid(bool value)
    {
        return new WildfireReleaseSettingsBoolValue(
            value,
            Array.Empty<WildfireReleaseSettingInvalidValue>());
    }

    public static WildfireReleaseSettingsBoolValue Defaulted(
        bool value,
        string key,
        string rawValue,
        string defaultValue,
        string reason)
    {
        return new WildfireReleaseSettingsBoolValue(
            value,
            new[]
            {
                new WildfireReleaseSettingInvalidValue(key, rawValue, defaultValue, reason),
            });
    }
}

internal sealed record WildfireReleaseSettingsEnumValue<TEnum>(
    TEnum Value,
    IReadOnlyList<WildfireReleaseSettingInvalidValue> InvalidValues)
    where TEnum : struct, Enum
{
    public static WildfireReleaseSettingsEnumValue<TEnum> Valid(TEnum value)
    {
        return new WildfireReleaseSettingsEnumValue<TEnum>(
            value,
            Array.Empty<WildfireReleaseSettingInvalidValue>());
    }

    public static WildfireReleaseSettingsEnumValue<TEnum> Defaulted(
        TEnum value,
        string key,
        string rawValue,
        string defaultValue,
        string reason)
    {
        return new WildfireReleaseSettingsEnumValue<TEnum>(
            value,
            new[]
            {
                new WildfireReleaseSettingInvalidValue(key, rawValue, defaultValue, reason),
            });
    }
}
