using Wildfire.Timberborn.FireBell;

namespace Wildfire.Timberborn.Tests;

public sealed class BorrowedDutyProgressTests
{
    [Fact]
    public void CancellationAndElapsedDutySurviveReloadWithoutRestartingTrip()
    {
        var progress = new BorrowedDutyProgress(); progress.Begin(); progress.Advance(.75f); progress.RequestCancel();
        var restored = new BorrowedDutyProgress(); restored.Restore((int)progress.Phase, progress.Hours, progress.CancellationRequested);
        Assert.True(restored.CancellationRequested); Assert.Equal(.75f, restored.Hours);
        Assert.Throws<InvalidOperationException>(() => restored.Arrive(false));
        restored.Return(); restored.Finish(); Assert.Equal(BorrowedDutyPhase.Idle, restored.Phase);
    }
    [Fact]
    public void AnyNativeWorkOwnershipOrHealthConflictRejectsBorrowing()
    {
        var ready = new BorrowedDutyEligibility(true, true, true, false, false, false, false, false, false, false, false);
        Assert.True(ready.CanJoin);
        var rejected = new[] { ready with { EmployedAtDonor = false }, ready with { SameDistrict = false },
            ready with { DuringWorkHours = false }, ready with { RefusesWork = true }, ready with { CriticalNeed = true },
            ready with { Mortal = true }, ready with { CarriesGoods = true }, ready with { HasReservation = true },
            ready with { HoldsResponseWater = true }, ready with { HasRunningExecutor = true }, ready with { ResourceStateUnsafe = true } };
        Assert.All(rejected, state => Assert.False(state.CanJoin));
    }
}
