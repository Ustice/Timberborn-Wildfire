using System.Globalization;
using System.Text.RegularExpressions;

namespace Wildfire.Timberborn;

public readonly record struct TimberbornResourceFuelProfile(
    string ResourceId,
    byte FuelValue,
    byte Flammability,
    bool Explosive,
    bool Contaminated,
    bool Known);

public sealed class TimberbornResourceFuelCatalog
{
    public static readonly TimberbornResourceFuelCatalog Default = FromBlueprintRoot();

    public static readonly TimberbornResourceFuelProfile UnknownResourceProfile = new(
        ResourceId: "",
        FuelValue: 1,
        Flammability: 0,
        Explosive: false,
        Contaminated: false,
        Known: false);

    private readonly IReadOnlyDictionary<string, TimberbornResourceFuelProfile> _profilesByResourceId;

    public TimberbornResourceFuelCatalog(IEnumerable<TimberbornResourceFuelProfile> profiles)
    {
        if (profiles is null)
        {
            throw new ArgumentNullException(nameof(profiles));
        }

        _profilesByResourceId = profiles.ToDictionary(
            static profile => profile.ResourceId,
            static profile => profile,
            StringComparer.Ordinal);
    }

    public IReadOnlyCollection<string> KnownResourceIds => _profilesByResourceId.Keys.OrderBy(static id => id).ToArray();

    public static TimberbornResourceFuelCatalog FromBlueprintRoot(string? blueprintRoot = null)
    {
        return new TimberbornResourceFuelCatalog(
            TimberbornResourceFuelBlueprintLoader.LoadProfiles(blueprintRoot));
    }

    public bool Contains(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return false;
        }

        return _profilesByResourceId.ContainsKey(resourceId.Trim());
    }

    public TimberbornResourceFuelProfile Lookup(string? resourceId)
    {
        string normalizedResourceId = resourceId?.Trim() ?? "";
        if (normalizedResourceId.Length == 0)
        {
            return UnknownResourceProfile;
        }

        return _profilesByResourceId.TryGetValue(normalizedResourceId, out TimberbornResourceFuelProfile profile)
            ? profile
            : UnknownResourceProfile with { ResourceId = normalizedResourceId };
    }

}

internal static class TimberbornResourceFuelBlueprintLoader
{
    private const string BlueprintRootEnvironmentVariable = "WILDFIRE_BLUEPRINT_ROOT";

    public static IEnumerable<TimberbornResourceFuelProfile> LoadProfiles(string? blueprintRoot)
    {
        string root = ResolveBlueprintRoot(blueprintRoot);
        string goodsRoot = Path.Combine(root, "Goods");
        if (!Directory.Exists(goodsRoot))
        {
            throw new InvalidOperationException($"Wildfire fuel blueprint Goods directory is missing: {goodsRoot}");
        }

        return Directory
            .EnumerateFiles(goodsRoot, "*.blueprint.json", SearchOption.AllDirectories)
            .Select(ReadProfile)
            .OrderBy(static profile => profile.ResourceId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ResolveBlueprintRoot(string? explicitRoot)
    {
        string[] candidates = CandidateBlueprintRoots(explicitRoot)
            .Where(static candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(static candidate => Path.GetFullPath(candidate))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return candidates.FirstOrDefault(Directory.Exists) ??
            throw new InvalidOperationException(
                "Wildfire fuel blueprints are missing. Run `bun run blueprints:generate` or set WILDFIRE_BLUEPRINT_ROOT. " +
                $"Checked: {string.Join(", ", candidates)}");
    }

    private static IEnumerable<string> CandidateBlueprintRoots(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            yield return explicitRoot;
        }

        string? environmentRoot = Environment.GetEnvironmentVariable(BlueprintRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentRoot))
        {
            yield return environmentRoot;
        }

        yield return Path.Combine(AppContext.BaseDirectory, "..", "Blueprints");

        foreach (string ancestor in Ancestors(AppContext.BaseDirectory).Concat(Ancestors(Directory.GetCurrentDirectory())))
        {
            yield return Path.Combine(ancestor, "src", "Wildfire.Timberborn", "Blueprints");
        }
    }

    private static IEnumerable<string> Ancestors(string start)
    {
        DirectoryInfo? directory = new(Path.GetFullPath(start));
        while (directory is not null)
        {
            yield return directory.FullName;
            directory = directory.Parent;
        }
    }

    private static TimberbornResourceFuelProfile ReadProfile(string path)
    {
        string text = File.ReadAllText(path).TrimStart('\uFEFF');
        string resourceId = ResourceIdFromPath(path);

        return Profile(
            resourceId,
            ToPackedFuelValue(ReadDouble(text, "Fuel")),
            ToFlammability(ReadInt32(text, "Flammability")),
            ReadBoolean(text, "Explosive"),
            ReadBoolean(text, "Contaminated"),
            known: true);
    }

    private static string ResourceIdFromPath(string path)
    {
        string fileName = Path.GetFileName(path);
        Match match = Regex.Match(
            fileName,
            "^Good\\.(?<id>.+)\\.blueprint\\.json$",
            RegexOptions.CultureInvariant);

        return match.Success
            ? match.Groups["id"].Value
            : throw new InvalidOperationException($"Wildfire fuel blueprint path is not a Good blueprint: {path}");
    }

    private static byte ToPackedFuelValue(double fuel)
    {
        if (fuel <= 0)
        {
            return 0;
        }

        return (byte)Math.Clamp((int)Math.Ceiling(fuel), 1, 15);
    }

    private static byte ToFlammability(int flammability)
    {
        return (byte)Math.Clamp(flammability, 0, 3);
    }

    private static int ReadInt32(string text, string propertyName)
    {
        return (int)Math.Round(ReadDouble(text, propertyName));
    }

    private static double ReadDouble(string text, string propertyName)
    {
        Match match = Regex.Match(
            text,
            $"\"{Regex.Escape(propertyName)}\"\\s*:\\s*(?<value>-?\\d+(?:\\.\\d+)?)",
            RegexOptions.CultureInvariant);

        return match.Success &&
            double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : throw new InvalidOperationException($"Missing or invalid {propertyName} in WildfireFuelSpec.");
    }

    private static bool ReadBoolean(string text, string propertyName)
    {
        Match match = Regex.Match(
            text,
            $"\"{Regex.Escape(propertyName)}\"\\s*:\\s*(?<value>true|false)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        return match.Success && bool.TryParse(match.Groups["value"].Value, out bool value)
            ? value
            : throw new InvalidOperationException($"Missing or invalid {propertyName} in WildfireFuelSpec.");
    }

    private static TimberbornResourceFuelProfile Profile(
        string resourceId,
        byte fuelValue,
        byte flammability,
        bool explosive,
        bool contaminated,
        bool known)
    {
        return new TimberbornResourceFuelProfile(
            resourceId,
            fuelValue,
            flammability,
            explosive,
            contaminated,
            known);
    }
}
