namespace Wildfire.Timberborn.Tests;

public sealed class TimberbornDestinationWalkTests
{
    [Fact]
    public void NativeDestinationIsVerifiedAfterEventReturnsAndNaturalArrivalClearsItsPointer()
    {
        var (f, destination) = Fixture();
        f.DuringLaunch = () =>
        {
            f.RaisePath();
            Assert.Null(f.CurrentDestination); // Native FindPath assigns it after subscribers.
            Assert.DoesNotContain("stop", f.Trace);
        };
        Assert.True(f.LaunchDestination(destination));
        Assert.False(f.Arrived(destination));
        f.CurrentDestination = null;
        f.Stopped = true;
        f.TickStatus = "Success";
        Assert.True(f.Arrived(destination));
        f.NearEndpoint = false;
        Assert.False(f.Arrived(destination)); // Stopped + reachable alone is insufficient.
        f.NearEndpoint = true;
        f.Endpoint = f.Position(9);
        Assert.False(f.Arrived(destination)); // Retained endpoint must still be the installed one.
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ImmediateOwnedSuccessRequiresPhysicalEndpoint(bool near)
    {
        var (f, destination) = Fixture();
        f.LaunchStatus = f.TickStatus = "Success";
        f.Stopped = true;
        f.NearEndpoint = near;
        Assert.Equal(near, f.LaunchDestination(destination));
        Assert.Equal(near, f.Arrived(destination));
        if (!near) Assert.Equal("stop", f.Trace.Last());
    }

    [Fact]
    public void SameNativeRefreshCanBeRevalidatedButValueEqualForeignDestinationCannot()
    {
        var (f, destination) = Fixture();
        Assert.True(f.LaunchDestination(destination));
        f.Trace.Clear();
        f.RaisePath(); // Automatic native refresh keeps exact current destination.
        Assert.Contains("pause", f.Trace);
        Assert.True(f.Refresh());
        Assert.Contains("release", f.Trace);
        var foreign = f.NewDestination();
        Assert.True(destination.Equals(foreign)); // Actual native value equality; distinct references.
        f.CurrentDestination = foreign;
        f.RaisePath();
        f.Trace.Clear();
        f.Revision++;
        Assert.False(f.Refresh());
        Assert.DoesNotContain("refresh", f.Trace); // Never refresh an unowned path into apparent ownership.
        Assert.False(f.Arrived(destination));
    }

    [Fact]
    public void ForeignReadyPathInvalidatesArrivalWithoutTouchingItsMover()
    {
        var (f, destination) = Fixture();
        Assert.True(f.LaunchDestination(destination));
        f.CurrentDestination = null;
        f.Stopped = true;
        f.TickStatus = "Success";
        Assert.True(f.Arrived(destination));
        f.Mode = "Ignore";
        f.Trace.Clear();
        f.RaisePath();
        f.CurrentDestination = f.NewDestination();
        Assert.Empty(f.Trace);
        Assert.False(f.Arrived(destination));
    }

    [Fact]
    public void UnsolicitedImmediateArrivalCannotPromoteUnverifiedEndpoint()
    {
        var (f, destination) = Fixture();
        Assert.True(f.LaunchDestination(destination));
        f.RaisePath();
        f.CurrentDestination = null;
        f.Stopped = true;
        f.TickStatus = "Success";
        Assert.False(f.Refresh());
        Assert.False(f.Arrived(destination));
    }

    [Fact]
    public void NestedStartsRejectAfterNativeReturnAndReleaseCallbackCannotSwapOwnership()
    {
        var (f, destination) = Fixture();
        f.DuringLaunch = () => { f.RaisePath(); f.RaisePath(); Assert.DoesNotContain("stop", f.Trace); };
        Assert.False(f.LaunchDestination(destination));
        Assert.Equal(new[] { "returned", "stop" }, f.Trace.TakeLast(2));
        (f, destination) = Fixture();
        f.DuringRelease = () => { f.RaisePath(); f.CurrentDestination = f.NewDestination(); };
        Assert.False(f.LaunchDestination(destination));
        Assert.Equal("stop", f.Trace.Last());
    }

    [Fact]
    public void UnsafeInstalledPathPausesInsideCallAndRefreshFailureLeavesStopToCaller()
    {
        var (f, destination) = Fixture();
        Assert.True(f.LaunchDestination(destination));
        f.Revision++;
        f.Safe = false;
        f.Trace.Clear();
        Assert.False(f.Refresh());
        Assert.Contains("pause", f.Trace);
        Assert.DoesNotContain("stop", f.Trace);
        f.Call("Stop");
        Assert.Equal("stop", f.Trace.Last());
    }

    [Fact]
    public void MissingEndpointAndMissingStartCannotAdmitDestination()
    {
        var (f, destination) = Fixture();
        f.Endpoint = null;
        Assert.False(f.LaunchDestination(destination));
        (f, destination) = Fixture();
        f.DuringLaunch = null;
        Assert.False(f.LaunchDestination(destination));
    }

    [Fact]
    public void RejectedDestinationDoesNotLeakIntoNextVectorSourceTripOrNextDestination()
    {
        var (f, rejected) = Fixture();
        f.Safe = false;
        Assert.False(f.LaunchDestination(rejected));
        f.Safe = true;
        Assert.True(f.Launch()); // Existing source-trip overload clears destination evidence.
        Assert.False(f.Arrived(rejected));
        f.RaisePath();
        Assert.True(f.Refresh());
        var next = f.NewDestination();
        Assert.True(f.LaunchDestination(next));
        f.CurrentDestination = null;
        f.Stopped = true;
        f.TickStatus = "Success";
        Assert.True(f.Arrived(next));
        Assert.False(f.Arrived(rejected));
    }

    private static (TimberbornFireWalkTests.Fixture Fixture, object Destination) Fixture()
    {
        var f = new TimberbornFireWalkTests.Fixture();
        f.Endpoint = f.Position(4);
        f.DuringLaunch = f.RaisePath;
        return (f, f.NewDestination());
    }
}
