using Wildfire.Core;

namespace Wildfire.Cli;

public static class ScenarioCatalog
{
    public const string DefaultScenarioName = "single-ignition";

    private static readonly IReadOnlyDictionary<string, ScenarioDefinition> Scenarios =
        new[]
        {
            Define("single-ignition", 21, 13, 1, BuildSingleIgnition),
            Define("line-of-fuel", 28, 9, 1, BuildLineOfFuel),
            Define("water-barrier", 25, 13, 1, BuildWaterBarrier),
            Define("vertical-fuel-column", 9, 9, 5, BuildVerticalFuelColumn),
            Define("sparse-forest", 32, 16, 1, BuildSparseForest),
            Define("building-cluster", 28, 16, 1, BuildBuildingCluster),
            Define("mixed-terrain", 32, 18, 3, BuildMixedTerrain),
        }.ToDictionary(scenario => scenario.Name, StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<string> Names => Scenarios.Keys.Order(StringComparer.OrdinalIgnoreCase);

    public static Scenario Build(CliOptions options)
    {
        if (!Scenarios.TryGetValue(options.Scenario, out ScenarioDefinition? definition))
        {
            string known = string.Join(", ", Names);
            throw new ArgumentException($"Unknown scenario '{options.Scenario}'. Known scenarios: {known}.");
        }

        int width = PositiveDimension(options.Width ?? definition.DefaultWidth, "width");
        int height = PositiveDimension(options.Height ?? definition.DefaultHeight, "height");
        int depth = PositiveDimension(options.Depth ?? definition.DefaultDepth, "depth");
        FireGrid grid = new(width, height, depth);
        ushort[] cells = definition.Build(grid, options.Seed);

        return new Scenario(definition.Name, options.Seed, grid, cells);
    }

    private static ScenarioDefinition Define(
        string name,
        int width,
        int height,
        int depth,
        Func<FireGrid, uint, ushort[]> build)
    {
        return new ScenarioDefinition(name, width, height, depth, build);
    }

    private static ushort[] BuildSingleIgnition(FireGrid grid, uint seed)
    {
        ushort[] cells = Fill(grid, Grass);
        Ignite(cells, grid, grid.Width / 2, grid.Height / 2, 0);
        return cells;
    }

    private static ushort[] BuildLineOfFuel(FireGrid grid, uint seed)
    {
        ushort[] cells = Fill(grid, Bare);
        int y = grid.Height / 2;

        Enumerable.Range(0, grid.Width)
            .Select(x => grid.ToIndex(x, y, 0))
            .ToList()
            .ForEach(index => cells[index] = Brush());

        Ignite(cells, grid, 1, y, 0);
        return cells;
    }

    private static ushort[] BuildWaterBarrier(FireGrid grid, uint seed)
    {
        ushort[] cells = Fill(grid, Grass);
        int barrierX = Math.Clamp(grid.Width / 2, 0, grid.Width - 1);

        Enumerable.Range(0, grid.Height)
            .Select(y => grid.ToIndex(barrierX, y, 0))
            .ToList()
            .ForEach(index => cells[index] = Water());

        Ignite(cells, grid, Math.Max(0, barrierX / 2), grid.Height / 2, 0);
        return cells;
    }

    private static ushort[] BuildVerticalFuelColumn(FireGrid grid, uint seed)
    {
        ushort[] cells = Fill(grid, Bare);
        int x = grid.Width / 2;
        int y = grid.Height / 2;

        Enumerable.Range(0, grid.Depth)
            .Select(z => grid.ToIndex(x, y, z))
            .ToList()
            .ForEach(index => cells[index] = Timber());

        Ignite(cells, grid, x, y, 0);
        return cells;
    }

    private static ushort[] BuildSparseForest(FireGrid grid, uint seed)
    {
        ushort[] cells = Enumerable
            .Range(0, grid.CellCount)
            .Select(index =>
            {
                uint hash = FireRandom.Hash((uint)index, 0, seed);
                bool tree = hash % 100 < 38;
                bool brush = hash % 100 is >= 38 and < 60;
                return tree ? Timber() : brush ? Brush() : Bare();
            })
            .ToArray();

        Ignite(cells, grid, grid.Width / 2, grid.Height / 2, 0);
        return cells;
    }

    private static ushort[] BuildBuildingCluster(FireGrid grid, uint seed)
    {
        ushort[] cells = Fill(grid, Grass);
        int centerX = grid.Width / 2;
        int centerY = grid.Height / 2;
        int radiusX = Math.Max(1, grid.Width / 7);
        int radiusY = Math.Max(1, grid.Height / 5);

        Enumerable.Range(0, grid.CellCount)
            .Where(index =>
            {
                (int x, int y, int z) = grid.FromIndex(index);
                return z == 0 && Math.Abs(x - centerX) <= radiusX && Math.Abs(y - centerY) <= radiusY;
            })
            .ToList()
            .ForEach(index => cells[index] = Building());

        Ignite(cells, grid, centerX, centerY, 0);
        return cells;
    }

    private static ushort[] BuildMixedTerrain(FireGrid grid, uint seed)
    {
        ushort[] cells = Enumerable
            .Range(0, grid.CellCount)
            .Select(index =>
            {
                (int x, int y, int z) = grid.FromIndex(index);
                uint hash = FireRandom.Hash((uint)index, 0, seed);

                if (z == 0 && x == grid.Width / 3)
                {
                    return Water();
                }

                if (z > 0 && hash % 100 < 18)
                {
                    return Timber();
                }

                if ((x + y + z) % 7 == 0)
                {
                    return Bare();
                }

                return hash % 100 < 24 ? Brush() : Grass();
            })
            .ToArray();

        Ignite(cells, grid, Math.Max(0, grid.Width / 5), grid.Height / 2, 0);
        return cells;
    }

    private static ushort[] Fill(FireGrid grid, Func<ushort> cell)
    {
        return Enumerable.Range(0, grid.CellCount).Select(_ => cell()).ToArray();
    }

    private static void Ignite(ushort[] cells, FireGrid grid, int x, int y, int z)
    {
        int safeX = Math.Clamp(x, 0, grid.Width - 1);
        int safeY = Math.Clamp(y, 0, grid.Height - 1);
        int safeZ = Math.Clamp(z, 0, grid.Depth - 1);
        int index = grid.ToIndex(safeX, safeY, safeZ);
        cells[index] = PackedCell.SetHeat(PackedCell.SetFuel(cells[index], Math.Max(PackedCell.Fuel(cells[index]), 8)), 15);
    }

    private static int PositiveDimension(int value, string name)
    {
        return value > 0 ? value : throw new ArgumentException($"{name} must be greater than zero.");
    }

    private static ushort Bare() => PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 1, heatLoss: 2);

    private static ushort Grass() => PackedCell.Pack(fuel: 7, heat: 0, flammability: 2, water: 0, terrain: 1, heatLoss: 2);

    private static ushort Brush() => PackedCell.Pack(fuel: 9, heat: 0, flammability: 3, water: 0, terrain: 1, heatLoss: 1);

    private static ushort Timber() => PackedCell.Pack(fuel: 12, heat: 0, flammability: 3, water: 0, terrain: 1, heatLoss: 1);

    private static ushort Building() => PackedCell.Pack(fuel: 15, heat: 0, flammability: 2, water: 0, terrain: 1, heatLoss: 1);

    private static ushort Water() => PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 3, terrain: 1, heatLoss: 5);
}

public sealed record Scenario(string Name, uint Seed, FireGrid Grid, ushort[] Cells);

public sealed record ScenarioDefinition(
    string Name,
    int DefaultWidth,
    int DefaultHeight,
    int DefaultDepth,
    Func<FireGrid, uint, ushort[]> Build);
