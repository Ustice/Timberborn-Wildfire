using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class FertilizerJobNativeTests
{
    private static readonly NativeManagedTestContext Native = NativeManagedTestContext.ProxyContracts;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Fact]
    public void NativeWorkerTransferKeepsActorBehaviorAsJobOwner()
    {
        using var f = new Fixture();
        var behavior = Activator.CreateInstance(f.Stock.Mod("FertilizerBehavior"))!;
        var workplace = Activator.CreateInstance(f.Stock.Mod("FertilizerWorkplaceBehavior"))!;
        var decision = f.Stock.Call(behavior, "Own", f.Executor)!;
        var wrapped = decision.GetType().GetMethod("TransferNow")!.Invoke(null, [workplace, decision])!;
        Assert.Same(behavior, f.Stock.Get(wrapped, "Behavior"));
        Assert.Same(f.Executor, f.Stock.Get(wrapped, "Executor"));
        Assert.Equal(false, f.Stock.Get(wrapped, "ShouldReturnToBehavior"));
        f.Set(f.Manager, "_runningBehavior", behavior);
        var worker = RuntimeHelpers.GetUninitializedObject(f.Stock.T("Timberborn.WorkSystem", "Worker"));
        f.Set(worker, "_behaviorManager", f.Manager);
        Assert.Equal(true, f.Stock.Get(worker, "JobRunning"));
        Assert.Equal(true, f.Stock.Get(f.Stock.Call(behavior, "Decide", new object?[] { null })!, "ShouldReleaseNow"));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("phase")]
    [InlineData("exit")]
    public void PreparedActorBoundaryRejectsChangedNativeOwnershipBeforeAnyQuantityRead(string changed)
    {
        using var f = new Fixture();
        var target = RuntimeHelpers.GetUninitializedObject(f.Stock.Mod("FertilizerJobTarget"));
        f.Set(f.Executor, "_target", target);
        f.Phase(3);
        f.Stock.Call(f.Executor, "RequirePreparedActor", target); // Actual manager recognizes the executor.
        if (changed == "owner") f.Set(f.Manager, "_runningExecutor", null);
        if (changed == "phase") f.Phase(4);
        if (changed == "exit") f.Set(f.Executor, "_exited", true);
        var error = Assert.Throws<TargetInvocationException>(() => f.Stock.Call(f.Executor, "RequirePreparedActor", target));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Equal(0, f.Stock.Quantity(f.Stock.Inventory));
        Assert.Equal(0, f.Stock.Consumption);
        // This exercises the shared pre/post consumption ownership boundary, not positive Unity admission.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualManagerSaveLoadCancelsIntentWithoutEmployerOrMovement(bool behaviorSurvives)
    {
        using var f = new Fixture();
        f.Phase(3);
        var values = new Dictionary<object, object?>();
        object ObjectProxy(string name) => NativePersistenceProxy.Create(f.Stock.T("Timberborn.Persistence", name), (method, args) =>
        {
            if (method.Name == "Set") { values[args[0]!] = args[1]; return null; }
            if (method.Name == "Has") return values.ContainsKey(args[0]!);
            if (method.Name == "Get") return values[args[0]!];
            throw new NotSupportedException(method.Name);
        });
        var saver = NativePersistenceProxy.Create(f.Stock.T("Timberborn.WorldPersistence", "IEntitySaver"), (_, _) => ObjectProxy("IObjectSaver"));
        f.Stock.Call(f.Manager, "SaveRunningExecutor", saver, ObjectProxy("IObjectSaver"));
        Assert.NotEmpty(values);
        using var restored = new Fixture();
        restored.Set(restored.Manager, "_runningExecutor", null);
        BindExecutorCache(restored);
        if (behaviorSurvives)
            restored.Set(restored.Manager, "_runningBehavior", Activator.CreateInstance(restored.Stock.Mod("FertilizerBehavior")));
        int loads = 0;
        var loader = NativePersistenceProxy.Create(f.Stock.T("Timberborn.WorldPersistence", "IEntityLoader"), (_, _) =>
        { loads++; return ObjectProxy("IObjectLoader"); });
        restored.Stock.Call(restored.Manager, "LoadRunningExecutor", loader, ObjectProxy("IObjectLoader"));
        Assert.Equal(behaviorSurvives ? 1 : 0, loads);
        Assert.Equal(behaviorSurvives ? 3 : 0, restored.CurrentPhase);
        Assert.Empty(restored.Walk.Trace);
        Assert.Equal(0, restored.Stock.Quantity(restored.Stock.Inventory));
        if (behaviorSurvives)
        {
            Assert.Equal("Success", restored.Tick());
            Assert.Equal(0, restored.CurrentPhase);
            Assert.Equal(new[] { "stop", "release" }, restored.Walk.Trace);
        }
        // Native manager SaveRunningBehavior requires real Unity truth. This fixture supplies only its
        // resolved actor behavior; the full behavior-reference Save/Load pipeline remains an engine gate.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestoredIntentReleasesOnlyItsExactSourceReferenceWithoutConsumingCargo(bool foreignSource)
    {
        using var f = new Fixture();
        f.Stock.Give(f.Stock.Source);
        f.Stock.Give(f.Stock.Inventory);
        f.Stock.Call(f.Stock.Reserver, "ReserveExactStockAmount", f.Stock.Source, f.Stock.Amount());
        f.Stock.Call(f.Stock.Reserver, "ReserveCapacity", f.Stock.Source, f.Stock.Amount());
        f.Set(f.Executor, "_source", foreignSource ? f.Stock.Inventory : f.Stock.Source);
        f.Set(f.Executor, "_restored", true);
        f.Phase(1);
        Assert.Equal("Success", f.Tick());
        Assert.Same(foreignSource ? f.Stock.Source : null, f.Stock.Get(f.Stock.Get(f.Stock.Reserver, "StockReservation")!, "Inventory"));
        Assert.Same(f.Stock.Source, f.Stock.Get(f.Stock.Get(f.Stock.Reserver, "CapacityReservation")!, "Inventory"));
        Assert.Equal(1, f.Stock.Quantity(f.Stock.Source));
        Assert.Equal(1, f.Stock.Quantity(f.Stock.Inventory));
        Assert.Equal(0, f.Stock.Consumption);
        Assert.False(f.Stock.Poisoned);
        // Actual native UnreserveStock clears this reference. Positive native reservation-counter
        // removal additionally requires Unity liveness and is not simulated here.
    }

    [Fact]
    public void ActualNativeDeathDuringCaptureContinuesTeardownAndCannotPublishSafeCapture()
    {
        using var f = new Fixture();
        f.Phase(1);
        int later = 0;
        var died = f.Actor.GetType().GetEvent("Died")!;
        died.AddEventHandler(f.Actor, Delegate.CreateDelegate(died.EventHandlerType!, f.Executor,
            f.Executor.GetType().GetMethod("OnDied", Flags)!));
        died.AddEventHandler(f.Actor, new EventHandler((_, _) => later++));
        f.Set(f.Executor, "_warn", new Action<string>(_ => throw new IOException("diagnostic")));
        Assert.Throws<TargetInvocationException>(() => f.Stock.Capture(() => f.Stock.Call(f.Actor, "KillCharacter")));
        Assert.Equal(1, later);
        Assert.True(f.Stock.Poisoned);
        Assert.Empty(f.Walk.Trace); // Busy cleanup did not bypass the native resource guard.
        Assert.Equal("Failure", f.Tick());
        Assert.Equal(0, f.Stock.Consumption);
        f.Stock.Call(f.Executor, "DeleteEntity");
        f.Stock.Call(f.Executor, "DeleteEntity");
        Assert.Equal(1, f.Walk.Unsubscriptions);
    }

    [Fact]
    public void ActualDyingFlagIsIndependentOfDeadState()
    {
        using var f = new Fixture();
        var living = Activator.CreateInstance(f.Stock.T("Timberborn.NaturalResourcesLifecycle", "LivingNaturalResource"))!;
        var dying = Activator.CreateInstance(f.Stock.T("Timberborn.NaturalResourcesLifecycle", "DyingNaturalResource"))!;
        f.Set(dying, "_livingNaturalResource", living);
        f.Stock.Call(dying, "OnStartedDying", dying, EventArgs.Empty);
        Assert.Equal(true, f.Stock.Get(dying, "IsDying"));
        Assert.Equal(false, f.Stock.Get(living, "IsDead"));
    }

    private static void BindExecutorCache(Fixture f)
    {
        f.Stock.AttachCache(f.Manager, f.Executor);
        var baseType = f.Stock.T("Timberborn.BaseComponentSystem", "BaseComponent");
        var cache = baseType.GetField("_componentCache", Flags)!.GetValue(f.Manager)!;
        var map = cache.GetType().GetField("_typeIndexMap", Flags)!.GetValue(cache)!;
        var components = new List<object> { f.Manager, f.Executor };
        var readOnly = Activator.CreateInstance(f.Stock.T("Timberborn.Common", "ReadOnlyList`1").MakeGenericType(typeof(object)), Flags, null, [components], null)!;
        map.GetType().GetMethod("CacheType")!.MakeGenericMethod(f.Stock.T("Timberborn.BehaviorSystem", "IExecutor")).Invoke(map, [readOnly]);
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly NativeFertilizerSatchelFixture Stock = new(Native);
        internal readonly TimberbornFireWalkTests.Fixture Walk = new();
        internal readonly object Executor, Manager, Actor;
        internal Fixture()
        {
            Executor = Activator.CreateInstance(Stock.Mod("FertilizerExecutor"), Stock.Resources, null, null, null, null, null)!;
            Manager = RuntimeHelpers.GetUninitializedObject(Stock.T("Timberborn.BehaviorSystem", "BehaviorManager"));
            Set(Manager, "_runningExecutor", Executor);
            Actor = Stock.ModelActorAwake();
            var mortal = RuntimeHelpers.GetUninitializedObject(Stock.T("Timberborn.MortalSystem", "Mortal"));
            Set(mortal, "_character", Actor);
            Set(Executor, "_manager", Manager); Set(Executor, "_mortal", mortal);
            Set(Executor, "_satchel", Stock.Satchel); Set(Executor, "_reserver", Stock.Reserver);
            Set(Executor, "_walk", Walk.Helper); Set(Executor, "_character", Actor);
        }
        internal int CurrentPhase => Convert.ToInt32(Stock.Get(Executor, "Phase"));
        internal void Phase(int value) => Set(Executor, "<Phase>k__BackingField", Enum.ToObject(Stock.Mod("FertilizerJobPhase"), value));
        internal void Set(object target, string field, object? value) => NativeFertilizerSatchelFixture.Set(target, field, value);
        internal string Tick() => Stock.Call(Executor, "Tick", .01f)!.ToString()!;
        public void Dispose() => Stock.Dispose();
    }
}
