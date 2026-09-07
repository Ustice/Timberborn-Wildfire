using Wildfire.Core;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeResourceStepSchedulerTests
{
    [Fact]
    public void BusyWaterAndAshAlternateWhileRejectedCapacitySpendsNeitherResource()
    {
        var scheduler = new NativeResourceStepScheduler();
        var commits = new List<string>();
        int water = 3, ash = 3, ordinaryTicks = 0, attempts = 0;
        bool reject = false;
        NativeResourceAttempt Attempt(bool collectAsh)
        {
            attempts++;
            if (reject) return new(true, null);
            if (collectAsh) { ash--; commits.Add("ash"); } else { water--; commits.Add("water"); }
            scheduler.Committed(collectAsh);
            return new(true, new GpuFireStepResult(Array.Empty<CellDelta>(), 1));
        }
        GpuFireStepResult Ordinary() { ordinaryTicks++; return new(Array.Empty<CellDelta>(), 1); }
        scheduler.Tick(Attempt, Ordinary);
        reject = true;
        scheduler.Tick(Attempt, Ordinary);
        Assert.Equal(2, attempts); // Capacity rejection does not try a second producer into the same full batch.
        Assert.Equal(2, water); Assert.Equal(3, ash); Assert.Equal(1, ordinaryTicks);
        reject = false;
        scheduler.Tick(Attempt, Ordinary); scheduler.Tick(Attempt, Ordinary); scheduler.Tick(Attempt, Ordinary);
        Assert.Equal(new[] { "water", "ash", "water", "ash" }, commits);
        Assert.Equal(1, water); Assert.Equal(1, ash);
    }
    [Fact]
    public void StalePreferredProducerYieldsToReadyOtherProducer()
    {
        var scheduler = new NativeResourceStepScheduler();
        var attempted = new List<bool>();
        scheduler.Tick(ash =>
        {
            attempted.Add(ash);
            return ash ? new(true, new GpuFireStepResult(Array.Empty<CellDelta>(), 1)) : default;
        }, () => throw new InvalidOperationException("Ready ash must advance the step"));
        Assert.Equal(new[] { false, true }, attempted);
    }
}
