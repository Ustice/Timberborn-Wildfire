namespace Wildfire.Timberborn;

public sealed class TimberbornBeaverFieldBehaviorOptions
{
    public static readonly TimberbornBeaverFieldBehaviorOptions Default = new();

    public TimberbornBeaverFieldBehaviorOptions(
        uint DecisionCooldownTicks = 3,
        int MaxDecisionsPerDispatch = 64,
        int SmokeCoughingThresholdSamples = 3,
        int SmokeChokingCandidateThresholdSamples = 8,
        int SmokeDeathCandidateThresholdSamples = 16,
        int SmokeRecoveryDecaySamples = 1)
    {
        if (DecisionCooldownTicks == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DecisionCooldownTicks),
                DecisionCooldownTicks,
                "The beaver behavior cooldown must be positive.");
        }

        if (MaxDecisionsPerDispatch <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxDecisionsPerDispatch),
                MaxDecisionsPerDispatch,
                "The maximum beaver behavior decision count must be positive.");
        }

        if (SmokeCoughingThresholdSamples <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SmokeCoughingThresholdSamples),
                SmokeCoughingThresholdSamples,
                "The smoke coughing threshold must be positive.");
        }

        if (SmokeChokingCandidateThresholdSamples < SmokeCoughingThresholdSamples)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SmokeChokingCandidateThresholdSamples),
                SmokeChokingCandidateThresholdSamples,
                "The smoke choking candidate threshold must not be below the coughing threshold.");
        }

        if (SmokeDeathCandidateThresholdSamples < SmokeChokingCandidateThresholdSamples)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SmokeDeathCandidateThresholdSamples),
                SmokeDeathCandidateThresholdSamples,
                "The smoke death candidate threshold must not be below the choking candidate threshold.");
        }

        if (SmokeRecoveryDecaySamples <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SmokeRecoveryDecaySamples),
                SmokeRecoveryDecaySamples,
                "The smoke recovery decay must be positive.");
        }

        this.DecisionCooldownTicks = DecisionCooldownTicks;
        this.MaxDecisionsPerDispatch = MaxDecisionsPerDispatch;
        this.SmokeCoughingThresholdSamples = SmokeCoughingThresholdSamples;
        this.SmokeChokingCandidateThresholdSamples = SmokeChokingCandidateThresholdSamples;
        this.SmokeDeathCandidateThresholdSamples = SmokeDeathCandidateThresholdSamples;
        this.SmokeRecoveryDecaySamples = SmokeRecoveryDecaySamples;
    }

    public uint DecisionCooldownTicks { get; }

    public int MaxDecisionsPerDispatch { get; }

    public int SmokeCoughingThresholdSamples { get; }

    public int SmokeChokingCandidateThresholdSamples { get; }

    public int SmokeDeathCandidateThresholdSamples { get; }

    public int SmokeRecoveryDecaySamples { get; }
}

public enum TimberbornBeaverFieldBehaviorVariant
{
    Smoke = 1,
    ToxicSmoke = 2,
    FireHeat = 3,
}

public enum TimberbornBeaverFieldBehaviorAction
{
    SafeNoOp = 1,
    CoughingSafeNoOp = 2,
}

public enum TimberbornBeaverFieldBehaviorActuatorStatus
{
    Applied = 1,
    SkippedNoSafeApi = 2,
    Failed = 3,
}

public readonly record struct TimberbornBeaverFieldBehaviorDecision(
    string BeaverId,
    int X,
    int Y,
    int Z,
    TimberbornBeaverFieldBehaviorVariant Variant,
    TimberbornBeaverFieldBehaviorAction Action,
    int RespiratoryExposureCells,
    int BurnExposureCells,
    int ContaminatedSmokeCells,
    int ToxicExposureCells,
    int SteamCells,
    int TaintedAftermathCells,
    uint? Tick);

public readonly record struct TimberbornBeaverFieldBehaviorActuatorResult(
    TimberbornBeaverFieldBehaviorActuatorStatus Status,
    string Reason)
{
    public static readonly TimberbornBeaverFieldBehaviorActuatorResult Applied = new(
        TimberbornBeaverFieldBehaviorActuatorStatus.Applied,
        "applied");

    public static readonly TimberbornBeaverFieldBehaviorActuatorResult SkippedNoSafeApi = new(
        TimberbornBeaverFieldBehaviorActuatorStatus.SkippedNoSafeApi,
        "no_safe_api");
}

