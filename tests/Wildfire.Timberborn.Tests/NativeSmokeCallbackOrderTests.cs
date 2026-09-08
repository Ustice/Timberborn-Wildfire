using System.Collections;
using System.Reflection;
using static Wildfire.Timberborn.Tests.NativeSmokeDeliveryFixture;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeSmokeCallbackOrderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StatusCallbackSpeedReductionIsReadBeforeApplyingAndCachingSmokeSpeed(bool alreadyCached)
    {
        var f = new NativeSmokeDeliveryFixture(); f.Add(A, worker: true);
        var originals = (IDictionary)GetField(f.Adapter, "_originalWorkingSpeedMultiplierByBeaverId")!;
        if (alreadyCached) originals.Add(A, 1f);
        // Actual StatusToggle event; the supplied subscriber changes the native worker backing field.
        f.OnToggle(A, () => Set(f.Workers[A], "_workingSpeedMultiplier", .2f));
        var error = Assert.Throws<TargetInvocationException>(() => f.Dispatch(1, true, A));
        Assert.IsType<NullReferenceException>(error.GetBaseException()); // Actual setter writes before missing animator.
        Assert.Equal(alreadyCached ? .2f : .1f, f.Speed(A));
        Assert.Equal(alreadyCached ? 1f : .2f, originals[A]);
        Assert.Empty(f.History());
    }

    [Fact]
    public void ActorRemovedByStatusCallbackCannotReceiveStaleWorkerWrite()
    {
        var f = new NativeSmokeDeliveryFixture(); f.Add(A, worker: true);
        f.OnToggle(A, () => ((IDictionary)GetField(f.Registry, "_entities")!).Remove(Guid.Parse(A)));
        var error = Assert.Throws<TargetInvocationException>(() => f.Dispatch(1, true, A));
        Assert.Equal("Wildfire.Timberborn.Beavers.TimberbornBeaverFieldDeliveryException", error.InnerException!.GetType().FullName);
        Assert.True(f.Active(A)); Assert.Equal(1f, f.Speed(A)); Assert.Empty(f.History());
        // Supplied registry removal, not Unity GameObject destruction or full native death lifecycle.
    }

    [Fact]
    public void RecoveryCallbackHigherSpeedPreventsStaleRestoration()
    {
        var f = new NativeSmokeDeliveryFixture(); f.Add(A, worker: true);
        Set(f.Workers[A], "_workingSpeedMultiplier", .5f);
        var originals = (IDictionary)GetField(f.Adapter, "_originalWorkingSpeedMultiplierByBeaverId")!;
        originals.Add(A, 1f);
        Call(f.Coughs[A], "Activate");
        f.OnToggle(A, () => Set(f.Workers[A], "_workingSpeedMultiplier", .8f));
        var result = Call(f.Adapter, "RecoverSmokeReaction", A)!;
        Assert.Equal("Applied", Get(result, "Status")!.ToString());
        Assert.Equal(.8f, f.Speed(A)); Assert.False(f.Active(A)); Assert.Empty(originals);
    }
}
