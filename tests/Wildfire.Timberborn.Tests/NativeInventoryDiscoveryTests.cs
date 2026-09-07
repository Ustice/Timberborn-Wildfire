using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeInventoryDiscoveryTests
{
    [Fact]
    public void DormantActualGoodStackAndEmptyManufactoryRemainDeclaredWithoutStockReads()
    {
        using var f = new Fixture();
        f.Native.InitializeNamedInventory("GoodStack");
        var stack = RuntimeHelpers.GetUninitializedObject(f.Native.T("Timberborn.GoodStackSystem", "GoodStack"));
        f.Native.Call(stack, "InitializeInventory", f.Inventory);
        var second = f.InventoryNamed("Manufactory"); // No stock registries: reading quantity would fail.
        var result = f.Validate([f.Inventory, second], [("Manufactory", second), ("GoodStack", f.Native.Property(stack, "Inventory"))]);
        Assert.Equal(new[] { "GoodStack", "Manufactory" }, f.Names(result));
        Assert.Equal(false, f.Native.Property(f.Inventory, "Enabled"));
        Assert.Equal(0, f.Native.Physical);
        Assert.Throws<NotSupportedException>(() => ((IList)result).Clear());
        NativeInventoryRoleFixture.Set(second, "<ComponentName>k__BackingField", "changed later");
        Assert.Equal(new[] { "GoodStack", "Manufactory" }, f.Names(result));
    }

    [Fact]
    public void ActualRoleInitializerAliasIsRejectedBeforeReturningAnyBindings()
    {
        using var f = new Fixture();
        var manufactory = f.Native.Role("Manufactory"); var output = f.Native.Role("SimpleOutput");
        var error = Assert.Throws<TargetInvocationException>(() => f.Validate([f.Inventory],
            [("Manufactory", f.Native.Property(manufactory, "Inventory")), ("SimpleOutput", f.Native.Property(output, "Inventory"))]));
        Assert.Contains("aliased", error.GetBaseException().Message);
        Assert.Equal(0, f.Native.Physical);
    }

    [Theory]
    [InlineData("duplicate-name")]
    [InlineData("duplicate-role")]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("unclaimed")]
    [InlineData("repeated-component")]
    [InlineData("blank-name")]
    public void AmbiguousOrIncompleteNativeTopologyRejects(string scenario)
    {
        using var f = new Fixture();
        var other = f.InventoryNamed(scenario == "duplicate-name" ? "first" : "second");
        object[] inventories = [f.Inventory, other];
        (string Role, object? Inventory)[] claims = [("GoodStack", f.Inventory), ("Manufactory", other)];
        switch (scenario)
        {
            case "duplicate-role": claims[1] = ("GoodStack", other); break;
            case "missing": claims[1] = ("Manufactory", null); break;
            case "foreign": claims[1] = ("Manufactory", f.InventoryNamed("foreign")); break;
            case "unclaimed": claims = [("GoodStack", f.Inventory)]; break;
            case "repeated-component": inventories = [f.Inventory, f.Inventory]; claims = [("GoodStack", f.Inventory)]; break;
            case "blank-name": NativeInventoryRoleFixture.Set(other, "<ComponentName>k__BackingField", " "); break;
        }
        Assert.Throws<TargetInvocationException>(() => f.Validate(inventories, claims));
        Assert.Equal(0, f.Native.Physical);
    }

    [Fact]
    public void IdentityIsExactNativeRoleAndNameAndRejectsUnknownRole()
    {
        Assert.Equal(new TimberbornInventoryDeclaration(TimberbornNativeInventoryRole.Manufactory, "Manufactory"),
            new TimberbornInventoryDeclaration(TimberbornNativeInventoryRole.Manufactory, "Manufactory"));
        Assert.NotEqual(new TimberbornInventoryDeclaration(TimberbornNativeInventoryRole.GoodStack, "GoodStack"),
            new TimberbornInventoryDeclaration(TimberbornNativeInventoryRole.RecoveredGoodStack, "GoodStack"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimberbornInventoryDeclaration((TimberbornNativeInventoryRole)999, "good"));
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly NativeInventoryRoleFixture Native = new(input: true);
        internal object Inventory => Native.Inventory;
        private readonly Type _helper, _claim, _role;
        internal Fixture()
        {
            var mod = Native.Stock.Resources.GetType().Assembly;
            _helper = mod.GetType("Wildfire.Timberborn.Runtime.TimberbornNativeInventoryRoles")!;
            _claim = _helper.GetNestedType("Claim", BindingFlags.NonPublic)!;
            _role = mod.GetType("Wildfire.Timberborn.Mapping.TimberbornNativeInventoryRole")!;
            NativeInventoryRoleFixture.Set(Inventory, "<ComponentName>k__BackingField", "first");
        }
        internal object InventoryNamed(string name)
        {
            var value = RuntimeHelpers.GetUninitializedObject(Inventory.GetType());
            NativeInventoryRoleFixture.Set(value, "<ComponentName>k__BackingField", name); return value;
        }
        internal object Validate(object[] inventories, (string Role, object? Inventory)[] claims)
        {
            var components = Array.CreateInstance(Inventory.GetType(), inventories.Length);
            for (int i = 0; i < inventories.Length; ++i) components.SetValue(inventories[i], i);
            var roles = Array.CreateInstance(_claim, claims.Length);
            for (int i = 0; i < claims.Length; ++i)
                roles.SetValue(Activator.CreateInstance(_claim, Enum.Parse(_role, claims[i].Role), claims[i].Inventory), i);
            return _helper.GetMethod("ValidateClaims", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [components, roles])!;
        }
        internal string[] Names(object result) => ((IEnumerable)result).Cast<object>()
            .Select(binding => (string)Native.Property(Native.Property(binding, "Declaration")!, "ComponentName")!).ToArray();
        public void Dispose() => Native.Dispose();
    }
}
