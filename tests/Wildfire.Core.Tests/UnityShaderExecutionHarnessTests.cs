using Wildfire.Cli;
using Wildfire.Core;
using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed class UnityShaderExecutionHarnessTests
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
    public void UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("WILDFIRE_RUN_UNITY_SHADER_HARNESS"), "1", StringComparison.Ordinal))
        {
            return;
        }

        string repoRoot = FindRepoRoot();
        string unityExecutable = Environment.GetEnvironmentVariable("WILDFIRE_UNITY_EXECUTABLE")
            ?? "/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity";
        ShaderSnapshotHarness harness = new(new UnityBatchmodeShaderSnapshotExecutor(new UnityBatchmodeShaderSnapshotExecutorOptions(
            UnityExecutablePath: unityExecutable,
            ProjectPath: Path.Combine(repoRoot, "src/Wildfire.Unity/UnityBatchmodeProject"),
            ComputeShaderPath: Path.Combine(repoRoot, "src/Wildfire.Unity/FireSim.compute"),
            Timeout: TimeSpan.FromMinutes(5))));

        ShaderSnapshotFixture singleIgnition = CreateFixture(
            scenario: "single-ignition",
            seed: 21,
            width: 5,
            height: 5,
            depth: 1);
        ShaderSnapshotCapture singleIgnitionCapture = harness.Capture(singleIgnition, tickCount: 2);

        Assert.Equal(singleIgnition.Scenario, singleIgnitionCapture.Scenario);
        Assert.Equal(singleIgnition.Seed, singleIgnitionCapture.Seed);
        Assert.Equal(singleIgnition.Grid, singleIgnitionCapture.Grid);
        Assert.Equal(singleIgnition.Grid.CellCount, singleIgnitionCapture.FinalPackedCells.Length);
        Assert.Equal(2, singleIgnitionCapture.Ticks.Length);
        Assert.Equal([5, 5], DeltaCounts(singleIgnitionCapture));
        Assert.Equal(5, CountFinalHotCells(singleIgnitionCapture));
        Assert.Equal("visual-fnv1a32:50C4978E", singleIgnitionCapture.Visual?.Checksum);

        ShaderSnapshotFixture lineOfFuel = CreateFixture(
            scenario: "line-of-fuel",
            seed: 42,
            width: 12,
            height: 5,
            depth: 1);
        ShaderSnapshotCapture lineOfFuelCapture = harness.Capture(lineOfFuel, tickCount: 4);

        Assert.Equal(lineOfFuel.Scenario, lineOfFuelCapture.Scenario);
        Assert.Equal(lineOfFuel.Seed, lineOfFuelCapture.Seed);
        Assert.Equal(lineOfFuel.Grid, lineOfFuelCapture.Grid);
        Assert.Equal(lineOfFuel.Grid.CellCount, lineOfFuelCapture.FinalPackedCells.Length);
        Assert.Equal(4, lineOfFuelCapture.Ticks.Length);
        Assert.Equal([5, 5, 5, 2], DeltaCounts(lineOfFuelCapture));
        Assert.Equal(5, CountFinalHotCells(lineOfFuelCapture));
        Assert.Equal("visual-fnv1a32:120F70AE", lineOfFuelCapture.Visual?.Checksum);

        ShaderSnapshotFixture waterBarrier = CreateFixture(
            scenario: "water-barrier",
            seed: 42,
            width: 12,
            height: 5,
            depth: 1);
        ShaderSnapshotCapture waterBarrierCapture = harness.Capture(waterBarrier, tickCount: 4);

        Assert.Equal(waterBarrier.Scenario, waterBarrierCapture.Scenario);
        Assert.Equal(waterBarrier.Seed, waterBarrierCapture.Seed);
        Assert.Equal(waterBarrier.Grid, waterBarrierCapture.Grid);
        Assert.Equal(waterBarrier.Grid.CellCount, waterBarrierCapture.FinalPackedCells.Length);
        Assert.Equal(4, waterBarrierCapture.Ticks.Length);
        Assert.Equal([5, 5, 5, 5], DeltaCounts(waterBarrierCapture));
        Assert.Equal(1, CountFinalHotCells(waterBarrierCapture));
        Assert.Equal("visual-fnv1a32:40818F57", waterBarrierCapture.Visual?.Checksum);
    }

    private static int[] DeltaCounts(ShaderSnapshotCapture capture)
    {
        return capture.Ticks.Select(static tick => tick.DeltaCount).ToArray();
    }

    private static int CountFinalHotCells(ShaderSnapshotCapture capture)
    {
        return capture.FinalPackedCells.Count(static cell => PackedCell.Heat(cell) > 0);
    }

    private static ShaderSnapshotFixture CreateFixture(
        string scenario,
        uint seed,
        int width,
        int height,
        int depth)
    {
        Scenario builtScenario = ScenarioCatalog.Build(CliOptions.Parse(
        [
            "--scenario=" + scenario,
            "--seed=" + seed,
            "--width=" + width,
            "--height=" + height,
            "--depth=" + depth,
            "--layer=0",
        ]));

        return ShaderSnapshotFixtureLoader.Load(FixtureExporter.Export(builtScenario, selectedLayer: 0));
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