public interface ITimberbornBeaverFieldBehaviorActuator
{
    TimberbornBeaverFieldBehaviorActuatorResult Apply(TimberbornBeaverFieldBehaviorDecision decision);

    TimberbornBeaverFieldBehaviorActuatorResult Recover(TimberbornBeaverFieldBehaviorStateEntry entry, uint? tick);

    void Clear();
}

public sealed class TimberbornNoOpBeaverFieldBehaviorActuator : ITimberbornBeaverFieldBehaviorActuator
{
    public static readonly TimberbornNoOpBeaverFieldBehaviorActuator Instance = new();

    private TimberbornNoOpBeaverFieldBehaviorActuator()
    {
    }

    public TimberbornBeaverFieldBehaviorActuatorResult Apply(TimberbornBeaverFieldBehaviorDecision decision)
    {
        return TimberbornBeaverFieldBehaviorActuatorResult.Applied;
    }

    public TimberbornBeaverFieldBehaviorActuatorResult Recover(TimberbornBeaverFieldBehaviorStateEntry entry, uint? tick)
    {
        return TimberbornBeaverFieldBehaviorActuatorResult.Applied;
    }

    public void Clear()
    {
    }
}

public readonly record struct TimberbornBeaverFieldBehaviorCounters(
    bool DispatcherEnabled,
    int TrackedBeaverCount,
    int DecisionsEvaluated,
    int SmokeDecisionsApplied,
    int ToxicSmokeDecisionsApplied,
    int FireHeatDecisionsApplied,
    int NoOpDecisionsApplied,
    int DecisionsSkippedCooldown,
    int DecisionsSkippedBatch,
    int SkippedNoSafeApi,
    int FailedDecisions,
    int RecoveryActions,
    int SmokeExposedSamples,
    int SmokeExposureAccumulatedSamples,
    int SmokeCoughingEntered,
    int SmokeCoughingRecovered,
    int SmokeRecoveryDecays,
    int SmokeChokingCandidates,
    int SmokeChokingSkippedUnsafeApi,
    int SmokeDeathCandidates,
    int SmokeDeathSkippedUnsafeApi,
    int PersistenceSaveCount,
    int PersistenceLoadCount,
    uint? LastDecisionTick);

public sealed record TimberbornBeaverFieldBehaviorSnapshot(
    int PersistenceVersion,
    IReadOnlyList<TimberbornBeaverFieldBehaviorStateEntry> Entries)
{
    public const int CurrentPersistenceVersion = 1;

    public static readonly TimberbornBeaverFieldBehaviorSnapshot Empty = new(
        CurrentPersistenceVersion,
        Array.Empty<TimberbornBeaverFieldBehaviorStateEntry>());
}

public sealed record TimberbornBeaverFieldBehaviorStateEntry(
    int PersistenceVersion,
    string BeaverId,
    TimberbornBeaverFieldBehaviorVariant LastVariant,
    uint LastDecisionTick,
    int ConsecutiveExposedSamples,
    bool IsExposed)
{
    public const int CurrentPersistenceVersion = 1;
}

public sealed class TimberbornBeaverFieldBehaviorDispatcher
{
    private readonly ITimberbornBeaverFieldBehaviorActuator _actuator;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly Dictionary<string, TimberbornBeaverFieldBehaviorStateEntry> _statesByBeaverId =
        new(StringComparer.Ordinal);
    private int _decisionsEvaluated;
    private int _smokeDecisionsApplied;
    private int _toxicSmokeDecisionsApplied;
    private int _fireHeatDecisionsApplied;
    private int _noOpDecisionsApplied;
    private int _decisionsSkippedCooldown;
    private int _decisionsSkippedBatch;
    private int _skippedNoSafeApi;
    private int _failedDecisions;
    private int _recoveryActions;
    private int _smokeExposedSamples;
    private int _smokeExposureAccumulatedSamples;
    private int _smokeCoughingEntered;
    private int _smokeCoughingRecovered;
    private int _smokeRecoveryDecays;
    private int _smokeChokingCandidates;
    private int _smokeChokingSkippedUnsafeApi;
    private int _smokeDeathCandidates;
    private int _smokeDeathSkippedUnsafeApi;
    private int _persistenceSaveCount;
    private int _persistenceLoadCount;
    private uint? _lastDecisionTick;

