using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Tests;

public sealed class NativePhysicalInventoryCaptureTests
{
    [Fact]
    public void DistinctActualFactoryAndOutputStockRetainsSameGoodReservationsAndDisabledQuantity()
    {
        using var f = new Fixture();
        f.Native.Stock.Reserve(capacity: false); f.Native.Give(); f.Native.Stock.Disable();
        for (int i = 0; i < 3; ++i) f.Native.Call(f.Output, "GiveExistingIgnoringCapacity", f.Native.Stock.Amount);
        var captured = f.Capture(f.Discover());
        Assert.Equal(new[] { TimberbornNativeInventoryRole.SimpleOutput, TimberbornNativeInventoryRole.Manufactory }, captured.Select(v => v.Declaration.Role));
        Assert.Equal(new[] { "Native.Output", "Native.Factory" }, captured.Select(v => v.Declaration.ComponentName));
        Assert.Equal(new[] { 3, 2 }, captured.Select(v => v.Stock.Single().Amount));
        Assert.Equal(new[] { true, false }, captured.Select(v => v.Enabled));
        Assert.All(captured, v => Assert.Equal("Log", v.Stock.Single().ResourceId));
        Assert.Equal(2, f.Native.Physical); Assert.Equal(1, f.Native.Stock.Reserved(stock: true));
        Assert.Same(f.Native.Inventory, f.Native.Stock.ReservedInventory(capacity: false));
        Assert.Equal(3, f.Native.Call(f.Output, "AmountInStock", "Log")); Assert.Equal(0, f.Native.Consumption);
        // No Unity liveness assertion: actual native role initializers, topology validation and Stock getters.
    }

    [Theory]
    [InlineData("alias", "aliased")]
    [InlineData("name", "duplicate inventory component names")]
    [InlineData("foreign", "declared components")]
    public void InvalidCompleteClaimsRejectWithoutReadingBrokenStock(string change, string expected)
    {
        using var f = new Fixture();
        NativeInventoryRoleFixture.Set(f.Native.Inventory, "_storage", null);
        if (change == "alias") NativeInventoryRoleFixture.Set(f.OutputRole, "<Inventory>k__BackingField", f.Native.Inventory);
        if (change == "name") NativeInventoryRoleFixture.Set(f.Output, "<ComponentName>k__BackingField", "Native.Factory");
        var failure = Assert.Throws<TargetInvocationException>(() => f.Discover(change == "foreign"));
        Assert.Contains(expected, failure.GetBaseException().Message);
        // This executes production topology validation. Provider preflight-before-stock ordering is source-reviewed,
        // rather than faking a live Unity entity or adding a test-only stock-read callback to production.
    }

    [Fact]
    public void NativeCallbackChangesToOtherRoleInvalidateExactBindingAndCopiedPhysicalReadings()
    {
        using var f = new Fixture();
        f.Native.Give(); f.Native.Call(f.Output, "GiveExistingIgnoringCapacity", f.Native.Stock.Amount);
        var bindings = f.Discover(); var before = f.Capture(bindings);
        f.Native.On("InventoryChanged", () =>
        {
            NativeInventoryRoleFixture.Set(f.Output, "<ComponentName>k__BackingField", "Changed.Output");
            f.Native.Call(f.Output, "GiveExistingIgnoringCapacity", f.Native.Stock.Amount);
        });
        f.Native.Give(); // Actual native event; no stock is consumed by capture.
        var afterBindings = f.Discover(); var after = f.Capture(afterBindings);
        Assert.False(f.SameBindings(bindings, afterBindings));
        Assert.Equal("Native.Output", before[0].Declaration.ComponentName); Assert.Equal(1, before[0].Stock.Single().Amount);
        Assert.Equal("Changed.Output", after[0].Declaration.ComponentName); Assert.Equal(2, after[0].Stock.Single().Amount);
        Assert.Equal(0, f.Native.Consumption);
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly NativeInventoryRoleFixture Native = new(input: true);
        internal readonly object Output, FactoryRole, OutputRole;
        private readonly Type _roles, _claim, _enum, _provider;
        internal Fixture()
        {
            var mod = Native.Stock.Resources.GetType().Assembly;
            _roles = mod.GetType("Wildfire.Timberborn.Runtime.TimberbornNativeInventoryRoles")!;
            _claim = _roles.GetNestedType("Claim", BindingFlags.NonPublic)!;
            _enum = mod.GetType("Wildfire.Timberborn.Mapping.TimberbornNativeInventoryRole")!;
            _provider = mod.GetType("Wildfire.Timberborn.Runtime.TimberbornInitialWorldProjectionProvider")!;
            NativeInventoryRoleFixture.Set(Native.Inventory, "<ComponentName>k__BackingField", "Native.Factory");
            FactoryRole = Native.Role("Manufactory");
            Output = RuntimeHelpers.GetUninitializedObject(Native.Inventory.GetType());
            foreach (var field in new[] { "_storage", "_reservedStock", "_reservedCapacity" })
                NativeInventoryRoleFixture.Set(Output, field, Activator.CreateInstance(Native.T("Timberborn.Goods", "GoodRegistry")));
            foreach (var field in new[] { "_allowedGoods", "_goodDisallower" })
                NativeInventoryRoleFixture.Set(Output, field, NativeInventoryRoleFixture.GetField(Native.Inventory, field));
            NativeInventoryRoleFixture.Set(Output, "<ComponentName>k__BackingField", "Native.Output");
            NativeInventoryRoleFixture.Set(Output, "<Capacity>k__BackingField", 10);
            Output.GetType().BaseType!.GetField("<Enabled>k__BackingField", NativeInventoryRoleFixture.Flags)!.SetValue(Output, true);
            OutputRole = RuntimeHelpers.GetUninitializedObject(Native.T("Timberborn.SimpleOutputBuildings", "SimpleOutputInventory"));
            Native.Call(OutputRole, "InitializeInventory", Output);
        }
        internal object Discover(bool omitOutput = false)
        {
            var inventories = Array.CreateInstance(Native.Inventory.GetType(), omitOutput ? 1 : 2);
            inventories.SetValue(Native.Inventory, 0); if (!omitOutput) inventories.SetValue(Output, 1);
            var claims = Array.CreateInstance(_claim, 2);
            claims.SetValue(Activator.CreateInstance(_claim, Enum.Parse(_enum, "Manufactory"), Native.Property(FactoryRole, "Inventory")), 0);
            claims.SetValue(Activator.CreateInstance(_claim, Enum.Parse(_enum, "SimpleOutput"), Native.Property(OutputRole, "Inventory")), 1);
            return _roles.GetMethod("ValidateClaims", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [inventories, claims])!;
        }
        internal TimberbornInventoryMaterial[] Capture(object bindings) => ((IEnumerable)_provider.GetMethod("CaptureInventories",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [bindings])!).Cast<object>().Select(value =>
        {
            var declaration = Native.Property(value, "Declaration")!;
            return new TimberbornInventoryMaterial(new((TimberbornNativeInventoryRole)Convert.ToInt32(Native.Property(declaration, "Role")),
                (string)Native.Property(declaration, "ComponentName")!), (bool)Native.Property(value, "Enabled")!,
                ((IEnumerable)Native.Property(value, "Stock")!).Cast<object>().Select(good => new TimberbornStoredGoodStack(
                    (string)Native.Property(good, "ResourceId")!, (int)Native.Property(good, "Amount")!)));
        }).ToArray();
        internal bool SameBindings(object before, object after) => (bool)_provider.GetMethod("SameInventories", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [before, after])!;
        public void Dispose() => Native.Dispose();
    }
}
