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
        NativeResourceAttempt Attempt(NativeResourceProducerKind kind)
        {
            if (kind == NativeResourceProducerKind.FertilizerApplication) return default;
            bool collectAsh = kind == NativeResourceProducerKind.AshCollection;
            attempts++;
            if (reject) return new(true, null);
            if (collectAsh) { ash--; commits.Add("ash"); } else { water--; commits.Add("water"); }
            scheduler.Completed(kind);
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
        var attempted = new List<NativeResourceProducerKind>();
        scheduler.Tick(ash =>
        {
            attempted.Add(ash);
            return ash == NativeResourceProducerKind.AshCollection ? new(true, new GpuFireStepResult(Array.Empty<CellDelta>(), 1)) : default;
        }, () => throw new InvalidOperationException("Ready ash must advance the step"));
        Assert.Equal(new[] { NativeResourceProducerKind.Water, NativeResourceProducerKind.AshCollection }, attempted);
    }
    [Fact]
    public void ThreeReadyKindsRotateOnlyAfterCompletedAdmission()
    {
        var scheduler = new NativeResourceStepScheduler();
        var order = new List<NativeResourceProducerKind>();
        for (int i = 0; i < 6; i++)
            scheduler.Tick(kind =>
            {
                order.Add(kind);
                scheduler.Completed(kind);
                return new(true, new GpuFireStepResult([], 1));
            }, () => throw new Exception("unexpected ordinary step"));
        Assert.Equal(new[] { NativeResourceProducerKind.Water, NativeResourceProducerKind.AshCollection,
            NativeResourceProducerKind.FertilizerApplication, NativeResourceProducerKind.Water,
            NativeResourceProducerKind.AshCollection, NativeResourceProducerKind.FertilizerApplication }, order);
    }

    [Fact]
    public void PreparedFertilizerWithFullQueueRunsOneOrdinaryStepAndNoOtherProducer()
    {
        var scheduler = new NativeResourceStepScheduler();
        scheduler.Completed(NativeResourceProducerKind.AshCollection);
        var attempts = new List<NativeResourceProducerKind>();
        int ordinary = 0;
        for (int i = 0; i < 2; i++)
            scheduler.Tick(kind => { attempts.Add(kind); return new(true, null); },
                () => { ordinary++; return new([], 1); });
        Assert.Equal(new[] { NativeResourceProducerKind.FertilizerApplication,
            NativeResourceProducerKind.FertilizerApplication }, attempts);
        Assert.Equal(2, ordinary);
    }

    [Fact]
    public void NoPreparedKindRunsOrdinaryExactlyOnce()
    {
        var scheduler = new NativeResourceStepScheduler();
        int attempts = 0, ordinary = 0;
        scheduler.Tick(_ => { attempts++; return default; }, () => { ordinary++; return new([], 1); });
        Assert.Equal(3, attempts);
        Assert.Equal(1, ordinary);
    }

}
