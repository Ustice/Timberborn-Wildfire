using Wildfire.Core;

namespace Wildfire.Timberborn;

public interface ITimberbornWorldCellSourceProvider
{
    TimberbornWorldCellImportProviderResult Import(FireGrid grid);
}

public sealed record TimberbornWorldCellImportProviderResult(
    string Family,
    IReadOnlyList<TimberbornCellSource> Sources,
    int SkippedSafeUnavailableCount = 0,
    string SafeUnavailableReason = "none")
{
    public static TimberbornWorldCellImportProviderResult SafeUnavailable(string family, string reason)
    {
        return new TimberbornWorldCellImportProviderResult(
            family,
            Array.Empty<TimberbornCellSource>(),
            SkippedSafeUnavailableCount: 1,
            SafeUnavailableReason: reason);
    }
}

public sealed record TimberbornWorldCellImportSummary(
    int TotalSources,
    IReadOnlyDictionary<WildfireMaterialClass, int> SourceCountsByMaterialClass,
    IReadOnlyDictionary<WildfireMaterialClass, int> ResolvedCellCountsByMaterialClass,
    IReadOnlyDictionary<string, int> ProviderSourceCounts,
    IReadOnlyDictionary<string, int> ProviderSafeUnavailableCounts)
{
    public string StatusToken =>
        "wildfire_timberborn_world_import_summary " +
        $"total_sources={TotalSources} " +
        $"terrain_sources={Count(WildfireMaterialClass.Terrain)} " +
        $"tree_sources={Count(WildfireMaterialClass.Tree)} " +
        $"crop_sources={Count(WildfireMaterialClass.Crop)} " +
        $"building_sources={Count(WildfireMaterialClass.Building)} " +
        $"storage_sources={Count(WildfireMaterialClass.Storage)} " +
        $"infrastructure_sources={Count(WildfireMaterialClass.Infrastructure)} " +
        $"water_sources={Count(WildfireMaterialClass.Water)} " +
        $"badwater_sources={Count(WildfireMaterialClass.Badwater)} " +
        $"resolved_empty_cells={ResolvedCount(WildfireMaterialClass.Empty)} " +
        $"resolved_terrain_cells={ResolvedCount(WildfireMaterialClass.Terrain)} " +
        $"resolved_tree_cells={ResolvedCount(WildfireMaterialClass.Tree)} " +
        $"resolved_crop_cells={ResolvedCount(WildfireMaterialClass.Crop)} " +
        $"resolved_building_cells={ResolvedCount(WildfireMaterialClass.Building)} " +
        $"resolved_storage_cells={ResolvedCount(WildfireMaterialClass.Storage)} " +
        $"resolved_infrastructure_cells={ResolvedCount(WildfireMaterialClass.Infrastructure)} " +
        $"resolved_water_cells={ResolvedCount(WildfireMaterialClass.Water)} " +
        $"resolved_badwater_cells={ResolvedCount(WildfireMaterialClass.Badwater)} " +
        $"safe_unavailable={ProviderSafeUnavailableCounts.Values.Sum()} " +
        $"safe_unavailable_families={TimberbornQaCommandBridge.FormatToken(FormatSafeUnavailableFamilies())}";

    public int Count(WildfireMaterialClass materialClass)
    {
        return SourceCountsByMaterialClass.TryGetValue(materialClass, out int count) ? count : 0;
    }

    public int ResolvedCount(WildfireMaterialClass materialClass)
    {
        return ResolvedCellCountsByMaterialClass.TryGetValue(materialClass, out int count) ? count : 0;
    }

    private string FormatSafeUnavailableFamilies()
    {
        string[] unavailableFamilies = ProviderSafeUnavailableCounts
            .Where(static item => item.Value > 0)
            .Select(static item => $"{item.Key}:{item.Value}")
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();

        return unavailableFamilies.Length == 0
            ? "none"
            : string.Join(",", unavailableFamilies);
    }
}

public sealed record TimberbornWorldCellImportResult(
    IReadOnlyList<TimberbornCellSource> Sources,
    WildfireCompanionField[] CompanionFields,
    TimberbornWorldCellImportSummary Summary);

