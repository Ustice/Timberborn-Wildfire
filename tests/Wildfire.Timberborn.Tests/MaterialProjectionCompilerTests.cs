using Wildfire.Core;
using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Tests;

public sealed class MaterialProjectionCompilerTests
{
    [Fact]
    public void RemainingYieldAndAccountingAvailabilityDoNotReconstructMaterialDefinition()
    {
        var id = Guid.NewGuid();
        var footprint = new[] { new TimberbornMaterialFootprintSlot(new(0, 0, 0), 1) };
        TimberbornInitialMaterialBody Body(int quantity, bool enabled) => new(id, "BlueberryBush",
            TimberbornInitialBodyShape.Vegetation, footprint,
            [new(TimberbornCapturedYieldRole.Gatherable, "Gatherable", "Berries", quantity, "Berries", 3, false, enabled)], [], null);
        var before = TimberbornMaterialProjectionCompiler.Compile(Body(3, true));
        var after = TimberbornMaterialProjectionCompiler.Compile(Body(0, false));
        Assert.Equal(before.Parts, after.Parts);
        Assert.Equal(footprint, after.Footprint);
        Assert.Equal(WildfireMaterialClass.Vegetation, Assert.Single(after.Parts).MaterialClass);
        Assert.Equal(1, after.Parts[0].InitialFuel);
        // Whether this disabled owner may contribute now is a lifecycle admission decision, not a
        // reason for the pure definition compiler to mint depleted or refilled material history.
    }

    [Fact]
    public void PhysicalStockRemainsSeparateFromConstructionAndNoSelectionCanHideIt()
    {
        var body = new TimberbornInitialMaterialBody(Guid.NewGuid(), "SmallWarehouse.Folktails",
            TimberbornInitialBodyShape.Stockpile, [new(new(0, 0, 0), 0)], [],
            [new(new(TimberbornNativeInventoryRole.Stockpile, "Stockpile"), false, [new("Log", 2)])], [new("Log", 3)]);
        var projection = TimberbornMaterialProjectionCompiler.Compile(body);
        Assert.Contains(TimberbornMaterialPart.Building(body.SpecId), projection.Parts);
        Assert.Contains(TimberbornMaterialPart.StoredGood("Log"), projection.Parts);
        Assert.Equal(3, Assert.Single(body.ConstructionResources!).Amount);
        Assert.Equal(2, Assert.Single(body.Inventories[0].Stock).Amount);
    }
}
