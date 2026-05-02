using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornPooledFireSmokeAshEffectTests
{
    [Fact]
    public void UpdatesBoundedEffectFromVisualFieldSampleSelectedByDelta()
    {
        RecordingFireLogSink logSink = new();
        RecordingVisualFieldDataReader dataReader = new(
            sampleByCellIndex: new Dictionary<int, TimberbornGpuVisualFieldSample>
            {
                [13] = new(
                    CellIndex: 13,
                    Tick: 5,
                    Fire: 0.1f,
                    Smoke: 0.8f,
                    Ash: 0.3f,
                    Visibility: 1f),
            });
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(logSink, dataReader);
        RecordingPooledEffectPresenter presenter = new();
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(MaxActiveEffects: 4, MaxUpdatedVisualRegionsPerDispatch: 8),
            presenter);

        sink.BeginVisualEffectDispatch(5);
        sink.UpdateVisualEffect(EffectEvent(cellIndex: 13, tick: 5));
        sink.CompleteVisualEffectDispatch(5);

        TimberbornPooledFireEffectState state = Assert.Single(presenter.UpdatedEffects);
        Assert.Equal(13, state.CellIndex);
        Assert.Equal(1, state.X);
        Assert.Equal(0, state.Y);
        Assert.Equal(1, state.Z);
        Assert.Equal(TimberbornPooledFireEffectKind.Smoke, state.Kind);
        Assert.Equal(0.8f, state.Intensity, precision: 4);
        Assert.Equal([13], dataReader.RequestedCellIndices);
        Assert.Equal(1, sink.Counters.ActivePooledEffectCount);
        Assert.Equal(1, sink.Counters.UpdatedVisualRegionCount);
        Assert.Equal(1, sink.Counters.LastNonZeroUpdatedVisualRegionCount);
        Assert.Equal(5u, sink.Counters.LastNonZeroUpdatedVisualRegionTick);
        Assert.Equal(4, sink.Counters.MaxActivePooledEffectCount);
        Assert.True(sink.Counters.VisibleEffectsEnabled);
        Assert.True(sink.Counters.NativeEffectPrefabResolved);
        Assert.Equal("native-smoke", sink.Counters.LastNativeEffectPrefabName);
        Assert.Contains(
            logSink.InfoMessages,
            message => message.Contains(
                "wildfire_timberborn_pooled_fire_effects_updated tick=5 active_pooled_effects=1 updated_visual_regions=1"));
    }

    [Fact]
    public void KeepsPoolBoundedAndReplacesWeakestEffectDeterministically()
    {
        RecordingFireLogSink logSink = new();
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(
            logSink,
            new RecordingVisualFieldDataReader(
                sampleByCellIndex: new Dictionary<int, TimberbornGpuVisualFieldSample>
                {
                    [0] = Sample(0, fire: 0.2f, smoke: 0f, ash: 0f),
                    [1] = Sample(1, fire: 0.5f, smoke: 0f, ash: 0f),
                    [2] = Sample(2, fire: 0.8f, smoke: 0f, ash: 0f),
                }));
        RecordingPooledEffectPresenter presenter = new();
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(MaxActiveEffects: 2, MaxUpdatedVisualRegionsPerDispatch: 8),
            presenter);

        sink.BeginVisualEffectDispatch(7);
        new[] { 0, 1, 2 }
            .Select(cellIndex => EffectEvent(cellIndex, tick: 7))
            .ToList()
            .ForEach(sink.UpdateVisualEffect);
        sink.CompleteVisualEffectDispatch(7);

        Assert.Equal(2, sink.Counters.ActivePooledEffectCount);
        Assert.Equal([1, 2], sink.ActiveEffectsByCellIndex.Keys.OrderBy(static cellIndex => cellIndex).ToArray());
        Assert.Equal(3, presenter.UpdatedEffects.Count);
        Assert.DoesNotContain(0, sink.ActiveEffectsByCellIndex.Keys);
        Assert.Contains("dropped_visual_regions=0", logSink.InfoMessages.Last());
    }

    [Fact]
    public void LimitsUpdatedVisualRegionsPerDispatch()
    {
        RecordingFireLogSink logSink = new();
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(
            logSink,
            new RecordingVisualFieldDataReader(
                sampleByCellIndex: new Dictionary<int, TimberbornGpuVisualFieldSample>
                {
                    [0] = Sample(0, fire: 1f, smoke: 0f, ash: 0f),
                    [1] = Sample(1, fire: 1f, smoke: 0f, ash: 0f),
                }));
        RecordingPooledEffectPresenter presenter = new();
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(MaxActiveEffects: 4, MaxUpdatedVisualRegionsPerDispatch: 1),
            presenter);

        sink.BeginVisualEffectDispatch(9);
        new[] { 0, 1 }
            .Select(cellIndex => EffectEvent(cellIndex, tick: 9))
            .ToList()
            .ForEach(sink.UpdateVisualEffect);
        sink.CompleteVisualEffectDispatch(9);

        Assert.Equal(1, sink.Counters.ActivePooledEffectCount);
        Assert.Equal(1, sink.Counters.UpdatedVisualRegionCount);
        Assert.Equal(1, sink.Counters.LastNonZeroUpdatedVisualRegionCount);
        Assert.Equal([0], presenter.UpdatedEffects.Select(static state => state.CellIndex).ToArray());
        Assert.Contains("dropped_visual_regions=1", logSink.InfoMessages.Last());
    }

    [Fact]
    public void DeltaConsumerResetsAndCompletesPooledEffectDispatchEvenWithoutVisualEvents()
    {
        RecordingFireLogSink logSink = new();
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(logSink, new RecordingVisualFieldDataReader());
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(MaxActiveEffects: 2, MaxUpdatedVisualRegionsPerDispatch: 2),
            NullTimberbornPooledFireEffectPresenter.Instance);
        TimberbornFireDeltaConsumer consumer = new(
            logSink,
            new TimberbornFireDeltaConsumerSinks(visualEffectSink: sink));

        consumer.Consume(tick: 12, []);

        Assert.Equal(12u, sink.Counters.LastUpdatedTick);
        Assert.Equal(0, sink.Counters.UpdatedVisualRegionCount);
        Assert.Contains(
            logSink.InfoMessages,
            message => message.Contains(
                "wildfire_timberborn_pooled_fire_effects_updated tick=12 active_pooled_effects=0 updated_visual_regions=0"));
    }

    [Fact]
    public void DisabledPresenterDoesNotClaimVisibleUpdates()
    {
        RecordingFireLogSink logSink = new();
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(
            logSink,
            new RecordingVisualFieldDataReader(
                sampleByCellIndex: new Dictionary<int, TimberbornGpuVisualFieldSample>
                {
                    [0] = Sample(0, fire: 1f, smoke: 0f, ash: 0f),
                }));
        RecordingPooledEffectPresenter presenter = new(
            TimberbornPooledFireEffectPresentationResult.Disabled("native_effect_prefab_unavailable"),
            new TimberbornPooledFireEffectPresenterState(
                VisibleEffectsEnabled: false,
                NativeEffectPrefabResolved: false,
                LastNativeEffectPrefabName: null));
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(MaxActiveEffects: 2, MaxUpdatedVisualRegionsPerDispatch: 2),
            presenter);

        sink.BeginVisualEffectDispatch(14);
        sink.UpdateVisualEffect(EffectEvent(cellIndex: 0, tick: 14));
        sink.CompleteVisualEffectDispatch(14);

        Assert.Equal(0, sink.Counters.ActivePooledEffectCount);
        Assert.Equal(0, sink.Counters.UpdatedVisualRegionCount);
        Assert.Equal(1, sink.Counters.DisabledVisualRegionCount);
        Assert.False(sink.Counters.VisibleEffectsEnabled);
        Assert.False(sink.Counters.NativeEffectPrefabResolved);
        Assert.Contains("disabled_visual_regions=1", logSink.InfoMessages.Last());
        Assert.Contains("visible_effects_enabled=false", logSink.InfoMessages.Last());
    }

    [Fact]
    public void LastNonzeroVisualRegionTelemetrySurvivesLaterEmptyDispatch()
    {
        RecordingFireLogSink logSink = new();
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(
            logSink,
            new RecordingVisualFieldDataReader(
                sampleByCellIndex: new Dictionary<int, TimberbornGpuVisualFieldSample>
                {
                    [0] = Sample(0, fire: 1f, smoke: 0f, ash: 0f),
                }));
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(MaxActiveEffects: 2, MaxUpdatedVisualRegionsPerDispatch: 2),
            new RecordingPooledEffectPresenter());

        sink.BeginVisualEffectDispatch(15);
        sink.UpdateVisualEffect(EffectEvent(cellIndex: 0, tick: 15));
        sink.CompleteVisualEffectDispatch(15);
        sink.BeginVisualEffectDispatch(16);
        sink.CompleteVisualEffectDispatch(16);

        Assert.Equal(0, sink.Counters.UpdatedVisualRegionCount);
        Assert.Equal(1, sink.Counters.LastNonZeroUpdatedVisualRegionCount);
        Assert.Equal(15u, sink.Counters.LastNonZeroUpdatedVisualRegionTick);
        Assert.Contains("last_nonzero_updated_visual_regions=1", logSink.InfoMessages.Last());
        Assert.Contains("last_nonzero_updated_visual_regions_tick=15", logSink.InfoMessages.Last());
    }

    [Fact]
    public void TimberbornFireRuntimeHasSinglePublicBinditoConstructor()
    {
        string source = ReadTimberbornFireRuntimeSource();

        Assert.Equal(1, CountOccurrences(source, "public TimberbornFireRuntime("));
        Assert.Contains("public TimberbornFireRuntime(ITimberbornGpuVisualFieldSurface visualFieldSurface)", source);
        Assert.DoesNotContain("public TimberbornFireRuntime()\n", source);
    }

    [Fact]
    public void UnityPresenterUsesFireGridAxesWithoutSwappingYAndZ()
    {
        TimberbornPooledFireEffectLocalPosition position = TimberbornUnityPooledFireEffectPresenter.ToUnityLocalPosition(
            new TimberbornPooledFireEffectState(
                SlotId: 0,
                CellIndex: 0,
                Tick: 1,
                X: 2,
                Y: 5,
                Z: 7,
                Kind: TimberbornPooledFireEffectKind.Fire,
                Fire: 1f,
                Smoke: 0f,
                Ash: 0f,
                Visibility: 1f,
                Intensity: 1f));

        Assert.Equal(2.5f, position.X);
        Assert.Equal(5.5f, position.Y);
        Assert.Equal(7.5f, position.Z);
    }

    [Fact]
    public void UnityPresenterReusesSlotOnlyWhenEffectKindAndPrefabMatch()
    {
        Assert.True(TimberbornUnityPooledFireEffectPresenter.CanReuseInstance(
            TimberbornPooledFireEffectKind.Smoke,
            "SmelterSmoke",
            TimberbornPooledFireEffectKind.Smoke,
            "SmelterSmoke"));
        Assert.False(TimberbornUnityPooledFireEffectPresenter.CanReuseInstance(
            TimberbornPooledFireEffectKind.Smoke,
            "SmelterSmoke",
            TimberbornPooledFireEffectKind.Fire,
            "SmelterSmoke"));
        Assert.False(TimberbornUnityPooledFireEffectPresenter.CanReuseInstance(
            TimberbornPooledFireEffectKind.Smoke,
            "SmelterSmoke",
            TimberbornPooledFireEffectKind.Smoke,
            "SteamEngineSmoke"));
    }

    private static TimberbornGpuVisualFieldSurface CreateBoundSurface(
        RecordingFireLogSink logSink,
        RecordingVisualFieldDataReader dataReader)
    {
        TimberbornGpuVisualFieldSurface surface = new(logSink, dataReader);
        surface.Bind(new TimberbornGpuVisualFieldSurfaceBinding(
            new object(),
            width: 4,
            height: 3,
            depth: 2,
            cellCount: 24,
            strideBytes: 16,
            channels: TimberbornGpuVisualFieldChannels.All));
        surface.MarkUpdated(5);
        return surface;
    }

    private static TimberbornGpuVisualFieldSample Sample(int cellIndex, float fire, float smoke, float ash)
    {
        return new TimberbornGpuVisualFieldSample(
            CellIndex: cellIndex,
            Tick: 1,
            Fire: fire,
            Smoke: smoke,
            Ash: ash,
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
            Water: 0,
            IsBurning: true);
    }

    private static string ReadTimberbornFireRuntimeSource()
    {
        string path = SelfAndParents(new DirectoryInfo(AppContext.BaseDirectory))
            .Select(directory => Path.Combine(
                directory.FullName,
                "src",
                "Wildfire.Timberborn",
                "TimberbornFireRuntime.cs"))
            .First(File.Exists);

        return File.ReadAllText(path);
    }

    private static int CountOccurrences(string value, string pattern)
    {
        return value.Split(new[] { pattern }, StringSplitOptions.None).Length - 1;
    }

    private static IEnumerable<DirectoryInfo> SelfAndParents(DirectoryInfo directory)
    {
        return directory.Parent is null
            ? [directory]
            : new[] { directory }.Concat(SelfAndParents(directory.Parent));
    }

    private sealed class RecordingVisualFieldDataReader(
        IReadOnlyDictionary<int, TimberbornGpuVisualFieldSample>? sampleByCellIndex = null)
        : ITimberbornGpuVisualFieldDataReader
    {
        public int[] RequestedCellIndices { get; private set; } = [];

        public IReadOnlyList<TimberbornGpuVisualFieldSample> ReadSamples(
            TimberbornGpuVisualFieldSurfaceBinding binding,
            IReadOnlyList<int> cellIndices,
            uint? tick)
        {
            IReadOnlyDictionary<int, TimberbornGpuVisualFieldSample> samples =
                sampleByCellIndex ?? new Dictionary<int, TimberbornGpuVisualFieldSample>();
            RequestedCellIndices = RequestedCellIndices
                .Concat(cellIndices)
                .ToArray();
            return cellIndices
                .Select(cellIndex => samples.ContainsKey(cellIndex)
                    ? samples[cellIndex]
                    : new TimberbornGpuVisualFieldSample(
                        cellIndex,
                        tick,
                        Fire: 0f,
                        Smoke: 0f,
                        Ash: 0f,
                        Visibility: 0f))
                .ToArray();
        }
    }

    private sealed class RecordingPooledEffectPresenter : ITimberbornPooledFireEffectPresenter
    {
        private readonly TimberbornPooledFireEffectPresentationResult _result;

        public RecordingPooledEffectPresenter()
            : this(
                TimberbornPooledFireEffectPresentationResult.Applied("native-smoke"),
                new TimberbornPooledFireEffectPresenterState(
                    VisibleEffectsEnabled: true,
                    NativeEffectPrefabResolved: true,
                    LastNativeEffectPrefabName: "native-smoke"))
        {
        }

        public RecordingPooledEffectPresenter(
            TimberbornPooledFireEffectPresentationResult result,
            TimberbornPooledFireEffectPresenterState state)
        {
            _result = result;
            State = state;
        }

        public List<TimberbornPooledFireEffectState> UpdatedEffects { get; } = [];

        public List<int> ReleasedSlotIds { get; } = [];

        public int ClearCount { get; private set; }

        public TimberbornPooledFireEffectPresenterState State { get; }

        public TimberbornPooledFireEffectPresentationResult UpdateEffect(TimberbornPooledFireEffectState state)
        {
            UpdatedEffects.Add(state);
            return _result;
        }

        public void ReleaseEffect(int slotId)
        {
            ReleasedSlotIds.Add(slotId);
        }

        public void Clear()
        {
            ClearCount++;
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
