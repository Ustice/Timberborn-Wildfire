using System.Globalization;
using System.Security.Cryptography;
using Timberborn.GameSaveRepositorySystem;

namespace Wildfire.Timberborn.Qa;

internal sealed class TimberbornQaSaveCopy
{
    private readonly SaveReference _loaded;
    private readonly string _loadedName;
    private readonly string _settlementName;
    private readonly string _saveDirectory;
    internal Guid Id { get; }
    internal string Path { get; }
    internal string TempPath { get; }
    internal string State { get; set; } = "queued";
    internal bool Pending => State is "queued" or "running";
    internal bool Published { get; private set; }
    internal long? Bytes { get; private set; }
    internal string? Hash { get; private set; }
    internal string? Error { get; set; }
    internal float? NativeDayBefore { get; set; }
    internal float? NativeDayAtSave { get; set; }
    internal uint? RuntimeTick { get; set; }

    internal TimberbornQaSaveCopy(Guid id, SaveReference loaded, string path)
    {
        Id = id; _loaded = loaded; Path = path;
        _loadedName = loaded.SaveName;
        _settlementName = loaded.SettlementReference.SettlementName;
        _saveDirectory = loaded.SettlementReference.SaveDirectory;
        TempPath = path + "." + id.ToString("N") + ".partial";
    }

    internal bool Matches(SaveReference? current) => ReferenceEquals(current, _loaded) &&
        current.SaveName == _loadedName && current.SettlementReference.SettlementName == _settlementName &&
        current.SettlementReference.SaveDirectory == _saveDirectory;

    internal static bool ValidName(string name) => name is { Length: >= 4 and <= 80 } &&
        name.StartsWith("QA-", StringComparison.Ordinal) &&
        name.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_');

    internal static void RequireDestination(string path)
    {
        if (!Directory.Exists(System.IO.Path.GetDirectoryName(path)))
            throw new IOException("The current settlement directory does not exist.");
        if (File.Exists(path) || Directory.Exists(path)) throw new IOException("The QA destination already exists.");
    }

    internal void Publish(Action<Stream> serialize)
    {
        RequireDestination(Path);
        // Native SaveWriter closes its archive/output before returning. Our using also
        // guarantees closure if any registered native entry writer throws.
        using (var stream = new FileStream(TempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            serialize(stream);
        File.Move(TempPath, Path); // Atomic no-overwrite publication; keep partial on failure.
        Published = true;
        using var saved = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Bytes = saved.Length;
        using var sha = SHA256.Create();
        Hash = BitConverter.ToString(sha.ComputeHash(saved)).Replace("-", "").ToLowerInvariant();
    }

    internal string Describe()
    {
        string Token(object? value) => TimberbornQaCommandBridge.FormatToken(
            Convert.ToString(value, CultureInfo.InvariantCulture) ?? "unavailable");
        return $"request_id={Id:D} save_state={State} published={Published.ToString().ToLowerInvariant()} " +
            $"save_path={Token(Path)} temporary_path={Token(TempPath)} bytes={Token(Bytes)} sha256={Token(Hash)} " +
            $"loaded_save={Token(_loadedName)} settlement={Token(_settlementName)} " +
            $"native_day_before={Token(NativeDayBefore)} native_day_at_save={Token(NativeDayAtSave)} " +
            $"runtime_tick_at_save={Token(RuntimeTick)} error={Token(Error)}";
    }
}
