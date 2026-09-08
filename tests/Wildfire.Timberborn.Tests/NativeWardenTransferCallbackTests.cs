using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed partial class NativeWardenDistrictLifecycleTests
{
    [Fact]
    public void ReturnDestinationDisabledByActualNativeTakeCallbackMustNotReportSuccessfulTransfer()
    {
        using var f = new Fixture();
        var destination = f.ReturnDestination();
        f.On(f.Inventory, "InventoryChanged", () =>
        {
            Assert.Equal(0, f.Quantity); // Actual native source subtraction already happened.
            f.Call(destination, "Disable");
        });
        bool? returned = null;
        var failure = Record.Exception(() => returned = (bool)f.Call(f.Equipment, "TryReturn", destination)!);
        int destinationWater = (int)f.Call(destination, "AmountInStock", "Water")!;
        var saveFailure = Record.Exception(() => f.Call(f.Resources, "ThrowIfSaveUnsafe"));
        Assert.False((bool)f.Get(destination, "Enabled")!);
        Assert.Equal(0, f.Quantity);
        Assert.True(f.Poisoned,
            $"Known callback-invalidated transfer reported {returned}; failure={failure?.GetType().Name ?? "none"}, destinationWater={destinationWater}, save remained permitted={saveFailure is null}.");
        Assert.NotNull(failure);
        Assert.Null(returned);
    }

    [Fact]
    public void FullDestinationAfterTakeIsAlreadyRejectedByActualNativeCapacityCheck()
    {
        using var f = new Fixture();
        var destination = f.ReturnDestination();
        f.On(f.Inventory, "InventoryChanged", () => f.Call(destination, "GiveExisting", f.WaterUnit()));
        Assert.Throws<TargetInvocationException>(() => f.Call(f.Equipment, "TryReturn", destination));
        Assert.True(f.Poisoned);
        Assert.Equal(0, f.Quantity);
        Assert.Equal(1, f.Call(destination, "AmountInStock", "Water")); // No overflow from the second give.
        Assert.Throws<TargetInvocationException>(() => f.Call(f.Resources, "ThrowIfSaveUnsafe"));
    }

    [Fact]
    public void OrdinaryNativeReturnPreservesOneUnitAndAllowsSave()
    {
        using var f = new Fixture();
        var destination = f.ReturnDestination();
        Assert.True((bool)f.Call(f.Equipment, "TryReturn", destination)!);
        Assert.Equal(0, f.Quantity);
        Assert.Equal(1, f.Call(destination, "AmountInStock", "Water"));
        Assert.False(f.Poisoned);
        f.Call(f.Resources, "ThrowIfSaveUnsafe");
    }

    private sealed partial class Fixture
    {
        internal object WaterUnit() => Activator.CreateInstance(T("Timberborn.Goods", "GoodAmount"), "Water", 1)!;
        internal object ReturnDestination()
        {
            var inventory = RuntimeHelpers.GetUninitializedObject(T("Timberborn.InventorySystem", "Inventory"));
            foreach (string field in new[] { "_storage", "_reservedStock", "_reservedCapacity" })
                Set(inventory, field, Activator.CreateInstance(T("Timberborn.Goods", "GoodRegistry")));
            Set(inventory, "_allowedGoods", Activator.CreateInstance(T("Timberborn.Goods", "StorableGoodRegistry")));
            AttachCache(inventory, Activator.CreateInstance(T("Timberborn.InventorySystem", "Inventories"))!);
            var factory = Activator.CreateInstance(T("Timberborn.InventorySystem", "InventoryInitializerFactory"), new object?[] { null })!;
            var initializer = Call(factory, "Create", inventory, 1, "Fixture.WardenReturn")!;
            var good = T("Timberborn.Goods", "StorableGood").GetMethod("CreateAsGivable")!.Invoke(null, ["Water"]);
            var allowed = Activator.CreateInstance(T("Timberborn.Goods", "StorableGoodAmount"), good, 1)!;
            Call(initializer, "AddAllowedGood", allowed);
            Call(initializer, "Initialize");
            Call(inventory, "Enable");
            return inventory;
        }
    }
}
