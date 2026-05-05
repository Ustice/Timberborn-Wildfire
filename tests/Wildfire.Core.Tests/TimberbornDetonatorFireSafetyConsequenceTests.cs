using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornDetonatorFireSafetyConsequenceTests
{
    [Fact]
    public void SinkSkipsExposureWhenSettingDisabled()
    {
        RecordingDetonatorTargetApi targetApi = new(Target());
        TimberbornDetonatorFireSafetySink sink = new(
            () => false,
            targetApi);

        TimberbornDetonatorFireSafetySummary summary = sink.ApplyConsequences(
            1,
            [Decision(12, heat: 5)]);

        Assert.Equal(1, summary.ConsideredDeltaCount);
        Assert.Equal(1, summary.SkippedSettingDisabledCount);
        Assert.Equal(0, summary.DisabledTargetCount);
        Assert.Empty(targetApi.ResolvedConsequences);
        Assert.Empty(targetApi.DisabledTargets);
    }

    [Fact]
    public void SinkDisarmsMatchedDetonatorWithoutArming()
    {
        RecordingDetonatorTargetApi targetApi = new(Target());
        TimberbornDetonatorFireSafetySink sink = new(
            () => true,
            targetApi);

        TimberbornDetonatorFireSafetySummary summary = sink.ApplyConsequences(
            2,
            [Decision(12, heat: 6)]);

        Assert.Equal(1, summary.MatchedTargetCellCount);
        Assert.Equal(1, summary.DisabledTargetCount);
        Assert.Equal(0, summary.ArmedTargetCount);
        Assert.Equal(1, summary.RecoverabilityPreservedCount);
        Assert.Equal([12], targetApi.DisabledTargets.Select(static target => target.CellIndex).ToArray());
    }

    [Fact]
    public void SinkSuppressesDuplicateCellsForSameDetonatorTarget()
    {
        RecordingDetonatorTargetApi targetApi = new(Target());
        TimberbornDetonatorFireSafetySink sink = new(
            () => true,
            targetApi);

        TimberbornDetonatorFireSafetySummary summary = sink.ApplyConsequences(
            3,
            [
                Decision(12, heat: 5),
                Decision(13, heat: 8),
            ]);

        Assert.Equal(2, summary.MatchedTargetCellCount);
        Assert.Equal(1, summary.DuplicateTargetSuppressedCount);
        Assert.Equal(1, summary.DisabledTargetCount);
        Assert.Single(targetApi.DisabledTargets);
        Assert.Equal(13, targetApi.DisabledTargets[0].CellIndex);
    }

    [Fact]
    public void SinkReportsSafeUnavailableWrapperWithoutDisabling()
    {
        RecordingDetonatorTargetApi targetApi = new(Target(canDisable: false, canPreserveAutomationState: false));
        TimberbornDetonatorFireSafetySink sink = new(
            () => true,
            targetApi);

        TimberbornDetonatorFireSafetySummary summary = sink.ApplyConsequences(
            4,
            [Decision(12, heat: 5)]);

        Assert.Equal(0, summary.DisabledTargetCount);
        Assert.Equal(1, summary.SkippedNoSafeApiCount);
        Assert.Equal(1, summary.RecoverabilityUnknownCount);
        Assert.Empty(targetApi.DisabledTargets);
    }

    [Fact]
    public void DeltaConsumerRoutesDetonatorTelemetry()
    {
        RecordingDetonatorTargetApi targetApi = new(Target());
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(
                detonatorFireSafetySink: new TimberbornDetonatorFireSafetySink(
                    () => true,
                    targetApi)));

        TimberbornFireDeltaConsumerSummary summary = consumer.Consume(
            5,
            [new CellDelta(12, Cell(heat: 0), Cell(heat: 7))]);

        Assert.Equal(1, summary.DetonatorFireSafetyConsideredDeltaCount);
        Assert.Equal(1, summary.DetonatorFireSafetyDisabledTargetCount);
        Assert.Equal(0, summary.DetonatorFireSafetyArmedTargetCount);
    }

    private static TimberbornDetonatorFireSafetyTarget Target(
        bool canDisable = true,
        bool canPreserveAutomationState = true,
        string stableId = "detonator-1",
        int cellIndex = 12)
    {
        return new TimberbornDetonatorFireSafetyTarget(
            stableId,
            cellIndex,
            canDisable,
            canPreserveAutomationState);
    }

    private static TimberbornFireCellDeltaDecision Decision(int cellIndex, int heat)
    {
        return TimberbornFireCellDeltaDecision.FromDelta(
            new CellDelta(cellIndex, Cell(heat: 0), Cell(heat)));
    }

    private static ushort Cell(int heat)
    {
        return PackedCell.Pack(fuel: 0, heat, flammability: 0, water: 0, terrain: 1, heatLoss: 0);
    }

    private sealed class RecordingDetonatorTargetApi(TimberbornDetonatorFireSafetyTarget? target)
        : ITimberbornDetonatorFireSafetyTargetApi
    {
        public List<TimberbornDetonatorFireSafetyConsequence> ResolvedConsequences { get; } = [];

        public List<TimberbornDetonatorFireSafetyTarget> DisabledTargets { get; } = [];

        public TimberbornDetonatorFireSafetyTarget? ResolveTarget(
            TimberbornDetonatorFireSafetyConsequence consequence)
        {
            ResolvedConsequences.Add(consequence);
            return target is null ? null : target with { CellIndex = consequence.CellIndex };
        }

        public TimberbornDetonatorFireSafetyDisableResult DisableTarget(
            TimberbornDetonatorFireSafetyTarget disableTarget)
        {
            DisabledTargets.Add(disableTarget);
            return new TimberbornDetonatorFireSafetyDisableResult(
                TimberbornDetonatorFireSafetyDisableStatus.Disabled,
                RecoverabilityPreserved: disableTarget.CanPreserveAutomationState);
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
