namespace Wildfire.Core;

public static class FireRules
{
    public static ushort StepCell(FireGrid grid, int index, ushort cell, ReadOnlySpan<ushort> cells, uint tick, uint seed)
    {
        int fuel = PackedCell.Fuel(cell);
        int heat = PackedCell.Heat(cell);
        int flammability = PackedCell.Flammability(cell);
        int water = PackedCell.Water(cell);
        int terrain = PackedCell.Terrain(cell);
        int heatLoss = PackedCell.HeatLoss(cell);
        int neighborHeat = AverageNeighborHeat(grid, index, cells);
        int burningNeighborCount = CountBurningNeighbors(grid, index, cells);

        heat = ((heat * 3) + neighborHeat) / 4;
        heat += burningNeighborCount;

        if (water > 0)
        {
            heat -= water;
            if (heat > 8)
            {
                water -= 1;
            }
        }

        bool canBurn = terrain == 1 && fuel > 0;
        int ignitionThreshold = 12 - flammability + water;
        if (canBurn && heat >= ignitionThreshold)
        {
            uint roll = FireRandom.Hash((uint)index, tick, seed) & 15u;
            int burnChance = Math.Clamp(heat + flammability - water, 0, 15);
            if (roll < burnChance)
            {
                fuel = Math.Max(0, fuel - 1);
                heat = Math.Min(15, heat + 2 + flammability);
            }
        }

        heat -= 1 + (heatLoss / 3);
        heat = Math.Clamp(heat, 0, 15);
        fuel = Math.Clamp(fuel, 0, 15);
        water = Math.Clamp(water, 0, 3);

        return PackedCell.Pack(fuel, heat, flammability, water, terrain, heatLoss);
    }

    public static bool ShouldRemainActive(ushort cell)
    {
        int fuel = PackedCell.Fuel(cell);
        int heat = PackedCell.Heat(cell);
        int water = PackedCell.Water(cell);
        int terrain = PackedCell.Terrain(cell);
        int flammability = PackedCell.Flammability(cell);

        if (heat > 0)
        {
            return true;
        }

        int ignitionThreshold = 12 - flammability + water;
        return terrain == 1 && fuel > 0 && heat >= ignitionThreshold - 2;
    }

    public static int AverageNeighborHeat(FireGrid grid, int index, ReadOnlySpan<ushort> cells)
    {
        int sum = 0;
        int count = 0;
        (int x, int y, int z) = grid.FromIndex(index);
        int layerSize = grid.Width * grid.Height;

        if (x > 0)
        {
            sum += PackedCell.Heat(cells[index - 1]);
            count += 1;
        }

        if (x + 1 < grid.Width)
        {
            sum += PackedCell.Heat(cells[index + 1]);
            count += 1;
        }

        if (y > 0)
        {
            sum += PackedCell.Heat(cells[index - grid.Width]);
            count += 1;
        }

        if (y + 1 < grid.Height)
        {
            sum += PackedCell.Heat(cells[index + grid.Width]);
            count += 1;
        }

        if (z > 0)
        {
            sum += PackedCell.Heat(cells[index - layerSize]);
            count += 1;
        }

        if (z + 1 < grid.Depth)
        {
            sum += PackedCell.Heat(cells[index + layerSize]);
            count += 1;
        }

        return count == 0 ? 0 : sum / count;
    }

    public static int CountBurningNeighbors(FireGrid grid, int index, ReadOnlySpan<ushort> cells)
    {
        int count = 0;
        (int x, int y, int z) = grid.FromIndex(index);
        int layerSize = grid.Width * grid.Height;

        if (x > 0 && PackedCell.IsBurning(cells[index - 1]))
        {
            count += 1;
        }

        if (x + 1 < grid.Width && PackedCell.IsBurning(cells[index + 1]))
        {
            count += 1;
        }

        if (y > 0 && PackedCell.IsBurning(cells[index - grid.Width]))
        {
            count += 1;
        }

        if (y + 1 < grid.Height && PackedCell.IsBurning(cells[index + grid.Width]))
        {
            count += 1;
        }

        if (z > 0 && PackedCell.IsBurning(cells[index - layerSize]))
        {
            count += 1;
        }

        if (z + 1 < grid.Depth && PackedCell.IsBurning(cells[index + layerSize]))
        {
            count += 1;
        }

        return count;
    }

    public static void ForEachNeighbor(FireGrid grid, int index, Action<int> action)
    {
        (int x, int y, int z) = grid.FromIndex(index);

        if (x > 0)
        {
            action(index - 1);
        }

        if (x + 1 < grid.Width)
        {
            action(index + 1);
        }

        if (y > 0)
        {
            action(index - grid.Width);
        }

        if (y + 1 < grid.Height)
        {
            action(index + grid.Width);
        }

        int layerSize = grid.Width * grid.Height;
        if (z > 0)
        {
            action(index - layerSize);
        }

        if (z + 1 < grid.Depth)
        {
            action(index + layerSize);
        }
    }
}
