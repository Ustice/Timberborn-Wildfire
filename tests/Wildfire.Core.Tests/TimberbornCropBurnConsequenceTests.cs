using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornCropBurnConsequenceTests
{
    [Fact]
    public void CropBurnConsequenceReducesYieldFromAppliedBurnDamage()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            CropDescriptor("Crop.Carrot", "Carrot", amount: 2));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("crop-carrot-1", "Crop.Carrot", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingCropBurnConsequenceApi cropApi = new();
        TimberbornCropBurnConsequenceSink cropSink = new(burnDamageService, cropApi);

        TimberbornBurnDamageApplySummary damageSummary =
            burnDamageService.ApplyDamage(21, [Decision(0, oldFuel: 10, newFuel: 8)]);
        TimberbornCropBurnConsequenceSummary cropSummary =
            cropSink.ApplyConsequences(21, [Decision(0, oldFuel: 10, newFuel: 8)]);

        TimberbornCropBurnConsequence consequence = Assert.Single(cropApi.Consequences);
        Assert.Equal(2, damageSummary.TotalDamageApplied);
        Assert.Equal(2, consequence.DamageApplied);
        Assert.Equal(2, cropSummary.YieldLost);
        Assert.Equal(1, cropSummary.ConsideredCropTargetCount);
        Assert.Equal(1, cropSummary.BurnableCropTargetCount);
    }

    [Fact]
    public void FullyBurnedCropRequestsDeathAndBurnedVisualThroughSafeBoundary()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            CropDescriptor("Crop.Carrot", "Carrot", amount: 1));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("crop-carrot-1", "Crop.Carrot", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingCropBurnConsequenceApi cropApi = new(static consequence =>
            new TimberbornCropBurnConsequenceResult(
                MatchedCropTarget: true,
                YieldLost: consequence.DamageApplied,
                KilledCrop: consequence.IsFullyBurned,
                VisualStateUpdated: consequence.IsFullyBurned,
                SkippedUnsafeApi: false));
        TimberbornCropBurnConsequenceSink cropSink = new(burnDamageService, cropApi);

        burnDamageService.ApplyDamage(22, [Decision(0, oldFuel: 3, newFuel: 0)]);
        TimberbornCropBurnConsequenceSummary summary =
            cropSink.ApplyConsequences(22, [Decision(0, oldFuel: 3, newFuel: 0)]);

        TimberbornCropBurnConsequence consequence = Assert.Single(cropApi.Consequences);
        Assert.True(consequence.IsFullyBurned);
        Assert.Equal(1, summary.KilledCropCount);
        Assert.Equal(1, summary.VisualStateUpdateCount);
    }

    [Fact]
    public void MultiCellHarvestableSuppressesDuplicateCropConsequences()
    {
        FireGrid grid = new(2, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            HarvestableDescriptor("Bush.Blueberry", "Berries", amount: 4));
        burnDamageService.RegisterTargets(
            grid,
            [Registration(
                "bush-blueberry-1",
                "Bush.Blueberry",
                [new TimberbornCellCoordinates(0, 0, 0), new TimberbornCellCoordinates(1, 0, 0)])]);
        RecordingCropBurnConsequenceApi cropApi = new();
        TimberbornCropBurnConsequenceSink cropSink = new(burnDamageService, cropApi);

        TimberbornFireCellDeltaDecision[] decisions =
        [
            Decision(0, oldFuel: 10, newFuel: 7),
            Decision(1, oldFuel: 10, newFuel: 7),
        ];
        burnDamageService.ApplyDamage(23, decisions);
        TimberbornCropBurnConsequenceSummary summary = cropSink.ApplyConsequences(23, decisions);

        Assert.Single(cropApi.Consequences);
        Assert.Equal(1, summary.ConsideredCropTargetCount);
        Assert.Equal(1, summary.DuplicateCellSuppressedCount);
        Assert.Equal(3, summary.YieldLost);
    }

    [Fact]
    public void CuttableLogResourceTargetsStayOutsideCropBurnConsequences()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            HarvestableDescriptor("Tree.Oak", "Log", amount: 4));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("tree-oak-log-1", "Tree.Oak", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingCropBurnConsequenceApi cropApi = new();
        TimberbornCropBurnConsequenceSink cropSink = new(burnDamageService, cropApi);

        burnDamageService.ApplyDamage(24, [Decision(0, oldFuel: 10, newFuel: 7)]);
        TimberbornCropBurnConsequenceSummary summary =
            cropSink.ApplyConsequences(24, [Decision(0, oldFuel: 10, newFuel: 7)]);

        Assert.Empty(cropApi.Consequences);
        Assert.Equal(0, summary.ConsideredCropTargetCount);
        Assert.Equal(0, summary.YieldLost);
    }

    [Fact]
    public void NonBurnableCropTargetNoOpsWithoutCallingUnsafeApi()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            new TimberbornBurnDamageDescriptor(
                "Crop.WaterBulb",
                TimberbornBurnDamageTargetKind.Crop,
                TimberbornBurnMaterialKind.Organic,
                resourceYields: [new TimberbornBurnDamageResourceStack("Water", 1)]));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("crop-water-bulb-1", "Crop.WaterBulb", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingCropBurnConsequenceApi cropApi = new();
        TimberbornCropBurnConsequenceSink cropSink = new(burnDamageService, cropApi);

        burnDamageService.ApplyDamage(24, [Decision(0, oldFuel: 8, newFuel: 0)]);
        TimberbornCropBurnConsequenceSummary summary =
            cropSink.ApplyConsequences(24, [Decision(0, oldFuel: 8, newFuel: 0)]);

        Assert.Empty(cropApi.Consequences);
        Assert.Equal(1, summary.ConsideredCropTargetCount);
        Assert.Equal(0, summary.BurnableCropTargetCount);
        Assert.Equal(1, summary.NonBurnableCropTargetCount);
        Assert.Equal(0, summary.YieldLost);
    }

    [Fact]
    public void UnmappedTargetsAndUnknownHarvestResourcesFailClosed()
    {
        FireGrid grid = new(2, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            CropDescriptor("Crop.Modded", "MysteryCrop", amount: 2));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("crop-modded-1", "Crop.Modded", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingCropBurnConsequenceApi cropApi = new();
        TimberbornCropBurnConsequenceSink cropSink = new(burnDamageService, cropApi);

        TimberbornFireCellDeltaDecision[] decisions =
        [
            Decision(0, oldFuel: 8, newFuel: 0),
            Decision(1, oldFuel: 8, newFuel: 0),
        ];
        burnDamageService.ApplyDamage(25, decisions);
        TimberbornCropBurnConsequenceSummary summary = cropSink.ApplyConsequences(25, decisions);

        Assert.Empty(cropApi.Consequences);
        Assert.Equal(1, summary.ConsideredCropTargetCount);
        Assert.Equal(1, summary.UnknownHarvestResourceCount);
        Assert.Equal(1, summary.UnmappedTargetCount);
        Assert.Equal(0, summary.YieldLost);
    }

    [Fact]
    public void UnavailableCropApiReportsSkippedUnsafeApiWithoutClaimingYieldLoss()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            CropDescriptor("Crop.Carrot", "Carrot", amount: 1));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("crop-carrot-1", "Crop.Carrot", [new TimberbornCellCoordinates(0, 0, 0)])]);
        TimberbornCropBurnConsequenceSink cropSink = new(
            burnDamageService,
            UnavailableTimberbornCropBurnConsequenceApi.Instance);

        burnDamageService.ApplyDamage(26, [Decision(0, oldFuel: 3, newFuel: 0)]);
        TimberbornCropBurnConsequenceSummary summary =
            cropSink.ApplyConsequences(26, [Decision(0, oldFuel: 3, newFuel: 0)]);

        Assert.Equal(1, summary.SkippedUnsafeApiCount);
        Assert.Equal(0, summary.YieldLost);
        Assert.Equal(0, summary.KilledCropCount);
        Assert.Equal(0, summary.VisualStateUpdateCount);
    }

    [Fact]
    public void DeltaConsumerAndQaStatusExposeCropBurnTelemetry()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            CropDescriptor("Crop.Carrot", "Carrot", amount: 1));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("crop-carrot-1", "Crop.Carrot", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingCropBurnConsequenceApi cropApi = new();
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(
                burnDamageSink: burnDamageService,
                cropBurnConsequenceSink: new TimberbornCropBurnConsequenceSink(burnDamageService, cropApi)));

        TimberbornFireDeltaConsumerSummary summary =
            consumer.Consume(27, [Delta(0, oldFuel: 3, newFuel: 0)]);
        TimberbornQaCommandResult result = TimberbornQaCommandResult.CreateSuccess(
            "qa-readiness",
            new TimberbornQaCommandState(
                IsSimulatorIntegrated: true,
                LastDeltaConsumerCropBurnConsideredTargetCount: summary.CropBurnConsideredTargetCount,
                LastDeltaConsumerCropBurnYieldLost: summary.CropBurnYieldLost,
                LastDeltaConsumerCropBurnKilledCropCount: summary.CropBurnKilledCropCount,
                LastDeltaConsumerCropBurnSkippedUnsafeApiCount: summary.CropBurnSkippedUnsafeApiCount),
            ["help", "qa-readiness", "status"]);

        Assert.Equal(1, summary.CropBurnConsideredTargetCount);
        Assert.Equal(3, summary.CropBurnYieldLost);
        Assert.Equal(1, summary.CropBurnKilledCropCount);
        Assert.Contains("crop_burn_yield_lost=3", summary.ToLogToken());
        Assert.Contains("last_delta_consumer_crop_burn_considered_targets=1", result.ResultToken);
        Assert.Contains("last_delta_consumer_crop_burn_yield_lost=3", result.ResultToken);
        Assert.Contains("last_delta_consumer_crop_burn_skipped_unsafe_apis=0", result.ResultToken);
    }

    private static TimberbornBurnDamageService CreateService(params TimberbornBurnDamageDescriptor[] descriptors)
    {
        return new TimberbornBurnDamageService(
            new TimberbornBurnDamageDescriptorCatalog(descriptors),
            new TimberbornBurnDamageCapacityCalculator());
    }

    private static TimberbornBurnDamageDescriptor CropDescriptor(string specId, string resourceId, int amount)
    {
        return new TimberbornBurnDamageDescriptor(
            specId,
            TimberbornBurnDamageTargetKind.Crop,
            TimberbornBurnMaterialKind.Organic,
            resourceYields: [new TimberbornBurnDamageResourceStack(resourceId, amount)]);
    }

    private static TimberbornBurnDamageDescriptor HarvestableDescriptor(string specId, string resourceId, int amount)
    {
        return new TimberbornBurnDamageDescriptor(
            specId,
            TimberbornBurnDamageTargetKind.Resource,
            TimberbornBurnMaterialKind.Organic,
            resourceYields: [new TimberbornBurnDamageResourceStack(resourceId, amount)]);
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

    private sealed class RecordingCropBurnConsequenceApi(
        Func<TimberbornCropBurnConsequence, TimberbornCropBurnConsequenceResult>? apply = null)
        : ITimberbornCropBurnConsequenceApi
    {
        public List<TimberbornCropBurnConsequence> Consequences { get; } = [];

        public TimberbornCropBurnConsequenceResult ApplyConsequence(TimberbornCropBurnConsequence consequence)
        {
            Consequences.Add(consequence);
            return apply?.Invoke(consequence) ?? new TimberbornCropBurnConsequenceResult(
                MatchedCropTarget: true,
                YieldLost: consequence.DamageApplied,
                KilledCrop: consequence.IsFullyBurned,
                VisualStateUpdated: consequence.IsFullyBurned,
                SkippedUnsafeApi: false);
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
