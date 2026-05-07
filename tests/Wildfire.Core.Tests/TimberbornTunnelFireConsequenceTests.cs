using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornTunnelFireConsequenceTests
{
    [Fact]
    public void SinkSkipsExposureWhenBehaviorSettingDisabled()
    {
        RecordingTunnelTargetApi targetApi = new(Target());
        TimberbornTunnelFireSink sink = new(
            () => Settings(behaviorEnabled: false),
            targetApi);

        TimberbornTunnelFireSummary summary = sink.ApplyConsequences(
            1,
            [Decision(12, heat: 5)]);

        Assert.Equal(1, summary.ConsideredDeltaCount);
        Assert.Equal(1, summary.SkippedSettingDisabledCount);
        Assert.Equal(0, summary.UnstableTargetCount);
        Assert.Empty(targetApi.ResolvedConsequences);
        Assert.Empty(targetApi.ExplodedTargets);
    }

    [Fact]
    public void SinkMarksTunnelUnstableAndDefersDestructionByDefault()
    {
        RecordingTunnelTargetApi targetApi = new(Target());
        TimberbornTunnelFireSink sink = new(
            () => Settings(destructionEnabled: false),
            targetApi);

        TimberbornTunnelFireSummary summary = sink.ApplyConsequences(
            2,
            [Decision(12, heat: 6)]);

        Assert.Equal(1, summary.MatchedTargetCellCount);
        Assert.Equal(1, summary.UnstableTargetCount);
        Assert.Equal(1, summary.DestructionDeferredCount);
        Assert.Equal(0, summary.NativeExplodeAttemptedCount);
        Assert.Equal(0, summary.NativeExplodeAppliedCount);
        Assert.Empty(targetApi.ExplodedTargets);
    }

    [Fact]
    public void SinkSuppressesDuplicateCellsForSameTunnelTarget()
    {
        RecordingTunnelTargetApi targetApi = new(Target());
        TimberbornTunnelFireSink sink = new(
            () => Settings(destructionEnabled: false),
            targetApi);

        TimberbornTunnelFireSummary summary = sink.ApplyConsequences(
            3,
            [
                Decision(12, heat: 5),
                Decision(13, heat: 8),
            ]);

        Assert.Equal(2, summary.MatchedTargetCellCount);
        Assert.Equal(1, summary.DuplicateTargetSuppressedCount);
        Assert.Equal(1, summary.UnstableTargetCount);
        Assert.Equal(1, summary.DestructionDeferredCount);
    }

    [Fact]
    public void SinkCallsNativeExplodeOnlyWhenTerrainDestructionEnabled()
    {
        RecordingTunnelTargetApi targetApi = new(Target(canExplodeNative: true));
        TimberbornTunnelFireSink sink = new(
            () => Settings(destructionEnabled: true),
            targetApi);

        TimberbornTunnelFireSummary summary = sink.ApplyConsequences(
            4,
            [Decision(12, heat: 5)]);

        Assert.Equal(1, summary.NativeExplodeAttemptedCount);
        Assert.Equal(1, summary.NativeExplodeAppliedCount);
        Assert.Equal(0, summary.DestructionDeferredCount);
        Assert.Equal([12], targetApi.ExplodedTargets.Select(static target => target.CellIndex).ToArray());
    }

    [Fact]
    public void SinkReportsSafeUnavailableWrapperWithoutNativeMutation()
    {
        RecordingTunnelTargetApi targetApi = new(Target(canExplodeNative: false, canRecover: false));
        TimberbornTunnelFireSink sink = new(
            () => Settings(destructionEnabled: true),
            targetApi);

        TimberbornTunnelFireSummary summary = sink.ApplyConsequences(
            5,
            [Decision(12, heat: 5)]);

        Assert.Equal(0, summary.NativeExplodeAttemptedCount);
        Assert.Equal(0, summary.NativeExplodeAppliedCount);
        Assert.Equal(1, summary.SkippedNoSafeApiCount);
        Assert.Equal(1, summary.RecoverabilityUnknownCount);
        Assert.Empty(targetApi.ExplodedTargets);
    }

    [Fact]
    public void SinkContainsResolverTypeFailureAsSafeUnavailableTelemetry()
    {
        RecordingTunnelTargetApi targetApi = new(Target(), throwOnResolve: true);
        TimberbornTunnelFireSink sink = new(
            () => Settings(destructionEnabled: true),
            targetApi);

        TimberbornTunnelFireSummary summary = sink.ApplyConsequences(
            5,
            [Decision(12, heat: 5)]);

        Assert.Equal(1, summary.ConsideredDeltaCount);
        Assert.Equal(0, summary.MatchedTargetCellCount);
        Assert.Equal(0, summary.NativeExplodeAttemptedCount);
        Assert.Equal(0, summary.NativeExplodeAppliedCount);
        Assert.Equal(1, summary.SkippedNoSafeApiCount);
        Assert.Equal(1, summary.RecoverabilityUnknownCount);
        Assert.Empty(targetApi.ExplodedTargets);
    }

    [Fact]
    public void SinkReportsExplodeMethodFailureAsSafeUnavailableTelemetry()
    {
        RecordingTunnelTargetApi targetApi = new(
            Target(canExplodeNative: true),
            explodeResult: new TimberbornTunnelNativeExplodeResult(
                TimberbornTunnelNativeExplodeStatus.SkippedNoSafeApi,
                RecoverabilityPreserved: false));
        TimberbornTunnelFireSink sink = new(
            () => Settings(destructionEnabled: true),
            targetApi);

        TimberbornTunnelFireSummary summary = sink.ApplyConsequences(
            5,
            [Decision(12, heat: 5)]);

        Assert.Equal(1, summary.NativeExplodeAttemptedCount);
        Assert.Equal(0, summary.NativeExplodeAppliedCount);
        Assert.Equal(1, summary.SkippedNoSafeApiCount);
        Assert.Equal(1, summary.RecoverabilityUnknownCount);
        Assert.Single(targetApi.ExplodedTargets);
    }

    [Fact]
    public void SinkContainsExplodeInvocationFailureAsSafeUnavailableTelemetry()
    {
        RecordingTunnelTargetApi targetApi = new(
            Target(canExplodeNative: true),
            throwOnExplode: true);
        TimberbornTunnelFireSink sink = new(
            () => Settings(destructionEnabled: true),
            targetApi);

        TimberbornTunnelFireSummary summary = sink.ApplyConsequences(
            5,
            [Decision(12, heat: 5)]);

        Assert.Equal(1, summary.NativeExplodeAttemptedCount);
        Assert.Equal(0, summary.NativeExplodeAppliedCount);
        Assert.Equal(1, summary.SkippedNoSafeApiCount);
        Assert.Equal(1, summary.RecoverabilityUnknownCount);
        Assert.Single(targetApi.ExplodedTargets);
    }

    [Fact]
    public void DeltaConsumerRoutesTunnelTelemetry()
    {
        RecordingTunnelTargetApi targetApi = new(Target());
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(
                tunnelFireSink: new TimberbornTunnelFireSink(
                    () => Settings(destructionEnabled: false),
                    targetApi)));

        TimberbornFireDeltaConsumerSummary summary = consumer.Consume(
            6,
            [new CellDelta(12, Cell(heat: 0), Cell(heat: 7))]);

        Assert.Equal(1, summary.TunnelFireConsideredDeltaCount);
        Assert.Equal(1, summary.TunnelFireUnstableTargetCount);
        Assert.Equal(1, summary.TunnelFireDestructionDeferredCount);
    }

    private static TimberbornTunnelFireSettings Settings(
        bool behaviorEnabled = true,
        bool destructionEnabled = false)
    {
        return new TimberbornTunnelFireSettings(behaviorEnabled, destructionEnabled);
    }

    private static TimberbornTunnelFireTarget Target(
        bool canExplodeNative = true,
        bool canRecover = false,
        string stableId = "tunnel-1",
        int cellIndex = 12)
    {
        return new TimberbornTunnelFireTarget(
            stableId,
            cellIndex,
            BottomLevel: 0,
            CanMarkUnstable: true,
            canExplodeNative,
            canRecover);
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

    private sealed class RecordingTunnelTargetApi(
        TimberbornTunnelFireTarget? target,
        bool throwOnResolve = false,
        TimberbornTunnelNativeExplodeResult? explodeResult = null,
        bool throwOnExplode = false)
        : ITimberbornTunnelFireTargetApi
    {
        public List<TimberbornTunnelFireConsequence> ResolvedConsequences { get; } = [];

        public List<TimberbornTunnelFireTarget> ExplodedTargets { get; } = [];

        public TimberbornTunnelFireTarget? ResolveTarget(TimberbornTunnelFireConsequence consequence)
        {
            if (throwOnResolve)
            {
                throw new TypeLoadException("tunnel type lookup failed");
            }

            ResolvedConsequences.Add(consequence);
            return target is null ? null : target with { CellIndex = consequence.CellIndex };
        }

        public TimberbornTunnelNativeExplodeResult ExplodeNative(TimberbornTunnelFireTarget explodeTarget)
        {
            ExplodedTargets.Add(explodeTarget);
            if (throwOnExplode)
            {
                throw new InvalidOperationException("tunnel explode failed");
            }

            return explodeResult ?? new TimberbornTunnelNativeExplodeResult(
                TimberbornTunnelNativeExplodeStatus.Applied,
                RecoverabilityPreserved: explodeTarget.CanRecover);
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
