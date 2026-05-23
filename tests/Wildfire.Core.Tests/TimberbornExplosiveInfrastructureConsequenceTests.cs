using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class TimberbornExplosiveInfrastructureConsequenceTests
{
    [Fact]
    public void SinkSkipsExposureWhenSettingDisabled()
    {
        RecordingExplosiveTargetApi targetApi = new(Target(depth: 1));
        RecordingHeatPulseSink heatPulseSink = new();
        TimberbornExplosiveInfrastructureConsequenceSink sink = new(
            () => Settings(enabled: false),
            targetApi,
            heatPulseSink);

        TimberbornExplosiveInfrastructureConsequenceSummary summary = sink.ApplyConsequences(
            1,
            [Decision(12, heat: 3)]);

        Assert.Equal(1, summary.ConsideredDeltaCount);
        Assert.Equal(1, summary.SkippedSettingDisabledCount);
        Assert.Equal(0, summary.TriggeredTargetCount);
        Assert.Empty(targetApi.ResolvedConsequences);
        Assert.Empty(heatPulseSink.Pulses);
    }

    [Fact]
    public void SinkTracksSustainedThresholdBeforeTriggeringPulse()
    {
        RecordingExplosiveTargetApi targetApi = new(Target(depth: 2));
        RecordingHeatPulseSink heatPulseSink = new();
        TimberbornExplosiveInfrastructureConsequenceSink sink = new(
            () => Settings(thresholdTicks: 2),
            targetApi,
            heatPulseSink);

        TimberbornExplosiveInfrastructureConsequenceSummary firstSummary = sink.ApplyConsequences(
            7,
            [Decision(12, heat: 3)]);
        TimberbornExplosiveInfrastructureConsequenceSummary secondSummary = sink.ApplyConsequences(
            8,
            [Decision(12, heat: 4)]);

        Assert.Equal(0, firstSummary.TriggeredTargetCount);
        Assert.Equal(1, secondSummary.ArmedTargetCount);
        Assert.Equal(1, secondSummary.TriggeredTargetCount);
        Assert.Equal(5, secondSummary.HeatPulseCellCount);
        Assert.Equal(2, secondSummary.LastTriggeredDepth);
        Assert.Single(heatPulseSink.Pulses);
    }

    [Fact]
    public void SinkTracksSustainedThresholdAcrossVolatileNativeStableIds()
    {
        RecordingExplosiveTargetApi targetApi = new(
            Target(depth: 2, canTriggerNative: true),
            stableIdByResolve: index => $"dynamite:volatile-{index}");
        RecordingHeatPulseSink heatPulseSink = new();
        TimberbornExplosiveInfrastructureConsequenceSink sink = new(
            () => Settings(thresholdTicks: 2, nativeEnabled: false),
            targetApi,
            heatPulseSink);

        TimberbornExplosiveInfrastructureConsequenceSummary firstSummary = sink.ApplyConsequences(
            7,
            [Decision(12, heat: 3)]);
        TimberbornExplosiveInfrastructureConsequenceSummary secondSummary = sink.ApplyConsequences(
            8,
            [Decision(12, heat: 4)]);

        Assert.Equal(1, firstSummary.MatchedTargetCellCount);
        Assert.Equal(0, firstSummary.TriggeredTargetCount);
        Assert.Equal(1, secondSummary.ArmedTargetCount);
        Assert.Equal(1, secondSummary.TriggeredTargetCount);
        Assert.Equal(5, secondSummary.HeatPulseCellCount);
        Assert.Equal(1, secondSummary.SkippedNoSafeApiCount);
        Assert.Equal(0, targetApi.NativeTriggerCalls);
        Assert.Single(heatPulseSink.Pulses);
    }

    [Fact]
    public void SinkCountsVolatileDuplicateExposureOnlyOncePerDispatch()
    {
        RecordingExplosiveTargetApi targetApi = new(
            Target(depth: 2, canTriggerNative: true),
            stableIdByResolve: index => $"dynamite:volatile-{index}");
        RecordingHeatPulseSink heatPulseSink = new();
        TimberbornExplosiveInfrastructureConsequenceSink sink = new(
            () => Settings(thresholdTicks: 2, nativeEnabled: false),
            targetApi,
            heatPulseSink);

        TimberbornExplosiveInfrastructureConsequenceSummary firstSummary = sink.ApplyConsequences(
            7,
            [
                Decision(12, heat: 3),
                Decision(12, heat: 4),
            ]);
        TimberbornExplosiveInfrastructureConsequenceSummary secondSummary = sink.ApplyConsequences(
            8,
            [Decision(12, heat: 4)]);

        Assert.Equal(2, firstSummary.MatchedTargetCellCount);
        Assert.Equal(0, firstSummary.TriggeredTargetCount);
        Assert.Equal(1, secondSummary.TriggeredTargetCount);
        Assert.Equal(1, secondSummary.SkippedNoSafeApiCount);
        Assert.Equal(0, targetApi.NativeTriggerCalls);
        Assert.Single(heatPulseSink.Pulses);
    }

    [Fact]
    public void SinkSuppressesDuplicateCellsForSameDynamiteTarget()
    {
        TimberbornExplosiveInfrastructureTarget target = Target(depth: 3);
        RecordingExplosiveTargetApi targetApi = new(target);
        RecordingHeatPulseSink heatPulseSink = new();
        TimberbornExplosiveInfrastructureConsequenceSink sink = new(
            () => Settings(thresholdTicks: 1),
            targetApi,
            heatPulseSink);

        TimberbornExplosiveInfrastructureConsequenceSummary summary = sink.ApplyConsequences(
            9,
            [
                Decision(12, heat: 3),
                Decision(13, heat: 8),
            ]);

        Assert.Equal(2, summary.MatchedTargetCellCount);
        Assert.Equal(1, summary.DuplicateTargetSuppressedCount);
        Assert.Equal(1, summary.TriggeredTargetCount);
        Assert.Single(heatPulseSink.Pulses);
        Assert.Equal(13, targetApi.ResolvedConsequences[1].CellIndex);
    }

    [Fact]
    public void SinkCallsNativeWrapperOnlyWhenEnabledAndAvailable()
    {
        RecordingExplosiveTargetApi targetApi = new(Target(depth: 1, canTriggerNative: true));
        RecordingHeatPulseSink heatPulseSink = new();
        TimberbornExplosiveInfrastructureConsequenceSink sink = new(
            () => Settings(thresholdTicks: 1, nativeEnabled: true),
            targetApi,
            heatPulseSink);

        TimberbornExplosiveInfrastructureConsequenceSummary summary = sink.ApplyConsequences(
            10,
            [Decision(12, heat: 10)]);

        Assert.Equal(1, summary.NativeTriggeredTargetCount);
        Assert.Equal(1, targetApi.NativeTriggerCalls);
    }

    [Fact]
    public void SinkKeepsNativeDisabledPathPulseOnlyWithoutCallingWrapper()
    {
        RecordingExplosiveTargetApi targetApi = new(Target(depth: 1, canTriggerNative: true));
        RecordingHeatPulseSink heatPulseSink = new();
        TimberbornExplosiveInfrastructureConsequenceSink sink = new(
            () => Settings(thresholdTicks: 1, nativeEnabled: false),
            targetApi,
            heatPulseSink);

        TimberbornExplosiveInfrastructureConsequenceSummary summary = sink.ApplyConsequences(
            10,
            [Decision(12, heat: 10)]);

        Assert.Equal(1, summary.TriggeredTargetCount);
        Assert.Equal(5, summary.HeatPulseCellCount);
        Assert.Equal(0, summary.NativeTriggeredTargetCount);
        Assert.Equal(1, summary.SkippedNoSafeApiCount);
        Assert.Equal(0, targetApi.NativeTriggerCalls);
        Assert.Single(heatPulseSink.Pulses);
    }

    [Fact]
    public void SinkReportsSkippedNoSafeApiWhenNativeWrapperUnavailable()
    {
        RecordingExplosiveTargetApi targetApi = new(Target(depth: 1, canTriggerNative: false));
        RecordingHeatPulseSink heatPulseSink = new();
        TimberbornExplosiveInfrastructureConsequenceSink sink = new(
            () => Settings(thresholdTicks: 1, nativeEnabled: true),
            targetApi,
            heatPulseSink);

        TimberbornExplosiveInfrastructureConsequenceSummary summary = sink.ApplyConsequences(
            10,
            [Decision(12, heat: 10)]);

        Assert.Equal(1, summary.SkippedNoSafeApiCount);
        Assert.Equal(0, targetApi.NativeTriggerCalls);
    }

    [Fact]
    public void SinkContainsNativeWrapperFailureAsSafeUnavailableTelemetry()
    {
        RecordingExplosiveTargetApi targetApi = new(
            Target(depth: 1, canTriggerNative: true),
            throwOnNativeTrigger: true);
        RecordingHeatPulseSink heatPulseSink = new();
        TimberbornExplosiveInfrastructureConsequenceSink sink = new(
            () => Settings(thresholdTicks: 1, nativeEnabled: true),
            targetApi,
            heatPulseSink);

        TimberbornExplosiveInfrastructureConsequenceSummary summary = sink.ApplyConsequences(
            10,
            [Decision(12, heat: 10)]);

        Assert.Equal(1, summary.TriggeredTargetCount);
        Assert.Equal(5, summary.HeatPulseCellCount);
        Assert.Equal(0, summary.NativeTriggeredTargetCount);
        Assert.Equal(1, summary.SkippedNoSafeApiCount);
        Assert.Equal(1, targetApi.NativeTriggerCalls);
        Assert.Single(heatPulseSink.Pulses);
    }

    [Fact]
    public void HeatPulseCreatesBoundedThreeDimensionalNeighborShell()
    {
        FireGrid grid = new(3, 3, 3);
        int origin = grid.ToIndex(1, 1, 1);

        FireSimChange[] changes = TimberbornQueuedFireSimHeatPulseSink.CreatePulseChanges(
            grid,
            origin,
            pulseHeat: 12,
            pulseRadius: 1);

        int[] expected =
        [
            grid.ToIndex(1, 1, 1),
            grid.ToIndex(0, 1, 1),
            grid.ToIndex(2, 1, 1),
            grid.ToIndex(1, 0, 1),
            grid.ToIndex(1, 2, 1),
            grid.ToIndex(1, 1, 0),
            grid.ToIndex(1, 1, 2),
        ];
        Assert.Equal(expected.OrderBy(static value => value), changes.Select(static change => change.CellIndex).Order());
        Assert.All(changes, static change => Assert.Equal((byte?)12, change.AddHeat));
    }

    [Fact]
    public void DeltaConsumerRoutesExplosiveTelemetry()
    {
        RecordingExplosiveTargetApi targetApi = new(Target(depth: 1));
        RecordingHeatPulseSink heatPulseSink = new();
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(
                explosiveInfrastructureConsequenceSink: new TimberbornExplosiveInfrastructureConsequenceSink(
                    () => Settings(thresholdTicks: 1),
                    targetApi,
                    heatPulseSink)));

        TimberbornFireDeltaConsumerSummary summary = consumer.Consume(
            11,
            [new CellDelta(12, Cell(heat: 0), Cell(heat: 10))]);

        Assert.Equal(1, summary.ExplosiveInfrastructureConsideredDeltaCount);
        Assert.Equal(1, summary.ExplosiveInfrastructureTriggeredTargetCount);
        Assert.Equal(5, summary.ExplosiveInfrastructureHeatPulseCellCount);
    }

    private static TimberbornExplosiveInfrastructureConsequenceSettings Settings(
        bool enabled = true,
        bool nativeEnabled = false,
        int thresholdTicks = 1,
        int pulseHeat = 10,
        int pulseRadius = 1)
    {
        return new TimberbornExplosiveInfrastructureConsequenceSettings(
            enabled,
            nativeEnabled,
            thresholdTicks,
            pulseHeat,
            pulseRadius);
    }

    private static TimberbornExplosiveInfrastructureTarget Target(
        int depth,
        bool canTriggerNative = false,
        string stableId = "dynamite-1",
        int cellIndex = 12)
    {
        return new TimberbornExplosiveInfrastructureTarget(
            stableId,
            TimberbornExplosiveInfrastructureKind.Dynamite,
            cellIndex,
            depth,
            canTriggerNative);
    }

    private static TimberbornFireCellDeltaDecision Decision(int cellIndex, int heat)
    {
        return TimberbornFireCellDeltaDecision.FromDelta(
            new CellDelta(cellIndex, Cell(heat: 0), Cell(heat)));
    }

    private static ushort Cell(int heat)
    {
        return PackedCell.Pack(fuel: 0, heat, flammability: 0, water: 0, terrain: 1, burningLevel: 0);
    }

    private sealed class RecordingExplosiveTargetApi(
        TimberbornExplosiveInfrastructureTarget? target,
        bool throwOnNativeTrigger = false,
        Func<int, string>? stableIdByResolve = null)
        : ITimberbornExplosiveInfrastructureTargetApi
    {
        public List<TimberbornExplosiveInfrastructureConsequence> ResolvedConsequences { get; } = [];

        public int NativeTriggerCalls { get; private set; }

        public TimberbornExplosiveInfrastructureTarget? ResolveTarget(
            TimberbornExplosiveInfrastructureConsequence consequence)
        {
            ResolvedConsequences.Add(consequence);
            return target is null
                ? null
                : target with
                {
                    StableId = stableIdByResolve?.Invoke(ResolvedConsequences.Count) ?? target.StableId,
                    CellIndex = consequence.CellIndex,
                };
        }

        public TimberbornExplosiveInfrastructureNativeTriggerResult TriggerNative(
            TimberbornExplosiveInfrastructureTarget triggerTarget,
            int delayTicks)
        {
            NativeTriggerCalls++;
            if (throwOnNativeTrigger)
            {
                throw new InvalidOperationException("native trigger failed");
            }

            return new TimberbornExplosiveInfrastructureNativeTriggerResult(
                TimberbornExplosiveInfrastructureNativeTriggerStatus.Triggered);
        }
    }

    private sealed class RecordingHeatPulseSink : ITimberbornExplosiveInfrastructureHeatPulseSink
    {
        public List<TimberbornExplosiveInfrastructureTarget> Pulses { get; } = [];

        public int EnqueueHeatPulse(
            TimberbornExplosiveInfrastructureTarget target,
            int pulseHeat,
            int pulseRadius)
        {
            Pulses.Add(target);
            return 5;
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
