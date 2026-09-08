using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class AshGrowthObservationBoundaryTests
{
    [Fact]
    public void RestoringExistingAshDoesNotInvokeNativeGrowthMutation()
    {
        var growth = new RecordingGrowth();
        var original = new TimberbornAshFieldService(growth);
        original.SyncFromTransportFields(42, [Fertile], dayNumber: 7);
        var saved = original.SaveSnapshot();
        growth.Calls = 0;

        var restored = new TimberbornAshFieldService(growth);
        restored.RestoreSnapshot(42, saved, dayNumber: 7);

        Assert.True(restored.TryGetEntry(0, out var entry));
        Assert.Equal(WildfireAshQuality.Fertile, entry.Quality);
        Assert.Equal(0, growth.Calls);
    }

    [Fact]
    public void RefreshingSameObservedFieldDoesNotAdvanceGrowth()
    {
        var growth = new RecordingGrowth();
        var service = new TimberbornAshFieldService(growth);
        service.SyncFromTransportFields(42, [Fertile], dayNumber: 7);
        growth.Calls = 0;

        service.SyncFromTransportFields(42, [Fertile], dayNumber: 7);

        Assert.Equal(1, service.LastSummary.FertileAshCellCount);
        Assert.Equal(0, growth.Calls);
    }

    private static uint Fertile => new WildfireTransportFieldState(0, 0, 0, 1, 0, false).Pack();
    private sealed class RecordingGrowth : ITimberbornAshGrowthAdapter
    {
        public int Calls;
        public TimberbornAshGrowthApplicationResult ApplyGrowthBonuses(uint tick, IReadOnlyList<TimberbornAshGrowthBonusRequest> requests)
        {
            Calls++;
            return new(requests.Count, requests.Count, 0, 0);
        }
    }
}
