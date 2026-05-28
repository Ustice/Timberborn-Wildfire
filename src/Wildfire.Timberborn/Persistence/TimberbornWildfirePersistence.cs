using System.Globalization;
using System.Text;
using Timberborn.Persistence;
using Timberborn.WorldPersistence;
using Wildfire.Core;

namespace Wildfire.Timberborn.Persistence;

public sealed record TimberbornFireSimPersistenceSnapshot(
    int Width,
    int Height,
    int Depth,
    uint Tick,
    IReadOnlyList<ushort> Cells,
    IReadOnlyList<uint> TransportFields)
{
    public IReadOnlyList<uint> AtmosphericFields => TransportFields;
}

public sealed record TimberbornConsequencePersistenceSnapshot(
    IReadOnlyList<TimberbornBurnDamagePersistenceEntry> BurnDamageStates);

public sealed record TimberbornBurnDamagePersistenceEntry(
    string TargetKey,
    int DamageTaken,
    uint LastDamagedTick);

public sealed record TimberbornWildfirePersistenceSnapshot(
    int PersistenceVersion,
    TimberbornFireSimPersistenceSnapshot? FireSim,
    TimberbornAshFieldSnapshot AshField,
    TimberbornBeaverFieldBehaviorSnapshot BeaverBehavior,
    TimberbornConsequencePersistenceSnapshot Consequences)
{
    public const int CurrentPersistenceVersion = 1;

    public static readonly TimberbornWildfirePersistenceSnapshot Empty = new(
        CurrentPersistenceVersion,
        FireSim: null,
        new TimberbornAshFieldSnapshot(
            TimberbornAshFieldEntry.CurrentPersistenceVersion,
            Array.Empty<TimberbornAshFieldEntry>()),
        TimberbornBeaverFieldBehaviorSnapshot.Empty,
        new TimberbornConsequencePersistenceSnapshot(Array.Empty<TimberbornBurnDamagePersistenceEntry>()));
}

public interface ITimberbornFireSimPersistenceState
{
    TimberbornFireSimPersistenceSnapshot CaptureFireSimState();

    void RestoreFireSimState(TimberbornFireSimPersistenceSnapshot snapshot);
}

public static class TimberbornWildfirePersistenceCodec
{
    private const char Separator = '\t';
    private const string HeaderRecord = "WF";
    private const string FireSimRecord = "FIRE";
    private const string AshRecord = "ASH";
    private const string BeaverBehaviorRecord = "BEAVER";
    private const string BurnDamageRecord = "BURN";

    public static string Encode(TimberbornWildfirePersistenceSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        List<string> lines = new()
        {
            string.Join(Separator, HeaderRecord, snapshot.PersistenceVersion.ToString(CultureInfo.InvariantCulture)),
        };

        if (snapshot.FireSim is { } fireSim)
        {
            lines.Add(string.Join(
                Separator,
                FireSimRecord,
                fireSim.Width.ToString(CultureInfo.InvariantCulture),
                fireSim.Height.ToString(CultureInfo.InvariantCulture),
                fireSim.Depth.ToString(CultureInfo.InvariantCulture),
                fireSim.Tick.ToString(CultureInfo.InvariantCulture),
                EncodeUInt16Array(fireSim.Cells),
                EncodeUInt32Array(fireSim.TransportFields)));
        }

        snapshot.AshField.Entries
            .OrderBy(static entry => entry.CellIndex)
            .ToList()
            .ForEach(entry => lines.Add(string.Join(
                Separator,
                AshRecord,
                entry.CellIndex.ToString(CultureInfo.InvariantCulture),
                ((int)entry.Quality).ToString(CultureInfo.InvariantCulture),
                entry.Strength.ToString(CultureInfo.InvariantCulture),
                ((int)entry.SourceKind).ToString(CultureInfo.InvariantCulture),
                entry.CreatedTick.ToString(CultureInfo.InvariantCulture),
                entry.UpdatedTick.ToString(CultureInfo.InvariantCulture),
                entry.PersistenceVersion.ToString(CultureInfo.InvariantCulture),
                entry.CreatedDayNumber.ToString(CultureInfo.InvariantCulture),
                entry.UpdatedDayNumber.ToString(CultureInfo.InvariantCulture))));

        snapshot.BeaverBehavior.Entries
            .OrderBy(static entry => entry.BeaverId, StringComparer.Ordinal)
            .ToList()
            .ForEach(entry => lines.Add(string.Join(
                Separator,
                BeaverBehaviorRecord,
                EncodeString(entry.BeaverId),
                ((int)entry.LastVariant).ToString(CultureInfo.InvariantCulture),
                ((int)entry.LastAction).ToString(CultureInfo.InvariantCulture),
                entry.LastDecisionTick.ToString(CultureInfo.InvariantCulture),
                entry.ConsecutiveExposedSamples.ToString(CultureInfo.InvariantCulture),
                entry.IsExposed ? "1" : "0",
                entry.ConsecutiveFireHeatExposedSamples.ToString(CultureInfo.InvariantCulture),
                entry.PersistenceVersion.ToString(CultureInfo.InvariantCulture))));

        snapshot.Consequences.BurnDamageStates
            .OrderBy(static entry => entry.TargetKey, StringComparer.Ordinal)
            .ToList()
            .ForEach(entry => lines.Add(string.Join(
                Separator,
                BurnDamageRecord,
                EncodeString(entry.TargetKey),
                entry.DamageTaken.ToString(CultureInfo.InvariantCulture),
                entry.LastDamagedTick.ToString(CultureInfo.InvariantCulture))));

        return string.Join("\n", lines);
    }

