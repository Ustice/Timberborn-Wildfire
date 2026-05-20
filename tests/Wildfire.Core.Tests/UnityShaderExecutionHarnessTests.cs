using Wildfire.Core;
using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed class UnityShaderExecutionHarnessTests
{
    private static readonly FuelBurnDownScenario[] FuelBurnDownScenarios =
    [
        new(
            "low-fuel-burn-down",
            89,
            "low-fuel-burn-down-seed89-5x5x1.fixture.json"),
        new(
            "medium-fuel-burn-down",
            89,
            "medium-fuel-burn-down-seed89-5x5x1.fixture.json"),
        new(
            "high-fuel-burn-down",
            89,
            "high-fuel-burn-down-seed89-5x5x1.fixture.json"),
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
    public void FuelBurnDownFixturesKeepSingleBurningSourceCoverage()
    {
        foreach (FuelBurnDownScenario scenario in FuelBurnDownScenarios)
        {
            ShaderSnapshotFixture fixture = LoadFuelBurnDownFixture(scenario);
            ShaderCellFields[] cells = fixture.InitialCells
                .Select(ShaderCellFields.Create)
                .ToArray();
            ShaderCellFields[] fueledTerrainCells = cells
                .Where(static cell => cell.Terrain == 1 && cell.Fuel > 0)
                .ToArray();

            Assert.Equal(scenario.Name, fixture.Scenario);
            Assert.Equal(scenario.Seed, fixture.Seed);
            Assert.Single(fueledTerrainCells);
            Assert.True(fueledTerrainCells[0].IsBurning);
        }
    }

    [Fact]
    public void UnityHarnessNoWindHeatKernelIsRadialWhenEnabled()
    {
        ShaderSnapshotCapture? capture = CaptureWhenUnityHarnessEnabled(CreateSingleHotSourceFixture(
            "field-model-no-wind-radial",
            width: 9,
            height: 9,
            wind: FireSimWind.None));
        if (capture is null)
        {
            return;
        }

        Assert.Equal(HeatAt(capture, 5, 4), HeatAt(capture, 3, 4));
        Assert.Equal(HeatAt(capture, 4, 5), HeatAt(capture, 4, 3));
        Assert.Equal(HeatAt(capture, 5, 5), HeatAt(capture, 3, 5));
        Assert.Equal(HeatAt(capture, 5, 5), HeatAt(capture, 5, 3));
        Assert.True(HeatAt(capture, 5, 4) > 0);
        Assert.True(HeatAt(capture, 6, 4) > 0);
        Assert.Equal(0, HeatAt(capture, 7, 4));
    }

    [Fact]
    public void UnityHarnessWindStretchesHeatDownwindWhenEnabled()
    {
        ShaderSnapshotCapture? capture = CaptureWhenUnityHarnessEnabled(CreateSingleHotSourceFixture(
            "field-model-wind-ellipse",
            width: 9,
            height: 9,
            wind: new FireSimWind(1f, 0f, 1f)));
        if (capture is null)
        {
            return;
        }

        int downwind = HeatAt(capture, 5, 4);
        int crosswind = HeatAt(capture, 4, 5);
        int upwind = HeatAt(capture, 3, 4);

        Assert.True(downwind > crosswind, $"Expected downwind {downwind} to exceed crosswind {crosswind}.");
        Assert.True(crosswind > upwind, $"Expected crosswind {crosswind} to exceed upwind {upwind}.");
        Assert.True(HeatAt(capture, 7, 4) > HeatAt(capture, 1, 4));
    }

    [Fact]
    public void UnityHarnessSingleIgnitionExpandsOverMultipleTicksWhenEnabled()
    {
        int width = 7;
        int height = 7;
        ushort[] cells = Enumerable.Range(0, width * height)
            .Select(index => index == ToIndex(3, 3, width)
                ? PackedCell.Pack(fuel: 15, heat: 15, flammability: 3, water: 0, terrain: 1, burningLevel: 7)
                : PackedCell.Pack(fuel: 15, heat: 0, flammability: 3, water: 0, terrain: 1, burningLevel: 0))
            .ToArray();
        ShaderSnapshotFixture fixture = CreateFixture(
            "field-model-slow-single-ignition",
            width,
            height,
            cells,
            wind: FireSimWind.None);

        ShaderSnapshotCapture? capture = CaptureWhenUnityHarnessEnabled(fixture, tickCount: 3);
        if (capture is null)
        {
            return;
        }

        int[] burningCounts = PackedCellsByTick(fixture, capture)
            .Select(cellsByTick => cellsByTick.Count(static cell => ShaderCellFields.Create(cell).IsBurning))
            .ToArray();

        Assert.InRange(burningCounts[0], 2, width * height - 1);
        Assert.True(burningCounts[1] > burningCounts[0], $"Expected tick 2 burning count {burningCounts[1]} to exceed tick 1 count {burningCounts[0]}.");
        Assert.True(burningCounts[2] > burningCounts[1], $"Expected tick 3 burning count {burningCounts[2]} to exceed tick 2 count {burningCounts[1]}.");
        Assert.True(burningCounts[0] < width * height / 2, $"Expected first tick not to fill the reachable area; burning count was {burningCounts[0]}.");
    }

    [Fact]
    public void UnityHarnessWaterMoistureSlowsIgnitionWhenEnabled()
    {
        int width = 9;
        int height = 5;
        ushort[] cells = CreateTerrainCells(width, height);
        cells[ToIndex(4, 2, width)] = PackedCell.Pack(fuel: 0, heat: 15, flammability: 0, water: 0, terrain: 1, burningLevel: 0);
        cells[ToIndex(5, 2, width)] = PackedCell.Pack(fuel: 15, heat: 0, flammability: 3, water: 0, terrain: 1, burningLevel: 0);
        cells[ToIndex(3, 2, width)] = PackedCell.Pack(fuel: 15, heat: 0, flammability: 3, water: 3, terrain: 1, burningLevel: 0);

        ShaderSnapshotCapture? capture = CaptureWhenUnityHarnessEnabled(CreateFixture(
            "field-model-water-slows-ignition",
            width,
            height,
            cells,
            wind: FireSimWind.None));
        if (capture is null)
        {
            return;
        }

        Assert.True(ShaderCellFields.Create(capture.FinalPackedCells[ToIndex(5, 2, width)]).IsBurning);
        Assert.False(ShaderCellFields.Create(capture.FinalPackedCells[ToIndex(3, 2, width)]).IsBurning);
        Assert.True(HeatAt(capture, 3, 2) > 0);
    }

    [Fact]
    public void UnityHarnessAtmosphericFieldsUseDirectionalTransportWhenEnabled()
    {
        int width = 7;
        int height = 5;
        ushort[] cells = CreateTerrainCells(width, height);
        uint[] atmosphericFields = new uint[cells.Length];
        atmosphericFields[ToIndex(3, 2, width)] = new WildfireTransportFieldState(
            Steam: 5,
            Smoke: 5,
            SmokeContamination: 0,
            Ash: 5,
            AshContamination: 0,
            Source: true).Pack();
        ShaderSnapshotFixture fixture = CreateFixture(
            "field-model-atmospheric-transport",
            width,
            height,
            cells,
            initialAtmosphericFields: atmosphericFields,
            wind: new FireSimWind(1f, 0f, 1f));

        ShaderSnapshotCapture? capture = CaptureWhenUnityHarnessEnabled(fixture);
        if (capture is null)
        {
            return;
        }

        WildfireTransportFieldState downwind = AtmosphereAt(capture, 4, 2);
        WildfireTransportFieldState crosswind = AtmosphereAt(capture, 3, 3);
        WildfireTransportFieldState upwind = AtmosphereAt(capture, 2, 2);

        Assert.True(downwind.Smoke > crosswind.Smoke, $"Expected downwind smoke {downwind.Smoke} to exceed crosswind {crosswind.Smoke}.");
        Assert.True(crosswind.Smoke > upwind.Smoke, $"Expected crosswind smoke {crosswind.Smoke} to exceed upwind {upwind.Smoke}.");
        Assert.True(downwind.Steam < downwind.Smoke, $"Expected steam {downwind.Steam} to decay faster than smoke {downwind.Smoke}.");
        Assert.True(downwind.Ash > downwind.Smoke, $"Expected ash {downwind.Ash} to persist longer than smoke {downwind.Smoke}.");
    }

    [Fact]
    public void UnityHarnessSmokeContaminationDilutesWhenCleanSmokeMixesWhenEnabled()
    {
        int width = 5;
        int height = 3;
        ushort[] cells = CreateTerrainCells(width, height);
        uint[] atmosphericFields = new uint[cells.Length];
        atmosphericFields[ToIndex(1, 1, width)] = new WildfireTransportFieldState(
            Steam: 0,
            Smoke: 7,
            SmokeContamination: 7,
            Ash: 0,
            AshContamination: 0,
            Source: false).Pack();
        atmosphericFields[ToIndex(2, 1, width)] = new WildfireTransportFieldState(
            Steam: 0,
            Smoke: 7,
            SmokeContamination: 0,
            Ash: 0,
            AshContamination: 0,
            Source: false).Pack();
        ShaderSnapshotFixture fixture = CreateFixture(
            "field-model-smoke-contamination-dilution",
            width,
            height,
            cells,
            initialAtmosphericFields: atmosphericFields,
            wind: FireSimWind.None);

        ShaderSnapshotCapture? capture = CaptureWhenUnityHarnessEnabled(fixture);
        if (capture is null)
        {
            return;
        }

        WildfireTransportFieldState mixedSmoke = AtmosphereAt(capture, 2, 1);

        Assert.True(mixedSmoke.Smoke > 0);
        Assert.InRange(mixedSmoke.SmokeContamination, 1, 6);
    }

    [Fact]
    public void UnityHarnessContaminationRidesSmokeAndAshWhenEnabled()
    {
        int width = 11;
        int height = 3;
        ushort[] cells = CreateTerrainCells(width, height);
        cells[ToIndex(1, 1, width)] = PackedCell.Pack(fuel: 5, heat: 5, flammability: 0, water: 0, terrain: 1, burningLevel: 0);

        uint[] atmosphericFields = new uint[cells.Length];
        atmosphericFields[ToIndex(5, 1, width)] = new WildfireTransportFieldState(
            Steam: 0,
            Smoke: 7,
            SmokeContamination: 7,
            Ash: 0,
            AshContamination: 0,
            Source: false).Pack();
        atmosphericFields[ToIndex(3, 1, width)] = new WildfireTransportFieldState(
            Steam: 0,
            Smoke: 7,
            SmokeContamination: 0,
            Ash: 7,
            AshContamination: 0,
            Source: false).Pack();
        atmosphericFields[ToIndex(9, 1, width)] = new WildfireTransportFieldState(
            Steam: 0,
            Smoke: 7,
            SmokeContamination: 0,
            Ash: 0,
            AshContamination: 0,
            Source: false).Pack();

        uint[] companionFields = new uint[cells.Length];
        companionFields[ToIndex(1, 1, width)] = ContaminatedCompanion();
        companionFields[ToIndex(3, 1, width)] = ContaminatedCompanion();

        ShaderSnapshotFixture fixture = CreateFixture(
            "field-model-contamination-carry",
            width,
            height,
            cells,
            initialAtmosphericFields: atmosphericFields,
            companionFields: companionFields,
            wind: FireSimWind.None);

        ShaderSnapshotCapture? capture = CaptureWhenUnityHarnessEnabled(fixture);
        if (capture is null)
        {
            return;
        }

        WildfireTransportFieldState contaminatedSmokeSource = AtmosphereAt(capture, 1, 1);
        WildfireTransportFieldState contaminatedSmokeDeposit = AtmosphereAt(capture, 5, 1);
        WildfireTransportFieldState taintedTransitCell = AtmosphereAt(capture, 3, 1);
        WildfireTransportFieldState cleanSmokeDeposit = AtmosphereAt(capture, 9, 1);

        Assert.True(contaminatedSmokeSource.Smoke > 0);
        Assert.True(contaminatedSmokeSource.SmokeContamination > 0);
        Assert.True(contaminatedSmokeDeposit.Ash > 0);
        Assert.True(contaminatedSmokeDeposit.AshContamination > 0);
        Assert.True(taintedTransitCell.Smoke > 0);
        Assert.True(taintedTransitCell.SmokeContamination > 0);
        Assert.True(taintedTransitCell.Ash > 0);
        Assert.True(taintedTransitCell.AshContamination > 0);
        Assert.True(cleanSmokeDeposit.Ash > 0);
        Assert.Equal(0, cleanSmokeDeposit.AshContamination);
    }

    [Fact]
    public void UnityHarnessAshFallsToOakBaseInsteadOfUpperTreeBlocksWhenEnabled()
    {
        AssertAshFallsToStackBaseInsteadOfUpperBlocks(
            "oak-stack-ash-falls-to-base",
            WildfireMaterialClass.Tree);
    }

    [Fact]
    public void UnityHarnessAshFallsToBuildingBaseInsteadOfUpperBuildingBlocksWhenEnabled()
    {
        AssertAshFallsToStackBaseInsteadOfUpperBlocks(
            "building-stack-ash-falls-to-base",
            WildfireMaterialClass.Building);
    }

    private static void AssertAshFallsToStackBaseInsteadOfUpperBlocks(string scenario, WildfireMaterialClass materialClass)
    {
        int width = 1;
        int height = 1;
        int depth = 3;
        ushort[] cells = Enumerable.Repeat(PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 0, burningLevel: 0), width * height * depth)
            .ToArray();
        uint[] atmosphericFields = new uint[cells.Length];
        atmosphericFields[ToIndex(0, 0, 2, width, height)] = new WildfireTransportFieldState(
            Steam: 0,
            Smoke: 0,
            SmokeContamination: 0,
            Ash: 7,
            AshContamination: 0,
            Source: false).Pack();
        uint[] companionFields = Enumerable.Repeat(Companion(materialClass), cells.Length)
            .ToArray();

        ShaderSnapshotFixture fixture = CreateFixture(
            scenario,
            width,
            height,
            cells,
            depth: depth,
            initialAtmosphericFields: atmosphericFields,
            companionFields: companionFields,
            wind: FireSimWind.None);

        ShaderSnapshotCapture? capture = CaptureWhenUnityHarnessEnabled(fixture);
        if (capture is null)
        {
            return;
        }

        WildfireMaterialFieldState baseTree = CompanionAt(capture, 0, 0, 0);
        WildfireMaterialFieldState middleTree = CompanionAt(capture, 0, 0, 1);
        WildfireMaterialFieldState topTree = CompanionAt(capture, 0, 0, 2);

        Assert.Equal(materialClass, baseTree.MaterialClass);
        Assert.Equal(3, baseTree.AshStrength & 0x3);
        Assert.Equal(materialClass, middleTree.MaterialClass);
        Assert.Equal(0, middleTree.AshStrength & 0x3);
        Assert.Equal(materialClass, topTree.MaterialClass);
        Assert.Equal(0, topTree.AshStrength & 0x3);
    }

    [Fact]
    public void UnityHarnessFuelBurnDownCoverageRunsFromFixturesWhenEnabled()
    {
        FuelBurnDownResult[] results = FuelBurnDownScenarios
            .Select(CaptureFuelBurnDownResultWhenEnabled)
            .OfType<FuelBurnDownResult>()
            .ToArray();

        if (results.Length == 0)
        {
            return;
        }

        Assert.Equal(FuelBurnDownScenarios.Length, results.Length);
        Assert.True(results[0].FuelTotals[0] < results[1].FuelTotals[0]);
        Assert.True(results[1].FuelTotals[0] < results[2].FuelTotals[0]);
        Assert.All(results, static result => Assert.True(result.FuelTotals[0] > 0));
        Assert.True(results[1].FuelTotals[^1] > 0, "Expected medium fuel not to be consumed within five ticks.");
        Assert.True(results[2].FuelTotals[^1] > 0, "Expected high fuel not to be consumed within five ticks.");
    }

    private static FuelBurnDownResult? CaptureFuelBurnDownResultWhenEnabled(FuelBurnDownScenario scenario)
    {
        ShaderSnapshotFixture fixture = LoadFuelBurnDownFixture(scenario);
        ShaderSnapshotCapture? capture = CaptureWhenUnityHarnessEnabled(fixture, tickCount: 5);
        if (capture is null)
        {
            return null;
        }

        int[] fuelTotals = FuelTotalsByTick(fixture, capture);
        return new FuelBurnDownResult(scenario.Name, fuelTotals, Array.FindIndex(fuelTotals, static total => total == 0) + 1);
    }

    private static ShaderSnapshotCapture? CaptureWhenUnityHarnessEnabled(ShaderSnapshotFixture fixture, int tickCount = 1)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("WILDFIRE_RUN_UNITY_SHADER_HARNESS"), "1", StringComparison.Ordinal))
        {
            return null;
        }

        string repoRoot = FindRepoRoot();
        string unityExecutable = Environment.GetEnvironmentVariable("WILDFIRE_UNITY_EXECUTABLE")
            ?? "/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity";
        ShaderSnapshotHarness harness = new(new UnityBatchmodeShaderSnapshotExecutor(new UnityBatchmodeShaderSnapshotExecutorOptions(
            UnityExecutablePath: unityExecutable,
            ProjectPath: Path.Combine(repoRoot, "src/Wildfire.Unity/UnityBatchmodeProject"),
            ComputeShaderPath: Path.Combine(repoRoot, "src/Wildfire.Unity/FireSim.compute"),
            Timeout: TimeSpan.FromMinutes(5))));

        return harness.Capture(fixture, tickCount);
    }

    private static ShaderSnapshotFixture CreateSingleHotSourceFixture(
        string scenario,
        int width,
        int height,
        FireSimWind wind)
    {
        ushort[] cells = CreateTerrainCells(width, height);
        cells[ToIndex(width / 2, height / 2, width)] = PackedCell.Pack(
            fuel: 0,
            heat: 15,
            flammability: 0,
            water: 0,
            terrain: 1,
            burningLevel: 0);

        return CreateFixture(scenario, width, height, cells, wind: wind);
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

    private static ushort[][] PackedCellsByTick(ShaderSnapshotFixture fixture, ShaderSnapshotCapture capture)
    {
        ushort[] cells = fixture.InitialCells.ToArray();
        return capture.Ticks
            .Select(tick =>
            {
                cells = cells.ToArray();
                tick.Deltas
                    .ToList()
                    .ForEach(delta => cells[delta.CellIndex] = delta.NewCell);
                return cells;
            })
            .ToArray();
    }

    private static ShaderSnapshotFixture LoadFuelBurnDownFixture(FuelBurnDownScenario scenario)
    {
        string path = Path.Combine(
            FindRepoRoot(),
            "tests/Wildfire.Core.Tests/ShaderSnapshots/twf-089",
            scenario.FixtureFile);
        return ShaderSnapshotFixtureLoader.LoadFile(path);
    }

    private static ShaderSnapshotFixture CreateFixture(
        string scenario,
        int width,
        int height,
        ushort[] cells,
        int depth = 1,
        uint[]? initialAtmosphericFields = null,
        uint[]? companionFields = null,
        FireSimWind? wind = null)
    {
        Assert.Equal(width * height * depth, cells.Length);
        return new ShaderSnapshotFixture(
            FormatVersion: 1,
            Scenario: scenario,
            Seed: 1,
            Grid: new ComputeGridDimensions(width, height, depth),
            SelectedLayer: new ShaderSnapshotLayer(0, 0, width * height),
            InitialCells: cells,
            InitialAtmosphericFields: initialAtmosphericFields,
            CompanionFields: companionFields,
            Wind: wind);
    }

    private static ushort[] CreateTerrainCells(int width, int height)
    {
        return Enumerable.Repeat(
                PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 1, burningLevel: 0),
                width * height)
            .ToArray();
    }

    private static int HeatAt(ShaderSnapshotCapture capture, int x, int y)
    {
        return ShaderCellFields.Create(capture.FinalPackedCells[ToIndex(x, y, capture.Grid.Width)]).Heat;
    }

    private static WildfireTransportFieldState AtmosphereAt(ShaderSnapshotCapture capture, int x, int y)
    {
        Assert.NotNull(capture.FinalAtmosphericFields);
        return WildfireTransportFieldState.Unpack(capture.FinalAtmosphericFields[ToIndex(x, y, capture.Grid.Width)]);
    }

    private static WildfireMaterialFieldState CompanionAt(ShaderSnapshotCapture capture, int x, int y, int z)
    {
        Assert.NotNull(capture.FinalCompanionFields);
        return WildfireMaterialFieldState.Unpack(capture.FinalCompanionFields[ToIndex(x, y, z, capture.Grid.Width, capture.Grid.Height)]);
    }

    private static int ToIndex(int x, int y, int width)
    {
        return x + (y * width);
    }

    private static int ToIndex(int x, int y, int z, int width, int height)
    {
        return x + (y * width) + (z * width * height);
    }

    private static uint Companion(WildfireMaterialClass materialClass)
    {
        return new WildfireMaterialFieldState(
            materialClass,
            BurnCapacity: 0,
            BurnHistory: 0,
            AshStrength: 0,
            WildfireAshQuality.Fertile,
            WildfireContaminationBehavior.None).Pack();
    }

    private static uint ContaminatedCompanion()
    {
        return new WildfireMaterialFieldState(
            WildfireMaterialClass.Badwater,
            BurnCapacity: 0,
            BurnHistory: 0,
            AshStrength: 0,
            WildfireAshQuality.Tainted,
            WildfireContaminationBehavior.TaintedSource).Pack();
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

    private sealed record FuelBurnDownScenario(
        string Name,
        uint Seed,
        string FixtureFile);

    private sealed record FuelBurnDownResult(string Name, int[] FuelTotals, int DepletedTick);

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
