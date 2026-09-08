using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeInjuryEmploymentTests
{
    [Fact]
    public void NativeInjuryTransitionUnassignsEmployerBeforePublishingUnemployment()
    {
        using var fixture = new NativeInjuryFixture();
        var employment = AttachWorkRefuser(fixture);
        var events = new List<string>();
        NativeInjuryFixture.On(employment.Workplace, "WorkerUnassigned", () =>
        {
            Assert.Null(NativeInjuryFixture.Get(employment.Worker, "Workplace"));
            Assert.Empty(employment.AssignedWorkers);
            events.Add("workplace-unassigned");
        });
        NativeInjuryFixture.On(employment.Worker, "GotUnemployed", () => events.Add("worker-unemployed"));
        NativeInjuryFixture.On(employment.Refuser, "RefusesWorkChanged", () => events.Add("refusal"));
        fixture.On("NeedChangedActiveState", () => events.Add("injury-active"));
        fixture.Apply(-.25f);
        Assert.Equal(new[] { "workplace-unassigned", "worker-unemployed", "refusal", "injury-active" }, events);
        Assert.Equal(true, NativeInjuryFixture.Get(employment.Refuser, "RefusesWork"));
        fixture.Apply(.25f);
        Assert.Equal(false, NativeInjuryFixture.Get(employment.Refuser, "RefusesWork"));
        Assert.Null(NativeInjuryFixture.Get(employment.Worker, "Workplace")); // Healing does not re-employ the old job.
    }

    [Fact]
    public void EmployerCallbackFailureLeavesInjuryAndUnemploymentButSkipsLaterNativeListeners()
    {
        using var fixture = new NativeInjuryFixture();
        var employment = AttachWorkRefuser(fixture);
        int unemployedNotices = 0, activeNotices = 0;
        NativeInjuryFixture.On(employment.Workplace, "WorkerUnassigned", () => throw new InvalidOperationException("employer callback"));
        NativeInjuryFixture.On(employment.Worker, "GotUnemployed", () => unemployedNotices++);
        fixture.On("NeedChangedActiveState", () => activeNotices++);
        Assert.Throws<TargetInvocationException>(() => fixture.Apply(-.25f));
        Assert.Equal(-.25f, fixture.Points);
        Assert.Null(NativeInjuryFixture.Get(employment.Worker, "Workplace"));
        Assert.Empty(employment.AssignedWorkers);
        Assert.Equal(true, NativeInjuryFixture.Get(employment.Refuser, "RefusesWork"));
        Assert.Equal(0, unemployedNotices);
        Assert.Equal(0, activeNotices);
        fixture.Apply(-.25f);
        Assert.Equal(-.5f, fixture.Points);
        Assert.Equal(0, unemployedNotices); // Repeating damage neither rolls back nor repairs the skipped chain.
    }

    private static Employment AttachWorkRefuser(NativeInjuryFixture fixture)
    {
        var workerType = fixture.Type("Timberborn.WorkSystem", "Worker");
        object worker = RuntimeHelpers.GetUninitializedObject(workerType);
        object workplace = RuntimeHelpers.GetUninitializedObject(fixture.Type("Timberborn.WorkSystem", "Workplace"));
        object refuser = RuntimeHelpers.GetUninitializedObject(fixture.Type("Timberborn.WorkSystem", "WorkRefuser"));
        var workers = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(workerType))!;
        workers.Add(worker);
        NativeInjuryFixture.SetField(workplace, "_assignedWorkers", workers);
        NativeInjuryFixture.SetField(worker, "<Workplace>k__BackingField", workplace);
        NativeInjuryFixture.SetField(refuser, "_needManager", fixture.Manager);
        NativeInjuryFixture.SetField(refuser, "_worker", worker);
        var needEvent = fixture.Manager.GetType().GetEvent("NeedChangedCriticalState")!;
        var nativeListener = refuser.GetType().GetMethod("OnNeedChangedCriticalState", BindingFlags.Instance | BindingFlags.NonPublic)!;
        needEvent.AddEventHandler(fixture.Manager, nativeListener.CreateDelegate(needEvent.EventHandlerType!, refuser));
        return new Employment(worker, workplace, refuser, workers);
    }

    private sealed record Employment(object Worker, object Workplace, object Refuser, IList AssignedWorkers);
}
