using Wildfire.Timberborn.FireBell;

namespace Wildfire.Timberborn.Tests;

public sealed class BorrowedWaterProgressTests
{
    [Fact]
    public void OneWaterTripCannotSkipPhysicalArrivalsOrApplyTwice()
    {
        var trip = new BorrowedDutyProgress();
        trip.BeginWater();
        Assert.Throws<InvalidOperationException>(() => trip.Loaded());
        Assert.Throws<InvalidOperationException>(() => trip.ArriveSource(false));
        trip.ArriveSource(true);
        trip.Advance(.25f); // Waiting for actual credited native volume does not restart deadline.
        trip.Loaded();
        Assert.Throws<InvalidOperationException>(() => trip.Applied());
        Assert.Throws<InvalidOperationException>(() => trip.ArriveFire(false));
        trip.ArriveFire(true);
        trip.Applied();
        Assert.Equal(BorrowedDutyPhase.Returning, trip.Phase);
        Assert.Equal(.25f, trip.Hours);
        Assert.Throws<InvalidOperationException>(() => trip.Applied());
    }
}
