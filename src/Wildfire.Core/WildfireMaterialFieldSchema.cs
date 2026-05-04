namespace Wildfire.Core;

public enum WildfireMaterialClass : byte
{
    Empty = 0,
    Terrain = 1,
    Vegetation = 2,
    Crop = 3,
    Tree = 4,
    Building = 5,
    Storage = 6,
    Infrastructure = 7,
    Water = 8,
    Badwater = 9,
    Unknown = 10,
}

public enum WildfireConsequenceTargetKind : byte
{
    None = 0,
    Crop = 1,
    Tree = 2,
    Structure = 3,
    Storage = 4,
    Infrastructure = 5,
    Water = 6,
}

public enum WildfireAshQuality : byte
{
    None = 0,
    Fertile = 1,
    Spent = 2,
    Tainted = 3,
}

public enum WildfireContaminationBehavior : byte
{
    None = 0,
    TaintIfSourceContaminated = 1,
    TaintedSource = 2,
    SuppressesWithoutCleaning = 3,
    FailClosed = 4,
}

public enum WildfireResourcePolicy : byte
{
    Fixed = 0,
    UseResourceCatalog = 1,
    FailClosed = 2,
}

public readonly record struct WildfireMaterialFieldProfile(
    WildfireMaterialClass MaterialClass,
    byte Fuel,
    byte Flammability,
    byte HeatLoss,
    byte Terrain,
    byte Water,
    byte BurnCapacity,
    WildfireConsequenceTargetKind ConsequenceTargetKind,
    WildfireAshQuality AshQuality,
    WildfireContaminationBehavior ContaminationBehavior,
    WildfireResourcePolicy ResourcePolicy);

public sealed class WildfireMaterialFieldSchema
{
    public const int CurrentFormatVersion = 1;

    public static readonly WildfireMaterialFieldSchema Default = new(CreateDefaultProfiles());

    private readonly IReadOnlyDictionary<WildfireMaterialClass, WildfireMaterialFieldProfile> _profilesByClass;

    public WildfireMaterialFieldSchema(IEnumerable<WildfireMaterialFieldProfile> profiles)
    {
        if (profiles is null)
        {
            throw new ArgumentNullException(nameof(profiles));
        }

        WildfireMaterialFieldProfile[] materialProfiles = profiles.ToArray();
        _profilesByClass = materialProfiles.ToDictionary(
            static profile => profile.MaterialClass,
            static profile => profile);
        Profiles = materialProfiles
            .OrderBy(static profile => profile.MaterialClass)
            .ToArray();
    }

    public IReadOnlyList<WildfireMaterialFieldProfile> Profiles { get; }

    public WildfireMaterialFieldProfile Lookup(WildfireMaterialClass materialClass)
    {
        return _profilesByClass.TryGetValue(materialClass, out WildfireMaterialFieldProfile profile)
            ? profile
            : _profilesByClass[WildfireMaterialClass.Unknown];
    }

