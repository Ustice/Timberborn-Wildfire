using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Wildfire.Core;
using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Tests;

public sealed class InitialNativeMaterialCaptureTests
{
    private static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly TimberbornMaterialFootprintSlot[] Cell = [new(new(0, 0, 0), 0)];

    [Fact]
    public void EmptyWarehouseRetainsPhysicalBodyAndWhitelistNeverBecomesAnInventoryGood()
    {
        using var blueprint = NativeBlueprint("Buildings/Storage/SmallWarehouse/SmallWarehouse.Folktails.blueprint.json");
        Assert.Equal("Box", blueprint.RootElement.GetProperty("StockpileSpec").GetProperty("WhitelistedGoodType").GetString());
        var empty = new TimberbornInventoryMaterial(new(TimberbornNativeInventoryRole.Stockpile, "Stockpile"), true, []);
        var body = Body("SmallWarehouse.Folktails", TimberbornInitialBodyShape.Stockpile, [], [empty]);
        Assert.Equal(NativeBurnTargetFamily.Stockpile, body.Family);
        Assert.Equal(TimberbornBurnDamageTargetKind.Structure, body.PhysicalBodyKind);
        Assert.Equal(2, body.BodyProfile.FuelValue);
        Assert.Equal(1, body.BodyProfile.DamageCapacity);
        Assert.Empty(body.Inventories.Single().Goods);
        Assert.Contains(TimberbornInitialCompositionGap.PhysicalReconstructionUnavailable, body.CompositionGaps);
    }

    [Fact]
    public void PhysicalInventoryCaptureKeepsActualQuantityAndDisabledStockWithoutMultiplyingFuel()
    {
        var source = new[] { new TimberbornStoredGoodStack("Log", 12), new TimberbornStoredGoodStack("Plank", 0) };
        var inventory = new TimberbornInventoryMaterial(new(TimberbornNativeInventoryRole.SimpleOutput, "SimpleOutput"), false, source);
        source[0] = new("Log", 0);
        var body = Body("LumberMill.Folktails", TimberbornInitialBodyShape.Structure, [], [inventory]);
        Assert.Single(body.Inventories);
        Assert.Equal(12, inventory.Stock.Single().Amount);
        Assert.Equal(2, inventory.Goods.Single().FuelValue); // Catalog fact, not quantity-scaled fuel.
        Assert.Equal(NativeBurnTargetFamily.Structure, body.Family);
        Assert.Contains(TimberbornInitialCompositionGap.InventoryUnavailable, body.CompositionGaps);
    }

    [Fact]
    public void ActualPineBlueprintPreservesWoodAndResinAsSeparateNamedInputsUnderOneBody()
    {
        using var blueprint = NativeBlueprint("NaturalResources/Trees/Pine/Pine.blueprint.json");
        var yields = BlueprintYields(blueprint.RootElement);
        var body = Body("Pine", TimberbornInitialBodyShape.Tree, yields, []);
        Assert.Equal(2, body.Yields.Count);
        Assert.Equal("Log", body.Yields.Single(yield => yield.Role == TimberbornCapturedYieldRole.Cuttable).DeclaredGoodId);
        var resin = body.Yields.Single(yield => yield.Role == TimberbornCapturedYieldRole.Gatherable);
        Assert.Equal("PineResin", resin.DeclaredGoodId);
        Assert.Equal(2, resin.ActualAmount);
        Assert.Equal(1, resin.GoodProfile.FuelValue);
        Assert.Contains(TimberbornInitialCompositionGap.MultipleNamedYields, body.CompositionGaps);
        Assert.Contains(TimberbornInitialCompositionGap.NaturalBudgetUnresolved, body.CompositionGaps);
        Assert.Empty(body.Inventories); // A native yielder is not a stockpile inventory.
    }

