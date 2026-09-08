using System.Reflection;

namespace Wildfire.Timberborn.Tests;

public sealed partial class BorrowedDutyRecoveryTests
{
    [Theory]
    [InlineData(4)] // fetching
    [InlineData(5)] // waiting for real credit
    [InlineData(6)] // carrying to fire
    [InlineData(7)] // awaiting application
    [InlineData(3)] // consumed and returning
    public void NativeManagerRestoreCancelsWaterIntentOnFirstOwnedTickWithoutResourceOrNavigationWork(int phase)
    {
        using var saved = new Fixture();
        var original = WaterExecutor(saved, phase);
        saved.AssignMissingDestination();
        AttachWaterExecutor(saved, original);
        Set(saved.Roots.Manager, "_runningBehavior", saved.Behavior);
        Set(saved.Roots.Manager, "_runningExecutor", original);
        var serialized = saved.NewSerialized();
        var factory = Activator.CreateInstance(saved.Type("Timberborn.WorldPersistence", "SerializedWorldFactory"), new object?[3])!;
        saved.Call(factory, "SaveEntity", saved.Entity, serialized);

        using var restored = new Fixture();
        var executor = WaterExecutor(restored, 0);
        AttachWaterExecutor(restored, executor);
        var walk = new TimberbornFireWalkTests.Fixture();
        Set(executor, "_movement", walk.Helper);
        var loader = restored.Loader(serialized);
        restored.Call(restored.Behavior, "Load", loader);
        restored.Call(restored.Roots.Manager, "Load", loader);
        Assert.Same(executor, Get(restored.Roots.Manager, "_runningExecutor"));
        Assert.True((bool)Get(executor, "_waterIntent")!);
        Assert.Null(Get(executor, "_waterTrip"));
        Assert.Empty(walk.Trace);
        Assert.True((bool)restored.Call(restored.Behavior, "get_ReturnAssigned")!);
        // None of the source, equipment, target, employer, navigation or simulator dependencies exist.
        // This verifies intent cancellation, not a positive actor pickup or full inventory restore.
        Assert.Equal("Success", restored.Call(executor, "Tick", .01f)!.ToString());
        Assert.Equal("Idle", restored.Call(executor, "get_Phase")!.ToString());
        Assert.False((bool)Get(executor, "_waterIntent")!);
        Assert.True((bool)restored.Call(restored.Behavior, "get_ReturnAssigned")!);
        Assert.Equal(new[] { "stop", "release" }, walk.Trace);
        restored.Call(Get(restored.Behavior, "_resources")!, "ThrowIfSaveUnsafe");
    }

    [Fact]
    public void PreparingRecoveryOutsideArbitrationLeavesCurrentJobAndUnassignedBehaviorInert()
    {
        using var f = new Fixture();
        f.Call(f.Behavior, "PostInitializeEntity");
        Set(f.Roots.Manager, "_runningExecutor", f.Wait);
        f.Call(f.Behavior, "PrepareRecovery");
        int version = f.Roots.Version;
        Assert.Same(f.Wait, Get(f.Roots.Manager, "_runningExecutor"));
        Assert.True((bool)f.Call(f.Behavior, "get_RecoveryPrepared")!);
        Assert.False(f.Ready);
        Assert.False((bool)f.Call(f.Behavior, "get_ReturnAssigned")!);
        Assert.True(f.Release(f.Call(f.Behavior, "Decide", new object?[] { null })!));
        f.Call(f.Behavior, "PrepareRecovery");
        Assert.Equal(version, f.Roots.Version);
        var trace = new List<string>();
        Assert.Equal("WorkerRootBehavior", f.Roots.Arbitrate(name => name == "WorkerRootBehavior", trace));
        Assert.Contains("BorrowedDutyBehavior", trace);
        f.Call(Get(f.Behavior, "_resources")!, "ThrowIfSaveUnsafe");
    }

    [Fact]
    public void UnsupportedPreparationRefusesBeforeMutationWithoutPoisoningCurrentJob()
    {
        using var f = new Fixture();
        f.Call(f.Behavior, "PostInitializeEntity");
        f.Roots.Roots.Remove(f.Roots.Stranded);
        int version = f.Roots.Version;
        var error = Assert.Throws<TargetInvocationException>(() => f.Call(f.Behavior, "PrepareRecovery"));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Contains("ordered adult roots", error.InnerException!.Message);
        Assert.Equal(version, f.Roots.Version);
        Assert.False((bool)f.Call(f.Behavior, "get_ReturnAssigned")!);
        f.Call(Get(f.Behavior, "_resources")!, "ThrowIfSaveUnsafe");
    }

