using Wildfire.Core;
using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed class ShaderExecutionContractTests
{
    [Fact]
    public void UnityExecutorReportsMissingUnityAsEnvironmentFailure()
    {
        UnityBatchmodeShaderSnapshotExecutor executor = new(new UnityBatchmodeShaderSnapshotExecutorOptions(
            UnityExecutablePath: "/missing/Unity",
            ProjectPath: "src/Wildfire.Unity/UnityBatchmodeProject",
            ComputeShaderPath: "src/Wildfire.Unity/FireSim.compute",
            Timeout: TimeSpan.FromMilliseconds(1)));
        ShaderSnapshotFixture fixture = new(
            FormatVersion: 1,
            Scenario: "single-ignition",
            Seed: 1,
            Grid: new ComputeGridDimensions(1, 1, 1),
            SelectedLayer: new ShaderSnapshotLayer(0, 0, 1),
            InitialCells: [0x1001]);

        ShaderSnapshotExecutionFailedException exception = Assert.Throws<ShaderSnapshotExecutionFailedException>(
            () => executor.Capture(fixture, tickCount: 1));

        Assert.Equal("environment", exception.Phase);
        Assert.Contains("Unity executable was not found", exception.Message);
    }

    [Fact]
    public void SnapshotJsonLoadsUnityCaptureShape()
    {
        string json = """
        {
          "formatVersion": 1,
          "scenario": "single-ignition",
          "seed": 21,
          "grid": {
            "width": 2,
            "height": 1,
            "depth": 1
          },
          "tickCount": 1,
          "finalPackedCells": [
            4097,
            4098
          ],
          "perTickDeltaCounts": [
            1
          ],
          "perTickDeltas": [
            {
              "tick": 1,
              "deltaCount": 1,
              "deltas": [
                {
                  "cellIndex": 1,
                  "oldCell": 4097,
                  "newCell": 4098
                }
              ]
            }
          ],
          "visual": {
            "checksum": "visual-fnv1a32:00000001"
          }
        }
        """;

        ShaderSnapshotCapture capture = ShaderSnapshotJson.Load(json);

        Assert.Equal("single-ignition", capture.Scenario);
        Assert.Equal(21u, capture.Seed);
        Assert.Equal(new ComputeGridDimensions(2, 1, 1), capture.Grid);
        Assert.Equal([0x1001, 0x1002], capture.FinalPackedCells);
        Assert.Equal(new ShaderSnapshotDelta(1, 0x1001, 0x1002), capture.Ticks[0].Deltas[0]);
        Assert.Equal("visual-fnv1a32:00000001", capture.Visual?.Checksum);
    }

    [Fact]
    public void FuelBurnDownFixturesKeepSingleBurningSourceCoverage()
    {
        foreach (string name in new[] { "low", "medium", "high" })
        {
            string scenario = $"{name}-fuel-burn-down";
            ShaderSnapshotFixture fixture = ShaderSnapshotFixtureLoader.LoadFile(Path.Combine(
                FindRepoRoot(), "tests/Wildfire.Core.Tests/ShaderSnapshots/twf-089", $"{scenario}-seed89-5x5x1.fixture.json"));
            ushort[] fueledTerrainCells = fixture.InitialCells
                .Where(static cell => PackedCell.Terrain(cell) == 1 && PackedCell.Fuel(cell) > 0)
                .ToArray();

            Assert.Equal(scenario, fixture.Scenario);
            Assert.Equal(89u, fixture.Seed);
            Assert.Single(fueledTerrainCells);
            ushort cell = fueledTerrainCells[0];
            Assert.True(PackedCell.Heat(cell) >= 11 - PackedCell.Flammability(cell) + (PackedCell.Water(cell) * 2));
        }
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(Environment.CurrentDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Wildfire.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate Wildfire.slnx from test working directory.");
    }

}
