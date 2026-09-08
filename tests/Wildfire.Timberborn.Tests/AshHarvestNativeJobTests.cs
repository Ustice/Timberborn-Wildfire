using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class AshHarvestNativeJobTests
{
    [Fact]
    public void OwnedAshTransferCountsAsNativeWorkAndDoesNotSelectTheWorkerAsNonworking()
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        var work = native.LoadNative("Timberborn.WorkSystem");
        var workerType = work.GetType("Timberborn.WorkSystem.Worker")!;
        var workplaceType = work.GetType("Timberborn.WorkSystem.Workplace")!;
        var worker = RuntimeHelpers.GetUninitializedObject(workerType);
        var workplace = RuntimeHelpers.GetUninitializedObject(workplaceType);
        Field(workerType, "<Workplace>k__BackingField").SetValue(worker, workplace);
        var assigned = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(workerType))!;
        assigned.Add(worker);
        Field(workplaceType, "_assignedWorkers").SetValue(workplace, assigned);
        var managerType = native.LoadNative("Timberborn.BehaviorSystem").GetType("Timberborn.BehaviorSystem.BehaviorManager")!;
        var manager = RuntimeHelpers.GetUninitializedObject(managerType);
        Field(workerType, "_behaviorManager").SetValue(worker, manager);
        var behaviorType = mod.GetType("Wildfire.Timberborn.Ash.AshHarvestBehavior")!;
        var behavior = RuntimeHelpers.GetUninitializedObject(behaviorType);
        var executor = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.Ash.AshHarvestExecutor")!, new object?[5])!;
        var workplaceBehavior = RuntimeHelpers.GetUninitializedObject(mod.GetType("Wildfire.Timberborn.Ash.TimberbornFertileAshFieldWorkplaceBehavior")!);
        var decision = behaviorType.GetMethod("Own", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(behavior, [executor])!;
        var decisionType = decision.GetType();
        // This is exactly how native WorkerRootBehavior wraps a workplace's result.
        var wrapped = decisionType.GetMethod("TransferNow")!.Invoke(null, [workplaceBehavior, decision])!;
        Assert.Same(behavior, decisionType.GetProperty("Behavior")!.GetValue(wrapped));
        Assert.Same(executor, decisionType.GetProperty("Executor")!.GetValue(wrapped));
        Assert.Equal(false, decisionType.GetProperty("ShouldReturnToBehavior")!.GetValue(wrapped));
        var nonworkingPredicate = workplaceType.GetNestedType("<>c", BindingFlags.NonPublic)!
            .GetMethod("<UnassignWorkerIfNonworking>b__55_0", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var predicateOwner = RuntimeHelpers.GetUninitializedObject(nonworkingPredicate.DeclaringType!);
        void Observe(object current, bool job)
        {
            // Supply the manager's current behavior; execute the actual native callers.
            // No simulated Unity liveness or claim about full root arbitration is involved.
            Field(managerType, "_runningBehavior").SetValue(manager, current);
            Assert.Equal(job, workerType.GetProperty("JobRunning")!.GetValue(worker));
            Assert.Equal(job, workplaceType.GetMethod("AnyWorkerHasJobRunning")!.Invoke(workplace, null));
            Assert.Equal(!job, nonworkingPredicate.Invoke(predicateOwner, [worker]));
            Assert.Same(workplace, workerType.GetProperty("Workplace")!.GetValue(worker));
        }
        Observe(behavior, true);

        // Actual executor completion and ash behavior release do not request a return
        // to the workplace. A later native decision can replace this job normally.
        Field(managerType, "_runningExecutor").SetValue(manager, executor);
        var clockType = native.LoadNative("Timberborn.TimeSystem").GetType("Timberborn.TimeSystem.DayNightCycle")!;
        var tickType = native.LoadNative("Timberborn.TickSystem").GetType("Timberborn.TickSystem.TickService")!;
        Field(managerType, "_dayNightCycle").SetValue(manager, RuntimeHelpers.GetUninitializedObject(clockType));
        Field(managerType, "_tickService").SetValue(manager, RuntimeHelpers.GetUninitializedObject(tickType));
        managerType.GetMethod("TickRunningExecutor", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(manager, null);
        Assert.Null(Field(managerType, "_runningExecutor").GetValue(manager));
        var release = behaviorType.GetMethod("Decide")!.Invoke(behavior, [null])!;
        Assert.Equal(true, decisionType.GetProperty("ShouldReleaseNow")!.GetValue(release));

        var borrowedType = mod.GetType("Wildfire.Timberborn.FireBell.BorrowedDutyBehavior")!;
        var borrowed = RuntimeHelpers.GetUninitializedObject(borrowedType);
        Observe(borrowed, false); // An interruption/non-job decision must clear the flag.
        Observe(behavior, true); // Reentry is not latched off by the earlier non-job.
        Observe(workplaceBehavior, true); // Native workplace work remains a normal job.
        Assert.False(work.GetType("Timberborn.WorkSystem.IJobBehavior")!.IsAssignableFrom(borrowedType));
    }

    private static FieldInfo Field(Type type, string name) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