    [Fact]
    public void CancelingLoadedApplicationRetainsNativeBucketAndIndependentReturnBinding()
    {
        using var f = new Fixture();
        using var nativeWater = new NativeShorelineWaterFixture(NativeManagedTestContext.ProxyContracts);
        var inventory = nativeWater.Bucket();
        f.AttachInventory(inventory);
        f.Call(inventory, "Enable");
        f.Call(inventory, "GiveExisting", Activator.CreateInstance(f.Type("Timberborn.Goods", "GoodAmount"), "Water", 1)!);
        var resources = Get(f.Behavior, "_resources")!;
        var equipmentType = NativeManagedTestContext.ProxyContracts.LoadMod().GetType("Wildfire.Timberborn.FireResponse.WardenEquipment")!;
        var equipment = Activator.CreateInstance(equipmentType, resources)!;
        f.Call(equipment, "InitializeInventory", inventory);
        var executor = WaterExecutor(f, 7);
        Set(executor, "_equipment", equipment);
        Set(executor, "_returnBehavior", f.Behavior);
        var walk = new TimberbornFireWalkTests.Fixture();
        Set(executor, "_movement", walk.Helper);
        f.AssignMissingDestination();
        f.Call(executor, "RequestCancel");
        // Supplied application phase reaches the real cancellation resource disposition.
        // It does not fabricate a positive live actor or run a simulator step.
        Assert.Equal("Success", f.Call(executor, "AbortWater")!.ToString());
        Assert.Equal(1, nativeWater.Stock(inventory));
        Assert.True((bool)f.Call(f.Behavior, "get_ReturnAssigned")!);
        Assert.False((bool)Get(executor, "_waterIntent")!);
        Assert.Equal(new[] { "stop", "release" }, walk.Trace);
        f.Call(resources, "ThrowIfSaveUnsafe");
    }

    [Fact]
    public void LegacySaveCannotReinterpretAnExtendedPhaseAsAWaterJob()
    {
        using var f = new Fixture();
        var source = WaterExecutor(f, 7);
        Set(source, "_waterIntent", false); // malformed inactive save; production never emits this phase pairing.
        var serialized = f.NewSerialized();
        f.Call(source, "Save", Activator.CreateInstance(f.Type("Timberborn.WorldPersistence", "EntitySaver"), serialized)!);
        var restored = WaterExecutor(f, 0);
        var error = Assert.Throws<TargetInvocationException>(() => f.Call(restored, "Load", f.Loader(serialized)));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    [Fact]
    public void RestoredIntentCannotPublishPickupOrApplicationBeforeManagerOwnedCancellation()
    {
        using var f = new Fixture();
        var executor = WaterExecutor(f, 7);
        Set(executor, "_restored", true);
        f.Call(executor, "TryCollectPendingWater"); // no trip can be reconstructed from saved phase
        var args = new object?[] { null, null };
        Assert.False((bool)executor.GetType().GetMethod("TryPrepareWaterApplication", Flags)!.Invoke(executor, args)!);
        Assert.Null(args[1]);
        Assert.True((bool)Get(executor, "_waterIntent")!); // observation did not mutate restore state
    }

    private static object WaterExecutor(Fixture f, int phase)
    {
        var executor = Activator.CreateInstance(f.Mod("BorrowedDutyExecutor"), null, null, Get(f.Behavior, "_resources"), null, null)!;
        Set(executor, "_manager", f.Roots.Manager);
        Set(executor, "_waterIntent", phase != 0);
        f.Call(Get(executor, "_progress")!, "Restore", phase, .25f, false);
        return executor;
    }
    private static void AttachWaterExecutor(Fixture f, object executor) =>
        typeof(FertilizerRecoveryOrderTests.Fixture).GetMethod("AttachCache", Flags)!.Invoke(f.Roots,
            [new List<object> { f.Roots.Manager, f.Behavior, f.Wait, f.Entity, executor },
                new[] { f.Type("Timberborn.BehaviorSystem", "Behavior"), f.Type("Timberborn.BehaviorSystem", "IExecutor") }]);
}
