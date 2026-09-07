using Wildfire.Timberborn.FireResponse;

namespace Wildfire.Timberborn.Tests;

public sealed class WardenSortieTests
{
    [Fact]
    public void StoppedOutboundWalkDoesNotBecomeApplication()
    {
        var sortie = new WardenSortie();
        sortie.Begin(alreadyLoaded: true);
        sortie.Arrived(physicallyAtApproach: false);
        sortie.Advance(.2f);
        Assert.Equal(WardenPhase.Returning, sortie.Phase);
        Assert.False(sortie.AwaitingApplication);
    }

    [Fact]
    public void CancellationWinsOverLateWalkSuccess()
    {
        var sortie = new WardenSortie();
        sortie.Begin(alreadyLoaded: true);
        sortie.Cancel();
        sortie.Arrived(physicallyAtApproach: true);
        Assert.Equal(WardenPhase.Returning, sortie.Phase);
    }

    [Fact]
    public void SavedPendingApplicationWaitsForExplicitCommit()
    {
        var original = new WardenSortie();
        original.Begin(alreadyLoaded: true);
        original.Arrived(physicallyAtApproach: true);
        original.Advance(.05f);
        var restored = new WardenSortie();
        restored.Restore((int)original.Phase, original.HoursInPhase);
        restored.Advance(.1f);
        Assert.True(restored.AwaitingApplication);
        restored.Applied();
        Assert.Equal(WardenPhase.Returning, restored.Phase);
        Assert.Throws<InvalidOperationException>(() => restored.Applied());
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(40, 0)]
    [InlineData(1, -1)]
    [InlineData(1, float.NaN)]
    public void CorruptSavedStateIsRejected(int phase, float hours) =>
        Assert.Throws<InvalidOperationException>(() => new WardenSortie().Restore(phase, hours));
}
