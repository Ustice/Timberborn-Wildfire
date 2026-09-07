using System.Reflection;
using System.Runtime.CompilerServices;
using Wildfire.Timberborn.Beavers.Emergency;

namespace Wildfire.Timberborn.Tests;

/// <summary>Exercises actual installed field metadata without constructing a native entity or invoking game behavior.</summary>
public sealed class CarryEmergencyNativeContractTests
{
    [Fact]
    public void FailedHandoffPausesForeignNativeWalkWithoutCreatingArrival()
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        var walking = native.LoadNative("Timberborn.WalkingSystem");
        var behavior = native.LoadNative("Timberborn.BehaviorSystem");
        var carrying = native.LoadNative("Timberborn.Carrying");
        var components = native.LoadNative("Timberborn.BaseComponentSystem");
        var movement = native.LoadNative("Timberborn.CharacterMovementSystem");
        object Make(System.Reflection.Assembly assembly, string name) => RuntimeHelpers.GetUninitializedObject(assembly.GetType(name)!);
        var manager = Make(behavior, "Timberborn.BehaviorSystem.BehaviorManager");
        var carry = Make(carrying, "Timberborn.Carrying.CarryRootBehavior");
        var walk = Make(walking, "Timberborn.WalkingSystem.WalkToAccessibleExecutor");
        var emergency = Make(mod, "Wildfire.Timberborn.Beavers.Emergency.WildfireCarryEmergencyExecutor");
        var walker = Make(walking, "Timberborn.WalkingSystem.Walker");
        var mover = Make(walking, "Timberborn.WalkingSystem.WalkerMover");
        var follower = Make(movement, "Timberborn.CharacterMovementSystem.PathFollower");
        var destination = Make(walking, "Timberborn.WalkingSystem.PositionDestination");
        var path = Array.CreateInstance(native.LoadNative("Timberborn.Navigation").GetType("Timberborn.Navigation.PathCorner")!, 1);
        Field(follower, "_pathCorners").SetValue(follower, path);
        Field(manager, "_runningBehavior").SetValue(manager, carry);
        Field(manager, "_runningExecutor").SetValue(manager, walk); // Native handoff already occurred.
        Field(walker, "_currentDestination").SetValue(walker, destination);
        Field(walker, "<PathFollower>k__BackingField").SetValue(walker, follower);
        var baseType = components.GetType("Timberborn.BaseComponentSystem.BaseComponent")!;
        baseType.GetField("_componentCache", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(mover,
            Make(components, "Timberborn.BaseComponentSystem.ComponentCache"));
        baseType.GetField("<Enabled>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(mover, true);
        var helper = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.Compatibility.TimberbornOwnedWalker")!, walker, mover);
        Field(emergency, "_movement").SetValue(emergency, helper);
        Field(emergency, "_manager").SetValue(emergency, manager);
        Field(emergency, "_carryBehavior").SetValue(emergency, carry);
        Field(emergency, "_session").SetValue(emergency,
            Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.Beavers.Emergency.CarryEmergencySession")!));
        var freeze = emergency.GetType().GetMethod("FreezeFailedMovement", BindingFlags.Instance | BindingFlags.NonPublic)!;
        freeze.Invoke(emergency, null);
        Assert.Equal(false, baseType.GetProperty("Enabled")!.GetValue(mover));
        Assert.Same(destination, Field(walker, "_currentDestination").GetValue(walker));
        Assert.Same(path, Field(follower, "_pathCorners").GetValue(follower));
        Assert.Same(walk, Field(manager, "_runningExecutor").GetValue(manager));
        Assert.Equal(false, walker.GetType().GetMethod("Stopped")!.Invoke(walker, null));
        // When exact emergency ownership remains, coherent native cleanup is allowed. The native
        // animation dependency is deliberately absent and throws only after destination is cleared.
        Field(manager, "_runningExecutor").SetValue(manager, emergency);
        Assert.Throws<TargetInvocationException>(() => freeze.Invoke(emergency, null));
        Assert.Null(Field(walker, "_currentDestination").GetValue(walker));
        static FieldInfo Field(object owner, string name) => owner.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic)!;
    }

    [Fact]
    public void ReviewedNativeFieldsSwapByIdentityAndRejectForeignOwner()
    {
        using var native = new NativeManagedTestContext();
        string managed = native.ManagedPath;
        var mod = native.LoadFromAssemblyPath(typeof(CarryEmergencyBuild).Assembly.Location);
        var accessType = mod.GetType("Wildfire.Timberborn.Beavers.Emergency.CarryEmergencyBehaviorAccess")!;
        var access = accessType.GetMethod("CreateVerified")!.Invoke(null, null)!;
        var behaviorAssembly = native.LoadFromAssemblyPath(Path.Combine(managed, "Timberborn.BehaviorSystem.dll"));
        var managerType = behaviorAssembly.GetType("Timberborn.BehaviorSystem.BehaviorManager")!;
        var carrying = native.LoadFromAssemblyPath(Path.Combine(managed, "Timberborn.Carrying.dll"));
        var walking = native.LoadFromAssemblyPath(Path.Combine(managed, "Timberborn.WalkingSystem.dll"));
        var manager = RuntimeHelpers.GetUninitializedObject(managerType);
        var carry = RuntimeHelpers.GetUninitializedObject(carrying.GetType("Timberborn.Carrying.CarryRootBehavior")!);
        var walk = RuntimeHelpers.GetUninitializedObject(walking.GetType("Timberborn.WalkingSystem.WalkToAccessibleExecutor")!);
        var emergency = RuntimeHelpers.GetUninitializedObject(mod.GetType("Wildfire.Timberborn.Beavers.Emergency.WildfireCarryEmergencyExecutor")!);
        var executor = managerType.GetField("_runningExecutor", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var elapsed = managerType.GetField("_runningExecutorElapsedTime", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var returns = managerType.GetField("_returnToBehavior", BindingFlags.Instance | BindingFlags.NonPublic)!;
        managerType.GetField("_runningBehavior", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, carry);
        executor.SetValue(manager, walk);
        elapsed.SetValue(manager, 20f);
        returns.SetValue(manager, true);
        Assert.Equal(true, accessType.GetMethod("IsNativeDelivery")!.Invoke(access, new[] { manager, carry, walk }));
        executor.SetValue(manager, null); // ProcessBehaviors is deciding a new native carrying launch.
        Assert.Equal(false, accessType.GetMethod("IsNativeDelivery")!.Invoke(access, new[] { manager, carry, walk }));
        executor.SetValue(manager, walk);
        var replace = accessType.GetMethod("Replace")!;
        replace.Invoke(access, new[] { manager, carry, walk, emergency, false });
        Assert.Same(emergency, executor.GetValue(manager));
        Assert.Equal(0f, elapsed.GetValue(manager));
        Assert.Equal(false, returns.GetValue(manager));
        // The wrong expected owner must fail before any field is changed.
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() =>
            replace.Invoke(access, new[] { manager, carry, walk, emergency, true })).InnerException);
        Assert.Same(emergency, executor.GetValue(manager));
        Assert.Equal(false, returns.GetValue(manager));
        var foreignCarry = RuntimeHelpers.GetUninitializedObject(carry.GetType());
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() =>
            replace.Invoke(access, new[] { manager, foreignCarry, emergency, walk, true })).InnerException);
        Assert.Same(emergency, executor.GetValue(manager));
        replace.Invoke(access, new[] { manager, carry, emergency, walk, true });
        Assert.Same(walk, executor.GetValue(manager));
        Assert.Equal(true, returns.GetValue(manager));

