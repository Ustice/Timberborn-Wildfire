using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class TimberbornAshFieldServiceTests
{
    [Fact]
    public void SyncFromTransportFieldsBuildsSimulatorBackedReadModel()
    {
        RecordingAshGrowthAdapter growthAdapter = new();
        TimberbornAshFieldService service = new(growthAdapter);

        TimberbornAshFieldSummary summary = service.SyncFromTransportFields(
            9,
            [
                new WildfireTransportFieldState(0, 0, 0, Ash: 2, AshContamination: 0, Source: false).Pack(),
                new WildfireTransportFieldState(0, 0, 0, Ash: 3, AshContamination: 6, Source: false).Pack(),
                WildfireTransportFieldState.Empty.Pack(),
            ]);

        Assert.Equal(1, summary.FertileAshCellCount);
        Assert.Equal(1, summary.TaintedAshCellCount);
        Assert.Equal(1, summary.GrowthCandidateCellCount);
        Assert.True(service.TryGetEntry(0, out TimberbornAshFieldEntry fertile));
        Assert.Equal(WildfireAshQuality.Fertile, fertile.Quality);
        Assert.Equal(2 * TimberbornFertileAshCollectionService.StrengthPerGood, fertile.Strength);
        Assert.True(service.TryGetEntry(1, out TimberbornAshFieldEntry tainted));
        Assert.Equal(WildfireAshQuality.Tainted, tainted.Quality);
        Assert.Equal(3 * TimberbornFertileAshCollectionService.StrengthPerGood, tainted.Strength);
    }

    [Fact]
    public void SimulatorFertileAshRequestsLinearGrowth()
    {
        RecordingAshGrowthAdapter growthAdapter = new();
        TimberbornAshFieldService service = new(growthAdapter);

        TimberbornAshFieldSummary summary = SyncAsh(
            service,
            10,
            (CellIndex: 4, Ash: 3, AshContamination: 0));

        Assert.True(service.TryGetEntry(4, out TimberbornAshFieldEntry entry));
        Assert.Equal(WildfireAshQuality.Fertile, entry.Quality);
        Assert.Equal(3, entry.Strength);
        Assert.Equal(1, summary.FertileAshCellCount);
        Assert.Equal(1, summary.GrowthCandidateCellCount);
        Assert.Single(growthAdapter.Requests);
        Assert.Equal(1.10f, growthAdapter.Requests.Single().GrowthMultiplier, precision: 3);
    }

    [Fact]
    public void SimulatorContaminatedAshCreatesTaintedAshAndNeverRequestsGrowth()
    {
        RecordingAshGrowthAdapter growthAdapter = new();
        TimberbornAshFieldService service = new(growthAdapter);

        TimberbornAshFieldSummary summary = SyncAsh(
            service,
            11,
            (CellIndex: 5, Ash: 3, AshContamination: 1));

        Assert.True(service.TryGetEntry(5, out TimberbornAshFieldEntry entry));
        Assert.Equal(WildfireAshQuality.Tainted, entry.Quality);
        Assert.Equal(0, summary.GrowthCandidateCellCount);
        Assert.Equal(1, summary.GrowthSkippedTaintedCellCount);
        Assert.Empty(growthAdapter.Requests);
    }

    [Fact]
    public void ActiveAshSourceIsNotCollectableUntilItSettles()
    {
        TimberbornAshFieldService service = new(new RecordingAshGrowthAdapter());
        service.SyncFromTransportFields(
            12,
            [
                new WildfireTransportFieldState(
                    Steam: 0,
                    Smoke: 4,
                    SmokeContamination: 0,
                    Ash: 3,
                    AshContamination: 0,
                    Source: true).Pack(),
            ]);
        TimberbornFertileAshCollectionService collection = new(new RecordingFertileAshCollectionAdapter([]));

        TimberbornAshFieldCollectionRemoval removal = service.CalculateCollectedFertileStrengthRemoval(
            0,
            TimberbornFertileAshCollectionService.StrengthPerGood);
        TimberbornFertileAshCollectionSummary summary = collection.Apply(13, service);

        Assert.Equal(0, removal.StrengthRemoved);
        Assert.Equal(0, summary.CandidateCellCount);
        Assert.Equal(0, summary.CollectedGoodCount);
    }

    [Fact]
    public void GrowthMultiplierClampsAtTenPercent()
    {
        RecordingAshGrowthAdapter growthAdapter = new();
        TimberbornAshFieldService service = new(growthAdapter);

        SyncAsh(
            service,
            13,
            (CellIndex: 7, Ash: 7, AshContamination: 0));

        Assert.True(service.TryGetEntry(7, out TimberbornAshFieldEntry entry));
        Assert.Equal(TimberbornAshFieldService.MaxStrength, entry.Strength);
        Assert.Equal(1.10f, growthAdapter.Requests.Single().GrowthMultiplier, precision: 3);
    }

    [Fact]
    public void GrowthAdapterReceivesAllFertileCellsAndReportsUnsupportedGrowables()
    {
        RecordingAshGrowthAdapter growthAdapter = new(
            request => request.CellIndex is not 22);
        TimberbornAshFieldService service = new(growthAdapter);

        TimberbornAshFieldSummary summary = SyncAsh(
            service,
            14,
            (CellIndex: 20, Ash: 1, AshContamination: 0),
            (CellIndex: 21, Ash: 1, AshContamination: 0),
            (CellIndex: 22, Ash: 1, AshContamination: 0));

        Assert.Equal([20, 21, 22], growthAdapter.Requests.Select(static request => request.CellIndex).ToArray());
        Assert.Equal(3, summary.GrowthCandidateCellCount);
        Assert.Equal(2, summary.GrowthAppliedGrowableCount);
        Assert.Equal(1, summary.GrowthUnsupportedGrowableCount);
    }

    [Fact]
    public void AshDecayWaitsForInGameDay()
    {
        RecordingAshGrowthAdapter growthAdapter = new();
        TimberbornAshFieldService service = new(growthAdapter);
        SyncAsh(
            service,
            20,
            (CellIndex: 8, Ash: 2, AshContamination: 0));

        growthAdapter.Requests.Clear();
        TimberbornAshFieldSummary summary = service.Advance(22, dayNumber: 0);

        Assert.True(service.TryGetEntry(8, out TimberbornAshFieldEntry entry));
        Assert.Equal(2, entry.Strength);
        Assert.Equal(0, summary.DecayedAshCellCount);
        Assert.Single(growthAdapter.Requests);
    }

    [Fact]
    public void FertileAshDecaysOneUnitEveryFifteenInGameDays()
    {
        RecordingAshGrowthAdapter growthAdapter = new();
        TimberbornAshFieldService service = new(growthAdapter);
        SyncAsh(
            service,
            20,
            dayNumber: 0,
            (CellIndex: 8, Ash: 2, AshContamination: 0));

        growthAdapter.Requests.Clear();
        TimberbornAshFieldSummary waitingSummary = service.Advance(21, dayNumber: 14);

        Assert.True(service.TryGetEntry(8, out TimberbornAshFieldEntry beforeDecay));
        Assert.Equal(2, beforeDecay.Strength);
        Assert.Equal(0, waitingSummary.DecayedAshCellCount);

        growthAdapter.Requests.Clear();
        List<TimberbornAshFieldCollectionRemoval> removals = new();
        TimberbornAshFieldSummary summary = service.ApplyDayDecay(
            22,
            dayNumber: 30,
            removals.Add);

        Assert.True(service.TryGetEntry(8, out TimberbornAshFieldEntry afterDecayQueued));
        Assert.Equal(2, afterDecayQueued.Strength);
        TimberbornAshFieldCollectionRemoval removal = Assert.Single(removals);
        Assert.Equal(8, removal.CellIndex);
        Assert.Equal(2, removal.StrengthRemoved);
        Assert.True(removal.RemovedEntry);
        Assert.Equal(1, summary.DecayedAshCellCount);
        Assert.Equal(1, summary.GrowthCandidateCellCount);
        Assert.Single(growthAdapter.Requests);
    }

    [Fact]
    public void TaintedAshDecaysOneUnitEveryThirtyInGameDays()
    {
        TimberbornAshFieldService service = new(new RecordingAshGrowthAdapter());
        SyncAsh(
            service,
            20,
            dayNumber: 0,
            (CellIndex: 9, Ash: 3, AshContamination: 1));

        service.Advance(21, dayNumber: 29);

        Assert.True(service.TryGetEntry(9, out TimberbornAshFieldEntry beforeDecay));
        Assert.Equal(3, beforeDecay.Strength);

        List<TimberbornAshFieldCollectionRemoval> removals = new();
        TimberbornAshFieldSummary summary = service.ApplyDayDecay(
            22,
            dayNumber: 30,
            removals.Add);

        Assert.True(service.TryGetEntry(9, out TimberbornAshFieldEntry afterThirtyDays));
        Assert.Equal(3, afterThirtyDays.Strength);
        TimberbornAshFieldCollectionRemoval removal = Assert.Single(removals);
        Assert.Equal(9, removal.CellIndex);
        Assert.Equal(1, removal.StrengthRemoved);
        Assert.False(removal.RemovedEntry);
        Assert.Equal(1, summary.DecayedAshCellCount);
    }

    [Fact]
    public void SnapshotRoundTripsSparseAshFields()
    {
        TimberbornAshFieldSnapshot snapshot = new(
            TimberbornAshFieldEntry.CurrentPersistenceVersion,
            [
                new TimberbornAshFieldEntry(
                    CellIndex: 9,
                    Quality: WildfireAshQuality.Fertile,
                    Strength: 3,
                    SourceKind: TimberbornAshSourceKind.Unknown,
                    CreatedTick: 30,
                    UpdatedTick: 30,
                    PersistenceVersion: TimberbornAshFieldEntry.CurrentPersistenceVersion),
                new TimberbornAshFieldEntry(
                    CellIndex: 10,
                    Quality: WildfireAshQuality.Spent,
                    Strength: 1,
                    SourceKind: TimberbornAshSourceKind.Structure,
                    CreatedTick: 30,
                    UpdatedTick: 30,
                    PersistenceVersion: TimberbornAshFieldEntry.CurrentPersistenceVersion),
            ]);
        RecordingAshGrowthAdapter growthAdapter = new();
        TimberbornAshFieldService restored = new(growthAdapter);

        TimberbornAshFieldSummary summary = restored.RestoreSnapshot(31, snapshot);

        Assert.True(restored.TryGetEntry(9, out TimberbornAshFieldEntry fertile));
        Assert.True(restored.TryGetEntry(10, out TimberbornAshFieldEntry spent));
        Assert.Equal(WildfireAshQuality.Fertile, fertile.Quality);
        Assert.Equal(WildfireAshQuality.Spent, spent.Quality);
        Assert.Equal(1, summary.PersistenceLoadCount);
        Assert.Single(growthAdapter.Requests);
    }

    [Fact]
    public void DeltaConsumerReportsAshSourceEventsWithoutMutatingReadModel()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            new TimberbornBurnDamageDescriptor(
                "Tree.Pine",
                TimberbornBurnDamageTargetKind.Tree,
                TimberbornBurnMaterialKind.Wood,
                resourceYields: [new TimberbornBurnDamageResourceStack("Log", 2)]));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("tree-pine-1", "Tree.Pine", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingAshGrowthAdapter growthAdapter = new();
        TimberbornAshFieldService ashFieldService = new(growthAdapter);
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(
                burnDamageSink: burnDamageService,
                ashFieldSink: new TimberbornAshFieldSink(burnDamageService, ashFieldService)));

        TimberbornFireDeltaConsumerSummary summary =
            consumer.Consume(40, [Delta(0, oldFuel: 10, newFuel: 0)]);

        Assert.False(ashFieldService.TryGetEntry(0, out _));
        Assert.Equal(1, summary.AshFieldSourceEventCount);
        Assert.Equal(0, summary.AshFieldNewAshCellCount);
        Assert.Equal(1, summary.AshFieldFertileAshCellCount);
        Assert.Equal(1, summary.AshFieldGrowthCandidateCellCount);
        Assert.Contains("ash_field_fertile_cells=1", summary.ToLogToken());
    }

    [Fact]
    public void DeltaConsumerMarksAffectedContaminatedCellsAsTaintedAsh()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            new TimberbornBurnDamageDescriptor(
                "Crop.Carrot",
                TimberbornBurnDamageTargetKind.Crop,
                TimberbornBurnMaterialKind.Organic,
                resourceYields: [new TimberbornBurnDamageResourceStack("Carrot", 1)]));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("crop-carrot-1", "Crop.Carrot", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingAshGrowthAdapter growthAdapter = new();
        TimberbornAshFieldService ashFieldService = new(growthAdapter);
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(
                burnDamageSink: burnDamageService,
                ashFieldSink: new TimberbornAshFieldSink(
                    burnDamageService,
                    ashFieldService,
                    affectedCellContaminationProvider: _ => true)));

        TimberbornFireDeltaConsumerSummary summary =
            consumer.Consume(41, [Delta(0, oldFuel: 3, newFuel: 0)]);

        Assert.False(ashFieldService.TryGetEntry(0, out _));
        Assert.Equal(1, summary.AshFieldSourceEventCount);
        Assert.Equal(1, summary.AshFieldTaintedAshCellCount);
        Assert.Equal(0, summary.AshFieldContaminatedBurnSourceCellCount);
        Assert.Equal(1, summary.AshFieldContaminatedAffectedCellCount);
        Assert.Empty(growthAdapter.Requests);
    }

    [Fact]
    public void DeltaConsumerReportsContaminatedBurnSourcesAsTaintedAsh()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            new TimberbornBurnDamageDescriptor(
                "Building.BadwaterPump",
                TimberbornBurnDamageTargetKind.Structure,
                TimberbornBurnMaterialKind.Constructed));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("building-badwater-pump-1", "Building.BadwaterPump", [new TimberbornCellCoordinates(0, 0, 0)])]);
        TimberbornAshFieldService ashFieldService = new(new RecordingAshGrowthAdapter());
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(
                burnDamageSink: burnDamageService,
                ashFieldSink: new TimberbornAshFieldSink(burnDamageService, ashFieldService)));

        TimberbornFireDeltaConsumerSummary summary =
            consumer.Consume(42, [Delta(0, oldFuel: 5, newFuel: 0)]);

        Assert.Equal(1, summary.AshFieldSourceEventCount);
        Assert.Equal(1, summary.AshFieldTaintedAshCellCount);
        Assert.Equal(1, summary.AshFieldContaminatedBurnSourceCellCount);
        Assert.Equal(0, summary.AshFieldContaminatedAffectedCellCount);
        Assert.Contains("ash_field_contaminated_burn_sources=1", summary.ToLogToken());
        Assert.Contains("ash_field_contaminated_affected_cells=0", summary.ToLogToken());
    }

    [Fact]
    public void UnavailableGrowthAdapterFailsLoudlyForGrowthCandidates()
    {
        TimberbornAshFieldService service = new(UnavailableTimberbornAshGrowthAdapter.Instance);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => SyncAsh(
            service,
            50,
            (CellIndex: 11, Ash: 1, AshContamination: 0)));

        Assert.Contains("Ash growth adapter is unavailable", exception.Message);
    }

    [Fact]
    public void TaintedAshSoilPoisoningFailsLoudlyWhenAdapterIsUnavailable()
    {
        TimberbornAshFieldService service = new(new RecordingAshGrowthAdapter());
        SyncAsh(
            service,
            60,
            (CellIndex: 12, Ash: 1, AshContamination: 1));
        TimberbornTaintedAshSoilPoisoningService poisoning = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            poisoning.Apply(61, service.Entries));

        Assert.Contains("Tainted ash soil poisoning adapter is unavailable", exception.Message);
    }

    [Fact]
    public void AshWaterWashoutQueuesSimulatorRemovalForCleanAshWithoutWaterTaint()
    {
        TimberbornAshFieldService service = new(new RecordingAshGrowthAdapter());
        SyncAsh(
            service,
            60,
            (CellIndex: 12, Ash: 2, AshContamination: 0));
        TimberbornAshWaterWashoutService washout = new();
        List<TimberbornAshWaterWashoutRemoval> removals = new();

        TimberbornAshWaterWashoutSummary summary = washout.Apply(
            61,
            service.Entries,
            new Dictionary<int, TimberbornAshWaterContact>
            {
                [12] = new TimberbornAshWaterContact(
                    12,
                    WaterLevel: 3,
                    TimberbornAshWaterContactKind.CleanWater),
            },
            removals.Add);

        TimberbornAshWaterWashoutRemoval removal = Assert.Single(removals);
        Assert.Equal(12, removal.CellIndex);
        Assert.Equal(2, removal.StrengthRemoved);
        Assert.Equal(WildfireAshQuality.Fertile, removal.Quality);
        Assert.Equal(1, summary.CleanAshWashedCellCount);
        Assert.Equal(0, summary.TaintedAshWashedCellCount);
        Assert.Equal(0, summary.WaterTaintAttemptCount);
    }

    [Fact]
    public void AshWaterContactClassifierSeparatesCleanWaterFromContaminatedWaterLikeCells()
    {
        TimberbornFireSimPersistenceSnapshot snapshot = new(
            Width: 3,
            Height: 1,
            Depth: 1,
            Tick: 64,
            Cells:
            [
                PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 1, terrain: 1, burningLevel: 0),
                PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 2, terrain: 1, burningLevel: 0),
                PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 1, burningLevel: 0),
            ],
            TransportFields: []);
        TimberbornImportedFieldTarget[] importedTargets =
        [
            new(
                CellIndex: 0,
                X: 0,
                Y: 0,
                Z: 0,
                WildfireMaterialClass.Water,
                CompanionTargetId: 1,
                InitialCell: snapshot.Cells[0]),
            new(
                CellIndex: 1,
                X: 1,
                Y: 0,
                Z: 0,
                WildfireMaterialClass.Badwater,
                CompanionTargetId: 2,
                InitialCell: snapshot.Cells[1]),
        ];

        IReadOnlyDictionary<int, TimberbornAshWaterContact> contacts =
            TimberbornAshWaterContactClassifier.FromFireSimState(snapshot, importedTargets);

        Assert.Equal(2, contacts.Count);
        Assert.Equal(TimberbornAshWaterContactKind.CleanWater, contacts[0].Kind);
        Assert.Equal(1, contacts[0].WaterLevel);
        Assert.Equal(TimberbornAshWaterContactKind.BadwaterOrContaminatedWater, contacts[1].Kind);
        Assert.Equal(2, contacts[1].WaterLevel);
        Assert.False(contacts.ContainsKey(2));
    }

    [Fact]
    public void AshWaterWashoutFailsLoudlyWhenTaintedWaterAdapterIsUnavailable()
    {
        TimberbornAshFieldService service = new(new RecordingAshGrowthAdapter());
        SyncAsh(
            service,
            60,
            (CellIndex: 12, Ash: 3, AshContamination: 7));
        TimberbornAshWaterWashoutService washout = new();
        List<TimberbornAshWaterWashoutRemoval> removals = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => washout.Apply(
            61,
            service.Entries,
            new Dictionary<int, TimberbornAshWaterContact>
            {
                [12] = new TimberbornAshWaterContact(
                    12,
                    WaterLevel: 3,
                    TimberbornAshWaterContactKind.BadwaterOrContaminatedWater),
            },
            removals.Add));

        TimberbornAshWaterWashoutRemoval removal = Assert.Single(removals);
        Assert.Equal(WildfireAshQuality.Tainted, removal.Quality);
        Assert.Equal(TimberbornAshWaterContactKind.BadwaterOrContaminatedWater, removal.WaterKind);
        Assert.Contains("Ash water taint adapter is unavailable", exception.Message);
    }

    [Fact]
    public void AshWaterWashoutPreservesNoOpCellsWithoutWaterOrWhileActiveSource()
    {
        TimberbornAshFieldService service = new(new RecordingAshGrowthAdapter());
        service.SyncFromTransportFields(
            60,
            [
                new WildfireTransportFieldState(0, 0, 0, Ash: 2, AshContamination: 0, Source: false).Pack(),
                new WildfireTransportFieldState(0, 0, 0, Ash: 2, AshContamination: 0, Source: true).Pack(),
            ]);
        TimberbornAshWaterWashoutService washout = new();
        List<TimberbornAshWaterWashoutRemoval> removals = new();

        TimberbornAshWaterWashoutSummary summary = washout.Apply(
            61,
            service.Entries,
            new Dictionary<int, TimberbornAshWaterContact>
            {
                [1] = new TimberbornAshWaterContact(
                    1,
                    WaterLevel: 3,
                    TimberbornAshWaterContactKind.CleanWater),
            },
            removals.Add);

        Assert.Empty(removals);
        Assert.Equal(2, summary.CandidateAshCellCount);
        Assert.Equal(2, summary.NoOpCellCount);
        Assert.Equal(0, summary.CleanAshWashedCellCount);
    }

    [Fact]
    public void AshWaterWashoutCanReportWaterTaintAttemptSuccessWithoutDeletingLocally()
    {
        TimberbornAshFieldService service = new(new RecordingAshGrowthAdapter());
        SyncAsh(
            service,
            60,
            (CellIndex: 12, Ash: 1, AshContamination: 1));
        TimberbornAshWaterWashoutService washout = new(new RecordingWaterTaintAdapter());

        TimberbornAshWaterWashoutSummary summary = washout.Apply(
            61,
            service.Entries,
            new Dictionary<int, TimberbornAshWaterContact>
            {
                [12] = new TimberbornAshWaterContact(
                    12,
                    WaterLevel: 3,
                    TimberbornAshWaterContactKind.CleanWater),
            });

        Assert.True(service.TryGetEntry(12, out TimberbornAshFieldEntry entry));
        Assert.Equal(1, entry.Strength);
        Assert.Equal(1, summary.WaterTaintAttemptCount);
        Assert.Equal(1, summary.WaterTaintSuccessCount);
    }

    [Fact]
    public void TaintedAshSoilPoisoningReadsContaminationWithNativeMapIndex()
    {
        FireGrid grid = new(50, 50, 23);
        int simulatorCellIndex = grid.ToIndex(12, 13, 4);
        int nativeMapIndex = 13 * 50 + 12;
        RecordingSoilContaminationPoisoningApi soilContamination = new(nativeMapIndex, currentContamination: 0.2f);
        TimberbornSoilContaminationAshPoisoningAdapter adapter = new(
            soilContamination,
            () => grid,
            (x, y) => y * 50 + x,
            new RecordingFireLogSink());

        TimberbornTaintedAshSoilPoisoningSummary summary = adapter.ApplyPoisoning(
            62,
            [new TimberbornTaintedAshSoilPoisoningCandidate(simulatorCellIndex, Strength: 3)]);

        Assert.Equal(1, summary.CandidateCellCount);
        Assert.Equal(1, summary.AppliedCellCount);
        Assert.Equal([nativeMapIndex], soilContamination.ContaminationIndices);
        Assert.Equal([(12, 13, 4)], soilContamination.UpdatedCoordinates);
        Assert.Equal([0.9f], soilContamination.UpdatedContamination);
    }

    [Fact]
    public void FertileAshCollectionDepletesOnlyCollectedFertileCells()
    {
        TimberbornAshFieldService service = new(new RecordingAshGrowthAdapter());
        SyncAsh(
            service,
            70,
            (CellIndex: 13, Ash: 3, AshContamination: 0),
            (CellIndex: 14, Ash: 3, AshContamination: 1));
        TimberbornFertileAshCollectionService collection = new(new RecordingFertileAshCollectionAdapter(
            [
                new TimberbornFertileAshCollectedCell(
                    CellIndex: 13,
                    StrengthToRemove: TimberbornFertileAshCollectionService.StrengthPerGood,
                    GoodAmount: 1),
            ]));

        TimberbornFertileAshCollectionSummary summary = collection.Apply(71, service);

        Assert.Equal(1, summary.CollectedGoodCount);
        Assert.Equal(0, summary.DepletedAshCellCount);
        Assert.Equal(1, summary.SkippedTaintedOrSpentCellCount);
        Assert.True(service.TryGetEntry(13, out TimberbornAshFieldEntry fertile));
        Assert.Equal(3, fertile.Strength);
        Assert.True(service.TryGetEntry(14, out TimberbornAshFieldEntry tainted));
        Assert.Equal(WildfireAshQuality.Tainted, tainted.Quality);
    }

    [Fact]
    public void FertileAshCollectionRemovesFullyCollectedCell()
    {
        TimberbornAshFieldService service = new(new RecordingAshGrowthAdapter());
        SyncAsh(
            service,
            80,
            (CellIndex: 15, Ash: 1, AshContamination: 0));
        TimberbornFertileAshCollectionService collection = new(new RecordingFertileAshCollectionAdapter(
            [
                new TimberbornFertileAshCollectedCell(
                    CellIndex: 15,
                    StrengthToRemove: TimberbornFertileAshCollectionService.StrengthPerGood,
                    GoodAmount: 1),
            ]));

        TimberbornFertileAshCollectionSummary summary = collection.Apply(81, service);

        Assert.Equal(1, summary.CollectedGoodCount);
        Assert.Equal(1, summary.DepletedAshCellCount);
        Assert.True(service.TryGetEntry(15, out TimberbornAshFieldEntry entry));
        Assert.Equal(1, entry.Strength);
    }

    [Fact]
    public void SyncFromTransportFieldsReplacesReadModelWithSimulatorSnapshot()
    {
        TimberbornAshFieldService service = new(new RecordingAshGrowthAdapter());

        SyncAsh(service, 10, (CellIndex: 20, Ash: 3, AshContamination: 0));
        SyncAsh(service, 11, (CellIndex: 20, Ash: 1, AshContamination: 0));

        Assert.True(service.TryGetEntry(20, out TimberbornAshFieldEntry entry));
        Assert.Equal(1, entry.Strength);
    }

    [Fact]
    public void SyncFromTransportFieldsCountsOnlyNewAshCells()
    {
        TimberbornAshFieldService service = new(new RecordingAshGrowthAdapter());

        TimberbornAshFieldSummary initial = SyncAsh(service, 10, (CellIndex: 20, Ash: 3, AshContamination: 0));
        TimberbornAshFieldSummary repeated = SyncAsh(service, 11, (CellIndex: 20, Ash: 3, AshContamination: 0));

        Assert.Equal(1, initial.NewAshCellCount);
        Assert.Equal(0, repeated.NewAshCellCount);
    }

    [Fact]
    public void DayDecaySkipsDuplicateGrowthScanWhenNothingChangedInSameTick()
    {
        RecordingAshGrowthAdapter growthAdapter = new();
        TimberbornAshFieldService service = new(growthAdapter);

        SyncAsh(service, 10, (CellIndex: 20, Ash: 3, AshContamination: 0));
        TimberbornAshFieldSummary decay = service.ApplyDayDecay(10, dayNumber: 0);

        Assert.Single(growthAdapter.Requests);
        Assert.Equal(service.LastSummary, decay);
    }

    private static TimberbornAshFieldSummary SyncAsh(
        TimberbornAshFieldService service,
        uint tick,
        params (int CellIndex, byte Ash, byte AshContamination)[] cells)
    {
        return SyncAsh(service, tick, dayNumber: 0, cells);
    }

    private static TimberbornAshFieldSummary SyncAsh(
        TimberbornAshFieldService service,
        uint tick,
        int dayNumber,
        params (int CellIndex, byte Ash, byte AshContamination)[] cells)
    {
        int fieldLength = cells
            .Select(static cell => cell.CellIndex)
            .DefaultIfEmpty(0)
            .Max() + 1;
        uint[] atmosphericFields = Enumerable
            .Range(0, fieldLength)
            .Select(index =>
            {
                (int CellIndex, byte Ash, byte AshContamination) cell = cells
                    .Where(candidate => candidate.CellIndex == index)
                    .DefaultIfEmpty((index, (byte)0, (byte)0))
                    .Single();
                return new WildfireTransportFieldState(
                    Steam: 0,
                    Smoke: 0,
                    SmokeContamination: 0,
                    cell.Ash,
                    cell.AshContamination,
                    Source: false).Pack();
            })
            .ToArray();
        return service.SyncFromTransportFields(tick, atmosphericFields, dayNumber);
    }

    private static TimberbornBurnDamageService CreateService(params TimberbornBurnDamageDescriptor[] descriptors)
    {
        return new TimberbornBurnDamageService(
            new TimberbornBurnDamageDescriptorCatalog(descriptors),
            new TimberbornBurnDamageCapacityCalculator());
    }

    private static TimberbornBurnDamageTargetRegistration Registration(
        string stableId,
        string specId,
        IReadOnlyList<TimberbornCellCoordinates> ownedCells)
    {
        return new TimberbornBurnDamageTargetRegistration(
            new TimberbornBurnDamageTargetKey(stableId),
            specId,
            ownedCells);
    }

    private static CellDelta Delta(int cellIndex, int oldFuel, int newFuel)
    {
        return new CellDelta(
            cellIndex,
            PackedCell.Pack(oldFuel, heat: 10, flammability: 3, water: 0, terrain: 1, burningLevel: 1),
            PackedCell.Pack(newFuel, heat: 10, flammability: 3, water: 0, terrain: 1, burningLevel: 1));
    }

    private sealed class RecordingAshGrowthAdapter(
        Func<TimberbornAshGrowthBonusRequest, bool>? apply = null)
        : ITimberbornAshGrowthAdapter
    {
        public List<TimberbornAshGrowthBonusRequest> Requests { get; } = [];

        public TimberbornAshGrowthApplicationResult ApplyGrowthBonuses(
            uint tick,
            IReadOnlyList<TimberbornAshGrowthBonusRequest> requests)
        {
            Requests.AddRange(requests);
            int applied = requests.Count(request => apply?.Invoke(request) ?? true);
            return new TimberbornAshGrowthApplicationResult(
                CandidateGrowableCount: requests.Count,
                AppliedGrowableCount: applied,
                FailedConsequenceCount: 0,
                UnsupportedGrowableCount: requests.Count - applied);
        }
    }

    private sealed class RecordingFertileAshCollectionAdapter(
        IReadOnlyList<TimberbornFertileAshCollectedCell> collectedCells)
        : ITimberbornFertileAshCollectionAdapter
    {
        public TimberbornFertileAshCollectionAdapterResult Collect(
            uint tick,
            IReadOnlyList<TimberbornFertileAshCollectionCandidate> candidates)
        {
            return new TimberbornFertileAshCollectionAdapterResult(
                GathererPostCount: 1,
                CandidateCellCount: candidates.Count,
                ReachableCellCount: collectedCells.Count,
                CollectedGoodCount: collectedCells.Sum(static cell => cell.GoodAmount),
                CollectedCells: collectedCells);
        }
    }

    private sealed class RecordingWaterTaintAdapter : ITimberbornAshWaterTaintAdapter
    {
        public TimberbornAshWaterWashoutSummary ApplyWaterTaint(
            uint tick,
            IReadOnlyList<TimberbornAshWaterWashoutRemoval> taintedWashouts,
            TimberbornAshWaterWashoutSummary washoutSummary)
        {
            return washoutSummary with
            {
                WaterTaintSuccessCount = washoutSummary.WaterTaintSuccessCount + taintedWashouts.Count,
            };
        }
    }

    private sealed class RecordingSoilContaminationPoisoningApi(
        int expectedContaminationIndex,
        float currentContamination)
        : ITimberbornSoilContaminationPoisoningApi
    {
        public bool IsAvailable => true;

        public List<int> ContaminationIndices { get; } = [];

        public List<(int X, int Y, int Z)> UpdatedCoordinates { get; } = [];

        public List<float> UpdatedContamination { get; } = [];

        public float Contamination(int mapCellIndex)
        {
            ContaminationIndices.Add(mapCellIndex);
            if (mapCellIndex != expectedContaminationIndex)
            {
                throw new IndexOutOfRangeException();
            }

            return currentContamination;
        }

        public void UpdateContamination(int x, int y, int z, float contamination)
        {
            UpdatedCoordinates.Add((x, y, z));
            UpdatedContamination.Add(contamination);
        }
    }

    private sealed class RecordingFireLogSink : ITimberbornFireLogSink
    {
        public void Info(string message)
        {
        }

        public void Warning(string message)
        {
        }
    }
}
