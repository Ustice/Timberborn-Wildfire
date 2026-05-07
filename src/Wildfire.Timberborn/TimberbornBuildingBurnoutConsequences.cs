using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.Navigation;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornPausableBuildingBurnoutConsequenceApi :
    ITimberbornBuildingBurnoutConsequenceApi,
    ITimberbornQaBuildingBurnoutStimulusTargetProvider
{
    private readonly FireGrid _grid;
    private readonly IBlockService _blockService;

    public TimberbornPausableBuildingBurnoutConsequenceApi(FireGrid grid, IBlockService blockService)
    {
        _grid = grid;
        _blockService = blockService ?? throw new ArgumentNullException(nameof(blockService));
    }

    public TimberbornBuildingBurnoutConsequenceResult ApplyConsequence(
        TimberbornBuildingBurnoutConsequence consequence)
    {
        (int x, int y, int z) = _grid.FromIndex(consequence.CellIndex);
        Vector3Int coordinates = new(x, y, z);
        PausableBuilding[] pausableBuildings = _blockService
            .GetObjectsWithComponentAt<PausableBuilding>(coordinates)
            .ToArray();
        BlockObject? blockObject = _blockService
            .GetObjectsWithComponentAt<BlockObject>(coordinates)
            .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
            .FirstOrDefault();
        Accessible[] accessiblesToBlock = consequence.ShouldApplyBurnout
            ? (blockObject is null
                ? Array.Empty<Accessible>()
                : GetBlockableAccessibles(blockObject)
                    .Where(static accessible => accessible.Accesses.Count > 0)
                    .ToArray())
            : Array.Empty<Accessible>();

        Accessible[] uniqueAccessiblesToBlock = accessiblesToBlock
            .GroupBy(static accessible => RuntimeHelpers.GetHashCode(accessible))
            .Select(static group => group.First())
            .ToArray();

        Array.ForEach(uniqueAccessiblesToBlock, static accessible => accessible.ClearAccesses());

        return new TimberbornBuildingBurnoutConsequenceResult(
            MatchedBuildingCell: pausableBuildings.Length > 0,
            AppliedConsequence: uniqueAccessiblesToBlock.Length > 0);
    }

    public TimberbornQaBuildingBurnoutStimulusTarget FindTarget(FireGrid grid)
    {
        return TimberbornQaBuildingBurnoutStimulusTargets.FindFirstUsableTarget(
            grid,
            HasUnpausedPausableBuildingAt);
    }

    private bool HasUnpausedPausableBuildingAt(TimberbornQaBuildingBurnoutStimulusTarget target)
    {
        return _blockService
            .GetObjectsWithComponentAt<PausableBuilding>(new Vector3Int(target.X, target.Y, target.Z))
            .Any(static building => !building.Paused);
    }

    private static Accessible[] GetBlockableAccessibles(BlockObject blockObject)
    {
        return blockObject.GetComponentsAllocating<Accessible>()
            .Concat(blockObject.Transform.GetComponentsInChildren<Accessible>(includeInactive: true))
            .GroupBy(static accessible => RuntimeHelpers.GetHashCode(accessible))
            .Select(static group => group.First())
            .ToArray();
    }
}

public static class TimberbornQaBuildingBurnoutStimulusTargets
{
    public static TimberbornQaBuildingBurnoutStimulusTarget FindFirstUsableTarget(
        FireGrid grid,
        Func<TimberbornQaBuildingBurnoutStimulusTarget, bool> isUsableTarget)
    {
        TimberbornQaBuildingBurnoutStimulusTarget? target = Enumerable.Range(0, grid.CellCount)
            .Select(index =>
            {
                (int x, int y, int z) = grid.FromIndex(index);
                return new TimberbornQaBuildingBurnoutStimulusTarget(
                    index,
                    x,
                    y,
                    z,
                    ScannedCellCount: index + 1);
            })
            .FirstOrDefault(isUsableTarget);

        return target ??
            throw new InvalidOperationException(
                "No unpaused pausable Timberborn building cell was found for QA burnout stimulus.");
    }
}
