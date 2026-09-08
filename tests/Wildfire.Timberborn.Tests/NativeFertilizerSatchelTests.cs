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
        Assert.Equal(true, f.Call(f.Get(f.Inventory, "InputGoods")!, "Contains", "FertileAsh"));
        Assert.Equal(0, f.Get(f.Get(f.Inventory, "OutputGoods")!, "Count"));
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
        f.Apply(); // Actual stock seam through the attached-world application commit privilege.
        Assert.Equal(0, f.Quantity(f.Inventory));
        Assert.Equal(1, f.Consumption);
        Assert.Equal(0, f.Production);
        Assert.Equal(0, f.Quantity(f.SaveLoadInventory()));
        Assert.Throws<TargetInvocationException>(() => f.Apply());
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
        var failure = Assert.Throws<TargetInvocationException>(() => f.Apply(() => committed = true));
        Assert.Same(cause, failure.GetBaseException());
        Assert.False(committed);
        Assert.Equal(0, f.Quantity(f.Inventory));
        Assert.Equal(counted, f.Consumption);
        Assert.True(f.Poisoned);
        Assert.Throws<TargetInvocationException>(() => f.Apply());
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
    public void RawExactReservationOwnershipRemainsVisibleWhenDisabled()
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Source, 2);
        f.Call(f.Reserver, "ReserveExactStockAmount", f.Source, f.Amount());
        Assert.True(f.Reservation("Stock", f.Source));
        Assert.False(f.Reservation("Stock", f.Inventory));
        f.Call(f.Source, "Disable");
        Assert.True(f.Reservation("Stock", f.Source)); // Ownership is not the Enabled getter.
        Assert.Equal(1, f.Call(f.Source.GetType().GetField("_reservedStock", NativeFertilizerSatchelFixture.Flags)!.GetValue(f.Source)!, "Amount", "FertileAsh"));
    }

    [Theory]
    [InlineData("Water", 1, true, false)]
    [InlineData("FertileAsh", 2, true, false)]
    [InlineData("FertileAsh", 1, false, false)]
    [InlineData("FertileAsh", 1, true, true)]
    public void PickupReservationMustBeExactNonconsumingUnit(string good, int quantity, bool fixedAmount, bool consume)
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.SetReservation("Stock", f.Source, good, quantity, fixedAmount, consume);
        Assert.False(f.Reservation("Stock", f.Source));
        Assert.Same(f.Source, f.Get(f.Get(f.Reserver, "StockReservation")!, "Inventory"));
    }

    [Fact]
    public void ReturnRequiresExactCapacityAndNeverAcceptsForeignStockOwnership()
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Call(f.Reserver, "ReserveCapacity", f.Source, f.Amount());
        Assert.True(f.Reservation("Capacity", f.Source));
        Assert.False(f.Reservation("Capacity", f.Inventory));
        f.SetReservation("Stock", f.Inventory);
        Assert.False(f.Reservation("Capacity", f.Source));
        Assert.False(f.Reservation("Stock", f.Inventory));
    }

    [Fact]
    public void ConsumptionPrivilegeRejectsOutsideAndReadOrTransferScopesAndValidRejectionKeepsCargo()
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Inventory);
        Assert.Throws<TargetInvocationException>(() => f.Consume());
        f.Capture(() => Assert.Throws<TargetInvocationException>(() => f.Consume()));
        f.Transfer(() => Assert.Throws<TargetInvocationException>(() => f.Consume()));
        f.Apply(reject: true);
        Assert.Equal(1, f.Quantity(f.Inventory));
        Assert.Equal(0, f.Consumption);
        Assert.False(f.Poisoned);
        Assert.Throws<TargetInvocationException>(() => f.Consume());
    }

    [Fact]
    public void PhaseFailureAfterActualConsumptionRetainsLossAndPoisonsTheSameCoordinator()
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Inventory);
        var cause = new IOException("worker phase callback");
        var failure = Assert.Throws<TargetInvocationException>(() => f.Apply(() => throw cause));
        Assert.Same(cause, failure.GetBaseException());
        Assert.Equal(0, f.Quantity(f.Inventory));
        Assert.Equal(1, f.Consumption);
        Assert.True(f.Poisoned);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReturnDestinationOrPhaseFailureNeverRefundsACompletedNativeTransfer(bool phaseFailure)
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Inventory);
        if (!phaseFailure) f.On(f.Source, "InventoryChanged", () => throw new IOException("destination callback"));
        Assert.Throws<TargetInvocationException>(() => f.Transfer(() =>
        {
            Assert.True(f.Move(f.Inventory, f.Source));
            throw new IOException("worker phase callback");
        }));
        Assert.Equal(0, f.Quantity(f.Inventory));
        Assert.Equal(1, f.Quantity(f.Source));
        Assert.Equal(0, f.Consumption);
        Assert.True(f.Poisoned);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeathCleanupDoesNotAbortNativeMortalityOrRetryPoisonedRegistration(bool alreadyPoisoned)
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Inventory);
        f.ModelDistrictRegistration();
        int unregistered = 0;
        f.On(f.District, "InventoryUnregistered", () => { unregistered++; throw new IOException("district cleanup callback"); });
        if (alreadyPoisoned) Assert.Throws<TargetInvocationException>(() => f.Transfer(() => throw new IOException("prior poison")));
        f.Call(f.Satchel, "OnDied", null, EventArgs.Empty);
        f.Call(f.Satchel, "OnDied", null, EventArgs.Empty);
        Assert.Equal(alreadyPoisoned ? 0 : 1, unregistered);
        Assert.True(f.Poisoned);
        Assert.Equal(1, f.Quantity(f.Inventory)); // Native owner deletion, not a refund or consumption, owns eventual loss.
        Assert.Equal(0, f.Consumption);
    }

    [Fact]
    public void CaughtDeathDuringCapturePreventsSnapshotPublicationAndAnyLaterSave()
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Inventory);
        f.ModelDistrictRegistration();
        bool mortalityContinued = false;
        Assert.Throws<TargetInvocationException>(() => f.Capture(() =>
        {
            f.Call(f.Satchel, "OnDied", null, EventArgs.Empty);
            mortalityContinued = true;
        }));
        Assert.True(mortalityContinued);
        Assert.True(f.Poisoned);
        Assert.Equal(1, f.RegisteredProcessors); // Reentrant cleanup was denied, never reported complete.
        Assert.Equal(1, f.Quantity(f.Inventory));
        Assert.Throws<TargetInvocationException>(() => f.Call(f.Resources, "ThrowIfSaveUnsafe"));
    }

    [Fact]
    public void SuccessfulDeathCleanupUnregistersNativePrivateCounterWithoutSpendingGoods()
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Inventory);
        f.ModelDistrictRegistration();
        int unregistered = 0;
        f.On(f.District, "InventoryUnregistered", () => unregistered++);
        f.Call(f.Satchel, "OnDied", null, EventArgs.Empty);
        f.Call(f.Satchel, "OnDied", null, EventArgs.Empty);
        Assert.Equal(1, unregistered);
        Assert.Equal(0, f.RegisteredProcessors);
        Assert.Equal(1, f.Quantity(f.Inventory));
        Assert.Equal(0, f.Consumption);
        Assert.False(f.Poisoned);
    }

    [Fact]
    public void DeathDuringAppliedCallbackConsumesOnlyOnceButCannotReportSuccessfulStep()
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Inventory);
        f.ModelDistrictRegistration();
        var failure = Assert.Throws<TargetInvocationException>(() =>
            f.Apply(() => f.Call(f.Satchel, "OnDied", null, EventArgs.Empty)));
        var step = Assert.IsType<Wildfire.Core.FireSimStepInputException>(failure.InnerException);
        Assert.Equal(Wildfire.Core.FireSimStepInputOutcome.Indeterminate, step.Outcome);
        Assert.Equal(0, f.Quantity(f.Inventory));
        Assert.Equal(1, f.Consumption);
        Assert.True(f.Poisoned);
        Assert.Throws<TargetInvocationException>(() => f.Call(f.Resources, "RequireAshApplicationCommit"));
    }

    [Fact]
    public void ComponentRejectsNullPhaseBeforeReadingLiveStateOrConsuming()
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Inventory);
        var error = Assert.Throws<TargetInvocationException>(() => f.Call(f.Satchel, "ConsumeCommittedUnit", new object?[] { null }));
        Assert.IsType<ArgumentNullException>(error.InnerException);
        Assert.Equal(1, f.Quantity(f.Inventory));
        Assert.False(f.Poisoned);
    }

    [Theory]
    [InlineData("empty", 0)]
    [InlineData("overfilled", 2)]
    [InlineData("reserved", 1)]
    [InlineData("foreign", 1)]
    public void AcceptedApplicationCannotConsumeEmptyReservedOrMismatchedNativeCargo(string kind, int quantity)
    {
        using var f = new NativeFertilizerSatchelFixture();
        if (quantity > 0) f.Give(f.Inventory, quantity);
        if (kind == "reserved") f.Call(f.Inventory, "ReserveStock", f.Amount());
        if (kind == "foreign")
        {
            // Native Inventory.Load trusts its serialized storage registry; model unexpected
            // saved stock without pretending the satchel initializer admitted that good.
            var storage = f.Inventory.GetType().GetField("_storage", NativeFertilizerSatchelFixture.Flags)!.GetValue(f.Inventory)!;
            f.Call(storage, "Add", f.Amount("Water"));
        }
        Assert.Throws<TargetInvocationException>(() => f.Apply());
        Assert.Equal(quantity, f.Quantity(f.Inventory));
        Assert.Equal(0, f.Consumption);
        Assert.True(f.Poisoned); // The fake GPU receipt was already applied; never replay it.
    }

    [Fact]
    public void ActualCachedActorAwakeHooksNativeDeathAndDeletionUnsubscribesWithoutSpending()
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Inventory);
        var character = f.ModelActorAwake();
        f.ModelDistrictRegistration();
        var eventField = character.GetType().GetField("Died", NativeFertilizerSatchelFixture.Flags)!;
        var handlers = (Delegate)eventField.GetValue(character)!;
        Assert.Single(handlers.GetInvocationList());
        handlers.DynamicInvoke(character, EventArgs.Empty); // Drive the real subscribed handler, not native mortality itself.
        Assert.Equal(0, f.RegisteredProcessors);
        f.Call(f.Satchel, "DeleteEntity");
        Assert.Null(eventField.GetValue(character));
        Assert.Equal(1, f.Quantity(f.Inventory));
        Assert.Equal(0, f.Consumption);
        Assert.False(f.Poisoned);
    }

    [Fact]
    public void ReturnRejectsNonfixedReservationWhileNativeCapacityReservationRemainsSupported()
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Call(f.Reserver, "ReserveCapacity", f.Source, f.Amount());
        Assert.Equal(true, f.Get(f.Get(f.Reserver, "CapacityReservation")!, "FixedAmount"));
        Assert.True(f.Reservation("Capacity", f.Source));
        f.SetReservation("Capacity", f.Source, fixedAmount: false);
        Assert.False(f.Reservation("Capacity", f.Source));
        Assert.Same(f.Source, f.Get(f.Get(f.Reserver, "CapacityReservation")!, "Inventory"));
    }

}
