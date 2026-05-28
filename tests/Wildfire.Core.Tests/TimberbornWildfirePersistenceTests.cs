using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class TimberbornWildfirePersistenceTests
{
    [Fact]
    public void CodecRoundTripsFireSimAshAndConsequenceState()
    {
        TimberbornWildfirePersistenceSnapshot snapshot = new(
            TimberbornWildfirePersistenceSnapshot.CurrentPersistenceVersion,
            new TimberbornFireSimPersistenceSnapshot(
                Width: 2,
                Height: 1,
                Depth: 1,
                Tick: 42,
                Cells:
                [
                    PackedCell.Pack(fuel: 8, heat: 4, flammability: 3, water: 0, terrain: 1, burningLevel: 1),
                    PackedCell.Pack(fuel: 3, heat: 7, flammability: 2, water: 1, terrain: 1, burningLevel: 2),
                ],
                TransportFields: [1u, 2u]),
            new TimberbornAshFieldSnapshot(
                TimberbornAshFieldEntry.CurrentPersistenceVersion,
                [
                    new TimberbornAshFieldEntry(
                        CellIndex: 1,
                        WildfireAshQuality.Fertile,
                        Strength: 80,
                        TimberbornAshSourceKind.Tree,
                        CreatedTick: 40,
                        UpdatedTick: 42,
                        TimberbornAshFieldEntry.CurrentPersistenceVersion),
                ]),
            new TimberbornBeaverFieldBehaviorSnapshot(
                TimberbornBeaverFieldBehaviorSnapshot.CurrentPersistenceVersion,
                [
                    new TimberbornBeaverFieldBehaviorStateEntry(
                        TimberbornBeaverFieldBehaviorStateEntry.CurrentPersistenceVersion,
                        "beaver:1",
                        TimberbornBeaverFieldBehaviorVariant.FireHeat,
                        TimberbornBeaverFieldBehaviorAction.NoOp,
                        LastDecisionTick: 40,
                        ConsecutiveExposedSamples: 2,
                        ConsecutiveFireHeatExposedSamples: 2,
                        IsExposed: true),
                ]),
            new TimberbornConsequencePersistenceSnapshot(
                [
                    new TimberbornBurnDamagePersistenceEntry("tree:pine:1", DamageTaken: 12, LastDamagedTick: 42),
                ]));

        TimberbornWildfirePersistenceSnapshot roundTrip =
            TimberbornWildfirePersistenceCodec.Decode(TimberbornWildfirePersistenceCodec.Encode(snapshot));

        Assert.NotNull(roundTrip.FireSim);
        Assert.Equal(42u, roundTrip.FireSim.Tick);
        Assert.Equal(snapshot.FireSim!.Cells, roundTrip.FireSim.Cells);
        Assert.Equal(snapshot.FireSim.TransportFields, roundTrip.FireSim.TransportFields);
        TimberbornAshFieldEntry ashEntry = Assert.Single(roundTrip.AshField.Entries);
        Assert.Equal(WildfireAshQuality.Fertile, ashEntry.Quality);
        Assert.Equal(80, ashEntry.Strength);
        TimberbornBeaverFieldBehaviorStateEntry behaviorEntry = Assert.Single(roundTrip.BeaverBehavior.Entries);
        Assert.Equal("beaver:1", behaviorEntry.BeaverId);
        Assert.Equal(TimberbornBeaverFieldBehaviorVariant.FireHeat, behaviorEntry.LastVariant);
        Assert.Equal(2, behaviorEntry.ConsecutiveFireHeatExposedSamples);
        Assert.True(behaviorEntry.IsExposed);
        TimberbornBurnDamagePersistenceEntry burnEntry = Assert.Single(roundTrip.Consequences.BurnDamageStates);
        Assert.Equal("tree:pine:1", burnEntry.TargetKey);
        Assert.Equal(12, burnEntry.DamageTaken);
    }

    [Fact]
    public void RestoreConsequencesAppliesDamageToMatchingRegisteredTargetsOnly()
    {
        FireGrid grid = new(2, 1, 1);
        TimberbornBurnDamageService service = CreateService(
            TreeDescriptor("Tree.Pine", "Log", amount: 2),
            TreeDescriptor("Tree.Oak", "Log", amount: 2));
        TimberbornBurnDamageTargetKey pineKey = new("tree-pine-1");
        TimberbornBurnDamageTargetKey oakKey = new("tree-oak-1");
        service.RegisterTargets(
            grid,
            [
                Registration(pineKey, "Tree.Pine", [new TimberbornCellCoordinates(0, 0, 0)]),
                Registration(oakKey, "Tree.Oak", [new TimberbornCellCoordinates(1, 0, 0)]),
            ]);

        TimberbornWildfirePersistenceCodec.RestoreConsequences(
            service,
            new TimberbornConsequencePersistenceSnapshot(
                [
                    new TimberbornBurnDamagePersistenceEntry("tree-pine-1", DamageTaken: 9, LastDamagedTick: 77),
                    new TimberbornBurnDamagePersistenceEntry("missing-target", DamageTaken: 12, LastDamagedTick: 88),
                ]));

        Assert.Equal(9, service.States[pineKey].DamageTaken);
        Assert.Equal(77u, service.States[pineKey].LastDamagedTick);
        Assert.Equal(0, service.States[oakKey].DamageTaken);
    }

    [Fact]
    public void FireSystemRestoreClearsPersistedFireFromCurrentNoFuelCells()
    {
        FireGrid grid = new(2, 1, 1);
        RecordingPersistenceSimulator simulator = new(grid.Width, grid.Height, grid.Depth);
        TimberbornFireSystem fireSystem = new(
            new RecordingPersistenceSimulatorFactory(simulator),
            new TimberbornFireCellMapper(),
            NullTimberbornFireLogSink.Instance);
        TimberbornTerrainAdapter terrainAdapter = new();
        TimberbornResourceAdapter resourceAdapter = new();
        TimberbornCellSource[] currentSources =
        [
            terrainAdapter.CreateSource(0, 0, 0, isSolid: true),
            resourceAdapter.CreateTreeSource(0, 0, 0, "Pine"),
            terrainAdapter.CreateSource(1, 0, 0, isSolid: true),
        ];
        WildfireMaterialField[] materialFields = new TimberbornFireCellMapper().CreateMaterialFields(
            grid,
            currentSources);
        ushort validTreeBurningCell = PackedCell.Pack(
            fuel: 7,
            heat: 15,
            flammability: 3,
            water: 0,
            terrain: 1,
            burningLevel: 3);
        ushort staleStumpBurningCell = PackedCell.Pack(
            fuel: 8,
            heat: 15,
            flammability: 3,
            water: 0,
            terrain: 1,
            burningLevel: 2);
        TimberbornFireSimPersistenceSnapshot snapshot = new(
            Width: 2,
            Height: 1,
            Depth: 1,
            Tick: 12,
            Cells: [validTreeBurningCell, staleStumpBurningCell],
            TransportFields: [4u, 5u]);

        fireSystem.InitializeFromPersistentFireSimState(grid, currentSources, materialFields, snapshot);

        Assert.NotNull(simulator.RestoredSnapshot);
        Assert.Equal(12u, simulator.RestoredSnapshot.Tick);
        Assert.Equal(validTreeBurningCell, simulator.RestoredSnapshot.Cells[0]);
        Assert.Equal(
            PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 1, burningLevel: 0),
            simulator.RestoredSnapshot.Cells[1]);
        Assert.Equal([4u, 5u], simulator.RestoredSnapshot.TransportFields);
        Assert.Equal(1, fireSystem.LastPersistentRestoreNoLiveFuelCellsCleared);
    }

    [Fact]
    public void FireSystemRestorePreservesPersistedFireOnCurrentVegetationFuelCells()
    {
        FireGrid grid = new(2, 1, 1);
        RecordingPersistenceSimulator simulator = new(grid.Width, grid.Height, grid.Depth);
        TimberbornFireSystem fireSystem = new(
            new RecordingPersistenceSimulatorFactory(simulator),
            new TimberbornFireCellMapper(),
            NullTimberbornFireLogSink.Instance);
        TimberbornTerrainAdapter terrainAdapter = new();
        TimberbornResourceAdapter resourceAdapter = new();
        TimberbornCellSource[] currentSources =
        [
            terrainAdapter.CreateSource(0, 0, 0, isSolid: true),
            resourceAdapter.CreateVegetationSource(0, 0, 0),
            terrainAdapter.CreateSource(1, 0, 0, isSolid: true),
            resourceAdapter.CreateVegetationSource(1, 0, 0),
        ];
        WildfireMaterialField[] materialFields = new TimberbornFireCellMapper().CreateMaterialFields(
            grid,
            currentSources);
        ushort firstBurningVegetationCell = PackedCell.Pack(
            fuel: 9,
            heat: 15,
            flammability: 3,
            water: 0,
            terrain: 1,
            burningLevel: 2);
        ushort secondBurningVegetationCell = PackedCell.Pack(
            fuel: 6,
            heat: 12,
            flammability: 3,
            water: 0,
            terrain: 1,
            burningLevel: 1);
        TimberbornFireSimPersistenceSnapshot snapshot = new(
            Width: 2,
            Height: 1,
            Depth: 1,
            Tick: 8,
            Cells: [firstBurningVegetationCell, secondBurningVegetationCell],
            TransportFields: []);

        fireSystem.InitializeFromPersistentFireSimState(grid, currentSources, materialFields, snapshot);

        Assert.NotNull(simulator.RestoredSnapshot);
        Assert.Equal([firstBurningVegetationCell, secondBurningVegetationCell], simulator.RestoredSnapshot.Cells);
        Assert.Equal(0, fireSystem.LastPersistentRestoreNoLiveFuelCellsCleared);
    }

    private static TimberbornBurnDamageService CreateService(params TimberbornBurnDamageDescriptor[] descriptors)
    {
        return new TimberbornBurnDamageService(
            new TimberbornBurnDamageDescriptorCatalog(descriptors),
            new TimberbornBurnDamageCapacityCalculator());
    }

    private static TimberbornBurnDamageDescriptor TreeDescriptor(string specId, string resourceId, int amount)
    {
        return new TimberbornBurnDamageDescriptor(
            specId,
            TimberbornBurnDamageTargetKind.Tree,
            TimberbornBurnMaterialKind.Wood,
            resourceYields: [new TimberbornBurnDamageResourceStack(resourceId, amount)]);
    }

    private static TimberbornBurnDamageTargetRegistration Registration(
        TimberbornBurnDamageTargetKey targetKey,
        string specId,
        IReadOnlyList<TimberbornCellCoordinates> ownedCells)
    {
        return new TimberbornBurnDamageTargetRegistration(targetKey, specId, ownedCells);
    }

    private sealed class RecordingPersistenceSimulatorFactory(RecordingPersistenceSimulator simulator) :
        ITimberbornFireSimulatorFactory
    {
        public IGpuFireSimulator Create(
            FireGrid grid,
            ReadOnlySpan<ushort> initialCells,
            ReadOnlySpan<WildfireMaterialField> materialFields)
        {
            Assert.Equal(simulator.Width, grid.Width);
            Assert.Equal(simulator.Height, grid.Height);
            Assert.Equal(simulator.Depth, grid.Depth);
            return simulator;
        }
    }

    private sealed class RecordingPersistenceSimulator(int width, int height, int depth) :
        IGpuFireSimulator,
        ITimberbornFireSimPersistenceState
    {
        public int Width { get; } = width;

        public int Height { get; } = height;

        public int Depth { get; } = depth;

        public TimberbornFireSimPersistenceSnapshot RestoredSnapshot { get; private set; } =
            new(width, height, depth, Tick: 0, Cells: [], TransportFields: []);

        public void RegisterChange(FireSimChange change)
        {
        }

        public GpuFireStepResult Tick()
        {
            return new GpuFireStepResult([], Tick: 1);
        }

        public IDisposable Subscribe(IFireSimListener listener)
        {
            return NullDisposable.Instance;
        }

        public TimberbornFireSimPersistenceSnapshot CaptureFireSimState()
        {
            return RestoredSnapshot;
        }

        public void RestoreFireSimState(TimberbornFireSimPersistenceSnapshot snapshot)
        {
            RestoredSnapshot = snapshot;
        }
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        private NullDisposable()
        {
        }

        public void Dispose()
        {
        }
    }
}
