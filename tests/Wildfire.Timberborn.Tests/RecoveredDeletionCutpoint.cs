using System.Collections;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

// Run the real recovered-stack -> EntityService -> EntityComponent deletion chain,
// stopping at its native event before registry removal and UnityEngine.Object.Destroy.
internal sealed class RecoveredDeletionCutpoint
{
    private readonly object _owner, _foreign, _registry;
    private readonly Guid _ownerId = Guid.NewGuid(), _foreignId = Guid.NewGuid();
    internal readonly Exception Failure = new InvalidOperationException("native owner deletion event interrupted");
    internal RecoveredDeletionCutpoint(NativeInventoryRoleFixture f, object recovered)
    {
        var entityType = f.T("Timberborn.EntitySystem", "EntityComponent");
        _owner = RuntimeHelpers.GetUninitializedObject(entityType);
        _foreign = RuntimeHelpers.GetUninitializedObject(entityType);
        NativeInventoryRoleFixture.Set(_owner, "<EntityId>k__BackingField", _ownerId);
        NativeInventoryRoleFixture.Set(_foreign, "<EntityId>k__BackingField", _foreignId);
        var state = entityType.GetField("_entityState", NativeInventoryRoleFixture.Flags)!;
        state.SetValue(_owner, Enum.Parse(state.FieldType, "Initialized"));
        _registry = Activator.CreateInstance(f.T("Timberborn.EntitySystem", "EntityRegistry"))!;
        Entries.Add(_ownerId, _owner); Entries.Add(_foreignId, _foreign);
        var ordered = (IList)NativeInventoryRoleFixture.GetField(_registry, "_entitiesInInstantiationOrder");
        ordered.Add(_owner); ordered.Add(_foreign);
        var cache = RuntimeHelpers.GetUninitializedObject(f.T("Timberborn.BaseComponentSystem", "ComponentCache"));
        NativeInventoryRoleFixture.Set(cache, "_components", new List<object> { _owner, recovered });
        var index = Activator.CreateInstance(f.T("Timberborn.BaseComponentSystem", "TypeIndexMap"))!;
        var types = (IDictionary)NativeInventoryRoleFixture.GetField(index, "_typeIndex");
        types.Add(entityType, 0);
        types.Add(f.T("Timberborn.EntitySystem", "IDeletableEntity"), null);
        NativeInventoryRoleFixture.Set(cache, "_typeIndexMap", index);
        var cacheField = f.T("Timberborn.BaseComponentSystem", "BaseComponent").GetField("_componentCache", NativeInventoryRoleFixture.Flags)!;
        cacheField.SetValue(_owner, cache); cacheField.SetValue(recovered, cache);
        NativeInventoryRoleFixture.Set(_owner, "<RegisteredComponents>k__BackingField",
            Activator.CreateInstance(typeof(List<>).MakeGenericType(f.T("Timberborn.EntitySystem", "IRegisteredComponent")))!);
        NativeInventoryRoleFixture.Set(_owner, "_entityComponentRegistry",
            RuntimeHelpers.GetUninitializedObject(f.T("Timberborn.EntitySystem", "EntityComponentRegistry")));
        var bus = Activator.CreateInstance(f.T("Timberborn.SingletonSystem", "EventBus"))!;
        NativeInventoryRoleFixture.Set(bus, "_ready", true);
        var subscriptions = NativeInventoryRoleFixture.GetField(bus, "_subscriptions");
        subscriptions.GetType().GetMethod("Add")!.Invoke(subscriptions,
            [f.T("Timberborn.EntitySystem", "EntityDeletedEvent"), this, (Action<object>)(_ => throw Failure)]);
        NativeInventoryRoleFixture.Set(_owner, "_eventBus", bus);
        var service = RuntimeHelpers.GetUninitializedObject(f.T("Timberborn.EntitySystem", "EntityService"));
        NativeInventoryRoleFixture.Set(service, "_entityRegistry", _registry);
        NativeInventoryRoleFixture.Set(recovered, "_entityService", service);
    }
    private IDictionary Entries => (IDictionary)NativeInventoryRoleFixture.GetField(_registry, "_entities");
    internal string State => NativeInventoryRoleFixture.GetField(_owner, "_entityState").ToString()!;
    internal bool OwnerStillRegistered => ReferenceEquals(Entries[_ownerId], _owner);
    internal bool ForeignStillRegistered => ReferenceEquals(Entries[_foreignId], _foreign);
}
