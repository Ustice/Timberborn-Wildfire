using System.Globalization;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class WildfireReleaseSettingsTests
{
    [Fact]
    public void MissingSettingsUseConservativeDefaults()
    {
        WildfireReleaseSettings settings = WildfireReleaseSettings.FromStore(
            new DictionaryReleaseSettingsStore());

        WildfireReleaseSettingsSnapshot snapshot = settings.GetSnapshot();

        Assert.Equal(1, snapshot.SettingsSchemaVersion);
        Assert.Equal("test", snapshot.SourceName);
        Assert.Empty(snapshot.InvalidValues);
        Assert.Equal([WildfireReleaseSettings.SettingsSchemaVersionKey], WildfireReleaseSettings.StableKeys);
        Assert.Contains("wildfire_release_settings", snapshot.StatusToken);
        Assert.Contains("settings_schema_version=1", snapshot.StatusToken);
        Assert.Contains("invalid_values=0", snapshot.StatusToken);
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
