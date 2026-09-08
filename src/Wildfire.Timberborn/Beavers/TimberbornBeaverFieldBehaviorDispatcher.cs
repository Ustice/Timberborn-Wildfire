namespace Wildfire.Timberborn.Beavers;

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
    private int _failedDecisions;
    private int _unsupportedDecisions;
    private int _recoveryActions;
    private int _smokeExposedSamples;
    private int _smokeExposureAccumulatedSamples;
    private int _smokeCoughingEntered;
    private int _smokeCoughingRecovered;
    private int _smokeCoughingSlowdownsApplied;
    private int _smokeCoughingSlowdownsRecovered;
    private int _smokeRecoveryDecays;
    private int _smokeChokingSlowdownsApplied;
    private int _smokeChokingSlowdownsRecovered;
    private int _toxicSmokeExposedBeavers;
    private int _toxicSmokeExposureAccumulatedSamples;
    private int _toxicSmokeRecoveryDecays;
    private int _fireHeatExposedBeavers;
    private int _fireHeatActiveFlameContacts;
    private int _fireHeatRecoveryDecays;
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
        FailedDecisions: _failedDecisions,
        UnsupportedDecisions: _unsupportedDecisions,
        RecoveryActions: _recoveryActions,
        SmokeExposedSamples: _smokeExposedSamples,
        SmokeExposureAccumulatedSamples: _smokeExposureAccumulatedSamples,
        SmokeCoughingEntered: _smokeCoughingEntered,
        SmokeCoughingRecovered: _smokeCoughingRecovered,
        SmokeCoughingSlowdownsApplied: _smokeCoughingSlowdownsApplied,
        SmokeCoughingSlowdownsRecovered: _smokeCoughingSlowdownsRecovered,
        SmokeRecoveryDecays: _smokeRecoveryDecays,
        SmokeChokingSlowdownsApplied: _smokeChokingSlowdownsApplied,
        SmokeChokingSlowdownsRecovered: _smokeChokingSlowdownsRecovered,
        ToxicSmokeExposedBeavers: _toxicSmokeExposedBeavers,
        ToxicSmokeExposureAccumulatedSamples: _toxicSmokeExposureAccumulatedSamples,
        ToxicSmokeRecoveryDecays: _toxicSmokeRecoveryDecays,
        FireHeatExposedBeavers: _fireHeatExposedBeavers,
        FireHeatActiveFlameContacts: _fireHeatActiveFlameContacts,
        FireHeatRecoveryDecays: _fireHeatRecoveryDecays,
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
                (entry.IsExposed ||
                    entry.ConsecutiveExposedSamples > 0 ||
                    entry.ConsecutiveFireHeatExposedSamples > 0))
            .ToArray()
            .ToList()
            .ForEach(entry => Recover(entry, tick));

        // Existing successful state provides fair order across advancing ticks and saves.
        // Unsupported and cooling actors never spend the supported-application budget.
        var exposedClassifications = classifications
            .Where(static classification => classification.HasExposure)
            .OrderBy(classification => _statesByBeaverId.ContainsKey(classification.BeaverId) ? 1 : 0)
            .ThenBy(classification => _statesByBeaverId.TryGetValue(classification.BeaverId, out var state) ? state.LastDecisionTick : 0)
            .ThenBy(static classification => classification.BeaverId, StringComparer.Ordinal)
            .ToArray();
        int applied = 0;
        foreach (var classification in exposedClassifications)
            if (ApplyDecision(CreateDecision(classification, tick), tick, applied < Options.MaxDecisionsPerDispatch)) applied++;

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
        _failedDecisions = 0;
        _unsupportedDecisions = 0;
        _recoveryActions = 0;
        _smokeExposedSamples = 0;
        _smokeExposureAccumulatedSamples = 0;
        _smokeCoughingEntered = 0;
        _smokeCoughingRecovered = 0;
        _smokeCoughingSlowdownsApplied = 0;
        _smokeCoughingSlowdownsRecovered = 0;
        _smokeRecoveryDecays = 0;
        _smokeChokingSlowdownsApplied = 0;
        _smokeChokingSlowdownsRecovered = 0;
        _toxicSmokeExposedBeavers = 0;
        _toxicSmokeExposureAccumulatedSamples = 0;
        _toxicSmokeRecoveryDecays = 0;
        _fireHeatExposedBeavers = 0;
        _fireHeatActiveFlameContacts = 0;
        _fireHeatRecoveryDecays = 0;
        _lastDecisionTick = null;
        _actuator.Clear();
    }

    private bool ApplyDecision(TimberbornBeaverFieldBehaviorDecision decision, uint? tick, bool budgetAvailable)
    {
        _decisionsEvaluated++;
        _smokeExposedSamples += HasSmokeReactionExposure(decision) ? 1 : 0;
        CountFireHeatObservation(decision);
        if (IsCoolingDown(decision.BeaverId, tick))
        {
            _decisionsSkippedCooldown++;
            return false;
        }
        if (!budgetAvailable)
        {
            _decisionsSkippedBatch++;
            return false;
        }

        bool hasPreviousState = _statesByBeaverId.TryGetValue(
            decision.BeaverId,
            out TimberbornBeaverFieldBehaviorStateEntry previous);
        int previousExposedSamples = hasPreviousState ? previous.ConsecutiveExposedSamples : 0;
        int previousFireHeatExposedSamples = hasPreviousState ? previous.ConsecutiveFireHeatExposedSamples : 0;
        int exposedSamples = HasSmokeReactionExposure(decision)
            ? previousExposedSamples + SmokeExposureSampleWeight(decision)
            : Math.Max(0, previousExposedSamples - Options.SmokeRecoveryDecaySamples);
        int fireHeatExposedSamples = decision.Variant == TimberbornBeaverFieldBehaviorVariant.FireHeat
            ? previousFireHeatExposedSamples + 1
            : Math.Max(0, previousFireHeatExposedSamples - Options.FireHeatRecoveryDecaySamples);
        TimberbornBeaverFieldBehaviorDecision progressedDecision = decision with
        {
            Action = SelectAction(
                decision,
                exposedSamples,
                fireHeatExposedSamples),
        };

        TimberbornBeaverFieldBehaviorActuatorResult result = _actuator.Apply(progressedDecision);
        if (result.Status == TimberbornBeaverFieldBehaviorActuatorStatus.Unsupported)
        {
            _unsupportedDecisions++;
            return false;
        }
        if (result.Status != TimberbornBeaverFieldBehaviorActuatorStatus.Applied)
        {
            _failedDecisions++;
            throw new TimberbornBeaverFieldDeliveryException(progressedDecision.BeaverId,
                new InvalidOperationException($"Actuator returned failed: {result.Reason}."));
        }

        try
        {
            _statesByBeaverId[decision.BeaverId] = new TimberbornBeaverFieldBehaviorStateEntry(
                TimberbornBeaverFieldBehaviorStateEntry.CurrentPersistenceVersion,
                progressedDecision.BeaverId,
                progressedDecision.Variant,
                progressedDecision.Action,
                tick ?? 0,
                exposedSamples,
                fireHeatExposedSamples,
                IsExposed: true);
            _lastDecisionTick = tick;
            CountAppliedVariant(progressedDecision);
            CountSmokeProgression(progressedDecision, previousExposedSamples, exposedSamples);
        }
        catch (Exception exception)
        {
            throw new TimberbornBeaverFieldDeliveryException(decision.BeaverId, exception);
        }
        return true;
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
        if (result.Status == TimberbornBeaverFieldBehaviorActuatorStatus.Unsupported)
        {
            _unsupportedDecisions++;
            return;
        }
        if (result.Status != TimberbornBeaverFieldBehaviorActuatorStatus.Applied)
        {
            _failedDecisions++;
            throw new TimberbornBeaverFieldDeliveryException(entry.BeaverId,
                new InvalidOperationException($"Recovery actuator returned failed: {result.Reason}."));
        }

        try
        {
            _statesByBeaverId[entry.BeaverId] = entry with
            {
                IsExposed = false,
                ConsecutiveExposedSamples = RecoverySmokeExposedSamples(entry.ConsecutiveExposedSamples),
                ConsecutiveFireHeatExposedSamples =
                    RecoveryFireHeatExposedSamples(entry.ConsecutiveFireHeatExposedSamples),
            };
            _recoveryActions++;
            CountSmokeRecovery(entry, _statesByBeaverId[entry.BeaverId].ConsecutiveExposedSamples);
            CountFireHeatRecovery(entry, _statesByBeaverId[entry.BeaverId].ConsecutiveFireHeatExposedSamples);
        }
        catch (Exception exception)
        {
            throw new TimberbornBeaverFieldDeliveryException(entry.BeaverId, exception);
        }
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
        if (!HasSmokeReactionExposure(decision))
        {
            return;
        }

        _smokeExposureAccumulatedSamples += SmokeExposureSampleWeight(decision);
        if (IsToxicSmokeExposure(decision))
        {
            _toxicSmokeExposedBeavers++;
            _toxicSmokeExposureAccumulatedSamples += SmokeExposureSampleWeight(decision);
        }

        _smokeCoughingEntered += !IsSmokeCoughing(previousExposedSamples) && IsSmokeCoughing(exposedSamples) ? 1 : 0;
        _smokeCoughingSlowdownsApplied += decision.Action == TimberbornBeaverFieldBehaviorAction.CoughingWorkSlowdown
            ? 1
            : 0;
        _smokeChokingSlowdownsApplied += decision.Action == TimberbornBeaverFieldBehaviorAction.ChokingWorkSlowdown
            ? 1
            : 0;
    }

    private void CountSmokeRecovery(TimberbornBeaverFieldBehaviorStateEntry entry, int recoveredExposedSamples)
    {
        if (!IsSmokeReactionVariant(entry.LastVariant))
        {
            return;
        }

        _smokeRecoveryDecays += recoveredExposedSamples < entry.ConsecutiveExposedSamples ? 1 : 0;
        _toxicSmokeRecoveryDecays +=
            entry.LastVariant == TimberbornBeaverFieldBehaviorVariant.ToxicSmoke &&
            recoveredExposedSamples < entry.ConsecutiveExposedSamples
                ? 1
                : 0;
        _smokeCoughingSlowdownsRecovered += entry.LastAction == TimberbornBeaverFieldBehaviorAction.CoughingWorkSlowdown
            ? 1
            : 0;
        _smokeChokingSlowdownsRecovered += entry.LastAction == TimberbornBeaverFieldBehaviorAction.ChokingWorkSlowdown
            ? 1
            : 0;
        _smokeCoughingRecovered +=
            IsSmokeCoughing(entry.ConsecutiveExposedSamples) && !IsSmokeCoughing(recoveredExposedSamples)
                ? 1
                : 0;
    }

    private void CountFireHeatObservation(TimberbornBeaverFieldBehaviorDecision decision)
    {
        if (decision.Variant != TimberbornBeaverFieldBehaviorVariant.FireHeat)
        {
            return;
        }

        _fireHeatExposedBeavers++;
        bool activeFlameContact = decision.MaxFire >= Options.ActiveFlameContactThreshold;
        _fireHeatActiveFlameContacts += activeFlameContact ? 1 : 0;
    }

    private void CountFireHeatRecovery(TimberbornBeaverFieldBehaviorStateEntry entry, int recoveredExposedSamples)
    {
        if (entry.LastVariant != TimberbornBeaverFieldBehaviorVariant.FireHeat)
        {
            return;
        }

        _fireHeatRecoveryDecays += recoveredExposedSamples < entry.ConsecutiveFireHeatExposedSamples ? 1 : 0;
    }

    private int RecoverySmokeExposedSamples(int exposedSamples)
    {
        return Math.Max(0, exposedSamples - Options.SmokeRecoveryDecaySamples);
    }

    private int RecoveryFireHeatExposedSamples(int exposedSamples)
    {
        return Math.Max(0, exposedSamples - Options.FireHeatRecoveryDecaySamples);
    }

    private TimberbornBeaverFieldBehaviorAction SelectAction(
        TimberbornBeaverFieldBehaviorDecision decision,
        int exposedSamples,
        int fireHeatExposedSamples)
    {
        if (decision.Variant == TimberbornBeaverFieldBehaviorVariant.FireHeat)
        {
            return TimberbornBeaverFieldBehaviorAction.FireHeatExposureAttempt;
        }

        if (!HasSmokeReactionExposure(decision))
        {
            return TimberbornBeaverFieldBehaviorAction.NoOp;
        }

        if (exposedSamples >= Options.SmokeChokingThresholdSamples)
        {
            return TimberbornBeaverFieldBehaviorAction.ChokingWorkSlowdown;
        }

        return IsSmokeCoughing(exposedSamples)
            ? TimberbornBeaverFieldBehaviorAction.CoughingWorkSlowdown
            : TimberbornBeaverFieldBehaviorAction.NoOp;
    }

    private bool IsSmokeCoughing(int exposedSamples)
    {
        return exposedSamples >= Options.SmokeCoughingThresholdSamples;
    }

    private static bool IsSmokeReactionVariant(TimberbornBeaverFieldBehaviorVariant variant)
    {
        return variant is TimberbornBeaverFieldBehaviorVariant.Smoke or
            TimberbornBeaverFieldBehaviorVariant.ToxicSmoke;
    }

    private static bool HasSmokeReactionExposure(TimberbornBeaverFieldBehaviorDecision decision)
    {
        return IsSmokeReactionVariant(decision.Variant) ||
            decision.RespiratoryExposureCells > 0 ||
            decision.ContaminatedSmokeCells > 0 ||
            decision.ToxicExposureCells > 0 ||
            decision.SteamCells > 0;
    }

    private int SmokeExposureSampleWeight(TimberbornBeaverFieldBehaviorDecision decision)
    {
        return IsToxicSmokeExposure(decision)
            ? Options.ToxicSmokeExposureSampleWeight
            : 1;
    }

    private static bool IsToxicSmokeExposure(TimberbornBeaverFieldBehaviorDecision decision)
    {
        return decision.Variant == TimberbornBeaverFieldBehaviorVariant.ToxicSmoke ||
            decision.ContaminatedSmokeCells > 0 ||
            decision.ToxicExposureCells > 0;
    }

    private static bool IsNoOpAction(TimberbornBeaverFieldBehaviorAction action)
    {
        return action == TimberbornBeaverFieldBehaviorAction.NoOp;
    }

    private static bool IsSmokeReactionAction(TimberbornBeaverFieldBehaviorAction action)
    {
        return action is TimberbornBeaverFieldBehaviorAction.CoughingWorkSlowdown or
            TimberbornBeaverFieldBehaviorAction.ChokingWorkSlowdown;
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
            TimberbornBeaverFieldBehaviorAction.NoOp,
            classification.RespiratoryExposureCells,
            classification.BurnExposureCells,
            classification.ContaminatedSmokeCells,
            classification.ToxicExposureCells,
            classification.SteamCells,
            classification.TaintedAftermathCells,
            classification.MaxFire,
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
            $"failed_decisions={counters.FailedDecisions} " +
            $"unsupported_decisions={counters.UnsupportedDecisions} " +
            $"recovery_actions={counters.RecoveryActions} " +
            $"smoke_exposed_samples={counters.SmokeExposedSamples} " +
            $"smoke_exposure_accumulated_samples={counters.SmokeExposureAccumulatedSamples} " +
            $"smoke_coughing_entered={counters.SmokeCoughingEntered} " +
            $"smoke_coughing_recovered={counters.SmokeCoughingRecovered} " +
            $"smoke_coughing_slowdowns_applied={counters.SmokeCoughingSlowdownsApplied} " +
            $"smoke_coughing_slowdowns_recovered={counters.SmokeCoughingSlowdownsRecovered} " +
            $"smoke_recovery_decays={counters.SmokeRecoveryDecays} " +
            $"smoke_choking_slowdowns_applied={counters.SmokeChokingSlowdownsApplied} " +
            $"smoke_choking_slowdowns_recovered={counters.SmokeChokingSlowdownsRecovered} " +
            $"toxic_smoke_exposed_beavers={counters.ToxicSmokeExposedBeavers} " +
            $"toxic_smoke_exposure_accumulated_samples={counters.ToxicSmokeExposureAccumulatedSamples} " +
            $"toxic_smoke_recovery_decays={counters.ToxicSmokeRecoveryDecays} " +
            $"fire_heat_exposed_beavers={counters.FireHeatExposedBeavers} " +
            $"fire_heat_active_flame_contacts={counters.FireHeatActiveFlameContacts} " +
            "fire_heat_contact_basis=nearby_visual_proxy " +
            $"fire_heat_recovery_decays={counters.FireHeatRecoveryDecays} " +
            $"persistence_saves={counters.PersistenceSaveCount} " +
            $"persistence_loads={counters.PersistenceLoadCount}");
    }

    private static string FormatNumber(uint? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }
}
