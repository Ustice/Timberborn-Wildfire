using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornResourceFuelCatalogTests
{
    [Fact]
    public void DefaultCatalogCoversInspectedShippedGoods()
    {
        TimberbornResourceFuelCatalog catalog = TimberbornResourceFuelCatalog.Default;

        Assert.Equal(61, catalog.KnownResourceIds.Count);
        Assert.Contains("Log", catalog.KnownResourceIds);
        Assert.Contains("Water", catalog.KnownResourceIds);
        Assert.Contains("Antidote", catalog.KnownResourceIds);
        Assert.Contains("Explosives", catalog.KnownResourceIds);
        Assert.Contains("Fireworks", catalog.KnownResourceIds);
        Assert.Contains("FertileAsh", catalog.KnownResourceIds);
    }

    [Fact]
    public void LookupDefaultsUnknownResourcesToSafeLowFuel()
    {
        TimberbornResourceFuelProfile profile = TimberbornResourceFuelCatalog.Default.Lookup("MysteryResource");

        Assert.Equal("MysteryResource", profile.ResourceId);
        Assert.Equal(1, profile.FuelValue);
        Assert.Equal(0, profile.Flammability);
        Assert.False(profile.Explosive);
        Assert.False(profile.Contaminated);
        Assert.False(profile.Known);
    }

    [Fact]
    public void LookupDefaultsBlankResourcesToAnonymousUnknownProfile()
    {
        TimberbornResourceFuelProfile profile = TimberbornResourceFuelCatalog.Default.Lookup(" ");

        Assert.Equal("", profile.ResourceId);
        Assert.Equal(1, profile.FuelValue);
        Assert.Equal(0, profile.Flammability);
        Assert.False(profile.Known);
    }

    [Theory]
    [InlineData("Dirt")]
    [InlineData("MetalBlock")]
    [InlineData("MetalPart")]
    [InlineData("ScrapMetal")]
    [InlineData("Water")]
    [InlineData("FertileAsh")]
    public void LookupClassifiesStoneMetalAndWaterAsInert(string resourceId)
    {
        TimberbornResourceFuelProfile profile = TimberbornResourceFuelCatalog.Default.Lookup(resourceId);

        Assert.Equal(0, profile.FuelValue);
        Assert.Equal(0, profile.Flammability);
        Assert.False(profile.Explosive);
        Assert.False(profile.Contaminated);
        Assert.True(profile.Known);
    }

    [Theory]
    [InlineData("Log", 2, 1)]
    [InlineData("Plank", 1, 2)]
    [InlineData("Gear", 1, 1)]
    [InlineData("Paper", 1, 3)]
    [InlineData("Book", 1, 3)]
    public void LookupClassifiesDryGoodsAsBurnableFuel(
        string resourceId,
        byte fuelValue,
        byte flammability)
    {
        TimberbornResourceFuelProfile profile = TimberbornResourceFuelCatalog.Default.Lookup(resourceId);

        Assert.Equal(fuelValue, profile.FuelValue);
        Assert.Equal(flammability, profile.Flammability);
        Assert.False(profile.Explosive);
        Assert.False(profile.Contaminated);
        Assert.True(profile.Known);
    }

    [Fact]
    public void LookupMarksContaminatedResourcesExplicitly()
    {
        TimberbornResourceFuelProfile badwater = TimberbornResourceFuelCatalog.Default.Lookup("Badwater");

        Assert.True(badwater.Known);
        Assert.True(badwater.Contaminated);
        Assert.False(badwater.Explosive);
    }

    [Fact]
    public void LookupMarksExplosiveResourcesExplicitly()
    {
        TimberbornResourceFuelProfile biofuel = TimberbornResourceFuelCatalog.Default.Lookup("Biofuel");
        TimberbornResourceFuelProfile explosives = TimberbornResourceFuelCatalog.Default.Lookup("Explosives");
        TimberbornResourceFuelProfile fireworks = TimberbornResourceFuelCatalog.Default.Lookup("Fireworks");

        Assert.False(biofuel.Explosive);
        Assert.Equal(2, biofuel.FuelValue);
        Assert.True(explosives.Explosive);
        Assert.Equal(1, explosives.FuelValue);
        Assert.True(fireworks.Explosive);
        Assert.Equal(1, fireworks.FuelValue);
    }

    [Fact]
    public void BurnableCatalogReadsTreeCropAndBuildingBlueprintValues()
    {
        TimberbornBurnableCatalog catalog = TimberbornBurnableCatalog.Default;

        TimberbornBurnableProfile pine = catalog.Lookup("Tree.Pine");
        TimberbornBurnableProfile corn = catalog.Lookup("Corn");
        TimberbornBurnableProfile lumberMill = catalog.Lookup("LumberMill.Folktails");

        Assert.Equal("Pine", pine.SpecId);
        Assert.Equal("tree", pine.Type);
        Assert.Equal(8, pine.FuelValue);
        Assert.Equal(6, pine.DestructionThreshold);
        Assert.Equal(3, pine.Flammability);
        Assert.True(pine.IsBurnable);
        Assert.Equal("Corn", corn.SpecId);
        Assert.Equal(2, corn.FuelValue);
        Assert.Equal(2, corn.DamageCapacity);
        Assert.Equal("LumberMill.Folktails", lumberMill.SpecId);
        Assert.Equal(3, lumberMill.FuelValue);
        Assert.Equal(2, lumberMill.DamageCapacity);
        Assert.Equal(1, lumberMill.Flammability);
    }

    [Fact]
    public void CatalogsFallBackToEmbeddedBlueprintsWhenRuntimePathIsUnavailable()
    {
        string unavailableRoot = Path.Combine(
            Path.GetTempPath(),
            $"wildfire-missing-blueprints-{Guid.NewGuid():N}");

        TimberbornResourceFuelCatalog fuelCatalog = TimberbornResourceFuelCatalog.FromBlueprintRoot(unavailableRoot);
        TimberbornBurnableCatalog burnableCatalog = TimberbornBurnableCatalog.FromBlueprintRoot(unavailableRoot);

        Assert.Equal(61, fuelCatalog.KnownResourceIds.Count);
        Assert.Equal(2, fuelCatalog.Lookup("Log").FuelValue);
        Assert.Equal(0, fuelCatalog.Lookup("FertileAsh").FuelValue);
        Assert.Equal(8, burnableCatalog.Lookup("Pine").FuelValue);
        Assert.Equal(3, burnableCatalog.Lookup("LumberMill.Folktails").FuelValue);
    }

    [Fact]
    public void BurnableCatalogPreservesFactionSpecificBuildingProfiles()
    {
        TimberbornBurnableCatalog catalog = TimberbornBurnableCatalog.Default;

        TimberbornBurnableProfile folktailsStatue = catalog.Lookup("BeaverStatue.Folktails");
        TimberbornBurnableProfile ironTeethStatue = catalog.Lookup("BeaverStatue.IronTeeth");
        TimberbornBurnableProfile ambiguousStatue = catalog.Lookup("BeaverStatue");

        Assert.Equal(11, folktailsStatue.FuelValue);
        Assert.Equal(10, folktailsStatue.DamageCapacity);
        Assert.True(folktailsStatue.IsBurnable);
        Assert.Equal(0, ironTeethStatue.FuelValue);
        Assert.False(ironTeethStatue.IsBurnable);
        Assert.False(ambiguousStatue.Known);
    }

    [Fact]
    public void ResourceAdaptersMapNaturalResourcesFromBurnableBlueprints()
    {
        TimberbornResourceAdapter resourceAdapter = new();
        TimberbornFireCellMapper mapper = new();
        FireGrid grid = new(3, 1, 1);

        ushort[] cells = mapper.CreateInitialCells(
            grid,
            [
                resourceAdapter.CreateTreeSource(0, 0, 0, "Pine"),
                resourceAdapter.CreateCropSource(1, 0, 0, "Corn"),
                resourceAdapter.CreateCropSource(2, 0, 0, "Carrot"),
            ]);

        Assert.Equal(PackedCell.Pack(fuel: 8, heat: 0, flammability: 3, water: 0, terrain: 1, burningLevel: 0), cells[0]);
        Assert.Equal(PackedCell.Pack(fuel: 2, heat: 0, flammability: 3, water: 0, terrain: 1, burningLevel: 0), cells[1]);
        Assert.Equal(PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 1, burningLevel: 0), cells[2]);
    }

    [Fact]
    public void BuildingAdapterMapsBuildingBlueprintValuesAndNonBurnableProfiles()
    {
        TimberbornBuildingAdapter buildingAdapter = new();
        TimberbornFireCellMapper mapper = new();
        FireGrid grid = new(2, 1, 1);

        ushort[] cells = mapper.CreateInitialCells(
            grid,
            [
                buildingAdapter.CreateBuildingSource(0, 0, 0, "LumberMill.Folktails"),
                buildingAdapter.CreateBuildingSource(1, 0, 0, "BeaverStatue.IronTeeth"),
            ]);

        Assert.Equal(PackedCell.Pack(fuel: 3, heat: 0, flammability: 1, water: 0, terrain: 1, burningLevel: 0), cells[0]);
        Assert.Equal(PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 1, burningLevel: 0), cells[1]);
    }

    [Fact]
    public void BurnDamageCapacityUsesBuildingBurnableBlueprintValuesForState()
    {
        TimberbornBurnableProfile lumberMill = TimberbornBurnableCatalog.Default.Lookup("LumberMill.Folktails");
        TimberbornBurnDamageDescriptor descriptor = new(
            "LumberMill.Folktails",
            TimberbornBurnDamageTargetKind.Structure,
            TimberbornBurnMaterialKind.Constructed,
            constructionResources: [new TimberbornBurnDamageResourceStack("Log", 99)],
            burnableProfile: lumberMill);
        TimberbornBurnDamageCapacity capacity = new TimberbornBurnDamageCapacityCalculator().Calculate(descriptor);

        Assert.Equal(2, capacity.Capacity);
        Assert.Equal(3, capacity.FuelValue);
        Assert.Equal(1, capacity.Flammability);
        Assert.Equal(["LumberMill.Folktails"], capacity.AccountedResourceIds);
    }

    [Fact]
    public void ResourceAdapterMapsKnownResourceIdsToStockpileSources()
    {
        TimberbornResourceAdapter resourceAdapter = new();
        TimberbornFireCellMapper mapper = new();
        FireGrid grid = new(1, 1, 1);

        ushort[] cells = mapper.CreateInitialCells(
            grid,
            [resourceAdapter.CreateStockpileResourceSource(0, 0, 0, "Log")]);

        Assert.Equal(PackedCell.Pack(fuel: 2, heat: 0, flammability: 1, water: 0, terrain: 1, burningLevel: 0), cells[0]);
    }

    [Fact]
    public void ResourceAdapterMapsUnknownResourceIdsToSafeLowFuelStockpileSources()
    {
        TimberbornResourceAdapter resourceAdapter = new();
        TimberbornFireCellMapper mapper = new();
        FireGrid grid = new(1, 1, 1);

        ushort[] cells = mapper.CreateInitialCells(
            grid,
            [resourceAdapter.CreateStockpileResourceSource(0, 0, 0, "MysteryResource")]);

        Assert.Equal(PackedCell.Pack(fuel: 1, heat: 0, flammability: 0, water: 0, terrain: 1, burningLevel: 0), cells[0]);
    }
}
