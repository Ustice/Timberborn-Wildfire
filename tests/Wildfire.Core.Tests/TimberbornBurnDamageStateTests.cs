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
    public void LiveCropCollectorRegistersKnownBurnableYieldTargets()
    {
        FireGrid grid = new(50, 50, 23);
        int cropCellIndex = grid.ToIndex(25, 25, 3);
        TimberbornLiveCropBurnDamageTargets targets =
            TimberbornLiveCropBurnDamageTargetCollector.CollectCandidates(
                grid,
                [
                    new TimberbornLiveCropBurnDamageCandidate(
                        "crop-kohlrabi-live-1",
                        "Kohlrabi.Folktails",
                        "Kohlrabi",
                        1,
                        [
                            new TimberbornCellCoordinates(25, 25, 3),
                            new TimberbornCellCoordinates(25, 26, 3),
                        ]),
                    new TimberbornLiveCropBurnDamageCandidate(
                        "crop-unknown-live-1",
                        "MysteryCrop",
                        "MysteryCrop",
                        1,
                        [new TimberbornCellCoordinates(1, 1, 1)]),
                ]);
        RecordingFireLogSink logSink = new();
        TimberbornBurnDamageService service = new(
            targets.DescriptorCatalog,
            logSink: logSink);

        TimberbornBurnDamageRegistrationSummary summary = service.RegisterTargets(grid, targets.Registrations);
        TimberbornBurnDamageTargetState state = Assert.Single(service.States.Values);

        Assert.Equal(1, summary.TargetCount);
        Assert.Equal(2, summary.OwnedCellCount);
        Assert.Equal(TimberbornBurnDamageTargetKind.Crop, state.TargetKind);
        Assert.Equal(TimberbornBurnMaterialKind.Organic, state.MaterialKind);
        Assert.Equal(["Kohlrabi"], state.AccountedResourceIds);
        Assert.True(state.DamageCapacity > 0);
        Assert.Contains(cropCellIndex, state.OwnedCellIndices);
        Assert.Equal(state.TargetKey, service.TargetKeyByCellIndex[cropCellIndex]);
    }

    [Fact]
    public void LiveCropCollectorRegistersBlueberryHarvestableAsOrganicResourceTarget()
    {
        FireGrid grid = new(50, 50, 23);
        int blueberryCellIndex = grid.ToIndex(12, 13, 2);
        TimberbornLiveCropBurnDamageTargets targets =
            TimberbornLiveCropBurnDamageTargetCollector.CollectCandidates(
                grid,
                [
                    new TimberbornLiveCropBurnDamageCandidate(
                        "harvestable-blueberry-live-1",
                        "BlueberryBush",
                        "Berries",
                        3,
                        [new TimberbornCellCoordinates(12, 13, 2)]),
                ]);
        TimberbornBurnDamageService service = new(targets.DescriptorCatalog);

        TimberbornBurnDamageRegistrationSummary summary = service.RegisterTargets(grid, targets.Registrations);
        TimberbornBurnDamageTargetState state = Assert.Single(service.States.Values);

        Assert.Equal(1, summary.TargetCount);
        Assert.Equal(1, summary.OwnedCellCount);
        Assert.Equal(TimberbornBurnDamageTargetKind.Resource, state.TargetKind);
        Assert.Equal(TimberbornBurnMaterialKind.Organic, state.MaterialKind);
        Assert.Equal(["Berries"], state.AccountedResourceIds);
        Assert.Equal(9, state.DamageCapacity);
        Assert.Contains(blueberryCellIndex, state.OwnedCellIndices);
        Assert.Equal(state.TargetKey, service.TargetKeyByCellIndex[blueberryCellIndex]);
    }

    [Fact]
    public void SelectedBlueberryLiveShapeRegistersFromSelectedObjectWhenGlobalRegistrationIsEmpty()
    {
        FireGrid grid = new(50, 50, 23);
        int blueberryCellIndex = grid.ToIndex(12, 13, 2);
        TimberbornLiveCropBurnDamageTargets selectedTargets =
            TimberbornLiveCropBurnDamageTargetCollector.CollectSelectedObject(
                grid,
                "selected-blueberry-live-1",
                "BlueberryBush(Clone)",
                [
                    new LiveShapeGatherable(
                        new LiveShapeYielder(
                            new LiveShapeGoodAmount("Berries", 3),
                            new LiveShapeYielderSpec(new LiveShapeGoodAmountSpec("Berries", 3)))),
                    new LiveShapeDirectYielder(
                        new LiveShapeGoodAmount("Berries", 3),
                        new LiveShapeYielderSpec(new LiveShapeGoodAmountSpec("Berries", 3))),
                ],
                [new TimberbornCellCoordinates(12, 13, 2)]);
        TimberbornBurnDamageService service = new(
            new TimberbornBurnDamageDescriptorCatalog([]));

        TimberbornBurnDamageRegistrationSummary summary = service.RegisterTargets(
            grid,
            selectedTargets.Registrations,
            selectedTargets.Descriptors);
        TimberbornQaSelectedCropTarget target = TimberbornSelectedCropTargetProvider.ResolveSelectedTarget(
            grid,
            service.States,
            [blueberryCellIndex]);
        TimberbornBurnDamageTargetState state = Assert.Single(service.States.Values);

        Assert.Equal(1, summary.TargetCount);
        Assert.Equal(1, summary.OwnedCellCount);
        Assert.Equal(new TimberbornBurnDamageTargetKey("selected-blueberry-live-1"), state.TargetKey);
        Assert.Equal("BlueberryBush(Clone)", state.SpecId);
        Assert.Equal(TimberbornBurnDamageTargetKind.Resource, state.TargetKind);
        Assert.Equal(TimberbornBurnMaterialKind.Organic, state.MaterialKind);
        Assert.Equal(["Berries"], state.AccountedResourceIds);
        Assert.Equal(9, state.DamageCapacity);
        Assert.Equal(new TimberbornQaSelectedCropTarget(
            blueberryCellIndex,
            12,
            13,
            2,
            "selected_crop_target"), target);
    }

    [Fact]
    public void LiveCropCollectorIgnoresCuttableTreeLogTargets()
    {
        FireGrid grid = new(50, 50, 23);
        int blueberryCellIndex = grid.ToIndex(12, 13, 2);
        TimberbornLiveCropBurnDamageTargets targets =
            TimberbornLiveCropBurnDamageTargetCollector.CollectCandidates(
                grid,
                [
                    new TimberbornLiveCropBurnDamageCandidate(
                        "harvestable-blueberry-live-1",
                        "BlueberryBush",
                        "Berries",
                        3,
                        [new TimberbornCellCoordinates(12, 13, 2)],
                        TimberbornLiveYieldSource.Gatherable),
                    new TimberbornLiveCropBurnDamageCandidate(
                        "cuttable-oak-live-1",
                        "Oak",
                        "Log",
                        8,
                        [new TimberbornCellCoordinates(13, 13, 2)],
                        TimberbornLiveYieldSource.Cuttable),
                ]);
        TimberbornBurnDamageService service = new(targets.DescriptorCatalog);

        TimberbornBurnDamageRegistrationSummary summary = service.RegisterTargets(grid, targets.Registrations);
        TimberbornBurnDamageTargetState state = Assert.Single(service.States.Values);

        Assert.Equal(1, summary.TargetCount);
        Assert.Equal(new TimberbornBurnDamageTargetKey("harvestable-blueberry-live-1"), state.TargetKey);
        Assert.Equal(["Berries"], state.AccountedResourceIds);
        Assert.DoesNotContain("Log", state.AccountedResourceIds);
        Assert.Contains(blueberryCellIndex, state.OwnedCellIndices);
    }

    [Fact]
    public void SelectedTreeOrLogLiveShapeDoesNotRegisterAsCropBurnTarget()
    {
        FireGrid grid = new(50, 50, 23);

        TimberbornLiveCropBurnDamageTargets selectedTargets =
            TimberbornLiveCropBurnDamageTargetCollector.CollectSelectedObject(
                grid,
                "selected-oak-live-1",
                "Oak(Clone)",
                [
                    new LiveShapeTreeComponent(),
                    new LiveShapeGatherable(
                        new LiveShapeYielder(
                            new LiveShapeGoodAmount("Log", 8),
                            new LiveShapeYielderSpec(new LiveShapeGoodAmountSpec("Log", 8)))),
                ],
                [new TimberbornCellCoordinates(12, 13, 2)]);
        TimberbornBurnDamageService service = new(
            new TimberbornBurnDamageDescriptorCatalog([]));

        TimberbornBurnDamageRegistrationSummary summary = service.RegisterTargets(
            grid,
            selectedTargets.Registrations,
            selectedTargets.Descriptors);

        Assert.Empty(selectedTargets.Registrations);
        Assert.Equal(0, summary.TargetCount);
        Assert.Empty(service.States);
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

    [Fact]
    public void CropBurnRegistrationSummaryDoesNotCountOrganicLogResourceTargets()
    {
        FireGrid grid = new(4, 1, 1);
        TimberbornBurnDamageTargetKey cropKey = new("crop-carrot-1");
        TimberbornBurnDamageTargetKey blueberryKey = new("harvestable-blueberry-1");
        TimberbornBurnDamageTargetKey logKey = new("cuttable-oak-log-1");
        TimberbornBurnDamageService service = CreateService(
            new TimberbornBurnDamageDescriptor(
                "Crop.Carrot",
                TimberbornBurnDamageTargetKind.Crop,
                TimberbornBurnMaterialKind.Organic,
                resourceYields: [new TimberbornBurnDamageResourceStack("Carrot", 1)]),
            new TimberbornBurnDamageDescriptor(
                "Harvestable.Blueberry",
                TimberbornBurnDamageTargetKind.Resource,
                TimberbornBurnMaterialKind.Organic,
                resourceYields: [new TimberbornBurnDamageResourceStack("Berries", 1)]),
            new TimberbornBurnDamageDescriptor(
                "Tree.Oak",
                TimberbornBurnDamageTargetKind.Resource,
                TimberbornBurnMaterialKind.Organic,
                resourceYields: [new TimberbornBurnDamageResourceStack("Log", 1)]));
        service.RegisterTargets(
            grid,
            [
                new TimberbornBurnDamageTargetRegistration(
                    cropKey,
                    "Crop.Carrot",
                    [
                        new TimberbornCellCoordinates(0, 0, 0),
                        new TimberbornCellCoordinates(1, 0, 0),
                    ]),
                new TimberbornBurnDamageTargetRegistration(
                    blueberryKey,
                    "Harvestable.Blueberry",
                    [new TimberbornCellCoordinates(2, 0, 0)]),
                new TimberbornBurnDamageTargetRegistration(
                    logKey,
                    "Tree.Oak",
                    [new TimberbornCellCoordinates(3, 0, 0)]),
            ]);

        TimberbornCropBurnTargetRegistrationSummary summary =
            TimberbornCropBurnTargetClassifier.SummarizeRegisteredTargets(service.States.Values);

        Assert.Equal(2, summary.TargetCount);
        Assert.Equal(3, summary.OwnedCellCount);
        Assert.True(TimberbornCropBurnTargetClassifier.IsCropOrHarvestable(service.States[cropKey]));
        Assert.True(TimberbornCropBurnTargetClassifier.IsCropOrHarvestable(service.States[blueberryKey]));
        Assert.False(TimberbornCropBurnTargetClassifier.IsCropOrHarvestable(service.States[logKey]));
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
            PackedCell.Pack(oldFuel, heat: 10, flammability: 3, water: 0, terrain: 1, burningLevel: 1),
            PackedCell.Pack(newFuel, heat: 10, flammability: 3, water: 0, terrain: 1, burningLevel: 1));
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

    private sealed class LiveShapeGatherable(LiveShapeYielder yielder)
    {
        public LiveShapeYielder Yielder { get; } = yielder;
    }

    private sealed class LiveShapeDirectYielder(
        LiveShapeGoodAmount yield,
        LiveShapeYielderSpec yielderSpec)
    {
        public LiveShapeGoodAmount Yield { get; } = yield;

        public LiveShapeYielderSpec YielderSpec { get; } = yielderSpec;
    }

    private sealed class LiveShapeYielder(
        LiveShapeGoodAmount yield,
        LiveShapeYielderSpec yielderSpec)
    {
        public LiveShapeGoodAmount Yield { get; } = yield;

        public LiveShapeYielderSpec YielderSpec { get; } = yielderSpec;
    }

    private sealed class LiveShapeYielderSpec(LiveShapeGoodAmountSpec yield)
    {
        public LiveShapeGoodAmountSpec Yield { get; } = yield;
    }

    private sealed class LiveShapeGoodAmount(string goodId, int amount)
    {
        public string GoodId { get; } = goodId;

        public int Amount { get; } = amount;
    }

    private sealed class LiveShapeGoodAmountSpec(string id, int amount)
    {
        public string Id { get; } = id;

        public int Amount { get; } = amount;
    }

    private sealed class LiveShapeTreeComponent
    {
    }
}
