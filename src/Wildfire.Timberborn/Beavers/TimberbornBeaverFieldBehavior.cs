using System.Reflection;
using Timberborn.Beavers;
using Timberborn.EntitySystem;
using Timberborn.StatusSystem;
using Timberborn.WorkSystem;

namespace Wildfire.Timberborn.Beavers;

public sealed class TimberbornBeaverFieldBehaviorOptions
{
    public static readonly TimberbornBeaverFieldBehaviorOptions Default = new();

    public TimberbornBeaverFieldBehaviorOptions(
        uint DecisionCooldownTicks = 3,
        int MaxDecisionsPerDispatch = 64,
        int SmokeCoughingThresholdSamples = 3,
        int SmokeChokingCandidateThresholdSamples = 8,
        int SmokeDeathCandidateThresholdSamples = 16,
        int SmokeRecoveryDecaySamples = 1,
        int FireHeatSingedThresholdSamples = 2,
        int FireHeatBurnedThresholdSamples = 5,
        int FireHeatDeathCandidateThresholdSamples = 12,
        int FireHeatRecoveryDecaySamples = 1,
        float ActiveFlameContactThreshold = 0.75f)
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

        if (FireHeatSingedThresholdSamples <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(FireHeatSingedThresholdSamples),
                FireHeatSingedThresholdSamples,
                "The fire/heat singed threshold must be positive.");
        }

        if (FireHeatBurnedThresholdSamples < FireHeatSingedThresholdSamples)
        {
            throw new ArgumentOutOfRangeException(
                nameof(FireHeatBurnedThresholdSamples),
                FireHeatBurnedThresholdSamples,
                "The fire/heat burned threshold must not be below the singed threshold.");
        }

        if (FireHeatDeathCandidateThresholdSamples < FireHeatBurnedThresholdSamples)
        {
            throw new ArgumentOutOfRangeException(
                nameof(FireHeatDeathCandidateThresholdSamples),
                FireHeatDeathCandidateThresholdSamples,
                "The fire/heat death candidate threshold must not be below the burned threshold.");
        }

        if (FireHeatRecoveryDecaySamples <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(FireHeatRecoveryDecaySamples),
                FireHeatRecoveryDecaySamples,
                "The fire/heat recovery decay must be positive.");
        }

        if (ActiveFlameContactThreshold <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ActiveFlameContactThreshold),
                ActiveFlameContactThreshold,
                "The active flame contact threshold must be positive.");
        }

        this.DecisionCooldownTicks = DecisionCooldownTicks;
        this.MaxDecisionsPerDispatch = MaxDecisionsPerDispatch;
        this.SmokeCoughingThresholdSamples = SmokeCoughingThresholdSamples;
        this.SmokeChokingCandidateThresholdSamples = SmokeChokingCandidateThresholdSamples;
        this.SmokeDeathCandidateThresholdSamples = SmokeDeathCandidateThresholdSamples;
        this.SmokeRecoveryDecaySamples = SmokeRecoveryDecaySamples;
        this.FireHeatSingedThresholdSamples = FireHeatSingedThresholdSamples;
        this.FireHeatBurnedThresholdSamples = FireHeatBurnedThresholdSamples;
        this.FireHeatDeathCandidateThresholdSamples = FireHeatDeathCandidateThresholdSamples;
        this.FireHeatRecoveryDecaySamples = FireHeatRecoveryDecaySamples;
        this.ActiveFlameContactThreshold = ActiveFlameContactThreshold;
    }

    public uint DecisionCooldownTicks { get; }

    public int MaxDecisionsPerDispatch { get; }

    public int SmokeCoughingThresholdSamples { get; }

    public int SmokeChokingCandidateThresholdSamples { get; }

    public int SmokeDeathCandidateThresholdSamples { get; }

    public int SmokeRecoveryDecaySamples { get; }

    public int FireHeatSingedThresholdSamples { get; }

    public int FireHeatBurnedThresholdSamples { get; }

    public int FireHeatDeathCandidateThresholdSamples { get; }

    public int FireHeatRecoveryDecaySamples { get; }

    public float ActiveFlameContactThreshold { get; }
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
    CoughingWorkSlowdown = 3,
    ChokingWorkSlowdown = 4,
    FireHeatAvoidanceSafeNoOp = 5,
    FireHeatSingedSafeNoOp = 6,
    FireHeatBurnedSafeNoOp = 7,
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
    float MaxFire,
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

public interface ITimberbornBeaverWorkerSpeedAdapter
{
    TimberbornBeaverWorkerSpeedResult ApplySmokeReaction(
        string beaverId,
        TimberbornBeaverFieldBehaviorAction action,
        float multiplier);

