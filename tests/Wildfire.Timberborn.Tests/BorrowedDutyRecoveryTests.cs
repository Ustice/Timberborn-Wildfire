using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed partial class BorrowedDutyRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothInstallationOrdersPreserveNativeAndFertilizerAdjacency(bool fertilizerFirst)
    {
        using var f = new Fixture();
        var original = f.Roots.Roots.Cast<object>().ToArray();
        if (fertilizerFirst) f.Roots.PostInitialize();
        f.AssignMissingDestination();
        f.Call(f.Behavior, "PostInitializeEntity");
        if (!fertilizerFirst) f.Roots.PostInitialize();
        Assert.True(f.Ready);
        Assert.True(f.Roots.Ready);
        var list = f.Roots.Roots;
        Assert.Equal(list.IndexOf(f.Behavior) + 1, list.IndexOf(f.Roots.Recovery));
        Assert.Equal("WorkerRootBehavior", list[list.IndexOf(f.Roots.Recovery) + 1]!.GetType().Name);
        Assert.Equal(original, list.Cast<object>().Where(value => value != f.Behavior && value != f.Roots.Recovery));
        int version = f.Roots.Version;
        f.Call(f.Behavior, "PostInitializeEntity");
        f.Roots.PostInitialize();
        Assert.Equal(version, f.Roots.Version);
    }

    [Fact]
    public void NonparticipatingAdultAndLegacyEmptyBehaviorDoNotChangeNativeRoots()
    {
        using var f = new Fixture();
        f.NativeInitializeAgain();
        var roots = f.Roots.Roots.Cast<object>().ToArray();
        f.Call(f.Behavior, "PostInitializeEntity");
        Assert.Equal(roots, f.Roots.Roots.Cast<object>());
        Assert.False(f.Ready);
        Assert.True(f.Release(f.Call(f.Behavior, "Decide", new object?[] { null })!));
        Assert.Equal("BorrowedDutyBehavior", f.Call(f.Behavior, "get_ComponentName"));
    }

    [Fact]
    public void ChangedNativeOrderBecomesUnavailableWithoutRepeatedArbitrationExceptions()
    {
        using var f = new Fixture();
        f.AssignMissingDestination();
        f.Call(f.Behavior, "PostInitializeEntity");
        f.Roots.Roots.Remove(f.Roots.Stranded);
        Assert.False(f.Ready);
        Assert.Contains("ordered adult roots", (string)f.Call(f.Behavior, "get_UnsupportedReason")!);
        Assert.True(f.Release(f.Call(f.Behavior, "Decide", new object?[] { null })!));
        Assert.True(f.Release(f.Call(f.Behavior, "Decide", new object?[] { null })!));
    }

    [Theory]
    [InlineData("CriticalNeederRootBehavior")]
    [InlineData("StrandedRootBehavior")]
    [InlineData("BorrowedDutyBehavior")]
    public void NativeArbitrationRespectsUrgentRootsAndDoesNotReturnDirectlyToRecovery(string winner)
    {
        using var f = new Fixture();
        f.AssignMissingDestination();
        f.Call(f.Behavior, "PostInitializeEntity");
        var trace = new List<string>();
        Assert.Equal(winner, f.Roots.Arbitrate(name => name == winner || name is "BorrowedDutyBehavior" or "WorkerRootBehavior", trace));
        Assert.DoesNotContain("WorkerRootBehavior", trace);
        if (winner != "BorrowedDutyBehavior") Assert.DoesNotContain("BorrowedDutyBehavior", trace);
        // Decision probes occupy the verified native list positions; not a positive Unity actor launch.
    }

    [Fact]
    public void ActorPersistenceRetainsMissingReturnIdentityWhileNativeManagerSavesOnlyForeignExecutor()
    {
        using var f = new Fixture();
        f.AssignMissingDestination();
        var serialized = f.SaveActorWithForeignWait();
        using var restored = new Fixture();
        var loader = restored.Loader(serialized);
        restored.Call(restored.Behavior, "Load", loader);
        restored.Call(restored.Roots.Manager, "Load", loader);
        Assert.True((bool)restored.Call(restored.Behavior, "get_ReturnAssigned")!);
        Assert.Null(restored.Call(restored.Behavior, "get_ReturnDestination"));
        Assert.Same(restored.Wait, Get(restored.Roots.Manager, "_runningExecutor"));
        Assert.False(restored.Ready); // Load did not install roots, plan movement or use stock.
        Assert.DoesNotContain(restored.Behavior, restored.Roots.Roots.Cast<object>());
        restored.Call(restored.Behavior, "PostInitializeEntity");
        Assert.True(restored.Ready);
        // Manager never serialized the inactive borrowed executor's application/route state.
        Assert.False(restored.HasComponent(loader, "Wildfire.BorrowedDutyExecutor"));
    }

    [Fact]
    public void LegacyAbsentReturnComponentDoesNotInferAssignmentFromEquipment()
    {
        using var f = new Fixture();
        f.AssignMissingDestination();
        f.Call(f.Behavior, "Load", f.Loader(f.NewSerialized()));
        Assert.False((bool)f.Call(f.Behavior, "get_ReturnAssigned")!);
        Assert.Null(f.Call(f.Behavior, "get_ReturnDestination"));
    }

    [Fact]
    public void ExactNativeWaterStockAdmissionRejectsReservedDisabledAndForeignContents()
    {
        using var f = new Fixture();
        using var water = new NativeShorelineWaterFixture(NativeManagedTestContext.ProxyContracts);
        var inventory = water.Bucket();
        f.AttachInventory(inventory);
        var unit = Activator.CreateInstance(f.Type("Timberborn.Goods", "GoodAmount"), "Water", 1)!;
        bool HasWater() => (bool)f.Behavior.GetType().GetMethod("HasReturnWater", Flags)!.Invoke(null, [inventory])!;
        f.Call(inventory, "Enable");
        Assert.False(HasWater());
        f.Call(inventory, "GiveExisting", unit);
        Assert.True(HasWater());
        f.Call(inventory, "ReserveStock", unit);
        Assert.False(HasWater());
        f.Call(inventory, "UnreserveStock", unit);
        f.Call(inventory, "Disable");
        Assert.False(HasWater());
        f.Call(inventory, "Enable");
        var storage = Get(inventory, "_storage")!;
        f.Call(storage, "Add", Activator.CreateInstance(f.Type("Timberborn.Goods", "GoodAmount"), "Log", 1)!);
        Assert.False(HasWater());
        Assert.Equal(1, water.Stock(inventory));
    }

    [Fact]
    public void ReturnLaunchWithForeignCurrentExecutorRejectsBeforeTouchingMovementOrStock()
    {
        using var f = new Fixture();
        var executor = Activator.CreateInstance(f.Mod("BorrowedDutyExecutor"), new object?[5])!;
        Set(executor, "_manager", f.Roots.Manager);
        Set(f.Roots.Manager, "_runningExecutor", f.Wait);
        Assert.False((bool)f.Call(executor, "TryLaunchReturn", new object?[] { null })!);
        Assert.Same(f.Wait, Get(f.Roots.Manager, "_runningExecutor"));
    }

    [Fact]
    public void MissingRestoredDestinationEndsOnlyOwnedReturnAndRetainsIndependentAssignment()
    {
        using var f = new Fixture();
        using var actor = new NativeFertilizerSatchelFixture(NativeManagedTestContext.ProxyContracts);
        var walk = new TimberbornFireWalkTests.Fixture();
        var resources = Get(f.Behavior, "_resources")!;
        var executor = Activator.CreateInstance(f.Mod("BorrowedDutyExecutor"), null, null, resources, null, null)!;
        Set(executor, "_manager", f.Roots.Manager);
        Set(f.Roots.Manager, "_runningExecutor", executor);
        Set(executor, "_movement", walk.Helper);
        Set(executor, "_returnBehavior", f.Behavior);
        Set(executor, "_reserver", actor.Reserver);
        var mortal = RuntimeHelpers.GetUninitializedObject(f.Type("Timberborn.MortalSystem", "Mortal"));
        Set(mortal, "_character", actor.ModelActorAwake());
        Set(executor, "_mortal", mortal);
        Set(executor, "_returnOnly", true);
        Set(executor, "_restored", true);
        f.AssignMissingDestination();
        f.Call(Get(executor, "_progress")!, "Restore", 3, .25f, false);
        Assert.Equal("Success", f.Call(executor, "Tick", .01f)!.ToString());
        Assert.Equal("Idle", f.Call(executor, "get_Phase")!.ToString());
        Assert.True((bool)f.Call(f.Behavior, "get_ReturnAssigned")!);
        Assert.Contains("stop", walk.Trace);
        Assert.DoesNotContain("launch", walk.Trace);
        f.Call(resources, "ThrowIfSaveUnsafe");
    }

    [Fact]
    public void VersionedReturnLoadDoesNotTouchWorldAndRejectsNonreturnPhase()
    {
        using var f = new Fixture();
        var resources = Get(f.Behavior, "_resources")!;
        var source = Activator.CreateInstance(f.Mod("BorrowedDutyExecutor"), null, null, resources, null, null)!;
        Set(source, "_returnOnly", true);
        f.Call(Get(source, "_progress")!, "Restore", 3, .25f, false);
        var serialized = f.NewSerialized();
        var saver = Activator.CreateInstance(f.Type("Timberborn.WorldPersistence", "EntitySaver"), serialized)!;
        f.Call(source, "Save", saver);
        var restored = Activator.CreateInstance(f.Mod("BorrowedDutyExecutor"), new object?[5])!;
        f.Call(restored, "Load", f.Loader(serialized));
        Assert.True((bool)Get(restored, "_returnOnly")!);
        Assert.True((bool)Get(restored, "_restored")!);
        Assert.Null(Get(restored, "_returnInventory"));
        f.Call(Get(source, "_progress")!, "Restore", 1, .25f, false);
        f.Call(source, "Save", saver);
        var failure = Assert.Throws<TargetInvocationException>(() => f.Call(restored, "Load", f.Loader(serialized)));
        Assert.IsType<InvalidOperationException>(failure.InnerException);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly NativeManagedTestContext _native = NativeManagedTestContext.ProxyContracts;
        internal readonly FertilizerRecoveryOrderTests.Fixture Roots = new();
        internal readonly object Behavior, Wait, Entity;
        internal Fixture()
        {
            var clock = NativePersistenceProxy.Create(Type("Timberborn.TimeSystem", "IDayNightCycle"), (_, _) => 0f);
            Behavior = Activator.CreateInstance(Mod("BorrowedDutyBehavior"),
                Activator.CreateInstance(_native.LoadMod().GetType("Wildfire.Timberborn.Resources.NativeResourceCoordinator")!), null, clock)!;
            Set(Behavior, "_manager", Roots.Manager);
            Wait = Activator.CreateInstance(Type("Timberborn.BehaviorSystem", "WaitExecutor"), new object?[] { null })!;
            Entity = RuntimeHelpers.GetUninitializedObject(Type("Timberborn.EntitySystem", "EntityComponent"));
            var components = new List<object> { Roots.Manager, Behavior, Wait, Entity };
            typeof(FertilizerRecoveryOrderTests.Fixture).GetMethod("AttachCache", Flags)!.Invoke(Roots,
                [components, new[] { Type("Timberborn.BehaviorSystem", "Behavior"), Type("Timberborn.BehaviorSystem", "IExecutor") }]);
            Roots.Arbitrate(name => name == "WorkerRootBehavior", new List<string>()); // Native manager logging dependencies.
        }
        internal void NativeInitializeAgain()
        {
            var initializer = RuntimeHelpers.GetUninitializedObject(Type("Timberborn.BeaverBehavior", "BeaverBehaviorInitializer"));
            var components = Roots.Roots.Cast<object>().Append(Roots.Manager).Append(Behavior).Append(initializer)
                .Append(RuntimeHelpers.GetUninitializedObject(Type("Timberborn.BeaverBehavior", "BeaverNeedBehaviorPicker")))
                .Append(RuntimeHelpers.GetUninitializedObject(Type("Timberborn.SleepSystem", "SleepNeedBehavior"))).ToList();
            typeof(FertilizerRecoveryOrderTests.Fixture).GetMethod("AttachCache", Flags)!.Invoke(Roots, [components, Array.Empty<Type>()]);
            Roots.Roots.Clear();
            Call(initializer, "InitializeBehaviors", true);
            Assert.Equal(10, Roots.Roots.Count);
            Assert.DoesNotContain(Behavior, Roots.Roots.Cast<object>());
        }
        internal void AttachInventory(object inventory)
        {
            var inventories = Activator.CreateInstance(Type("Timberborn.InventorySystem", "Inventories"))!;
            typeof(FertilizerRecoveryOrderTests.Fixture).GetMethod("AttachCache", Flags)!.Invoke(Roots,
                [new List<object> { inventory, inventories }, Array.Empty<Type>()]);
        }
        internal Type Type(string assembly, string name) => _native.LoadNative(assembly).GetType(assembly + "." + name)!;
        internal Type Mod(string name) => _native.LoadMod().GetType("Wildfire.Timberborn.FireBell." + name)!;
        internal bool Ready => (bool)Call(Behavior, "get_RecoveryReady")!;
        internal void AssignMissingDestination() => Set(Behavior, "<ReturnAssigned>k__BackingField", true);
        internal bool Release(object decision) => (bool)Call(decision, "get_ShouldReleaseNow")!;
        internal object NewSerialized() => Activator.CreateInstance(Type("Timberborn.WorldSerialization", "SerializedEntity"), Guid.NewGuid(), "Fixture.BorrowedAdult")!;
        internal object Loader(object serialized) => Activator.CreateInstance(Type("Timberborn.WorldPersistence", "EntityLoader"), serialized)!;
        internal bool HasComponent(object loader, string name)
        {
            var key = Activator.CreateInstance(Type("Timberborn.WorldPersistence", "ComponentKey"), name)!;
            return (bool)Call(loader, "HasComponent", key)!;
        }
        internal object SaveActorWithForeignWait()
        {
            Set(Roots.Manager, "_runningBehavior", Behavior);
            Set(Roots.Manager, "_runningExecutor", Wait);
            var serialized = NewSerialized();
            var factory = Activator.CreateInstance(Type("Timberborn.WorldPersistence", "SerializedWorldFactory"), new object?[3])!;
            Call(factory, "SaveEntity", Entity, serialized);
            return serialized;
        }
        internal object? Call(object target, string name, params object?[] args) => target.GetType().GetMethods(Flags)
            .Single(method => method.Name == name && !method.ContainsGenericParameters && method.GetParameters().Length == args.Length &&
                method.GetParameters().Select((parameter, index) => args[index] is null || parameter.ParameterType.IsInstanceOfType(args[index])).All(value => value)).Invoke(target, args);
        public void Dispose() => Roots.Dispose();
    }
    private static object? Get(object target, string name) => target.GetType().GetField(name, Flags)!.GetValue(target);
    private static void Set(object target, string name, object? value) => target.GetType().GetField(name, Flags)!.SetValue(target, value);
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
}
