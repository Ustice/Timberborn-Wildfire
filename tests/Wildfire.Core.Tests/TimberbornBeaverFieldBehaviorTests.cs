namespace Wildfire.Core.Tests;

public sealed class TimberbornBeaverFieldBehaviorTests
{
    [Fact]
    public void DispatcherFailsLoudlyWhenFireHeatBeaverAvoidanceIsRequested()
    {
        RecordingActuator actuator = new(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(actuator, new RecordingFireLogSink());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => dispatcher.Dispatch(
            Snapshot([
                Classification("beaver-smoke", respiratory: 2),
                Classification("beaver-toxic", respiratory: 1, contaminatedSmoke: 1, toxic: 1),
                Classification("beaver-fire", burn: 1),
            ]),
            tick: 10));

        Assert.Contains("Fire heat beaver avoidance is not implemented", exception.Message);
    }

    [Fact]
    public void DispatcherFailsLoudlyWhenFireHeatExposureRequiresBeaverAvoidance()
    {
        RecordingActuator actuator = new(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(actuator, new RecordingFireLogSink());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            dispatcher.Dispatch(Snapshot([Classification("beaver-fire", burn: 2)]), tick: 10));

        TimberbornBeaverFieldBehaviorDecision decision = Assert.Single(actuator.Decisions);
        Assert.Contains("Fire heat beaver avoidance is not implemented", exception.Message);
        Assert.Equal(TimberbornBeaverFieldBehaviorVariant.FireHeat, decision.Variant);
        Assert.Equal(TimberbornBeaverFieldBehaviorAction.FireHeatExposureAttempt, decision.Action);
        Assert.Equal(1, dispatcher.Counters.FireHeatExposedBeavers);
    }

    [Fact]
    public void DispatcherTracksActiveFlameContactBeforeFailingFireHeatAttempt()
    {
        RecordingActuator actuator = new(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(actuator, new RecordingFireLogSink());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            dispatcher.Dispatch(Snapshot([Classification("beaver-fire", burn: 1, maxFire: 0.9f)]), tick: 10));

        TimberbornBeaverFieldBehaviorDecision decision = Assert.Single(actuator.Decisions);
        Assert.Equal(TimberbornBeaverFieldBehaviorAction.FireHeatExposureAttempt, decision.Action);
        Assert.Contains("Fire heat beaver avoidance is not implemented", exception.Message);
        Assert.Equal(1, dispatcher.Counters.FireHeatActiveFlameContacts);
        Assert.Equal(0.9f, decision.MaxFire);
    }

    [Fact]
    public void DispatcherFailsLoudlyWhenFireHeatExposureHasNoImplementedActuator()
    {
        RecordingActuator actuator = new(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(
            actuator,
            new RecordingFireLogSink(),
            new TimberbornBeaverFieldBehaviorOptions(
                DecisionCooldownTicks: 1));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            dispatcher.Dispatch(Snapshot([Classification("beaver-fire", burn: 1)]), tick: 10));

        Assert.Contains("Fire heat beaver avoidance is not implemented", exception.Message);
    }

    [Fact]
    public void DispatcherDoesNotPrimeFireHeatInjuryFromSmokeOrSteamExposure()
    {
        RecordingActuator actuator = new(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(
            actuator,
            new RecordingFireLogSink(),
            new TimberbornBeaverFieldBehaviorOptions(
                DecisionCooldownTicks: 1,
                SmokeCoughingThresholdSamples: 8));

        new[]
        {
            Classification("beaver-mixed", respiratory: 1),
            Classification("beaver-mixed", steam: 1),
            Classification("beaver-mixed", respiratory: 1),
        }.Select((classification, index) => (classification, index))
            .ToList()
            .ForEach(sample => dispatcher.Dispatch(Snapshot([sample.classification]), tick: (uint)(10 + sample.index)));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            dispatcher.Dispatch(Snapshot([Classification("beaver-mixed", burn: 1)]), tick: 13));

        TimberbornBeaverFieldBehaviorDecision decision = actuator.Decisions.Last();
        TimberbornBeaverFieldBehaviorStateEntry entry = Assert.Single(dispatcher.CaptureState().Entries);
        Assert.Contains("Fire heat beaver avoidance is not implemented", exception.Message);
        Assert.Equal(TimberbornBeaverFieldBehaviorVariant.FireHeat, decision.Variant);
        Assert.Equal(TimberbornBeaverFieldBehaviorAction.FireHeatExposureAttempt, decision.Action);
        Assert.Equal(1, entry.ConsecutiveFireHeatExposedSamples);
    }

