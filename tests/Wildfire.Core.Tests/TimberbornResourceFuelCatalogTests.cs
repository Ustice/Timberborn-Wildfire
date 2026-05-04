using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornResourceFuelCatalogTests
{
    [Fact]
    public void DefaultCatalogCoversInspectedShippedGoods()
    {
        TimberbornResourceFuelCatalog catalog = TimberbornResourceFuelCatalog.Default;

        Assert.Equal(60, catalog.KnownResourceIds.Count);
        Assert.Contains("Log", catalog.KnownResourceIds);
        Assert.Contains("Water", catalog.KnownResourceIds);
        Assert.Contains("Antidote", catalog.KnownResourceIds);
        Assert.Contains("Explosives", catalog.KnownResourceIds);
        Assert.Contains("Fireworks", catalog.KnownResourceIds);
    }

    [Fact]
    public void LookupDefaultsUnknownResourcesToSafeLowFuel()
    {
        TimberbornResourceFuelProfile profile = TimberbornResourceFuelCatalog.Default.Lookup("MysteryResource");

        Assert.Equal("MysteryResource", profile.ResourceId);
        Assert.Equal(1, profile.FuelValue);
        Assert.Equal(0, profile.Flammability);
        Assert.Equal(TimberbornResourceSmokeProfile.LightOrganic, profile.SmokeProfile);
        Assert.Equal(TimberbornResourceResidueQuality.Unresolved, profile.ResidueQuality);
        Assert.Equal(TimberbornResourceHazardClass.Unknown, profile.HazardClass);
    }

    [Fact]
    public void LookupDefaultsBlankResourcesToAnonymousUnknownProfile()
    {
        TimberbornResourceFuelProfile profile = TimberbornResourceFuelCatalog.Default.Lookup(" ");

        Assert.Equal("", profile.ResourceId);
        Assert.Equal(1, profile.FuelValue);
        Assert.Equal(0, profile.Flammability);
        Assert.Equal(TimberbornResourceHazardClass.Unknown, profile.HazardClass);
    }

    [Theory]
    [InlineData("Dirt", TimberbornResourceResidueQuality.Mineral)]
    [InlineData("MetalBlock", TimberbornResourceResidueQuality.Metal)]
    [InlineData("MetalPart", TimberbornResourceResidueQuality.Metal)]
    [InlineData("ScrapMetal", TimberbornResourceResidueQuality.Metal)]
    [InlineData("Water", TimberbornResourceResidueQuality.None)]
    public void LookupClassifiesStoneMetalAndWaterAsInert(string resourceId, TimberbornResourceResidueQuality residueQuality)
    {
        TimberbornResourceFuelProfile profile = TimberbornResourceFuelCatalog.Default.Lookup(resourceId);

        Assert.Equal(0, profile.FuelValue);
        Assert.Equal(0, profile.Flammability);
        Assert.Equal(TimberbornResourceSmokeProfile.None, profile.SmokeProfile);
        Assert.Equal(residueQuality, profile.ResidueQuality);
        Assert.Equal(TimberbornResourceHazardClass.Inert, profile.HazardClass);
    }

    [Theory]
    [InlineData("Log", 12, 2, TimberbornResourceSmokeProfile.DryWood)]
    [InlineData("Plank", 10, 2, TimberbornResourceSmokeProfile.DryWood)]
    [InlineData("Gear", 8, 2, TimberbornResourceSmokeProfile.DryWood)]
    [InlineData("Paper", 5, 3, TimberbornResourceSmokeProfile.Paper)]
    [InlineData("Book", 6, 3, TimberbornResourceSmokeProfile.Paper)]
    public void LookupClassifiesDryGoodsAsBurnableFuel(
        string resourceId,
        byte fuelValue,
        byte flammability,
        TimberbornResourceSmokeProfile smokeProfile)
    {
        TimberbornResourceFuelProfile profile = TimberbornResourceFuelCatalog.Default.Lookup(resourceId);

        Assert.Equal(fuelValue, profile.FuelValue);
        Assert.Equal(flammability, profile.Flammability);
        Assert.Equal(smokeProfile, profile.SmokeProfile);
        Assert.Equal(TimberbornResourceHazardClass.DryFuel, profile.HazardClass);
    }

    [Fact]
    public void LookupSeparatesFoodAndMedicineLikeResourcesFromDryFuel()
    {
        TimberbornResourceFuelProfile carrot = TimberbornResourceFuelCatalog.Default.Lookup("Carrot");
        TimberbornResourceFuelProfile canolaOil = TimberbornResourceFuelCatalog.Default.Lookup("CanolaOil");
        TimberbornResourceFuelProfile antidote = TimberbornResourceFuelCatalog.Default.Lookup("Antidote");

        Assert.Equal(TimberbornResourceHazardClass.FoodLike, carrot.HazardClass);
        Assert.Equal(TimberbornResourceResidueQuality.SpoiledOrganic, carrot.ResidueQuality);
        Assert.Equal(TimberbornResourceHazardClass.FoodLike, canolaOil.HazardClass);
        Assert.Equal(TimberbornResourceSmokeProfile.Oily, canolaOil.SmokeProfile);
        Assert.Equal(TimberbornResourceHazardClass.MedicineLike, antidote.HazardClass);
        Assert.Equal(TimberbornResourceSmokeProfile.Chemical, antidote.SmokeProfile);
    }

    [Fact]
    public void LookupClassifiesVolatileAndExplosiveResourcesWithoutHighFuelDefault()
    {
        TimberbornResourceFuelProfile biofuel = TimberbornResourceFuelCatalog.Default.Lookup("Biofuel");
        TimberbornResourceFuelProfile explosives = TimberbornResourceFuelCatalog.Default.Lookup("Explosives");
        TimberbornResourceFuelProfile fireworks = TimberbornResourceFuelCatalog.Default.Lookup("Fireworks");

        Assert.Equal(TimberbornResourceHazardClass.Volatile, biofuel.HazardClass);
        Assert.Equal(8, biofuel.FuelValue);
        Assert.Equal(TimberbornResourceHazardClass.Explosive, explosives.HazardClass);
        Assert.Equal(4, explosives.FuelValue);
        Assert.Equal(TimberbornResourceHazardClass.Explosive, fireworks.HazardClass);
        Assert.Equal(4, fireworks.FuelValue);
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

        Assert.Equal(PackedCell.Pack(fuel: 12, heat: 0, flammability: 2, water: 0, terrain: 1, heatLoss: 3), cells[0]);
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

        Assert.Equal(PackedCell.Pack(fuel: 1, heat: 0, flammability: 0, water: 0, terrain: 1, heatLoss: 3), cells[0]);
    }
}
