using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class BorrowedDutyQaNativeTests
{
    [Fact]
    public void ExactNativeRegistryLookupRejectsMissingAndDeletedDonor()
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        var assembly = native.LoadNative("Timberborn.EntitySystem");
        var registryType = assembly.GetType("Timberborn.EntitySystem.EntityRegistry")!;
        var registry = Activator.CreateInstance(registryType)!;
        var resolver = mod.GetType("Wildfire.Timberborn.FireBell.BorrowedDutyQaApi")!.GetMethod("ResolveDonor", BindingFlags.Static | BindingFlags.NonPublic)!;
        var id = Guid.NewGuid();
        Assert.IsType<ArgumentException>(Assert.Throws<TargetInvocationException>(() => resolver.Invoke(null, new[] { registry, (object)id })).InnerException);
        var entityType = assembly.GetType("Timberborn.EntitySystem.EntityComponent")!;
        var entity = RuntimeHelpers.GetUninitializedObject(entityType);
        var state = entityType.GetField("_entityState", BindingFlags.Instance | BindingFlags.NonPublic)!;
        state.SetValue(entity, Enum.Parse(state.FieldType, "Deleted"));
        var entries = (IDictionary)registryType.GetField("_entities", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(registry)!;
        entries.Add(id, entity);
        Assert.IsType<ArgumentException>(Assert.Throws<TargetInvocationException>(() => resolver.Invoke(null, new[] { registry, (object)id })).InnerException);
        // Installed GetEntity and Deleted execute, with no simulated Unity liveness or scene object.
    }

    [Fact]
    public void AdmissionSwitchIsRequiredEvenWithNativeQaAdapterPresent()
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        var fixture = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.FireBell.BorrowedDutyFixture")!)!;
        var type = mod.GetType("Wildfire.Timberborn.FireBell.BorrowedDutyQaApi")!;
        var api = Activator.CreateInstance(type, new[] { fixture, null, null, null, null })!;
        var request = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.Qa.BorrowedDutyQaArmRequest")!, Guid.NewGuid(), 1f, 2f, 3f)!;
        var failure = Assert.Throws<TargetInvocationException>(() => type.GetMethod("Arm")!.Invoke(api, new[] { request }));
        Assert.Contains("--wildfire-enable-borrowed-duty", failure.InnerException!.Message);
        var status = type.GetMethod("Status")!.Invoke(api, null)!;
        Assert.Empty((IEnumerable)status.GetType().GetProperty("Actors")!.GetValue(status)!);
    }

    [Fact]
    public void SavedActivePhaseAndActualNativeOwnershipAreReportedSeparatelyWithoutOptIn()
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        var fixtureType = mod.GetType("Wildfire.Timberborn.FireBell.BorrowedDutyFixture")!;
        var fixture = Activator.CreateInstance(fixtureType)!;
        var executorType = mod.GetType("Wildfire.Timberborn.FireBell.BorrowedDutyExecutor")!;
        var executor = Activator.CreateInstance(executorType, new object?[5])!;
        var progress = Field(executorType, "_progress").GetValue(executor)!;
        progress.GetType().GetMethod("Restore")!.Invoke(progress, new object[] { 3, .5f, false });
        var id = Guid.NewGuid();
        var entityType = native.LoadNative("Timberborn.EntitySystem").GetType("Timberborn.EntitySystem.EntityComponent")!;
        var entity = RuntimeHelpers.GetUninitializedObject(entityType);
        Field(entityType, "<EntityId>k__BackingField").SetValue(entity, id);
        Field(executorType, "_entity").SetValue(executor, entity);
        fixtureType.GetMethod("Register", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(fixture, new[] { executor });
        string Detail() { var status = fixtureType.GetMethod("CaptureQaStatus")!.Invoke(fixture, null)!; return (string)status.GetType().GetProperty("Detail")!.GetValue(status)!; }
        Assert.Contains("active_count=1 running_count=0", Detail());
        var managerType = native.LoadNative("Timberborn.BehaviorSystem").GetType("Timberborn.BehaviorSystem.BehaviorManager")!;
        var manager = RuntimeHelpers.GetUninitializedObject(managerType);
        Field(managerType, "_runningExecutor").SetValue(manager, executor);
        Field(executorType, "_manager").SetValue(executor, manager);
        Assert.Contains("active_count=1 running_count=1", Detail());
        Assert.Contains(id.ToString("D") + ":Returning:cancel_false:owned_true", Detail());
        fixtureType.GetMethod("Cancel")!.Invoke(fixture, null);
        Assert.Contains("cancel_true:owned_true", Detail());
        Assert.Contains("admissions_enabled=false", Detail());
        var restoredId = Guid.NewGuid();
        Field(entityType, "<EntityId>k__BackingField").SetValue(entity, restoredId);
        Assert.Contains(restoredId.ToString("D") + ":Returning", Detail()); // Read current native identity, not Awake timing.
    }
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
