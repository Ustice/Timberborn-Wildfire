namespace Wildfire.Timberborn;

public enum TimberbornResourceSmokeProfile
{
    None,
    LightOrganic,
    DenseOrganic,
    DryWood,
    Paper,
    Oily,
    Chemical,
    Volatile,
}

public enum TimberbornResourceResidueQuality
{
    None,
    CleanAsh,
    CharredFiber,
    SpoiledOrganic,
    Mineral,
    Metal,
    Toxic,
    Unresolved,
}

public enum TimberbornResourceHazardClass
{
    Unknown,
    Inert,
    DryFuel,
    FoodLike,
    MedicineLike,
    Chemical,
    Volatile,
    Explosive,
}

public readonly record struct TimberbornResourceFuelProfile(
    string ResourceId,
    byte FuelValue,
    byte Flammability,
    TimberbornResourceSmokeProfile SmokeProfile,
    TimberbornResourceResidueQuality ResidueQuality,
    TimberbornResourceHazardClass HazardClass);

public sealed class TimberbornResourceFuelCatalog
{
    public static readonly TimberbornResourceFuelCatalog Default = new(CreateDefaultProfiles());

    public static readonly TimberbornResourceFuelProfile UnknownResourceProfile = new(
        ResourceId: "",
        FuelValue: 1,
        Flammability: 0,
        SmokeProfile: TimberbornResourceSmokeProfile.LightOrganic,
        ResidueQuality: TimberbornResourceResidueQuality.Unresolved,
        HazardClass: TimberbornResourceHazardClass.Unknown);

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

    private static IEnumerable<TimberbornResourceFuelProfile> CreateDefaultProfiles()
    {
        return InertProfiles()
            .Concat(DryFuelProfiles())
            .Concat(FoodLikeProfiles())
            .Concat(MedicineLikeProfiles())
            .Concat(ChemicalProfiles())
            .Concat(VolatileProfiles())
            .Concat(ExplosiveProfiles());
    }

    private static IEnumerable<TimberbornResourceFuelProfile> InertProfiles()
    {
        return new[]
        {
            Inert("Dirt", TimberbornResourceResidueQuality.Mineral),
            Inert("MetalBlock", TimberbornResourceResidueQuality.Metal),
            Inert("MetalPart", TimberbornResourceResidueQuality.Metal),
            Inert("ScrapMetal", TimberbornResourceResidueQuality.Metal),
            Inert("Water", TimberbornResourceResidueQuality.None),
            Profile("Badwater", 0, 0, TimberbornResourceSmokeProfile.Chemical, TimberbornResourceResidueQuality.Toxic, TimberbornResourceHazardClass.Chemical),
        };
    }

    private static IEnumerable<TimberbornResourceFuelProfile> DryFuelProfiles()
    {
        return new[]
        {
            Profile("Book", 6, 3, TimberbornResourceSmokeProfile.Paper, TimberbornResourceResidueQuality.CharredFiber, TimberbornResourceHazardClass.DryFuel),
            Profile("BotChassis", 5, 1, TimberbornResourceSmokeProfile.DryWood, TimberbornResourceResidueQuality.Metal, TimberbornResourceHazardClass.DryFuel),
            Profile("BotHead", 3, 1, TimberbornResourceSmokeProfile.DryWood, TimberbornResourceResidueQuality.Metal, TimberbornResourceHazardClass.DryFuel),
            Profile("BotLimb", 3, 1, TimberbornResourceSmokeProfile.DryWood, TimberbornResourceResidueQuality.Metal, TimberbornResourceHazardClass.DryFuel),
            Profile("Gear", 8, 2, TimberbornResourceSmokeProfile.DryWood, TimberbornResourceResidueQuality.CleanAsh, TimberbornResourceHazardClass.DryFuel),
            Profile("Log", 12, 2, TimberbornResourceSmokeProfile.DryWood, TimberbornResourceResidueQuality.CleanAsh, TimberbornResourceHazardClass.DryFuel),
            Profile("Paper", 5, 3, TimberbornResourceSmokeProfile.Paper, TimberbornResourceResidueQuality.CharredFiber, TimberbornResourceHazardClass.DryFuel),
            Profile("Plank", 10, 2, TimberbornResourceSmokeProfile.DryWood, TimberbornResourceResidueQuality.CleanAsh, TimberbornResourceHazardClass.DryFuel),
            Profile("PunchCard", 4, 3, TimberbornResourceSmokeProfile.Paper, TimberbornResourceResidueQuality.CharredFiber, TimberbornResourceHazardClass.DryFuel),
            Profile("TreatedPlank", 9, 2, TimberbornResourceSmokeProfile.Chemical, TimberbornResourceResidueQuality.Toxic, TimberbornResourceHazardClass.DryFuel),
        };
    }