    [Fact]
    public void ActualCarrotAndBlueberryRolesKeepTheirDifferentExistingMaterialPoliciesVisible()
    {
        using var carrot = NativeBlueprint("NaturalResources/Crops/Carrot/Carrot.blueprint.json");
        using var blueberry = NativeBlueprint("NaturalResources/Bushes/Blueberry/BlueberryBush.blueprint.json");
        var annual = Body("Carrot", TimberbornInitialBodyShape.Crop, BlueprintYields(carrot.RootElement), []);
        var bush = Body("BlueberryBush", TimberbornInitialBodyShape.Vegetation, BlueprintYields(blueberry.RootElement), []);
        Assert.Equal(TimberbornCapturedYieldRole.Cuttable, annual.Yields.Single().Role);
        Assert.True(annual.Yields.Single().RemoveOnCut);
        Assert.False(annual.BodyProfile.IsBurnable); // Do not activate Carrot through the organic fallback.
        Assert.Equal(TimberbornCapturedYieldRole.Gatherable, bush.Yields.Single().Role);
        Assert.Equal("Berries", bush.Yields.Single().DeclaredGoodId);
        Assert.Equal(1, bush.BodyProfile.FuelValue);
        Assert.Equal(10, TimberbornResourceAdapter.VegetationFuel);
        Assert.DoesNotContain(TimberbornInitialCompositionGap.VegetationProfileMismatch, bush.CompositionGaps);
    }

    [Fact]
    public void NativeYielderGetterCapturePreservesActualPartialAndZeroAmountsWithoutSpecRefill()
    {
        using var native = new NativePartialYieldFixture();
        var helper = native.ModType("Wildfire.Timberborn.Runtime.TimberbornInitialWorldProjectionProvider")
            .GetMethod("CaptureNamedYield", BindingFlags.Static | BindingFlags.NonPublic)!;
        var role = Enum.Parse(native.ModType("Wildfire.Timberborn.Mapping.TimberbornCapturedYieldRole"), "Gatherable");
        foreach (int amount in new[] { 3, 0 })
        {
            NativePartialYieldFixture.Set(native.Yielder, "_yield", Activator.CreateInstance(native.AmountType, "Carrot", amount)!);
            var captured = helper.Invoke(null, [native.Yielder, role, false])!;
            Assert.Equal(amount, captured.GetType().GetProperty("ActualAmount")!.GetValue(captured));
            Assert.Equal(5, captured.GetType().GetProperty("DeclaredAmount")!.GetValue(captured));
            Assert.Equal(amount, native.RawQuantity(native.Yielder));
        }
        NativePartialYieldFixture.Set(native.Yielder, "_yield", Activator.CreateInstance(native.AmountType, "Carrot", 3)!);
        native.EnabledField.SetValue(native.Yielder, false);
        var disabled = helper.Invoke(null, [native.Yielder, role, false])!;
        Assert.Equal(false, disabled.GetType().GetProperty("YieldEnabled")!.GetValue(disabled));
        Assert.Equal(0, disabled.GetType().GetProperty("ActualAmount")!.GetValue(disabled));
        Assert.Equal(3, native.RawQuantity(native.Yielder)); // Getter zero is availability, not proof of empty material.
    }

