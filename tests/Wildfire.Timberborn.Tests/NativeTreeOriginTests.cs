using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeTreeOriginTests
{
    [Fact]
    public void NativeTreeActuatorRechecksExactRegistryAndSkipsMissingDeletedOrNotInitialized()
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        var assembly = native.LoadNative("Timberborn.EntitySystem");
        var registryType = assembly.GetType("Timberborn.EntitySystem.EntityRegistry")!;
        var registry = Activator.CreateInstance(registryType)!;
        var entityType = assembly.GetType("Timberborn.EntitySystem.EntityComponent")!;
        var apiType = mod.GetType("Wildfire.Timberborn.Visuals.TimberbornTextureTreeBurnConsequenceApi")!;
        var api = RuntimeHelpers.GetUninitializedObject(apiType);
        Field(apiType, "_entities").SetValue(api, registry);
        var entries = (IDictionary)Field(registryType, "_entities").GetValue(registry)!;
        var id = Guid.NewGuid();
        bool IsLive() => (bool)apiType.GetMethod("IsLive")!.Invoke(api, [id])!;
        Assert.False(IsLive());
        var entity = RuntimeHelpers.GetUninitializedObject(entityType);
        Field(entityType, "<EntityId>k__BackingField").SetValue(entity, id);
        entries.Add(id, entity); // Added after actuator construction: lookup is not an initial snapshot.
        Assert.Same(entity, registryType.GetMethod("GetEntity")!.Invoke(registry, [id]));
        Assert.False(IsLive()); // Native not-initialized state, before any Unity liveness call.
        var state = Field(entityType, "_entityState");
        state.SetValue(entity, Enum.Parse(state.FieldType, "Deleted"));
        Assert.False(IsLive());
        var consequenceType = mod.GetType("Wildfire.Timberborn.Consequences.TimberbornTreeBurnConsequence")!;
        var keyType = mod.GetType("Wildfire.Timberborn.Consequences.TimberbornBurnDamageTargetKey")!;
        var kindType = mod.GetType("Wildfire.Timberborn.Consequences.TimberbornTreeBurnConsequenceKind")!;
        var key = Activator.CreateInstance(keyType, $"tree_cuttable:entity:{id:D}")!;
        foreach (object kind in Enum.GetValues(kindType))
        {
            var consequence = Activator.CreateInstance(consequenceType,
                key, "Pine", kind, "Log", 1, 0, 1u, 0, 1, 1, 10, id)!;
            var result = apiType.GetMethod("ApplyConsequence")!.Invoke(api, [consequence])!;
            Assert.Equal(false, result.GetType().GetProperty("Applied")!.GetValue(result));
            Assert.Equal(false, result.GetType().GetProperty("Failed")!.GetValue(result));
        }
        entries.Remove(id);
        Assert.False(IsLive());
        // Executes installed EntityRegistry.GetEntity and EntityComponent state getters. A positive
        // live tree/component/visual call requires Unity and remains native game QA, not fake liveness.
    }

    [Fact]
    public void HashOrMismatchedGuidConsequenceCannotReachNativeMutation()
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        var apiType = mod.GetType("Wildfire.Timberborn.Visuals.TimberbornTextureTreeBurnConsequenceApi")!;
        var api = RuntimeHelpers.GetUninitializedObject(apiType); // No registry: identity must fail first.
        var consequenceType = mod.GetType("Wildfire.Timberborn.Consequences.TimberbornTreeBurnConsequence")!;
        var keyType = mod.GetType("Wildfire.Timberborn.Consequences.TimberbornBurnDamageTargetKey")!;
        var kindType = mod.GetType("Wildfire.Timberborn.Consequences.TimberbornTreeBurnConsequenceKind")!;
        foreach (string key in new[] { "tree_cuttable:12345", $"tree_cuttable:entity:{Guid.NewGuid():D}" })
        {
            var consequence = Activator.CreateInstance(consequenceType, Activator.CreateInstance(keyType, key),
                "Pine", Enum.Parse(kindType, "KillTree"), "Log", 1, 0, 1u, 0, 1, 1, 10, Guid.NewGuid())!;
            var error = Assert.Throws<TargetInvocationException>(() => apiType.GetMethod("ApplyConsequence")!.Invoke(api, [consequence]));
            Assert.IsType<InvalidOperationException>(error.InnerException);
            Assert.Contains("exact Guid family binding", error.InnerException!.Message);
        }
    }
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
