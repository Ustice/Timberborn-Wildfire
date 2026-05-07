using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornBeaverHazardAvoidanceTests
{
    [Fact]
    public void AvoidanceRestrictsFireAndSmokeCells()
    {
        FireGrid grid = new(3, 3, 1);
        RecordingHazardBlocker blocker = new(isAvailable: true);
        TimberbornBeaverHazardAvoidanceSink sink = new(
            new RecordingVisualFieldSurface(
                isBound: true,
                samplesByCell: new Dictionary<int, TimberbornGpuVisualFieldSample>
                {
                    [grid.ToIndex(1, 1, 0)] = Sample(grid.ToIndex(1, 1, 0), fire: 0.2f, smoke: 0f),
                    [grid.ToIndex(2, 1, 0)] = Sample(grid.ToIndex(2, 1, 0), fire: 0f, smoke: 0.3f),
                }),
            blocker,
            new RecordingFireLogSink());

        sink.BeginVisualEffectDispatch(4);
        sink.UpdateVisualEffect(EffectEvent(grid.ToIndex(1, 1, 0), 4));
        sink.UpdateVisualEffect(EffectEvent(grid.ToIndex(2, 1, 0), 4));
        sink.CompleteVisualEffectDispatch(4);

        Assert.Equal([grid.ToIndex(1, 1, 0), grid.ToIndex(2, 1, 0)], blocker.RestrictedCellIndices.OrderBy(static index => index));
        Assert.Equal(2, sink.Counters.ObservedHazardCellCount);
        Assert.Equal(2, sink.Counters.RestrictedCellCount);
        Assert.Equal(2, sink.Counters.AppliedRestrictionCount);
        Assert.Equal(0, sink.Counters.FailedRestrictionCount);
    }

    [Fact]
    public void AvoidanceReleasesCellsWhenHazardClears()
    {
        FireGrid grid = new(3, 3, 1);
        RecordingVisualFieldSurface surface = new(
            isBound: true,
            samplesByCell: new Dictionary<int, TimberbornGpuVisualFieldSample>
            {
                [grid.ToIndex(1, 1, 0)] = Sample(grid.ToIndex(1, 1, 0), fire: 0.2f, smoke: 0f),
            });
        RecordingHazardBlocker blocker = new(isAvailable: true);
        TimberbornBeaverHazardAvoidanceSink sink = new(
            surface,
            blocker,
            new RecordingFireLogSink(),
            new TimberbornBeaverHazardAvoidanceOptions(ReleaseAfterMissingTicks: 2));

        sink.BeginVisualEffectDispatch(4);
        sink.UpdateVisualEffect(EffectEvent(grid.ToIndex(1, 1, 0), 4));
        sink.CompleteVisualEffectDispatch(4);
        surface.SamplesByCell = new Dictionary<int, TimberbornGpuVisualFieldSample>
        {
            [grid.ToIndex(1, 1, 0)] = Sample(grid.ToIndex(1, 1, 0), fire: 0f, smoke: 0f),
        };
        sink.BeginVisualEffectDispatch(5);
        sink.UpdateVisualEffect(EffectEvent(grid.ToIndex(1, 1, 0), 5));
        sink.CompleteVisualEffectDispatch(5);

        Assert.Empty(blocker.RestrictedCellIndices);
        Assert.Equal(0, sink.Counters.RestrictedCellCount);
        Assert.Equal(1, sink.Counters.ReleasedRestrictionCount);
    }

    [Fact]
    public void AvoidanceSkipsSafelyWhenNoBlockerApiIsAvailable()
    {
        FireGrid grid = new(3, 3, 1);
        TimberbornBeaverHazardAvoidanceSink sink = new(
            new RecordingVisualFieldSurface(
                isBound: true,
                samplesByCell: new Dictionary<int, TimberbornGpuVisualFieldSample>
                {
                    [grid.ToIndex(1, 1, 0)] = Sample(grid.ToIndex(1, 1, 0), fire: 0.2f, smoke: 0f),
                }),
            NullTimberbornBeaverHazardBlocker.Instance,
            new RecordingFireLogSink());

        sink.BeginVisualEffectDispatch(4);
        sink.UpdateVisualEffect(EffectEvent(grid.ToIndex(1, 1, 0), 4));
        sink.CompleteVisualEffectDispatch(4);

        Assert.False(sink.Counters.AvoidanceEnabled);
        Assert.Equal(1, sink.Counters.ObservedHazardCellCount);
        Assert.Equal(1, sink.Counters.SkippedNoSafeApiCount);
    }

    [Fact]
    public void AvoidanceIgnoresMissingVisualSamplesWithoutThrowing()
    {
        FireGrid grid = new(3, 3, 1);
        RecordingFireLogSink logSink = new();
        TimberbornBeaverHazardAvoidanceSink sink = new(
            new RecordingVisualFieldSurface(
                isBound: true,
                samplesByCell: new Dictionary<int, TimberbornGpuVisualFieldSample>()),
            new RecordingHazardBlocker(isAvailable: true),
            logSink);

        sink.BeginVisualEffectDispatch(4);
        sink.UpdateVisualEffect(EffectEvent(grid.ToIndex(1, 1, 0), 4));
        sink.CompleteVisualEffectDispatch(4);

        Assert.Equal(0, sink.Counters.ObservedHazardCellCount);
        Assert.Equal(1, sink.Counters.FailedRestrictionCount);
        Assert.Contains(
            logSink.WarningMessages,
            static message => message.Contains("stage=inspect", StringComparison.Ordinal));
    }

    [Fact]
    public void AvoidanceBatchesRestrictionChangesPerDispatch()
    {
        FireGrid grid = new(4, 4, 1);
        Dictionary<int, TimberbornGpuVisualFieldSample> samplesByCell = Enumerable.Range(0, 8)
            .ToDictionary(
                cellIndex => cellIndex,
                cellIndex => Sample(cellIndex, fire: 0.2f + cellIndex * 0.01f, smoke: 0f));
        RecordingHazardBlocker blocker = new(isAvailable: true);
        TimberbornBeaverHazardAvoidanceSink sink = new(
            new RecordingVisualFieldSurface(
                isBound: true,
                samplesByCell: samplesByCell,
                width: grid.Width,
                height: grid.Height,
                depth: grid.Depth),
            blocker,
            new RecordingFireLogSink(),
            new TimberbornBeaverHazardAvoidanceOptions(
                MaxRestrictedCells: 8,
                MaxRestrictionChangesPerDispatch: 3));

        sink.BeginVisualEffectDispatch(4);
        samplesByCell.Keys
            .ToList()
            .ForEach(cellIndex => sink.UpdateVisualEffect(EffectEvent(cellIndex, 4)));
        sink.CompleteVisualEffectDispatch(4);
        sink.BeginVisualEffectDispatch(5);
        sink.CompleteVisualEffectDispatch(5);

        Assert.Equal(6, blocker.RestrictedCellIndices.Count);
        Assert.Equal(6, sink.Counters.RestrictedCellCount);
        Assert.Equal(6, sink.Counters.AppliedRestrictionCount);
    }

    private static TimberbornGpuVisualFieldSample Sample(int cellIndex, float fire, float smoke)
    {
        return new TimberbornGpuVisualFieldSample(
            CellIndex: cellIndex,
            Tick: 1,
            Fire: fire,
            Smoke: smoke,
            Ash: 0f,
            Visibility: 1f);
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

    private sealed class RecordingHazardBlocker(bool isAvailable) : ITimberbornBeaverHazardBlocker
    {
        public HashSet<int> RestrictedCellIndices { get; } = [];

        public bool IsAvailable { get; } = isAvailable;

        public bool TryRestrict(TimberbornBeaverHazardCell cell)
        {
            RestrictedCellIndices.Add(cell.CellIndex);
            return IsAvailable;
        }

        public bool TryRelease(int cellIndex)
        {
            RestrictedCellIndices.Remove(cellIndex);
            return IsAvailable;
        }

        public void Clear()
        {
            RestrictedCellIndices.Clear();
        }
    }

    private sealed class RecordingVisualFieldSurface(
        bool isBound,
        IReadOnlyDictionary<int, TimberbornGpuVisualFieldSample> samplesByCell,
        int width = 3,
        int height = 3,
        int depth = 1) : ITimberbornGpuVisualFieldSurface
    {
        public IReadOnlyDictionary<int, TimberbornGpuVisualFieldSample> SamplesByCell { get; set; } = samplesByCell;

        public TimberbornGpuVisualFieldSurfaceState State { get; } = isBound
            ? new TimberbornGpuVisualFieldSurfaceState(
                IsBound: true,
                Width: width,
                Height: height,
                Depth: depth,
                CellCount: width * height * depth,
                StrideBytes: 16,
                Channels: TimberbornGpuVisualFieldChannels.All,
                LastUpdatedTick: 1)
            : TimberbornGpuVisualFieldSurfaceState.Unbound;

        public bool TryGetBinding(out TimberbornGpuVisualFieldSurfaceBinding binding)
        {
            binding = new TimberbornGpuVisualFieldSurfaceBinding(
                visualFieldsBuffer: new object(),
                width: width,
                height: height,
                depth: depth,
                cellCount: width * height * depth,
                strideBytes: 16,
                channels: TimberbornGpuVisualFieldChannels.All);
            return State.IsBound;
        }

        public void Bind(TimberbornGpuVisualFieldSurfaceBinding binding)
        {
        }

        public void MarkUpdated(uint tick)
        {
        }

        public IReadOnlyList<TimberbornGpuVisualFieldSample> InspectCells(IReadOnlyList<int> cellIndices)
        {
            return cellIndices
                .Select(cellIndex => SamplesByCell[cellIndex])
                .ToArray();
        }

        public void Unbind()
        {
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