    TimberbornBeaverWorkerSpeedResult RecoverSmokeReaction(string beaverId);

    void Clear();
}

public readonly record struct TimberbornBeaverWorkerSpeedResult(
    TimberbornBeaverFieldBehaviorActuatorStatus Status,
    string Reason)
{
    public static readonly TimberbornBeaverWorkerSpeedResult Applied = new(
        TimberbornBeaverFieldBehaviorActuatorStatus.Applied,
        "applied");

    public static TimberbornBeaverWorkerSpeedResult Skipped(string reason)
    {
        return new TimberbornBeaverWorkerSpeedResult(
            TimberbornBeaverFieldBehaviorActuatorStatus.SkippedNoSafeApi,
            string.IsNullOrWhiteSpace(reason) ? "no_safe_api" : reason);
    }

    public static TimberbornBeaverWorkerSpeedResult Failed(string reason)
    {
        return new TimberbornBeaverWorkerSpeedResult(
            TimberbornBeaverFieldBehaviorActuatorStatus.Failed,
            string.IsNullOrWhiteSpace(reason) ? "failed" : reason);
    }
}

public sealed class TimberbornWorkerSpeedBeaverFieldBehaviorActuator : ITimberbornBeaverFieldBehaviorActuator
{
    public const float CoughingWorkingSpeedMultiplier = 0.5f;
    public const float ChokingWorkingSpeedMultiplier = 0.25f;

    private readonly ITimberbornBeaverWorkerSpeedAdapter _workerSpeedAdapter;

    public TimberbornWorkerSpeedBeaverFieldBehaviorActuator(ITimberbornBeaverWorkerSpeedAdapter workerSpeedAdapter)
    {
        _workerSpeedAdapter = workerSpeedAdapter ?? throw new ArgumentNullException(nameof(workerSpeedAdapter));
    }

    public TimberbornBeaverFieldBehaviorActuatorResult Apply(TimberbornBeaverFieldBehaviorDecision decision)
    {
        return IsSmokeReactionAction(decision.Action)
            ? ToActuatorResult(_workerSpeedAdapter.ApplySmokeReaction(
                decision.BeaverId,
                decision.Action,
                SmokeReactionWorkingSpeedMultiplier(decision.Action)))
            : TimberbornBeaverFieldBehaviorActuatorResult.Applied;
    }

    public TimberbornBeaverFieldBehaviorActuatorResult Recover(
        TimberbornBeaverFieldBehaviorStateEntry entry,
        uint? tick)
    {
        return IsSmokeReactionAction(entry.LastAction)
            ? ToActuatorResult(_workerSpeedAdapter.RecoverSmokeReaction(entry.BeaverId))
            : TimberbornBeaverFieldBehaviorActuatorResult.Applied;
    }

    public void Clear()
    {
        _workerSpeedAdapter.Clear();
    }

    private static TimberbornBeaverFieldBehaviorActuatorResult ToActuatorResult(
        TimberbornBeaverWorkerSpeedResult result)
    {
        return new TimberbornBeaverFieldBehaviorActuatorResult(result.Status, result.Reason);
    }

    private static float SmokeReactionWorkingSpeedMultiplier(TimberbornBeaverFieldBehaviorAction action)
    {
        return action == TimberbornBeaverFieldBehaviorAction.ChokingWorkSlowdown
            ? ChokingWorkingSpeedMultiplier
            : CoughingWorkingSpeedMultiplier;
    }

    private static bool IsSmokeReactionAction(TimberbornBeaverFieldBehaviorAction action)
    {
        return action is TimberbornBeaverFieldBehaviorAction.CoughingWorkSlowdown or
            TimberbornBeaverFieldBehaviorAction.ChokingWorkSlowdown;
    }
}

