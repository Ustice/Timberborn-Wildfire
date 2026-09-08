using Timberborn.EntitySystem;

namespace Wildfire.Timberborn.Consequences;

public sealed class TimberbornOwnedBodyLiveness : ITimberbornOwnedBodyLiveness
{
    private readonly EntityRegistry _entities;
    public TimberbornOwnedBodyLiveness(EntityRegistry entities) => _entities = entities ?? throw new ArgumentNullException(nameof(entities));
    public bool IsLive(Guid entityId) => ObservePresence(entityId) == TimberbornOwnedBodyPresence.Live;

    public TimberbornOwnedBodyPresence ObservePresence(Guid entityId)
    {
        if (entityId == Guid.Empty) throw new ArgumentException("Owned body requires a native identity.", nameof(entityId));
        var entity = _entities.GetEntity(entityId);
        if (entity is null) return TimberbornOwnedBodyPresence.Absent;
        if (entity.EntityId != entityId) throw new InvalidOperationException("Native registry returned the wrong body identity.");
        if (entity.Deleted) return TimberbornOwnedBodyPresence.Deleted;
        if (!entity.Initialized) return TimberbornOwnedBodyPresence.Uninitialized;
        return entity ? TimberbornOwnedBodyPresence.Live : TimberbornOwnedBodyPresence.InvalidNativeReference;
    }
}
