using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BuildingsNavigation;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;
using Timberborn.Growing;
using Timberborn.InventorySystem;
using Timberborn.MapIndexSystem;
using Timberborn.NaturalResourcesLifecycle;
using Timberborn.Planting;
using Timberborn.TerrainSystem;
using Timberborn.WorkSystem;
using UnityEngine;
using Wildfire.Core;
using Wildfire.Timberborn.FireSafety;

namespace Wildfire.Timberborn.Fertilizer;

internal sealed record FertilizerJobTarget(Workplace Employer, BlockObject Plant, Inventory Source,
    Vector3Int Placement, Vector3Int Ground, Vector3 Standing, FireGrid Grid, int CellIndex, byte Limit)
{
    internal static bool TryCreate(Workplace employer, BlockObject plant, Inventory source, byte limit,
        FireFieldObservation observation, MapIndexService indices, IThreadSafeColumnTerrainMap columns,
        out FertilizerJobTarget target)
    {
        target = null!;
        if (!plant || !source || limit is < 1 or > 3) return false;
        var grid = new FireGrid(observation.Width, observation.Height, observation.Depth);
        var ground = plant.CoordinatesAtBaseZ;
        if ((uint)ground.x >= grid.Width || (uint)ground.y >= grid.Height || (uint)ground.z >= grid.Depth)
            return false;
        target = new(employer, plant, source, plant.Coordinates, ground, CoordinateSystem.GridToWorldCentered(ground),
            grid, grid.ToIndex(ground.x, ground.y, ground.z), limit);
        return target.IsCurrent(indices, columns);
    }

    internal bool IsCurrent(MapIndexService indices, IThreadSafeColumnTerrainMap columns)
    {
        if (!Live(Employer) || !Employer.Enabled || Employer.GetComponent<BlockObject>() is not { IsFinished: true } ||
            !Live(Plant) || !Plant.Positioned || !Plant.AddedToService || Plant.Coordinates != Placement ||
            Plant.CoordinatesAtBaseZ != Ground)
            return false;
        var plantable = Plant.GetComponent<PlantableSpec>();
        var living = Plant.GetComponent<LivingNaturalResource>();
        var dying = Plant.GetComponent<DyingNaturalResource>();
        var growable = Plant.GetComponent<Growable>();
        var planter = Employer.GetComponent<PlanterBuilding>();
        var range = Employer.GetComponent<BuildingTerrainRange>();
        return Plant.GetComponent<Plantable>() is not null && living is not null && !living.IsDead && dying?.IsDying != true &&
            growable is not null && growable.GrowthInProgress && plantable is not null && planter is not null &&
            planter.CanPlant(plantable.TemplateName) && range is not null && range.GetRange().Contains(Ground) &&
            columns.TryGetIndexAtCeiling(indices.CellToIndex(new Vector2Int(Ground.x, Ground.y)), Ground.z, out _);
    }

    internal bool Matches(FireFieldObservation observation) =>
        observation.Width == Grid.Width && observation.Height == Grid.Height && observation.Depth == Grid.Depth;

    internal static bool Live(BaseComponent component)
    {
        if (!component) return false;
        var entity = component.GetComponent<EntityComponent>();
        return entity is not null && entity.Initialized && !entity.Deleted;
    }
}
