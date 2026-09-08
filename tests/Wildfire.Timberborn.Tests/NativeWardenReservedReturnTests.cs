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
        Assert.False(f.Exact()); Assert.Equal(false, f.Call(f.Equipment, "TryReturn", f.Destination, f.Reserver, (Action)(() => throw new Exception("must not commit")))); Assert.Same(f.Destination, f.Get(f.Get(f.Reserver, "CapacityReservation")!, "Inventory"));
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
    [Theory]
    [InlineData("disabled")]
    [InlineData("reserved")]
    [InlineData("foreign")]
    [InlineData("full")]
    public void OrdinaryRefusalKeepsCargoAndGuardHealthy(string kind)
    {
        using var f = new NativeWardenReturnFixture(); f.Give(f.Source);
        if (kind == "disabled") f.Call(f.Source, "Disable");
        if (kind == "reserved") f.Call(f.Source, "ReserveStock", f.Amount());
        if (kind == "foreign") f.Call(f.Source.GetType().GetField("_storage", NativeWardenReturnFixture.Flags)!.GetValue(f.Source)!, "Add", f.Amount("FertileAsh"));
        if (kind == "full") f.Give(f.Destination, 10);
        f.Transfer(() => Assert.False(f.Move()));
        Assert.Equal(1, f.Quantity(f.Source)); Assert.False(f.Poisoned);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacementReservationAfterDebitRejectsBeforeCredit(bool stock)
    {
        using var f = new NativeWardenReturnFixture(); f.Give(f.Source);
        f.On(f.Source, "InventoryChanged", () => f.Reservation(f.Destination, kind: stock ? "Stock" : "Capacity"));
        Assert.Throws<TargetInvocationException>(() => f.Transfer(() => f.Move(afterDebit: () =>
            f.Mod("FireResponse.WardenEquipmentReturnStock").GetMethod("RequireReleased", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [f.Reserver]))));
        Assert.Equal(0, f.Quantity(f.Source)); Assert.Equal(0, f.Quantity(f.Destination)); Assert.True(f.Poisoned);
        Assert.Same(f.Destination, f.Get(f.Get(f.Reserver, stock ? "StockReservation" : "CapacityReservation")!, "Inventory"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DestinationOrPhaseStockDriftCannotReportCompletedReturn(bool phase)
    {
        using var f = new NativeWardenReturnFixture(); f.Give(f.Source);
        Action change = () => f.Give(f.Source);
        if (!phase) f.On(f.Destination, "InventoryChanged", change);
        Assert.Throws<TargetInvocationException>(() => f.Transfer(() => f.Move(commit: phase ? change : () => { })));
        Assert.Equal(1, f.Quantity(f.Source)); Assert.Equal(1, f.Quantity(f.Destination)); Assert.True(f.Poisoned);
    }
    [Theory]
    [InlineData("Wildfire.WardenEquipment", 1, false, true)]
    [InlineData("Foreign.PrivateInventory", 1, false, false)]
    [InlineData("Wildfire.WardenEquipment", 2, false, false)]
    [InlineData("Wildfire.WardenEquipment", 1, true, false)]
    public void NativeDedicatedInventoryTopologyRejectsAliasAndPublicRole(string name, int capacity, bool publicInput, bool expected)
    {
        using var f = new NativeWardenReturnFixture(name, capacity, publicInput);
        var method = f.Mod("FireResponse.WardenEquipmentReturnStock").GetMethod("IsDedicatedInventory", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(expected, method.Invoke(null, [f.Source]));
    }
    [Fact]
    public void RegistrationRetainsItsOriginalInventoryIdentity()
    {
        using var f = new NativeFertilizerSatchelFixture(); f.ModelActorAwake();
        Assert.Equal(true, f.Call(f.Registration, "OwnsInventory", f.Inventory));
        Assert.Equal(false, f.Call(f.Registration, "OwnsInventory", f.Source));
    }
    [Fact]
    public void CapacityObserverVerifiesActualNativeEventThenExplicitRecordClearWithoutUnityTruthClaim()
    {
        using var f = new NativeWardenReturnFixture();
        f.Call(f.Reserver, "ReserveCapacity", f.Destination, f.Amount());
        var watch = f.WatchRelease();
        try
        {
            // Execute real Inventory release event; explicitly supply outer record clear.
            // Actual GoodReserver.UnreserveCapacity's positive Unity branch is engine-only.
            f.Transfer(() => { f.Call(f.Destination, "UnreserveCapacity", f.Amount()); f.ClearReservation(); f.Call(watch, "RequireComplete"); });
            Assert.Equal(0, f.Call(f.Destination, "ReservedCapacity", "Water")); Assert.False(f.Poisoned);
        }
        finally { f.Call(watch, "Dispose"); }
        f.Call(f.Destination, "ReserveCapacity", f.Amount()); // Observer was removed.
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapacityObserverRejectsReplacementOrRepeatedNativeEventsAndUnsubscribes(bool repeated)
    {
        using var f = new NativeWardenReturnFixture();
        f.Call(f.Reserver, "ReserveCapacity", f.Destination, f.Amount());
        if (repeated) f.Call(f.Destination, "ReserveCapacity", f.Amount());
        else f.On(f.Destination, "InventoryChanged", () => f.Reservation(f.Source));
        var watch = f.WatchRelease();
        try
        {
            Assert.Throws<TargetInvocationException>(() => f.Transfer(() =>
            {
                f.Call(f.Destination, "UnreserveCapacity", f.Amount());
                if (repeated) f.Call(f.Destination, "UnreserveCapacity", f.Amount());
            }));
            Assert.True(f.Poisoned); Assert.Equal(0, f.Quantity(f.Source)); Assert.Equal(0, f.Quantity(f.Destination));
        }
        finally { f.Call(watch, "Dispose"); }
        f.Call(f.Destination, "ReserveCapacity", f.Amount()); // No leaked throwing observer.
    }
}
