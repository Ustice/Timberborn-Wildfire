namespace Wildfire.Core.Tests;

public sealed partial class TimberbornBeaverFieldBehaviorTests
{
    [Fact]
    public void UnsupportedFireDoesNotConsumeBudgetOrSuppressLaterSmoke()
    {
        var adapter = new RecordingWorkerSpeedAdapter();
        var actuator = new TimberbornWorkerSpeedBeaverFieldBehaviorActuator(adapter);
        var dispatcher = new TimberbornBeaverFieldBehaviorDispatcher(actuator, new RecordingFireLogSink(),
            new TimberbornBeaverFieldBehaviorOptions(MaxDecisionsPerDispatch: 1, SmokeCoughingThresholdSamples: 1));
        var fire = Classification("a-fire", burn: 1, maxFire: .9f);
        dispatcher.Dispatch(Snapshot([fire, Classification("z-smoke", respiratory: 1)]), 10);
        Assert.Single(adapter.Actions);
        Assert.Equal(1, dispatcher.Counters.UnsupportedDecisions);
        Assert.Equal(0, dispatcher.Counters.FireHeatDecisionsApplied);
        Assert.Equal(1, dispatcher.Counters.FireHeatExposedBeavers);
        Assert.Equal(1, dispatcher.Counters.FireHeatActiveFlameContacts);
        Assert.Equal(0, dispatcher.Counters.DecisionsSkippedBatch);
        Assert.Equal("z-smoke", Assert.Single(dispatcher.CaptureState().Entries).BeaverId);
        dispatcher.Dispatch(Snapshot([fire]), 11);
        Assert.Equal(2, dispatcher.Counters.UnsupportedDecisions);
        Assert.Equal(0, dispatcher.Counters.DecisionsSkippedCooldown);
    }

    [Fact]
    public void UnsupportedFirePreservesSavedSmokeCleanupAndSuccessfulCooldown()
    {
        var adapter = new RecordingWorkerSpeedAdapter();
        var actuator = new TimberbornWorkerSpeedBeaverFieldBehaviorActuator(adapter);
        var options = new TimberbornBeaverFieldBehaviorOptions(DecisionCooldownTicks: 1, SmokeCoughingThresholdSamples: 1);
        var source = new TimberbornBeaverFieldBehaviorDispatcher(actuator, new RecordingFireLogSink(), options);
        source.Dispatch(Snapshot([Classification("beaver", respiratory: 1)]), 10);
        var smoke = Assert.Single(source.CaptureState().Entries);
        source.Dispatch(Snapshot([Classification("beaver", burn: 1)]), 11);
        Assert.Equal(smoke, Assert.Single(source.CaptureState().Entries));
        var restored = new TimberbornBeaverFieldBehaviorDispatcher(actuator, new RecordingFireLogSink(), options);
        restored.RestoreState(source.CaptureState());
        restored.Dispatch(Snapshot([Classification("beaver")]), 12);
        Assert.Equal(new[] { "beaver" }, adapter.RecoveredBeaverIds);
        Assert.Equal(1, restored.Counters.SmokeCoughingSlowdownsRecovered);
        Assert.False(Assert.Single(restored.CaptureState().Entries).IsExposed);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(4u)]
    public void LaterActorsProgressBeyondSixtyFourAcrossCooldownAndSave(uint cooldown)
    {
        var actuator = new RecordingActuator(TimberbornBeaverFieldBehaviorActuatorStatus.Applied);
        var options = new TimberbornBeaverFieldBehaviorOptions(DecisionCooldownTicks: cooldown);
        var source = new TimberbornBeaverFieldBehaviorDispatcher(actuator, new RecordingFireLogSink(), options);
        var exposure = Snapshot(Enumerable.Range(0, 130).Select(i => Classification($"beaver-{i:D3}", respiratory: 1)).ToArray());
        source.Dispatch(exposure, 10);
        Assert.Equal(64, actuator.Decisions.Count);
        var restored = new TimberbornBeaverFieldBehaviorDispatcher(actuator, new RecordingFireLogSink(), options);
        restored.RestoreState(source.CaptureState());
        restored.Dispatch(exposure, 11);
        Assert.Equal(128, actuator.Decisions.Select(d => d.BeaverId).Distinct().Count());
        restored.Dispatch(exposure, 12);
        Assert.Equal(130, actuator.Decisions.Select(d => d.BeaverId).Distinct().Count());
        Assert.All(actuator.Decisions.GroupBy(d => d.Tick), batch => Assert.InRange(batch.Count(), 1, 64));
        if (cooldown == 4) Assert.True(restored.Counters.DecisionsSkippedCooldown > 64);
    }

    [Fact]
    public void FailedOrMutatingThrowingActuatorIsNeverDowngradedToUnsupported()
    {
        var failed = new RecordingActuator(TimberbornBeaverFieldBehaviorActuatorStatus.Failed);
        var dispatcher = new TimberbornBeaverFieldBehaviorDispatcher(failed, new RecordingFireLogSink());
        var batch = Snapshot([Classification("a-smoke", respiratory: 1), Classification("z-smoke", respiratory: 1)]);
        Assert.Throws<TimberbornBeaverFieldDeliveryException>(() => dispatcher.Dispatch(batch, 10));
        Assert.Single(failed.Decisions);
        Assert.Equal(1, dispatcher.Counters.FailedDecisions);
        Assert.Equal(0, dispatcher.Counters.UnsupportedDecisions);
        Assert.Empty(dispatcher.CaptureState().Entries);

        var mutating = new MutatingThrowingActuator();
        dispatcher = new TimberbornBeaverFieldBehaviorDispatcher(mutating, new RecordingFireLogSink());
        Assert.Same(mutating.Cause, Assert.Throws<InvalidOperationException>(() => dispatcher.Dispatch(batch, 10)));
        Assert.Equal(1, mutating.Mutations);
        Assert.Equal(0, dispatcher.Counters.UnsupportedDecisions);
        Assert.Equal(0, dispatcher.Counters.SmokeDecisionsApplied);
        Assert.Empty(dispatcher.CaptureState().Entries);
        // No retry is performed by this dispatch. Cross-dispatch uncertain mutation protection
        // belongs to the native adapter delivery classification tested in NativeSmokeDeliveryTests.
    }

    private sealed class MutatingThrowingActuator : ITimberbornBeaverFieldBehaviorActuator
    {
        public int Mutations { get; private set; }
        public InvalidOperationException Cause { get; } = new("callback after mutation");
        public TimberbornBeaverFieldBehaviorActuatorResult Apply(TimberbornBeaverFieldBehaviorDecision decision)
        { Mutations++; throw Cause; }
        public TimberbornBeaverFieldBehaviorActuatorResult Recover(TimberbornBeaverFieldBehaviorStateEntry entry, uint? tick) => throw new NotSupportedException();
        public void Clear() { }
    }
}
