using Timberborn.EntitySystem;
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
            var dead = ReadDeathState(entity, body.Shape);
            var inventories = TimberbornNativeInventoryRoles.Capture(entity);
            return new TimberbornRetainedBodyObservation(body.EntityId, Exclusion(entity, block), dead,
                TimberbornRetainedTreeMaterialEvidence.Observe(entity, body), inventories.Select(inventory => inventory.Declaration));
        }).ToArray();
        return new(world, retained, states);
    }

    private static bool? ReadDeathState(EntityComponent entity, TimberbornInitialBodyShape shape)
    {
        if (entity.TryGetComponent<LivingNaturalResource>(out var living)) return living.IsDead;
        // Installed natural templates carry NaturalResourceSpec, whose native decorator supplies
        // LivingNaturalResource. Missing it is incomplete observation, not evidence of a live body.
        if (shape is TimberbornInitialBodyShape.Tree or TimberbornInitialBodyShape.Crop or TimberbornInitialBodyShape.Vegetation)
            throw new InvalidOperationException("Retained natural body lacks its native lifecycle component.");
        return null;
    }

}