public sealed class TimberbornWorldCellImporter
{
    private readonly IReadOnlyList<ITimberbornWorldCellSourceProvider> _providers;
    private readonly TimberbornFireCellMapper _cellMapper;

    public TimberbornWorldCellImporter(IEnumerable<ITimberbornWorldCellSourceProvider> providers)
        : this(providers, new TimberbornFireCellMapper())
    {
    }

    public TimberbornWorldCellImporter(
        IEnumerable<ITimberbornWorldCellSourceProvider> providers,
        TimberbornFireCellMapper cellMapper)
    {
        if (providers is null)
        {
            throw new ArgumentNullException(nameof(providers));
        }

        _providers = providers.ToArray();
        _cellMapper = cellMapper ?? throw new ArgumentNullException(nameof(cellMapper));
    }

    public TimberbornWorldCellImportResult Import(FireGrid grid)
    {
        TimberbornWorldCellImportProviderResult[] providerResults = _providers
            .Select(provider => provider.Import(grid))
            .ToArray();
        TimberbornCellSource[] sources = providerResults
            .SelectMany(static result => result.Sources)
            .ToArray();
        WildfireCompanionField[] companionFields = _cellMapper.CreateCompanionFields(grid, sources);

        return new TimberbornWorldCellImportResult(
            sources,
            companionFields,
            CreateSummary(providerResults, sources, companionFields));
    }

    private static TimberbornWorldCellImportSummary CreateSummary(
        IReadOnlyList<TimberbornWorldCellImportProviderResult> providerResults,
        IReadOnlyList<TimberbornCellSource> sources,
        IReadOnlyList<WildfireCompanionField> companionFields)
    {
        IReadOnlyDictionary<WildfireMaterialClass, int> sourceCountsByMaterialClass = sources
            .GroupBy(static source => source.MaterialClass)
            .ToDictionary(static group => group.Key, static group => group.Count());
        IReadOnlyDictionary<WildfireMaterialClass, int> resolvedCountsByMaterialClass = companionFields
            .GroupBy(static field => field.State.MaterialClass)
            .ToDictionary(static group => group.Key, static group => group.Count());
        IReadOnlyDictionary<string, int> providerSourceCounts = providerResults
            .ToDictionary(static result => result.Family, static result => result.Sources.Count, StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, int> providerSafeUnavailableCounts = providerResults
            .ToDictionary(static result => result.Family, static result => result.SkippedSafeUnavailableCount, StringComparer.OrdinalIgnoreCase);

        return new TimberbornWorldCellImportSummary(
            sources.Count,
            sourceCountsByMaterialClass,
            resolvedCountsByMaterialClass,
            providerSourceCounts,
            providerSafeUnavailableCounts);
    }
}

public sealed class TimberbornStaticCellSourceProvider : ITimberbornWorldCellSourceProvider
{
    private readonly IReadOnlyList<TimberbornCellSource> _sources;

    public TimberbornStaticCellSourceProvider(string family, IEnumerable<TimberbornCellSource> sources)
    {
        Family = string.IsNullOrWhiteSpace(family)
            ? throw new ArgumentException("Provider family is required.", nameof(family))
            : family;
        _sources = (sources ?? throw new ArgumentNullException(nameof(sources))).ToArray();
    }

    public string Family { get; }

    public TimberbornWorldCellImportProviderResult Import(FireGrid grid)
    {
        return new TimberbornWorldCellImportProviderResult(Family, _sources);
    }
}

public sealed class TimberbornSafeUnavailableCellSourceProvider : ITimberbornWorldCellSourceProvider
{
    private readonly string _reason;

    public TimberbornSafeUnavailableCellSourceProvider(string family, string reason)
    {
        Family = string.IsNullOrWhiteSpace(family)
            ? throw new ArgumentException("Provider family is required.", nameof(family))
            : family;
        _reason = string.IsNullOrWhiteSpace(reason) ? "safe_api_unavailable" : reason;
    }

    public string Family { get; }

    public TimberbornWorldCellImportProviderResult Import(FireGrid grid)
    {
        return TimberbornWorldCellImportProviderResult.SafeUnavailable(Family, _reason);
    }
}
