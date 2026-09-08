using System.Reflection;
using Wildfire.Core;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

public sealed partial class NativeWaterOwnershipTests
{
    [Fact]
    public void CollectionRunsAfterNativeCreditAndOutsideItsGuardBeforeNextSingleton()
    {
        using var f = new F(); f.Load(); f.Track();
        var tests = NativeManagedTestContext.ProxyContracts.LoadFromAssemblyPath(typeof(NativeWaterOwnershipTests).Assembly.Location);
        var collector = Activator.CreateInstance(tests.GetType(typeof(CollectionObserver).FullName!)!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [(Action)(() =>
            {
                Assert.Equal(.2f, f.Water.Buffer(f.Water.Source));
                f.Call(f.Coordinator, "ThrowIfSaveUnsafe");
                f.Call(f.Coordinator, "TransferInventory", (Action)(() =>
                {
                    Assert.Throws<TargetInvocationException>(() => f.Call(f.Coordinator, "ThrowIfSaveUnsafe"));
                    f.Events.Add("collect");
                }));
            })], null)!;
        f.Call(f.Coordinator, "Register", collector);
        f.Water.SupplyCompletedNativeReceipt(.2f); f.Tick();
        Assert.Equal(new[] { "resume", "collect", "pause", "resume", "later", "pause" }, f.Events);
        f.Call(f.Coordinator, "ThrowIfSaveUnsafe");
    }
    [Fact]
    public void FillRequiresSameCoordinatorAndSettledOwningThreadWithoutTaintingOnRead()
    {
        using var f = new F(); f.Load(); f.Track();
        Assert.True((bool)f.Call(f.Boundary, "CanFill", f.Coordinator)!);
        var other = Activator.CreateInstance(f.Coordinator.GetType())!;
        Assert.False((bool)f.Call(f.Boundary, "CanFill", other)!);
        NativeShorelineWaterFixture.Set(f.Scheduler, "<ParalleTicklIsFinished>k__BackingField", false);
        Assert.False((bool)f.Call(f.Boundary, "CanFill", f.Coordinator)!);
        NativeShorelineWaterFixture.Set(f.Scheduler, "<ParalleTicklIsFinished>k__BackingField", true);
        bool admittedOnOtherThread = true;
        var thread = new Thread(() => admittedOnOtherThread = (bool)f.Call(f.Boundary, "CanFill", f.Coordinator)!);
        thread.Start(); thread.Join();
        Assert.False(admittedOnOtherThread);
        Assert.False((bool)f.Property(f.Source, "Tainted")!);
        Assert.True((bool)f.Call(f.Boundary, "CanFill", f.Coordinator)!);
        f.Call(f.Coordinator, "ThrowIfSaveUnsafe");
    }
    private sealed class CollectionObserver(Action collect) : INativeWaterApplicationProducer, INativeNaturalWaterCollector
    {
        public void TryCollectPendingWater() => collect();
        public bool TryPrepareApplication(out FireSimChange input, out Action commit) => throw new InvalidOperationException("Collection must not apply GPU water.");
    }
}
