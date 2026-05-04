using System.Globalization;
using Timberborn.SettingsSystem;
using Timberborn.SingletonSystem;

namespace Wildfire.Timberborn;

public sealed class WildfireReleaseSettings
{
    public const string ModId = "JasonKleinberg.Wildfire";
    public const string KeyPrefix = ModId + ".release.";
    public const string SettingsSchemaVersionKey = KeyPrefix + "settings_schema_version";
    public const string WildfireEnabledKey = KeyPrefix + "wildfire_enabled";
    public const int CurrentSettingsSchemaVersion = 1;
    public const bool DefaultWildfireEnabled = true;
    public const bool InvalidWildfireEnabled = false;

    public static readonly IReadOnlyList<string> StableKeys = new[]
    {
        SettingsSchemaVersionKey,
        WildfireEnabledKey,
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

        return new WildfireReleaseSettingsSnapshot(
            schemaVersion.Value,
            wildfireEnabled.Value,
            _store.SourceName,
            schemaVersion.InvalidValues.Concat(wildfireEnabled.InvalidValues).ToArray());
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
        WildfireReleaseSettingReadResult result = _store.ReadInt32(WildfireEnabledKey);

        if (result.Status == WildfireReleaseSettingReadStatus.Missing)
        {
            return WildfireReleaseSettingsBoolValue.Valid(DefaultWildfireEnabled);
        }

        if (result.Status == WildfireReleaseSettingReadStatus.Invalid)
        {
            return WildfireReleaseSettingsBoolValue.Defaulted(
                InvalidWildfireEnabled,
                WildfireEnabledKey,
                result.RawValue,
                FormatBoolSetting(InvalidWildfireEnabled),
                "not_an_integer");
        }

        if (result.Value is 0 or 1)
        {
            return WildfireReleaseSettingsBoolValue.Valid(result.Value == 1);
        }

        return WildfireReleaseSettingsBoolValue.Defaulted(
            InvalidWildfireEnabled,
            WildfireEnabledKey,
            result.RawValue,
            FormatBoolSetting(InvalidWildfireEnabled),
            "outside_supported_range_0_to_1");
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

public sealed record WildfireReleaseSettingsSnapshot(
    int SettingsSchemaVersion,
    bool IsWildfireEnabled,
    string SourceName,
    IReadOnlyList<WildfireReleaseSettingInvalidValue> InvalidValues)
{
    public bool HasInvalidValues => InvalidValues.Count > 0;

    public string StatusToken =>
        "wildfire_release_settings " +
        $"source={FormatToken(SourceName)} " +
        $"key_prefix={WildfireReleaseSettings.KeyPrefix} " +
        $"schema_key={WildfireReleaseSettings.SettingsSchemaVersionKey} " +
        $"wildfire_enabled_key={WildfireReleaseSettings.WildfireEnabledKey} " +
        $"settings_schema_version={SettingsSchemaVersion} " +
        $"wildfire_enabled={IsWildfireEnabled.ToString().ToLowerInvariant()} " +
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
