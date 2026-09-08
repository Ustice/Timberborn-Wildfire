using System.Collections;
using System.Reflection;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeFertilizerSatchelTests
{
    [Fact]
    public void DedicatedInitializerCreatesPrivateOneUnitNativeInputAndPreservesNativeSaveData()
    {
        using var f = new NativeFertilizerSatchelFixture();
        Assert.Equal("Wildfire.FertilizerSatchel", f.Get(f.Inventory, "ComponentName"));
        Assert.Equal(1, f.Get(f.Inventory, "Capacity"));
        Assert.Equal(false, f.Get(f.Inventory, "PublicInput"));
        Assert.Equal(false, f.Get(f.Inventory, "PublicOutput"));
        Assert.Contains("FertileAsh", ((IEnumerable)f.Get(f.Inventory, "InputGoods")!).Cast<string>());
        Assert.Empty(((IEnumerable)f.Get(f.Inventory, "OutputGoods")!).Cast<string>());
        f.Give(f.Source);
        bool pickedUp = false;
        f.Transfer(() => pickedUp = f.Move(f.Source, f.Inventory));
        Assert.True(pickedUp);
        Assert.Equal(0, f.Quantity(f.Source));
        Assert.Equal(1, f.Quantity(f.Inventory));
        Assert.Equal(0, f.Consumption);
        Assert.Equal(0, f.Production);
        var restored = f.SaveLoadInventory();
        Assert.Equal(1, f.Quantity(restored));
        Assert.Equal(0, f.Consumption);
        Assert.NotSame(f.Inventory, restored);
        f.Transfer(() => Assert.True(f.Move(f.Inventory, f.Source)));
        Assert.Equal(1, f.Quantity(f.Source));
        Assert.Equal(0, f.Quantity(f.Inventory));
        Assert.Equal(0, f.Consumption);
    }

    [Fact]
    public void SuccessfulNativeConsumptionCountsOnceAndEmptySaveDoesNotRestoreCargo()
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Inventory);
        f.Transfer(f.Consume); // Stock seam only; application phase enforcement is tested separately.
        Assert.Equal(0, f.Quantity(f.Inventory));
        Assert.Equal(1, f.Consumption);
        Assert.Equal(0, f.Production);
        Assert.Equal(0, f.Quantity(f.SaveLoadInventory()));
        Assert.Throws<TargetInvocationException>(() => f.Transfer(f.Consume));
        Assert.Equal(1, f.Consumption);
    }

    [Theory]
    [InlineData("InventoryChanged", 0)]
    [InlineData("InventoryStockChanged", 1)]
    public void NativeConsumptionSubscriberFailurePoisonsWithoutReceiptOrReplay(string eventName, int counted)
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Inventory);
        var cause = new InvalidOperationException("native consume subscriber");
        f.On(f.Inventory, eventName, () => throw cause);
        bool committed = false;
        var failure = Assert.Throws<TargetInvocationException>(() => f.Transfer(() => { f.Consume(); committed = true; }));
        Assert.Same(cause, failure.GetBaseException());
        Assert.False(committed);
        Assert.Equal(0, f.Quantity(f.Inventory));
        Assert.Equal(counted, f.Consumption);
        Assert.True(f.Poisoned);
        Assert.Throws<TargetInvocationException>(() => f.Transfer(f.Consume));
        Assert.Throws<TargetInvocationException>(() => f.Call(f.Resources, "ThrowIfSaveUnsafe"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PickupCallbacksCannotProduceAFalseCompletedTransfer(bool disableDestination)
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Source);
        f.On(f.Source, "InventoryChanged", () =>
        {
            if (disableDestination) f.Call(f.Inventory, "Disable");
            else throw new InvalidOperationException("source callback");
        });
        bool completed = false;
        Assert.Throws<TargetInvocationException>(() => f.Transfer(() => completed = f.Move(f.Source, f.Inventory)));
        Assert.False(completed);
        Assert.Equal(0, f.Quantity(f.Source));
        Assert.Equal(0, f.Quantity(f.Inventory));
        Assert.True(f.Poisoned);
        Assert.Equal(0, f.Consumption);
    }

    [Fact]
    public void FullDisabledAndSameInventoryTransfersLeaveNativeGoodsUnchanged()
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Source);
        f.Give(f.Inventory);
        f.Transfer(() => Assert.False(f.Move(f.Source, f.Inventory)));
        f.Transfer(() => Assert.False(f.Move(f.Inventory, f.Inventory)));
        f.Call(f.Source, "Disable");
        f.Transfer(() => Assert.False(f.Move(f.Inventory, f.Source)));
        Assert.Equal(1, f.Quantity(f.Source));
        Assert.Equal(1, f.Quantity(f.Inventory));
        Assert.False(f.Poisoned);
    }

    [Fact]
    public void RawExactReservationsRejectForeignAndWrongQuantityEvenWhenDisabled()
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Source, 2);
        f.Call(f.Reserver, "ReserveExactStockAmount", f.Source, f.Amount());
        Assert.True(f.Reservation("Stock", f.Source));
        Assert.False(f.Reservation("Stock", f.Inventory));
        f.Call(f.Source, "Disable");
        Assert.True(f.Reservation("Stock", f.Source)); // Ownership is not the Enabled getter.
        Assert.Equal(1, f.Call(f.Source, "ReservedAmountInStock", "FertileAsh"));
    }
}
