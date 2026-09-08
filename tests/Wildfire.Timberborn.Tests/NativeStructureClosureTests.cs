using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

// Installed native managed methods only. No GameObject, fabricated Unity liveness or runtime policy activation.
public sealed class NativeStructureClosureTests
{
    [Fact]
    public void OwnedBlockerIsIdempotentAndCannotRemoveAnotherOwnersClosure()
    {
        using var f = new Fixture();
        var fire = new object();
        var other = new object();
        int blocked = 0, unblocked = 0;
        f.BlockType.GetEvent("ObjectBlocked")!.AddEventHandler(f.Blockable, (EventHandler)((_, _) => blocked++));
        f.BlockType.GetEvent("ObjectUnblocked")!.AddEventHandler(f.Blockable, (EventHandler)((_, _) => unblocked++));
        f.Block(fire); f.Block(fire); f.Block(other);
        Assert.False(f.Unblocked);
        Assert.Equal(1, blocked);
        f.Unblock(fire); f.Unblock(fire); f.Unblock(new object());
        Assert.False(f.Unblocked);
        Assert.Equal(0, unblocked);
        f.Unblock(other); f.Unblock(other);
        Assert.True(f.Unblocked);
        Assert.Equal(1, unblocked);
    }

    [Fact]
    public void ActualNativePlayerPauseAndFireBlockerRemainIndependentInEitherReleaseOrder()
    {
        using var f = new Fixture();
        var type = f.Native.LoadNative("Timberborn.Buildings").GetType("Timberborn.Buildings.PausableBuilding")!;
        var pause = RuntimeHelpers.GetUninitializedObject(type);
        Set(pause, "_blockableObject", f.Blockable);
        var status = f.Native.LoadNative("Timberborn.StatusSystem").GetType("Timberborn.StatusSystem.StatusToggle")!;
        Set(pause, "_pauseStatusToggle", RuntimeHelpers.GetUninitializedObject(status));
        var fire = new object();
        type.GetMethod("Pause")!.Invoke(pause, null);
        f.Block(fire); f.Unblock(fire);
        Assert.Equal(true, type.GetProperty("Paused")!.GetValue(pause));
        Assert.False(f.Unblocked);
        f.Block(fire);
        type.GetMethod("Resume")!.Invoke(pause, null);
        Assert.Equal(false, type.GetProperty("Paused")!.GetValue(pause));
        Assert.False(f.Unblocked);
        f.Unblock(fire);
        Assert.True(f.Unblocked);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeCallbackFailureAfterBlockerMutationRequiresSharedGuardFailStop(bool unblocking)
    {
        using var f = new Fixture();
        var owner = new object();
        if (unblocking) f.Block(owner);
        var cause = new ApplicationException("native closure observer failed");
        int callbacks = 0;
        f.BlockType.GetEvent(unblocking ? "ObjectUnblocked" : "ObjectBlocked")!.AddEventHandler(
            f.Blockable, (EventHandler)((_, _) => { callbacks++; throw cause; }));
        var guard = new NativeResourceTransaction();
        Action mutation = () => { if (unblocking) f.Unblock(owner); else f.Block(owner); };
        var error = Assert.Throws<TargetInvocationException>(() => guard.TransferInventory(mutation));
        Assert.Same(cause, error.InnerException);
        Assert.Equal(unblocking, f.Unblocked); // Mutation preceded the native event failure.
        Assert.True(guard.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(() => guard.TransferInventory(mutation));
        Assert.Throws<InvalidOperationException>(guard.ThrowIfSaveUnsafe);
        Assert.Equal(1, callbacks);
    }

    [Fact]
    public void NativeWorkplaceBlockedHandlerActuallyUnemploysAssignedWorkers()
    {
        using var f = new Fixture();
        var assembly = f.Native.LoadNative("Timberborn.WorkSystem");
        var workplaceType = assembly.GetType("Timberborn.WorkSystem.Workplace")!;
        var workerType = assembly.GetType("Timberborn.WorkSystem.Worker")!;
        var workplace = RuntimeHelpers.GetUninitializedObject(workplaceType);
        var workers = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(workerType))!;
        var worker = RuntimeHelpers.GetUninitializedObject(workerType);
        Set(worker, "<Workplace>k__BackingField", workplace);
        workers.Add(worker);
        Set(workplace, "_assignedWorkers", workers);
        var callback = workplaceType.GetMethod("OnObjectBlocked", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(EventHandler), workplace);
        f.BlockType.GetEvent("ObjectBlocked")!.AddEventHandler(f.Blockable, callback);
        int gotUnemployed = 0;
        workerType.GetEvent("GotUnemployed")!.AddEventHandler(worker,
            (EventHandler<EventArgs>)((_, _) => gotUnemployed++));
        f.Block(new object());
        Assert.Empty(workers);
        Assert.Null(workerType.GetProperty("Workplace")!.GetValue(worker));
        Assert.Equal(1, gotUnemployed);
        // The native closure mechanism intentionally changes employment. It does not preserve a donor job.
    }

    [Fact]
    public void NativeConstructionIsOnRequiresAllOwnersToReleaseClosure()
    {
        using var f = new Fixture();
        var assembly = f.Native.LoadNative("Timberborn.ConstructionSites");
        var type = assembly.GetType("Timberborn.ConstructionSites.ConstructionSite")!;
        var site = RuntimeHelpers.GetUninitializedObject(type);
        Set(site, "_blockableObject", f.Blockable);
        Set(site, "_constructionSiteValidators", Activator.CreateInstance(typeof(List<>).MakeGenericType(
            assembly.GetType("Timberborn.ConstructionSites.IConstructionSiteValidator")!))!);
        bool IsOn() => (bool)type.GetProperty("IsOn")!.GetValue(site)!;
        Assert.True(IsOn());
        var fire = new object(); var user = new object();
        f.Block(fire); f.Block(user);
        Assert.False(IsOn());
        f.Unblock(fire);
        Assert.False(IsOn());
        f.Unblock(user);
        Assert.True(IsOn());
    }

    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    private sealed class Fixture : IDisposable
    {
        internal readonly NativeManagedTestContext Native = new();
        internal readonly Type BlockType;
        internal readonly object Blockable;
        internal Fixture()
        {
            BlockType = Native.LoadNative("Timberborn.BlockingSystem").GetType("Timberborn.BlockingSystem.BlockableObject")!;
            Blockable = Activator.CreateInstance(BlockType)!;
        }
        internal bool Unblocked => (bool)BlockType.GetProperty("IsUnblocked")!.GetValue(Blockable)!;
        internal void Block(object owner) => BlockType.GetMethod("Block")!.Invoke(Blockable, [owner]);
        internal void Unblock(object owner) => BlockType.GetMethod("Unblock")!.Invoke(Blockable, [owner]);
        public void Dispose() => Native.Dispose();
    }
}
