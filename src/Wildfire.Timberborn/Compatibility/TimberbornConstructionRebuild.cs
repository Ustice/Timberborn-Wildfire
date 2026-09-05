using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.ConstructionSites;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;

namespace Wildfire.Timberborn.Compatibility;

/// <summary>A captured native creation request, prepared while the original entity still exists.</summary>
internal sealed class TimberbornConstructionRebuild
{
    private readonly EntitySetup.Builder _setup;
    private readonly Placement _placement;
    private readonly string _name;

    private TimberbornConstructionRebuild(EntitySetup.Builder setup, Placement placement, string name)
    {
        _setup = setup;
        _placement = placement;
        _name = name;
    }

    public static TimberbornConstructionRebuild Prepare(BlockObject original)
    {
        if (!original.TryGetComponent(out Building building) || building.Spec?.Blueprint is null)
        {
            throw new InvalidOperationException($"Cannot prepare construction rebuild: {original.Name} has no building blueprint.");
        }

        // BuildingSpec alone is not a creation template in the native 1.1 API.
        // Capture the complete blueprint and placement before any entity deletion.
        return new TimberbornConstructionRebuild(
            new EntitySetup.Builder(building.Spec.Blueprint), original.Placement, original.Name);
    }

    public BlockObject CreateUnfinished(ConstructionFactory factory)
    {
        BlockObject rebuilt = factory.CreateAsUnfinished(_setup, _placement);
        if (!rebuilt.IsUnfinished)
        {
            throw new InvalidOperationException($"Construction rebuild recreated {_name} outside unfinished state.");
        }

        if (!rebuilt.TryGetComponent(out ConstructionSite constructionSite))
        {
            throw new InvalidOperationException($"Construction rebuild recreated {_name} without a construction site.");
        }

        return rebuilt;
    }
}
