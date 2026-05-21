using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornGpuVisualFieldSurfaceTests
{
    [Fact]
    public void ChannelsMatchGpuVisualFieldBufferOrder()
    {
        Assert.Equal(
            ["fire", "smoke", "ash", "visibility"],
            TimberbornGpuVisualFieldChannels.All);
    }

    [Fact]
    public void BindingRejectsUnexpectedChannelOrder()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new TimberbornGpuVisualFieldSurfaceBinding(
                new object(),
                width: 4,
                height: 3,
                depth: 2,
                cellCount: 24,
                strideBytes: 16,
                channels: ["fire", "ash", "smoke", "visibility"]));

        Assert.Contains("fire, smoke, ash, and visibility", exception.Message);
    }

    [Fact]
    public void SurfaceStoresBindingMetadataAndUpdateTick()
    {
        RecordingFireLogSink logSink = new();
        TimberbornSmokeHeightTelemetry telemetry = new(
            Tick: 17,
            SmokeCellCount: 12,
            GroundContactSmokeCellCount: 4,
            AbsoluteGroundSmokeCellCount: 1,
            NearBottomSmokeCellCount: 3,
            LowestSmokeZ: 1,
            HighestSmokeZ: 4,
            PeakSmoke: 6,
            SmokeCellCountAtLowestZ: 2,
            ContaminatedSmokeCellCount: 5,
            SourceSmokeCellCount: 3,
            NonSourceSmokeCellCount: 9,
            NonSourceGroundContactSmokeCellCount: 2,
            MaxNonSourceSmokeDistanceFromSource: 6);
        TimberbornGpuVisualFieldSurface surface = new(
            logSink,
            new RecordingVisualFieldDataReader(
                cellIndex => new TimberbornGpuVisualFieldSample(
                    cellIndex,
                    Tick: 17,
                    Fire: 0f,
                    Smoke: 0f,
                    Ash: 0f,
                    Visibility: 0f),
                telemetry));
        object visualFieldsBuffer = new();

        surface.Bind(new TimberbornGpuVisualFieldSurfaceBinding(
            visualFieldsBuffer,
            width: 4,
            height: 3,
            depth: 2,
            cellCount: 24,
            strideBytes: 16,
            channels: TimberbornGpuVisualFieldChannels.All));
        surface.MarkUpdated(17);

        Assert.True(surface.State.IsBound);
        Assert.Equal(4, surface.State.Width);
        Assert.Equal(3, surface.State.Height);
        Assert.Equal(2, surface.State.Depth);
        Assert.Equal(24, surface.State.CellCount);
        Assert.Equal(16, surface.State.StrideBytes);
        Assert.Equal(TimberbornGpuVisualFieldChannels.All, surface.State.Channels);
        Assert.Equal(17u, surface.State.LastUpdatedTick);
        Assert.Equal(telemetry, surface.State.SmokeHeightTelemetry);
        Assert.Equal(telemetry, surface.SnapshotSmokeHeight());
        Assert.True(surface.TryGetBinding(out TimberbornGpuVisualFieldSurfaceBinding binding));
        Assert.Same(visualFieldsBuffer, binding.VisualFieldsBuffer);
        Assert.Contains(
            "wildfire_timberborn_gpu_visual_field_surface_bound width=4 height=3 depth=2 cell_count=24 stride_bytes=16 channels=fire,smoke,ash,visibility",
            logSink.InfoMessages);
        Assert.Contains(
            "wildfire_timberborn_gpu_visual_field_surface_updated tick=17 cell_count=24 channels=fire,smoke,ash,visibility",
            logSink.InfoMessages);
        Assert.Contains(
            "wildfire_timberborn_smoke_height_sampled tick=17 smoke_cells=12 ground_contact_smoke_cells=4 absolute_ground_smoke_cells=1 near_bottom_smoke_cells=3 lowest_smoke_z=1 highest_smoke_z=4 peak_smoke=6 smoke_cells_at_lowest_z=2 contaminated_smoke_cells=5 source_smoke_cells=3 non_source_smoke_cells=9 non_source_ground_contact_smoke_cells=2 max_non_source_smoke_distance_from_source=6",
            logSink.InfoMessages);
    }

    [Fact]
    public void SurfaceUnbindClearsInspectableState()
    {
        TimberbornGpuVisualFieldSurface surface = new(new RecordingFireLogSink());
        surface.Bind(new TimberbornGpuVisualFieldSurfaceBinding(
            new object(),
            width: 1,
            height: 1,
            depth: 1,
            cellCount: 1,
            strideBytes: 16,
            channels: TimberbornGpuVisualFieldChannels.All));

        surface.Unbind();

        Assert.Equal(TimberbornGpuVisualFieldSurfaceState.Unbound, surface.State);
        Assert.False(surface.TryGetBinding(out _));
    }

    [Fact]
    public void SurfaceProvidesBoundedInspectableSamplesFromBoundBuffer()
    {
        object visualFieldsBuffer = new();
        RecordingVisualFieldDataReader dataReader = new(
            cellIndex => new TimberbornGpuVisualFieldSample(
                cellIndex,
                Tick: 11,
                Fire: cellIndex + 0.1f,
                Smoke: cellIndex + 0.2f,
                Ash: cellIndex + 0.3f,
                Visibility: cellIndex + 0.4f));
        TimberbornGpuVisualFieldSurface surface = new(new RecordingFireLogSink(), dataReader);
        surface.Bind(new TimberbornGpuVisualFieldSurfaceBinding(
            visualFieldsBuffer,
            width: 4,
            height: 3,
            depth: 2,
            cellCount: 24,
            strideBytes: 16,
            channels: TimberbornGpuVisualFieldChannels.All));
        surface.MarkUpdated(11);

        IReadOnlyList<TimberbornGpuVisualFieldSample> samples = surface.InspectCells([0, 7, 23]);

        Assert.Equal([0, 7, 23], samples.Select(static sample => sample.CellIndex).ToArray());
        Assert.Equal([0.1f, 7.1f, 23.1f], samples.Select(static sample => sample.Fire).ToArray());
        Assert.True(surface.TryGetBinding(out TimberbornGpuVisualFieldSurfaceBinding binding));
        Assert.Same(visualFieldsBuffer, binding.VisualFieldsBuffer);
        Assert.Equal([0, 7, 23], dataReader.LastCellIndices);
        Assert.Equal(11u, dataReader.LastTick);
        Assert.Same(visualFieldsBuffer, dataReader.LastBinding?.VisualFieldsBuffer);
    }

    [Fact]
    public void SurfaceRejectsUnboundedOrOutOfRangeInspection()
    {
        TimberbornGpuVisualFieldSurface surface = new(new RecordingFireLogSink());

        Assert.Throws<InvalidOperationException>(() => surface.InspectCells([0]));

        surface.Bind(new TimberbornGpuVisualFieldSurfaceBinding(
            new object(),
            width: 2,
            height: 2,
            depth: 1,
            cellCount: 4,
            strideBytes: 16,
            channels: TimberbornGpuVisualFieldChannels.All));

        Assert.Throws<ArgumentOutOfRangeException>(() => surface.InspectCells([4]));
        Assert.Throws<ArgumentOutOfRangeException>(() => surface.InspectCells(
            Enumerable.Range(0, TimberbornGpuVisualFieldSurface.MaxInspectionCellCount + 1).ToArray()));
    }

    [Fact]
    public void BindingLifecycleUsesRealGridMetadataAndUnbindsOnDisposePath()
    {
        object visualFieldsBuffer = new();
        TimberbornGpuVisualFieldSurface surface = new(new RecordingFireLogSink());
        TimberbornGpuVisualFieldSurfaceBindingLifecycle lifecycle = new(
            surface,
            visualFieldsBuffer,
            new FireGrid(4, 3, 2),
            strideBytes: 16);

        lifecycle.Bind();
        lifecycle.MarkUpdated(19);
        lifecycle.Unbind();
        lifecycle.MarkUpdated(20);

        Assert.Equal(TimberbornGpuVisualFieldSurfaceState.Unbound, surface.State);
        Assert.False(surface.TryGetBinding(out _));
    }

    [Fact]
    public void BindingLifecycleBindsGridMetadataAndMarksDispatchUpdates()
    {
        object visualFieldsBuffer = new();
        object atmosphericFieldsBuffer = new();
        object companionFieldsBuffer = new();
        TimberbornGpuVisualFieldSurface surface = new(new RecordingFireLogSink());
        TimberbornGpuVisualFieldSurfaceBindingLifecycle lifecycle = new(
            surface,
            visualFieldsBuffer,
            atmosphericFieldsBuffer,
            companionFieldsBuffer,
            new FireGrid(4, 3, 2),
            strideBytes: 16);

        lifecycle.Bind();
        lifecycle.MarkUpdated(19);

        Assert.True(surface.State.IsBound);
        Assert.Equal(4, surface.State.Width);
        Assert.Equal(3, surface.State.Height);
        Assert.Equal(2, surface.State.Depth);
        Assert.Equal(24, surface.State.CellCount);
        Assert.Equal(16, surface.State.StrideBytes);
        Assert.Equal(19u, surface.State.LastUpdatedTick);
        Assert.True(surface.TryGetBinding(out TimberbornGpuVisualFieldSurfaceBinding binding));
        Assert.Same(visualFieldsBuffer, binding.VisualFieldsBuffer);
        Assert.Same(atmosphericFieldsBuffer, binding.TransportFieldsBuffer);
        Assert.Same(companionFieldsBuffer, binding.MaterialFieldsBuffer);
    }

    [Fact]
    public void BindingLifecycleRefreshesRestoredTransportAndMaterialBuffersBeforeDispatch()
    {
        object visualFieldsBuffer = new();
        object initialTransportFieldsBuffer = new();
        object initialMaterialFieldsBuffer = new();
        object restoredTransportFieldsBuffer = new();
        object restoredMaterialFieldsBuffer = new();
        TimberbornGpuVisualFieldSurface surface = new(new RecordingFireLogSink());
        TimberbornGpuVisualFieldSurfaceBindingLifecycle lifecycle = new(
            surface,
            visualFieldsBuffer,
            initialTransportFieldsBuffer,
            initialMaterialFieldsBuffer,
            new FireGrid(4, 3, 2),
            strideBytes: 16);

        lifecycle.Bind();
        lifecycle.UpdateTransportFieldsBuffer(restoredTransportFieldsBuffer);
        lifecycle.UpdateMaterialFieldsBuffer(restoredMaterialFieldsBuffer);
        lifecycle.MarkUpdated(41);

        Assert.True(surface.TryGetBinding(out TimberbornGpuVisualFieldSurfaceBinding binding));
        Assert.Same(visualFieldsBuffer, binding.VisualFieldsBuffer);
        Assert.Same(restoredTransportFieldsBuffer, binding.TransportFieldsBuffer);
        Assert.Same(restoredMaterialFieldsBuffer, binding.MaterialFieldsBuffer);
        Assert.Equal(41u, surface.State.LastUpdatedTick);
    }

    [Fact]
    public void ComputeFactoryUsesInjectedSurfaceReachableByConsumer()
    {
        object visualFieldsBuffer = new();
        TimberbornGpuVisualFieldSurface consumerSurface = new(new RecordingFireLogSink());
        TimberbornFireSimParameterPresetState presetState = new();
        presetState.SelectFireSimParameterPreset("harsh");
        TimberbornComputeFireSimulatorFactory factory = new(
            consumerSurface,
            presetState,
            NullTimberbornWindProvider.Instance);
        TimberbornGpuVisualFieldSurfaceBindingLifecycle lifecycle = new(
            factory.VisualFieldSurface,
            visualFieldsBuffer,
            new FireGrid(3, 2, 2),
            strideBytes: 16);

        Assert.Equal("harsh", factory.CurrentPreset.Name);
        Assert.Equal(9u, factory.CurrentPreset.Parameters.IgnitionPoint);

        lifecycle.Bind();
        lifecycle.MarkUpdated(33);

        Assert.Same(consumerSurface, factory.VisualFieldSurface);
        Assert.True(consumerSurface.TryGetBinding(out TimberbornGpuVisualFieldSurfaceBinding binding));
        Assert.Same(visualFieldsBuffer, binding.VisualFieldsBuffer);
        Assert.Equal(12, consumerSurface.State.CellCount);
        Assert.Equal(33u, consumerSurface.State.LastUpdatedTick);

        lifecycle.Unbind();

        Assert.False(consumerSurface.TryGetBinding(out _));
        Assert.False(consumerSurface.State.IsBound);
    }

    [Fact]
    public void LiveSurfaceProvidesRuntimeComputeBufferReader()
    {
        TimberbornLiveGpuVisualFieldSurface surface = new();

        Assert.Equal(TimberbornGpuVisualFieldSurfaceState.Unbound, surface.State);
    }

    [Fact]
    public void FireSystemReportsVisualFieldSurfaceStateWithoutChangingGameplayDeltas()
    {
        TimberbornGpuVisualFieldSurfaceState surfaceState = new(
            IsBound: true,
            Width: 2,
            Height: 2,
            Depth: 1,
            CellCount: 4,
            StrideBytes: 16,
            Channels: TimberbornGpuVisualFieldChannels.All,
            LastUpdatedTick: 9);
        RecordingVisualFieldSimulator simulator = new(surfaceState);
        TimberbornFireSystem fireSystem = new(simulator);

        GpuFireStepResult result = fireSystem.Tick();

        Assert.Equal(surfaceState, fireSystem.VisualFieldSurfaceState);
        Assert.Equal(1u, result.Tick);
        Assert.Empty(result.Deltas);
        Assert.Equal(1, simulator.TickCallCount);
        Assert.Equal(0, fireSystem.LastDeltaConsumerSummary.GameplayConsequenceCount);
    }

    [Fact]
    public void QaResultTokenIncludesVisualFieldSurfaceTelemetry()
    {
        TimberbornQaCommandState state = new(
            IsSimulatorIntegrated: true,
            IsGameContextRuntimeLoaded: true,
            Width: 4,
            Height: 3,
            Depth: 2,
            TickCount: 9,
            VisualFieldSurfaceBound: true,
            VisualFieldSurfaceCellCount: 24,
            VisualFieldSurfaceLastUpdatedTick: 9,
            SmokeHeightSmokeCellCount: 12,
            SmokeHeightGroundContactSmokeCellCount: 4,
            SmokeHeightAbsoluteGroundSmokeCellCount: 1,
            SmokeHeightNearBottomSmokeCellCount: 3,
            SmokeHeightLowestSmokeZ: 1,
            SmokeHeightHighestSmokeZ: 4,
            SmokeHeightPeakSmoke: 6,
            SmokeHeightSmokeCellCountAtLowestZ: 2,
            SmokeHeightContaminatedSmokeCellCount: 5,
            SmokeHeightSourceSmokeCellCount: 3,
            SmokeHeightNonSourceSmokeCellCount: 9,
            SmokeHeightNonSourceGroundContactSmokeCellCount: 2,
            SmokeHeightMaxNonSourceSmokeDistanceFromSource: 6);
        TimberbornQaCommandBridge bridge = new(
            new RecordingStateProvider(state),
            new RecordingQaLogSink());

        TimberbornQaCommandResult result = bridge.Execute("qa-readiness");

        Assert.Contains("visual_field_surface_bound=true", result.ResultToken);
        Assert.Contains("visual_field_surface_cells=24", result.ResultToken);
        Assert.Contains("visual_field_surface_updated_tick=9", result.ResultToken);
        Assert.Contains("smoke_height_smoke_cells=12", result.ResultToken);
        Assert.Contains("smoke_height_ground_contact_smoke_cells=4", result.ResultToken);
        Assert.Contains("smoke_height_absolute_ground_smoke_cells=1", result.ResultToken);
        Assert.Contains("smoke_height_near_bottom_smoke_cells=3", result.ResultToken);
        Assert.Contains("smoke_height_lowest_smoke_z=1", result.ResultToken);
        Assert.Contains("smoke_height_highest_smoke_z=4", result.ResultToken);
        Assert.Contains("smoke_height_peak_smoke=6", result.ResultToken);
        Assert.Contains("smoke_height_smoke_cells_at_lowest_z=2", result.ResultToken);
        Assert.Contains("smoke_height_contaminated_smoke_cells=5", result.ResultToken);
        Assert.Contains("smoke_height_source_smoke_cells=3", result.ResultToken);
        Assert.Contains("smoke_height_non_source_smoke_cells=9", result.ResultToken);
        Assert.Contains("smoke_height_non_source_ground_contact_smoke_cells=2", result.ResultToken);
        Assert.Contains("smoke_height_max_non_source_smoke_distance_from_source=6", result.ResultToken);
    }

    private sealed class RecordingVisualFieldSimulator(TimberbornGpuVisualFieldSurfaceState surfaceState) :
        IGpuFireSimulator,
        ITimberbornGpuVisualFieldStateProvider
    {
        public int Width => 2;

        public int Height => 2;

        public int Depth => 1;

        public int TickCallCount { get; private set; }

        public TimberbornGpuVisualFieldSurfaceState VisualFieldSurfaceState => surfaceState;

        public void RegisterChange(FireSimChange change)
        {
        }

        public GpuFireStepResult Tick()
        {
            TickCallCount++;
            return new GpuFireStepResult([], Tick: (uint)TickCallCount);
        }

        public IDisposable Subscribe(IFireSimListener listener)
        {
            return NullDisposable.Instance;
        }
    }

    private sealed class RecordingStateProvider(TimberbornQaCommandState state) : ITimberbornQaCommandStateProvider
    {
        public TimberbornQaCommandState GetState()
        {
            return state;
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

    private sealed class RecordingVisualFieldDataReader(
        Func<int, TimberbornGpuVisualFieldSample> sampleFactory,
        TimberbornSmokeHeightTelemetry? smokeHeightTelemetry = null) : ITimberbornGpuVisualFieldDataReader
    {
        public TimberbornGpuVisualFieldSurfaceBinding? LastBinding { get; private set; }

        public int[] LastCellIndices { get; private set; } = [];

        public uint? LastTick { get; private set; }

        public IReadOnlyList<TimberbornGpuVisualFieldSample> ReadSamples(
            TimberbornGpuVisualFieldSurfaceBinding binding,
            IReadOnlyList<int> cellIndices,
            uint? tick)
        {
            LastBinding = binding;
            LastCellIndices = cellIndices.ToArray();
            LastTick = tick;
            return cellIndices.Select(sampleFactory).ToArray();
        }

        public TimberbornSmokeHeightTelemetry ReadSmokeHeightTelemetry(
            TimberbornGpuVisualFieldSurfaceBinding binding,
            uint? tick)
        {
            return smokeHeightTelemetry ?? (TimberbornSmokeHeightTelemetry.Empty with { Tick = tick });
        }
    }

    private sealed class RecordingQaLogSink : ITimberbornQaCommandLogSink
    {
        public void Info(string message)
        {
        }

        public void Warning(string message)
        {
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
