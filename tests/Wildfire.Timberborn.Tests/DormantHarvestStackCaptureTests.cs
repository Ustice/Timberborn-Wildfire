using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class DormantHarvestStackCaptureTests
{
    [Theory]
    [InlineData("Pine", "Tree")]
    [InlineData("Carrot", "Crop")]
    [InlineData("BlueberryBush", "Vegetation")]
    [InlineData("GoodStack.ModGood", "GoodStack")]
    public void ActualNativeComponentLookupDoesNotReplacePlantIdentityWithHarvestCapability(string name, string expected)
    {
        using var native = new NativeManagedTestContext();
        var components = native.LoadNative("Timberborn.BaseComponentSystem");
        var entity = RuntimeHelpers.GetUninitializedObject(native.LoadNative("Timberborn.EntitySystem").GetType("Timberborn.EntitySystem.EntityComponent")!);
        var stack = RuntimeHelpers.GetUninitializedObject(native.LoadNative("Timberborn.GoodStackSystem").GetType("Timberborn.GoodStackSystem.GoodStack")!);
        var cache = RuntimeHelpers.GetUninitializedObject(components.GetType("Timberborn.BaseComponentSystem.ComponentCache")!);
        Set(cache, "_components", new List<object> { entity, stack });
        var typeIndices = Activator.CreateInstance(components.GetType("Timberborn.BaseComponentSystem.TypeIndexMap")!)!;
        var lookup = (System.Collections.IDictionary)typeIndices.GetType().GetField("_typeIndex", Flags)!.GetValue(typeIndices)!;
        foreach (var (assembly, type) in new[] { ("Timberborn.Stockpiles", "Timberborn.Stockpiles.Stockpile"),
            ("Timberborn.Buildings", "Timberborn.Buildings.Building"), ("Timberborn.Forestry", "Timberborn.Forestry.TreeComponent") })
            lookup.Add(native.LoadNative(assembly).GetType(type)!, null);
        lookup.Add(stack.GetType(), 1);
        Set(cache, "_typeIndexMap", typeIndices);
        components.GetType("Timberborn.BaseComponentSystem.BaseComponent")!.GetField("_componentCache", Flags)!.SetValue(entity, cache);
        var helper = native.LoadMod().GetType("Wildfire.Timberborn.Runtime.TimberbornInitialWorldProjectionProvider")!;
        Assert.Equal(expected, helper.GetMethod("Shape", Flags | BindingFlags.Static)!.Invoke(null, [entity, name])!.ToString());
        // Native cached type lookup is real; no Unity GameObject/liveness is claimed.
    }

    [Theory]
    [InlineData("GoodStack", false, false, false)]
    [InlineData("GoodStack", false, true, true)]
    [InlineData("GoodStack", true, false, true)]
    [InlineData("Stockpile", false, false, true)]
    [InlineData("SimpleOutput", false, false, true)]
    public void ActualInventoryStockRetainsDisabledPhysicalGoodsButOmitsEmptyDormantHarvestRole(
        string roleName, bool enabled, bool hasStock, bool included)
    {
        using var fixture = new NativeReservationFixture("Log");
        if (hasStock) fixture.Call(fixture.Inventory, "GiveExisting", fixture.Amount);
        if (!enabled) fixture.Disable();
        var mod = fixture.Resources.GetType().Assembly;
        var role = Enum.Parse(mod.GetType("Wildfire.Timberborn.Mapping.TimberbornCapturedInventoryRole")!, roleName);
        var helper = mod.GetType("Wildfire.Timberborn.Runtime.TimberbornInitialWorldProjectionProvider")!;
        var capture = helper.GetMethod("CaptureInventoryMaterial", Flags | BindingFlags.Static)!.Invoke(null, [role, fixture.Inventory]);
        Assert.Equal(included, capture is not null);
        if (capture is not null)
        {
            Assert.Equal(enabled, capture.GetType().GetProperty("Enabled")!.GetValue(capture));
            var stock = ((System.Collections.IEnumerable)capture.GetType().GetProperty("Stock")!.GetValue(capture)!).Cast<object>().ToArray();
            Assert.Equal(hasStock ? 1 : 0, stock.Length);
        }
        Assert.Equal(hasStock ? 1 : 0, fixture.Call(fixture.Inventory, "AmountInStock", "Log"));
    }
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags)!.SetValue(target, value);
}