    public TimberbornBeaverFieldBehaviorDispatcher(
        ITimberbornBeaverFieldBehaviorActuator actuator,
        ITimberbornFireLogSink logSink,
        TimberbornBeaverFieldBehaviorOptions? options = null)
    {
        _actuator = actuator ?? throw new ArgumentNullException(nameof(actuator));
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        Options = options ?? TimberbornBeaverFieldBehaviorOptions.Default;
    }

    public TimberbornBeaverFieldBehaviorOptions Options { get; }

    public TimberbornBeaverFieldBehaviorCounters Counters => new(
        DispatcherEnabled: true,
        TrackedBeaverCount: _statesByBeaverId.Count,
        DecisionsEvaluated: _decisionsEvaluated,
        SmokeDecisionsApplied: _smokeDecisionsApplied,
        ToxicSmokeDecisionsApplied: _toxicSmokeDecisionsApplied,
        FireHeatDecisionsApplied: _fireHeatDecisionsApplied,
        NoOpDecisionsApplied: _noOpDecisionsApplied,
        DecisionsSkippedCooldown: _decisionsSkippedCooldown,
        DecisionsSkippedBatch: _decisionsSkippedBatch,
        SkippedNoSafeApi: _skippedNoSafeApi,
        FailedDecisions: _failedDecisions,
        RecoveryActions: _recoveryActions,
        SmokeExposedSamples: _smokeExposedSamples,
        SmokeExposureAccumulatedSamples: _smokeExposureAccumulatedSamples,
        SmokeCoughingEntered: _smokeCoughingEntered,
        SmokeCoughingRecovered: _smokeCoughingRecovered,
        SmokeRecoveryDecays: _smokeRecoveryDecays,
        SmokeChokingCandidates: _smokeChokingCandidates,
        SmokeChokingSkippedUnsafeApi: _smokeChokingSkippedUnsafeApi,
        SmokeDeathCandidates: _smokeDeathCandidates,
        SmokeDeathSkippedUnsafeApi: _smokeDeathSkippedUnsafeApi,
        PersistenceSaveCount: _persistenceSaveCount,
        PersistenceLoadCount: _persistenceLoadCount,
        LastDecisionTick: _lastDecisionTick);

