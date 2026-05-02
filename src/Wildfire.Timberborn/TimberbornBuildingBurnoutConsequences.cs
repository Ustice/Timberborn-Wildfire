using Timberborn.BlockSystem;
using Timberborn.Buildings;
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
        PausableBuilding[] buildingsToPause = consequence.ShouldApplyBurnout
            ? pausableBuildings.Where(static building => !building.Paused).ToArray()
            : Array.Empty<PausableBuilding>();

        Array.ForEach(buildingsToPause, static building => building.Pause());

        return new TimberbornBuildingBurnoutConsequenceResult(
            MatchedBuildingCell: pausableBuildings.Length > 0,
            AppliedConsequence: buildingsToPause.Length > 0);
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
