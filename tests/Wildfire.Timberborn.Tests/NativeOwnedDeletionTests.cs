using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeOwnedDeletionTests
{
    [Fact]
    public void NativeDeletionMarksDeletedBeforeComponentsAndEventButOnlyRegistryRemovalProvesAbsence()
    {
        var f = new Fixture();
        f.Deleting = () => Assert.Equal("Deleted", f.Presence());
        f.DeletedEvent = () => Assert.Equal("Deleted", f.Presence());
        f.DeleteComponent();
        Assert.Equal(new[] { "component", "event" }, f.Calls);
        Assert.Equal("Deleted", f.Presence());
        f.RemoveRegistryEntry();
        Assert.Equal("Absent", f.Presence());
        // EntityService performs these native calls in this order, then Unity Destroy (not executed here).
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualNativeCallbackFailureLeavesDeletedEntryAndDoesNotAuthorizeRetirement(bool failEvent)
    {
        var f = new Fixture(); var cause = new InvalidOperationException("native deletion callback failed");
        if (failEvent) f.DeletedEvent = () => throw cause; else f.Deleting = () => throw cause;
        var thrown = Assert.Throws<TargetInvocationException>(f.DeleteComponent);
        Assert.Same(cause, thrown.InnerException);
        Assert.Equal("Deleted", f.Presence());
        Assert.Equal(failEvent ? new[] { "component", "event" } : new[] { "component" }, f.Calls);
    }

    private sealed class Fixture
    {
        private static readonly NativeManagedTestContext Native = NativeManagedTestContext.ProxyContracts;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly object _entity, _registry, _probe;
        internal readonly List<string> Calls = new();
        internal Action? Deleting, DeletedEvent;
        private readonly Guid _id = Guid.NewGuid();
        internal Fixture()
        {
            var entityType = T("Timberborn.EntitySystem", "EntityComponent");
            _entity = RuntimeHelpers.GetUninitializedObject(entityType);
            Set(_entity, "<EntityId>k__BackingField", _id);
            var state = entityType.GetField("_entityState", Flags)!;
            state.SetValue(_entity, Enum.Parse(state.FieldType, "Initialized"));
            _registry = Activator.CreateInstance(T("Timberborn.EntitySystem", "EntityRegistry"))!;
            ((IDictionary)Get(_registry, "_entities")).Add(_id, _entity);
            ((IList)Get(_registry, "_entitiesInInstantiationOrder")).Add(_entity);
            var deletionInterface = T("Timberborn.EntitySystem", "IDeletableEntity");
            var callback = NativePersistenceProxy.Create(deletionInterface, (_, _) =>
            { Calls.Add("component"); Deleting?.Invoke(); return null; });
            var cache = RuntimeHelpers.GetUninitializedObject(T("Timberborn.BaseComponentSystem", "ComponentCache"));
            Set(cache, "_components", new List<object> { _entity, callback });
            var index = Activator.CreateInstance(T("Timberborn.BaseComponentSystem", "TypeIndexMap"))!;
            ((IDictionary)Get(index, "_typeIndex")).Add(deletionInterface, 1);
            Set(cache, "_typeIndexMap", index);
            T("Timberborn.BaseComponentSystem", "BaseComponent").GetField("_componentCache", Flags)!.SetValue(_entity, cache);
            Set(_entity, "<RegisteredComponents>k__BackingField", Activator.CreateInstance(typeof(List<>).MakeGenericType(T("Timberborn.EntitySystem", "IRegisteredComponent")))!);
            Set(_entity, "_entityComponentRegistry", RuntimeHelpers.GetUninitializedObject(T("Timberborn.EntitySystem", "EntityComponentRegistry")));
            var bus = Activator.CreateInstance(T("Timberborn.SingletonSystem", "EventBus"))!;
            Set(bus, "_ready", true);
            var subscriptions = Get(bus, "_subscriptions");
            subscriptions.GetType().GetMethod("Add")!.Invoke(subscriptions,
                new object[] { T("Timberborn.EntitySystem", "EntityDeletedEvent"), this,
                    (Action<object>)(_ => { Calls.Add("event"); DeletedEvent?.Invoke(); }) });
            Set(_entity, "_eventBus", bus);
            _probe = Activator.CreateInstance(Native.LoadMod().GetType("Wildfire.Timberborn.Consequences.TimberbornOwnedBodyLiveness")!, _registry)!;
        }
        internal string Presence() => _probe.GetType().GetMethod("ObservePresence")!.Invoke(_probe, new object[] { _id })!.ToString()!;
        internal void DeleteComponent() => _entity.GetType().GetMethod("Delete", Flags)!.Invoke(_entity, null);
        internal void RemoveRegistryEntry() => _registry.GetType().GetMethod("RemoveEntity")!.Invoke(_registry, new[] { _entity });
        private static Type T(string assembly, string name) => Native.LoadNative(assembly).GetType(assembly + "." + name)!;
        private static object Get(object target, string field) => target.GetType().GetField(field, Flags)!.GetValue(target)!;
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags)!.SetValue(target, value);
    }
}
