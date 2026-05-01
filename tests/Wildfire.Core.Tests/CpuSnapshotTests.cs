using System.Text;
using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class CpuSnapshotTests
{
    [Theory]
    [MemberData(nameof(StarterScenarios))]
    public void StarterScenarioSnapshotsAreStable(SnapshotScenario scenario)
    {
        string first = RunSnapshot(scenario);
        string second = RunSnapshot(scenario);

        Assert.Equal(first, second);
        Assert.Equal(scenario.ExpectedSnapshot, first);
    }

    public static TheoryData<SnapshotScenario> StarterScenarios()
    {
        return
        [
            SingleIgnitionPoint(),
            WaterBarrier(),
            VerticalFuelColumn(),
        ];
    }

    private static SnapshotScenario SingleIgnitionPoint()
    {
        int width = 5;
        int height = 5;
        ushort[] cells = Enumerable
            .Range(0, width * height)
            .Select(_ => PackedCell.Pack(fuel: 8, heat: 0, flammability: 2, water: 0, terrain: 1, heatLoss: 2))
            .ToArray();

        cells[2 + (2 * width)] = PackedCell.Pack(fuel: 8, heat: 15, flammability: 3, water: 0, terrain: 1, heatLoss: 1);

        return new SnapshotScenario(
            Name: "single-ignition-point",
            Width: width,
            Height: height,
            Depth: 1,
            Seed: 101,
            TickCount: 4,
            InitialCells: cells,
            ExpectedSnapshot: """
                scenario=single-ignition-point
                seed=101
                size=5x5x1
                ticks=4
                deltas=1:1,2:1,3:1,4:1
                z=0
                5208 5208 5208 5208 5208
                5208 5208 5208 5208 5208
                5208 5208 3356 5208 5208
                5208 5208 5208 5208 5208
                5208 5208 5208 5208 5208
                """);
    }

    private static SnapshotScenario WaterBarrier()
    {
        int width = 7;
        int height = 5;
        ushort[] cells = Enumerable
            .Range(0, width * height)
            .Select(index =>
            {
                int x = index % width;
                int water = x == 3 ? 3 : 0;
                int heatLoss = x == 3 ? 3 : 1;
                return PackedCell.Pack(fuel: 7, heat: 0, flammability: 2, water: (byte)water, terrain: 1, heatLoss: (byte)heatLoss);
            })
            .ToArray();

        cells[1 + (2 * width)] = PackedCell.Pack(fuel: 9, heat: 15, flammability: 3, water: 0, terrain: 1, heatLoss: 1);

        return new SnapshotScenario(
            Name: "water-barrier",
            Width: width,
            Height: height,
            Depth: 1,
            Seed: 202,
            TickCount: 5,
            InitialCells: cells,
            ExpectedSnapshot: """
                scenario=water-barrier
                seed=202
                size=7x5x1
                ticks=5
                deltas=1:1,2:1,3:1,4:1,5:1
                z=0
                3207 3207 3207 7E07 3207 3207 3207
                3207 3207 3207 7E07 3207 3207 3207
                3207 3309 3207 7E07 3207 3207 3207
                3207 3207 3207 7E07 3207 3207 3207
                3207 3207 3207 7E07 3207 3207 3207
                """);
    }

    private static SnapshotScenario VerticalFuelColumn()
    {
        int width = 3;
        int height = 3;
        int depth = 3;
        FireGrid grid = new(width, height, depth);
        ushort[] cells = Enumerable
            .Range(0, grid.CellCount)
            .Select(index =>
            {
                (int x, int y, int z) = grid.FromIndex(index);
                bool column = x == 1 && y == 1;
                byte fuel = column ? (byte)10 : (byte)3;
                byte flammability = column ? (byte)3 : (byte)1;
                byte heatLoss = z == 2 ? (byte)2 : (byte)1;
                return PackedCell.Pack(fuel, heat: 0, flammability, water: 0, terrain: 1, heatLoss);
            })
            .ToArray();

        cells[grid.ToIndex(1, 1, 0)] = PackedCell.Pack(fuel: 10, heat: 15, flammability: 3, water: 0, terrain: 1, heatLoss: 1);

        return new SnapshotScenario(
            Name: "vertical-fuel-column",
            Width: width,
            Height: height,
            Depth: depth,
            Seed: 303,
            TickCount: 5,
            InitialCells: cells,
            ExpectedSnapshot: """
                scenario=vertical-fuel-column
                seed=303
                size=3x3x3
                ticks=5
                deltas=1:1,2:1,3:1,4:1,5:1
                z=0
                3103 3103 3103
                3103 3309 3103
                3103 3103 3103
                z=1
                3103 3103 3103
                3103 330A 3103
                3103 3103 3103
                z=2
                5103 5103 5103
                5103 530A 5103
                5103 5103 5103
                """);
    }

    private static string RunSnapshot(SnapshotScenario scenario)
    {
        CpuFireSimulator simulator = new(
            scenario.Width,
            scenario.Height,
            scenario.Depth,
            scenario.Seed,
            scenario.InitialCells);
        FireStepResult[] results = Enumerable
            .Range(0, scenario.TickCount)
            .Select(_ => simulator.Tick())
            .ToArray();

        return FormatSnapshot(scenario, simulator.Cells.ToArray(), results);
    }

    private static string FormatSnapshot(SnapshotScenario scenario, ushort[] cells, FireStepResult[] results)
    {
        StringBuilder builder = new();
        builder.AppendLine($"scenario={scenario.Name}");
        builder.AppendLine($"seed={scenario.Seed}");
        builder.AppendLine($"size={scenario.Width}x{scenario.Height}x{scenario.Depth}");
        builder.AppendLine($"ticks={scenario.TickCount}");
        builder.AppendLine($"deltas={string.Join(",", results.Select(result => $"{result.Tick}:{result.Deltas.Count}"))}");

        IEnumerable<string> layerLines = Enumerable
            .Range(0, scenario.Depth)
            .SelectMany(z => FormatLayer(scenario, cells, z));

        foreach (string line in layerLines)
        {
            builder.AppendLine(line);
        }

        return builder.ToString().TrimEnd();
    }

    private static IEnumerable<string> FormatLayer(SnapshotScenario scenario, ushort[] cells, int z)
    {
        int layerOffset = z * scenario.Width * scenario.Height;

        yield return $"z={z}";

        foreach (int y in Enumerable.Range(0, scenario.Height))
        {
            IEnumerable<string> row = Enumerable
                .Range(0, scenario.Width)
                .Select(x => cells[layerOffset + x + (y * scenario.Width)].ToString("X4"));

            yield return string.Join(" ", row);
        }
    }
}

public sealed record SnapshotScenario(
    string Name,
    int Width,
    int Height,
    int Depth,
    uint Seed,
    int TickCount,
    ushort[] InitialCells,
    string ExpectedSnapshot);
