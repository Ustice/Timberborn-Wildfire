using System.Reflection;
using static Wildfire.Timberborn.Tests.NativeSmokeDeliveryFixture;

namespace Wildfire.Timberborn.Tests;

public sealed partial class NativeRuntimeIncompleteDispatchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeSmokeCallbackFailureBlocksRealRuntimeSaveAndContinuation(bool warningAlsoFails)
    {
        var runtime = new Fixture(synchronizationAlreadyComplete: true) { FailWarnings = warningAlsoFails };
        var smoke = new NativeSmokeDeliveryFixture(); smoke.Add(A); smoke.Add(B);
        var cause = new InvalidOperationException("native second actor callback");
        smoke.OnToggle(B, () =>
        {
            Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(runtime.SaveAdmission).InnerException);
            throw cause;
        });
        runtime.ConfigureSmoke(smoke, true, A, B);
        var error = Assert.Throws<TargetInvocationException>(runtime.Dispatch);
        Assert.Same(cause, error.GetBaseException());
        Assert.True(runtime.Poisoned); Assert.True(smoke.Active(A)); Assert.True(smoke.Active(B));
        Assert.Equal(A, Get(Assert.Single(smoke.History()), "BeaverId"));
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(runtime.SaveAdmission).InnerException);
        Assert.Throws<TargetInvocationException>(runtime.Dispatch); Assert.Equal(1, runtime.Simulator.Swaps);
    }

    [Fact]
    public void NativeRecoveryCallbackFailureBlocksRuntimeSaveWithPriorExposedHistoryRetained()
    {
        var runtime = new Fixture(synchronizationAlreadyComplete: true);
        var smoke = new NativeSmokeDeliveryFixture(); smoke.Add(A); smoke.Dispatch(0, true, A);
        var cause = new InvalidOperationException("native recovery callback"); smoke.OnToggle(A, () => throw cause);
        runtime.ConfigureSmoke(smoke, false, A);
        Assert.Same(cause, Assert.Throws<TargetInvocationException>(runtime.Dispatch).GetBaseException());
        Assert.False(smoke.Active(A)); Assert.Equal(true, Get(Assert.Single(smoke.History()), "IsExposed"));
        Assert.True(runtime.Poisoned);
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(runtime.SaveAdmission).InnerException);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnchangedActorPreflightFailureDoesNotInvalidateEarlierCompletedActor(bool completedPrefix)
    {
        var runtime = new Fixture(synchronizationAlreadyComplete: true);
        var smoke = new NativeSmokeDeliveryFixture();
        if (completedPrefix) smoke.Add(A);
        runtime.ConfigureSmoke(smoke, true, completedPrefix ? [A, B] : [B]);
        runtime.Dispatch();
        Assert.False(runtime.Poisoned);
        Assert.Equal(completedPrefix ? 1 : 0, smoke.History().Length);
        if (completedPrefix) { Assert.True(smoke.Active(A)); Assert.Equal(A, Get(smoke.History()[0], "BeaverId")); }
        Assert.IsType<ArgumentNullException>(Assert.Throws<TargetInvocationException>(runtime.SaveAdmission).InnerException);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FinalSmokeObservationFailureKeepsCompletedNativeStateSaveable(bool playerAlert, bool warningAlsoFails)
    {
        var runtime = new Fixture(synchronizationAlreadyComplete: true) { FailWarnings = warningAlsoFails };
        var smoke = new NativeSmokeDeliveryFixture(); smoke.Add(A);
        var cause = new IOException("final observation");
        if (!playerAlert) smoke.OnLog = _ => throw cause;
        runtime.ConfigureSmoke(smoke, true, A);
        if (playerAlert) runtime.ThrowPlayerAlert(cause);
        runtime.Dispatch();
        Assert.False(runtime.Poisoned); Assert.True(smoke.Active(A)); Assert.Single(smoke.History());
        Assert.IsType<ArgumentNullException>(Assert.Throws<TargetInvocationException>(runtime.SaveAdmission).InnerException);
    }

    private sealed partial class Fixture
    {
        internal void ThrowPlayerAlert(Exception cause) => Set(_runtime, "_playerFireAlerts",
            New("Alerts.TimberbornPlayerFireAlertSink",
                Proxy("Alerts.ITimberbornPlayerNotificationSink", (_, _) => null),
                Proxy("Runtime.ITimberbornFireLogSink", (_, _) => throw cause), 1u));

        internal void ConfigureSmoke(NativeSmokeDeliveryFixture smoke, bool exposed, params string[] ids)
        {
            Set(_runtime, "_beaverFieldBehaviorDispatcher", smoke.Dispatcher);
            Array positions = Array.CreateInstance(M("Beavers.TimberbornBeaverPositionSample"), ids.Length);
            for (int i = 0; i < ids.Length; i++) positions.SetValue(Record("Beavers.TimberbornBeaverPositionSample",
                new() { ["BeaverId"] = ids[i] }), i);
            object positionSnapshot = M("Beavers.TimberbornBeaverPositionSnapshot").GetMethod("Available")!.Invoke(null, [positions])!;
            var provider = Proxy("Beavers.ITimberbornBeaverPositionProvider", (_, _) => positionSnapshot);
            Array samples = Array.CreateInstance(M("Visuals.TimberbornGpuVisualFieldSample"), 1);
            samples.SetValue(Record("Visuals.TimberbornGpuVisualFieldSample", new() { ["Smoke"] = exposed ? .3f : 0f }), 0);
            var visual = Proxy("Visuals.ITimberbornGpuVisualFieldSurface", (method, _) => method.Name switch
            {
                "get_State" => Record("Visuals.TimberbornGpuVisualFieldSurfaceState", new() { ["IsBound"] = true }),
                "InspectCells" => samples,
                _ => throw new NotSupportedException(method.Name),
            });
            Set(_runtime, "_beaverFieldExposureTelemetry", New("Beavers.TimberbornBeaverFieldExposureTelemetry",
                provider, visual, NativeSmokeDeliveryFixture.Proxy("Runtime.ITimberbornFireLogSink", (_, _) => null), false));
        }
    }
}
