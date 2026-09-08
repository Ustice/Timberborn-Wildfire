using Timberborn.NaturalResourcesLifecycle;
using Wildfire.Core;

namespace Wildfire.Timberborn.Runtime;

public sealed partial class TimberbornInitialWorldProjectionProvider
{
    /// <summary>Caller holds the session's same CaptureAtRest guard through staging and the final full reread.</summary>
    internal TimberbornOwnedRestoreObservation CaptureForRestoreDuringScope(FireGrid grid, IReadOnlyList<Guid> requiredRetainedIds)
    {
        var world = CaptureDuringScope(grid);
        var retained = CaptureRetainedBodies(grid, requiredRetainedIds);
        var states = retained.Select(body =>
        {
            var entity = RequireRetainedEntity(body.EntityId);
            var block = RequireRetainedBlock(entity);
            bool? dead = entity.TryGetComponent<LivingNaturalResource>(out var living) ? living.IsDead : null;
            var inventories = TimberbornNativeInventoryRoles.Capture(entity);
            return new TimberbornRetainedBodyObservation(body.EntityId, Exclusion(entity, block), dead,
                inventories.Select(inventory => inventory.Declaration));
        }).ToArray();
        return new(world, retained, states);
    }
}
