using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class FertilizerRecoveryExecutorTests
{
    [Fact]
    public void ActualCriticalNeedEndsOwnedWalkAndClearsItsReservationReferenceWithoutLosingLoadedStock()
    {
        using var f = new Fixture();
        f.Stock.Give(f.Stock.Inventory);
        f.Reserve();
        f.Critical();
        Assert.Equal("Success", f.Tick());
        Assert.False(f.Active);
        Assert.Equal(new[] { "stop", "release" }, f.Walk.Trace);
        Assert.Null(f.Stock.Get(f.Stock.Get(f.Stock.Reserver, "CapacityReservation")!, "Inventory"));
        Assert.Equal(1, f.Stock.Quantity(f.Stock.Inventory));
        Assert.False(f.Stock.Poisoned);
        // Actual GoodReserver clears its reference here. Releasing Inventory capacity itself
        // additionally requires Unity liveness and EntityComponent; that positive path is an engine gate.
    }

    [Fact]
    public void CleanupPreservesForeignStockReservationWhileClearingItsExactCapacityReference()
    {
        using var f = new Fixture(); f.Reserve();
        f.Stock.Give(f.Stock.Source);
        f.Stock.Call(f.Stock.Reserver, "ReserveExactStockAmount", f.Stock.Source, f.Stock.Amount());
        f.Critical();
        Assert.Equal("Success", f.Tick());
        var reservation = f.Stock.Get(f.Stock.Reserver, "StockReservation")!;
        Assert.Same(f.Stock.Source, f.Stock.Get(reservation, "Inventory"));
        Assert.Null(f.Stock.Get(f.Stock.Get(f.Stock.Reserver, "CapacityReservation")!, "Inventory"));
    }

    [Fact]
    public void PoisonPausesOwnedMovementButNativeMortalityStillWinsWithoutReenabling()
    {
        using var f = new Fixture(); f.Reserve();
        f.Stock.Call(f.Stock.Resources, "InvalidateAfterLifecycleFailure");
        Assert.Equal("Running", f.Tick());
        Assert.True(f.Active);
        Assert.Equal(new[] { "pause" }, f.Walk.Trace);
        f.Walk.Trace.Clear(); f.Set(f.Mortal, "<ShouldDie>k__BackingField", true);
        Assert.Equal("Failure", f.Tick());
        Assert.False(f.Active);
        Assert.Equal(new[] { "stop" }, f.Walk.Trace);
        Assert.True(f.Stock.Poisoned);
    }

    [Fact]
    public void CaptureRejectionPreservesActiveCancellationOwnershipUntilARealMutationScope()
    {
        using var f = new Fixture(); f.Reserve(); f.Critical();
        Assert.Throws<TargetInvocationException>(() => f.Stock.Capture(() => f.Tick()));
        Assert.True(f.Active);
        Assert.False(f.Stock.Poisoned); // No native inventory mutation was admitted.
        Assert.Same(f.Stock.Source, f.Stock.Get(f.Stock.Get(f.Stock.Reserver, "CapacityReservation")!, "Inventory"));
        Assert.Equal("Success", f.Tick());
        Assert.False(f.Active);
    }

    [Fact]
    public void MortalityDuringCaptureWinsButInvalidatesSnapshotWhenCleanupCannotEnterGuard()
    {
        using var f = new Fixture(); f.Reserve();
        f.Set(f.Mortal, "<ShouldDie>k__BackingField", true);
        Assert.Throws<TargetInvocationException>(() => f.Stock.Capture(() => Assert.Equal("Failure", f.Tick())));
        Assert.False(f.Active);
        Assert.True(f.Stock.Poisoned);
        Assert.Equal(new[] { "stop" }, f.Walk.Trace);
    }

    [Fact]
    public void IdleTickNeverTouchesNativeMovementOrRequiresActorComponents()
    {
        using var f = new Fixture();
        f.Set(f.Executor, "<Active>k__BackingField", false);
        Assert.Equal("Success", f.Tick());
        Assert.Empty(f.Walk.Trace);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadReadsOnlyOneActiveReturnPhaseAndDoesNotTouchMovementOrStock(bool active)
    {
        using var f = new Fixture();
        f.Stock.Give(f.Stock.Inventory);
        object Key(string name) => f.Executor.GetType().GetField(name, StaticFlags)!.GetValue(null)!;
        object activeKey = Key("ActiveKey"), hoursKey = Key("HoursKey");
        var loader = NativePersistenceProxy.Create(f.Stock.T("Timberborn.Persistence", "IObjectLoader"), (method, args) =>
        {
            if (method.Name == "Has") return false;
            if (method.Name != "Get") throw new NotSupportedException(method.Name);
            if (Equals(args[0], activeKey)) return active;
            if (Equals(args[0], hoursKey)) return .25f;
            throw new NotSupportedException("Unexpected persisted application state.");
        });
        var entity = NativePersistenceProxy.Create(f.Stock.T("Timberborn.WorldPersistence", "IEntityLoader"), (_, _) => loader);
        f.Stock.Call(f.Executor, "Load", entity);
        Assert.Equal(active, f.Active);
        Assert.Empty(f.Walk.Trace);
        Assert.Equal(1, f.Stock.Quantity(f.Stock.Inventory));
        Assert.Equal(0, f.Stock.Consumption);
    }

    [Fact]
    public void DeclinedTripBacksOffOnNativeClockAndCannotBusyRetryOrResumeOldApplication()
    {
        using var f = new Fixture(); f.Critical(); f.Stock.Give(f.Stock.Inventory);
        f.Set(f.Executor, "<Active>k__BackingField", false);
        f.Set(f.Root, "_satchel", f.Stock.Satchel); f.Set(f.Root, "_executor", f.Executor);
        Assert.True(f.Stock.Get(f.Stock.Call(f.Root, "Decide", new object?[] { null })!, "ShouldReleaseNow") is true);
        Assert.Empty(f.Walk.Trace);
        // A second decision at the same native time cannot even reach the executor.
        f.Set(f.Root, "_executor", null);
        Assert.True(f.Stock.Get(f.Stock.Call(f.Root, "Decide", new object?[] { null })!, "ShouldReleaseNow") is true);
        Assert.Equal(1, f.Stock.Quantity(f.Stock.Inventory));
        f.Day = .1f; // Advance the native clock contract; no persisted retry task exists.
        f.Set(f.Root, "_executor", f.Executor);
        Assert.True(f.Stock.Get(f.Stock.Call(f.Root, "Decide", new object?[] { null })!, "ShouldReleaseNow") is true);
    }

    [Fact]
    public void NativeEntitySaverRoundTripsActiveReturnWithoutEmployerOrApplicationTarget()
    {
        using var f = new Fixture();
        f.Set(f.Executor, "_hours", .25f);
        var serialized = Activator.CreateInstance(f.Stock.T("Timberborn.WorldSerialization", "SerializedEntity"), Guid.NewGuid(), "Fixture.Adult")!;
        f.Stock.Call(f.Executor, "Save", Activator.CreateInstance(f.Stock.T("Timberborn.WorldPersistence", "EntitySaver"), serialized));
        var restored = Activator.CreateInstance(f.Executor.GetType(), f.Stock.Resources, null, null, null)!;
        f.Set(restored, "_walk", f.Walk.Helper);
        f.Stock.Call(restored, "Load", Activator.CreateInstance(f.Stock.T("Timberborn.WorldPersistence", "EntityLoader"), serialized));
        Assert.True(f.Stock.Get(restored, "Active") is true);
        Assert.Equal(.25f, restored.GetType().GetField("_hours", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(restored));
        Assert.Empty(f.Walk.Trace); // Missing obsolete destination is settled on first owned Tick, not Load.
    }

    [Fact]
    public void NativeWorkerClassifiesRecoveryAsNonjobWithoutChangingEmployment()
    {
        using var f = new Fixture();
        var worker = RuntimeHelpers.GetUninitializedObject(f.Stock.T("Timberborn.WorkSystem", "Worker"));
        var workplace = RuntimeHelpers.GetUninitializedObject(f.Stock.T("Timberborn.WorkSystem", "Workplace"));
        f.Set(worker, "<Workplace>k__BackingField", workplace);
        f.Set(worker, "_behaviorManager", f.Manager);
        f.Set(f.Manager, "_runningBehavior", f.Root);
        Assert.False((bool)f.Stock.Get(worker, "JobRunning")!);
        Assert.Same(workplace, f.Stock.Get(worker, "Workplace"));
        f.Set(worker, "<Workplace>k__BackingField", null);
        Assert.False((bool)f.Stock.Get(worker, "JobRunning")!); // Recovery has no employer dependency.
        var decision = f.Stock.T("Timberborn.BehaviorSystem", "Decision").GetMethod("ReleaseWhenFinished")!.Invoke(null, [f.Executor])!;
        Assert.False((bool)f.Stock.Get(decision, "ShouldReturnToBehavior")!);
        Assert.Same(f.Executor, f.Stock.Get(decision, "Executor"));
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly NativeFertilizerSatchelFixture Stock = new(NativeManagedTestContext.ProxyContracts);
        internal readonly TimberbornFireWalkTests.Fixture Walk = new();
        private readonly FertilizerRecoveryOrderTests.Fixture _roots;
        internal float Day;
        internal readonly object Executor, Mortal;
        internal object Root => _roots.Recovery;
        internal object Manager => _roots.Manager;
        internal Fixture()
        {
            _roots = new(() => Day);
            _roots.PostInitialize();
            Executor = Activator.CreateInstance(Stock.Mod("FertilizerRecoveryExecutor"), Stock.Resources, null, null, null)!;
            Set(Executor, "_root", _roots.Recovery); Set(Executor, "_walk", Walk.Helper);
            Set(Executor, "_manager", _roots.Manager); Set(Executor, "_satchel", Stock.Satchel);
            Set(Executor, "_reserver", Stock.Reserver); Set(Executor, "<Active>k__BackingField", true);
            Set(_roots.Manager, "_runningExecutor", Executor);
            var actor = Stock.ModelActorAwake();
            Mortal = RuntimeHelpers.GetUninitializedObject(Stock.T("Timberborn.MortalSystem", "Mortal"));
            Set(Mortal, "_character", actor); Set(Executor, "_mortal", Mortal);
        }
        internal void Reserve()
        {
            Stock.Call(Stock.Reserver, "ReserveCapacity", Stock.Source, Stock.Amount());
            Set(Executor, "_destination", Stock.Source);
        }
        internal void Critical()
        {
            var need = RuntimeHelpers.GetUninitializedObject(Stock.T("Timberborn.NeedSystem", "Need"));
            Set(need, "<IsCritical>k__BackingField", true); // Native IsInCriticalState reads critical + Points=0.
            var needs = RuntimeHelpers.GetUninitializedObject(Stock.T("Timberborn.NeedSystem", "Needs"));
            var array = Array.CreateInstance(need.GetType(), 1); array.SetValue(need, 0); Set(needs, "_needArray", array);
            var manager = RuntimeHelpers.GetUninitializedObject(Stock.T("Timberborn.NeedSystem", "NeedManager"));
            Set(manager, "_needs", needs);
            Assert.True(Stock.Call(manager, "AnyNeedIsInCriticalState") is true);
            Set(Executor, "_needs", manager);
        }
        internal void Set(object target, string field, object? value) => NativeFertilizerSatchelFixture.Set(target, field, value);
        internal bool Active => (bool)Stock.Get(Executor, "Active")!;
        internal string Tick() => Stock.Call(Executor, "Tick", .01f)!.ToString()!;
        public void Dispose() { Stock.Dispose(); _roots.Dispose(); }
    }
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.NonPublic;
}
