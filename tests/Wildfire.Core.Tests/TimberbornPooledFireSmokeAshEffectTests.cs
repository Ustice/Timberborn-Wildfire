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
    public void ProceduralFireParticlesEmitFromBottomFootprintWithCircularBillboards()
    {
        string source = ReadTimberbornPooledFireSmokeAshEffectsSource();

        Assert.Contains("shape.shapeType = ParticleSystemShapeType.Box;", source);
        Assert.Contains("new Vector3(0.92f, 0.04f, 0.92f)", source);
        Assert.Contains("main.startSpeed = 0f;", source);
        Assert.Contains("velocity.x = HorizontalDriftVelocity(state, salt: 43, axis: 0);", source);
        Assert.Contains("velocity.z = HorizontalDriftVelocity(state, salt: 59, axis: 1);", source);
        Assert.Contains("new ParticleSystem.MinMaxCurve(drift - 0.5f, drift + 0.5f)", source);
        Assert.Contains("main.simulationSpace = ParticleSystemSimulationSpace.World;", source);
        Assert.Contains("particleSystem.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmitting);", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Smoke => new ParticleSystem.MinMaxCurve(0.24f, 0.66f)", source);
        Assert.Contains("private static TimberbornPooledFireEffectState SmoothEffectState(", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Smoke => 0.32f", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Smoke => Lerp(1f, 4.5f, intensity)", source);
        Assert.Contains("TimberbornPooledFireEffectKind.ToxicSmoke => Lerp(0.75f, 3.5f, intensity)", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Smoke => new ParticleSystem.MinMaxCurve(4.8f)", source);
        Assert.Contains("TimberbornPooledFireEffectKind.ToxicSmoke => new ParticleSystem.MinMaxCurve(5.4f)", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Smoke => 6.5f", source);
        Assert.Contains("TimberbornPooledFireEffectKind.ToxicSmoke => 4f", source);
        Assert.Contains("material.mainTexture = CreateCircularParticleTexture(kind);", source);
        Assert.Contains("material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 500;", source);
        Assert.Contains("material.SetInt(\"_ZWrite\", 0);", source);
        Assert.Contains("float edgeSoftness = kind == TimberbornPooledFireEffectKind.Fire ? 0.18f : 0.32f;", source);
        Assert.Contains("new Color(1f, 1f, 1f, alpha)", source);
        Assert.Contains("EmissionRate(state.Kind, visibleIntensity)", source);
        Assert.Contains("state.Kind == TimberbornPooledFireEffectKind.Fire && !created", source);
        Assert.Contains("ConfigureParticleEmission(instance, state);", source);
        Assert.Contains("private static void ConfigureParticleEmission(GameObject instance, TimberbornPooledFireEffectState state)", source);
        Assert.Contains("StartLifetime(state.Kind, visibleIntensity)", source);
        Assert.Contains("StartSizeMin(state.Kind, visibleIntensity)", source);
        Assert.Contains("StartSizeMax(state.Kind, visibleIntensity)", source);
        Assert.Contains("private static float OverLifetimeIntensity(TimberbornPooledFireEffectKind kind, float intensity)", source);
        Assert.Contains("return kind == TimberbornPooledFireEffectKind.Fire ? 1f : intensity;", source);
        Assert.Contains("UpwardVelocityMin(state.Kind, OverLifetimeIntensity(state.Kind, visibleIntensity))", source);
        Assert.Contains("LifetimeGradient(\n                state.Kind,\n                OverLifetimeIntensity(state.Kind, visibleIntensity))", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Fire => Lerp(1.85f, 2.85f, intensity)", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Fire => Lerp(3.75f, 5.35f, intensity)", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Fire => new ParticleSystem.MinMaxCurve(-0.55f, 0.55f)", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Fire => new ParticleSystem.MinMaxCurve(4.4f, 9.2f)", source);
        Assert.Contains("new ParticleSystem.MinMaxCurve(0.3f, 0.6f)", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Steam => Lerp(3f, 13.5f, intensity)", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Steam => new ParticleSystem.MinMaxCurve(4.8f)", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Steam => 19.5f", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Steam => Lerp(0.45f, 0.85f, intensity)", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Steam => Lerp(0.9f, 1.55f, intensity)", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Steam => Lerp(0.35f, 0.65f, intensity)", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Steam => Lerp(0.85f, 1.35f, intensity)", source);
        Assert.Contains("force.enabled = state.Kind == TimberbornPooledFireEffectKind.Fire;", source);
        Assert.Contains("forceOverLifetime.enabled = kind == TimberbornPooledFireEffectKind.Fire;", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Steam => 0.42f", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Steam => 0.45f", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Steam => new ParticleSystem.MinMaxCurve(0.24f, 0.66f)", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Steam => new ParticleSystem.MinMaxCurve(0.55f, 1.1f)", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Steam => new ParticleSystem.MinMaxCurve(0.06f, 0.36f)", source);
        Assert.DoesNotContain("new Vector3(0.08f, 0.025f, 0.08f)", source);
        Assert.Contains("AnimationCurve.EaseInOut(0f, 1.25f, 1f, 0.18f)", source);
        Assert.Contains("AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.85f)", source);
        Assert.DoesNotContain("new Keyframe(0.72f, 0.55f)", source);
        Assert.DoesNotContain("new Keyframe(1f, 1.95f)", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Smoke => new Color(0.88f, 0.88f, 0.82f, Lerp(0.5f, 0.78f, intensity))", source);
        Assert.Contains("TimberbornPooledFireEffectKind.ToxicSmoke => new Color(0.42f, 0.03f, 0.1f, Lerp(0.36f, 0.66f, intensity))", source);
        Assert.Contains("TimberbornPooledFireEffectKind.ToxicSmoke => new Color(0.26f, 0.01f, 0.07f, Lerp(0.28f, 0.52f, intensity))", source);
        Assert.Contains("TimberbornPooledFireEffectKind.ToxicSmoke => new Color(0.12f, 0.0f, 0.04f, 0f)", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Steam => new Color(0.96f, 0.96f, 0.92f, Lerp(0.34f, 0.62f, intensity))", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Steam => new Color(0.86f, 0.86f, 0.82f, Lerp(0.24f, 0.5f, intensity))", source);
        Assert.Contains("TimberbornPooledFireEffectKind.Steam => new Color(0.82f, 0.82f, 0.78f, 0f)", source);
        Assert.DoesNotContain("TimberbornPooledFireEffectKind.Ash,\n            TimberbornPooledFireEffectKind.ToxicAsh", source);
        Assert.Contains("_ => new Color(0.46f, 0.46f, 0.42f, Lerp(0.4f, 0.7f, intensity))", source);
        Assert.DoesNotContain("LifetimeGradient(state.Kind, visibleIntensity)", source);
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
            OldWater: 2,
            Water: 2,
            IsBurning: false));
        sink.CompleteVisualEffectDispatch(21);

        TimberbornPooledFireEffectState state = Assert.Single(presenter.UpdatedEffects);
        Assert.Equal(TimberbornPooledFireEffectKind.Steam, state.Kind);
        Assert.Equal(0f, state.MoistureDrop, precision: 4);
        Assert.Equal(5f / 7f, state.Steam, precision: 4);
        Assert.Equal(5f / 7f, state.Intensity, precision: 4);
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
    public void TimberbornFireRuntimeHasSinglePublicBinditoConstructor()
    {
        string source = ReadTimberbornFireRuntimeSource();

        Assert.Equal(1, CountOccurrences(source, " TimberbornFireRuntime("));
        Assert.Contains("public TimberbornFireRuntime(\n        ITimberbornGpuVisualFieldSurface visualFieldSurface,\n        EntityRegistry entityRegistry,\n        EntitySelectionService entitySelectionService,\n        QuickNotificationService quickNotificationService,\n        TimberbornPlayerFireAlertCameraFocus playerFireAlertCameraFocus,\n        WildfireReleaseSettings releaseSettings,\n        TimberbornFireSimParameterPresetState fireSimParameterPresetState,\n        ITimberbornWindProvider windProvider)", source);
        Assert.DoesNotContain("public TimberbornFireRuntime()\n", source);
        Assert.DoesNotContain("internal TimberbornFireRuntime(", source);
        Assert.DoesNotContain("NullTimberbornGpuFieldRendererPresenter.Instance", source);
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

    private static string ReadTimberbornPooledFireSmokeAshEffectsSource()
    {
        string path = SelfAndParents(new DirectoryInfo(AppContext.BaseDirectory))
            .Select(directory => Path.Combine(
                directory.FullName,
                "src",
                "Wildfire.Timberborn",
                "TimberbornPooledFireSmokeAshEffects.cs"))
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
