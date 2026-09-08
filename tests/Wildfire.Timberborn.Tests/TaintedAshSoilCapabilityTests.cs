using Wildfire.Core;
using Wildfire.Timberborn.Ash;

namespace Wildfire.Timberborn.Tests;

public sealed class TaintedAshSoilCapabilityTests
{
    [Fact]
    public void UnavailableIsReportedOnceWhileSummaryTracksCurrentCandidates()
    {
        Log log = new();
        TimberbornTaintedAshSoilPoisoningService service = new(logSink: log);
        Dictionary<int, TimberbornAshFieldEntry> entries = [];
        Assert.Equal(TimberbornTaintedAshSoilPoisoningSummary.Empty, service.Apply(1, entries));
        Assert.Empty(log.Messages);

        entries.Add(4, Entry(4));
        Assert.Equal(TimberbornTaintedAshSoilPoisoningOutcome.Unavailable, service.Apply(2, entries).Outcome);
        entries.Add(5, Entry(5));
        Assert.Equal(2, service.Apply(3, entries).CandidateCellCount);
        Assert.Equal(0, service.LastSummary.AppliedCellCount);
        Assert.Contains("outcome=unavailable", Assert.Single(log.Messages));
        Assert.DoesNotContain("poisoning_applied", log.Messages[0]);
        entries.Clear();
        Assert.Equal(TimberbornTaintedAshSoilPoisoningSummary.Empty, service.Apply(4, entries));
        service.Clear();
        entries.Add(4, Entry(4));
        service.Apply(5, entries);
        Assert.Single(log.Messages); // Clear resets observations, not the service-instance diagnostic.
    }

    [Fact]
    public void ActualAdapterResultRemainsDistinctFromUnavailable()
    {
        Log log = new();
        TimberbornTaintedAshSoilPoisoningService service = new(new AppliedAdapter(), log);
        var summary = service.Apply(2, new Dictionary<int, TimberbornAshFieldEntry> { [4] = Entry(4) });
        Assert.Equal(TimberbornTaintedAshSoilPoisoningOutcome.Applied, summary.Outcome);
        Assert.Equal(1, summary.AppliedCellCount);
        Assert.Contains("outcome=applied", Assert.Single(log.Messages));
    }

    [Fact]
    public void ThrowingDiagnosticIsNotRetriedOnFollowingTicks()
    {
        Log log = new() { Throw = true };
        TimberbornTaintedAshSoilPoisoningService service = new(logSink: log);
        Dictionary<int, TimberbornAshFieldEntry> entries = new() { [4] = Entry(4) };
        Assert.Throws<InvalidOperationException>(() => service.Apply(1, entries));
        Assert.Equal(TimberbornTaintedAshSoilPoisoningOutcome.Unavailable, service.Apply(2, entries).Outcome);
        Assert.Single(log.Messages);
        Assert.Equal(1, entries[4].Strength);
    }

    private static TimberbornAshFieldEntry Entry(int index) =>
        new(index, WildfireAshQuality.Tainted, 1, default, 1, 1, TimberbornAshFieldEntry.CurrentPersistenceVersion);

    private sealed class AppliedAdapter : ITimberbornTaintedAshSoilPoisoningAdapter
    {
        public TimberbornTaintedAshSoilPoisoningSummary ApplyPoisoning(
            uint tick, IReadOnlyList<TimberbornTaintedAshSoilPoisoningCandidate> candidates) =>
            new(candidates.Count, candidates.Count, TimberbornTaintedAshSoilPoisoningOutcome.Applied);
    }

    private sealed class Log : ITimberbornFireLogSink
    {
        internal readonly List<string> Messages = [];
        internal bool Throw;
        public void Info(string message)
        {
            Messages.Add(message);
            if (Throw) throw new InvalidOperationException("diagnostic callback");
        }
        public void Warning(string message) => Info(message);
    }
}
