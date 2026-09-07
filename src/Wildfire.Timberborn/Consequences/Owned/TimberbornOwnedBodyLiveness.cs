using Timberborn.EntitySystem;

namespace Wildfire.Timberborn.Consequences;

public sealed class TimberbornOwnedBodyLiveness : ITimberbornOwnedBodyLiveness
{
    private readonly EntityRegistry _entities;
    public TimberbornOwnedBodyLiveness(EntityRegistry entities) => _entities = entities ?? throw new ArgumentNullException(nameof(entities));
    public bool IsLive(Guid entityId)
    {
        if (entityId == Guid.Empty) throw new ArgumentException("Owned body requires a native identity.", nameof(entityId));
        var entity = _entities.GetEntity(entityId);
        if (entity is null || entity.Deleted || !entity.Initialized || !entity) return false;
        if (entity.EntityId != entityId) throw new InvalidOperationException("Native registry returned the wrong body identity.");
        return true;
    }
}
