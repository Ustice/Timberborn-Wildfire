using Wildfire.Timberborn.FireSafety;

namespace Wildfire.Timberborn.Tests;

public sealed class FireRouteSafetyTests
{
    [Fact]
    public void BurningOriginAllowsShortEscapeButNotIngress()
    {
        FireRouteSample[] samples = { new(0, 4), new(.25f, 4), new(.5f, 2), new(1, 0) };
        Assert.True(FireRouteSafety.CanTraverse(samples, escaping: true));
        Assert.False(FireRouteSafety.CanTraverse(samples, escaping: false));
    }

    [Fact]
    public void EscapeCannotReenterFireAfterReachingSafety()
    {
        FireRouteSample[] samples = { new(0, 4), new(.25f, 0), new(.5f, 2), new(1, 0) };
        Assert.False(FireRouteSafety.CanTraverse(samples, escaping: true));
    }

    [Fact]
    public void EscapeCannotTravelDeeperOrStayInHazardIndefinitely()
    {
        Assert.False(FireRouteSafety.CanTraverse(new FireRouteSample[] { new(0, 2), new(.25f, 3), new(1, 0) }, true));
        Assert.False(FireRouteSafety.CanTraverse(new FireRouteSample[] { new(0, 3), new(2, 3), new(2.25f, 0) }, true));
        Assert.False(FireRouteSafety.CanTraverse(new FireRouteSample[] { new(0, 3), new(.25f, 2) }, true));
    }

    [Fact]
    public void UnknownWorldCellsAreNotTreatedAsSafeEscape()
    {
        Assert.False(FireRouteSafety.CanTraverse(new FireRouteSample[] { new(0, -1), new(.25f, 0) }, true));
        Assert.False(FireRouteSafety.CanTraverse(new FireRouteSample[] { new(0, 3), new(.25f, -1) }, true));
    }
}
