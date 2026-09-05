using Wildfire.Core;
using Wildfire.Unity;
using static Wildfire.Shader.Tests.UnityShaderHarness;

namespace Wildfire.Shader.Tests;

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

    [UnityShaderFact]
    public void UnityHarnessNoWindHeatKernelIsRadialWhenEnabled()
    {
        ShaderSnapshotCapture capture = Capture(CreateSingleHotSourceFixture(
            "field-model-no-wind-radial",
            width: 9,
            height: 9,
            wind: FireSimWind.None));
        Assert.Equal(HeatAt(capture, 5, 4), HeatAt(capture, 3, 4));
        Assert.Equal(HeatAt(capture, 4, 5), HeatAt(capture, 4, 3));
        Assert.Equal(HeatAt(capture, 5, 5), HeatAt(capture, 3, 5));
        Assert.Equal(HeatAt(capture, 5, 5), HeatAt(capture, 5, 3));
        Assert.True(HeatAt(capture, 5, 4) > 0);
        Assert.True(HeatAt(capture, 6, 4) > 0);
        Assert.Equal(0, HeatAt(capture, 7, 4));
    }

    [UnityShaderFact]
    public void UnityHarnessWindBiasesHeatDownwindWhenEnabled()
    {
        ShaderSnapshotCapture capture = Capture(CreateSingleHotSourceFixture(
            "field-model-wind-ellipse",
            width: 9,
            height: 9,
            wind: new FireSimWind(1f, 0f, 1f)));
        int downwind = HeatAt(capture, 5, 4);
        int crosswind = HeatAt(capture, 4, 5);
        int upwind = HeatAt(capture, 3, 4);

        Assert.True(downwind > upwind, $"Expected downwind {downwind} to exceed upwind {upwind}.");
        Assert.True(crosswind > upwind, $"Expected crosswind {crosswind} to exceed upwind {upwind}.");
        // Four-bit heat can round adjacent downwind and crosswind samples to
        // the same value. Test the field's directional bias and lateral balance.
        int downwindMoment = capture.FinalPackedCells
            .Select((cell, index) => (index % 9 - 4) * ShaderCellFields.Create(cell).Heat)
            .Sum();
        int crosswindMoment = capture.FinalPackedCells
            .Select((cell, index) => (index / 9 - 4) * ShaderCellFields.Create(cell).Heat)
            .Sum();
        Assert.True(downwindMoment > 0, $"Expected positive downwind heat moment, got {downwindMoment}.");
        Assert.Equal(0, crosswindMoment);
    }

    [UnityShaderFact]
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

        ShaderSnapshotCapture capture = Capture(fixture, tickCount: 3);
        int[] burningCounts = PackedCellsByTick(fixture, capture)
            .Select(cellsByTick => cellsByTick.Count(static cell => ShaderCellFields.Create(cell).IsBurning))
            .ToArray();

        Assert.InRange(burningCounts[0], 2, width * height - 1);
        Assert.True(burningCounts[1] > burningCounts[0], $"Expected tick 2 burning count {burningCounts[1]} to exceed tick 1 count {burningCounts[0]}.");
        Assert.True(burningCounts[2] > burningCounts[1], $"Expected tick 3 burning count {burningCounts[2]} to exceed tick 2 count {burningCounts[1]}.");
        Assert.True(burningCounts[0] < width * height / 2, $"Expected first tick not to fill the reachable area; burning count was {burningCounts[0]}.");
    }

    [UnityShaderFact]
    public void UnityHarnessWaterMoistureSlowsIgnitionWhenEnabled()
    {
        int width = 9;
        int height = 5;
        ushort[] cells = CreateTerrainCells(width, height);
        cells[ToIndex(4, 2, width)] = PackedCell.Pack(fuel: 0, heat: 15, flammability: 0, water: 0, terrain: 1, burningLevel: 0);
        cells[ToIndex(5, 2, width)] = PackedCell.Pack(fuel: 15, heat: 0, flammability: 3, water: 0, terrain: 1, burningLevel: 0);
        cells[ToIndex(3, 2, width)] = PackedCell.Pack(fuel: 15, heat: 0, flammability: 3, water: 3, terrain: 1, burningLevel: 0);

        ShaderSnapshotCapture capture = Capture(CreateFixture(
            "field-model-water-slows-ignition",
            width,
            height,
            cells,
            wind: FireSimWind.None));
        Assert.True(ShaderCellFields.Create(capture.FinalPackedCells[ToIndex(5, 2, width)]).IsBurning);
        Assert.False(ShaderCellFields.Create(capture.FinalPackedCells[ToIndex(3, 2, width)]).IsBurning);
        Assert.True(HeatAt(capture, 3, 2) > 0);
    }

    [UnityShaderFact]
    public void UnityHarnessWaterDoesNotWashAshWithoutExternalMutationWhenEnabled()
    {
        ushort[] cells =
        [
            PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 3, terrain: 1, burningLevel: 0),
        ];
        uint[] transportFields =
        [
            new WildfireTransportFieldState(
                Steam: 0,
                Smoke: 0,
                SmokeContamination: 0,
                Ash: 1,
                AshContamination: 7,
                Source: false).Pack(),
        ];
        ShaderSnapshotCapture capture = Capture(CreateFixture(
            "ash-water-no-inline-shader-washout",
            width: 1,
            height: 1,
            cells,
            initialAtmosphericFields: transportFields),
            tickCount: 64);
        WildfireTransportFieldState finalState = AtmosphereAt(capture, 0, 0);
        Assert.Equal(1, finalState.Ash);
        Assert.Equal(7, finalState.AshContamination);
    }

    [UnityShaderFact]
    public void UnityHarnessAtmosphericFieldsUseDirectionalTransportWhenEnabled()
    {
        int width = 7;
        int height = 5;
        int depth = 2;
        ushort[] cells = CreateAirCells(width, height, depth);
        uint[] atmosphericFields = new uint[cells.Length];
        atmosphericFields[ToIndex(2, 2, 0, width, height)] = new WildfireTransportFieldState(
            Steam: 7,
            Smoke: 7,
            SmokeContamination: 0,
            Ash: 0,
            AshContamination: 0,
            Source: false).Pack();
        ShaderSnapshotFixture fixture = CreateFixture(
            "field-model-atmospheric-transport",
            width,
            height,
            cells,
            depth: depth,
            initialAtmosphericFields: atmosphericFields,
            wind: new FireSimWind(1f, 0f, 1f));

        ShaderSnapshotCapture capture = Capture(fixture);
        // Wind carries both fields horizontally while lifting them along the vertical z axis.
        WildfireTransportFieldState downwind = AtmosphereAt(capture, 4, 2, 1);
        WildfireTransportFieldState crosswind = AtmosphereAt(capture, 2, 4, 1);

        Assert.True(downwind.Smoke > crosswind.Smoke, $"Expected downwind smoke {downwind.Smoke} to exceed crosswind {crosswind.Smoke}.");
        Assert.True(downwind.Steam > crosswind.Steam, $"Expected downwind steam {downwind.Steam} to exceed crosswind {crosswind.Steam}.");
        Assert.Equal(0, downwind.SmokeContamination);
    }

    [UnityShaderFact]
    public void UnityHarnessSteamDecaysBeforeSmokeAndDepositedAshPersistsWhenEnabled()
    {
        // A sealed landing surface separates local decay from wind and boundary transport.
        uint[] atmosphericFields =
        [
            new WildfireTransportFieldState(
                Steam: 7, Smoke: 7, SmokeContamination: 0,
                Ash: 3, AshContamination: 0, Source: false).Pack(),
        ];
        ShaderSnapshotCapture capture = Capture(CreateFixture(
            "field-model-atmospheric-decay",
            1,
            1,
            CreateTerrainCells(1, 1),
            initialAtmosphericFields: atmosphericFields,
            wind: FireSimWind.None), tickCount: 5);
        WildfireTransportFieldState source = AtmosphereAt(capture, 0, 0);

        Assert.Equal(0, source.Steam);
        Assert.Equal(2, source.Smoke);
        Assert.Equal(3, source.Ash);
    }

    [UnityShaderFact]
    public void UnityHarnessSmokeCanFallFromUpperLayerWhenEnabled()
    {
        int width = 3;
        int height = 3;
        int depth = 3;
        ushort[] cells = Enumerable.Repeat(
                PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 0, burningLevel: 0),
                width * height * depth)
            .ToArray();
        uint[] atmosphericFields = Enumerable.Range(0, cells.Length)
            .Select(index =>
            {
                int z = index / (width * height);
                return z == 2
                    ? new WildfireTransportFieldState(
                        Steam: 0,
                        Smoke: 7,
                        SmokeContamination: 0,
                        Ash: 0,
                        AshContamination: 0,
                        Source: false).Pack()
                    : 0u;
            })
            .ToArray();
        ShaderSnapshotFixture fixture = CreateFixture(
            "field-model-smoke-falls-from-upper-layer",
            width,
            height,
            cells,
            depth: depth,
            initialAtmosphericFields: atmosphericFields,
            wind: FireSimWind.None);

        ShaderSnapshotCapture capture = Capture(fixture);
        WildfireTransportFieldState middleLayer = AtmosphereAt(capture, 1, 1, 1);
        WildfireTransportFieldState upperLayer = AtmosphereAt(capture, 1, 1, 2);

        Assert.True(middleLayer.Smoke > 0, $"Expected smoke to fall into the middle layer, got {middleLayer.Smoke}.");
        Assert.True(upperLayer.Smoke < 7, $"Expected upper layer smoke to move or decay, got {upperLayer.Smoke}.");
    }

    [UnityShaderFact]
    public void UnityHarnessSteamComesFromWetHotSimulatorStateWhenEnabled()
    {
        int width = 3;
        int height = 3;
        ushort[] cells = CreateTerrainCells(width, height);
        cells[ToIndex(1, 1, width)] = PackedCell.Pack(
            fuel: 0,
            heat: 15,
            flammability: 0,
            water: 3,
            terrain: 1,
            burningLevel: 0);
        ShaderSnapshotFixture fixture = CreateFixture(
            "field-model-clean-steam-source",
            width,
            height,
            cells,
            wind: FireSimWind.None);

        ShaderSnapshotCapture capture = Capture(fixture);
        WildfireTransportFieldState wetHotCell = AtmosphereAt(capture, 1, 1);

        Assert.True(wetHotCell.Steam > 0, $"Expected wet hot simulator state to create steam, got {wetHotCell.Steam}.");
        Assert.Equal(0, wetHotCell.SmokeContamination);
    }

    [UnityShaderFact]
    public void UnityHarnessConvergingSteamTransportAccumulatesWhenEnabled()
    {
        int width = 5;
        int height = 5;
        int depth = 3;
        ushort[] cells = CreateTerrainCells(width, height, depth);
        uint[] atmosphericFields = new uint[cells.Length];
        // Interior walls leave each source one open destination and prevent off-grid sinks.
        foreach (int x in new[] { 1, 2, 3 })
        {
            cells[ToIndex(x, 2, 1, width, height)] = 0;
        }

        foreach (int x in new[] { 1, 3 })
        {
            atmosphericFields[ToIndex(x, 2, 1, width, height)] = new WildfireTransportFieldState(
                Steam: 7,
                Smoke: 0,
                SmokeContamination: 0,
                Ash: 0,
                AshContamination: 0,
                Source: false).Pack();
        }

        ShaderSnapshotFixture fixture = CreateFixture(
            "field-model-clean-steam-convergence",
            width,
            height,
            cells,
            depth: depth,
            initialAtmosphericFields: atmosphericFields,
            wind: FireSimWind.None) with
        {
            // Both sources emit on the first dispatch with this fixed stochastic fixture seed.
            Seed = 140,
        };

        ShaderSnapshotCapture capture = Capture(fixture);
        WildfireTransportFieldState leftSource = AtmosphereAt(capture, 1, 2, 1);
        WildfireTransportFieldState rightSource = AtmosphereAt(capture, 3, 2, 1);
        WildfireTransportFieldState convergenceTarget = AtmosphereAt(capture, 2, 2, 1);

        Assert.Equal(4, leftSource.Steam);
        Assert.Equal(4, rightSource.Steam);
        Assert.Equal(2, convergenceTarget.Steam);
    }

    [UnityShaderFact]
    public void UnityHarnessSmokeContaminationDilutesWhenCleanSmokeMixesWhenEnabled()
    {
        int width = 5;
        int height = 3;
        ushort[] cells = CreateAirCells(width, height);
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

        ShaderSnapshotCapture capture = Capture(fixture);
        WildfireTransportFieldState mixedSmoke = AtmosphereAt(capture, 2, 1);

        Assert.True(mixedSmoke.Smoke > 0);
        Assert.InRange(mixedSmoke.SmokeContamination, 1, 6);
    }

    [UnityShaderFact]
    public void UnityHarnessContaminationRidesSmokeAndAshWhenEnabled()
    {
        int width = 11;
        int height = 3;
        ushort[] cells = CreateTerrainCells(width, height);
        cells[ToIndex(1, 1, width)] = PackedCell.Pack(fuel: 5, heat: 5, flammability: 0, water: 0, terrain: 1, burningLevel: 0);
        // Fresh tainted emissions mix into existing clean smoke; static material alone is not smoke.
        cells[ToIndex(3, 1, width)] = cells[ToIndex(1, 1, width)];

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
            Ash: 3,
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

        ShaderSnapshotCapture capture = Capture(fixture);
        WildfireTransportFieldState contaminatedSmokeSource = AtmosphereAt(capture, 1, 1);
        WildfireTransportFieldState contaminatedSmokeDeposit = AtmosphereAt(capture, 5, 1);
        WildfireTransportFieldState taintedMixedSource = AtmosphereAt(capture, 3, 1);
        WildfireTransportFieldState cleanSmokeDeposit = AtmosphereAt(capture, 9, 1);

        Assert.True(contaminatedSmokeSource.Smoke > 0);
        Assert.True(contaminatedSmokeSource.SmokeContamination > 0);
        Assert.True(contaminatedSmokeDeposit.Ash > 0);
        Assert.True(contaminatedSmokeDeposit.AshContamination > 0);
        Assert.True(taintedMixedSource.Smoke > 0);
        Assert.True(taintedMixedSource.SmokeContamination > 0);
        Assert.True(taintedMixedSource.Ash > 0);
        Assert.True(taintedMixedSource.AshContamination > 0);
        Assert.True(cleanSmokeDeposit.Ash > 0);
        Assert.Equal(0, cleanSmokeDeposit.AshContamination);
    }

    [UnityShaderFact]
    public void UnityHarnessAshFallsToOakBaseInsteadOfUpperTreeBlocksWhenEnabled()
    {
        AssertAshFallsToStackBaseInsteadOfUpperBlocks(
            "oak-stack-ash-falls-to-base",
            WildfireMaterialClass.Tree);
    }

    [UnityShaderFact]
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
            Ash: 3,
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

        // Ash descends one z layer per dispatch before resting at the entity base.
        ShaderSnapshotCapture capture = Capture(fixture, tickCount: 2);
        WildfireMaterialFieldState baseTree = CompanionAt(capture, 0, 0, 0);
        WildfireMaterialFieldState middleTree = CompanionAt(capture, 0, 0, 1);
        WildfireMaterialFieldState topTree = CompanionAt(capture, 0, 0, 2);

        Assert.Equal(materialClass, baseTree.MaterialClass);
        Assert.Equal(3, AtmosphereAt(capture, 0, 0, 0).Ash);
        Assert.Equal(materialClass, middleTree.MaterialClass);
        Assert.Equal(0, AtmosphereAt(capture, 0, 0, 1).Ash);
        Assert.Equal(materialClass, topTree.MaterialClass);
        Assert.Equal(0, AtmosphereAt(capture, 0, 0, 2).Ash);
        Assert.Equal(companionFields, capture.FinalCompanionFields);
    }

    [UnityShaderFact]
    public void UnityHarnessFuelBurnDownCoverageRunsFromFixturesWhenEnabled()
    {
        FuelBurnDownResult[] results = FuelBurnDownScenarios
            .Select(CaptureFuelBurnDownResult)
            .ToArray();

        Assert.Equal(FuelBurnDownScenarios.Length, results.Length);
        Assert.True(results[0].FuelTotals[0] < results[1].FuelTotals[0]);
        Assert.True(results[1].FuelTotals[0] < results[2].FuelTotals[0]);
        Assert.All(results, static result => Assert.True(result.FuelTotals[0] > 0));
        Assert.True(results[1].FuelTotals[^1] > 0, "Expected medium fuel not to be consumed within five ticks.");
        Assert.True(results[2].FuelTotals[^1] > 0, "Expected high fuel not to be consumed within five ticks.");
    }

    private static FuelBurnDownResult CaptureFuelBurnDownResult(FuelBurnDownScenario scenario)
    {
        ShaderSnapshotFixture fixture = LoadFuelBurnDownFixture(scenario);
        ShaderSnapshotCapture capture = Capture(fixture, tickCount: 5);
        int[] fuelTotals = FuelTotalsByTick(fixture, capture);
        return new FuelBurnDownResult(scenario.Name, fuelTotals, Array.FindIndex(fuelTotals, static total => total == 0) + 1);
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

    private static ushort[] CreateTerrainCells(int width, int height, int depth = 1)
    {
        return Enumerable.Repeat(
                PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 1, burningLevel: 0),
                width * height * depth)
            .ToArray();
    }

    private static ushort[] CreateAirCells(int width, int height, int depth = 1)
    {
        return Enumerable.Repeat(
                PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 0, burningLevel: 0),
                width * height * depth)
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

    private static WildfireTransportFieldState AtmosphereAt(ShaderSnapshotCapture capture, int x, int y, int z)
    {
        Assert.NotNull(capture.FinalAtmosphericFields);
        return WildfireTransportFieldState.Unpack(capture.FinalAtmosphericFields[ToIndex(x, y, z, capture.Grid.Width, capture.Grid.Height)]);
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