    [Fact]
    public void CaptureRetainsAllOverlappingBodiesAndActualSparseNativeLocalSlots()
    {
        using var native = new NativeMaterialFootprintTests.NativeFootprintFixture();
        var grid = new FireGrid(20, 20, 5);
        var footprint = native.Project("Cw90", true, grid);
        var tree = new TimberbornInitialMaterialBody(Id, "Pine", TimberbornInitialBodyShape.Tree, footprint, [], [], null);
        var structure = new TimberbornInitialMaterialBody(Guid.NewGuid(), "LumberMill.Folktails", TimberbornInitialBodyShape.Structure, footprint.Reverse(), [], [], []);
        var capture = new TimberbornInitialWorldCapture(grid, [structure, tree], [], [], EmptyEnvironment(grid));
        Assert.Equal(2, capture.Bodies.Count);
        Assert.All(capture.Bodies, body => Assert.Equal(4, body.Footprint.Count));
        Assert.Equal(tree.Footprint, structure.Footprint);
        Assert.Throws<ArgumentException>(() => new TimberbornInitialWorldCapture(grid, [tree, tree], [], [], EmptyEnvironment(grid)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimberbornInitialWorldCapture(new FireGrid(1, 1, 1), [tree], [], [], EmptyEnvironment(new(1, 1, 1))));
    }

    [Fact]
    public void UnsupportedRolesAndUnknownProfilesRemainExplicitInsteadOfCreatingUsableFreshMaterial()
    {
        var inventory = new TimberbornInventoryMaterial(new(TimberbornNativeInventoryRole.GoodStack, "GoodStack"), true, [new("ModGood", 1)]);
        var stack = Body("GoodStack.ModGood", TimberbornInitialBodyShape.GoodStack, [], [inventory]);
        Assert.Null(stack.Family);
        Assert.Contains(TimberbornInitialCompositionGap.UnsupportedBodyFamily, stack.CompositionGaps);
        Assert.Contains(TimberbornInitialCompositionGap.UnknownGoodProfile, stack.CompositionGaps);
        var infrastructure = Body("Path.Folktails", TimberbornInitialBodyShape.Infrastructure, [], []);
        Assert.Equal(NativeBurnTargetFamily.PathInfrastructure, infrastructure.Family);
        Assert.Contains(TimberbornInitialCompositionGap.UnsupportedBodyFamily, infrastructure.CompositionGaps);
        var supported = Body("SmallWarehouse.Folktails", TimberbornInitialBodyShape.Stockpile, [], [
            new(new(TimberbornNativeInventoryRole.Stockpile, "Stockpile"), true, []), new(new(TimberbornNativeInventoryRole.SimpleOutput, "SimpleOutput"), true, [])]);
        Assert.DoesNotContain(TimberbornInitialCompositionGap.MultipleInventoryRoles, supported.CompositionGaps);
        Assert.Throws<ArgumentException>(() => new TimberbornNamedYieldMaterial(TimberbornCapturedYieldRole.Gatherable, "Gatherable", "Log", 1, "Berries", 3, false, true));
    }

    [Fact]
    public void WaterSourceFootprintRemainsSeparateFromOwnedBodiesAndCannotAliasTheirGuid()
    {
        var source = new TimberbornInitialWaterSource(Id, "BadwaterSource", true, Cell);
        var capture = new TimberbornInitialWorldCapture(new FireGrid(1, 1, 1), [], [], [source], EmptyEnvironment(new(1, 1, 1)));
        Assert.Empty(capture.Bodies);
        Assert.True(capture.WaterSources.Single().Badwater);
        Assert.Equal(0, capture.WaterSources.Single().Footprint.Single().CellIndex);
        Assert.Throws<ArgumentException>(() => new TimberbornInitialWorldCapture(new FireGrid(1, 1, 1),
            [Body("Pine", TimberbornInitialBodyShape.Tree, [], [])], [], [source], EmptyEnvironment(new(1, 1, 1))));
    }

    private static TimberbornInitialMaterialBody Body(string spec, TimberbornInitialBodyShape shape,
        IEnumerable<TimberbornNamedYieldMaterial> yields, IEnumerable<TimberbornInventoryMaterial> inventories) => new(Id, spec, shape, Cell, yields, inventories,
            shape is TimberbornInitialBodyShape.Structure or TimberbornInitialBodyShape.Stockpile ? [] : null);

    private static TimberbornInitialEnvironmentCapture EmptyEnvironment(FireGrid grid) => new(grid, [], [], []);

    private static JsonDocument NativeBlueprint(string entry)
    {
        using var native = new NativeManagedTestContext();
        using var archive = ZipFile.OpenRead(Path.Combine(native.ManagedPath, "../StreamingAssets/Modding/Blueprints.zip"));
        using var stream = archive.GetEntry(entry)!.Open();
        return JsonDocument.Parse(stream);
    }

    // Installed blueprint facts seed capture-model fixtures; this is not a live entity/provider execution claim.
    private static TimberbornNamedYieldMaterial[] BlueprintYields(JsonElement root) =>
        new[] { ("CuttableSpec", TimberbornCapturedYieldRole.Cuttable), ("GatherableSpec", TimberbornCapturedYieldRole.Gatherable) }
            .Where(pair => root.TryGetProperty(pair.Item1, out _)).Select(pair =>
            {
                var component = root.GetProperty(pair.Item1); var yielder = component.GetProperty("Yielder"); var amount = yielder.GetProperty("Yield");
                return new TimberbornNamedYieldMaterial(pair.Item2, yielder.GetProperty("YielderComponentName").GetString()!,
                    amount.GetProperty("Id").GetString()!, amount.GetProperty("Amount").GetInt32(), amount.GetProperty("Id").GetString()!,
                    amount.GetProperty("Amount").GetInt32(), component.TryGetProperty("RemoveOnCut", out var remove) && remove.GetBoolean(), true);
            }).ToArray();
}
