using Wildfire.Timberborn.Beavers.Emergency;

namespace Wildfire.Timberborn.Tests;

public sealed class CarryEmergencyTests
{
    [Theory]
    [InlineData(false, false, true, true)] // Rejected native path can still say success.
    [InlineData(true, false, true, false)] // A stopped walk is not physical arrival.
    [InlineData(true, false, false, true)] // Native failure cannot become delivery.
    public void InvalidLaunchReceiptKeepsNativeGoodsUnderEmergencyOwnership(bool accepted, bool running, bool success, bool atTarget)
    {
        var state = new CarryEmergencyState();
        state.Begin();
        Assert.False(state.CanRelease(true, true, new(accepted, running, success, atTarget)));
        Assert.True(state.Active);
    }
    [Theory]
    [InlineData(true, false, false)] // Newly launched native route is running.
    [InlineData(false, true, true)] // Already physically at the actual delivery access.
    public void VerifiedFreshDeliveryCanRelease(bool running, bool success, bool atTarget)
    {
        var state = new CarryEmergencyState();
        state.Begin();
        Assert.True(state.CanRelease(true, true, new(true, running, success, atTarget)));
        state.Release();
        Assert.Equal(CarryEmergencyPhase.Inactive, state.Phase);
    }
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void LostReservationOrUnsafeRefugeDefeatsOtherwiseSuccessfulLaunch(bool atSafeRefuge, bool reservationMatches)
    {
        var state = new CarryEmergencyState();
        state.Begin();
        Assert.False(state.CanRelease(atSafeRefuge, reservationMatches, new(true, true, false, false)));
        Assert.True(state.Active);
    }
    [Fact]
    public void SaveDuringTransitionIsRejectedWithoutPoisonWhenNoCallbackEscapes()
    {
        var safety = new CarryEmergencySafety();
        safety.Transition(() => Assert.Throws<InvalidOperationException>(safety.ThrowIfSaveUnsafe), () => { });
        safety.ThrowIfSaveUnsafe();
    }
    [Fact]
    public void PartialOwnershipMutationThenFailurePoisonsAndDoesNotReplay()
    {
        var safety = new CarryEmergencySafety();
        var exception = new InvalidOperationException("native path listener failed");
        var writes = 0;
        Assert.Same(exception, Assert.Throws<InvalidOperationException>(() => safety.Transition(() => { writes++; throw exception; }, () => { })));
        Assert.True(safety.IsPoisoned);
        Assert.Throws<InvalidOperationException>(() => safety.Transition(() => writes++, () => { }));
        Assert.Equal(1, writes);
        Assert.Same(exception, Assert.Throws<InvalidOperationException>(safety.ThrowIfSaveUnsafe).InnerException);
    }
    [Fact]
    public void FailureAfterMovementLaunchStopsEvenIfLogicalPhaseAlreadyReleased()
    {
        var state = new CarryEmergencyState();
        state.Begin();
        var safety = new CarryEmergencySafety();
        bool moving = false;
        Assert.Throws<InvalidOperationException>(() => safety.Transition(() =>
        {
            moving = true;
            state.Release();
            throw new InvalidOperationException("native ownership handoff failed");
        }, () => moving = false));
        Assert.False(moving);
        Assert.True(safety.IsPoisoned);
    }
    [Fact]
    public void FailedStopPreservesOriginalFailureAndPreventsSave()
    {
        var safety = new CarryEmergencySafety();
        var handoff = new Exception("handoff");
        var stop = new Exception("stop");
        Assert.Same(handoff, Assert.Throws<Exception>(() => safety.Transition(() => throw handoff, () => throw stop)));
        Assert.Same(stop, safety.StopFailure);
        Assert.Same(handoff, Assert.Throws<InvalidOperationException>(safety.ThrowIfSaveUnsafe).InnerException);
    }
    [Fact]
    public void NavigationCallbackOutsideTransitionPoisonsSameSaveGuard()
    {
        var safety = new CarryEmergencySafety();
        var exception = new Exception("refresh callback failed");
        safety.FailMovement(exception, () => { });
        safety.FailMovement(new Exception("secondary failure"), () => { });
        Assert.Same(exception, Assert.Throws<InvalidOperationException>(safety.ThrowIfSaveUnsafe).InnerException);
    }
    [Fact]
    public void RestoredHoldingNeverTimesOutIntoTheOldDelivery()
    {
        var state = new CarryEmergencyState();
        state.Restore(1, (int)CarryEmergencyPhase.Holding, (int)CarryEmergencyReason.CriticalNeeds, 2);
        state.Advance(100);
        Assert.True(state.Active);
        Assert.Equal(CarryEmergencyReason.CriticalNeeds, state.Reason);
        // This preserves ownership, not a starvation solution: the development UI states that limitation.
    }
    [Theory]
    [InlineData(2, 2, 4, 0)]
    [InlineData(1, 0, 0, 0)]
    [InlineData(1, 1, 4, 0)]
    [InlineData(1, 2, 1, 0)]
    [InlineData(1, 2, 4, -1)]
    [InlineData(1, 2, 4, float.NaN)]
    public void CorruptActiveSaveIsRejected(int version, int phase, int reason, float hours) =>
        Assert.Throws<InvalidOperationException>(() => new CarryEmergencyState().Restore(version, phase, reason, hours));
    [Fact]
    public void UnknownNativeBinaryRejectsBeforeTheCallerCanMutateOwnership()
    {
        var directory = Path.Combine(Path.GetTempPath(), "wildfire-carry-unknown-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Timberborn.BehaviorSystem.dll"), "unreviewed version");
            var admitted = false;
            Assert.Throws<InvalidOperationException>(() => { CarryEmergencyBuild.VerifyDirectory(directory); admitted = true; });
            Assert.False(admitted);
        }
        finally { Directory.Delete(directory, true); }
    }
}
