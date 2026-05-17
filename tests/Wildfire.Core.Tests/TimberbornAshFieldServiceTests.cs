using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornAshFieldServiceTests
{
    [Fact]
    public void CleanOrganicSourceCreatesFertileAshAndLinearGrowthRequest()
    {
        RecordingAshGrowthAdapter growthAdapter = new();
        TimberbornAshFieldService service = new(growthAdapter);

        TimberbornAshFieldSummary summary = service.ApplySources(
            10,
            [
                SourceEvent(
                    cellIndex: 4,
                    materialKind: TimberbornBurnMaterialKind.Organic,
                    strength: 50),
            ]);

        Assert.True(service.TryGetEntry(4, out TimberbornAshFieldEntry entry));
        Assert.Equal(WildfireAshQuality.Fertile, entry.Quality);
        Assert.Equal(50, entry.Strength);
        Assert.Equal(1, summary.FertileAshCellCount);
        Assert.Equal(1, summary.GrowthCandidateCellCount);
        Assert.Single(growthAdapter.Requests);
        Assert.Equal(1.05f, growthAdapter.Requests.Single().GrowthMultiplier, precision: 3);
    }

    [Fact]
    public void ContaminatedSourceCreatesTaintedAshAndNeverRequestsGrowth()
    {
        RecordingAshGrowthAdapter growthAdapter = new();
        TimberbornAshFieldService service = new(growthAdapter);

        TimberbornAshFieldSummary summary = service.ApplySources(
            11,
            [
                SourceEvent(
                    cellIndex: 5,
                    materialKind: TimberbornBurnMaterialKind.Organic,
                    strength: 100,
                    isSourceContaminated: true),
            ]);

        Assert.True(service.TryGetEntry(5, out TimberbornAshFieldEntry entry));
        Assert.Equal(WildfireAshQuality.Tainted, entry.Quality);
        Assert.Equal(0, summary.GrowthCandidateCellCount);
        Assert.Equal(1, summary.GrowthSkippedTaintedCellCount);
        Assert.Empty(growthAdapter.Requests);
    }

    [Fact]
    public void ConstructedInertSourceCreatesSpentAshWithoutGrowth()
    {
        RecordingAshGrowthAdapter growthAdapter = new();
        TimberbornAshFieldService service = new(growthAdapter);

        TimberbornAshFieldSummary summary = service.ApplySources(
            12,
            [
                SourceEvent(
                    cellIndex: 6,
                    materialKind: TimberbornBurnMaterialKind.Constructed,
                    strength: 100,
                    sourceKind: TimberbornAshSourceKind.Structure),
            ]);

        Assert.True(service.TryGetEntry(6, out TimberbornAshFieldEntry entry));
        Assert.Equal(WildfireAshQuality.Spent, entry.Quality);
        Assert.Equal(1, summary.SpentAshCellCount);
        Assert.Equal(0, summary.GrowthCandidateCellCount);
        Assert.Empty(growthAdapter.Requests);
    }

    [Fact]
    public void GrowthMultiplierClampsAtTenPercent()
    {
        RecordingAshGrowthAdapter growthAdapter = new();
        TimberbornAshFieldService service = new(growthAdapter);

        service.ApplySources(
            13,
            [
                SourceEvent(
                    cellIndex: 7,
                    materialKind: TimberbornBurnMaterialKind.Organic,
                    strength: 250),
            ]);

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

        TimberbornAshFieldSummary summary = service.ApplySources(
            14,
            [
                SourceEvent(
                    cellIndex: 20,
                    materialKind: TimberbornBurnMaterialKind.Organic,
                    strength: 100,
                    sourceKind: TimberbornAshSourceKind.Crop),
                SourceEvent(
                    cellIndex: 21,
                    materialKind: TimberbornBurnMaterialKind.Wood,
                    strength: 100,
                    sourceKind: TimberbornAshSourceKind.Tree),
                SourceEvent(
                    cellIndex: 22,
                    materialKind: TimberbornBurnMaterialKind.Organic,
                    strength: 100,
                    sourceKind: TimberbornAshSourceKind.Resource),
            ]);

        Assert.Equal([20, 21, 22], growthAdapter.Requests.Select(static request => request.CellIndex).ToArray());
        Assert.Equal(3, summary.GrowthCandidateCellCount);
        Assert.Equal(2, summary.GrowthAppliedGrowableCount);
        Assert.Equal(1, summary.GrowthSkippedUnsupportedGrowableCount);
    }

    [Fact]
    public void AshDecayRemovesExpiredGrowth()
    {
        RecordingAshGrowthAdapter growthAdapter = new();
        TimberbornAshFieldService service = new(growthAdapter);
        service.ApplySources(
            20,
            [
                SourceEvent(
                    cellIndex: 8,
                    materialKind: TimberbornBurnMaterialKind.Organic,
                    strength: 2),
            ]);

        growthAdapter.Requests.Clear();
        TimberbornAshFieldSummary summary = service.Advance(22);

        Assert.False(service.TryGetEntry(8, out _));
        Assert.Equal(1, summary.DecayedAshCellCount);
        Assert.Equal(0, summary.GrowthCandidateCellCount);
        Assert.Empty(growthAdapter.Requests);
    }

    [Fact]
    public void SnapshotRoundTripsSparseAshFields()
    {
        TimberbornAshFieldService source = new(new RecordingAshGrowthAdapter());
        source.ApplySources(
            30,
            [
                SourceEvent(
                    cellIndex: 9,
                    materialKind: TimberbornBurnMaterialKind.Organic,
                    strength: 75),
                SourceEvent(
                    cellIndex: 10,
                    materialKind: TimberbornBurnMaterialKind.Constructed,
                    strength: 40,
                    sourceKind: TimberbornAshSourceKind.Structure),
            ]);
        TimberbornAshFieldSnapshot snapshot = source.SaveSnapshot();
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
    public void DeltaConsumerRoutesBurnedSourceCellsIntoAshField()
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

        Assert.True(ashFieldService.TryGetEntry(0, out TimberbornAshFieldEntry entry));
        Assert.Equal(WildfireAshQuality.Fertile, entry.Quality);
        Assert.Equal(1, summary.AshFieldSourceEventCount);
        Assert.Equal(1, summary.AshFieldNewAshCellCount);
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

        Assert.True(ashFieldService.TryGetEntry(0, out TimberbornAshFieldEntry entry));
        Assert.Equal(WildfireAshQuality.Tainted, entry.Quality);
        Assert.Equal(1, summary.AshFieldTaintedAshCellCount);
        Assert.Empty(growthAdapter.Requests);
    }

    [Fact]
    public void UnavailableGrowthAdapterReportsSkippedUnsafeApi()
    {
        TimberbornAshFieldService service = new(UnavailableTimberbornAshGrowthAdapter.Instance);

        TimberbornAshFieldSummary summary = service.ApplySources(
            50,
            [
                SourceEvent(
                    cellIndex: 11,
                    materialKind: TimberbornBurnMaterialKind.Organic,
                    strength: 100),
            ]);

        Assert.Equal(1, summary.GrowthCandidateCellCount);
        Assert.Equal(1, summary.GrowthSkippedUnsafeApiCount);
        Assert.Equal(0, summary.GrowthAppliedGrowableCount);
    }

    private static TimberbornAshSourceEvent SourceEvent(
        int cellIndex,
        TimberbornBurnMaterialKind materialKind,
        int strength,
        TimberbornAshSourceKind sourceKind = TimberbornAshSourceKind.Crop,
        bool isSourceContaminated = false,
        bool isAffectedCellContaminated = false,
        IReadOnlyList<string>? accountedResourceIds = null)
    {
        return new TimberbornAshSourceEvent(
            cellIndex,
            Tick: 0,
            sourceKind,
            materialKind,
            strength,
            isSourceContaminated,
            isAffectedCellContaminated,
            accountedResourceIds ?? []);
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
                SkippedUnsafeApiCount: 0,
                SkippedUnsupportedGrowableCount: requests.Count - applied);
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
