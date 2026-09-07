using Wildfire.Timberborn.FireResponse;

namespace Wildfire.Timberborn.Tests;

public sealed class WardenRouteSafetyTests
{
    [Fact]
    public void BurningOriginAllowsShortEscapeButNotIngress()
    {
        WardenRouteSample[] samples = { new(0, 4), new(.25f, 4), new(.5f, 2), new(1, 0) };
        Assert.True(WardenRouteSafety.CanTraverse(samples, escaping: true));
        Assert.False(WardenRouteSafety.CanTraverse(samples, escaping: false));
    }

    [Fact]
    public void EscapeCannotReenterFireAfterReachingSafety()
    {
        WardenRouteSample[] samples = { new(0, 4), new(.25f, 0), new(.5f, 2), new(1, 0) };
        Assert.False(WardenRouteSafety.CanTraverse(samples, escaping: true));
    }

    [Fact]
    public void EscapeCannotTravelDeeperOrStayInHazardIndefinitely()
    {
        Assert.False(WardenRouteSafety.CanTraverse(new WardenRouteSample[] { new(0, 2), new(.25f, 3), new(1, 0) }, true));
        Assert.False(WardenRouteSafety.CanTraverse(new WardenRouteSample[] { new(0, 3), new(2, 3), new(2.25f, 0) }, true));
        Assert.False(WardenRouteSafety.CanTraverse(new WardenRouteSample[] { new(0, 3), new(.25f, 2) }, true));
    }

    [Fact]
    public void UnknownWorldCellsAreNotTreatedAsSafeEscape()
    {
        Assert.False(WardenRouteSafety.CanTraverse(new WardenRouteSample[] { new(0, -1), new(.25f, 0) }, true));
        Assert.False(WardenRouteSafety.CanTraverse(new WardenRouteSample[] { new(0, 3), new(.25f, -1) }, true));
    }
}
