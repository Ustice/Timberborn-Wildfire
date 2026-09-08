using Timberborn.BlockSystem;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;
using Wildfire.Core;

namespace Wildfire.Timberborn.Mapping;

public readonly record struct TimberbornMaterialFootprintSlot(TimberbornCellCoordinates LocalCoordinates, int CellIndex);

/// <summary>Native local coordinates identify slots; placement only decides their current world cells.</summary>
public static class TimberbornNativeMaterialFootprint
{
    public static TimberbornMaterialProjection Capture(BlockObject blockObject, FireGrid grid,
        IReadOnlyList<TimberbornMaterialPart> parts)
    {
        if (blockObject is null) throw new ArgumentNullException(nameof(blockObject));
        EntityComponent entity = blockObject.GetComponent<EntityComponent>();
        if (!entity.Initialized || entity.Deleted || !blockObject.Positioned || !blockObject.IsFinished)
            throw new InvalidOperationException("Material projection requires a settled, positioned, finished native entity.");
        return new TimberbornMaterialProjection(entity.EntityId, Project(blockObject.Blocks, blockObject.Placement, grid), parts);
    }

    public static IReadOnlyList<TimberbornMaterialFootprintSlot> Project(Blocks blocks, Placement placement, FireGrid grid)
    {
        if (blocks is null) throw new ArgumentNullException(nameof(blocks));
        var slots = blocks.GetOccupiedCoordinates().Select(local =>
        {
            var world = blocks.Transform(local, placement);
            return new TimberbornMaterialFootprintSlot(new TimberbornCellCoordinates(local.x, local.y, local.z),
                grid.ToIndex(world.x, world.y, world.z));
        }).OrderBy(slot => slot.LocalCoordinates.Z).ThenBy(slot => slot.LocalCoordinates.Y).ThenBy(slot => slot.LocalCoordinates.X).ToArray();
        if (slots.Select(slot => slot.LocalCoordinates).Distinct().Count() != slots.Length ||
            slots.Select(slot => slot.CellIndex).Distinct().Count() != slots.Length)
            throw new InvalidOperationException("Native footprint did not transform bijectively.");
        var positioned = PositionedBlocks.From(blocks, placement).GetOccupiedCoordinates()
            .Select(world => grid.ToIndex(world.x, world.y, world.z)).ToHashSet();
        if (!positioned.SetEquals(slots.Select(slot => slot.CellIndex)))
            throw new InvalidOperationException("Local slot projection differs from native positioned occupied blocks.");
        return Array.AsReadOnly(slots);
    }
}
