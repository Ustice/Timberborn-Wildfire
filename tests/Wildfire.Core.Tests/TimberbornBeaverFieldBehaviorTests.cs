using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornBeaverFieldBehaviorTests
{
    [Fact]
    public void DispatcherRoutesExposureTelemetryToBoundedNoOpVariantDecisions()
    {
        RecordingActuator actuator = new(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(actuator, new RecordingFireLogSink());

        dispatcher.Dispatch(
            Snapshot([
                Classification("beaver-smoke", respiratory: 2),
                Classification("beaver-toxic", respiratory: 1, contaminatedSmoke: 1, toxic: 1),
                Classification("beaver-fire", burn: 1),
            ]),
            tick: 10);

        Assert.Equal(3, dispatcher.Counters.DecisionsEvaluated);
        Assert.Equal(1, dispatcher.Counters.SmokeDecisionsApplied);
        Assert.Equal(1, dispatcher.Counters.ToxicSmokeDecisionsApplied);
        Assert.Equal(1, dispatcher.Counters.FireHeatDecisionsApplied);
        Assert.Equal(3, dispatcher.Counters.NoOpDecisionsApplied);
        Assert.Equal([
            TimberbornBeaverFieldBehaviorVariant.Smoke,
            TimberbornBeaverFieldBehaviorVariant.ToxicSmoke,
            TimberbornBeaverFieldBehaviorVariant.FireHeat,
        ], actuator.Decisions.Select(static decision => decision.Variant).OrderBy(static variant => variant).ToArray());
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
    public void DispatcherAccumulatesNormalSmokeSamplesUntilCoughingNoOpState()
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
        Assert.Equal(TimberbornBeaverFieldBehaviorAction.CoughingSafeNoOp, actuator.Decisions.Last().Action);
        Assert.Equal(3, Assert.Single(dispatcher.CaptureState().Entries).ConsecutiveExposedSamples);
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
    public void DispatcherKeepsSmokeChokingAndDeathAsSkippedUnsafeCandidates()
    {
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(
            new RecordingActuator(TimberbornBeaverFieldBehaviorActuatorStatus.Applied),
            new RecordingFireLogSink(),
            new TimberbornBeaverFieldBehaviorOptions(
                DecisionCooldownTicks: 1,
                SmokeCoughingThresholdSamples: 1,
                SmokeChokingCandidateThresholdSamples: 2,
                SmokeDeathCandidateThresholdSamples: 3));

        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 10);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 11);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1", respiratory: 1)]), tick: 12);

        Assert.Equal(1, dispatcher.Counters.SmokeChokingCandidates);
        Assert.Equal(1, dispatcher.Counters.SmokeChokingSkippedUnsafeApi);
        Assert.Equal(1, dispatcher.Counters.SmokeDeathCandidates);
        Assert.Equal(1, dispatcher.Counters.SmokeDeathSkippedUnsafeApi);
        Assert.Equal(2, dispatcher.Counters.SkippedNoSafeApi);
        Assert.Equal(3, dispatcher.Counters.SmokeDecisionsApplied);
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
    public void DispatcherRecoversSampledBeaverWhenExposureClears()
    {
        RecordingActuator actuator = new(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(actuator, new RecordingFireLogSink());

        dispatcher.Dispatch(Snapshot([Classification("beaver-1", burn: 1)]), tick: 10);
        dispatcher.Dispatch(Snapshot([Classification("beaver-1")]), tick: 15);

        Assert.Equal(1, dispatcher.Counters.RecoveryActions);
        TimberbornBeaverFieldBehaviorStateEntry recoveredEntry = Assert.Single(actuator.RecoveredEntries);
        Assert.Equal("beaver-1", recoveredEntry.BeaverId);
    }

    [Fact]
    public void DispatcherCountsSafeUnavailableActuatorSkipsWithoutMutatingState()
    {
        TimberbornBeaverFieldBehaviorDispatcher dispatcher = new(
            new RecordingActuator(TimberbornBeaverFieldBehaviorActuatorStatus.SkippedNoSafeApi),
            new RecordingFireLogSink());

        dispatcher.Dispatch(Snapshot([Classification("beaver-1", burn: 1)]), tick: 10);

        Assert.Equal(1, dispatcher.Counters.DecisionsEvaluated);
        Assert.Equal(1, dispatcher.Counters.SkippedNoSafeApi);
        Assert.Equal(0, dispatcher.Counters.TrackedBeaverCount);
        Assert.Equal(0, dispatcher.Counters.FireHeatDecisionsApplied);
    }

    [Fact]
    public void DispatcherCapturesAndRestoresVersionedPersistenceState()
    {
        TimberbornBeaverFieldBehaviorDispatcher source = new(
            new RecordingActuator(TimberbornBeaverFieldBehaviorActuatorStatus.Applied),
            new RecordingFireLogSink());
        source.Dispatch(Snapshot([Classification("beaver-1", burn: 1)]), tick: 10);

        TimberbornBeaverFieldBehaviorSnapshot snapshot = source.CaptureState();
        TimberbornBeaverFieldBehaviorDispatcher restored = new(
            new RecordingActuator(TimberbornBeaverFieldBehaviorActuatorStatus.Applied),
            new RecordingFireLogSink());
        restored.RestoreState(snapshot);
        restored.Dispatch(Snapshot([Classification("beaver-1", burn: 1)]), tick: 12);

        TimberbornBeaverFieldBehaviorStateEntry entry = Assert.Single(snapshot.Entries);
        Assert.Equal("beaver-1", entry.BeaverId);
        Assert.Equal(TimberbornBeaverFieldBehaviorVariant.FireHeat, entry.LastVariant);
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
        int taintedAftermath = 0)
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
            TaintedAftermathCells: taintedAftermath);
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