    private static IEnumerable<TimberbornResourceFuelProfile> FoodLikeProfiles()
    {
        string[] wetFoodIds =
        {
            "Algae",
            "Berries",
            "Carrot",
            "Cassava",
            "CattailRoot",
            "Chestnut",
            "CoffeeBean",
            "Corn",
            "Eggplant",
            "Kohlrabi",
            "MangroveFruit",
            "Mushroom",
            "Potato",
            "Soybean",
            "Spadderdock",
        };

        string[] dryFoodIds =
        {
            "AlgaeRation",
            "Bread",
            "CanolaSeeds",
            "CattailCracker",
            "CattailFlour",
            "CornRation",
            "EggplantRation",
            "FermentedCassava",
            "FermentedMushroom",
            "FermentedSoybean",
            "GrilledChestnut",
            "GrilledPotato",
            "GrilledSpadderdock",
            "MaplePastry",
            "SunflowerSeeds",
            "Wheat",
            "WheatFlour",
        };

        TimberbornResourceFuelProfile[] liquidFoodProfiles =
        {
            Profile("CanolaOil", 6, 2, TimberbornResourceSmokeProfile.Oily, TimberbornResourceResidueQuality.SpoiledOrganic, TimberbornResourceHazardClass.FoodLike),
            Profile("MapleSyrup", 4, 1, TimberbornResourceSmokeProfile.DenseOrganic, TimberbornResourceResidueQuality.SpoiledOrganic, TimberbornResourceHazardClass.FoodLike),
        };

        return wetFoodIds
            .Select(static id => Profile(id, 3, 1, TimberbornResourceSmokeProfile.LightOrganic, TimberbornResourceResidueQuality.SpoiledOrganic, TimberbornResourceHazardClass.FoodLike))
            .Concat(dryFoodIds.Select(static id => Profile(id, 4, 2, TimberbornResourceSmokeProfile.DenseOrganic, TimberbornResourceResidueQuality.SpoiledOrganic, TimberbornResourceHazardClass.FoodLike)))
            .Concat(liquidFoodProfiles);
    }

    private static IEnumerable<TimberbornResourceFuelProfile> MedicineLikeProfiles()
    {
        return new[]
        {
            Profile("Antidote", 2, 1, TimberbornResourceSmokeProfile.Chemical, TimberbornResourceResidueQuality.Toxic, TimberbornResourceHazardClass.MedicineLike),
            Profile("Coffee", 2, 1, TimberbornResourceSmokeProfile.DenseOrganic, TimberbornResourceResidueQuality.SpoiledOrganic, TimberbornResourceHazardClass.MedicineLike),
            Profile("Dandelion", 2, 1, TimberbornResourceSmokeProfile.LightOrganic, TimberbornResourceResidueQuality.SpoiledOrganic, TimberbornResourceHazardClass.MedicineLike),
        };
    }

    private static IEnumerable<TimberbornResourceFuelProfile> ChemicalProfiles()
    {
        return new[]
        {
            Profile("Catalyst", 3, 1, TimberbornResourceSmokeProfile.Chemical, TimberbornResourceResidueQuality.Toxic, TimberbornResourceHazardClass.Chemical),
            Profile("Extract", 3, 1, TimberbornResourceSmokeProfile.Chemical, TimberbornResourceResidueQuality.Toxic, TimberbornResourceHazardClass.Chemical),
        };
    }

    private static IEnumerable<TimberbornResourceFuelProfile> VolatileProfiles()
    {
        return new[]
        {
            Profile("Biofuel", 8, 3, TimberbornResourceSmokeProfile.Oily, TimberbornResourceResidueQuality.Toxic, TimberbornResourceHazardClass.Volatile),
            Profile("Grease", 7, 2, TimberbornResourceSmokeProfile.Oily, TimberbornResourceResidueQuality.Toxic, TimberbornResourceHazardClass.Volatile),
            Profile("PineResin", 7, 3, TimberbornResourceSmokeProfile.Volatile, TimberbornResourceResidueQuality.Toxic, TimberbornResourceHazardClass.Volatile),
        };
    }

    private static IEnumerable<TimberbornResourceFuelProfile> ExplosiveProfiles()
    {
        return new[]
        {
            Profile("Explosives", 4, 3, TimberbornResourceSmokeProfile.Volatile, TimberbornResourceResidueQuality.Toxic, TimberbornResourceHazardClass.Explosive),
            Profile("Fireworks", 4, 3, TimberbornResourceSmokeProfile.Volatile, TimberbornResourceResidueQuality.Toxic, TimberbornResourceHazardClass.Explosive),
        };
    }

    private static TimberbornResourceFuelProfile Inert(
        string resourceId,
        TimberbornResourceResidueQuality residueQuality)
    {
        return Profile(
            resourceId,
            0,
            0,
            TimberbornResourceSmokeProfile.None,
            residueQuality,
            TimberbornResourceHazardClass.Inert);
    }

    private static TimberbornResourceFuelProfile Profile(
        string resourceId,
        byte fuelValue,
        byte flammability,
        TimberbornResourceSmokeProfile smokeProfile,
        TimberbornResourceResidueQuality residueQuality,
        TimberbornResourceHazardClass hazardClass)
    {
        return new TimberbornResourceFuelProfile(
            resourceId,
            fuelValue,
            flammability,
            smokeProfile,
            residueQuality,
            hazardClass);
    }
}