public sealed class TimberbornEntityRegistryBeaverWorkerSpeedAdapter : ITimberbornBeaverWorkerSpeedAdapter
{
    private const float RestoreTolerance = 0.001f;
    private static readonly PropertyInfo? WorkerSpeedMultiplierProperty =
        typeof(Worker).GetProperty(
            nameof(Worker.WorkingSpeedMultiplier),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private readonly EntityRegistry _entityRegistry;
    private readonly Dictionary<string, float> _originalWorkingSpeedMultiplierByBeaverId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TimberbornBeaverSmokeStatusToggles> _statusTogglesByBeaverId =
        new(StringComparer.Ordinal);

    public TimberbornEntityRegistryBeaverWorkerSpeedAdapter(EntityRegistry entityRegistry)
    {
        _entityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
    }

    public TimberbornBeaverWorkerSpeedResult ApplySmokeReaction(
        string beaverId,
        TimberbornBeaverFieldBehaviorAction action,
        float multiplier)
    {
        if (TryGetEntity(beaverId, out EntityComponent? entity, out string unavailableReason) &&
            entity is not null)
        {
            TryApplyStatus(entity, beaverId, action);
            TryApplyWorkingSpeed(entity, beaverId, multiplier);
            return TimberbornBeaverWorkerSpeedResult.Applied;
        }

        return TimberbornBeaverWorkerSpeedResult.Skipped(unavailableReason);
    }

    public TimberbornBeaverWorkerSpeedResult RecoverSmokeReaction(string beaverId)
    {
        if (!TryGetEntity(beaverId, out EntityComponent? entity, out string unavailableReason) ||
            entity is null)
        {
            _originalWorkingSpeedMultiplierByBeaverId.Remove(beaverId);
            DeactivateStatus(beaverId);
            return TimberbornBeaverWorkerSpeedResult.Skipped(unavailableReason);
        }

        DeactivateStatus(beaverId);
        if (!entity.TryGetComponent(out Worker worker))
        {
            _originalWorkingSpeedMultiplierByBeaverId.Remove(beaverId);
            return TimberbornBeaverWorkerSpeedResult.Applied;
        }

        float originalMultiplier = _originalWorkingSpeedMultiplierByBeaverId.TryGetValue(
            beaverId,
            out float storedMultiplier)
            ? storedMultiplier
            : 1f;
        float slowedMultiplier = originalMultiplier * TimberbornWorkerSpeedBeaverFieldBehaviorActuator
            .CoughingWorkingSpeedMultiplier;
        if (worker.WorkingSpeedMultiplier <= slowedMultiplier + RestoreTolerance)
        {
            bool restored = TrySetWorkingSpeedMultiplier(worker, originalMultiplier);
            _originalWorkingSpeedMultiplierByBeaverId.Remove(beaverId);
            return restored
                ? TimberbornBeaverWorkerSpeedResult.Applied
                : TimberbornBeaverWorkerSpeedResult.Skipped("worker_speed_setter_unavailable");
        }

        _originalWorkingSpeedMultiplierByBeaverId.Remove(beaverId);
        return TimberbornBeaverWorkerSpeedResult.Applied;
    }

    public void Clear()
    {
        _originalWorkingSpeedMultiplierByBeaverId.Clear();
        _statusTogglesByBeaverId.Values
            .ToList()
            .ForEach(static toggles => toggles.DeactivateAll());
        _statusTogglesByBeaverId.Clear();
    }

    private bool TryGetEntity(string beaverId, out EntityComponent? entity, out string unavailableReason)
    {
        entity = null;
        unavailableReason = "none";
        if (!Guid.TryParse(beaverId, out Guid entityId))
        {
            unavailableReason = "invalid_beaver_id";
            return false;
        }

        try
        {
            entity = _entityRegistry.GetEntity(entityId);
            if (!entity.TryGetComponent(out Beaver _))
            {
                unavailableReason = "entity_not_beaver";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            unavailableReason = "worker_api_exception:" + exception.GetType().Name;
            return false;
        }
    }

    private void TryApplyWorkingSpeed(EntityComponent entity, string beaverId, float multiplier)
    {
        if (!entity.TryGetComponent(out Worker worker))
        {
            return;
        }

        float originalMultiplier = _originalWorkingSpeedMultiplierByBeaverId.TryGetValue(
            beaverId,
            out float storedMultiplier)
            ? storedMultiplier
            : worker.WorkingSpeedMultiplier;
        _originalWorkingSpeedMultiplierByBeaverId.TryAdd(beaverId, originalMultiplier);
        TrySetWorkingSpeedMultiplier(
            worker,
            Math.Min(worker.WorkingSpeedMultiplier, originalMultiplier * multiplier));
    }

    private static bool TrySetWorkingSpeedMultiplier(Worker worker, float multiplier)
    {
        try
        {
            MethodInfo? setter = WorkerSpeedMultiplierProperty?.GetSetMethod(nonPublic: true);
            if (setter is null)
            {
                return false;
            }

            setter.Invoke(worker, new object[] { multiplier });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void TryApplyStatus(
        EntityComponent entity,
        string beaverId,
        TimberbornBeaverFieldBehaviorAction action)
    {
        if (!entity.TryGetComponent(out StatusSubject statusSubject))
        {
            return;
        }

        try
        {
            TimberbornBeaverSmokeStatusToggles toggles = GetOrCreateStatusToggles(beaverId, statusSubject);
            toggles.Apply(action);
        }
        catch
        {
            _statusTogglesByBeaverId.Remove(beaverId);
        }
    }

    private TimberbornBeaverSmokeStatusToggles GetOrCreateStatusToggles(
        string beaverId,
        StatusSubject statusSubject)
    {
        if (_statusTogglesByBeaverId.TryGetValue(beaverId, out TimberbornBeaverSmokeStatusToggles toggles))
        {
            return toggles;
        }

        TimberbornBeaverSmokeStatusToggles createdToggles = TimberbornBeaverSmokeStatusToggles.Create(statusSubject);
        _statusTogglesByBeaverId[beaverId] = createdToggles;
        return createdToggles;
    }

    private void DeactivateStatus(string beaverId)
    {
        if (!_statusTogglesByBeaverId.TryGetValue(beaverId, out TimberbornBeaverSmokeStatusToggles toggles))
        {
            return;
        }

        toggles.DeactivateAll();
    }

    private sealed class TimberbornBeaverSmokeStatusToggles
    {
        private const string CoughingStatusIcon = "WildfireCoughingStatus";
        private const string ChokingStatusIcon = "WildfireChokingStatus";

        private readonly StatusToggle _coughingStatus;
        private readonly StatusToggle _chokingStatus;

        private TimberbornBeaverSmokeStatusToggles(StatusToggle coughingStatus, StatusToggle chokingStatus)
        {
            _coughingStatus = coughingStatus;
            _chokingStatus = chokingStatus;
        }

        public static TimberbornBeaverSmokeStatusToggles Create(StatusSubject statusSubject)
        {
            StatusToggle coughingStatus = StatusToggle.CreateNormalStatusWithFloatingIcon(
                CoughingStatusIcon,
                "Coughing from smoke",
                delayInHours: 0f);
            StatusToggle chokingStatus = StatusToggle.CreatePriorityStatusWithFloatingIcon(
                ChokingStatusIcon,
                "Choking on smoke",
                delayInHours: 0f);
            statusSubject.RegisterStatus(coughingStatus);
            statusSubject.RegisterStatus(chokingStatus);
            return new TimberbornBeaverSmokeStatusToggles(coughingStatus, chokingStatus);
        }

        public void Apply(TimberbornBeaverFieldBehaviorAction action)
        {
            if (action == TimberbornBeaverFieldBehaviorAction.ChokingWorkSlowdown)
            {
                _coughingStatus.Deactivate();
                _chokingStatus.Activate();
                return;
            }

            _chokingStatus.Deactivate();
            _coughingStatus.Activate();
        }

        public void DeactivateAll()
        {
            _coughingStatus.Deactivate();
            _chokingStatus.Deactivate();
        }
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
    int SmokeCoughingSlowdownsApplied,
    int SmokeCoughingSlowdownsRecovered,
    int SmokeCoughingSlowdownsSkippedNoSafeApi,
    int SmokeRecoveryDecays,
    int SmokeChokingCandidates,
    int SmokeChokingSlowdownsApplied,
    int SmokeChokingSlowdownsRecovered,
    int SmokeChokingSlowdownsSkippedNoSafeApi,
    int SmokeChokingSkippedUnsafeApi,
    int SmokeDeathCandidates,
    int SmokeDeathSkippedUnsafeApi,
    int FireHeatExposedBeavers,
    int FireHeatActiveFlameContacts,
    int FireHeatAvoidanceCandidates,
    int FireHeatAvoidedCells,
    int FireHeatAvoidanceSkippedNoSafeApi,
    int FireHeatInterruptedJobCandidates,
    int FireHeatInterruptedJobs,
    int FireHeatInterruptedJobsSkippedNoSafeApi,
    int FireHeatSingedEntered,
    int FireHeatSingedRecovered,
    int FireHeatSingedSkippedNoSafeApi,
    int FireHeatBurnedEntered,
    int FireHeatBurnedRecovered,
    int FireHeatBurnedSkippedNoSafeApi,
    int FireHeatDeathCandidates,
    int FireHeatDeathSkippedUnsafeApi,
    int FireHeatRecoveryDecays,
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
    TimberbornBeaverFieldBehaviorAction LastAction,
    uint LastDecisionTick,
    int ConsecutiveExposedSamples,
    int ConsecutiveFireHeatExposedSamples,
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
    private int _smokeCoughingSlowdownsApplied;
    private int _smokeCoughingSlowdownsRecovered;
    private int _smokeCoughingSlowdownsSkippedNoSafeApi;
    private int _smokeRecoveryDecays;
    private int _smokeChokingCandidates;
    private int _smokeChokingSlowdownsApplied;
    private int _smokeChokingSlowdownsRecovered;
    private int _smokeChokingSlowdownsSkippedNoSafeApi;
    private int _smokeChokingSkippedUnsafeApi;
    private int _smokeDeathCandidates;
    private int _smokeDeathSkippedUnsafeApi;
    private int _fireHeatExposedBeavers;
    private int _fireHeatActiveFlameContacts;
    private int _fireHeatAvoidanceCandidates;
    private int _fireHeatAvoidedCells;
    private int _fireHeatAvoidanceSkippedNoSafeApi;
    private int _fireHeatInterruptedJobCandidates;
    private int _fireHeatInterruptedJobs;
    private int _fireHeatInterruptedJobsSkippedNoSafeApi;
    private int _fireHeatSingedEntered;
    private int _fireHeatSingedRecovered;
    private int _fireHeatSingedSkippedNoSafeApi;
    private int _fireHeatBurnedEntered;
    private int _fireHeatBurnedRecovered;
    private int _fireHeatBurnedSkippedNoSafeApi;
    private int _fireHeatDeathCandidates;
    private int _fireHeatDeathSkippedUnsafeApi;
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
        SkippedNoSafeApi: _skippedNoSafeApi,
        FailedDecisions: _failedDecisions,
        RecoveryActions: _recoveryActions,
        SmokeExposedSamples: _smokeExposedSamples,
        SmokeExposureAccumulatedSamples: _smokeExposureAccumulatedSamples,
        SmokeCoughingEntered: _smokeCoughingEntered,
        SmokeCoughingRecovered: _smokeCoughingRecovered,
        SmokeCoughingSlowdownsApplied: _smokeCoughingSlowdownsApplied,
        SmokeCoughingSlowdownsRecovered: _smokeCoughingSlowdownsRecovered,
        SmokeCoughingSlowdownsSkippedNoSafeApi: _smokeCoughingSlowdownsSkippedNoSafeApi,
        SmokeRecoveryDecays: _smokeRecoveryDecays,
        SmokeChokingCandidates: _smokeChokingCandidates,
        SmokeChokingSlowdownsApplied: _smokeChokingSlowdownsApplied,
        SmokeChokingSlowdownsRecovered: _smokeChokingSlowdownsRecovered,
        SmokeChokingSlowdownsSkippedNoSafeApi: _smokeChokingSlowdownsSkippedNoSafeApi,
        SmokeChokingSkippedUnsafeApi: _smokeChokingSkippedUnsafeApi,
        SmokeDeathCandidates: _smokeDeathCandidates,
        SmokeDeathSkippedUnsafeApi: _smokeDeathSkippedUnsafeApi,
        FireHeatExposedBeavers: _fireHeatExposedBeavers,
        FireHeatActiveFlameContacts: _fireHeatActiveFlameContacts,
        FireHeatAvoidanceCandidates: _fireHeatAvoidanceCandidates,
        FireHeatAvoidedCells: _fireHeatAvoidedCells,
        FireHeatAvoidanceSkippedNoSafeApi: _fireHeatAvoidanceSkippedNoSafeApi,
        FireHeatInterruptedJobCandidates: _fireHeatInterruptedJobCandidates,
        FireHeatInterruptedJobs: _fireHeatInterruptedJobs,
        FireHeatInterruptedJobsSkippedNoSafeApi: _fireHeatInterruptedJobsSkippedNoSafeApi,
        FireHeatSingedEntered: _fireHeatSingedEntered,
        FireHeatSingedRecovered: _fireHeatSingedRecovered,
        FireHeatSingedSkippedNoSafeApi: _fireHeatSingedSkippedNoSafeApi,
        FireHeatBurnedEntered: _fireHeatBurnedEntered,
        FireHeatBurnedRecovered: _fireHeatBurnedRecovered,
        FireHeatBurnedSkippedNoSafeApi: _fireHeatBurnedSkippedNoSafeApi,
        FireHeatDeathCandidates: _fireHeatDeathCandidates,
        FireHeatDeathSkippedUnsafeApi: _fireHeatDeathSkippedUnsafeApi,
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
        _smokeCoughingSlowdownsApplied = 0;
        _smokeCoughingSlowdownsRecovered = 0;
        _smokeCoughingSlowdownsSkippedNoSafeApi = 0;
        _smokeRecoveryDecays = 0;
        _smokeChokingCandidates = 0;
        _smokeChokingSlowdownsApplied = 0;
        _smokeChokingSlowdownsRecovered = 0;
        _smokeChokingSlowdownsSkippedNoSafeApi = 0;
        _smokeChokingSkippedUnsafeApi = 0;
        _smokeDeathCandidates = 0;
        _smokeDeathSkippedUnsafeApi = 0;
        _fireHeatExposedBeavers = 0;
        _fireHeatActiveFlameContacts = 0;
        _fireHeatAvoidanceCandidates = 0;
        _fireHeatAvoidedCells = 0;
        _fireHeatAvoidanceSkippedNoSafeApi = 0;
        _fireHeatInterruptedJobCandidates = 0;
        _fireHeatInterruptedJobs = 0;
        _fireHeatInterruptedJobsSkippedNoSafeApi = 0;
        _fireHeatSingedEntered = 0;
        _fireHeatSingedRecovered = 0;
        _fireHeatSingedSkippedNoSafeApi = 0;
        _fireHeatBurnedEntered = 0;
        _fireHeatBurnedRecovered = 0;
        _fireHeatBurnedSkippedNoSafeApi = 0;
        _fireHeatDeathCandidates = 0;
        _fireHeatDeathSkippedUnsafeApi = 0;
        _fireHeatRecoveryDecays = 0;
        _lastDecisionTick = null;
        _actuator.Clear();
    }

    private void ApplyDecision(TimberbornBeaverFieldBehaviorDecision decision, uint? tick)
    {
        _decisionsEvaluated++;
        _smokeExposedSamples += HasSmokeReactionExposure(decision) ? 1 : 0;
        if (IsCoolingDown(decision.BeaverId, tick))
        {
            _decisionsSkippedCooldown++;
            return;
        }

        bool hasPreviousState = _statesByBeaverId.TryGetValue(
            decision.BeaverId,
            out TimberbornBeaverFieldBehaviorStateEntry previous);
        int previousExposedSamples = hasPreviousState ? previous.ConsecutiveExposedSamples : 0;
        int previousFireHeatExposedSamples = hasPreviousState ? previous.ConsecutiveFireHeatExposedSamples : 0;
        int exposedSamples = HasSmokeReactionExposure(decision)
            ? previousExposedSamples + 1
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
        if (result.Status == TimberbornBeaverFieldBehaviorActuatorStatus.SkippedNoSafeApi)
        {
            _skippedNoSafeApi++;
            CountSmokeSlowdownSkip(progressedDecision);
            return;
        }

        if (result.Status == TimberbornBeaverFieldBehaviorActuatorStatus.Failed)
        {
            _failedDecisions++;
            return;
        }

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
        CountFireHeatProgression(progressedDecision, previousFireHeatExposedSamples, fireHeatExposedSamples);
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
            CountSmokeSlowdownSkip(entry);
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
            ConsecutiveExposedSamples = RecoverySmokeExposedSamples(entry.ConsecutiveExposedSamples),
            ConsecutiveFireHeatExposedSamples =
                RecoveryFireHeatExposedSamples(entry.ConsecutiveFireHeatExposedSamples),
        };
        _recoveryActions++;
        CountSmokeRecovery(entry, _statesByBeaverId[entry.BeaverId].ConsecutiveExposedSamples);
        CountFireHeatRecovery(entry, _statesByBeaverId[entry.BeaverId].ConsecutiveFireHeatExposedSamples);
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

        _smokeExposureAccumulatedSamples++;
        _smokeCoughingEntered += !IsSmokeCoughing(previousExposedSamples) && IsSmokeCoughing(exposedSamples) ? 1 : 0;
        _smokeCoughingSlowdownsApplied += decision.Action == TimberbornBeaverFieldBehaviorAction.CoughingWorkSlowdown
            ? 1
            : 0;
        _smokeChokingSlowdownsApplied += decision.Action == TimberbornBeaverFieldBehaviorAction.ChokingWorkSlowdown
            ? 1
            : 0;
        bool chokingCandidateEntered = previousExposedSamples < Options.SmokeChokingCandidateThresholdSamples &&
            exposedSamples >= Options.SmokeChokingCandidateThresholdSamples;
        bool deathCandidateEntered = previousExposedSamples < Options.SmokeDeathCandidateThresholdSamples &&
            exposedSamples >= Options.SmokeDeathCandidateThresholdSamples;
        _smokeChokingCandidates += chokingCandidateEntered ? 1 : 0;
        _smokeDeathCandidates += deathCandidateEntered ? 1 : 0;
        _smokeDeathSkippedUnsafeApi += deathCandidateEntered ? 1 : 0;
        _skippedNoSafeApi += deathCandidateEntered
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

    private void CountFireHeatProgression(
        TimberbornBeaverFieldBehaviorDecision decision,
        int previousExposedSamples,
        int exposedSamples)
    {
        if (decision.Variant != TimberbornBeaverFieldBehaviorVariant.FireHeat)
        {
            return;
        }

        _fireHeatExposedBeavers++;
        bool activeFlameContact = decision.MaxFire >= Options.ActiveFlameContactThreshold;
        _fireHeatActiveFlameContacts += activeFlameContact ? 1 : 0;
        _fireHeatAvoidanceCandidates += decision.BurnExposureCells;
        _fireHeatAvoidanceSkippedNoSafeApi += decision.BurnExposureCells;
        _fireHeatInterruptedJobCandidates++;
        _fireHeatInterruptedJobsSkippedNoSafeApi++;
        _skippedNoSafeApi += decision.BurnExposureCells + 1;

        bool singedEntered = previousExposedSamples < Options.FireHeatSingedThresholdSamples &&
            exposedSamples >= Options.FireHeatSingedThresholdSamples;
        bool burnedEntered = previousExposedSamples < Options.FireHeatBurnedThresholdSamples &&
            exposedSamples >= Options.FireHeatBurnedThresholdSamples;
        bool deathCandidateEntered = previousExposedSamples < Options.FireHeatDeathCandidateThresholdSamples &&
            exposedSamples >= Options.FireHeatDeathCandidateThresholdSamples;

        _fireHeatSingedEntered += singedEntered ? 1 : 0;
        _fireHeatSingedSkippedNoSafeApi += singedEntered ? 1 : 0;
        _fireHeatBurnedEntered += burnedEntered ? 1 : 0;
        _fireHeatBurnedSkippedNoSafeApi += burnedEntered ? 1 : 0;
        _fireHeatDeathCandidates += deathCandidateEntered ? 1 : 0;
        _fireHeatDeathSkippedUnsafeApi += deathCandidateEntered ? 1 : 0;
        _skippedNoSafeApi += singedEntered ? 1 : 0;
        _skippedNoSafeApi += burnedEntered ? 1 : 0;
        _skippedNoSafeApi += deathCandidateEntered ? 1 : 0;
    }

    private void CountFireHeatRecovery(TimberbornBeaverFieldBehaviorStateEntry entry, int recoveredExposedSamples)
    {
        if (entry.LastVariant != TimberbornBeaverFieldBehaviorVariant.FireHeat)
        {
            return;
        }

        _fireHeatRecoveryDecays += recoveredExposedSamples < entry.ConsecutiveFireHeatExposedSamples ? 1 : 0;
        _fireHeatSingedRecovered +=
            IsFireHeatSinged(entry.ConsecutiveFireHeatExposedSamples) && !IsFireHeatSinged(recoveredExposedSamples)
                ? 1
                : 0;
        _fireHeatBurnedRecovered +=
            IsFireHeatBurned(entry.ConsecutiveFireHeatExposedSamples) && !IsFireHeatBurned(recoveredExposedSamples)
                ? 1
                : 0;
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
            if (fireHeatExposedSamples >= Options.FireHeatBurnedThresholdSamples)
            {
                return TimberbornBeaverFieldBehaviorAction.FireHeatBurnedSafeNoOp;
            }

            return fireHeatExposedSamples >= Options.FireHeatSingedThresholdSamples ||
                decision.MaxFire >= Options.ActiveFlameContactThreshold
                ? TimberbornBeaverFieldBehaviorAction.FireHeatSingedSafeNoOp
                : TimberbornBeaverFieldBehaviorAction.FireHeatAvoidanceSafeNoOp;
        }

        if (!HasSmokeReactionExposure(decision))
        {
            return TimberbornBeaverFieldBehaviorAction.SafeNoOp;
        }

        if (exposedSamples >= Options.SmokeChokingCandidateThresholdSamples)
        {
            return TimberbornBeaverFieldBehaviorAction.ChokingWorkSlowdown;
        }

        return IsSmokeCoughing(exposedSamples)
            ? TimberbornBeaverFieldBehaviorAction.CoughingWorkSlowdown
            : TimberbornBeaverFieldBehaviorAction.SafeNoOp;
    }

    private void CountSmokeSlowdownSkip(TimberbornBeaverFieldBehaviorDecision decision)
    {
        _smokeCoughingSlowdownsSkippedNoSafeApi +=
            decision.Action == TimberbornBeaverFieldBehaviorAction.CoughingWorkSlowdown ? 1 : 0;
        _smokeChokingSlowdownsSkippedNoSafeApi +=
            decision.Action == TimberbornBeaverFieldBehaviorAction.ChokingWorkSlowdown ? 1 : 0;
    }

    private void CountSmokeSlowdownSkip(TimberbornBeaverFieldBehaviorStateEntry entry)
    {
        _smokeCoughingSlowdownsSkippedNoSafeApi +=
            entry.LastAction == TimberbornBeaverFieldBehaviorAction.CoughingWorkSlowdown ? 1 : 0;
        _smokeChokingSlowdownsSkippedNoSafeApi +=
            entry.LastAction == TimberbornBeaverFieldBehaviorAction.ChokingWorkSlowdown ? 1 : 0;
    }

    private bool IsSmokeCoughing(int exposedSamples)
    {
        return exposedSamples >= Options.SmokeCoughingThresholdSamples;
    }

    private bool IsFireHeatSinged(int exposedSamples)
    {
        return exposedSamples >= Options.FireHeatSingedThresholdSamples;
    }

    private bool IsFireHeatBurned(int exposedSamples)
    {
        return exposedSamples >= Options.FireHeatBurnedThresholdSamples;
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

    private static bool IsNoOpAction(TimberbornBeaverFieldBehaviorAction action)
    {
        return action is TimberbornBeaverFieldBehaviorAction.SafeNoOp or
            TimberbornBeaverFieldBehaviorAction.CoughingSafeNoOp or
            TimberbornBeaverFieldBehaviorAction.FireHeatAvoidanceSafeNoOp or
            TimberbornBeaverFieldBehaviorAction.FireHeatSingedSafeNoOp or
            TimberbornBeaverFieldBehaviorAction.FireHeatBurnedSafeNoOp;
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
            TimberbornBeaverFieldBehaviorAction.SafeNoOp,
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
            $"skipped_no_safe_api={counters.SkippedNoSafeApi} " +
            $"failed_decisions={counters.FailedDecisions} " +
            $"recovery_actions={counters.RecoveryActions} " +
            $"smoke_exposed_samples={counters.SmokeExposedSamples} " +
            $"smoke_exposure_accumulated_samples={counters.SmokeExposureAccumulatedSamples} " +
            $"smoke_coughing_entered={counters.SmokeCoughingEntered} " +
            $"smoke_coughing_recovered={counters.SmokeCoughingRecovered} " +
            $"smoke_coughing_slowdowns_applied={counters.SmokeCoughingSlowdownsApplied} " +
            $"smoke_coughing_slowdowns_recovered={counters.SmokeCoughingSlowdownsRecovered} " +
            $"smoke_coughing_slowdowns_skipped_no_safe_api={counters.SmokeCoughingSlowdownsSkippedNoSafeApi} " +
            $"smoke_recovery_decays={counters.SmokeRecoveryDecays} " +
            $"smoke_choking_candidates={counters.SmokeChokingCandidates} " +
            $"smoke_choking_slowdowns_applied={counters.SmokeChokingSlowdownsApplied} " +
            $"smoke_choking_slowdowns_recovered={counters.SmokeChokingSlowdownsRecovered} " +
            $"smoke_choking_slowdowns_skipped_no_safe_api={counters.SmokeChokingSlowdownsSkippedNoSafeApi} " +
            $"smoke_choking_skipped_unsafe_api={counters.SmokeChokingSkippedUnsafeApi} " +
            $"smoke_death_candidates={counters.SmokeDeathCandidates} " +
            $"smoke_death_skipped_unsafe_api={counters.SmokeDeathSkippedUnsafeApi} " +
            $"fire_heat_exposed_beavers={counters.FireHeatExposedBeavers} " +
            $"fire_heat_active_flame_contacts={counters.FireHeatActiveFlameContacts} " +
            $"fire_heat_avoidance_candidates={counters.FireHeatAvoidanceCandidates} " +
            $"fire_heat_avoided_cells={counters.FireHeatAvoidedCells} " +
            $"fire_heat_avoidance_skipped_no_safe_api={counters.FireHeatAvoidanceSkippedNoSafeApi} " +
            $"fire_heat_interrupted_job_candidates={counters.FireHeatInterruptedJobCandidates} " +
            $"fire_heat_interrupted_jobs={counters.FireHeatInterruptedJobs} " +
            $"fire_heat_interrupted_jobs_skipped_no_safe_api={counters.FireHeatInterruptedJobsSkippedNoSafeApi} " +
            $"fire_heat_singed_entered={counters.FireHeatSingedEntered} " +
            $"fire_heat_singed_recovered={counters.FireHeatSingedRecovered} " +
            $"fire_heat_singed_skipped_no_safe_api={counters.FireHeatSingedSkippedNoSafeApi} " +
            $"fire_heat_burned_entered={counters.FireHeatBurnedEntered} " +
            $"fire_heat_burned_recovered={counters.FireHeatBurnedRecovered} " +
            $"fire_heat_burned_skipped_no_safe_api={counters.FireHeatBurnedSkippedNoSafeApi} " +
            $"fire_heat_death_candidates={counters.FireHeatDeathCandidates} " +
            $"fire_heat_death_skipped_unsafe_api={counters.FireHeatDeathSkippedUnsafeApi} " +
            $"fire_heat_recovery_decays={counters.FireHeatRecoveryDecays} " +
            $"persistence_saves={counters.PersistenceSaveCount} " +
            $"persistence_loads={counters.PersistenceLoadCount}");
    }

    private static string FormatNumber(uint? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }
}
