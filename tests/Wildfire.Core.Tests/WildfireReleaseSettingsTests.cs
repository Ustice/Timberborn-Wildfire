using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Timberborn.SettingsSystem;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class WildfireReleaseSettingsTests
{
    [Fact]
    public void BinditoBoundReleaseSettingTypesHaveOneParameterfulConstructor()
    {
        Type[] types =
        [
            typeof(TimberbornSettingsSystemWildfireReleaseSettingsStore),
            typeof(WildfireReleaseSettings),
        ];

        Assert.All(
            types,
            static type => Assert.Equal(
                1,
                type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Count(static constructor => constructor.GetParameters().Length > 0)));
    }

    [Fact]
    public void MissingSettingsUseConservativeDefaults()
    {
        WildfireReleaseSettings settings = WildfireReleaseSettings.FromStore(
            new DictionaryReleaseSettingsStore());

        WildfireReleaseSettingsSnapshot snapshot = settings.GetSnapshot();

        Assert.Equal(1, snapshot.SettingsSchemaVersion);
        Assert.True(snapshot.IsWildfireEnabled);
        Assert.True(snapshot.IsExplosiveInfrastructureEnabled);
        Assert.False(snapshot.IsNativeDynamiteTriggerEnabled);
        Assert.Equal(2, snapshot.ExplosiveInfrastructureArmedThresholdTicks);
        Assert.Equal(15, snapshot.ExplosiveInfrastructurePulseHeat);
        Assert.Equal(1, snapshot.ExplosiveInfrastructurePulseRadius);
        Assert.True(snapshot.IsDetonatorFireSafetyEnabled);
        Assert.True(snapshot.IsTunnelFireBehaviorEnabled);
        Assert.False(snapshot.IsTunnelTerrainDestructionEnabled);
        Assert.Equal("test", snapshot.SourceName);
        Assert.Empty(snapshot.InvalidValues);
        Assert.Equal(
            [
                WildfireReleaseSettings.SettingsSchemaVersionKey,
                WildfireReleaseSettings.WildfireEnabledKey,
                WildfireReleaseSettings.ExplosiveInfrastructureEnabledKey,
                WildfireReleaseSettings.NativeDynamiteTriggerEnabledKey,
                WildfireReleaseSettings.ExplosiveInfrastructureArmedThresholdTicksKey,
                WildfireReleaseSettings.ExplosiveInfrastructurePulseHeatKey,
                WildfireReleaseSettings.ExplosiveInfrastructurePulseRadiusKey,
                WildfireReleaseSettings.DetonatorFireSafetyEnabledKey,
                WildfireReleaseSettings.TunnelFireBehaviorEnabledKey,
                WildfireReleaseSettings.TunnelTerrainDestructionEnabledKey,
            ],
            WildfireReleaseSettings.StableKeys);
        Assert.Contains("wildfire_release_settings", snapshot.StatusToken);
        Assert.Contains("settings_schema_version=1", snapshot.StatusToken);
        Assert.Contains("wildfire_enabled=true", snapshot.StatusToken);
        Assert.Contains("explosive_infrastructure_enabled=true", snapshot.StatusToken);
        Assert.Contains("native_dynamite_trigger_enabled=false", snapshot.StatusToken);
        Assert.Contains("detonator_fire_safety_enabled=true", snapshot.StatusToken);
        Assert.Contains("tunnel_fire_behavior_enabled=true", snapshot.StatusToken);
        Assert.Contains("tunnel_terrain_destruction_enabled=false", snapshot.StatusToken);
        Assert.Contains("invalid_values=0", snapshot.StatusToken);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void WildfireEnabledSettingUsesStableZeroOneInterpretation(int rawValue, bool expectedEnabled)
    {
        WildfireReleaseSettings settings = WildfireReleaseSettings.FromStore(
            new DictionaryReleaseSettingsStore(
                new Dictionary<string, string?>
                {
                    [WildfireReleaseSettings.WildfireEnabledKey] = rawValue.ToString(),
                }));

        WildfireReleaseSettingsSnapshot snapshot = settings.GetSnapshot();

        Assert.Equal(expectedEnabled, snapshot.IsWildfireEnabled);
        Assert.Empty(snapshot.InvalidValues);
        Assert.Contains($"wildfire_enabled={expectedEnabled.ToString().ToLowerInvariant()}", snapshot.StatusToken);
    }

    [Fact]
    public void TimberbornSettingsReadsStoredIntegerZeroAsDisabled()
    {
        TypedTimberbornSettingsStore settingsStore = new(
            new Dictionary<string, int>
            {
                [WildfireReleaseSettings.SettingsSchemaVersionKey] = 1,
                [WildfireReleaseSettings.WildfireEnabledKey] = 0,
                [WildfireReleaseSettings.ExplosiveInfrastructureEnabledKey] = 1,
                [WildfireReleaseSettings.NativeDynamiteTriggerEnabledKey] = 0,
                [WildfireReleaseSettings.ExplosiveInfrastructureArmedThresholdTicksKey] = 2,
                [WildfireReleaseSettings.ExplosiveInfrastructurePulseHeatKey] = 15,
                [WildfireReleaseSettings.ExplosiveInfrastructurePulseRadiusKey] = 1,
                [WildfireReleaseSettings.DetonatorFireSafetyEnabledKey] = 1,
                [WildfireReleaseSettings.TunnelFireBehaviorEnabledKey] = 1,
                [WildfireReleaseSettings.TunnelTerrainDestructionEnabledKey] = 0,
            },
            new Dictionary<string, string>
            {
                [WildfireReleaseSettings.SettingsSchemaVersionKey] = "1",
                [WildfireReleaseSettings.WildfireEnabledKey] = "1",
            });
        WildfireReleaseSettings settings = new(
            new TimberbornSettingsSystemWildfireReleaseSettingsStore(settingsStore));

        WildfireReleaseSettingsSnapshot snapshot = settings.GetSnapshot();

        Assert.False(snapshot.IsWildfireEnabled);
        Assert.Equal(1, snapshot.SettingsSchemaVersion);
        Assert.Empty(snapshot.InvalidValues);
        Assert.Equal(
            WildfireReleaseSettings.StableKeys,
            settingsStore.SafeIntReads);
        Assert.Empty(settingsStore.SafeStringReads);
        Assert.Contains("wildfire_enabled=false", snapshot.StatusToken);
    }

    [Fact]
    public void TimberbornSettingsReadsSchemaVersionThroughIntegerSetting()
    {
        TypedTimberbornSettingsStore settingsStore = new(
            new Dictionary<string, int>
            {
                [WildfireReleaseSettings.SettingsSchemaVersionKey] = 2,
                [WildfireReleaseSettings.WildfireEnabledKey] = 1,
                [WildfireReleaseSettings.ExplosiveInfrastructureEnabledKey] = 1,
                [WildfireReleaseSettings.NativeDynamiteTriggerEnabledKey] = 0,
                [WildfireReleaseSettings.ExplosiveInfrastructureArmedThresholdTicksKey] = 2,
                [WildfireReleaseSettings.ExplosiveInfrastructurePulseHeatKey] = 15,
                [WildfireReleaseSettings.ExplosiveInfrastructurePulseRadiusKey] = 1,
                [WildfireReleaseSettings.DetonatorFireSafetyEnabledKey] = 1,
                [WildfireReleaseSettings.TunnelFireBehaviorEnabledKey] = 1,
                [WildfireReleaseSettings.TunnelTerrainDestructionEnabledKey] = 0,
            },
            new Dictionary<string, string>
            {
                [WildfireReleaseSettings.SettingsSchemaVersionKey] = "1",
                [WildfireReleaseSettings.WildfireEnabledKey] = "1",
            });
        WildfireReleaseSettings settings = new(
            new TimberbornSettingsSystemWildfireReleaseSettingsStore(settingsStore));

        WildfireReleaseSettingsSnapshot snapshot = settings.GetSnapshot();

        Assert.True(snapshot.IsWildfireEnabled);
        Assert.Equal(1, snapshot.SettingsSchemaVersion);
        WildfireReleaseSettingInvalidValue invalidValue = Assert.Single(snapshot.InvalidValues);
        Assert.Equal(WildfireReleaseSettings.SettingsSchemaVersionKey, invalidValue.Key);
        Assert.Equal("2", invalidValue.RawValue);
        Assert.Equal("outside_supported_range_1_to_1", invalidValue.Reason);
        Assert.Equal(
            WildfireReleaseSettings.StableKeys,
            settingsStore.SafeIntReads);
        Assert.Empty(settingsStore.SafeStringReads);
    }

    [Theory]
    [InlineData("bad", "not_an_integer")]
    [InlineData("2", "outside_supported_range_0_to_1")]
    public void InvalidWildfireEnabledSettingDefaultsDisabledAndReportsInvalidValue(
        string rawValue,
        string reason)
    {
        WildfireReleaseSettings settings = WildfireReleaseSettings.FromStore(
            new DictionaryReleaseSettingsStore(
                new Dictionary<string, string?>
                {
                    [WildfireReleaseSettings.WildfireEnabledKey] = rawValue,
                }));

        WildfireReleaseSettingsSnapshot snapshot = settings.GetSnapshot();

        Assert.False(snapshot.IsWildfireEnabled);
        WildfireReleaseSettingInvalidValue invalidValue = Assert.Single(snapshot.InvalidValues);
        Assert.Equal(WildfireReleaseSettings.WildfireEnabledKey, invalidValue.Key);
        Assert.Equal(rawValue, invalidValue.RawValue);
        Assert.Equal("0", invalidValue.DefaultValue);
        Assert.Equal(reason, invalidValue.Reason);
        Assert.Contains("wildfire_enabled=false", snapshot.StatusToken);
        Assert.Contains("invalid_values=1", snapshot.StatusToken);
        Assert.Contains(WildfireReleaseSettings.WildfireEnabledKey, snapshot.StatusToken);
    }

    [Fact]
    public void NonIntegerSchemaVersionDefaultsAndReportsInvalidValue()
    {
        WildfireReleaseSettings settings = WildfireReleaseSettings.FromStore(
            new DictionaryReleaseSettingsStore(
                new Dictionary<string, string?>
                {
                    [WildfireReleaseSettings.SettingsSchemaVersionKey] = "bad",
                }));

        WildfireReleaseSettingsSnapshot snapshot = settings.GetSnapshot();

        Assert.Equal(1, snapshot.SettingsSchemaVersion);
        WildfireReleaseSettingInvalidValue invalidValue = Assert.Single(snapshot.InvalidValues);
        Assert.Equal(WildfireReleaseSettings.SettingsSchemaVersionKey, invalidValue.Key);
        Assert.Equal("bad", invalidValue.RawValue);
        Assert.Equal("1", invalidValue.DefaultValue);
        Assert.Equal("not_an_integer", invalidValue.Reason);
        Assert.Contains("invalid_values=1", snapshot.StatusToken);
        Assert.Contains(WildfireReleaseSettings.SettingsSchemaVersionKey, snapshot.StatusToken);
        Assert.Contains("reason=not_an_integer", invalidValue.StatusToken);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void UnsupportedSchemaVersionDefaultsAndReportsInvalidValue(int schemaVersion)
    {
        WildfireReleaseSettings settings = WildfireReleaseSettings.FromStore(
            new DictionaryReleaseSettingsStore(
                new Dictionary<string, string?>
                {
                    [WildfireReleaseSettings.SettingsSchemaVersionKey] = schemaVersion.ToString(),
                }));

        WildfireReleaseSettingsSnapshot snapshot = settings.GetSnapshot();

        Assert.Equal(1, snapshot.SettingsSchemaVersion);
        WildfireReleaseSettingInvalidValue invalidValue = Assert.Single(snapshot.InvalidValues);
        Assert.Equal(schemaVersion.ToString(), invalidValue.RawValue);
        Assert.Equal("outside_supported_range_1_to_1", invalidValue.Reason);
    }

    [Fact]
    public void LogReporterLogsInvalidSettingsAsWarnings()
    {
        WildfireReleaseSettings settings = WildfireReleaseSettings.FromStore(
            new DictionaryReleaseSettingsStore(
                new Dictionary<string, string?>
                {
                    [WildfireReleaseSettings.SettingsSchemaVersionKey] = "bad",
                }));
        RecordingFireLogSink logSink = new();
        WildfireReleaseSettingsLogReporter reporter = new(settings, logSink);

        reporter.Report();

        Assert.Empty(logSink.InfoMessages);
        Assert.Equal(2, logSink.WarningMessages.Count);
        Assert.Contains("wildfire_release_settings", logSink.WarningMessages[0]);
        Assert.Contains("wildfire_release_setting_invalid", logSink.WarningMessages[1]);
    }

    private sealed class DictionaryReleaseSettingsStore : IWildfireReleaseSettingsStore
    {
        private readonly IReadOnlyDictionary<string, string?> _values;

        public DictionaryReleaseSettingsStore()
            : this(new Dictionary<string, string?>())
        {
        }

        public DictionaryReleaseSettingsStore(IReadOnlyDictionary<string, string?> values)
        {
            _values = values;
        }

        public string SourceName => "test";

        public WildfireReleaseSettingReadResult ReadInt32(string key)
        {
            if (!_values.TryGetValue(key, out string? rawValue))
            {
                return WildfireReleaseSettingReadResult.Missing();
            }

            return int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? WildfireReleaseSettingReadResult.Valid(value, rawValue)
                : WildfireReleaseSettingReadResult.Invalid(rawValue ?? string.Empty);
        }
    }

    private sealed class TypedTimberbornSettingsStore : ISettings
    {
        private readonly Dictionary<string, int> _intValues;
        private readonly Dictionary<string, string> _stringValues;

        public TypedTimberbornSettingsStore(
            IReadOnlyDictionary<string, int> intValues,
            IReadOnlyDictionary<string, string>? stringValues = null)
        {
            _intValues = new Dictionary<string, int>(intValues);
            _stringValues = stringValues is null ? [] : new Dictionary<string, string>(stringValues);
        }

        public List<string> SafeIntReads { get; } = [];

        public List<string> SafeStringReads { get; } = [];

        public int GetInt(string key, int defaultValue)
        {
            return _intValues.GetValueOrDefault(key, defaultValue);
        }

        public int GetSafeInt(string key, int defaultValue)
        {
            SafeIntReads.Add(key);
            return GetInt(key, defaultValue);
        }

        public void SetInt(string key, int value)
        {
            _intValues[key] = value;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            return GetInt(key, defaultValue ? 1 : 0) == 1;
        }

        public bool GetSafeBool(string key, bool defaultValue = false)
        {
            return GetBool(key, defaultValue);
        }

        public void SetBool(string key, bool value)
        {
            SetInt(key, value ? 1 : 0);
        }

        public float GetFloat(string key, float defaultValue)
        {
            return defaultValue;
        }

        public float GetSafeFloat(string key, float defaultValue)
        {
            return GetFloat(key, defaultValue);
        }

        public void SetFloat(string key, float value)
        {
        }

        public string GetString(string key, string defaultValue)
        {
            return _stringValues.GetValueOrDefault(key, defaultValue);
        }

        public string GetSafeString(string key, string defaultValue)
        {
            SafeStringReads.Add(key);
            return GetString(key, defaultValue);
        }

        public void SetString(string key, string value)
        {
            _stringValues[key] = value;
        }

        public bool Has(string key)
        {
            return _intValues.ContainsKey(key) || _stringValues.ContainsKey(key);
        }

        public void Clear(string key)
        {
            _intValues.Remove(key);
            _stringValues.Remove(key);
        }

        public void ValidateInt(string key, ImmutableArray<int> validValues, int defaultValue)
        {
        }

        public void ValidateString(string key, ImmutableArray<string> validValues, string defaultValue)
        {
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

}
