using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornBurnDamageStateTests
{
    [Fact]
    public void DescriptorCatalogReturnsStaticDescriptorsAndUnknownNoOpDescriptor()
    {
        TimberbornBurnDamageDescriptor descriptor = new(
            "Tree.Pine",
            TimberbornBurnDamageTargetKind.Tree,
            TimberbornBurnMaterialKind.Wood,
            resourceYields: [new TimberbornBurnDamageResourceStack("Log", 4)]);
        TimberbornBurnDamageDescriptorCatalog catalog = new([descriptor]);

        bool foundKnown = catalog.TryLookup("Tree.Pine", out TimberbornBurnDamageDescriptor known);
        bool foundUnknown = catalog.TryLookup("Tree.Mystery", out TimberbornBurnDamageDescriptor unknown);

        Assert.True(foundKnown);
        Assert.Same(descriptor, known);
        Assert.False(foundUnknown);
        Assert.Equal("Tree.Mystery", unknown.SpecId);
        Assert.Equal(TimberbornBurnDamageTargetKind.Unknown, unknown.TargetKind);
        Assert.False(unknown.HasResourceAccounting);
    }

    [Fact]
    public void CapacityUsesYieldAndConstructionInvestmentFromResourceCatalog()
    {
        TimberbornBurnDamageDescriptor descriptor = new(
            "Building.LumberMill",
            TimberbornBurnDamageTargetKind.Structure,
            TimberbornBurnMaterialKind.Constructed,
            resourceYields: [new TimberbornBurnDamageResourceStack("Log", 2)],
            constructionResources:
            [
                new TimberbornBurnDamageResourceStack("Plank", 3),
                new TimberbornBurnDamageResourceStack("Water", 9),
            ]);
        TimberbornBurnDamageCapacityCalculator calculator = new();

        TimberbornBurnDamageCapacity capacity = calculator.Calculate(descriptor);

        Assert.Equal(54, capacity.Capacity);
        Assert.Equal(12, capacity.FuelValue);
        Assert.Equal(2, capacity.Flammability);
        Assert.Equal(["Log", "Plank", "Water"], capacity.AccountedResourceIds);
        Assert.Empty(capacity.MissingResourceIds);
    }

    [Fact]
    public void UnknownResourcesFailClosedAndRemainSearchable()
    {
        TimberbornBurnDamageDescriptor descriptor = new(
            "Crop.Modded",
            TimberbornBurnDamageTargetKind.Crop,
            TimberbornBurnMaterialKind.Organic,
            resourceYields:
            [
                new TimberbornBurnDamageResourceStack("MysteryCrop", 8),
                new TimberbornBurnDamageResourceStack("Carrot", 2),
            ]);
        TimberbornBurnDamageCapacityCalculator calculator = new();

        TimberbornBurnDamageCapacity capacity = calculator.Calculate(descriptor);

        Assert.Equal(6, capacity.Capacity);
        Assert.Equal(["Carrot"], capacity.AccountedResourceIds);
        Assert.Equal(["MysteryCrop"], capacity.MissingResourceIds);
    }

    [Fact]
    public void RegisterTargetsExposesResourceFuelFlammabilityAndAccountedResources()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageDescriptor descriptor = new(
            "Building.LumberMill",
            TimberbornBurnDamageTargetKind.Structure,
            TimberbornBurnMaterialKind.Constructed,
            resourceYields: [new TimberbornBurnDamageResourceStack("Log", 2)],
            constructionResources:
            [
                new TimberbornBurnDamageResourceStack("Plank", 1),
                new TimberbornBurnDamageResourceStack("Water", 7),
            ]);
        TimberbornBurnDamageTargetKey targetKey = new("building-lumber-mill-1");
        TimberbornBurnDamageService service = CreateService(descriptor);

        service.RegisterTargets(
            grid,
            [new TimberbornBurnDamageTargetRegistration(
                targetKey,
                "Building.LumberMill",
                [new TimberbornCellCoordinates(0, 0, 0)])]);

        TimberbornBurnDamageTargetState state = service.States[targetKey];
        TimberbornBurnDamageStateSnapshot snapshot = Assert.Single(service.CaptureState());
        Assert.Equal(34, state.DamageCapacity);
        Assert.Equal(12, state.FuelValue);
        Assert.Equal(2, state.Flammability);
        Assert.Equal(["Log", "Plank", "Water"], state.AccountedResourceIds);
        Assert.Equal(state.FuelValue, snapshot.FuelValue);
        Assert.Equal(state.Flammability, snapshot.Flammability);
        Assert.Equal(state.AccountedResourceIds, snapshot.AccountedResourceIds);
    }

    [Fact]
    public void RegisterTargetsReportsBurnCapacityTelemetryInSummaryAndLogToken()
    {
        FireGrid grid = new(2, 1, 1);
        RecordingFireLogSink logSink = new();
        TimberbornBurnDamageService service = CreateService(
            logSink,
            new TimberbornBurnDamageDescriptor(
                "Building.WoodenHouse",
                TimberbornBurnDamageTargetKind.Structure,
                TimberbornBurnMaterialKind.Constructed,
                constructionResources: [new TimberbornBurnDamageResourceStack("Log", 5)]),
            new TimberbornBurnDamageDescriptor(
                "Terrain.Path",
                TimberbornBurnDamageTargetKind.Infrastructure,
                TimberbornBurnMaterialKind.NonBurnable));

        TimberbornBurnDamageRegistrationSummary summary = service.RegisterTargets(
            grid,
            [
                new TimberbornBurnDamageTargetRegistration(
                    new TimberbornBurnDamageTargetKey("building-wooden-house-1"),
                    "Building.WoodenHouse",
                    [new TimberbornCellCoordinates(0, 0, 0)]),
                new TimberbornBurnDamageTargetRegistration(
                    new TimberbornBurnDamageTargetKey("path-1"),
                    "Terrain.Path",
                    [new TimberbornCellCoordinates(1, 0, 0)]),
            ]);

        Assert.Equal(60, summary.TotalDamageCapacity);
        Assert.Equal(60, summary.MaxDamageCapacity);
        Assert.Equal(1, summary.ZeroCapacityTargetCount);
        string logToken = Assert.Single(logSink.InfoMessages);
        Assert.Contains("wildfire_timberborn_burn_damage_targets_registered", logToken);
        Assert.Contains("burn_capacity_total=60", logToken);
        Assert.Contains("burn_capacity_max=60", logToken);
        Assert.Contains("burn_capacity_zero_targets=1", logToken);
    }

    [Fact]
    public void RegisterTargetsMapsChangedCellsToSingleOwningTarget()
    {
        FireGrid grid = new(3, 3, 1);
        TimberbornBurnDamageService service = CreateService(
            new TimberbornBurnDamageDescriptor(
                "Building.WoodenHouse",
                TimberbornBurnDamageTargetKind.Structure,
                TimberbornBurnMaterialKind.Constructed,
                constructionResources: [new TimberbornBurnDamageResourceStack("Log", 5)]));
        TimberbornBurnDamageTargetKey lowerPriority = new("building-low");
        TimberbornBurnDamageTargetKey higherPriority = new("building-high");

        TimberbornBurnDamageRegistrationSummary registrationSummary = service.RegisterTargets(
            grid,
            [
                new TimberbornBurnDamageTargetRegistration(
                    lowerPriority,
                    "Building.WoodenHouse",
                    [new TimberbornCellCoordinates(1, 1, 0)],
                    ownershipPriority: 1),
                new TimberbornBurnDamageTargetRegistration(
                    higherPriority,
                    "Building.WoodenHouse",
                    [new TimberbornCellCoordinates(1, 1, 0)],
                    ownershipPriority: 5),
            ]);
        TimberbornBurnDamageApplySummary applySummary = service.ApplyDamage(
            10,
            [Decision(grid.ToIndex(1, 1, 0), oldFuel: 8, newFuel: 6)]);

        Assert.Equal(1, registrationSummary.OwnedCellCount);
        Assert.Equal(1, registrationSummary.OverlappingCellCount);
        Assert.Equal(higherPriority, service.TargetKeyByCellIndex[grid.ToIndex(1, 1, 0)]);
        Assert.Equal(1, applySummary.ResolvedTargetCellCount);
        Assert.Equal(1, applySummary.DamageAppliedTargetCount);
        Assert.Equal(0, service.States[lowerPriority].DamageTaken);
        Assert.Equal(2, service.States[higherPriority].DamageTaken);
    }

    [Fact]
    public void ApplyDamageSuppressesMultiCellAndVerticalDuplicateHits()
    {
        FireGrid grid = new(3, 3, 3);
        TimberbornBurnDamageTargetKey targetKey = new("tree-oak-17");
        TimberbornBurnDamageService service = CreateService(
            new TimberbornBurnDamageDescriptor(
                "Tree.Oak",
                TimberbornBurnDamageTargetKind.Tree,
                TimberbornBurnMaterialKind.Wood,
                resourceYields: [new TimberbornBurnDamageResourceStack("Log", 6)]));

        TimberbornBurnDamageRegistrationSummary registrationSummary = service.RegisterTargets(
            grid,
            [TimberbornBurnDamageTargetRegistration.FromFootprint(
                targetKey,
                "Tree.Oak",
                new TimberbornCellFootprint(X: 0, Y: 1, Z: 1, Width: 2, Height: 1, Depth: 2))]);
        int[] ownedCells = service.States[targetKey].OwnedCellIndices.ToArray();
        TimberbornBurnDamageApplySummary applySummary = service.ApplyDamage(
            11,
            ownedCells
                .Select(cellIndex => Decision(cellIndex, oldFuel: 10, newFuel: 7))
                .ToArray());

        Assert.Equal([12, 13, 21, 22], ownedCells);
        Assert.Equal(4, registrationSummary.OwnedCellCount);
        Assert.Equal(4, applySummary.ResolvedTargetCellCount);
        Assert.Equal(3, applySummary.DuplicateCellSuppressedCount);
        Assert.Equal(1, applySummary.DamageAppliedTargetCount);
        Assert.Equal(3, applySummary.TotalDamageApplied);
        Assert.Equal(3, service.States[targetKey].DamageTaken);
        Assert.Equal(11u, service.States[targetKey].LastDamagedTick);
    }

    [Fact]
    public void ApplyDamageRecordsUnresolvedCellsAndBoundsDamageByCapacity()
    {
        FireGrid grid = new(2, 1, 1);
        TimberbornBurnDamageTargetKey targetKey = new("crop-carrot-1");
        TimberbornBurnDamageService service = CreateService(
            new TimberbornBurnDamageDescriptor(
                "Crop.Carrot",
                TimberbornBurnDamageTargetKind.Crop,
                TimberbornBurnMaterialKind.Organic,
                resourceYields: [new TimberbornBurnDamageResourceStack("Carrot", 1)]));
        service.RegisterTargets(
            grid,
            [new TimberbornBurnDamageTargetRegistration(
                targetKey,
                "Crop.Carrot",
                [new TimberbornCellCoordinates(0, 0, 0)])]);

        TimberbornBurnDamageApplySummary first = service.ApplyDamage(
            12,
            [
                Decision(0, oldFuel: 9, newFuel: 2),
                Decision(1, oldFuel: 9, newFuel: 0),
            ]);
        TimberbornBurnDamageApplySummary second = service.ApplyDamage(
            13,
            [Decision(0, oldFuel: 9, newFuel: 0)]);

        Assert.Equal(2, first.DamageCandidateCellCount);
        Assert.Equal(1, first.UnresolvedCellCount);
        Assert.Equal(3, first.TotalDamageApplied);
        Assert.Equal(0, second.TotalDamageApplied);
        Assert.Equal(3, service.States[targetKey].DamageTaken);
        Assert.True(service.States[targetKey].IsFullyDamaged);
    }

    [Fact]
    public void CaptureAndRestoreStatePreservesPerInstanceDamage()
    {
        FireGrid grid = new(2, 1, 1);
        TimberbornBurnDamageDescriptor descriptor = new(
            "Tree.Maple",
            TimberbornBurnDamageTargetKind.Tree,
            TimberbornBurnMaterialKind.Wood,
            resourceYields: [new TimberbornBurnDamageResourceStack("Log", 2)]);
        TimberbornBurnDamageTargetKey targetKey = new("tree-maple-2");
        TimberbornBurnDamageService original = CreateService(descriptor);
        original.RegisterTargets(
            grid,
            [new TimberbornBurnDamageTargetRegistration(
                targetKey,
                "Tree.Maple",
                [new TimberbornCellCoordinates(0, 0, 0)])]);
        original.ApplyDamage(14, [Decision(0, oldFuel: 7, newFuel: 5)]);

        TimberbornBurnDamageService restored = CreateService(descriptor);
        restored.RegisterTargets(
            grid,
            [new TimberbornBurnDamageTargetRegistration(
                targetKey,
                "Tree.Maple",
                [new TimberbornCellCoordinates(0, 0, 0)])]);
        restored.RestoreState(original.CaptureState());
        restored.ApplyDamage(15, [Decision(0, oldFuel: 7, newFuel: 4)]);

        Assert.Equal(5, restored.States[targetKey].DamageTaken);
        Assert.Equal(15u, restored.States[targetKey].LastDamagedTick);
    }

    [Fact]
    public void DeltaConsumerRoutesDecisionsThroughOptionalBurnDamageSink()
    {
        RecordingBurnDamageSink burnDamageSink = new();
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(burnDamageSink: burnDamageSink));

        TimberbornFireDeltaConsumerSummary summary = consumer.Consume(
            16,
            [Delta(4, oldFuel: 7, newFuel: 5)]);

        Assert.Equal(16u, burnDamageSink.Tick);
        Assert.Equal([4], burnDamageSink.Decisions.Select(static decision => decision.CellIndex).ToArray());
        Assert.Equal(1, summary.BurnDamageConsideredCellCount);
        Assert.Equal(1, summary.BurnDamageResolvedTargetCellCount);
        Assert.Equal(1, summary.BurnDamageAppliedTargetCount);
        Assert.Equal(2, summary.BurnDamageTotalDamageApplied);
    }

    private static TimberbornBurnDamageService CreateService(params TimberbornBurnDamageDescriptor[] descriptors)
    {
        return new TimberbornBurnDamageService(
            new TimberbornBurnDamageDescriptorCatalog(descriptors),
            new TimberbornBurnDamageCapacityCalculator());
    }

    private static TimberbornBurnDamageService CreateService(
        ITimberbornFireLogSink logSink,
        params TimberbornBurnDamageDescriptor[] descriptors)
    {
        return new TimberbornBurnDamageService(
            new TimberbornBurnDamageDescriptorCatalog(descriptors),
            new TimberbornBurnDamageCapacityCalculator(),
            logSink);
    }

    private static TimberbornFireCellDeltaDecision Decision(int cellIndex, int oldFuel, int newFuel)
    {
        return TimberbornFireCellDeltaDecision.FromDelta(Delta(cellIndex, oldFuel, newFuel));
    }

    private static CellDelta Delta(int cellIndex, int oldFuel, int newFuel)
    {
        return new CellDelta(
            cellIndex,
            PackedCell.Pack(oldFuel, heat: 10, flammability: 3, water: 0, terrain: 1, heatLoss: 1),
            PackedCell.Pack(newFuel, heat: 10, flammability: 3, water: 0, terrain: 1, heatLoss: 1));
    }

    private sealed class RecordingBurnDamageSink : ITimberbornBurnDamageSink
    {
        public uint Tick { get; private set; }

        public List<TimberbornFireCellDeltaDecision> Decisions { get; } = [];

        public TimberbornBurnDamageApplySummary ApplyDamage(
            uint tick,
            IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
        {
            Tick = tick;
            Decisions.AddRange(decisions);
            return new TimberbornBurnDamageApplySummary(
                tick,
                ConsideredCellCount: decisions.Count,
                DamageCandidateCellCount: decisions.Count,
                ResolvedTargetCellCount: decisions.Count,
                UnresolvedCellCount: 0,
                DuplicateCellSuppressedCount: 0,
                DamageAppliedTargetCount: 1,
                TotalDamageApplied: 2,
                PersistenceWriteCount: 1);
        }
    }

    private sealed class RecordingFireLogSink : ITimberbornFireLogSink
    {
        public List<string> InfoMessages { get; } = [];

        public List<string> WarningMessages { get; } = [];

        public void Info(string message)
        {
            InfoMessages.Add(message);
        }

        public void Warning(string message)
        {
            WarningMessages.Add(message);
        }
    }
}
