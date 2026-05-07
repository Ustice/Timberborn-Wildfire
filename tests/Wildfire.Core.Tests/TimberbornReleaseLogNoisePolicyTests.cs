using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornReleaseLogNoisePolicyTests
{
    [Theory]
    [InlineData("wildfire_timberborn_dispatch_failed", TimberbornReleaseLogClass.ReleaseError)]
    [InlineData("wildfire_timberborn_compute_asset_missing", TimberbornReleaseLogClass.ReleaseWarning)]
    [InlineData("wildfire_command_result", TimberbornReleaseLogClass.QaOnly)]
    [InlineData("wildfire_timberborn_dispatch_completed", TimberbornReleaseLogClass.ReleaseDiagnostic)]
    [InlineData("wildfire_timberborn_path_infrastructure_fire_applied", TimberbornReleaseLogClass.TooNoisy)]
    public void ClassifiesReleaseLogTokens(string token, TimberbornReleaseLogClass expected)
    {
        Assert.Equal(expected, TimberbornReleaseLogNoisePolicy.ClassifyToken(token));
    }

    [Fact]
    public void ConsequenceSummaryLogsOnlyWhenSignificantCountersAreNonzero()
    {
        Assert.False(TimberbornReleaseLogNoisePolicy.ShouldLogConsequenceSummary(0, 0, 0));
        Assert.True(TimberbornReleaseLogNoisePolicy.ShouldLogConsequenceSummary(0, 1, 0));
    }

    [Fact]
    public void QuietConsequenceDispatchStillReturnsStatusCountersWithoutWritingLog()
    {
        RecordingPowerInfrastructureTargetApi targetApi = new();
        RecordingFireLogSink logSink = new();
        TimberbornPowerInfrastructureFireSink sink = new(targetApi, logSink: logSink);

        TimberbornPowerInfrastructureFireSummary summary = sink.ApplyConsequences(
            12,
            [Decision(4, oldFuel: 8, newFuel: 4)]);

        Assert.Equal(1, summary.ConsideredDeltaCount);
        Assert.Equal(0, summary.MatchedTargetCellCount);
        Assert.Empty(logSink.InfoMessages);
    }

    private static TimberbornFireCellDeltaDecision Decision(int cellIndex, int oldFuel, int newFuel)
    {
        return TimberbornFireCellDeltaDecision.FromDelta(
            new CellDelta(cellIndex, Cell(oldFuel), Cell(newFuel)));
    }

    private static ushort Cell(int fuel)
    {
        return PackedCell.Pack(fuel, heat: 10, flammability: 3, water: 0, terrain: 1, burningLevel: 0);
    }

    private sealed class RecordingPowerInfrastructureTargetApi : ITimberbornPowerInfrastructureFireTargetApi
    {
        public TimberbornPowerInfrastructureFireTarget? ResolveTarget(
            TimberbornPowerInfrastructureFireConsequence consequence)
        {
            return null;
        }

        public TimberbornPowerInfrastructureApplyResult ApplyDamage(
            TimberbornPowerInfrastructureFireTarget target,
            int damageApplied,
            bool isFullyDamaged)
        {
            return new TimberbornPowerInfrastructureApplyResult(false, false, false, false);
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