    public static TimberbornWildfirePersistenceSnapshot Decode(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return TimberbornWildfirePersistenceSnapshot.Empty;
        }

        string[] lines = encoded
            .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.TrimEnd('\r'))
            .ToArray();
        if (lines.Length == 0)
        {
            return TimberbornWildfirePersistenceSnapshot.Empty;
        }

        string[] header = SplitLine(lines[0]);
        if (header.Length < 2 ||
            header[0] != HeaderRecord ||
            ParseInt(header[1]) != TimberbornWildfirePersistenceSnapshot.CurrentPersistenceVersion)
        {
            return TimberbornWildfirePersistenceSnapshot.Empty;
        }

        TimberbornFireSimPersistenceSnapshot? fireSim = null;
        List<TimberbornAshFieldEntry> ashEntries = new();
        List<TimberbornBeaverFieldBehaviorStateEntry> beaverBehaviorEntries = new();
        List<TimberbornBurnDamagePersistenceEntry> burnDamageEntries = new();

        lines.Skip(1)
            .Select(SplitLine)
            .ToList()
            .ForEach(parts =>
            {
                if (parts.Length == 0)
                {
                    return;
                }

                switch (parts[0])
                {
                    case FireSimRecord when parts.Length >= 7:
                        fireSim = DecodeFireSim(parts);
                        break;
                    case AshRecord when parts.Length >= 8:
                        ashEntries.Add(DecodeAshEntry(parts));
                        break;
                    case BeaverBehaviorRecord when parts.Length >= 7:
                        beaverBehaviorEntries.Add(DecodeBeaverBehaviorEntry(parts));
                        break;
                    case BurnDamageRecord when parts.Length >= 4:
                        burnDamageEntries.Add(new TimberbornBurnDamagePersistenceEntry(
                            DecodeString(parts[1]),
                            ParseInt(parts[2]),
                            ParseUInt(parts[3])));
                        break;
                }
            });

        return new TimberbornWildfirePersistenceSnapshot(
            TimberbornWildfirePersistenceSnapshot.CurrentPersistenceVersion,
            fireSim,
            new TimberbornAshFieldSnapshot(
                TimberbornAshFieldEntry.CurrentPersistenceVersion,
                ashEntries),
            new TimberbornBeaverFieldBehaviorSnapshot(
                TimberbornBeaverFieldBehaviorSnapshot.CurrentPersistenceVersion,
                beaverBehaviorEntries),
            new TimberbornConsequencePersistenceSnapshot(burnDamageEntries));
    }

    public static TimberbornConsequencePersistenceSnapshot CaptureConsequences(
        TimberbornBurnDamageService? burnDamageService)
    {
        return new TimberbornConsequencePersistenceSnapshot(
            burnDamageService is null
                ? Array.Empty<TimberbornBurnDamagePersistenceEntry>()
                : burnDamageService.CaptureState()
                    .Select(static snapshot => new TimberbornBurnDamagePersistenceEntry(
                        snapshot.TargetKey.StableId,
                        snapshot.DamageTaken,
                        snapshot.LastDamagedTick))
                    .ToArray());
    }

    public static void RestoreConsequences(
        TimberbornBurnDamageService? burnDamageService,
        TimberbornConsequencePersistenceSnapshot snapshot)
    {
        if (burnDamageService is null || snapshot is null)
        {
            return;
        }

        Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamagePersistenceEntry> savedStates =
            snapshot.BurnDamageStates
                .Where(static entry => !string.IsNullOrWhiteSpace(entry.TargetKey))
                .GroupBy(static entry => new TimberbornBurnDamageTargetKey(entry.TargetKey))
                .ToDictionary(static group => group.Key, static group => group.First());

        TimberbornBurnDamageStateSnapshot[] restoredSnapshots = burnDamageService.CaptureState()
            .Select(current => savedStates.TryGetValue(current.TargetKey, out TimberbornBurnDamagePersistenceEntry saved)
                ? current with
                {
                    DamageTaken = saved.DamageTaken,
                    LastDamagedTick = saved.LastDamagedTick,
                }
                : current)
            .ToArray();
        burnDamageService.RestoreState(restoredSnapshots);
    }

    private static TimberbornFireSimPersistenceSnapshot DecodeFireSim(IReadOnlyList<string> parts)
    {
        return new TimberbornFireSimPersistenceSnapshot(
            ParseInt(parts[1]),
            ParseInt(parts[2]),
            ParseInt(parts[3]),
            ParseUInt(parts[4]),
            DecodeUInt16Array(parts[5]),
            DecodeUInt32Array(parts[6]));
    }

    private static TimberbornAshFieldEntry DecodeAshEntry(IReadOnlyList<string> parts)
    {
        int persistenceVersion = ParseInt(parts[7]);
        return new TimberbornAshFieldEntry(
            ParseInt(parts[1]),
            (WildfireAshQuality)ParseInt(parts[2]),
            ParseInt(parts[3]),
            (TimberbornAshSourceKind)ParseInt(parts[4]),
            ParseUInt(parts[5]),
            ParseUInt(parts[6]),
            persistenceVersion,
            CreatedDayNumber: parts.Count >= 10 ? ParseInt(parts[8]) : 0,
            UpdatedDayNumber: parts.Count >= 10 ? ParseInt(parts[9]) : 0);
    }

    private static TimberbornBeaverFieldBehaviorStateEntry DecodeBeaverBehaviorEntry(IReadOnlyList<string> parts)
    {
        bool hasLastAction = parts.Count >= 8;
        bool hasFireHeatSamples = parts.Count >= 9;
        TimberbornBeaverFieldBehaviorVariant variant =
            (TimberbornBeaverFieldBehaviorVariant)ParseInt(parts[2]);
        int exposedSamples = ParseInt(hasLastAction ? parts[5] : parts[4]);
        int fireHeatSamples = hasFireHeatSamples
            ? ParseInt(parts[7])
            : variant == TimberbornBeaverFieldBehaviorVariant.FireHeat
                ? exposedSamples
                : 0;

        return new TimberbornBeaverFieldBehaviorStateEntry(
            ParseInt(hasFireHeatSamples ? parts[8] : hasLastAction ? parts[7] : parts[6]),
            DecodeString(parts[1]),
            variant,
            hasLastAction
                ? (TimberbornBeaverFieldBehaviorAction)ParseInt(parts[3])
                : TimberbornBeaverFieldBehaviorAction.NoOp,
            ParseUInt(hasLastAction ? parts[4] : parts[3]),
            exposedSamples,
            fireHeatSamples,
            ParseInt(hasLastAction ? parts[6] : parts[5]) != 0);
    }

    private static string[] SplitLine(string line)
    {
        return line.Split(Separator);
    }

    private static string EncodeString(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }

    private static string DecodeString(string value)
    {
        return Encoding.UTF8.GetString(Convert.FromBase64String(value));
    }

    private static string EncodeUInt16Array(IReadOnlyList<ushort> values)
    {
        byte[] bytes = values
            .SelectMany(static value => BitConverter.GetBytes(value))
            .ToArray();
        return Convert.ToBase64String(bytes);
    }

    private static ushort[] DecodeUInt16Array(string encoded)
    {
        byte[] bytes = Convert.FromBase64String(encoded);
        return Enumerable.Range(0, bytes.Length / sizeof(ushort))
            .Select(index => BitConverter.ToUInt16(bytes, index * sizeof(ushort)))
            .ToArray();
    }

    private static string EncodeUInt32Array(IReadOnlyList<uint> values)
    {
        byte[] bytes = values
            .SelectMany(static value => BitConverter.GetBytes(value))
            .ToArray();
        return Convert.ToBase64String(bytes);
    }

    private static uint[] DecodeUInt32Array(string encoded)
    {
        byte[] bytes = Convert.FromBase64String(encoded);
        return Enumerable.Range(0, bytes.Length / sizeof(uint))
            .Select(index => BitConverter.ToUInt32(bytes, index * sizeof(uint)))
            .ToArray();
    }

    private static int ParseInt(string value)
    {
        return int.Parse(value, CultureInfo.InvariantCulture);
    }

    private static uint ParseUInt(string value)
    {
        return uint.Parse(value, CultureInfo.InvariantCulture);
    }
}

public static class TimberbornWildfirePersistenceKeys
{
    public static readonly SingletonKey Singleton = new("WildfireRuntime");

    public static readonly PropertyKey<string> Snapshot = new("Snapshot");
}