    [Fact]
    public void DispatcherDecaysFireHeatStateAfterExposureClears()
    {
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(
            new RecordingActuator(TimberbornBeaverFieldBehaviorActuatorStatus.Applied),
            new RecordingFireLogSink(),
            new TimberbornBeaverFieldBehaviorOptions(
                DecisionCooldownTicks: 1,
                FireHeatRecoveryDecaySamples: 2));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            dispatcher.Dispatch(Snapshot([Classification("beaver-fire", burn: 1)]), tick: 10));

        Assert.Contains("Fire heat beaver avoidance is not implemented", exception.Message);
    }

    [Fact]
    public void DispatcherTreatsCleanSteamAsNonToxicRespiratoryBehavior()
    {
        RecordingActuator actuator = new(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(actuator, new RecordingFireLogSink());

        dispatcher.Dispatch(Snapshot([Classification("beaver-steam", steam: 2)]), tick: 10);

        TimberbornBeaverFieldBehaviorDecision decision = Assert.Single(actuator.Decisions);
        Assert.Equal(TimberbornBeaverFieldBehaviorVariant.Smoke, decision.Variant);
        Assert.Equal(2, decision.SteamCells);
        Assert.Equal(0, dispatcher.Counters.ToxicSmokeDecisionsApplied);
        Assert.Equal(1, dispatcher.Counters.SmokeDecisionsApplied);
    }

    [Fact]
    public void DispatcherSkipsRepeatedDecisionsInsideCooldown()
    {
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(
            new RecordingActuator(TimberbornBeaverFieldBehaviorActuatorStatus.Applied),
            new RecordingFireLogSink(),
            new TimberbornBeaverFieldBehaviorOptions(DecisionCooldownTicks: 4));

        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 10);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 12);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 14);

        Assert.Equal(3, dispatcher.Counters.DecisionsEvaluated);
        Assert.Equal(1, dispatcher.Counters.DecisionsSkippedCooldown);
        Assert.Equal(2, dispatcher.Counters.SmokeDecisionsApplied);
    }

    [Fact]
    public void DispatcherAccumulatesNormalSmokeSamplesUntilCoughingDebuffState()
    {
        RecordingActuator actuator = new(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(
            actuator,
            new RecordingFireLogSink(),
            new TimberbornBeaverFieldBehaviorOptions(
                DecisionCooldownTicks: 1,
                SmokeCoughingThresholdSamples: 3));

        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 10);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 11);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 12);

        Assert.Equal(3, dispatcher.Counters.SmokeExposedSamples);
        Assert.Equal(3, dispatcher.Counters.SmokeExposureAccumulatedSamples);
        Assert.Equal(1, dispatcher.Counters.SmokeCoughingEntered);
        Assert.Equal(TimberbornBeaverFieldBehaviorAction.CoughingWorkSlowdown, actuator.Decisions.Last().Action);
        Assert.Equal(3, Assert.Single(dispatcher.CaptureState().Entries).ConsecutiveExposedSamples);
    }

    [Fact]
    public void DispatcherAccumulatesToxicSmokeSamplesUntilCoughingDebuffState()
    {
        RecordingActuator actuator = new(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(
            actuator,
            new RecordingFireLogSink(),
            new TimberbornBeaverFieldBehaviorOptions(
                DecisionCooldownTicks: 1,
                SmokeCoughingThresholdSamples: 3));

        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1, contaminatedSmoke: 1, toxic: 1)]), tick: 10);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1, contaminatedSmoke: 1, toxic: 1)]), tick: 11);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1, contaminatedSmoke: 1, toxic: 1)]), tick: 12);

        Assert.Equal(3, dispatcher.Counters.SmokeExposedSamples);
        Assert.Equal(6, dispatcher.Counters.SmokeExposureAccumulatedSamples);
        Assert.Equal(3, dispatcher.Counters.ToxicSmokeDecisionsApplied);
        Assert.Equal(1, dispatcher.Counters.SmokeCoughingEntered);
        Assert.Equal(3, dispatcher.Counters.ToxicSmokeExposedBeavers);
        Assert.Equal(6, dispatcher.Counters.ToxicSmokeExposureAccumulatedSamples);
        Assert.Equal(TimberbornBeaverFieldBehaviorVariant.ToxicSmoke, actuator.Decisions.Last().Variant);
        Assert.Equal(TimberbornBeaverFieldBehaviorAction.CoughingWorkSlowdown, actuator.Decisions.Last().Action);
        Assert.Equal(6, Assert.Single(dispatcher.CaptureState().Entries).ConsecutiveExposedSamples);
    }

    [Fact]
    public void DispatcherAdvancesToxicSmokeAcrossRespiratoryThresholdsFasterThanNormalSmoke()
    {
        RecordingActuator normalActuator = new(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        RecordingActuator toxicActuator = new(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        TimberbornBeaverFieldBehaviorOptions options = new(
            DecisionCooldownTicks: 1,
            SmokeCoughingThresholdSamples: 3,
            SmokeChokingThresholdSamples: 4,
            ToxicSmokeExposureSampleWeight: 2);
        TimberbornBeaverFieldBehaviorDispatcher normalDispatcher = new(
            normalActuator,
            new RecordingFireLogSink(),
            options);
        TimberbornBeaverFieldBehaviorDispatcher toxicDispatcher = new(
            toxicActuator,
            new RecordingFireLogSink(),
            options);

        new[]
        {
            10u,
            11u,
        }.ToList().ForEach(tick =>
        {
            normalDispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick);
            toxicDispatcher.Dispatch(
                Snapshot([Classification("beaver-1", respiratory: 1, contaminatedSmoke: 1, toxic: 1)]),
                tick);
        });

        Assert.Equal(2, normalDispatcher.Counters.SmokeExposureAccumulatedSamples);
        Assert.Equal(0, normalDispatcher.Counters.SmokeCoughingEntered);
        Assert.Equal(4, toxicDispatcher.Counters.SmokeExposureAccumulatedSamples);
        Assert.Equal(1, toxicDispatcher.Counters.SmokeCoughingEntered);
        Assert.Equal(TimberbornBeaverFieldBehaviorAction.ChokingWorkSlowdown, toxicActuator.Decisions.Last().Action);
    }

    [Fact]
    public void DispatcherTracksToxicSmokeExposureWithoutFutureContaminationCounters()
    {
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(
            new RecordingActuator(TimberbornBeaverFieldBehaviorActuatorStatus.Applied),
            new RecordingFireLogSink(),
            new TimberbornBeaverFieldBehaviorOptions(
                DecisionCooldownTicks: 1,
                SmokeCoughingThresholdSamples: 1,
                SmokeChokingThresholdSamples: 2));

        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1, contaminatedSmoke: 1)]), tick: 10);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1, contaminatedSmoke: 1)]), tick: 11);

        Assert.Equal(2, dispatcher.Counters.ToxicSmokeExposedBeavers);
        Assert.Equal(4, dispatcher.Counters.ToxicSmokeExposureAccumulatedSamples);
    }

    [Fact]
    public void DispatcherAccumulatesSmokeCoughingEvenWhenFireHeatIsPresent()
    {
        RecordingActuator actuator = new(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(
            actuator,
            new RecordingFireLogSink(),
            new TimberbornBeaverFieldBehaviorOptions(
                DecisionCooldownTicks: 1,
                SmokeCoughingThresholdSamples: 3));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1, burn: 1, toxic: 1)]), tick: 10));

        Assert.Contains("Fire heat beaver avoidance is not implemented", exception.Message);
        Assert.Equal(TimberbornBeaverFieldBehaviorVariant.FireHeat, actuator.Decisions.Last().Variant);
    }

    [Fact]
    public void DispatcherDecaysSmokeCoughingStateAfterExposureClears()
    {
        RecordingActuator actuator = new(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(
            actuator,
            new RecordingFireLogSink(),
            new TimberbornBeaverFieldBehaviorOptions(
                DecisionCooldownTicks: 1,
                SmokeCoughingThresholdSamples: 2,
                SmokeRecoveryDecaySamples: 1));

        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 10);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 11);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1")]), tick: 12);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1")]), tick: 13);

        Assert.Equal(2, dispatcher.Counters.RecoveryActions);
        Assert.Equal(2, dispatcher.Counters.SmokeRecoveryDecays);
        Assert.Equal(1, dispatcher.Counters.SmokeCoughingRecovered);
        TimberbornBeaverFieldBehaviorStateEntry entry = Assert.Single(dispatcher.CaptureState().Entries);
        Assert.False(entry.IsExposed);
        Assert.Equal(0, entry.ConsecutiveExposedSamples);
    }

    [Fact]
    public void DispatcherAppliesChokingDebuff()
    {
        RecordingActuator actuator = new(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(
            actuator,
            new RecordingFireLogSink(),
            new TimberbornBeaverFieldBehaviorOptions(
                DecisionCooldownTicks: 1,
                SmokeCoughingThresholdSamples: 1,
                SmokeChokingThresholdSamples: 2));

        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 10);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 11);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 12);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 13);

        Assert.Equal(3, dispatcher.Counters.SmokeChokingSlowdownsApplied);
        Assert.Equal(4, dispatcher.Counters.SmokeDecisionsApplied);
        Assert.Equal(TimberbornBeaverFieldBehaviorAction.ChokingWorkSlowdown, actuator.Decisions.Last().Action);
    }

    [Fact]
    public void DispatcherRecoversChokingSlowdownAfterExposureClears()
    {
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(
            new RecordingActuator(TimberbornBeaverFieldBehaviorActuatorStatus.Applied),
            new RecordingFireLogSink(),
            new TimberbornBeaverFieldBehaviorOptions(
                DecisionCooldownTicks: 1,
                SmokeCoughingThresholdSamples: 1,
                SmokeChokingThresholdSamples: 2,
                SmokeRecoveryDecaySamples: 2));

        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 10);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 11);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 12);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1")]), tick: 13);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1")]), tick: 14);

        Assert.Equal(2, dispatcher.Counters.SmokeChokingSlowdownsRecovered);
        Assert.Equal(2, dispatcher.Counters.SmokeRecoveryDecays);
        Assert.Equal(1, dispatcher.Counters.SmokeCoughingRecovered);
        TimberbornBeaverFieldBehaviorStateEntry entry = Assert.Single(dispatcher.CaptureState().Entries);
        Assert.False(entry.IsExposed);
        Assert.Equal(0, entry.ConsecutiveExposedSamples);
    }

    [Fact]
    public void DispatcherReportsBatchSkipsForBoundedSmokeProcessing()
    {
        RecordingActuator actuator = new(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(
            actuator,
            new RecordingFireLogSink(),
            new TimberbornBeaverFieldBehaviorOptions(
                DecisionCooldownTicks: 1,
                MaxDecisionsPerDispatch: 1));

        dispatcher.Dispatch(
            Snapshot([
                Classification("beaver-1", respiratory: 1),
                Classification("beaver-2", respiratory: 1),
            ]),
            tick: 10);

        Assert.Equal(1, dispatcher.Counters.DecisionsSkippedBatch);
        Assert.Equal(1, dispatcher.Counters.SmokeExposedSamples);
        Assert.Single(actuator.Decisions);
    }

    [Fact]
    public void WorkerSpeedActuatorAppliesAndRecoversSmokeDebuffs()
    {
        RecordingWorkerSpeedAdapter adapter = new();
        TimberbornWorkerSpeedBeaverFieldBehaviorActuator actuator = new(adapter);

        TimberbornBeaverFieldBehaviorActuatorResult coughing = actuator.Apply(Decision(
            TimberbornBeaverFieldBehaviorAction.CoughingWorkSlowdown,
            tick: 10));
        TimberbornBeaverFieldBehaviorActuatorResult choking = actuator.Apply(Decision(
            TimberbornBeaverFieldBehaviorAction.ChokingWorkSlowdown,
            tick: 11));
        TimberbornBeaverFieldBehaviorActuatorResult recovered = actuator.Recover(
            new TimberbornBeaverFieldBehaviorStateEntry(
                TimberbornBeaverFieldBehaviorStateEntry.CurrentPersistenceVersion,
                "beaver-1",
                TimberbornBeaverFieldBehaviorVariant.Smoke,
                TimberbornBeaverFieldBehaviorAction.ChokingWorkSlowdown,
                LastDecisionTick: 11,
                ConsecutiveExposedSamples: 8,
                ConsecutiveFireHeatExposedSamples: 0,
                IsExposed: true),
            tick: 12);

        Assert.Equal(TimberbornBeaverFieldBehaviorActuatorStatus.Applied, coughing.Status);
        Assert.Equal(TimberbornBeaverFieldBehaviorActuatorStatus.Applied, choking.Status);
        Assert.Equal(TimberbornBeaverFieldBehaviorActuatorStatus.Applied, recovered.Status);
        Assert.Equal([
            TimberbornBeaverFieldBehaviorAction.CoughingWorkSlowdown,
            TimberbornBeaverFieldBehaviorAction.ChokingWorkSlowdown,
        ], adapter.Actions);
        Assert.Equal([
            TimberbornWorkerSpeedBeaverFieldBehaviorActuator.CoughingWorkingSpeedMultiplier,
            TimberbornWorkerSpeedBeaverFieldBehaviorActuator.ChokingWorkingSpeedMultiplier,
        ], adapter.Multipliers);
        Assert.Equal(["beaver-1"], adapter.RecoveredBeaverIds);
    }

    [Fact]
    public void DispatcherRecoversSampledBeaverWhenExposureClears()
    {
        RecordingActuator actuator = new(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(actuator, new RecordingFireLogSink());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            dispatcher.Dispatch(Snapshot([Classification("beaver-1", burn: 1)]), tick: 10));

        Assert.Contains("Fire heat beaver avoidance is not implemented", exception.Message);
        Assert.Empty(actuator.RecoveredEntries);
    }

    [Fact]
    public void DispatcherFailsLoudlyWhenActuatorFails()
    {
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(
            new RecordingActuator(TimberbornBeaverFieldBehaviorActuatorStatus.Failed),
            new RecordingFireLogSink());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 10));

        Assert.Contains("Beaver field behavior actuator failed", exception.Message);
        Assert.Equal(1, dispatcher.Counters.DecisionsEvaluated);
        Assert.Equal(0, dispatcher.Counters.TrackedBeaverCount);
    }

    [Fact]
    public void DispatcherCapturesAndRestoresVersionedPersistenceState()
    {
        TimberbornBeaverFieldBehaviorDispatcher source = new(
            new RecordingActuator(TimberbornBeaverFieldBehaviorActuatorStatus.Applied),
            new RecordingFireLogSink(),
            new TimberbornBeaverFieldBehaviorOptions(
                DecisionCooldownTicks: 1,
                SmokeCoughingThresholdSamples: 1,
                SmokeChokingThresholdSamples: 2));
        source.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 10);
        source.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 11);
        source.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 12);

        TimberbornBeaverFieldBehaviorSnapshot snapshot = source.CaptureState();
        TimberbornBeaverFieldBehaviorDispatcher restored = new(
            new RecordingActuator(TimberbornBeaverFieldBehaviorActuatorStatus.Applied),
            new RecordingFireLogSink(),
            new TimberbornBeaverFieldBehaviorOptions(
                DecisionCooldownTicks: 3,
                SmokeCoughingThresholdSamples: 1,
                SmokeChokingThresholdSamples: 2));
        restored.RestoreState(snapshot);
        restored.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 13);

        TimberbornBeaverFieldBehaviorStateEntry entry = Assert.Single(snapshot.Entries);
        Assert.Equal("beaver-1", entry.BeaverId);
        Assert.Equal(TimberbornBeaverFieldBehaviorVariant.Smoke, entry.LastVariant);
        Assert.Equal(2, source.Counters.SmokeChokingSlowdownsApplied);
        Assert.Equal(1, source.Counters.PersistenceSaveCount);
        Assert.Equal(1, restored.Counters.PersistenceLoadCount);
        Assert.Equal(1, restored.Counters.DecisionsSkippedCooldown);
    }

    private static TimberbornBeaverFieldExposureSnapshot Snapshot(
        IReadOnlyList<TimberbornBeaverFieldExposureClassification> classifications)
    {
        return TimberbornBeaverFieldExposureSnapshot.FromClassifications(
            sampledBeavers: classifications.Count,
            skippedNoPositionApi: 0,
            skippedBoundedSampling: 0,
            classifications);
    }

    private static TimberbornBeaverFieldExposureClassification Classification(
        string beaverId,
        int respiratory = 0,
        int burn = 0,
        int contaminatedSmoke = 0,
        int toxic = 0,
        int steam = 0,
        int taintedAftermath = 0,
        float maxFire = 0f)
    {
        return new TimberbornBeaverFieldExposureClassification(
            beaverId,
            X: 1,
            Y: 2,
            Z: 0,
            CandidateCellCount: 9,
            RespiratoryExposureCells: respiratory,
            BurnExposureCells: burn,
            ContaminatedSmokeCells: contaminatedSmoke,
            ToxicExposureCells: toxic,
            SteamCells: steam,
            TaintedAftermathCells: taintedAftermath,
            MaxFire: maxFire);
    }

    private static TimberbornBeaverFieldBehaviorDecision Decision(
        TimberbornBeaverFieldBehaviorAction action,
        uint tick)
    {
        return new TimberbornBeaverFieldBehaviorDecision(
            "beaver-1",
            X: 1,
            Y: 2,
            Z: 0,
            TimberbornBeaverFieldBehaviorVariant.Smoke,
            action,
            RespiratoryExposureCells: 1,
            BurnExposureCells: 0,
            ContaminatedSmokeCells: 0,
            ToxicExposureCells: 0,
            SteamCells: 0,
            TaintedAftermathCells: 0,
            MaxFire: 0f,
            Tick: tick);
    }

    private sealed class RecordingActuator(TimberbornBeaverFieldBehaviorActuatorStatus status) :
        ITimberbornBeaverFieldBehaviorActuator
    {
        public List<TimberbornBeaverFieldBehaviorDecision> Decisions { get; } = [];

        public List<TimberbornBeaverFieldBehaviorStateEntry> RecoveredEntries { get; } = [];

        public TimberbornBeaverFieldBehaviorActuatorResult Apply(TimberbornBeaverFieldBehaviorDecision decision)
        {
            Decisions.Add(decision);
            return new TimberbornBeaverFieldBehaviorActuatorResult(status, status.ToString());
        }

        public TimberbornBeaverFieldBehaviorActuatorResult Recover(
            TimberbornBeaverFieldBehaviorStateEntry entry,
            uint? tick)
        {
            RecoveredEntries.Add(entry);
            return new TimberbornBeaverFieldBehaviorActuatorResult(status, status.ToString());
        }

        public void Clear()
        {
            Decisions.Clear();
            RecoveredEntries.Clear();
        }
    }

    private sealed class RecordingWorkerSpeedAdapter : ITimberbornBeaverWorkerSpeedAdapter
    {
        public List<TimberbornBeaverFieldBehaviorAction> Actions { get; } = [];

        public List<float> Multipliers { get; } = [];

        public List<string> RecoveredBeaverIds { get; } = [];

        public TimberbornBeaverWorkerSpeedResult ApplySmokeReaction(
            string beaverId,
            TimberbornBeaverFieldBehaviorAction action,
            float multiplier)
        {
            Actions.Add(action);
            Multipliers.Add(multiplier);
            return TimberbornBeaverWorkerSpeedResult.Applied;
        }

        public TimberbornBeaverWorkerSpeedResult RecoverSmokeReaction(string beaverId)
        {
            RecoveredBeaverIds.Add(beaverId);
            return TimberbornBeaverWorkerSpeedResult.Applied;
        }

        public void Clear()
        {
            Actions.Clear();
            Multipliers.Clear();
            RecoveredBeaverIds.Clear();
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
