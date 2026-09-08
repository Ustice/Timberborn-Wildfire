using System.Reflection;

namespace Wildfire.Timberborn.Tests;

public sealed class FertilizerNormalRuntimeTests
{
    [Fact]
    public void RegisteredConcreteIdleExecutorDeclinesWithoutChangingNormalRuntimeDelivery()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        // Reuse the existing actual Runtime fixture without editing its smoke/growth-owned declarations.
        Type fixtureType = typeof(NativeRuntimeIncompleteDispatchTests).GetNestedType("Fixture", BindingFlags.NonPublic)!;
        object fixture = Activator.CreateInstance(fixtureType, flags, null, [true], null)!;
        object resources = fixtureType.GetField("_resources", flags)!.GetValue(fixture)!;
        Type executorType = NativeManagedTestContext.ProxyContracts.LoadMod()
            .GetType("Wildfire.Timberborn.Fertilizer.FertilizerExecutor")!;
        object executor = Activator.CreateInstance(executorType, resources, null, null, null, null, null)!;
        resources.GetType().GetMethod("Register", flags, null, [executorType], null)!.Invoke(resources, [executor]);
        fixtureType.GetMethod("Dispatch", flags)!.Invoke(fixture, null);
        var simulator = (TimberbornIncompleteDispatchTests.Simulator)fixtureType.GetField("Simulator", flags)!.GetValue(fixture)!;
        Assert.Equal(1, simulator.Swaps);
        Assert.Equal(1L, resources.GetType().GetProperty("FieldRevision")!.GetValue(resources));
        Assert.Equal(false, resources.GetType().GetProperty("IsIndeterminate")!.GetValue(resources));
        var messages = (List<string>)fixtureType.GetField("Messages", flags)!.GetValue(fixture)!;
        Assert.Single(messages, message => message.StartsWith("wildfire_timberborn_runtime_dispatched"));
        var save = Assert.Throws<TargetInvocationException>(() => fixtureType.GetMethod("SaveAdmission", flags)!.Invoke(fixture, null));
        Assert.IsType<ArgumentNullException>(save.GetBaseException()); // Guard admitted; fixture has no real saver.
        Assert.Null(executorType.GetProperty("LastOutcome")!.GetValue(executor));
    }
}
