using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class AshHarvestNativeOwnershipTests
{
    [Fact]
    public void NativeTransferKeepsWorkerBehaviorWhenWorkplaceWrapsDecision()
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        var behaviorType = mod.GetType("Wildfire.Timberborn.Ash.AshHarvestBehavior")!;
        var executorType = mod.GetType("Wildfire.Timberborn.Ash.AshHarvestExecutor")!;
        var workplaceType = mod.GetType("Wildfire.Timberborn.Ash.TimberbornFertileAshFieldWorkplaceBehavior")!;
        var behavior = RuntimeHelpers.GetUninitializedObject(behaviorType);
        var executor = RuntimeHelpers.GetUninitializedObject(executorType);
        var workplace = RuntimeHelpers.GetUninitializedObject(workplaceType);
        var decision = behaviorType.GetMethod("Own", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(behavior, new[] { executor })!;
        var decisionType = decision.GetType();
        var wrapped = decisionType.GetMethod("TransferNow")!.Invoke(null, new[] { workplace, decision })!;
        Assert.Same(behavior, decisionType.GetProperty("Behavior")!.GetValue(wrapped));
        Assert.Same(executor, decisionType.GetProperty("Executor")!.GetValue(wrapped));
        Assert.Equal(false, decisionType.GetProperty("ShouldReturnToBehavior")!.GetValue(wrapped));
        Assert.Equal(false, decisionType.GetProperty("ShouldReleaseNow")!.GetValue(wrapped));
        var baseComponent = native.LoadNative("Timberborn.BaseComponentSystem").GetType("Timberborn.BaseComponentSystem.BaseComponent")!;
        Assert.True(baseComponent.IsAssignableFrom(executorType));
    }
}
