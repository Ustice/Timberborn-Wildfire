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
            new TimberbornGpuFieldRendererOptions(
                RegionSize: 2,
                MaxUpdatedRegionsPerDispatch: 8,
                DebugOverlayEnabled: true),
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
        Assert.Equal(0, sink.Counters.InvisibleRegionCount);
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
            new TimberbornGpuFieldRendererOptions(
                RegionSize: 1,
                MaxUpdatedRegionsPerDispatch: 1,
                DebugOverlayEnabled: true),
            presenter);

        sink.BeginVisualEffectDispatch(2);
        sink.UpdateVisualEffect(EffectEvent(0, 2));
        sink.UpdateVisualEffect(EffectEvent(3, 2));
        sink.CompleteVisualEffectDispatch(2);

        Assert.Single(presenter.RenderedRegions);
        Assert.Equal(1, sink.Counters.VisibleRegionCount);
        Assert.Equal(1, sink.Counters.UpdatedRegionCount);
        Assert.Equal(1, sink.Counters.DroppedRegionCount);
        Assert.Equal(0, sink.Counters.InvisibleRegionCount);
    }

    [Fact]
    public void CountsBelowThresholdRegionsSeparatelyFromDroppedRegions()
    {
        RecordingFireLogSink logSink = new();
        RecordingVisualFieldDataReader dataReader = new(new Dictionary<int, TimberbornGpuVisualFieldSample>
        {
            [0] = Sample(0, fire: 1f, smoke: 0f, ash: 0f, visibility: 1f),
            [3] = Sample(3, fire: 0.001f, smoke: 0f, ash: 0f, visibility: 1f),
        });
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(logSink, dataReader);
        RecordingGpuFieldRendererPresenter presenter = new();
        TimberbornGpuFieldRendererSink sink = new(
            surface,
            logSink,
            new TimberbornGpuFieldRendererOptions(
                RegionSize: 1,
                MaxUpdatedRegionsPerDispatch: 4,
                DebugOverlayEnabled: true),
            presenter);

        sink.BeginVisualEffectDispatch(4);
        sink.UpdateVisualEffect(EffectEvent(0, 4));
        sink.UpdateVisualEffect(EffectEvent(3, 4));
        sink.CompleteVisualEffectDispatch(4);

        Assert.Single(presenter.RenderedRegions);
        Assert.Equal(1, sink.Counters.VisibleRegionCount);
        Assert.Equal(1, sink.Counters.UpdatedRegionCount);
        Assert.Equal(0, sink.Counters.DroppedRegionCount);
        Assert.Equal(1, sink.Counters.InvisibleRegionCount);
        Assert.Contains(
            logSink.InfoMessages,
            message => message.Contains("dropped_regions=0 invisible_regions=1", StringComparison.Ordinal));
    }

    [Fact]
    public void CountsMissingSurfaceBindingAsDroppedRegion()
    {
        RecordingFireLogSink logSink = new();
        RecordingVisualFieldDataReader dataReader = new(new Dictionary<int, TimberbornGpuVisualFieldSample>());
        TimberbornGpuVisualFieldSurface surface = new(logSink, dataReader);
        RecordingGpuFieldRendererPresenter presenter = new();
        TimberbornGpuFieldRendererSink sink = new(
            surface,
            logSink,
            new TimberbornGpuFieldRendererOptions(
                RegionSize: 1,
                MaxUpdatedRegionsPerDispatch: 4,
                DebugOverlayEnabled: true),
            presenter);

        sink.BeginVisualEffectDispatch(6);
        sink.UpdateVisualEffect(EffectEvent(0, 6));
        sink.CompleteVisualEffectDispatch(6);

        Assert.Empty(presenter.RenderedRegions);
        Assert.Equal(0, sink.Counters.VisibleRegionCount);
        Assert.Equal(0, sink.Counters.UpdatedRegionCount);
        Assert.Equal(1, sink.Counters.DroppedRegionCount);
        Assert.Equal(0, sink.Counters.InvisibleRegionCount);
    }

    [Fact]
    public void AshOverlayEnabledRendererIgnoresFireOnlyRegions()
    {
        RecordingFireLogSink logSink = new();
        RecordingVisualFieldDataReader dataReader = new(new Dictionary<int, TimberbornGpuVisualFieldSample>
        {
            [0] = Sample(0, fire: 1f, smoke: 0f, ash: 0f, visibility: 1f),
            [1] = Sample(1, fire: 0f, smoke: 0f, ash: 0.5f, visibility: 1f),
        });
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(logSink, dataReader);
        RecordingGpuFieldRendererPresenter presenter = new();
        TimberbornGpuFieldRendererSink sink = new(
            surface,
            logSink,
            new TimberbornGpuFieldRendererOptions(
                RegionSize: 1,
                AshBlendCellRadius: 0),
            presenter);

        sink.BeginVisualEffectDispatch(10);
        sink.UpdateVisualEffect(EffectEvent(0, 10));
        sink.UpdateVisualEffect(EffectEvent(1, 10));
        sink.CompleteVisualEffectDispatch(10);

        Assert.True(sink.Counters.RendererEnabled);
        Assert.True(sink.Counters.MaterialReady);
        Assert.Equal(1, sink.Counters.VisibleRegionCount);
        Assert.Equal(1, sink.Counters.UpdatedRegionCount);
        Assert.Equal(0, sink.Counters.InvisibleRegionCount);
        Assert.Single(presenter.RenderedRegions);
        Assert.Equal(1, presenter.RenderedRegions[0].RegionId);
        Assert.Contains(
            logSink.InfoMessages,
            message => message.Contains("renderer_enabled=true", StringComparison.Ordinal));
    }

    [Fact]
    public void AshOverlayBleedsTwoTilesOutForSofterEdges()
    {
        RecordingFireLogSink logSink = new();
        RecordingVisualFieldDataReader dataReader = new(new Dictionary<int, TimberbornGpuVisualFieldSample>
        {
            [10] = Sample(10, fire: 0f, smoke: 0f, ash: 0.8f, visibility: 1f),
        });
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(logSink, dataReader);
        RecordingGpuFieldRendererPresenter presenter = new();
        TimberbornGpuFieldRendererSink sink = new(
            surface,
            logSink,
            new TimberbornGpuFieldRendererOptions(
                RegionSize: 1,
                AshBlendCellRadius: 2),
            presenter);

        sink.BeginVisualEffectDispatch(13);
        sink.UpdateVisualEffect(EffectEvent(10, 13));
        sink.CompleteVisualEffectDispatch(13);

        Assert.Equal(16, presenter.RenderedRegions.Count);
        Assert.Contains(presenter.RenderedRegions, static region => region.RegionId == 10 && region.Ash == 0.8f);
        Assert.Contains(presenter.RenderedRegions, static region => region.RegionId == 5 && region.Ash < 0.8f);
        Assert.Contains(presenter.RenderedRegions, static region => region.RegionId == 2 && region.Ash < 0.8f);
        Assert.Contains(
            logSink.InfoMessages,
            message => message.Contains("updated_regions=16", StringComparison.Ordinal));
    }

    [Fact]
    public void AshOverlayRendersAccumulatedVisibleAshRegions()
    {
        RecordingFireLogSink logSink = new();
        RecordingVisualFieldDataReader dataReader = new(new Dictionary<int, TimberbornGpuVisualFieldSample>
        {
            [0] = Sample(0, fire: 0f, smoke: 0f, ash: 0.6f, visibility: 1f),
            [15] = Sample(15, fire: 0f, smoke: 0f, ash: 0.7f, visibility: 1f),
        });
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(logSink, dataReader);
        RecordingGpuFieldRendererPresenter presenter = new();
        TimberbornGpuFieldRendererSink sink = new(
            surface,
            logSink,
            new TimberbornGpuFieldRendererOptions(
                RegionSize: 1,
                AshBlendCellRadius: 0),
            presenter);

        sink.BeginVisualEffectDispatch(14);
        sink.UpdateVisualEffect(EffectEvent(0, 14));
        sink.CompleteVisualEffectDispatch(14);
        sink.BeginVisualEffectDispatch(15);
        sink.UpdateVisualEffect(EffectEvent(15, 15));
        sink.CompleteVisualEffectDispatch(15);

        Assert.Equal(2, presenter.RenderedRegions.Count);
        Assert.Contains(presenter.RenderedRegions, static region => region.RegionId == 0 && region.Ash == 0.6f);
        Assert.Contains(presenter.RenderedRegions, static region => region.RegionId == 15);
        Assert.Equal(2, sink.Counters.VisibleRegionCount);
        Assert.Equal(1, sink.Counters.UpdatedRegionCount);
    }

    [Fact]
    public void RejectsInvalidPersistentAshScarOptions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimberbornGpuFieldRendererOptions(
            PersistentAshScarCellSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimberbornGpuFieldRendererOptions(
            MaxPersistentAshScarRegions: 0));
    }

    [Fact]
    public void AshOverlayPrioritizesNewAshWhenAccumulatedAshExceedsRenderLimit()
    {
        RecordingFireLogSink logSink = new();
        RecordingVisualFieldDataReader dataReader = new(new Dictionary<int, TimberbornGpuVisualFieldSample>
        {
            [0] = Sample(0, fire: 0f, smoke: 0f, ash: 1f, visibility: 1f),
            [1] = Sample(1, fire: 0f, smoke: 0f, ash: 1f, visibility: 1f),
            [2] = Sample(2, fire: 0f, smoke: 0f, ash: 0.5f, visibility: 1f),
        });
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(logSink, dataReader);
        RecordingGpuFieldRendererPresenter presenter = new();
        TimberbornGpuFieldRendererSink sink = new(
            surface,
            logSink,
            new TimberbornGpuFieldRendererOptions(
                RegionSize: 1,
                MaxUpdatedRegionsPerDispatch: 2,
                AshBlendCellRadius: 0),
            presenter);

        sink.BeginVisualEffectDispatch(14);
        sink.UpdateVisualEffect(EffectEvent(0, 14));
        sink.UpdateVisualEffect(EffectEvent(1, 14));
        sink.CompleteVisualEffectDispatch(14);
        sink.BeginVisualEffectDispatch(15);
        sink.UpdateVisualEffect(EffectEvent(2, 15));
        sink.CompleteVisualEffectDispatch(15);

        Assert.Equal(3, sink.Counters.VisibleRegionCount);
        Assert.Equal(2, presenter.RenderedRegions.Count);
        Assert.Contains(presenter.RenderedRegions, static region => region.RegionId == 2);
    }

    [Fact]
    public void AshOverlaySummarizesOldAshIntoPersistentScarWhenDetailLimitIsExceeded()
    {
        RecordingFireLogSink logSink = new();
        RecordingVisualFieldDataReader dataReader = new(new Dictionary<int, TimberbornGpuVisualFieldSample>
        {
            [0] = Sample(0, fire: 0f, smoke: 0f, ash: 0.6f, visibility: 1f),
            [1] = Sample(1, fire: 0f, smoke: 0f, ash: 0.6f, visibility: 1f),
            [2] = Sample(2, fire: 0f, smoke: 0f, ash: 0.6f, visibility: 1f),
            [3] = Sample(3, fire: 0f, smoke: 0f, ash: 0.6f, visibility: 1f),
        });
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(logSink, dataReader, width: 4, height: 1);
        RecordingGpuFieldRendererPresenter presenter = new();
        TimberbornGpuFieldRendererSink sink = new(
            surface,
            logSink,
            new TimberbornGpuFieldRendererOptions(
                RegionSize: 1,
                MaxUpdatedRegionsPerDispatch: 2,
                AshBlendCellRadius: 0,
                PersistentAshScarCellSize: 2,
                MaxPersistentAshScarRegions: 8),
            presenter);

        sink.BeginVisualEffectDispatch(1);
        sink.UpdateVisualEffect(EffectEvent(0, 1));
        sink.UpdateVisualEffect(EffectEvent(1, 1));
        sink.CompleteVisualEffectDispatch(1);
        sink.BeginVisualEffectDispatch(2);
        sink.UpdateVisualEffect(EffectEvent(2, 2));
        sink.UpdateVisualEffect(EffectEvent(3, 2));
        sink.CompleteVisualEffectDispatch(2);

        Assert.Equal([2, 3], presenter.RenderedRegions.Select(static region => region.RegionId).OrderBy(static id => id).ToArray());
        TimberbornGpuFieldRendererScarRegionState scarRegion = Assert.Single(presenter.RenderedScarRegions);
        Assert.Equal(2, scarRegion.SourceRegionCount);
        Assert.Equal(2, sink.Counters.RenderedDetailRegionCount);
        Assert.Equal(1, sink.Counters.RenderedScarRegionCount);
        Assert.Equal(2, sink.Counters.PersistentAshScarSourceRegionCount);
        Assert.Contains(logSink.InfoMessages, static message => message.Contains("rendered_scar_regions=1", StringComparison.Ordinal));
    }

    [Fact]
    public void AshOverlayKeepsFreshAshInDetailLayerWhenOldAshIsSummarized()
    {
        RecordingFireLogSink logSink = new();
        RecordingVisualFieldDataReader dataReader = new(new Dictionary<int, TimberbornGpuVisualFieldSample>
        {
            [0] = Sample(0, fire: 0f, smoke: 0f, ash: 1f, visibility: 1f),
            [1] = Sample(1, fire: 0f, smoke: 0f, ash: 1f, visibility: 1f),
            [2] = Sample(2, fire: 0f, smoke: 0f, ash: 1f, visibility: 1f),
            [3] = Sample(3, fire: 0f, smoke: 0f, ash: 0.4f, visibility: 1f),
        });
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(logSink, dataReader, width: 4, height: 1);
        RecordingGpuFieldRendererPresenter presenter = new();
        TimberbornGpuFieldRendererSink sink = new(
            surface,
            logSink,
            new TimberbornGpuFieldRendererOptions(
                RegionSize: 1,
                MaxUpdatedRegionsPerDispatch: 2,
                AshBlendCellRadius: 0,
                PersistentAshScarCellSize: 2,
                MaxPersistentAshScarRegions: 8),
            presenter);

        sink.BeginVisualEffectDispatch(1);
        sink.UpdateVisualEffect(EffectEvent(0, 1));
        sink.UpdateVisualEffect(EffectEvent(1, 1));
        sink.CompleteVisualEffectDispatch(1);
        sink.BeginVisualEffectDispatch(2);
        sink.UpdateVisualEffect(EffectEvent(3, 2));
        sink.CompleteVisualEffectDispatch(2);

        Assert.Contains(presenter.RenderedRegions, static region => region.RegionId == 3);
        Assert.True(presenter.RenderedRegions.Count(region => region.RegionId is 0 or 1) <= 1);
        Assert.Contains(presenter.RenderedScarRegions, static region => region.SourceRegionCount > 0);
    }

    [Fact]
    public void AshOverlayAccumulatesRepeatedAshOnSameGroundCell()
    {
        RecordingFireLogSink logSink = new();
        RecordingVisualFieldDataReader dataReader = new(new Dictionary<int, TimberbornGpuVisualFieldSample>
        {
            [0] = Sample(0, fire: 0f, smoke: 0f, ash: 0.35f, visibility: 1f),
        });
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(logSink, dataReader);
        RecordingGpuFieldRendererPresenter presenter = new();
        TimberbornGpuFieldRendererSink sink = new(
            surface,
            logSink,
            new TimberbornGpuFieldRendererOptions(
                RegionSize: 1,
                AshBlendCellRadius: 0),
            presenter);

        sink.BeginVisualEffectDispatch(14);
        sink.UpdateVisualEffect(EffectEvent(0, 14));
        sink.CompleteVisualEffectDispatch(14);
        sink.BeginVisualEffectDispatch(15);
        sink.UpdateVisualEffect(EffectEvent(0, 15));
        sink.CompleteVisualEffectDispatch(15);

        Assert.Single(presenter.RenderedRegions);
        Assert.Equal(0.7f, presenter.RenderedRegions[0].Ash, precision: 4);
    }

    [Fact]
    public void AshOverlayProjectsVerticalAshSamplesOntoOneHorizontalSurface()
    {
        RecordingFireLogSink logSink = new();
        RecordingVisualFieldDataReader dataReader = new(new Dictionary<int, TimberbornGpuVisualFieldSample>
        {
            [5] = Sample(5, fire: 0f, smoke: 0f, ash: 0.4f, visibility: 1f),
            [21] = Sample(21, fire: 0f, smoke: 0f, ash: 0.6f, visibility: 1f),
        });
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(logSink, dataReader, depth: 2);
        RecordingGpuFieldRendererPresenter presenter = new();
        TimberbornGpuFieldRendererSink sink = new(
            surface,
            logSink,
            new TimberbornGpuFieldRendererOptions(
                RegionSize: 1,
                AshBlendCellRadius: 0),
            presenter,
            new FixedAshOverlaySurfaceProvider(surfaceZ: 3));

        sink.BeginVisualEffectDispatch(16);
        sink.UpdateVisualEffect(EffectEvent(5, 16));
        sink.UpdateVisualEffect(EffectEvent(21, 16));
        sink.CompleteVisualEffectDispatch(16);

        Assert.All(presenter.RenderedRegions, static region =>
        {
            Assert.Equal(3, region.MinZ);
            Assert.Equal(3, region.MaxZ);
        });
        Assert.Contains(presenter.RenderedRegions, static region => region.RegionId == 5 && region.Ash > 0.4f);
    }

    [Fact]
    public void AshOverlaySkipsFringeCellsWithoutTerrainSurface()
    {
        RecordingFireLogSink logSink = new();
        RecordingVisualFieldDataReader dataReader = new(new Dictionary<int, TimberbornGpuVisualFieldSample>
        {
            [10] = Sample(10, fire: 0f, smoke: 0f, ash: 0.9f, visibility: 1f),
        });
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(logSink, dataReader);
        RecordingGpuFieldRendererPresenter presenter = new();
        TimberbornGpuFieldRendererSink sink = new(
            surface,
            logSink,
            new TimberbornGpuFieldRendererOptions(
                RegionSize: 1,
                AshBlendCellRadius: 2),
            presenter,
            new MissingAshOverlaySurfaceProvider(missingX: 0));

        sink.BeginVisualEffectDispatch(17);
        sink.UpdateVisualEffect(EffectEvent(10, 17));
        sink.CompleteVisualEffectDispatch(17);

        Assert.DoesNotContain(presenter.RenderedRegions, static region => region.MinX == 0);
        Assert.Contains(presenter.RenderedRegions, static region => region.RegionId == 10 && region.Ash == 0.9f);
        Assert.Equal(12, presenter.RenderedRegions.Count);
    }

    [Fact]
    public void GpuFieldRendererCanDisableAshOverlayForHeadlessTelemetry()
    {
        RecordingFireLogSink logSink = new();
        RecordingVisualFieldDataReader dataReader = new(new Dictionary<int, TimberbornGpuVisualFieldSample>
        {
            [0] = Sample(0, fire: 0f, smoke: 0f, ash: 0.6f, visibility: 1f),
        });
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(logSink, dataReader);
        TimberbornGpuFieldRendererSink sink = new(
            surface,
            logSink,
            new TimberbornGpuFieldRendererOptions(AshOverlayEnabled: false),
            NullTimberbornGpuFieldRendererPresenter.Instance);

        sink.BeginVisualEffectDispatch(11);
        sink.UpdateVisualEffect(EffectEvent(0, 11));
        sink.CompleteVisualEffectDispatch(11);

        Assert.False(sink.Counters.RendererEnabled);
        Assert.False(sink.Counters.MaterialReady);
        Assert.Equal(1, sink.Counters.VisibleRegionCount);
        Assert.Equal(1, sink.Counters.UpdatedRegionCount);
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
            GpuFieldRendererInvisibleRegionCount: 4,
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
        Assert.Contains("gpu_field_renderer_invisible_regions=4", result.ResultToken);
        Assert.Contains("gpu_field_renderer_material_failures=0", result.ResultToken);
    }

    private static TimberbornGpuVisualFieldSurface CreateBoundSurface(
        ITimberbornFireLogSink logSink,
        ITimberbornGpuVisualFieldDataReader dataReader,
        int width = 4,
        int height = 4,
        int depth = 1)
    {
        TimberbornGpuVisualFieldSurface surface = new(logSink, dataReader);
        surface.Bind(new TimberbornGpuVisualFieldSurfaceBinding(
            visualFieldsBuffer: new object(),
            width: width,
            height: height,
            depth: depth,
            cellCount: width * height * depth,
            strideBytes: 16,
            channels: TimberbornGpuVisualFieldChannels.All));
        surface.MarkUpdated(12);
        return surface;
    }

    private sealed class FixedAshOverlaySurfaceProvider(int surfaceZ) : ITimberbornAshOverlaySurfaceProvider
    {
        public bool TryProjectToSurfaceZ(int x, int y, int sourceZ, out int projectedSurfaceZ)
        {
            projectedSurfaceZ = surfaceZ;
            return true;
        }
    }

    private sealed class MissingAshOverlaySurfaceProvider(int missingX) : ITimberbornAshOverlaySurfaceProvider
    {
        public bool TryProjectToSurfaceZ(int x, int y, int sourceZ, out int surfaceZ)
        {
            surfaceZ = sourceZ;
            return x != missingX;
        }
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
            OldWater: 0,
            Water: 0,
            IsBurning: true);
    }

    private sealed class RecordingGpuFieldRendererPresenter : ITimberbornGpuFieldRendererPresenter
    {
        public List<TimberbornGpuFieldRendererRegionState> RenderedRegions { get; private set; } = [];

        public List<TimberbornGpuFieldRendererScarRegionState> RenderedScarRegions { get; private set; } = [];

        public TimberbornGpuFieldRendererPresenterState State { get; } = new(
            RendererEnabled: true,
            MaterialReady: true);

        public TimberbornGpuFieldRendererPresentationResult RenderRegions(
            IReadOnlyList<TimberbornGpuFieldRendererRegionState> regions)
        {
            return RenderPresentation(new TimberbornGpuFieldRendererPresentation(
                DetailRegions: regions,
                ScarRegions: Array.Empty<TimberbornGpuFieldRendererScarRegionState>()));
        }

        public TimberbornGpuFieldRendererPresentationResult RenderPresentation(
            TimberbornGpuFieldRendererPresentation presentation)
        {
            RenderedRegions = presentation.DetailRegions.ToList();
            RenderedScarRegions = presentation.ScarRegions.ToList();
            return TimberbornGpuFieldRendererPresentationResult.Applied;
        }

        public void Clear()
        {
            RenderedRegions.Clear();
            RenderedScarRegions.Clear();
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
                .Select(cellIndex => sampleByCellIndex.TryGetValue(cellIndex, out TimberbornGpuVisualFieldSample? sample)
                    ? sample
                    : new TimberbornGpuVisualFieldSample(
                        cellIndex,
                        Tick: null,
                        Fire: 0f,
                        Smoke: 0f,
                        Ash: 0f,
                        Visibility: 0f))
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
