using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeResourcePoisonMovementTests
{
    [Theory]
    [InlineData("Warden", true, true)]
    [InlineData("AshHarvest", true, true)]
    [InlineData("Warden", false, true)]
    [InlineData("AshHarvest", false, true)]
    [InlineData("Warden", true, false)]
    [InlineData("AshHarvest", true, false)]
    public void PoisonPausesOnlyAnActiveOwnedMoverWithoutStoppingOrMutatingResources(string kind, bool active, bool owned)
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        var walkerAssembly = native.LoadNative("Timberborn.WalkingSystem");
        var walkerType = walkerAssembly.GetType("Timberborn.WalkingSystem.Walker")!;
        var moverType = walkerAssembly.GetType("Timberborn.WalkingSystem.WalkerMover")!;
        var walker = RuntimeHelpers.GetUninitializedObject(walkerType);
        var mover = RuntimeHelpers.GetUninitializedObject(moverType);
        var baseAssembly = native.LoadNative("Timberborn.BaseComponentSystem");
        var baseType = baseAssembly.GetType("Timberborn.BaseComponentSystem.BaseComponent")!;
        Field(baseType, "_componentCache").SetValue(mover,
            RuntimeHelpers.GetUninitializedObject(baseAssembly.GetType("Timberborn.BaseComponentSystem.ComponentCache")!));
        Field(baseType, "<Enabled>k__BackingField").SetValue(mover, true);
        var destination = RuntimeHelpers.GetUninitializedObject(walkerAssembly.GetType("Timberborn.WalkingSystem.PositionDestination")!);
        Field(walkerType, "_currentDestination").SetValue(walker, destination);
        var helper = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.Compatibility.TimberbornOwnedWalker")!, walker, mover)!;
        var resourcesType = mod.GetType("Wildfire.Timberborn.Resources.NativeResourceCoordinator")!;
        var resources = Activator.CreateInstance(resourcesType)!;
        Assert.Throws<TargetInvocationException>(() => resourcesType.GetMethod("TransferInventory")!.Invoke(resources,
            new object[] { (Action)(() => throw new InvalidOperationException("uncertain native mutation")) }));
        bool warden = kind == "Warden";
        var executorType = mod.GetType($"Wildfire.Timberborn.{(warden ? "FireResponse" : "Ash")}.{kind}Executor")!;
        var executor = Activator.CreateInstance(executorType, new object?[5])!;
        Field(executorType, warden ? "_delivery" : "_resources").SetValue(executor, resources);
        Field(executorType, warden ? "_movement" : "_ownedWalk").SetValue(executor, helper);
        var mortalType = native.LoadNative("Timberborn.MortalSystem").GetType("Timberborn.MortalSystem.Mortal")!;
        var mortal = RuntimeHelpers.GetUninitializedObject(mortalType);
        var characterType = native.LoadNative("Timberborn.Characters").GetType("Timberborn.Characters.Character")!;
        var character = RuntimeHelpers.GetUninitializedObject(characterType);
        Field(characterType, "<Alive>k__BackingField").SetValue(character, true);
        Field(mortalType, "_character").SetValue(mortal, character);
        Field(executorType, "_mortal").SetValue(executor, mortal);
        var managerType = native.LoadNative("Timberborn.BehaviorSystem").GetType("Timberborn.BehaviorSystem.BehaviorManager")!;
        var manager = RuntimeHelpers.GetUninitializedObject(managerType);
        if (owned) Field(managerType, "_runningExecutor").SetValue(manager, executor);
        Field(executorType, warden ? "_behaviorManager" : "_behavior").SetValue(executor, manager);
        var cycle = Field(executorType, warden ? "_sortie" : "_cycle").GetValue(executor)!;
        if (active) cycle.GetType().GetMethod("Begin")!.Invoke(cycle, warden ? new object[] { true } : null);
        object? Tick() => executorType.GetMethod("Tick")!.Invoke(executor, new object[] { .01f });
        if (active && !owned) Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => Tick()).InnerException);
        else
        {
            Assert.Equal(active ? "Running" : "Success", Tick()!.ToString());
            Assert.Equal(active ? "Running" : "Success", Tick()!.ToString());
        }
        Assert.Equal(!(active && owned), baseType.GetProperty("Enabled")!.GetValue(mover));
        Assert.Same(destination, Field(walkerType, "_currentDestination").GetValue(walker));
        Assert.Equal(true, resourcesType.GetProperty("IsIndeterminate")!.GetValue(resources));
        Assert.Throws<TargetInvocationException>(() => resourcesType.GetMethod("ThrowIfSaveUnsafe")!.Invoke(resources, null));
    }
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