    public void Dispatch(TimberbornBeaverFieldExposureSnapshot exposure, uint? tick)
    {
        if (exposure is null)
        {
            throw new ArgumentNullException(nameof(exposure));
        }

        if (!exposure.IsAvailable)
        {
            LogState(tick, "exposure_unavailable");
            return;
        }

        IReadOnlyList<TimberbornBeaverFieldExposureClassification> classifications =
            exposure.Classifications ?? Array.Empty<TimberbornBeaverFieldExposureClassification>();
        HashSet<string> sampledBeaverIds = classifications
            .Select(static classification => classification.BeaverId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> exposedBeaverIds = classifications
            .Where(static classification => classification.HasExposure)
            .Select(static classification => classification.BeaverId)
            .ToHashSet(StringComparer.Ordinal);

        _statesByBeaverId.Values
            .Where(entry =>
                sampledBeaverIds.Contains(entry.BeaverId) &&
                !exposedBeaverIds.Contains(entry.BeaverId) &&
                (entry.IsExposed || entry.ConsecutiveExposedSamples > 0))
            .ToArray()
            .ToList()
            .ForEach(entry => Recover(entry, tick));

        TimberbornBeaverFieldExposureClassification[] exposedClassifications = classifications
            .Where(static classification => classification.HasExposure)
            .OrderBy(static classification => classification.BeaverId, StringComparer.Ordinal)
            .ToArray();
        _decisionsSkippedBatch += Math.Max(0, exposedClassifications.Length - Options.MaxDecisionsPerDispatch);

        exposedClassifications
            .Take(Options.MaxDecisionsPerDispatch)
            .Select(classification => CreateDecision(classification, tick))
            .ToList()
            .ForEach(decision => ApplyDecision(decision, tick));

        LogState(tick, "dispatched");
    }

    public TimberbornBeaverFieldBehaviorSnapshot CaptureState()
    {
        _persistenceSaveCount++;
        return new TimberbornBeaverFieldBehaviorSnapshot(
            TimberbornBeaverFieldBehaviorSnapshot.CurrentPersistenceVersion,
            _statesByBeaverId.Values
                .OrderBy(static entry => entry.BeaverId, StringComparer.Ordinal)
                .ToArray());
    }

    public void RestoreState(TimberbornBeaverFieldBehaviorSnapshot? snapshot)
    {
        _persistenceLoadCount++;
        _statesByBeaverId.Clear();
        if (snapshot is null ||
            snapshot.PersistenceVersion != TimberbornBeaverFieldBehaviorSnapshot.CurrentPersistenceVersion)
        {
            return;
        }

        snapshot.Entries
            .Where(static entry =>
                entry.PersistenceVersion == TimberbornBeaverFieldBehaviorStateEntry.CurrentPersistenceVersion &&
                !string.IsNullOrWhiteSpace(entry.BeaverId))
            .GroupBy(static entry => entry.BeaverId, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToList()
            .ForEach(entry => _statesByBeaverId[entry.BeaverId] = entry);
    }

    public void Clear()
    {
        _statesByBeaverId.Clear();
        _decisionsEvaluated = 0;
        _smokeDecisionsApplied = 0;
        _toxicSmokeDecisionsApplied = 0;
        _fireHeatDecisionsApplied = 0;
        _noOpDecisionsApplied = 0;
        _decisionsSkippedCooldown = 0;
        _decisionsSkippedBatch = 0;
        _skippedNoSafeApi = 0;
        _failedDecisions = 0;
        _recoveryActions = 0;
        _smokeExposedSamples = 0;
        _smokeExposureAccumulatedSamples = 0;
        _smokeCoughingEntered = 0;
        _smokeCoughingRecovered = 0;
        _smokeRecoveryDecays = 0;
        _smokeChokingCandidates = 0;
        _smokeChokingSkippedUnsafeApi = 0;
        _smokeDeathCandidates = 0;
        _smokeDeathSkippedUnsafeApi = 0;
        _lastDecisionTick = null;
        _actuator.Clear();
    }

    private void ApplyDecision(TimberbornBeaverFieldBehaviorDecision decision, uint? tick)
    {
        _decisionsEvaluated++;
        _smokeExposedSamples += decision.Variant == TimberbornBeaverFieldBehaviorVariant.Smoke ? 1 : 0;
        if (IsCoolingDown(decision.BeaverId, tick))
        {
            _decisionsSkippedCooldown++;
            return;
        }

        int previousExposedSamples = _statesByBeaverId.TryGetValue(
            decision.BeaverId,
            out TimberbornBeaverFieldBehaviorStateEntry previous)
            ? previous.ConsecutiveExposedSamples
            : 0;
        TimberbornBeaverFieldBehaviorDecision progressedDecision = decision with
        {
            Action = SelectAction(decision, previousExposedSamples + 1),
        };

        TimberbornBeaverFieldBehaviorActuatorResult result = _actuator.Apply(progressedDecision);
        if (result.Status == TimberbornBeaverFieldBehaviorActuatorStatus.SkippedNoSafeApi)
        {
            _skippedNoSafeApi++;
            return;
        }

        if (result.Status == TimberbornBeaverFieldBehaviorActuatorStatus.Failed)
        {
            _failedDecisions++;
            return;
        }

        int exposedSamples = previousExposedSamples + 1;
        _statesByBeaverId[decision.BeaverId] = new TimberbornBeaverFieldBehaviorStateEntry(
            TimberbornBeaverFieldBehaviorStateEntry.CurrentPersistenceVersion,
            progressedDecision.BeaverId,
            progressedDecision.Variant,
            tick ?? 0,
            exposedSamples,
            IsExposed: true);
        _lastDecisionTick = tick;
        CountAppliedVariant(progressedDecision);
        CountSmokeProgression(progressedDecision, previousExposedSamples, exposedSamples);
    }

    private bool IsCoolingDown(string beaverId, uint? tick)
    {
        return tick.HasValue &&
            _statesByBeaverId.TryGetValue(beaverId, out TimberbornBeaverFieldBehaviorStateEntry state) &&
            tick.Value >= state.LastDecisionTick &&
            tick.Value - state.LastDecisionTick < Options.DecisionCooldownTicks;
    }

    private void Recover(TimberbornBeaverFieldBehaviorStateEntry entry, uint? tick)
    {
        TimberbornBeaverFieldBehaviorActuatorResult result = _actuator.Recover(entry, tick);
        if (result.Status == TimberbornBeaverFieldBehaviorActuatorStatus.SkippedNoSafeApi)
        {
            _skippedNoSafeApi++;
            return;
        }

        if (result.Status == TimberbornBeaverFieldBehaviorActuatorStatus.Failed)
        {
            _failedDecisions++;
            return;
        }

        _statesByBeaverId[entry.BeaverId] = entry with
        {
            IsExposed = false,
            ConsecutiveExposedSamples = RecoveryExposedSamples(entry),
        };
        _recoveryActions++;
        CountSmokeRecovery(entry, _statesByBeaverId[entry.BeaverId].ConsecutiveExposedSamples);
    }

    private void CountAppliedVariant(TimberbornBeaverFieldBehaviorDecision decision)
    {
        _noOpDecisionsApplied += IsNoOpAction(decision.Action) ? 1 : 0;
        _smokeDecisionsApplied += decision.Variant == TimberbornBeaverFieldBehaviorVariant.Smoke ? 1 : 0;
        _toxicSmokeDecisionsApplied += decision.Variant == TimberbornBeaverFieldBehaviorVariant.ToxicSmoke ? 1 : 0;
        _fireHeatDecisionsApplied += decision.Variant == TimberbornBeaverFieldBehaviorVariant.FireHeat ? 1 : 0;
    }

    private void CountSmokeProgression(
        TimberbornBeaverFieldBehaviorDecision decision,
        int previousExposedSamples,
        int exposedSamples)
    {
        if (decision.Variant != TimberbornBeaverFieldBehaviorVariant.Smoke)
        {
            return;
        }

        _smokeExposureAccumulatedSamples++;
        _smokeCoughingEntered += !IsSmokeCoughing(previousExposedSamples) && IsSmokeCoughing(exposedSamples) ? 1 : 0;
        bool chokingCandidateEntered = previousExposedSamples < Options.SmokeChokingCandidateThresholdSamples &&
            exposedSamples >= Options.SmokeChokingCandidateThresholdSamples;
        bool deathCandidateEntered = previousExposedSamples < Options.SmokeDeathCandidateThresholdSamples &&
            exposedSamples >= Options.SmokeDeathCandidateThresholdSamples;
        _smokeChokingCandidates += chokingCandidateEntered ? 1 : 0;
        _smokeChokingSkippedUnsafeApi += chokingCandidateEntered ? 1 : 0;
        _smokeDeathCandidates += deathCandidateEntered ? 1 : 0;
        _smokeDeathSkippedUnsafeApi += deathCandidateEntered ? 1 : 0;
        _skippedNoSafeApi += chokingCandidateEntered || deathCandidateEntered
            ? Convert.ToInt32(chokingCandidateEntered) + Convert.ToInt32(deathCandidateEntered)
            : 0;
    }

    private void CountSmokeRecovery(TimberbornBeaverFieldBehaviorStateEntry entry, int recoveredExposedSamples)
    {
        if (entry.LastVariant != TimberbornBeaverFieldBehaviorVariant.Smoke)
        {
            return;
        }

        _smokeRecoveryDecays += recoveredExposedSamples < entry.ConsecutiveExposedSamples ? 1 : 0;
        _smokeCoughingRecovered +=
            IsSmokeCoughing(entry.ConsecutiveExposedSamples) && !IsSmokeCoughing(recoveredExposedSamples)
                ? 1
                : 0;
    }

    private int RecoveryExposedSamples(TimberbornBeaverFieldBehaviorStateEntry entry)
    {
        return entry.LastVariant == TimberbornBeaverFieldBehaviorVariant.Smoke
            ? Math.Max(0, entry.ConsecutiveExposedSamples - Options.SmokeRecoveryDecaySamples)
            : 0;
    }

    private TimberbornBeaverFieldBehaviorAction SelectAction(
        TimberbornBeaverFieldBehaviorDecision decision,
        int exposedSamples)
    {
        return decision.Variant == TimberbornBeaverFieldBehaviorVariant.Smoke &&
            IsSmokeCoughing(exposedSamples)
            ? TimberbornBeaverFieldBehaviorAction.CoughingSafeNoOp
            : TimberbornBeaverFieldBehaviorAction.SafeNoOp;
    }

    private bool IsSmokeCoughing(int exposedSamples)
    {
        return exposedSamples >= Options.SmokeCoughingThresholdSamples;
    }

    private static bool IsNoOpAction(TimberbornBeaverFieldBehaviorAction action)
    {
        return action is TimberbornBeaverFieldBehaviorAction.SafeNoOp or
            TimberbornBeaverFieldBehaviorAction.CoughingSafeNoOp;
    }

    private static TimberbornBeaverFieldBehaviorDecision CreateDecision(
        TimberbornBeaverFieldExposureClassification classification,
        uint? tick)
    {
        return new TimberbornBeaverFieldBehaviorDecision(
            classification.BeaverId,
            classification.X,
            classification.Y,
            classification.Z,
            SelectVariant(classification),
            TimberbornBeaverFieldBehaviorAction.SafeNoOp,
            classification.RespiratoryExposureCells,
            classification.BurnExposureCells,
            classification.ContaminatedSmokeCells,
            classification.ToxicExposureCells,
            classification.SteamCells,
            classification.TaintedAftermathCells,
            tick);
    }

    private static TimberbornBeaverFieldBehaviorVariant SelectVariant(
        TimberbornBeaverFieldExposureClassification classification)
    {
        if (classification.BurnExposureCells > 0)
        {
            return TimberbornBeaverFieldBehaviorVariant.FireHeat;
        }

        if (classification.ToxicExposureCells > 0 ||
            classification.ContaminatedSmokeCells > 0)
        {
            return TimberbornBeaverFieldBehaviorVariant.ToxicSmoke;
        }

        return TimberbornBeaverFieldBehaviorVariant.Smoke;
    }

    private void LogState(uint? tick, string status)
    {
        TimberbornBeaverFieldBehaviorCounters counters = Counters;
        _logSink.Info(
            "wildfire_timberborn_beaver_field_behavior_dispatched " +
            $"status={TimberbornQaCommandBridge.FormatToken(status)} " +
            $"tick={FormatNumber(tick)} " +
            $"tracked_beavers={counters.TrackedBeaverCount} " +
            $"decisions_evaluated={counters.DecisionsEvaluated} " +
            $"smoke_decisions_applied={counters.SmokeDecisionsApplied} " +
            $"toxic_smoke_decisions_applied={counters.ToxicSmokeDecisionsApplied} " +
            $"fire_heat_decisions_applied={counters.FireHeatDecisionsApplied} " +
            $"noop_decisions_applied={counters.NoOpDecisionsApplied} " +
            $"decisions_skipped_cooldown={counters.DecisionsSkippedCooldown} " +
            $"decisions_skipped_batch={counters.DecisionsSkippedBatch} " +
            $"skipped_no_safe_api={counters.SkippedNoSafeApi} " +
            $"failed_decisions={counters.FailedDecisions} " +
            $"recovery_actions={counters.RecoveryActions} " +
            $"smoke_exposed_samples={counters.SmokeExposedSamples} " +
            $"smoke_exposure_accumulated_samples={counters.SmokeExposureAccumulatedSamples} " +
            $"smoke_coughing_entered={counters.SmokeCoughingEntered} " +
            $"smoke_coughing_recovered={counters.SmokeCoughingRecovered} " +
            $"smoke_recovery_decays={counters.SmokeRecoveryDecays} " +
            $"smoke_choking_candidates={counters.SmokeChokingCandidates} " +
            $"smoke_choking_skipped_unsafe_api={counters.SmokeChokingSkippedUnsafeApi} " +
            $"smoke_death_candidates={counters.SmokeDeathCandidates} " +
            $"smoke_death_skipped_unsafe_api={counters.SmokeDeathSkippedUnsafeApi} " +
            $"persistence_saves={counters.PersistenceSaveCount} " +
            $"persistence_loads={counters.PersistenceLoadCount}");
    }

    private static string FormatNumber(uint? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }
}
