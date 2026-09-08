using System.Reflection;
namespace Wildfire.Timberborn.Tests;
public sealed class NativeWardenReservedReturnTests
{
    [Fact]
    public void ConcreteReservedOverloadRequiresPhaseCallbackBeforeReadingNativeLiveness()
    {
        using var f = new NativeWardenReturnFixture();
        var error = Assert.Throws<TargetInvocationException>(() => f.Call(f.Equipment, "TryReturn", f.Destination, f.Reserver, null));
        Assert.IsType<ArgumentNullException>(error.InnerException);
        Assert.False(f.Poisoned);
    }
    [Theory]
    [InlineData("Water", 2, true, false)]
    [InlineData("FertileAsh", 1, true, false)]
    [InlineData("Water", 1, false, false)]
    [InlineData("Water", 1, true, true)]
    public void ExactCapacityRejectsMalformedOrForeignNativeOwnership(string good, int amount, bool fixedAmount, bool consume)
    {
        using var f = new NativeWardenReturnFixture();
        f.Reservation(f.Destination, good, amount, fixedAmount, consume);
        Assert.False(f.Exact()); Assert.Same(f.Destination, f.Get(f.Get(f.Reserver, "CapacityReservation")!, "Inventory"));
    }
    [Fact]
    public void NativeFixedCapacityRequiresSameDestinationAndNoStockReservation()
    {
        using var f = new NativeWardenReturnFixture();
        f.Call(f.Reserver, "ReserveCapacity", f.Destination, f.Amount()); Assert.True(f.Exact());
        f.Reservation(f.Source, kind: "Stock"); Assert.False(f.Exact());
        f.Reservation(f.Source); Assert.False(f.Exact()); Assert.False(f.Poisoned);
    }
    [Fact]
    public void OrdinaryWaterStockSeamTransfersOnceWithoutProductionOrConsumption()
    {
        using var f = new NativeWardenReturnFixture(); f.Give(f.Source); int committed = 0;
        f.Transfer(() => Assert.True(f.Move(commit: () => committed++)));
        Assert.Equal(0, f.Quantity(f.Source)); Assert.Equal(1, f.Quantity(f.Destination)); Assert.Equal(1, committed);
        Assert.Equal(0, f.Call(f.Balance, "GetConsumption", "Water")); Assert.Equal(0, f.Call(f.Balance, "GetProduction", "Water"));
        f.Transfer(() => Assert.False(f.Move())); Assert.False(f.Poisoned);
    }
    [Theory]
    [InlineData("disable")]
    [InlineData("fill")]
    [InlineData("throw")]
    public void NativeSourceCallbackCannotCreditAfterDestinationInvalidation(string change)
    {
        using var f = new NativeWardenReturnFixture(); f.Give(f.Source); bool committed = false;
        var cause = new IOException("native source callback");
        f.On(f.Source, "InventoryChanged", () => { if (change == "throw") throw cause; if (change == "disable") f.Call(f.Destination, "Disable"); else f.Give(f.Destination, 10); });
        var error = Assert.Throws<TargetInvocationException>(() => f.Transfer(() => f.Move(commit: () => committed = true)));
        if (change == "throw") Assert.Same(cause, error.GetBaseException());
        Assert.Equal(0, f.Quantity(f.Source)); Assert.Equal(change == "fill" ? 10 : 0, f.Quantity(f.Destination)); Assert.False(committed); Assert.True(f.Poisoned);
        Assert.Throws<TargetInvocationException>(() => f.Transfer(() => f.Move()));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DestinationOrPhaseCallbackFailurePreservesCompletedNativeDepositWithoutRefund(bool phase)
    {
        using var f = new NativeWardenReturnFixture(); f.Give(f.Source); var cause = new IOException("deposit callback");
        if (!phase) f.On(f.Destination, "InventoryChanged", () => throw cause);
        var error = Assert.Throws<TargetInvocationException>(() => f.Transfer(() => f.Move(commit: () => throw cause)));
        Assert.Same(cause, error.GetBaseException()); Assert.Equal(0, f.Quantity(f.Source)); Assert.Equal(1, f.Quantity(f.Destination)); Assert.True(f.Poisoned);
    }
    [Fact]
    public void OwnerValidationAfterNativeDebitStopsBeforeCredit()
    {
        using var f = new NativeWardenReturnFixture(); f.Give(f.Source); var cause = new IOException("caller owner invalidated");
        var error = Assert.Throws<TargetInvocationException>(() => f.Transfer(() => f.Move(afterDebit: () => throw cause)));
        Assert.Same(cause, error.GetBaseException()); Assert.Equal(0, f.Quantity(f.Source)); Assert.Equal(0, f.Quantity(f.Destination)); Assert.True(f.Poisoned);
    }
}
