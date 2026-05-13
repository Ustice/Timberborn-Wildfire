using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Wildfire.Timberborn;

public readonly record struct TimberbornResourceFuelProfile(
    string ResourceId,
    byte FuelValue,
    byte Flammability,
    bool Explosive,
    bool Contaminated,
    bool Known);

public readonly record struct TimberbornBurnableProfile(
    string SpecId,
    string Type,
    byte FuelValue,
    int DestructionThreshold,
    byte Flammability,
    bool Explosive,
    bool Contaminated,
    bool Known)
{
    public int DamageCapacity => DestructionThreshold > 0 ? DestructionThreshold : FuelValue;

    public bool IsBurnable => FuelValue > 0 && DamageCapacity > 0 && Flammability > 0;
}

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

public sealed class TimberbornBurnableCatalog
{
    public static readonly TimberbornBurnableCatalog Default = FromBlueprintRoot();

    public static readonly TimberbornBurnableProfile UnknownBurnableProfile = new(
        SpecId: "",
        Type: "unknown",
        FuelValue: 0,
        DestructionThreshold: 0,
        Flammability: 0,
        Explosive: false,
        Contaminated: false,
        Known: false);

    private readonly IReadOnlyDictionary<string, TimberbornBurnableProfile> _profilesBySpecId;
    private readonly IReadOnlyDictionary<string, TimberbornBurnableProfile> _unambiguousProfilesByBaseId;

    public TimberbornBurnableCatalog(IEnumerable<TimberbornBurnableProfile> profiles)
    {
        if (profiles is null)
        {
            throw new ArgumentNullException(nameof(profiles));
        }

        TimberbornBurnableProfile[] profileArray = profiles.ToArray();
        _profilesBySpecId = profileArray.ToDictionary(
            static profile => profile.SpecId,
            static profile => profile,
            StringComparer.Ordinal);
        _unambiguousProfilesByBaseId = profileArray
            .GroupBy(static profile => BaseId(profile.SpecId), StringComparer.Ordinal)
            .Where(static group => group.Count() == 1)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
    }

    public IReadOnlyCollection<string> KnownSpecIds => _profilesBySpecId.Keys.OrderBy(static id => id).ToArray();

    public static TimberbornBurnableCatalog FromBlueprintRoot(string? blueprintRoot = null)
    {
        return new TimberbornBurnableCatalog(
            TimberbornBurnableBlueprintLoader.LoadProfiles(blueprintRoot));
    }

    public bool Contains(string specId)
    {
        return Lookup(specId).Known;
    }

    public TimberbornBurnableProfile Lookup(string? specId)
    {
        string normalizedSpecId = NormalizeSpecId(specId);
        if (normalizedSpecId.Length == 0)
        {
            return UnknownBurnableProfile;
        }

        if (_profilesBySpecId.TryGetValue(normalizedSpecId, out TimberbornBurnableProfile profile))
        {
            return profile;
        }

        string unprefixedSpecId = StripKnownPrefix(normalizedSpecId);
        if (_profilesBySpecId.TryGetValue(unprefixedSpecId, out profile) ||
            _unambiguousProfilesByBaseId.TryGetValue(unprefixedSpecId, out profile))
        {
            return profile;
        }

        return UnknownBurnableProfile with { SpecId = normalizedSpecId };
    }

    private static string NormalizeSpecId(string? specId)
    {
        return (specId ?? "")
            .Replace("(Clone)", "", StringComparison.Ordinal)
            .Trim();
    }

    private static string StripKnownPrefix(string specId)
    {
        string[] prefixes = { "Tree.", "Crop.", "Building." };
        return prefixes
            .Where(prefix => specId.StartsWith(prefix, StringComparison.Ordinal))
            .Select(prefix => specId[prefix.Length..])
            .FirstOrDefault() ?? specId;
    }

    private static string BaseId(string specId)
    {
        int factionSeparator = specId.IndexOf('.');
        return factionSeparator < 0 ? specId : specId[..factionSeparator];
    }
}

