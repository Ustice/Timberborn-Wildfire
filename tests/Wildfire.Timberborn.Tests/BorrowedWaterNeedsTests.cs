using System.Runtime.CompilerServices;
using Wildfire.Core;

namespace Wildfire.Timberborn.Tests;

public sealed partial class BorrowedDutyRecoveryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NativeCurrentExecutorYieldsEmptyReturnOnlyForCriticalNeed(bool critical)
    {
        using var f = new Fixture();
        var executor = WaterExecutor(f, 3);
        var walk = new TimberbornFireWalkTests.Fixture { Stopped = true, TickStatus = "Running" };
        Set(executor, "_movement", walk.Helper);
        Set(executor, "_field", ObservableField(f));
        Set(executor, "_needs", ActualNeeds(f, critical));
        var character = RuntimeHelpers.GetUninitializedObject(f.Type("Timberborn.Characters", "Character"));
        Set(character, "<Alive>k__BackingField", true); // Explicit managed life input; no positive Unity object claim.
        var mortal = RuntimeHelpers.GetUninitializedObject(f.Type("Timberborn.MortalSystem", "Mortal"));
        Set(mortal, "_character", character); Set(executor, "_mortal", mortal);
        Set(f.Roots.Manager, "_tickService", NativePersistenceProxy.Create(f.Type("Timberborn.TickSystem", "ITickService"), (_, _) => .01f));
        Set(f.Roots.Manager, "_runningExecutor", executor);
        // Actual installed manager calls this executor and clears it only after Success/Failure.
        f.Call(f.Roots.Manager, "TickRunningExecutor");
        if (critical)
        {
            Assert.Null(Get(f.Roots.Manager, "_runningExecutor"));
            Assert.Equal("Idle", f.Call(executor, "get_Phase")!.ToString());
            Assert.False((bool)Get(executor, "_waterIntent")!);
            Assert.Equal(new[] { "stop", "release" }, walk.Trace);
        }
        else
        {
            Assert.Same(executor, Get(f.Roots.Manager, "_runningExecutor"));
            Assert.Equal("Returning", f.Call(executor, "get_Phase")!.ToString());
            Assert.Empty(walk.Trace);
        }
        f.Call(Get(f.Behavior, "_resources")!, "ThrowIfSaveUnsafe");
    }

    private static object ActualNeeds(Fixture f, bool critical)
    {
        var need = RuntimeHelpers.GetUninitializedObject(f.Type("Timberborn.NeedSystem", "Need"));
        Set(need, "<IsCritical>k__BackingField", critical);
        var array = Array.CreateInstance(need.GetType(), 1); array.SetValue(need, 0);
        var needs = RuntimeHelpers.GetUninitializedObject(f.Type("Timberborn.NeedSystem", "Needs"));
        Set(needs, "_needArray", array); // The native critical predicate reads this exact supplied need set.
        var manager = RuntimeHelpers.GetUninitializedObject(f.Type("Timberborn.NeedSystem", "NeedManager"));
        Set(manager, "_needs", needs);
        Assert.Equal(critical, f.Call(manager, "AnyNeedIsInCriticalState"));
        return manager;
    }

    private static object ObservableField(Fixture f)
    {
        var mod = NativeManagedTestContext.ProxyContracts.LoadMod();
        object New(string type, params object?[] args) => Activator.CreateInstance(mod.GetType("Wildfire.Timberborn." + type)!, args)!;
        var runtime = RuntimeHelpers.GetUninitializedObject(mod.GetType("Wildfire.Timberborn.Runtime.TimberbornFireRuntime")!);
        var initialization = New("Runtime.TimberbornRuntimeInitialization");
        Set(runtime, "_resources", Get(f.Behavior, "_resources"));
        Set(runtime, "<Initialization>k__BackingField", initialization);
        f.Call(initialization, "Load");
        f.Call(initialization, "Update", (Func<FireGrid>)(() => new(1, 1, 1)), (Func<bool>)(() => true), (Action<FireGrid>)(_ => { }));
        Set(runtime, "_fireSystem", New("Runtime.TimberbornFireSystem", new TimberbornIncompleteDispatchTests.Simulator()));
        Set(runtime, "_observedCellsReader", NativePersistenceProxy.Create(mod.GetType("Wildfire.Timberborn.Simulation.ITimberbornCellFieldReader")!, (_, _) => new ushort[] { 0 }));
        Set(runtime, "_observedTransportReader", NativePersistenceProxy.Create(mod.GetType("Wildfire.Timberborn.Simulation.ITimberbornTransportFieldReader")!, (_, _) => new uint[] { 0 }));
        var field = New("FireSafety.FireSafetyField", runtime, null);
        Assert.True((bool)f.Call(field, "get_ObservationAvailable")!);
        return field;
    }
}
