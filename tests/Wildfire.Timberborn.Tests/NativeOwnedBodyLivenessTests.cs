using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeOwnedBodyLivenessTests
{
    [Fact]
    public void BodyProbeUsesCurrentExactRegistryAndRejectsMissingOrInactiveNativeEntity()
    {
        using var native = new NativeManagedTestContext();
        var assembly = native.LoadNative("Timberborn.EntitySystem");
        var registryType = assembly.GetType("Timberborn.EntitySystem.EntityRegistry")!;
        var entityType = assembly.GetType("Timberborn.EntitySystem.EntityComponent")!;
        var registry = Activator.CreateInstance(registryType)!;
        var entries = (IDictionary)Field(registryType, "_entities").GetValue(registry)!;
        var probeType = native.LoadMod().GetType("Wildfire.Timberborn.Consequences.TimberbornOwnedBodyLiveness")!;
        var probe = Activator.CreateInstance(probeType, registry)!;
        var id = Guid.NewGuid();
        bool IsLive(Guid target) => (bool)probeType.GetMethod("IsLive")!.Invoke(probe, [target])!;
        string Presence() => probeType.GetMethod("ObservePresence")!.Invoke(probe, [id])!.ToString()!;
        Assert.False(IsLive(id)); Assert.Equal("Absent", Presence());
        var entity = RuntimeHelpers.GetUninitializedObject(entityType);
        Field(entityType, "<EntityId>k__BackingField").SetValue(entity, id);
        entries.Add(id, entity);
        Assert.Equal("Uninitialized", Presence());
        Assert.False(IsLive(id)); // Real native uninitialized state exits before Unity liveness.
        var state = Field(entityType, "_entityState");
        state.SetValue(entity, Enum.Parse(state.FieldType, "Deleted"));
        Assert.False(IsLive(id));
        Assert.Equal("Deleted", Presence());
        Field(entityType, "<EntityId>k__BackingField").SetValue(entity, Guid.NewGuid());
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => Presence()).InnerException);
        entries.Remove(id);
        Assert.False(IsLive(id));
        var error = Assert.Throws<TargetInvocationException>(() => IsLive(Guid.Empty));
        Assert.IsType<ArgumentException>(error.InnerException);
        // Positive initialized GameObject liveness remains controller-owned Unity/game QA.
    }

    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
