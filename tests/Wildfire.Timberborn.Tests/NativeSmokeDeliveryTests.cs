using System.Reflection;
using static Wildfire.Timberborn.Tests.NativeSmokeDeliveryFixture;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeSmokeDeliveryTests
{
    private const string Incomplete = "Wildfire.Timberborn.Beavers.TimberbornBeaverFieldDeliveryException";

    [Fact]
    public void ActualNativeSpeedSetterWritesBeforeMissingAnimatorThrowsAndDoesNotPublishHistory()
    {
        var f = new NativeSmokeDeliveryFixture(); f.Add(A, status: false, worker: true);
        var error = Assert.Throws<TargetInvocationException>(() => f.Dispatch(1, true, A));
        Assert.Equal(.5f, f.Speed(A));
        Assert.IsType<NullReferenceException>(error.GetBaseException());
        Assert.Equal(Incomplete, error.InnerException!.GetType().FullName);
        Assert.Empty(f.History());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualNativeStatusCallbackFailureIsIncompleteIncludingOrdinaryRecovery(bool recovery)
    {
        var f = new NativeSmokeDeliveryFixture(); f.Add(A);
        if (recovery) f.Dispatch(1, true, A);
        var cause = new InvalidOperationException("native status subscriber failed after property write");
        f.OnToggle(A, () => throw cause);
        var error = Assert.Throws<TargetInvocationException>(() => f.Dispatch(5, !recovery, A));
        Assert.Equal(!recovery, f.Active(A));
        Assert.Same(cause, error.GetBaseException());
        Assert.Equal(Incomplete, error.InnerException!.GetType().FullName);
        if (recovery) Assert.Equal(true, Get(Assert.Single(f.History()), "IsExposed"));
        else Assert.Empty(f.History());
    }

    [Fact]
    public void CompletedActorHistorySurvivesLaterUnchangedPreflightFailureWithoutIncompleteClassification()
    {
        var f = new NativeSmokeDeliveryFixture(); f.Add(A); // B is absent from the actual registry.
        var error = Assert.Throws<TargetInvocationException>(() => f.Dispatch(1, true, A, B));
        Assert.NotEqual(Incomplete, error.InnerException!.GetType().FullName);
        Assert.True(f.Active(A));
        Assert.Equal(A, Get(Assert.Single(f.History()), "BeaverId"));
    }

    [Fact]
    public void CompletedActorDoesNotHideLaterNativeMutationWithoutHistory()
    {
        var f = new NativeSmokeDeliveryFixture(); f.Add(A); f.Add(B);
        var cause = new IOException("second actor subscriber"); f.OnToggle(B, () => throw cause);
        var error = Assert.Throws<TargetInvocationException>(() => f.Dispatch(1, true, A, B));
        Assert.Equal(Incomplete, error.InnerException!.GetType().FullName);
        Assert.True(f.Active(A)); Assert.True(f.Active(B));
        Assert.Equal(A, Get(Assert.Single(f.History()), "BeaverId")); Assert.Same(cause, error.GetBaseException());
    }

    [Fact]
    public void FinalSummaryFailureIsObservationalAfterNativeStatusAndHistoryComplete()
    {
        var f = new NativeSmokeDeliveryFixture(); f.Add(A);
        var cause = new IOException("final summary"); f.OnLog = _ => throw cause;
        var error = Assert.Throws<TargetInvocationException>(() => f.Dispatch(1, true, A));
        Assert.Same(cause, error.InnerException);
        Assert.True(f.Active(A)); Assert.Equal(A, Get(Assert.Single(f.History()), "BeaverId"));
    }
}
