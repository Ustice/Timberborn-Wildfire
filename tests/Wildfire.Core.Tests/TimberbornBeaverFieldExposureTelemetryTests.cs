using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornBeaverFieldExposureTelemetryTests
{
    [Fact]
    public void BeaverWorldPositionMapsUnityVerticalToFireGridZ()
    {
        FireGrid grid = new(50, 50, 23);

        TimberbornBeaverFireGridCoordinates? coordinates =
            TimberbornEntityRegistryBeaverPositionProvider.WorldToFireGridCoordinates(
                worldX: 22.9f,
                worldY: 4.1f,
                worldZ: 21.8f,
                grid);

        Assert.Equal(new TimberbornBeaverFireGridCoordinates(22, 21, 4), coordinates);
    }

    [Fact]
    public void BeaverWorldPositionOutsideFireGridReturnsNull()
    {
        FireGrid grid = new(50, 50, 23);

        TimberbornBeaverFireGridCoordinates? coordinates =
            TimberbornEntityRegistryBeaverPositionProvider.WorldToFireGridCoordinates(
                worldX: 22.9f,
                worldY: 30.1f,
                worldZ: 21.8f,
                grid);

        Assert.Null(coordinates);
    }

    [Fact]
    public void ClassifyCountsRespiratoryBurnToxicAndAftermathExposureSeparately()
    {
        FireGrid grid = new(3, 3, 1);
        IReadOnlyList<int> candidateCells = TimberbornBeaverFieldExposureTelemetry.CandidateCellIndices(
            grid,
            x: 1,
            y: 1,
            z: 0);
        Dictionary<int, TimberbornGpuVisualFieldSample> samples = new()
        {
            [grid.ToIndex(1, 1, 0)] = Sample(grid.ToIndex(1, 1, 0), fire: 0.2f, smoke: 0.7f, ash: 0.1f),
            [grid.ToIndex(0, 1, 0)] = Sample(grid.ToIndex(0, 1, 0), fire: 0.0f, smoke: 0.2f, ash: 0.4f),
            [grid.ToIndex(2, 1, 0)] = Sample(grid.ToIndex(2, 1, 0), fire: 0.0f, smoke: 0.0f, ash: 0.0f),
            [grid.ToIndex(1, 0, 0)] = Sample(
                grid.ToIndex(1, 0, 0),
                fire: 0.0f,
                smoke: 0.0f,
                ash: 0.0f,
                steam: 0.7f,
                smokeContamination: 0.6f),
        };

        TimberbornBeaverFieldExposureClassification classification =
            TimberbornBeaverFieldExposureTelemetry.Classify(
                new TimberbornBeaverPositionSample("beaver-1", 1, 1, 0),
                candidateCells,
                samples);

        Assert.True(classification.HasExposure);
        Assert.Equal(2, classification.RespiratoryExposureCells);
        Assert.Equal(1, classification.BurnExposureCells);
        Assert.Equal(2, classification.ToxicExposureCells);
        Assert.Equal(1, classification.TaintedAftermathCells);
        Assert.Equal(1, classification.SteamCells);
    }

    [Fact]
    public void ClassifyCountsCleanSteamWithoutTreatingItAsToxic()
    {
        FireGrid grid = new(3, 3, 1);
        IReadOnlyList<int> candidateCells = TimberbornBeaverFieldExposureTelemetry.CandidateCellIndices(
            grid,
            x: 1,
            y: 1,
            z: 0);
        Dictionary<int, TimberbornGpuVisualFieldSample> samples = new()
        {
            [grid.ToIndex(1, 1, 0)] = Sample(
                grid.ToIndex(1, 1, 0),
                fire: 0.0f,
                smoke: 0.0f,
                ash: 0.0f,
                steam: 0.7f,
                smokeContamination: 0.0f),
            [grid.ToIndex(0, 1, 0)] = Sample(
                grid.ToIndex(0, 1, 0),
                fire: 0.0f,
                smoke: 0.0f,
                ash: 0.0f,
                steam: 0.7f,
                smokeContamination: 0.4f),
        };

        TimberbornBeaverFieldExposureClassification classification =
            TimberbornBeaverFieldExposureTelemetry.Classify(
                new TimberbornBeaverPositionSample("beaver-1", 1, 1, 0),
                candidateCells,
                samples);

        Assert.True(classification.HasExposure);
        Assert.Equal(2, classification.SteamCells);
        Assert.Equal(0, classification.ToxicExposureCells);
    }

    [Fact]
    public void SampleReportsSafeUnavailableWhenPositionApiIsUnavailable()
    {
        RecordingFireLogSink logSink = new();
        TimberbornBeaverFieldExposureTelemetry telemetry = new(
            new UnavailablePositionProvider(),
            new RecordingVisualFieldSurface(isBound: true),
            logSink);

        TimberbornBeaverFieldExposureSnapshot snapshot = telemetry.Sample(new FireGrid(2, 2, 1), tick: 9);

        Assert.False(snapshot.IsAvailable);
        Assert.Equal("position_api_unavailable", snapshot.UnavailableReason);
        Assert.Equal(1, snapshot.SkippedNoPositionApi);
        Assert.Contains("available=false", logSink.InfoMessages.Single());
    }

    [Fact]
    public void SampleAggregatesExposureTelemetryFromVisualFieldSurface()
    {
        FireGrid grid = new(3, 3, 1);
        RecordingVisualFieldSurface surface = new(
            isBound: true,
            samplesByCell: new Dictionary<int, TimberbornGpuVisualFieldSample>
            {
                [grid.ToIndex(1, 1, 0)] = Sample(grid.ToIndex(1, 1, 0), fire: 0.2f, smoke: 0.3f, ash: 0.0f),
                [grid.ToIndex(0, 1, 0)] = Sample(grid.ToIndex(0, 1, 0), fire: 0.0f, smoke: 0.0f, ash: 0.5f),
            });
        RecordingFireLogSink logSink = new();
        TimberbornBeaverFieldExposureTelemetry telemetry = new(
            new StaticPositionProvider([new TimberbornBeaverPositionSample("beaver-1", 1, 1, 0)]),
            surface,
            logSink);

        TimberbornBeaverFieldExposureSnapshot snapshot = telemetry.Sample(grid, tick: 11);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(1, snapshot.SampledBeavers);
        Assert.Equal(1, snapshot.ExposedBeavers);
        Assert.Equal(1, snapshot.RespiratoryExposureCells);
        Assert.Equal(1, snapshot.BurnExposureCells);
        Assert.Equal(1, snapshot.TaintedAftermathCells);
        Assert.Equal(9, surface.LastInspectedCellIndices.Count);
        string aggregateLog = logSink.InfoMessages.Single(static message =>
            message.StartsWith(
                "wildfire_timberborn_beaver_field_exposure_sampled ",
                StringComparison.Ordinal));
        Assert.Contains("steam_cells=0", aggregateLog);
        Assert.DoesNotContain(logSink.InfoMessages, static message =>
            message.StartsWith(
                "wildfire_timberborn_beaver_field_exposure_beaver_sampled ",
                StringComparison.Ordinal));
    }

    [Fact]
    public void SampleCanLogPerBeaverExposureDetailsForQaProfiling()
    {
        FireGrid grid = new(3, 3, 1);
        RecordingVisualFieldSurface surface = new(
            isBound: true,
            samplesByCell: new Dictionary<int, TimberbornGpuVisualFieldSample>
            {
                [grid.ToIndex(1, 1, 0)] = Sample(grid.ToIndex(1, 1, 0), fire: 0.2f, smoke: 0.3f, ash: 0.0f),
            });
        RecordingFireLogSink logSink = new();
        TimberbornBeaverFieldExposureTelemetry telemetry = new(
            new StaticPositionProvider([new TimberbornBeaverPositionSample("beaver-1", 1, 1, 0)]),
            surface,
            logSink,
            logPerBeaverSamples: true);

        telemetry.Sample(grid, tick: 11);

        string beaverLog = logSink.InfoMessages.Single(static message =>
            message.StartsWith(
                "wildfire_timberborn_beaver_field_exposure_beaver_sampled ",
                StringComparison.Ordinal));
        Assert.Contains("beaver_id=beaver-1", beaverLog);
        Assert.Contains("max_smoke=0.3", beaverLog);
    }

    [Fact]
    public void SelectQaStimulusTargetUsesSampledBeaverCandidateCell()
    {
        FireGrid grid = new(5, 5, 2);
        TimberbornBeaverFieldExposureTelemetry telemetry = new(
            new StaticPositionProvider([
                new TimberbornBeaverPositionSample("beaver-b", 3, 3, 1),
                new TimberbornBeaverPositionSample("beaver-a", 2, 2, 1),
            ]),
            new RecordingVisualFieldSurface(isBound: true),
            new RecordingFireLogSink());

        TimberbornBeaverFieldExposureQaTarget target = telemetry.SelectQaStimulusTarget(grid);

        Assert.True(target.IsAvailable);
        Assert.Equal("beaver-a", target.BeaverId);
        Assert.Equal(2, target.BeaverX);
        Assert.Equal(2, target.BeaverY);
        Assert.Equal(1, target.BeaverZ);
        Assert.Equal(grid.ToIndex(2, 2, 1), target.CellIndex);
        Assert.Equal(9, target.CandidateCellCount);
        Assert.Equal(2, target.SampledBeaverCount);
        Assert.Equal(0, target.SkippedNoPositionApiCount);
        Assert.Equal(0, target.SkippedBoundedSamplingCount);
    }

    [Fact]
    public void SelectQaStimulusTargetReportsSafeUnavailableWhenPositionApiIsUnavailable()
    {
        TimberbornBeaverFieldExposureTelemetry telemetry = new(
            new UnavailablePositionProvider(),
            new RecordingVisualFieldSurface(isBound: true),
            new RecordingFireLogSink());

        TimberbornBeaverFieldExposureQaTarget target = telemetry.SelectQaStimulusTarget(new FireGrid(2, 2, 1));

        Assert.False(target.IsAvailable);
        Assert.Equal("position_api_unavailable", target.UnavailableReason);
        Assert.Equal(1, target.SkippedNoPositionApiCount);
        Assert.Null(target.CellIndex);
    }

    [Fact]
    public void SampleReportsBoundedSamplingSkipsWhenCandidateCellsExceedInspectionCap()
    {
        FireGrid grid = new(87, 3, 1);
        TimberbornBeaverPositionSample[] beavers = Enumerable.Range(0, 29)
            .Select(index => new TimberbornBeaverPositionSample(
                $"beaver-{index}",
                X: 1 + (index * 3),
                Y: 1,
                Z: 0))
            .ToArray();
        RecordingVisualFieldSurface surface = new(isBound: true);
        RecordingFireLogSink logSink = new();
        TimberbornBeaverFieldExposureTelemetry telemetry = new(
            new StaticPositionProvider(beavers),
            surface,
            logSink);

        TimberbornBeaverFieldExposureSnapshot snapshot = telemetry.Sample(grid, tick: 12);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(TimberbornGpuVisualFieldSurface.MaxInspectionCellCount, surface.LastInspectedCellIndices.Count);
        Assert.Equal(28, snapshot.SampledBeavers);
        Assert.Equal(1, snapshot.SkippedBoundedSampling);
        Assert.Equal(0, snapshot.ExposedBeavers);
        string aggregateLog = logSink.InfoMessages.Single(static message =>
            message.StartsWith(
                "wildfire_timberborn_beaver_field_exposure_sampled ",
                StringComparison.Ordinal));
        Assert.Contains("sampled_beavers=28", aggregateLog);
        Assert.Contains("skipped_bounded_sampling=1", aggregateLog);
    }

    private static TimberbornGpuVisualFieldSample Sample(
        int cellIndex,
        float fire,
        float smoke,
        float ash,
        float steam = 0f,
        float? smokeContamination = null,
        float? ashContamination = null)
    {
        return new TimberbornGpuVisualFieldSample(
            CellIndex: cellIndex,
            Tick: 1,
            Fire: fire,
            Smoke: smoke,
            Ash: ash,
            Visibility: 1f,
            Steam: steam,
            SmokeContamination: smokeContamination ??
                (smoke >= TimberbornBeaverFieldExposureTelemetry.ToxicSmokeThreshold ? smoke : 0f),
            AshContamination: ashContamination ??
                (ash >= TimberbornBeaverFieldExposureTelemetry.TaintedAftermathAshThreshold ? ash : 0f));
    }

    private sealed class StaticPositionProvider(
        IReadOnlyList<TimberbornBeaverPositionSample> positions) : ITimberbornBeaverPositionProvider
    {
        public TimberbornBeaverPositionSnapshot GetPositions(FireGrid grid)
        {
            return TimberbornBeaverPositionSnapshot.Available(positions);
        }
    }

    private sealed class UnavailablePositionProvider : ITimberbornBeaverPositionProvider
    {
        public TimberbornBeaverPositionSnapshot GetPositions(FireGrid grid)
        {
            return TimberbornBeaverPositionSnapshot.Unavailable("position_api_unavailable");
        }
    }

    private sealed class RecordingVisualFieldSurface(
        bool isBound,
        IReadOnlyDictionary<int, TimberbornGpuVisualFieldSample>? samplesByCell = null) : ITimberbornGpuVisualFieldSurface
    {
        private readonly IReadOnlyDictionary<int, TimberbornGpuVisualFieldSample> _samplesByCell =
            samplesByCell ?? new Dictionary<int, TimberbornGpuVisualFieldSample>();

        public IReadOnlyList<int> LastInspectedCellIndices { get; private set; } = [];

        public TimberbornGpuVisualFieldSurfaceState State { get; } = isBound
            ? new TimberbornGpuVisualFieldSurfaceState(
                IsBound: true,
                Width: 3,
                Height: 3,
                Depth: 1,
                CellCount: 9,
                StrideBytes: 16,
                Channels: TimberbornGpuVisualFieldChannels.All,
                LastUpdatedTick: 1)
            : TimberbornGpuVisualFieldSurfaceState.Unbound;

        public bool TryGetBinding(out TimberbornGpuVisualFieldSurfaceBinding binding)
        {
            binding = null!;
            return false;
        }

        public void Bind(TimberbornGpuVisualFieldSurfaceBinding binding)
        {
        }

        public void MarkUpdated(uint tick)
        {
        }

        public IReadOnlyList<TimberbornGpuVisualFieldSample> InspectCells(IReadOnlyList<int> cellIndices)
        {
            LastInspectedCellIndices = cellIndices.ToArray();
            return cellIndices
                .Where(_samplesByCell.ContainsKey)
                .Select(cellIndex => _samplesByCell[cellIndex])
                .ToArray();
        }

        public TimberbornSmokeHeightTelemetry SnapshotSmokeHeight()
        {
            return TimberbornSmokeHeightTelemetry.Empty;
        }

        public void Unbind()
        {
        }
    }

    private sealed class RecordingFireLogSink : ITimberbornFireLogSink
    {
        public List<string> InfoMessages { get; } = [];

        public void Info(string message)
        {
            InfoMessages.Add(message);
        }

        public void Warning(string message)
        {
        }
    }
}
