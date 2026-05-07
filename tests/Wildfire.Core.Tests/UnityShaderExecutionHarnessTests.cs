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

    private static readonly FuelBurnDownScenario[] FuelBurnDownScenarios =
    [
        new(
            "low-fuel-burn-down",
            89,
            "low-fuel-burn-down-seed89-5x5x1.fixture.json",
            "low-fuel-burn-down-seed89-5x5x1-tick15.capture.json",
            [4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 3, 2, 1, 1, 0]),
        new(
            "medium-fuel-burn-down",
            89,
            "medium-fuel-burn-down-seed89-5x5x1.fixture.json",
            "medium-fuel-burn-down-seed89-5x5x1-tick17.capture.json",
            [9, 8, 7, 7, 7, 7, 6, 6, 6, 6, 6, 5, 4, 3, 2, 1, 0]),
        new(
            "high-fuel-burn-down",
            89,
            "high-fuel-burn-down-seed89-5x5x1.fixture.json",
            "high-fuel-burn-down-seed89-5x5x1-tick27.capture.json",
            [12, 11, 10, 10, 10, 10, 10, 10, 10, 10, 10, 9, 8, 8, 7, 6, 5, 4, 4, 3, 2, 2, 2, 2, 2, 1, 0]),
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
    public void AcceptedReleaseShaderSnapshotsReflectTunedSpreadPaceAndFuelDuration()
    {
        AssertScenarioSummary(
            "single-ignition",
            perTickDeltas: [5, 0],
            summary: new ShaderSemanticSummary(HotCells: 5, BurningCells: 1, MaxHeat: 13, WaterCells: 0, FuelTotal: 176));
        AssertScenarioSummary(
            "line-of-fuel",
            perTickDeltas: [7, 2, 1, 5],
            summary: new ShaderSemanticSummary(HotCells: 4, BurningCells: 1, MaxHeat: 12, WaterCells: 0, FuelTotal: 105));
        AssertScenarioSummary(
            "sparse-forest",
            perTickDeltas: [5, 0, 1],
            summary: new ShaderSemanticSummary(HotCells: 5, BurningCells: 1, MaxHeat: 13, WaterCells: 0, FuelTotal: 980));
        AssertScenarioSummary(
            "building-cluster",
            perTickDeltas: [1, 1, 1],
            summary: new ShaderSemanticSummary(HotCells: 1, BurningCells: 1, MaxHeat: 12, WaterCells: 0, FuelTotal: 1177));
        AssertScenarioSummary(
            "water-barrier",
            perTickDeltas: [5, 1, 1, 1],
            summary: new ShaderSemanticSummary(HotCells: 5, BurningCells: 1, MaxHeat: 12, WaterCells: 5, FuelTotal: 382));
        AssertScenarioSummary(
            "vertical-fuel-column",
            perTickDeltas: [1, 1, 1, 1],
            summary: new ShaderSemanticSummary(HotCells: 1, BurningCells: 1, MaxHeat: 13, WaterCells: 0, FuelTotal: 45));
    }

    [Fact]
    public void AcceptedFuelBurnDownSnapshotsCompareLowMediumAndHighFuelInputs()
    {
        int[] depletedTicks = FuelBurnDownScenarios
            .Select(AssertFuelBurnDownSummary)
            .ToArray();

        Assert.Equal([15, 17, 27], depletedTicks);
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

        foreach (FuelBurnDownScenario scenario in FuelBurnDownScenarios)
        {
            AssertFuelBurnDownScenario(harness, scenario);
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

    private static void AssertFuelBurnDownScenario(ShaderSnapshotHarness harness, FuelBurnDownScenario scenario)
    {
        ShaderSnapshotFixture fixture = LoadFuelBurnDownFixture(scenario);
        ShaderSnapshotCapture expected = LoadExpectedFuelBurnDownCapture(scenario);
        ShaderSnapshotCapture capture = harness.Capture(fixture, scenario.ExpectedFuelTotalsByTick.Length);
        ShaderSnapshotComparison comparison = ShaderSnapshotComparison.Create(expected, capture, maxDifferences: 32);

        Assert.True(comparison.Matches, string.Join(Environment.NewLine, comparison.Differences));
    }

    private static void AssertScenarioSummary(
        string scenarioName,
        int[] perTickDeltas,
        ShaderSemanticSummary summary)
    {
        ReleaseShaderScenario scenario = ReleaseScenarios.Single(releaseScenario => releaseScenario.Name == scenarioName);
        ShaderSnapshotCapture capture = LoadExpectedCapture(scenario);

        Assert.Equal(perTickDeltas, capture.Ticks.Select(static tick => tick.DeltaCount).ToArray());
        Assert.Equal(summary, ShaderSemanticSummary.Create(capture.FinalPackedCells));
    }

    private static int AssertFuelBurnDownSummary(FuelBurnDownScenario scenario)
    {
        ShaderSnapshotFixture fixture = LoadFuelBurnDownFixture(scenario);
        ShaderSnapshotCapture capture = LoadExpectedFuelBurnDownCapture(scenario);
        int[] fuelTotals = FuelTotalsByTick(fixture, capture);

        Assert.Equal(scenario.Name, capture.Scenario);
        Assert.Equal(scenario.Seed, capture.Seed);
        Assert.Equal(fixture.Grid, capture.Grid);
        Assert.Equal(scenario.ExpectedFuelTotalsByTick.Length, capture.TickCount);
        Assert.Equal(scenario.ExpectedFuelTotalsByTick, fuelTotals);
        Assert.Equal(0, fuelTotals[^1]);

        return Array.FindIndex(fuelTotals, static total => total == 0) + 1;
    }

    private static int[] FuelTotalsByTick(ShaderSnapshotFixture fixture, ShaderSnapshotCapture capture)
    {
        ushort[] cells = fixture.InitialCells.ToArray();

        return capture.Ticks
            .Select(tick =>
            {
                foreach (ShaderSnapshotDelta delta in tick.Deltas)
                {
                    cells[delta.CellIndex] = delta.NewCell;
                }

                return cells.Sum(static cell => PackedCell.Fuel(cell));
            })
            .ToArray();
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

    private static ShaderSnapshotFixture LoadFuelBurnDownFixture(FuelBurnDownScenario scenario)
    {
        string path = Path.Combine(
            FindRepoRoot(),
            "tests/Wildfire.Core.Tests/ShaderSnapshots/twf-089",
            scenario.FixtureFile);
        return ShaderSnapshotFixtureLoader.LoadFile(path);
    }

    private static ShaderSnapshotCapture LoadExpectedFuelBurnDownCapture(FuelBurnDownScenario scenario)
    {
        string path = Path.Combine(
            FindRepoRoot(),
            "tests/Wildfire.Core.Tests/ShaderSnapshots/twf-089",
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

    private sealed record FuelBurnDownScenario(
        string Name,
        uint Seed,
        string FixtureFile,
        string ExpectedCaptureFile,
        int[] ExpectedFuelTotalsByTick);

    private sealed record ShaderSemanticSummary(int HotCells, int BurningCells, int MaxHeat, int WaterCells, int FuelTotal)
    {
        public static ShaderSemanticSummary Create(ushort[] cells)
        {
            return cells
                .Select(ShaderCellFields.Create)
                .Aggregate(
                    new ShaderSemanticSummary(HotCells: 0, BurningCells: 0, MaxHeat: 0, WaterCells: 0, FuelTotal: 0),
                    static (summary, cell) => new ShaderSemanticSummary(
                        summary.HotCells + (cell.Heat > 0 ? 1 : 0),
                        summary.BurningCells + (cell.IsBurning ? 1 : 0),
                        Math.Max(summary.MaxHeat, cell.Heat),
                        summary.WaterCells + (cell.Water > 0 ? 1 : 0),
                        summary.FuelTotal + cell.Fuel));
        }
    }

    private sealed record ShaderCellFields(int Fuel, int Heat, int Flammability, int Water, int Terrain)
    {
        public bool IsBurning => Terrain == 1 && Fuel > 0 && Heat >= 11 - Flammability + (Water * 2);

        public static ShaderCellFields Create(ushort cell)
        {
            return new ShaderCellFields(
                Fuel: cell & 0xF,
                Heat: (cell >> 4) & 0xF,
                Flammability: (cell >> 8) & 0x3,
                Water: (cell >> 10) & 0x3,
                Terrain: (cell >> 12) & 0x1);
        }
    }
}