    private static IEnumerable<WildfireMaterialFieldProfile> CreateDefaultProfiles()
    {
        return new[]
        {
            Profile(
                WildfireMaterialClass.Empty,
                fuel: 0,
                flammability: 0,
                heatLoss: 7,
                terrain: 0,
                water: 0,
                burnCapacity: 0,
                WildfireConsequenceTargetKind.None,
                WildfireAshQuality.None,
                WildfireContaminationBehavior.None,
                WildfireResourcePolicy.Fixed),
            Profile(
                WildfireMaterialClass.Terrain,
                fuel: 0,
                flammability: 0,
                heatLoss: 6,
                terrain: 1,
                water: 0,
                burnCapacity: 0,
                WildfireConsequenceTargetKind.None,
                WildfireAshQuality.None,
                WildfireContaminationBehavior.None,
                WildfireResourcePolicy.Fixed),
            Profile(
                WildfireMaterialClass.Vegetation,
                fuel: 10,
                flammability: 3,
                heatLoss: 1,
                terrain: 1,
                water: 0,
                burnCapacity: 10,
                WildfireConsequenceTargetKind.Tree,
                WildfireAshQuality.Fertile,
                WildfireContaminationBehavior.TaintIfSourceContaminated,
                WildfireResourcePolicy.Fixed),
            Profile(
                WildfireMaterialClass.Crop,
                fuel: 4,
                flammability: 2,
                heatLoss: 2,
                terrain: 1,
                water: 0,
                burnCapacity: 4,
                WildfireConsequenceTargetKind.Crop,
                WildfireAshQuality.Fertile,
                WildfireContaminationBehavior.TaintIfSourceContaminated,
                WildfireResourcePolicy.UseResourceCatalog),
            Profile(
                WildfireMaterialClass.Tree,
                fuel: 12,
                flammability: 2,
                heatLoss: 1,
                terrain: 1,
                water: 0,
                burnCapacity: 12,
                WildfireConsequenceTargetKind.Tree,
                WildfireAshQuality.Fertile,
                WildfireContaminationBehavior.TaintIfSourceContaminated,
                WildfireResourcePolicy.UseResourceCatalog),
            Profile(
                WildfireMaterialClass.Building,
                fuel: 15,
                flammability: 1,
                heatLoss: 3,
                terrain: 1,
                water: 0,
                burnCapacity: 15,
                WildfireConsequenceTargetKind.Structure,
                WildfireAshQuality.Fertile,
                WildfireContaminationBehavior.TaintIfSourceContaminated,
                WildfireResourcePolicy.UseResourceCatalog),
            Profile(
                WildfireMaterialClass.Storage,
                fuel: 8,
                flammability: 2,
                heatLoss: 3,
                terrain: 1,
                water: 0,
                burnCapacity: 8,
                WildfireConsequenceTargetKind.Storage,
                WildfireAshQuality.Fertile,
                WildfireContaminationBehavior.TaintIfSourceContaminated,
                WildfireResourcePolicy.UseResourceCatalog),
            Profile(
                WildfireMaterialClass.Infrastructure,
                fuel: 0,
                flammability: 0,
                heatLoss: 5,
                terrain: 1,
                water: 0,
                burnCapacity: 0,
                WildfireConsequenceTargetKind.Infrastructure,
                WildfireAshQuality.Fertile,
                WildfireContaminationBehavior.TaintIfSourceContaminated,
                WildfireResourcePolicy.UseResourceCatalog),
            Profile(
                WildfireMaterialClass.Water,
                fuel: 0,
                flammability: 0,
                heatLoss: 7,
                terrain: 0,
                water: 3,
                burnCapacity: 0,
                WildfireConsequenceTargetKind.Water,
                WildfireAshQuality.None,
                WildfireContaminationBehavior.SuppressesWithoutCleaning,
                WildfireResourcePolicy.Fixed),
            Profile(
                WildfireMaterialClass.Badwater,
                fuel: 0,
                flammability: 0,
                heatLoss: 7,
                terrain: 0,
                water: 3,
                burnCapacity: 0,
                WildfireConsequenceTargetKind.Water,
                WildfireAshQuality.Tainted,
                WildfireContaminationBehavior.TaintedSource,
                WildfireResourcePolicy.Fixed),
            Profile(
                WildfireMaterialClass.Unknown,
                fuel: 0,
                flammability: 0,
                heatLoss: 7,
                terrain: 0,
                water: 0,
                burnCapacity: 0,
                WildfireConsequenceTargetKind.None,
                WildfireAshQuality.None,
                WildfireContaminationBehavior.FailClosed,
                WildfireResourcePolicy.FailClosed),
        };
    }

    private static WildfireMaterialFieldProfile Profile(
        WildfireMaterialClass materialClass,
        byte fuel,
        byte flammability,
        byte heatLoss,
        byte terrain,
        byte water,
        byte burnCapacity,
        WildfireConsequenceTargetKind consequenceTargetKind,
        WildfireAshQuality ashQuality,
        WildfireContaminationBehavior contaminationBehavior,
        WildfireResourcePolicy resourcePolicy)
    {
        return new WildfireMaterialFieldProfile(
            materialClass,
            fuel,
            flammability,
            heatLoss,
            terrain,
            water,
            burnCapacity,
            consequenceTargetKind,
            ashQuality,
            contaminationBehavior,
            resourcePolicy);
    }
}
