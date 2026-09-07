using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class TimberbornOwnedWalkerTests
{
    [Fact]
    public void NativeStopClearsDestinationBeforePathEvenWhenAnimationStopFails()
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        var walkerAssembly = native.LoadNative("Timberborn.WalkingSystem");
        var movementAssembly = native.LoadNative("Timberborn.CharacterMovementSystem");
        var componentAssembly = native.LoadNative("Timberborn.BaseComponentSystem");
        var walkerType = walkerAssembly.GetType("Timberborn.WalkingSystem.Walker")!;
        var moverType = walkerAssembly.GetType("Timberborn.WalkingSystem.WalkerMover")!;
        var followerType = movementAssembly.GetType("Timberborn.CharacterMovementSystem.PathFollower")!;
        object walker = RuntimeHelpers.GetUninitializedObject(walkerType);
        object mover = RuntimeHelpers.GetUninitializedObject(moverType);
        object follower = RuntimeHelpers.GetUninitializedObject(followerType);
        var baseType = componentAssembly.GetType("Timberborn.BaseComponentSystem.BaseComponent")!;
        // The cache only touches frame-updatable interfaces; WalkerMover implements neither.
        var cache = RuntimeHelpers.GetUninitializedObject(componentAssembly.GetType("Timberborn.BaseComponentSystem.ComponentCache")!);
        Field(baseType, "_componentCache").SetValue(mover, cache);
        Field(baseType, "<Enabled>k__BackingField").SetValue(mover, true);
        Field(walkerType, "<PathFollower>k__BackingField").SetValue(walker, follower);
        Field(walkerType, "_currentDestination").SetValue(walker,
            RuntimeHelpers.GetUninitializedObject(walkerAssembly.GetType("Timberborn.WalkingSystem.PositionDestination")!));
        Field(walkerType, "_stopNextTick").SetValue(walker, true);
        var helperType = mod.GetType("Wildfire.Timberborn.Compatibility.TimberbornOwnedWalker")!;
        var helper = Activator.CreateInstance(helperType, walker, mover)!;
        helperType.GetMethod("RejectRoute")!.Invoke(helper, null);
        Assert.Equal(false, baseType.GetProperty("Enabled")!.GetValue(mover));
        var tickAssembly = native.LoadNative("Timberborn.TickSystem");
        var meteredType = tickAssembly.GetType("Timberborn.TickSystem.MeteredTickableComponent")!;
        var metered = RuntimeHelpers.GetUninitializedObject(meteredType);
        Field(meteredType, "_tickableComponent").SetValue(metered, mover);
        Assert.Equal(false, meteredType.GetProperty("Enabled")!.GetValue(metered));
        Assert.True(tickAssembly.GetType("Timberborn.TickSystem.ILateTickable")!.IsAssignableFrom(moverType));
        // Actual native stop is invoked. Deliberately missing MovementAnimator makes it fail AFTER
        // its pure managed destination/path cleanup; no Unity engine or entity behavior is invoked.
        Assert.Throws<TargetInvocationException>(() => helperType.GetMethod("Stop")!.Invoke(helper, null));
        Assert.Null(Field(walkerType, "_currentDestination").GetValue(walker));
        Assert.Null(Field(followerType, "_pathCorners").GetValue(follower));
        Assert.Equal(false, Field(walkerType, "_stopNextTick").GetValue(walker));
        Assert.Equal(false, baseType.GetProperty("Enabled")!.GetValue(mover));
        // Stopped Walker.Tick returns before touching Enterer/path fields (both absent in this fixture).
        walkerType.GetMethod("Tick")!.Invoke(walker, null);
        helperType.GetMethod("ReleasePause")!.Invoke(helper, null);
        Assert.Equal(true, baseType.GetProperty("Enabled")!.GetValue(mover));
    }
    private static FieldInfo Field(Type type, string name) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