        // Read the real native progress fields without invoking movement or Unity engine behavior.
        var movement = native.LoadFromAssemblyPath(Path.Combine(managed, "Timberborn.CharacterMovementSystem.dll"));
        var followerType = movement.GetType("Timberborn.CharacterMovementSystem.PathFollower")!;
        var follower = RuntimeHelpers.GetUninitializedObject(followerType);
        var navigation = native.LoadFromAssemblyPath(Path.Combine(managed, "Timberborn.Navigation.dll"));
        var cornerType = navigation.GetType("Timberborn.Navigation.PathCorner")!;
        var cornerConstructor = cornerType.GetConstructors().Single();
        var vector = Activator.CreateInstance(cornerConstructor.GetParameters()[0].ParameterType)!;
        var path = Array.CreateInstance(cornerType, 3);
        for (int index = 0; index < path.Length; index++)
            path.SetValue(cornerConstructor.Invoke(new[] { vector, 1f, (index + 1) * 10 }), index);
        followerType.GetField("_pathCorners", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(follower, path);
        var next = followerType.GetField("_nextCornerIndex", BindingFlags.Instance | BindingFlags.NonPublic)!;
        next.SetValue(follower, 2);
        var readPath = accessType.GetMethod("ReadRemainingPath")!;
        var remaining = (Array)readPath.Invoke(access, new[] { follower })!;
        Assert.Single(remaining.Cast<object>());
        Assert.Equal(30, cornerType.GetProperty("GroupId")!.GetValue(remaining.GetValue(0)));
        next.SetValue(follower, 0);
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => readPath.Invoke(access, new[] { follower })).InnerException);

        var executorType = behaviorAssembly.GetType("Timberborn.BehaviorSystem.IExecutor")!;
        var duplicates = Array.CreateInstance(executorType, 2);
        duplicates.SetValue(emergency, 0);
        duplicates.SetValue(RuntimeHelpers.GetUninitializedObject(emergency.GetType()), 1);
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() =>
            accessType.GetMethod("VerifyExecutorIdentity")!.Invoke(access, new object[] { duplicates, emergency })).InnerException);
    }

}
