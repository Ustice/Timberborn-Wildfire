using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornBeaverFieldExposureTelemetryTests
{
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
        };

        TimberbornBeaverFieldExposureClassification classification =
            TimberbornBeaverFieldExposureTelemetry.Classify(
                new TimberbornBeaverPositionSample("beaver-1", 1, 1, 0),
                candidateCells,
                samples);

        Assert.True(classification.HasExposure);
        Assert.Equal(2, classification.RespiratoryExposureCells);
        Assert.Equal(1, classification.BurnExposureCells);
        Assert.Equal(1, classification.ToxicExposureCells);
        Assert.Equal(1, classification.TaintedAftermathCells);
        Assert.Equal(0, classification.ToxicSteamCells);
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
        TimberbornBeaverFieldExposureTelemetry telemetry = new(
            new StaticPositionProvider([new TimberbornBeaverPositionSample("beaver-1", 1, 1, 0)]),
            surface,
            new RecordingFireLogSink());

        TimberbornBeaverFieldExposureSnapshot snapshot = telemetry.Sample(grid, tick: 11);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(1, snapshot.SampledBeavers);
        Assert.Equal(1, snapshot.ExposedBeavers);
        Assert.Equal(1, snapshot.RespiratoryExposureCells);
        Assert.Equal(1, snapshot.BurnExposureCells);
        Assert.Equal(1, snapshot.TaintedAftermathCells);
        Assert.Equal(9, surface.LastInspectedCellIndices.Count);
    }

    private static TimberbornGpuVisualFieldSample Sample(int cellIndex, float fire, float smoke, float ash)
    {
        return new TimberbornGpuVisualFieldSample(
            CellIndex: cellIndex,
            Tick: 1,
            Fire: fire,
            Smoke: smoke,
            Ash: ash,
            Visibility: 1f,
            SmokeContamination: smoke >= TimberbornBeaverFieldExposureTelemetry.ToxicSmokeThreshold ? smoke : 0f,
            AshContamination: ash >= TimberbornBeaverFieldExposureTelemetry.TaintedAftermathAshThreshold ? ash : 0f);
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
