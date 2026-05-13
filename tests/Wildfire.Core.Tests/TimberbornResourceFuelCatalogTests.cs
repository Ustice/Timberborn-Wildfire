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
