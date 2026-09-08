using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class AshGrowthObservationBoundaryTests
{
    [Fact]
    public void RestoringExistingAshDoesNotInvokeNativeGrowthMutation()
    {
        var original = new TimberbornAshFieldService();
        original.SyncFromTransportFields(42, [Fertile], dayNumber: 7);
        var saved = original.SaveSnapshot();

        var restored = new TimberbornAshFieldService();
        restored.RestoreSnapshot(42, saved, dayNumber: 7);

        Assert.True(restored.TryGetEntry(0, out var entry));
        Assert.Equal(WildfireAshQuality.Fertile, entry.Quality);
        Assert.Equal(0, restored.LastSummary.GrowthAppliedGrowableCount);
    }

    [Fact]
    public void RefreshingSameObservedFieldDoesNotAdvanceGrowth()
    {
        var service = new TimberbornAshFieldService();
        service.SyncFromTransportFields(42, [Fertile], dayNumber: 7);

        service.SyncFromTransportFields(42, [Fertile], dayNumber: 7);

        Assert.Equal(1, service.LastSummary.FertileAshCellCount);
        Assert.Equal(0, service.LastSummary.GrowthAppliedGrowableCount);
    }

    private static uint Fertile => new WildfireTransportFieldState(0, 0, 0, 1, 0, false).Pack();
}
