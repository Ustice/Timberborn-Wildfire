using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Tests;

public sealed class InitialConstructionCaptureTests
{
    [Theory]
    [InlineData("Buildings/Storage/SmallWarehouse/SmallWarehouse.Folktails.blueprint.json", "Log", 3)]
    [InlineData("Buildings/Wood/LumberMill/LumberMill.Folktails.blueprint.json", "Log", 15)]
    public void ActualBuildingSpecGetterCapturesInstalledBlueprintCosts(string entry, string expectedGood, int expectedAmount)
    {
        using var native = new NativeManagedTestContext();
        using var archive = ZipFile.OpenRead(Path.Combine(native.ManagedPath, "../StreamingAssets/Modding/Blueprints.zip"));
        using var stream = archive.GetEntry(entry)!.Open();
        using var document = JsonDocument.Parse(stream);
        var costs = document.RootElement.GetProperty("BuildingSpec").GetProperty("BuildingCost").EnumerateArray()
            .Select(value => new TimberbornBurnDamageResourceStack(value.GetProperty("Id").GetString()!, value.GetProperty("Amount").GetInt32())).ToArray();
        Assert.Contains(costs, cost => cost.ResourceId == expectedGood && cost.Amount == expectedAmount);
        var (spec, helper) = Fixture(native, costs);
        var captured = Read(helper, spec);
        Assert.Equal(costs.OrderBy(cost => cost.ResourceId, StringComparer.Ordinal), captured);
        // Mutating the native spec afterwards does not change the copied initial facts.
        var freeSpec = Fixture(native, []).Spec;
        var empty = freeSpec.GetType().GetProperty("BuildingCost")!.GetValue(freeSpec);
        spec.GetType().GetProperty("BuildingCost")!.SetValue(spec, empty);
        Assert.NotEmpty(captured);
        Assert.Empty(Read(helper, spec));
    }

    [Fact]
    public void ExplicitFreeBuildingIsDifferentFromMissingConstructionDefinition()
    {
        using var native = new NativeManagedTestContext();
        var (spec, helper) = Fixture(native, []);
        Assert.Empty(Read(helper, spec));
        Assert.Throws<TargetInvocationException>(() => helper.Invoke(null, [null]));
        spec.GetType().GetProperty("BuildingCost")!.SetValue(spec, Activator.CreateInstance(spec.GetType().GetProperty("BuildingCost")!.PropertyType));
        Assert.Throws<TargetInvocationException>(() => Read(helper, spec));
        var cell = new TimberbornMaterialFootprintSlot(new(0, 0, 0), 0);
        Assert.Throws<ArgumentException>(() => new TimberbornInitialMaterialBody(Guid.NewGuid(), "SmallWarehouse.Folktails",
            TimberbornInitialBodyShape.Stockpile, [cell], [], [], null));
        var free = new TimberbornInitialMaterialBody(Guid.NewGuid(), "Path.Folktails", TimberbornInitialBodyShape.Infrastructure, [cell], [], [], []);
        Assert.NotNull(free.ConstructionResources);
        Assert.Empty(free.ConstructionResources);
    }

    private static (object Spec, MethodInfo Helper) Fixture(NativeManagedTestContext native, TimberbornBurnDamageResourceStack[] costs)
    {
        var specType = native.LoadNative("Timberborn.Buildings").GetType("Timberborn.Buildings.BuildingSpec")!;
        var amountType = native.LoadNative("Timberborn.Goods").GetType("Timberborn.Goods.GoodAmountSpec")!;
        var values = Array.CreateInstance(amountType, costs.Length);
        for (int i = 0; i < costs.Length; i++)
        {
            var amount = Activator.CreateInstance(amountType)!;
            amountType.GetProperty("Id")!.SetValue(amount, costs[i].ResourceId); amountType.GetProperty("Amount")!.SetValue(amount, costs[i].Amount);
            values.SetValue(amount, i);
        }
        var immutable = typeof(ImmutableArray).GetMethods().Single(method => method.Name == "CreateRange" && method.IsGenericMethodDefinition &&
            method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType.IsGenericType &&
            method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>)).MakeGenericMethod(amountType).Invoke(null, [values]);
        var spec = Activator.CreateInstance(specType)!; specType.GetProperty("BuildingCost")!.SetValue(spec, immutable);
        var helper = native.LoadMod().GetType("Wildfire.Timberborn.Runtime.TimberbornInitialWorldProjectionProvider")!
            .GetMethod("CaptureBuildingCost", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (spec, helper);
    }
    private static TimberbornBurnDamageResourceStack[] Read(MethodInfo helper, object spec) =>
        ((System.Collections.IEnumerable)helper.Invoke(null, [spec])!).Cast<object>().Select(value => new TimberbornBurnDamageResourceStack(
            (string)value.GetType().GetProperty("ResourceId")!.GetValue(value)!, (int)value.GetType().GetProperty("Amount")!.GetValue(value)!)).ToArray();
}