internal static class TimberbornResourceFuelBlueprintLoader
{
    public static IEnumerable<TimberbornResourceFuelProfile> LoadProfiles(string? blueprintRoot)
    {
        string? root = TimberbornBlueprintRootResolver.TryResolveBlueprintRoot(blueprintRoot);
        if (root is not null)
        {
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

        return TimberbornEmbeddedBlueprintResources
            .ReadBlueprints(static name => name.Contains(".Goods.", StringComparison.Ordinal) &&
                name.Contains(".Good.", StringComparison.Ordinal))
            .Select(static blueprint => ReadProfile(blueprint.Name, blueprint.Text))
            .OrderBy(static profile => profile.ResourceId, StringComparer.Ordinal)
            .ToArray();
    }

    private static TimberbornResourceFuelProfile ReadProfile(string path)
    {
        return ReadProfile(path, File.ReadAllText(path).TrimStart('\uFEFF'));
    }

    private static TimberbornResourceFuelProfile ReadProfile(string path, string text)
    {
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
            "(?:^|\\.)Good\\.(?<id>[^.]+)\\.blueprint\\.json$",
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

internal static class TimberbornBurnableBlueprintLoader
{
    public static IEnumerable<TimberbornBurnableProfile> LoadProfiles(string? blueprintRoot)
    {
        string? root = TimberbornBlueprintRootResolver.TryResolveBlueprintRoot(blueprintRoot);
        if (root is not null)
        {
            return Directory
                .EnumerateFiles(root, "*.blueprint.json", SearchOption.AllDirectories)
                .Where(static path => !Path.GetFileName(path).StartsWith("Good.", StringComparison.Ordinal))
                .Select(ReadProfile)
                .OrderBy(static profile => profile.SpecId, StringComparer.Ordinal)
                .ToArray();
        }

        return TimberbornEmbeddedBlueprintResources
            .ReadBlueprints(static name => !name.Contains(".Goods.", StringComparison.Ordinal) &&
                name.EndsWith(".blueprint.json", StringComparison.Ordinal))
            .Select(static blueprint => ReadProfile(blueprint.Name, blueprint.Text))
            .OrderBy(static profile => profile.SpecId, StringComparer.Ordinal)
            .ToArray();
    }

    private static TimberbornBurnableProfile ReadProfile(string path)
    {
        return ReadProfile(path, File.ReadAllText(path).TrimStart('\uFEFF'));
    }

    private static TimberbornBurnableProfile ReadProfile(string path, string text)
    {
        string specId = SpecIdFromPath(path);

        return new TimberbornBurnableProfile(
            specId,
            ReadString(text, "Type"),
            ToPackedFuelValue(ReadDouble(text, "Fuel")),
            ToDestructionThreshold(ReadDouble(text, "Destruction Threshold")),
            ToFlammability(ReadInt32(text, "Flammability")),
            ReadBoolean(text, "Explosive"),
            ReadBoolean(text, "Contaminated"),
            Known: true);
    }

    private static string SpecIdFromPath(string path)
    {
        string fileName = Path.GetFileName(path);
        Match match = Regex.Match(
            fileName,
            "(?:^|\\.)(?<id>[^.]+(?:\\.(?:Folktails|IronTeeth))?)\\.blueprint\\.json$",
            RegexOptions.CultureInvariant);

        return match.Success
            ? match.Groups["id"].Value
            : throw new InvalidOperationException($"Wildfire burnable blueprint path is invalid: {path}");
    }

    private static byte ToPackedFuelValue(double fuel)
    {
        if (fuel <= 0)
        {
            return 0;
        }

        return (byte)Math.Clamp((int)Math.Ceiling(fuel), 1, 15);
    }

    private static int ToDestructionThreshold(double destructionThreshold)
    {
        if (destructionThreshold <= 0)
        {
            return 0;
        }

        return Math.Clamp((int)Math.Ceiling(destructionThreshold), 1, int.MaxValue);
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
            : throw new InvalidOperationException($"Missing or invalid {propertyName} in WildfireBurnableSpec.");
    }

    private static string ReadString(string text, string propertyName)
    {
        Match match = Regex.Match(
            text,
            $"\"{Regex.Escape(propertyName)}\"\\s*:\\s*\"(?<value>[^\"]*)\"",
            RegexOptions.CultureInvariant);

        return match.Success ? match.Groups["value"].Value : "";
    }

    private static bool ReadBoolean(string text, string propertyName)
    {
        Match match = Regex.Match(
            text,
            $"\"{Regex.Escape(propertyName)}\"\\s*:\\s*(?<value>true|false)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        return match.Success && bool.TryParse(match.Groups["value"].Value, out bool value)
            ? value
            : throw new InvalidOperationException($"Missing or invalid {propertyName} in WildfireBurnableSpec.");
    }
}

internal static class TimberbornBlueprintRootResolver
{
    private const string BlueprintRootEnvironmentVariable = "WILDFIRE_BLUEPRINT_ROOT";

    public static string ResolveBlueprintRoot(string? explicitRoot)
    {
        return TryResolveBlueprintRoot(explicitRoot) ??
            throw new InvalidOperationException(
                "Wildfire blueprints are missing. Run `bun run blueprints:generate` or set WILDFIRE_BLUEPRINT_ROOT. " +
                $"Checked: {string.Join(", ", CandidateBlueprintRoots(explicitRoot).Select(Path.GetFullPath).Distinct(StringComparer.Ordinal))}");
    }

    public static string? TryResolveBlueprintRoot(string? explicitRoot)
    {
        string[] candidates = CandidateBlueprintRoots(explicitRoot)
            .Where(static candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(static candidate => Path.GetFullPath(candidate))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return candidates.FirstOrDefault(Directory.Exists);
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

        string? assemblyLocation = typeof(TimberbornBlueprintRootResolver).Assembly.Location;
        if (!string.IsNullOrWhiteSpace(assemblyLocation))
        {
            yield return Path.Combine(Path.GetDirectoryName(assemblyLocation) ?? "", "..", "Blueprints");
        }

        foreach (string ancestor in Ancestors(AppContext.BaseDirectory)
            .Concat(AssemblyAncestors())
            .Concat(Ancestors(Directory.GetCurrentDirectory())))
        {
            yield return Path.Combine(ancestor, "src", "Wildfire.Timberborn", "Blueprints");
        }
    }

    private static IEnumerable<string> AssemblyAncestors()
    {
        string? assemblyLocation = typeof(TimberbornBlueprintRootResolver).Assembly.Location;
        return string.IsNullOrWhiteSpace(assemblyLocation)
            ? Array.Empty<string>()
            : Ancestors(Path.GetDirectoryName(assemblyLocation) ?? "");
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
}

internal static class TimberbornEmbeddedBlueprintResources
{
    private static readonly Assembly Assembly = typeof(TimberbornEmbeddedBlueprintResources).Assembly;

    public static IEnumerable<TimberbornEmbeddedBlueprint> ReadBlueprints(Func<string, bool> predicate)
    {
        TimberbornEmbeddedBlueprint[] blueprints = Assembly
            .GetManifestResourceNames()
            .Where(static name => name.Contains(".Blueprints.", StringComparison.Ordinal) &&
                name.EndsWith(".blueprint.json", StringComparison.Ordinal))
            .Where(predicate)
            .Select(ReadBlueprint)
            .ToArray();

        return blueprints.Length > 0
            ? blueprints
            : throw new InvalidOperationException("Embedded Wildfire blueprints are missing from Wildfire.Timberborn.dll.");
    }

    private static TimberbornEmbeddedBlueprint ReadBlueprint(string resourceName)
    {
        using Stream stream = Assembly.GetManifestResourceStream(resourceName) ??
            throw new InvalidOperationException($"Embedded Wildfire blueprint is missing: {resourceName}");
        using StreamReader reader = new(stream);

        return new TimberbornEmbeddedBlueprint(resourceName, reader.ReadToEnd().TrimStart('\uFEFF'));
    }
}

internal readonly record struct TimberbornEmbeddedBlueprint(string Name, string Text);
