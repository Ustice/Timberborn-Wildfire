using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornGpuFieldRendererTests
{
    [Fact]
    public void BatchesChangedVisualSamplesIntoGpuFieldRegions()
    {
        RecordingFireLogSink logSink = new();
        RecordingVisualFieldDataReader dataReader = new(new Dictionary<int, TimberbornGpuVisualFieldSample>
        {
            [0] = Sample(0, fire: 1f, smoke: 0.2f, ash: 0f, visibility: 1f),
            [1] = Sample(1, fire: 0.8f, smoke: 0.3f, ash: 0f, visibility: 1f),
            [10] = Sample(10, fire: 0f, smoke: 0.7f, ash: 0.2f, visibility: 0.8f),
        });
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(logSink, dataReader);
        RecordingGpuFieldRendererPresenter presenter = new();
        TimberbornGpuFieldRendererSink sink = new(
            surface,
            logSink,
            new TimberbornGpuFieldRendererOptions(RegionSize: 2, MaxUpdatedRegionsPerDispatch: 8),
            presenter);

        sink.BeginVisualEffectDispatch(12);
        sink.UpdateVisualEffect(EffectEvent(0, 12));
        sink.UpdateVisualEffect(EffectEvent(1, 12));
        sink.UpdateVisualEffect(EffectEvent(10, 12));
        sink.CompleteVisualEffectDispatch(12);

        Assert.Equal([0, 1, 10], dataReader.RequestedCellIndices);
        Assert.Equal(2, presenter.RenderedRegions.Count);
        Assert.Equal(2, sink.Counters.VisibleRegionCount);
        Assert.Equal(2, sink.Counters.UpdatedRegionCount);
        Assert.Equal(0, sink.Counters.DroppedRegionCount);
        Assert.Equal(0, sink.Counters.MaterialFailureCount);
        Assert.True(sink.Counters.RendererEnabled);
        Assert.True(sink.Counters.MaterialReady);
        Assert.True(sink.Counters.VisualFieldSurfaceBound);
        Assert.Contains(
            logSink.InfoMessages,
            message => message.Contains(
                "wildfire_timberborn_gpu_field_renderer_updated tick=12 visible_regions=2 updated_regions=2",
                StringComparison.Ordinal));
    }

    [Fact]
    public void DropsRegionsPastConfiguredLimitWithoutCreatingPerCellObjects()
    {
        RecordingFireLogSink logSink = new();
        RecordingVisualFieldDataReader dataReader = new(new Dictionary<int, TimberbornGpuVisualFieldSample>
        {
            [0] = Sample(0, fire: 1f, smoke: 0f, ash: 0f, visibility: 1f),
            [3] = Sample(3, fire: 1f, smoke: 0f, ash: 0f, visibility: 1f),
        });
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(logSink, dataReader);
        RecordingGpuFieldRendererPresenter presenter = new();
        TimberbornGpuFieldRendererSink sink = new(
            surface,
            logSink,
            new TimberbornGpuFieldRendererOptions(RegionSize: 1, MaxUpdatedRegionsPerDispatch: 1),
            presenter);

        sink.BeginVisualEffectDispatch(2);
        sink.UpdateVisualEffect(EffectEvent(0, 2));
        sink.UpdateVisualEffect(EffectEvent(3, 2));
        sink.CompleteVisualEffectDispatch(2);

        Assert.Single(presenter.RenderedRegions);
        Assert.Equal(1, sink.Counters.VisibleRegionCount);
        Assert.Equal(1, sink.Counters.UpdatedRegionCount);
        Assert.Equal(1, sink.Counters.DroppedRegionCount);
    }

    [Fact]
    public void QaResultTokenIncludesGpuFieldRendererTelemetry()
    {
        TimberbornQaCommandState state = new(
            IsSimulatorIntegrated: true,
            VisualFieldSurfaceBound: true,
            VisualFieldSurfaceCellCount: 16,
            GpuFieldRendererEnabled: true,
            GpuFieldRendererMaterialReady: true,
            GpuFieldRendererSurfaceBound: true,
            GpuFieldRendererVisibleRegionCount: 3,
            GpuFieldRendererUpdatedRegionCount: 2,
            GpuFieldRendererLastNonZeroUpdatedRegionCount: 2,
            GpuFieldRendererLastNonZeroUpdatedRegionTick: 9,
            GpuFieldRendererMaxUpdatedRegionCount: 512,
            GpuFieldRendererDroppedRegionCount: 1,
            GpuFieldRendererMaterialFailureCount: 0,
            GpuFieldRendererLastUpdatedTick: 9);

        TimberbornQaCommandResult result = TimberbornQaCommandResult.CreateSuccess(
            "status",
            state,
            ["status"]);

        Assert.Contains("gpu_field_renderer_enabled=true", result.ResultToken);
        Assert.Contains("gpu_field_renderer_material_ready=true", result.ResultToken);
        Assert.Contains("gpu_field_renderer_visible_regions=3", result.ResultToken);
        Assert.Contains("gpu_field_renderer_updated_regions=2", result.ResultToken);
        Assert.Contains("gpu_field_renderer_dropped_regions=1", result.ResultToken);
        Assert.Contains("gpu_field_renderer_material_failures=0", result.ResultToken);
    }

    private static TimberbornGpuVisualFieldSurface CreateBoundSurface(
        ITimberbornFireLogSink logSink,
        ITimberbornGpuVisualFieldDataReader dataReader)
    {
        TimberbornGpuVisualFieldSurface surface = new(logSink, dataReader);
        surface.Bind(new TimberbornGpuVisualFieldSurfaceBinding(
            visualFieldsBuffer: new object(),
            width: 4,
            height: 4,
            depth: 1,
            cellCount: 16,
            strideBytes: 16,
            channels: TimberbornGpuVisualFieldChannels.All));
        surface.MarkUpdated(12);
        return surface;
    }

    private static TimberbornGpuVisualFieldSample Sample(
        int cellIndex,
        float fire,
        float smoke,
        float ash,
        float visibility)
    {
        return new TimberbornGpuVisualFieldSample(
            CellIndex: cellIndex,
            Tick: 1,
            Fire: fire,
            Smoke: smoke,
            Ash: ash,
            Visibility: visibility);
    }

    private static TimberbornFireVisualEffectEvent EffectEvent(int cellIndex, uint tick)
    {
        return new TimberbornFireVisualEffectEvent(
            CellIndex: cellIndex,
            Tick: tick,
            Kind: TimberbornFireVisualEffectKind.HeatChanged,
            Fuel: 10,
            Heat: 10,
            Water: 0,
            IsBurning: true);
    }

    private sealed class RecordingGpuFieldRendererPresenter : ITimberbornGpuFieldRendererPresenter
    {
        public List<TimberbornGpuFieldRendererRegionState> RenderedRegions { get; private set; } = [];

        public TimberbornGpuFieldRendererPresenterState State { get; } = new(
            RendererEnabled: true,
            MaterialReady: true);

        public TimberbornGpuFieldRendererPresentationResult RenderRegions(
            IReadOnlyList<TimberbornGpuFieldRendererRegionState> regions)
        {
            RenderedRegions = regions.ToList();
            return TimberbornGpuFieldRendererPresentationResult.Applied;
        }

        public void Clear()
        {
            RenderedRegions.Clear();
        }
    }

    private sealed class RecordingVisualFieldDataReader(
        IReadOnlyDictionary<int, TimberbornGpuVisualFieldSample> sampleByCellIndex)
        : ITimberbornGpuVisualFieldDataReader
    {
        public int[] RequestedCellIndices { get; private set; } = [];

        public IReadOnlyList<TimberbornGpuVisualFieldSample> ReadSamples(
            TimberbornGpuVisualFieldSurfaceBinding binding,
            IReadOnlyList<int> cellIndices,
            uint? tick)
        {
            RequestedCellIndices = RequestedCellIndices
                .Concat(cellIndices)
                .ToArray();
            return cellIndices
                .Select(cellIndex => sampleByCellIndex[cellIndex])
                .ToArray();
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
