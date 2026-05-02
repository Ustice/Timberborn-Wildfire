using Wildfire.Cli;
using Wildfire.Core;
using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed class UnityShaderExecutionHarnessTests
{
    private static readonly ReleaseShaderScenario[] ReleaseScenarios =
    [
        new("single-ignition", 21, 5, 5, 1, 2, "single-ignition-seed21-5x5x1-tick2.capture.json"),
        new("line-of-fuel", 42, 12, 5, 1, 4, "line-of-fuel-seed42-12x5x1-tick4.capture.json"),
        new("water-barrier", 42, 12, 5, 1, 4, "water-barrier-seed42-12x5x1-tick4.capture.json"),
        new("vertical-fuel-column", 17, 5, 5, 4, 4, "vertical-fuel-column-seed17-5x5x4-tick4.capture.json"),
        new("sparse-forest", 73, 16, 10, 1, 3, "sparse-forest-seed73-16x10x1-tick3.capture.json"),
        new("building-cluster", 91, 14, 10, 1, 3, "building-cluster-seed91-14x10x1-tick3.capture.json"),
        new("mixed-terrain", 123, 16, 10, 3, 3, "mixed-terrain-seed123-16x10x3-tick3.capture.json"),
    ];

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
    public void AcceptedReleaseShaderSnapshotResourcesContainExactCellsAndDeltas()
    {
        foreach (ReleaseShaderScenario scenario in ReleaseScenarios)
        {
            ShaderSnapshotCapture expected = LoadExpectedCapture(scenario);

            AssertExpectedCaptureMetadata(expected, scenario);
            Assert.Equal(scenario.Width * scenario.Height * scenario.Depth, expected.FinalPackedCells.Length);
            Assert.Equal(scenario.TickCount, expected.Ticks.Length);
            Assert.All(expected.Ticks, static tick => Assert.Equal(tick.DeltaCount, tick.Deltas.Length));
            Assert.NotNull(expected.Visual?.Checksum);
        }
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

        foreach (ReleaseShaderScenario scenario in ReleaseScenarios)
        {
            AssertReleaseShaderScenario(harness, scenario);
        }
    }

    private static void AssertReleaseShaderScenario(ShaderSnapshotHarness harness, ReleaseShaderScenario scenario)
    {
        ShaderSnapshotCapture expected = LoadExpectedCapture(scenario);
        ShaderSnapshotFixture fixture = CreateFixture(
            scenario: scenario.Name,
            seed: scenario.Seed,
            width: scenario.Width,
            height: scenario.Height,
            depth: scenario.Depth);
        ShaderSnapshotCapture capture = harness.Capture(fixture, scenario.TickCount);
        ShaderSnapshotComparison comparison = ShaderSnapshotComparison.Create(expected, capture, maxDifferences: 32);

        Assert.True(comparison.Matches, string.Join(Environment.NewLine, comparison.Differences));
    }

    private static void AssertExpectedCaptureMetadata(ShaderSnapshotCapture capture, ReleaseShaderScenario scenario)
    {
        Assert.Equal(scenario.Name, capture.Scenario);
        Assert.Equal(scenario.Seed, capture.Seed);
        Assert.Equal(new ComputeGridDimensions(scenario.Width, scenario.Height, scenario.Depth), capture.Grid);
        Assert.Equal(scenario.TickCount, capture.TickCount);
    }

    private static ShaderSnapshotCapture LoadExpectedCapture(ReleaseShaderScenario scenario)
    {
        string path = Path.Combine(
            FindRepoRoot(),
            "tests/Wildfire.Core.Tests/ShaderSnapshots/release",
            scenario.ExpectedCaptureFile);
        return ShaderSnapshotJson.LoadFile(path);
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

    private sealed record ReleaseShaderScenario(
        string Name,
        uint Seed,
        int Width,
        int Height,
        int Depth,
        int TickCount,
        string ExpectedCaptureFile);
}
