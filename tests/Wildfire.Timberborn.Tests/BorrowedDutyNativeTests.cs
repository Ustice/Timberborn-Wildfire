using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class BorrowedDutyNativeTests
{
    [Fact]
    public void NativeProductionFlagFollowsBorrowedBehaviorWithoutChangingEmployment()
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        var work = native.LoadNative("Timberborn.WorkSystem");
        var workerType = work.GetType("Timberborn.WorkSystem.Worker")!;
        var worker = RuntimeHelpers.GetUninitializedObject(workerType);
        var donor = RuntimeHelpers.GetUninitializedObject(work.GetType("Timberborn.WorkSystem.Workplace")!);
        Field(workerType, "<Workplace>k__BackingField").SetValue(worker, donor);
        var managerType = native.LoadNative("Timberborn.BehaviorSystem").GetType("Timberborn.BehaviorSystem.BehaviorManager")!;
        var manager = RuntimeHelpers.GetUninitializedObject(managerType);
        Field(workerType, "_behaviorManager").SetValue(worker, manager);
        var nativeJob = RuntimeHelpers.GetUninitializedObject(native.LoadNative("Timberborn.Gathering").GetType("Timberborn.Gathering.GatherWorkplaceBehavior")!);
        Field(managerType, "_runningBehavior").SetValue(manager, nativeJob);
        Assert.Equal(true, workerType.GetProperty("JobRunning")!.GetValue(worker));
        var behaviorType = mod.GetType("Wildfire.Timberborn.FireBell.BorrowedDutyBehavior")!;
        var behavior = RuntimeHelpers.GetUninitializedObject(behaviorType);
        var executorType = mod.GetType("Wildfire.Timberborn.FireBell.BorrowedDutyExecutor")!;
        var executor = Activator.CreateInstance(executorType, new object?[5])!;
        var decision = behaviorType.GetMethod("Own", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(behavior, new[] { executor })!;
        var decisionType = decision.GetType();
        var wrapped = decisionType.GetMethod("TransferNow")!.Invoke(null, new[] { nativeJob, decision })!;
        Assert.Same(behavior, decisionType.GetProperty("Behavior")!.GetValue(wrapped));
        Assert.Same(executor, decisionType.GetProperty("Executor")!.GetValue(wrapped));
        Assert.Equal(false, decisionType.GetProperty("ShouldReturnToBehavior")!.GetValue(wrapped));
        Field(managerType, "_runningBehavior").SetValue(manager, behavior);
        Field(managerType, "_runningExecutor").SetValue(manager, executor);
        Assert.Equal(false, workerType.GetProperty("JobRunning")!.GetValue(worker));
        Assert.Same(donor, workerType.GetProperty("Workplace")!.GetValue(worker));
        // Native TickRunningExecutor observes the idle executor's Success and releases it.
        // Supply no game day-cycle dependency by invoking the executor completion directly here.
        Assert.Equal("Success", executorType.GetMethod("Tick")!.Invoke(executor, new object[] { 0f })!.ToString());
        Field(managerType, "_runningExecutor").SetValue(manager, null);
        Field(managerType, "_runningBehavior").SetValue(manager, nativeJob);
        Assert.Equal(true, workerType.GetProperty("JobRunning")!.GetValue(worker));
        Assert.Same(donor, workerType.GetProperty("Workplace")!.GetValue(worker));
        Assert.False(work.GetType("Timberborn.WorkSystem.IJobBehavior")!.IsAssignableFrom(behaviorType));
    }

    [Fact]
    public void TemporaryOfferOrderRestoresNativeNeighborsAndIsStableBetweenUpdates()
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        var work = native.LoadNative("Timberborn.WorkSystem");
        var workplaceType = work.GetType("Timberborn.WorkSystem.Workplace")!;
        var workplace = RuntimeHelpers.GetUninitializedObject(workplaceType);
        var behaviorType = work.GetType("Timberborn.WorkSystem.WorkplaceBehavior")!;
        var listType = typeof(List<>).MakeGenericType(behaviorType);
        var list = (IList)Activator.CreateInstance(listType)!;
        var ordinaryType = native.LoadNative("Timberborn.Gathering").GetType("Timberborn.Gathering.GatherWorkplaceBehavior")!;
        var first = RuntimeHelpers.GetUninitializedObject(ordinaryType);
        var last = RuntimeHelpers.GetUninitializedObject(ordinaryType);
        var offer = RuntimeHelpers.GetUninitializedObject(mod.GetType("Wildfire.Timberborn.FireBell.BorrowedDutyWorkplaceBehavior")!);
        list.Add(first); list.Add(offer); list.Add(last);
        Field(workplaceType, "_workplaceBehaviors").SetValue(workplace, list);
        var orderType = mod.GetType("Wildfire.Timberborn.FireBell.BorrowedDutyWorkplaceOrder")!;
        var order = Activator.CreateInstance(orderType, nonPublic: true)!;
        void Update(bool armed) => orderType.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(order, new[] { workplace, offer, armed });
        Update(true); Assert.Same(offer, list[0]);
        var version = listType.GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(list);
        Update(true); Assert.Equal(version, listType.GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(list));
        Update(false); Assert.Equal(new[] { first, offer, last }, list.Cast<object>());
        version = listType.GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(list);
        Update(false); Assert.Equal(version, listType.GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(list));
    }
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
