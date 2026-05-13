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
                    Visibility: 1f,
                    AtmosphericSmoke: 0.8f),
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

        TimberbornPooledFireEffectState[] states = presenter.UpdatedEffects
            .OrderBy(static state => state.Kind)
            .ToArray();
        Assert.Single(states);
        Assert.All(states, state =>
        {
            Assert.Equal(13, state.CellIndex);
            Assert.Equal(1, state.X);
            Assert.Equal(0, state.Y);
            Assert.Equal(1, state.Z);
        });
        TimberbornPooledFireEffectState smokeState = Assert.Single(
            states,
            static state => state.Kind == TimberbornPooledFireEffectKind.Smoke);
        Assert.Equal(0.8f, smokeState.Intensity, precision: 4);
        Assert.DoesNotContain(states, static state => state.Kind == TimberbornPooledFireEffectKind.Ash);
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
    public void PresentsIndependentAtmosphericLanesForOneCell()
    {
        RecordingFireLogSink logSink = new();
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(
            logSink,
            new RecordingVisualFieldDataReader(
                sampleByCellIndex: new Dictionary<int, TimberbornGpuVisualFieldSample>
                {
                    [4] = Sample(
                        4,
                        fire: 0.9f,
                        smoke: 0.7f,
                        ash: 0.5f,
                        steam: 3f / 7f,
                        smokeContamination: 0.6f,
                        ashContamination: 0.4f),
                }));
        RecordingPooledEffectPresenter presenter = new();
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(MaxActiveEffects: 8, MaxUpdatedVisualRegionsPerDispatch: 8),
            presenter);

        sink.BeginVisualEffectDispatch(6);
        sink.UpdateVisualEffect(new TimberbornFireVisualEffectEvent(
            CellIndex: 4,
            Tick: 6,
            Kind: TimberbornFireVisualEffectKind.WaterChanged,
            Fuel: 10,
            Heat: 10,
            OldWater: 3,
            Water: 2,
            IsBurning: true));
        sink.CompleteVisualEffectDispatch(6);

        Assert.Equal(
            [
                TimberbornPooledFireEffectKind.Fire,
                TimberbornPooledFireEffectKind.Smoke,
                TimberbornPooledFireEffectKind.ToxicSmoke,
                TimberbornPooledFireEffectKind.Steam,
            ],
            presenter.UpdatedEffects
                .Select(static state => state.Kind)
                .OrderBy(static kind => kind)
                .ToArray());
        Assert.Equal(4, sink.Counters.ActivePooledEffectCount);
        Assert.Equal(4, sink.Counters.UpdatedVisualRegionCount);
    }

    [Fact]
    public void AtmosphericUpdatesCreateFreshParticlesWhilePreviousParticlesFinish()
    {
        RecordingFireLogSink logSink = new();
        Dictionary<int, TimberbornGpuVisualFieldSample> samples = new()
        {
            [4] = Sample(4, fire: 0f, smoke: 0.7f, ash: 0f),
        };
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(
            logSink,
            new RecordingVisualFieldDataReader(sampleByCellIndex: samples));
        RecordingPooledEffectPresenter presenter = new();
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(MaxActiveEffects: 4, MaxUpdatedVisualRegionsPerDispatch: 8),
            presenter);

        sink.BeginVisualEffectDispatch(30);
        sink.UpdateVisualEffect(EffectEvent(cellIndex: 4, tick: 30));
        sink.CompleteVisualEffectDispatch(30);
        sink.BeginVisualEffectDispatch(31);
        sink.UpdateVisualEffect(EffectEvent(cellIndex: 4, tick: 31));
        sink.CompleteVisualEffectDispatch(31);

        Assert.Equal(2, sink.Counters.ActivePooledEffectCount);
        Assert.Equal(2, presenter.ActiveEffects.Count);
        Assert.Contains(
            presenter.ActiveEffects.Values,
            static state => state.Kind == TimberbornPooledFireEffectKind.Smoke && state.Intensity == 0f);
        Assert.Contains(
            presenter.ActiveEffects.Values,
            static state => state.Kind == TimberbornPooledFireEffectKind.Smoke && state.Intensity > 0f);
        Assert.Empty(presenter.ReleasedSlotIds);
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
                    [0] = Sample(0, fire: 0f, smoke: 0.2f, ash: 0f),
                    [1] = Sample(1, fire: 0f, smoke: 0.5f, ash: 0f),
                    [2] = Sample(2, fire: 0f, smoke: 0.8f, ash: 0f),
                }));
        RecordingPooledEffectPresenter presenter = new(
            TimberbornPooledFireEffectPresentationResult.Applied(null),
            new TimberbornPooledFireEffectPresenterState(
                VisibleEffectsEnabled: false,
                NativeEffectPrefabResolved: false,
                LastNativeEffectPrefabName: null));
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
    public void PooledPresenterRoutesFireSamplesToProceduralFireParticles()
    {
        RecordingFireLogSink logSink = new();
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(
            logSink,
            new RecordingVisualFieldDataReader(
                sampleByCellIndex: new Dictionary<int, TimberbornGpuVisualFieldSample>
                {
                    [0] = Sample(0, fire: 1f, smoke: 0.1f, ash: 0f),
                }));
        RecordingPooledEffectPresenter presenter = new(
            TimberbornPooledFireEffectPresentationResult.Applied("WildfireProceduralFireParticles"),
            new TimberbornPooledFireEffectPresenterState(
                VisibleEffectsEnabled: true,
                NativeEffectPrefabResolved: true,
                LastNativeEffectPrefabName: "WildfireProceduralFireParticles"));
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(MaxActiveEffects: 4, MaxUpdatedVisualRegionsPerDispatch: 8),
            presenter);

        sink.BeginVisualEffectDispatch(8);
        sink.UpdateVisualEffect(EffectEvent(cellIndex: 0, tick: 8));
        sink.CompleteVisualEffectDispatch(8);

        TimberbornPooledFireEffectState state = Assert.Single(presenter.UpdatedEffects);
        Assert.Equal(TimberbornPooledFireEffectKind.Fire, state.Kind);
        Assert.Equal(1, sink.Counters.ActivePooledEffectCount);
        Assert.Equal(1, sink.Counters.UpdatedVisualRegionCount);
        Assert.True(sink.Counters.NativeEffectPrefabResolved);
        Assert.Equal("WildfireProceduralFireParticles", sink.Counters.LastNativeEffectPrefabName);
    }

    [Fact]
    public void SteamIntensityComesFromAtmosphericSteamSample()
    {
        RecordingFireLogSink logSink = new();
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(
            logSink,
            new RecordingVisualFieldDataReader(
                sampleByCellIndex: new Dictionary<int, TimberbornGpuVisualFieldSample>
                {
                    [6] = Sample(6, fire: 0.1f, smoke: 0f, ash: 0f, steam: 5f / 7f),
                }));
        RecordingPooledEffectPresenter presenter = new();
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(MaxActiveEffects: 4, MaxUpdatedVisualRegionsPerDispatch: 8),
            presenter);

        sink.BeginVisualEffectDispatch(21);
        sink.UpdateVisualEffect(new TimberbornFireVisualEffectEvent(
            CellIndex: 6,
            Tick: 21,
            Kind: TimberbornFireVisualEffectKind.WaterChanged,
            Fuel: 10,
            Heat: 8,
            OldWater: 3,
            Water: 1,
            IsBurning: false));
        sink.CompleteVisualEffectDispatch(21);

        TimberbornPooledFireEffectState state = Assert.Single(presenter.UpdatedEffects);
        Assert.Equal(TimberbornPooledFireEffectKind.Steam, state.Kind);
        Assert.Equal(2f / 3f, state.MoistureDrop, precision: 4);
        Assert.Equal(5f / 7f, state.Steam, precision: 4);
        Assert.Equal(5f / 7f, state.Intensity, precision: 4);
    }

    [Fact]
    public void AtmosphericSteamSampleCreatesSteamWithoutPackedWaterDrop()
    {
        RecordingFireLogSink logSink = new();
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(
            logSink,
            new RecordingVisualFieldDataReader(
                sampleByCellIndex: new Dictionary<int, TimberbornGpuVisualFieldSample>
                {
                    [6] = Sample(6, fire: 0.1f, smoke: 0f, ash: 0f, steam: 5f / 7f),
                }));
        RecordingPooledEffectPresenter presenter = new();
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(MaxActiveEffects: 4, MaxUpdatedVisualRegionsPerDispatch: 8),
            presenter);

        sink.BeginVisualEffectDispatch(22);
        sink.UpdateVisualEffect(new TimberbornFireVisualEffectEvent(
            CellIndex: 6,
            Tick: 22,
            Kind: TimberbornFireVisualEffectKind.WaterChanged,
            Fuel: 10,
            Heat: 8,
            OldWater: 2,
            Water: 2,
            IsBurning: false));
        sink.CompleteVisualEffectDispatch(22);

        TimberbornPooledFireEffectState state = Assert.Single(presenter.UpdatedEffects);
        Assert.Equal(TimberbornPooledFireEffectKind.Steam, state.Kind);
        Assert.Equal(5f / 7f, state.Steam, precision: 4);
        Assert.Equal(1, sink.Counters.ActivePooledEffectCount);
    }

    [Fact]
    public void SteamEffectsStopEmittingBeforeReleaseWhenAtmosphericSteamEnds()
    {
        RecordingFireLogSink logSink = new();
        Dictionary<int, TimberbornGpuVisualFieldSample> samples = new()
        {
            [6] = Sample(6, fire: 0.1f, smoke: 0f, ash: 0f, steam: 5f / 7f),
        };
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(
            logSink,
            new RecordingVisualFieldDataReader(sampleByCellIndex: samples));
        RecordingPooledEffectPresenter presenter = new();
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(
                MaxActiveEffects: 4,
                MaxUpdatedVisualRegionsPerDispatch: 8,
                SteamEffectLifetimeTicks: 2),
            presenter);

        sink.BeginVisualEffectDispatch(21);
        sink.UpdateVisualEffect(new TimberbornFireVisualEffectEvent(
            CellIndex: 6,
            Tick: 21,
            Kind: TimberbornFireVisualEffectKind.WaterChanged,
            Fuel: 10,
            Heat: 8,
            OldWater: 3,
            Water: 1,
            IsBurning: false));
        sink.CompleteVisualEffectDispatch(21);
        samples[6] = Sample(6, fire: 0.1f, smoke: 0f, ash: 0f, steam: 0f);
        sink.BeginVisualEffectDispatch(22);
        sink.CompleteVisualEffectDispatch(22);
        sink.BeginVisualEffectDispatch(23);
        sink.CompleteVisualEffectDispatch(23);
        sink.BeginVisualEffectDispatch(24);
        sink.CompleteVisualEffectDispatch(24);

        Assert.Equal(0, sink.Counters.ActivePooledEffectCount);
        Assert.Equal([0], presenter.ReleasedSlotIds);
    }

    [Fact]
    public void FireParticlesStopEmittingBeforeReleaseWhenCurrentVisualFieldDropsBelowThreshold()
    {
        RecordingFireLogSink logSink = new();
        Dictionary<int, TimberbornGpuVisualFieldSample> samples = new()
        {
            [4] = Sample(4, fire: 1f, smoke: 0f, ash: 0f),
        };
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(
            logSink,
            new RecordingVisualFieldDataReader(sampleByCellIndex: samples));
        RecordingPooledEffectPresenter presenter = new();
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(MaxActiveEffects: 4, MaxUpdatedVisualRegionsPerDispatch: 8),
            presenter);

        sink.BeginVisualEffectDispatch(31);
        sink.UpdateVisualEffect(EffectEvent(cellIndex: 4, tick: 31));
        sink.CompleteVisualEffectDispatch(31);

        samples[4] = Sample(4, fire: 0f, smoke: 0f, ash: 0f);
        sink.BeginVisualEffectDispatch(32);
        sink.CompleteVisualEffectDispatch(32);

        Assert.Equal(1, sink.Counters.ActivePooledEffectCount);
        Assert.Empty(presenter.ReleasedSlotIds);
        Assert.Equal(0f, presenter.UpdatedEffects.Last().Intensity);

        sink.BeginVisualEffectDispatch(33);
        sink.CompleteVisualEffectDispatch(33);

        Assert.Equal(0, sink.Counters.ActivePooledEffectCount);
        Assert.Equal([0], presenter.ReleasedSlotIds);
    }

    [Fact]
    public void RefreshesActiveEffectsInVisualFieldInspectionBatches()
    {
        RecordingFireLogSink logSink = new();
        Dictionary<int, TimberbornGpuVisualFieldSample> samples = Enumerable.Range(0, 300)
            .ToDictionary(
                static cellIndex => cellIndex,
                static cellIndex => Sample(cellIndex, fire: 1f, smoke: 0f, ash: 0f));
        RecordingVisualFieldDataReader dataReader = new(samples);
        TimberbornGpuVisualFieldSurface surface = new(logSink, dataReader);
        surface.Bind(new TimberbornGpuVisualFieldSurfaceBinding(
            new object(),
            width: 20,
            height: 15,
            depth: 2,
            cellCount: 600,
            strideBytes: 16,
            channels: TimberbornGpuVisualFieldChannels.All));
        surface.MarkUpdated(41);
        RecordingPooledEffectPresenter presenter = new();
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(MaxActiveEffects: 320, MaxUpdatedVisualRegionsPerDispatch: 320),
            presenter);

        sink.BeginVisualEffectDispatch(41);
        samples.Keys
            .Select(cellIndex => EffectEvent(cellIndex, tick: 41))
            .ToList()
            .ForEach(sink.UpdateVisualEffect);
        sink.CompleteVisualEffectDispatch(41);

        samples.Keys
            .ToList()
            .ForEach(cellIndex => samples[cellIndex] = Sample(cellIndex, fire: 0f, smoke: 0f, ash: 0f));
        sink.BeginVisualEffectDispatch(42);
        sink.CompleteVisualEffectDispatch(42);

        Assert.Equal(300, sink.Counters.ActivePooledEffectCount);
        Assert.Empty(presenter.ReleasedSlotIds);
        Assert.Equal(300, presenter.UpdatedEffects.Count(static state => state.Tick == 41 && state.Intensity == 0f));

        sink.BeginVisualEffectDispatch(43);
        sink.CompleteVisualEffectDispatch(43);

        Assert.Equal(0, sink.Counters.ActivePooledEffectCount);
        Assert.Equal(300, presenter.ReleasedSlotIds.Distinct().Count());
        Assert.Equal(600, dataReader.RequestedCellIndices.Length);
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
                    [0] = Sample(0, fire: 0f, smoke: 1f, ash: 0f),
                    [1] = Sample(1, fire: 0f, smoke: 1f, ash: 0f),
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
    public void EmptyVisualSamplesDoNotExhaustUpdatedVisualRegionBudget()
    {
        RecordingFireLogSink logSink = new();
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(
            logSink,
            new RecordingVisualFieldDataReader(
                sampleByCellIndex: new Dictionary<int, TimberbornGpuVisualFieldSample>
                {
                    [0] = Sample(0, fire: 0f, smoke: 0f, ash: 0f),
                    [1] = Sample(1, fire: 0f, smoke: 0f, ash: 0f),
                    [2] = Sample(2, fire: 0f, smoke: 1f, ash: 0f),
                }));
        RecordingPooledEffectPresenter presenter = new();
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(MaxActiveEffects: 4, MaxUpdatedVisualRegionsPerDispatch: 1),
            presenter);

        sink.BeginVisualEffectDispatch(10);
        new[] { 0, 1, 2 }
            .Select(cellIndex => EffectEvent(cellIndex, tick: 10))
            .ToList()
            .ForEach(sink.UpdateVisualEffect);
        sink.CompleteVisualEffectDispatch(10);

        TimberbornPooledFireEffectState state = Assert.Single(presenter.UpdatedEffects);
        Assert.Equal(2, state.CellIndex);
        Assert.Equal(1, sink.Counters.ActivePooledEffectCount);
        Assert.Equal(1, sink.Counters.UpdatedVisualRegionCount);
        Assert.Contains("dropped_visual_regions=0", logSink.InfoMessages.Last());
    }

    [Fact]
    public void SaturatedPoolPreservesSmokeWhenFireWouldOtherwiseEvictIt()
    {
        RecordingFireLogSink logSink = new();
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(
            logSink,
            new RecordingVisualFieldDataReader(
                sampleByCellIndex: new Dictionary<int, TimberbornGpuVisualFieldSample>
                {
                    [0] = Sample(0, fire: 1f, smoke: 0.8f, ash: 0f),
                    [1] = Sample(1, fire: 1f, smoke: 0.8f, ash: 0f),
                }));
        RecordingPooledEffectPresenter presenter = new();
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(MaxActiveEffects: 2, MaxUpdatedVisualRegionsPerDispatch: 8),
            presenter);

        sink.BeginVisualEffectDispatch(11);
        new[] { 0, 1 }
            .Select(cellIndex => EffectEvent(cellIndex, tick: 11))
            .ToList()
            .ForEach(sink.UpdateVisualEffect);
        sink.CompleteVisualEffectDispatch(11);

        Assert.Contains(
            presenter.ActiveEffects.Values,
            static state => state.Kind == TimberbornPooledFireEffectKind.Smoke);
    }

    [Fact]
    public void SmokeCanEnterSaturatedFireOnlyPoolBelowReservation()
    {
        RecordingFireLogSink logSink = new();
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(
            logSink,
            new RecordingVisualFieldDataReader(
                sampleByCellIndex: new Dictionary<int, TimberbornGpuVisualFieldSample>
                {
                    [0] = Sample(0, fire: 1f, smoke: 0f, ash: 0f),
                    [1] = Sample(1, fire: 1f, smoke: 0f, ash: 0f),
                    [2] = Sample(2, fire: 0f, smoke: 0.8f, ash: 0f),
                }));
        RecordingPooledEffectPresenter presenter = new();
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(MaxActiveEffects: 2, MaxUpdatedVisualRegionsPerDispatch: 8),
            presenter);

        sink.BeginVisualEffectDispatch(12);
        new[] { 0, 1, 2 }
            .Select(cellIndex => EffectEvent(cellIndex, tick: 12))
            .ToList()
            .ForEach(sink.UpdateVisualEffect);
        sink.CompleteVisualEffectDispatch(12);

        Assert.Contains(
            presenter.ActiveEffects.Values,
            static state => state.Kind == TimberbornPooledFireEffectKind.Smoke);
    }

    [Fact]
    public void SaturatedPoolPreservesSteamWhenFireWouldOtherwiseEvictIt()
    {
        RecordingFireLogSink logSink = new();
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(
            logSink,
            new RecordingVisualFieldDataReader(
                sampleByCellIndex: new Dictionary<int, TimberbornGpuVisualFieldSample>
                {
                    [0] = Sample(0, fire: 1f, smoke: 0f, ash: 0f),
                    [1] = Sample(1, fire: 1f, smoke: 0f, ash: 0f),
                    [2] = Sample(2, fire: 0f, smoke: 0f, ash: 0f, steam: 5f / 7f),
                }));
        RecordingPooledEffectPresenter presenter = new();
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(MaxActiveEffects: 2, MaxUpdatedVisualRegionsPerDispatch: 8),
            presenter);

        sink.BeginVisualEffectDispatch(13);
        new[]
            {
                EffectEvent(0, tick: 13),
                EffectEvent(1, tick: 13),
                new TimberbornFireVisualEffectEvent(
                    CellIndex: 2,
                    Tick: 13,
                    Kind: TimberbornFireVisualEffectKind.WaterChanged,
                    Fuel: 10,
                    Heat: 8,
                    OldWater: 3,
                    Water: 1,
                    IsBurning: false),
            }
            .ToList()
            .ForEach(sink.UpdateVisualEffect);
        sink.CompleteVisualEffectDispatch(13);

        Assert.Contains(
            presenter.ActiveEffects.Values,
            static state => state.Kind == TimberbornPooledFireEffectKind.Steam);
    }

    [Fact]
    public void AtmosphericParticleEffectsCanBeDisabledWhenCloudRendererOwnsSmokeAndSteam()
    {
        RecordingFireLogSink logSink = new();
        TimberbornGpuVisualFieldSurface surface = CreateBoundSurface(
            logSink,
            new RecordingVisualFieldDataReader(
                sampleByCellIndex: new Dictionary<int, TimberbornGpuVisualFieldSample>
                {
                    [0] = Sample(0, fire: 1f, smoke: 0.8f, ash: 0f, steam: 5f / 7f, smokeContamination: 0.6f),
                }));
        RecordingPooledEffectPresenter presenter = new();
        TimberbornPooledFireSmokeAshEffectSink sink = new(
            surface,
            logSink,
            new TimberbornPooledFireEffectOptions(
                MaxActiveEffects: 4,
                MaxUpdatedVisualRegionsPerDispatch: 8,
                AtmosphericParticleEffectsEnabled: false),
            presenter);

        sink.BeginVisualEffectDispatch(17);
        sink.UpdateVisualEffect(new TimberbornFireVisualEffectEvent(
            CellIndex: 0,
            Tick: 17,
            Kind: TimberbornFireVisualEffectKind.WaterChanged,
            Fuel: 10,
            Heat: 8,
            OldWater: 3,
            Water: 1,
            IsBurning: true));
        sink.CompleteVisualEffectDispatch(17);

        TimberbornPooledFireEffectState state = Assert.Single(presenter.UpdatedEffects);
        Assert.Equal(TimberbornPooledFireEffectKind.Fire, state.Kind);
        Assert.Equal(1, sink.Counters.ActivePooledEffectCount);
        Assert.Equal(1, sink.Counters.UpdatedVisualRegionCount);
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
                    [0] = Sample(0, fire: 0f, smoke: 1f, ash: 0f),
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
                    [0] = Sample(0, fire: 0f, smoke: 1f, ash: 0f),
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
    public void UnityPresenterMapsFireGridZToUnityHeight()
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
                Steam: 0f,
                Ash: 0f,
                Visibility: 1f,
                MoistureDrop: 0f,
                Intensity: 1f));

        Assert.Equal(2.5f, position.X);
        Assert.InRange(position.Y, 7.01f, 7.03f);
        Assert.Equal(5.5f, position.Z);
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

    [Fact]
    public void UnityPresenterUpdatesTransformWhenReusingEffectInstances()
    {
        Assert.True(TimberbornUnityPooledFireEffectPresenter.ShouldUpdateTransformForReusedInstance(
            TimberbornPooledFireEffectKind.Fire));
        Assert.True(TimberbornUnityPooledFireEffectPresenter.ShouldUpdateTransformForReusedInstance(
            TimberbornPooledFireEffectKind.Smoke));
        Assert.True(TimberbornUnityPooledFireEffectPresenter.ShouldUpdateTransformForReusedInstance(
            TimberbornPooledFireEffectKind.Steam));
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

    private static TimberbornGpuVisualFieldSample Sample(
        int cellIndex,
        float fire,
        float smoke,
        float ash,
        float steam = 0f,
        float smokeContamination = 0f,
        float ashContamination = 0f)
    {
        return new TimberbornGpuVisualFieldSample(
            CellIndex: cellIndex,
            Tick: 1,
            Fire: fire,
            Smoke: smoke,
            Ash: ash,
            Visibility: 1f,
            Steam: steam,
            AtmosphericSmoke: smoke,
            SmokeContamination: smokeContamination,
            AshContamination: ashContamination);
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

        public Dictionary<int, TimberbornPooledFireEffectState> ActiveEffects { get; } = [];

        public List<int> ReleasedSlotIds { get; } = [];

        public int ClearCount { get; private set; }

        public TimberbornPooledFireEffectPresenterState State { get; }

        public TimberbornPooledFireEffectPresentationResult UpdateEffect(TimberbornPooledFireEffectState state)
        {
            UpdatedEffects.Add(state);
            ActiveEffects[state.SlotId] = state;
            return _result;
        }

        public void ReleaseEffect(int slotId)
        {
            ReleasedSlotIds.Add(slotId);
            ActiveEffects.Remove(slotId);
        }

        public void Clear()
        {
            ClearCount++;
            ActiveEffects.Clear();
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
