using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeInventoryRoleTests
{
    [Fact]
    public void ManufactoryDedicatedInputStockUsesActualUnreservedQuantityAndConsumedCounter()
    {
        using var f = new NativeInventoryRoleFixture(input: true);
        f.InitializeNamedInventory(f.NativeInventoryName("Manufactory"));
        var role = f.Role("Manufactory");
        f.Call(f.Inventory, "Enable"); // Native finished-state lifecycle enables this named inventory.
        Assert.Same(f.Inventory, f.Property(role, "Inventory"));
        Assert.Equal(f.NativeInventoryName("Manufactory"), f.Property(f.Inventory, "ComponentName"));
        f.Stock.Reserve(capacity: false); f.Give();
        Assert.Equal(2, f.Physical); Assert.Equal(1, f.Unreserved);
        Assert.Equal(0, f.Listed("UnreservedTakeableStock"));
        Assert.Equal(1, f.Listed("UnreservedStock"));
        int removed = -1;
        f.Stock.Transfer(() => removed = f.ConsumeOne());
        Assert.Equal(1, removed); Assert.Equal(1, f.Consumption); Assert.Equal(0, f.Production);
        Assert.Equal(1, f.Physical); Assert.Equal(1, f.Stock.Reserved(stock: true));
        Assert.Same(f.Inventory, f.Stock.ReservedInventory(capacity: false));
        f.Stock.Transfer(() => removed = f.ConsumeOne());
        Assert.Equal(0, removed); Assert.Equal(1, f.Consumption);
    }

    [Theory]
    [InlineData("InventoryChanged", 0)]
    [InlineData("InventoryStockChanged", 1)]
    public void CallbackCutpointControlsNativeCounterButNeverFabricatesCompletedRemoval(string stage, int counted)
    {
        using var f = new NativeInventoryRoleFixture(input: true);
        _ = f.Role("Manufactory"); f.Stock.Reserve(capacity: false); f.Give();
        var cause = new InvalidOperationException("native subscriber interrupted consumption");
        f.On(stage, () => throw cause);
        int receipt = -1;
        var error = Assert.Throws<TargetInvocationException>(() => f.Stock.Transfer(() => receipt = f.ConsumeOne()));
        Assert.Same(cause, error.GetBaseException()); Assert.Equal(-1, receipt);
        Assert.Equal(1, f.Physical); Assert.Equal(1, f.Stock.Reserved(stock: true));
        Assert.Equal(counted, f.Consumption); Assert.Equal(true, f.Stock.Get(f.Stock.Resources, "IsIndeterminate"));
        Assert.Throws<TargetInvocationException>(() => f.Call(f.Stock.Resources, "ThrowIfSaveUnsafe"));
        Assert.Throws<TargetInvocationException>(() => f.Stock.Transfer(() => receipt = f.ConsumeOne()));
    }

    [Fact]
    public void NativeCounterUnsubscriptionDuringInventoryChangedDoesNotUndoCompletedConsumption()
    {
        using var f = new NativeInventoryRoleFixture(input: true);
        _ = f.Role("Manufactory"); f.Give();
        f.On("InventoryChanged", () => f.Call(f.Balance, "OnInventoryUnregistered", null, f.Inventory));
        int receipt = -1; f.Stock.Transfer(() => receipt = f.ConsumeOne());
        Assert.Equal(1, receipt); Assert.Equal(0, f.Physical); Assert.Equal(0, f.Consumption);
        Assert.Equal(false, f.Stock.Get(f.Stock.Resources, "IsIndeterminate"));
        f.Call(f.Stock.Resources, "ThrowIfSaveUnsafe");
        // Actual native counter unsubscription, supplied callback: not a complete recovered-pile deletion proof.
    }

    [Fact]
    public void ActualRoleInitializersDoNotPreventTwoRolesAliasingOneInventory()
    {
        using var f = new NativeInventoryRoleFixture(input: true);
        var manufactory = f.Role("Manufactory"); var output = f.Role("SimpleOutput"); f.Give();
        Assert.Same(f.Property(manufactory, "Inventory"), f.Property(output, "Inventory"));
        var references = new[] { f.Property(manufactory, "Inventory")!, f.Property(output, "Inventory")! };
        Assert.Single(references.Distinct(ReferenceEqualityComparer.Instance));
        Assert.Equal(1, f.Physical); // Two declared roles do not represent two stock units.
    }

    [Fact]
    public void RecoveredStackIsDistinctRoleAndNonterminalRemovalReachesNativeConsumedCounter()
    {
        using var f = new NativeInventoryRoleFixture(input: false);
        f.InitializeNamedInventory(f.NativeInventoryName("Recovered"));
        var recovered = f.Role("Recovered");
        Assert.True(f.T("Timberborn.GoodStackSystem", "IGoodStackInventory").IsInstanceOfType(recovered));
        Assert.False(f.T("Timberborn.GoodStackSystem", "GoodStack").IsInstanceOfType(recovered));
        Assert.Same(f.Inventory, f.Property(recovered, "Inventory"));
        Assert.Equal(true, f.Property(f.Inventory, "Enabled"));
        f.Stock.Reserve(capacity: false); f.Give();
        int receipt = -1; f.Stock.Transfer(() => receipt = f.ConsumeOne());
        Assert.Equal(1, receipt); Assert.Equal(1, f.Consumption); Assert.Equal(0, f.Production);
        Assert.Equal(1, f.Physical); Assert.Equal(1, f.Stock.Reserved(stock: true));
    }

    [Fact]
    public void EmptyRecoveredStackStartsExactOwnerDeletionBeforeConsumedCallbackAndFailurePoisons()
    {
        using var f = new NativeInventoryRoleFixture(input: false);
        var recovered = f.Role("Recovered"); f.Give();
        var deletion = new RecoveredDeletionCutpoint(f, recovered);
        int receipt = -1;
        var error = Assert.Throws<TargetInvocationException>(() => f.Stock.Transfer(() => receipt = f.ConsumeOne()));
        Assert.Same(deletion.Failure, error.GetBaseException());
        Assert.Equal(-1, receipt); Assert.Equal(0, f.Physical); Assert.Equal(0, f.Consumption);
        Assert.Equal("Deleted", deletion.State); Assert.True(deletion.OwnerStillRegistered);
        Assert.True(deletion.ForeignStillRegistered); Assert.Equal(true, f.Stock.Get(f.Stock.Resources, "IsIndeterminate"));
        Assert.Throws<TargetInvocationException>(() => f.Call(f.Stock.Resources, "ThrowIfSaveUnsafe"));
        Assert.Throws<TargetInvocationException>(() => f.Stock.Transfer(() => receipt = f.ConsumeOne()));
        // Native deletion callback deliberately stops before registry removal and Unity Destroy.
    }

    [Fact]
    public void DormantHarvestStackStillHasNamedRoleWhenPhysicalCaptureOmitsIt()
    {
        using var f = new NativeInventoryRoleFixture(input: false);
        string name = f.NativeInventoryName("Recovered"); f.InitializeNamedInventory(name);
        var goodStack = RuntimeHelpers.GetUninitializedObject(f.T("Timberborn.GoodStackSystem", "GoodStack"));
        f.Call(goodStack, "InitializeInventory", f.Inventory);
        Assert.Same(f.Inventory, f.Property(goodStack, "Inventory")); Assert.Equal(name, f.Property(f.Inventory, "ComponentName"));
        Assert.Equal(false, f.Property(f.Inventory, "Enabled")); Assert.Equal(0, f.Physical);
        var assembly = f.Stock.Resources.GetType().Assembly;
        var role = Enum.Parse(assembly.GetType("Wildfire.Timberborn.Mapping.TimberbornNativeInventoryRole")!, "GoodStack");
        var capture = assembly.GetType("Wildfire.Timberborn.Runtime.TimberbornInitialWorldProjectionProvider")!
            .GetMethod("CaptureInventoryMaterial", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [Activator.CreateInstance(assembly.GetType("Wildfire.Timberborn.Mapping.TimberbornInventoryDeclaration")!, role, name), f.Inventory]);
        Assert.Null(capture); // Existing physical-part omission cannot be used as a static-role witness.
    }
}
