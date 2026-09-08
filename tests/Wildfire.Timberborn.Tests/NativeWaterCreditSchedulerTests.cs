using System.Reflection;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeWaterCreditSchedulerTests
{
    [Fact]
    public void NativeLifecycleInstallsAtPostLoadAndObservesBeforeCreditAndLaterCallbacks()
    {
        using var f = new NativeWaterCreditSchedulerFixture();
        f.LoadAll(); // Actual SingletonLifecycleService calls native scheduler.Load then installer.PostLoad.
        f.AssertPreservedSchedulerEntry();
        var other = f.Water.NewInput();
        f.Water.SupplyCompletedNativeReceipt(f.Water.BucketVolume);
        f.LaterAction = () =>
        {
            Assert.True(f.Tainted);
            Assert.Equal(f.Water.BucketVolume, f.Water.Buffer(f.Water.Source));
            NativeShorelineWaterFixture.Call(other, "OnExitFinishedState");
        };
        f.RunNativeSingletonTicks();
        Assert.Equal(new[] { "observe", "credited", "later" }, f.Events);
        Assert.Equal(2, f.MetricStarts); Assert.Equal(2, f.MetricStops);
        f.Water.RequireExclusiveInput(f.Water.Source); // Competing registration has gone.
        Assert.True(f.Tainted); // Ownership history cannot be forgotten.
        bool restoredMarker = f.MarkerRoundTrip(); // Actual native bool serialization, no stored volume.
        Assert.True(restoredMarker);
        f.Tainted = restoredMarker;
        var bucket = f.Water.Bucket();
        Assert.False(f.TryFillWithMarker(bucket));
        Assert.Equal(0, f.Water.Stock(bucket));
    }

    [Fact]
    public void SameInstallationIsIdempotentAndNativeReloadRebuildsThenInstallsOnce()
    {
        using var f = new NativeWaterCreditSchedulerFixture();
        f.LoadAll(); var targets = f.Targets;
        f.Install(); Assert.Equal(targets, f.Targets);
        f.ReloadSchedulerOnly(); Assert.Same(f.Water.InputService, f.Targets[0]);
        f.Install(); f.Install();
        f.RunNativeSingletonTicks();
        Assert.Equal(1, f.Observations);
        f.AssertPreservedSchedulerEntry();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingForeignOrDuplicateTargetIsRejectedWithoutGuessing(bool duplicate)
    {
        using var f = new NativeWaterCreditSchedulerFixture();
        f.LoadAll();
        if (duplicate) f.DuplicateOriginal(); else f.ForeignReplacement();
        var before = f.Targets;
        Assert.Throws<InvalidOperationException>(f.Install);
        Assert.Equal(before, f.Targets);
    }

    [Fact]
    public void ObserverFailurePreventsNativeCreditAndLaterConsumer()
    {
        using var f = new NativeWaterCreditSchedulerFixture();
        f.LoadAll();
        f.Water.SupplyCompletedNativeReceipt(f.Water.BucketVolume);
        var cause = new InvalidOperationException("ownership state could not be recorded");
        f.BeforeObservation = () => throw cause;
        Assert.Same(cause, Assert.Throws<TargetInvocationException>(f.RunNativeSingletonTicks).InnerException);
        Assert.Equal(0f, f.Water.Buffer(f.Water.Source));
        Assert.Equal(new[] { "observe" }, f.Events);
    }

    [Fact]
    public void SourceBornAfterOneCreditIsCaughtBeforeItsFirstSharedCredit()
    {
        using var f = new NativeWaterCreditSchedulerFixture();
        f.LoadAll();
        f.Water.SupplyCompletedNativeReceipt(f.Water.BucketVolume);
        f.LaterAction = () => { f.Water.NewInput(); f.LaterAction = null; };
        f.RunNativeSingletonTicks(); Assert.False(f.Tainted);
        f.Water.SupplyCompletedNativeReceipt(f.Water.BucketVolume); // Next completed native-water batch.
        f.RunNativeSingletonTicks(); Assert.True(f.Tainted);
    }

    [Fact]
    public void NativePipeCoordinateReadDoesNotInvokeCoordinateChangedCallbacksDuringCredit()
    {
        using var f = new NativeWaterCreditSchedulerFixture();
        var pipe = RuntimeHelpers.GetUninitializedObject(f.Water.Type("Timberborn.WaterBuildings", "Timberborn.WaterBuildings.WaterInputPipeCoordinates"));
        var coordinates = f.Water.Source.GetType().GetProperty("Coordinates")!.GetValue(f.Water.Source)!;
        NativeShorelineWaterFixture.Set(pipe, "<Coordinates>k__BackingField", coordinates);
        var changed = pipe.GetType().GetEvent("CoordinatesChanged")!;
        var parameters = changed.EventHandlerType!.GetMethod("Invoke")!.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
        changed.AddEventHandler(pipe, Expression.Lambda(changed.EventHandlerType, Expression.Throw(Expression.New(typeof(InvalidOperationException))), parameters).Compile());
        NativeShorelineWaterFixture.Set(f.Water.Source, "_inputCoordinates", pipe);
        f.LoadAll(); f.Water.SupplyCompletedNativeReceipt(f.Water.BucketVolume);
        f.RunNativeSingletonTicks();
        Assert.False(f.Tainted);
        Assert.Equal(f.Water.BucketVolume, f.Water.Buffer(f.Water.Source));
    }

    [Fact]
    public void LaterConsumerFailureDoesNotErasePriorCollisionMarker()
    {
        using var f = new NativeWaterCreditSchedulerFixture();
        f.LoadAll(); var other = f.Water.NewInput();
        f.Water.SupplyCompletedNativeReceipt(f.Water.BucketVolume);
        var cause = new InvalidOperationException("later consumer failed");
        f.LaterAction = () => { NativeShorelineWaterFixture.Call(other, "OnExitFinishedState"); throw cause; };
        Assert.Same(cause, Assert.Throws<TargetInvocationException>(f.RunNativeSingletonTicks).InnerException);
        Assert.True(f.MarkerRoundTrip());
        Assert.False(f.TryFillWithMarker(f.Water.Bucket()));
    }
}
