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
        int SmokeChokingThresholdSamples = 8,
        int SmokeRecoveryDecaySamples = 1,
        int ToxicSmokeExposureSampleWeight = 2,
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

        if (SmokeChokingThresholdSamples < SmokeCoughingThresholdSamples)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SmokeChokingThresholdSamples),
                SmokeChokingThresholdSamples,
                "The smoke choking threshold must not be below the coughing threshold.");
        }

        if (SmokeRecoveryDecaySamples <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SmokeRecoveryDecaySamples),
                SmokeRecoveryDecaySamples,
                "The smoke recovery decay must be positive.");
        }

        if (ToxicSmokeExposureSampleWeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ToxicSmokeExposureSampleWeight),
                ToxicSmokeExposureSampleWeight,
                "The toxic smoke exposure sample weight must be positive.");
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
        this.SmokeChokingThresholdSamples = SmokeChokingThresholdSamples;
        this.SmokeRecoveryDecaySamples = SmokeRecoveryDecaySamples;
        this.ToxicSmokeExposureSampleWeight = ToxicSmokeExposureSampleWeight;
        this.FireHeatRecoveryDecaySamples = FireHeatRecoveryDecaySamples;
        this.ActiveFlameContactThreshold = ActiveFlameContactThreshold;
    }

    public uint DecisionCooldownTicks { get; }

    public int MaxDecisionsPerDispatch { get; }

    public int SmokeCoughingThresholdSamples { get; }

    public int SmokeChokingThresholdSamples { get; }

    public int SmokeRecoveryDecaySamples { get; }

    public int ToxicSmokeExposureSampleWeight { get; }

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
    NoOp = 1,
    CoughingWorkSlowdown = 3,
    ChokingWorkSlowdown = 4,
    FireHeatExposureAttempt = 5,
}

public enum TimberbornBeaverFieldBehaviorActuatorStatus
{
    Applied = 1,
    Failed = 2,
    Unsupported = 3,
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

    public static TimberbornBeaverFieldBehaviorActuatorResult Unsupported(string reason) => new(
        TimberbornBeaverFieldBehaviorActuatorStatus.Unsupported, reason);
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
        throw new InvalidOperationException("Beaver field behavior actuator is unavailable.");
    }

    public TimberbornBeaverFieldBehaviorActuatorResult Recover(TimberbornBeaverFieldBehaviorStateEntry entry, uint? tick)
    {
        throw new InvalidOperationException("Beaver field behavior actuator is unavailable.");
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
        if (decision.Action == TimberbornBeaverFieldBehaviorAction.FireHeatExposureAttempt)
            return TimberbornBeaverFieldBehaviorActuatorResult.Unsupported("fire_heat_actuation_unavailable");

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
        EntityComponent entity = GetEntityOrThrow(beaverId);
        TryApplyStatus(entity, beaverId, action);
        TryApplyWorkingSpeed(entity, beaverId, multiplier);
        return TimberbornBeaverWorkerSpeedResult.Applied;
    }

    public TimberbornBeaverWorkerSpeedResult RecoverSmokeReaction(string beaverId)
    {
        EntityComponent entity = GetEntityOrThrow(beaverId);

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
            if (!restored)
            {
                throw new InvalidOperationException("Worker speed setter is unavailable.");
            }

            return TimberbornBeaverWorkerSpeedResult.Applied;
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

    private EntityComponent GetEntityOrThrow(string beaverId)
    {
        if (!Guid.TryParse(beaverId, out Guid entityId))
        {
            throw new InvalidOperationException($"Invalid beaver id: {beaverId}.");
        }

        EntityComponent entity = _entityRegistry.GetEntity(entityId);
        if (!entity.TryGetComponent(out Beaver _))
        {
            throw new InvalidOperationException($"Entity {beaverId} is not a beaver.");
        }

        return entity;
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
        if (!TrySetWorkingSpeedMultiplier(
            worker,
            Math.Min(worker.WorkingSpeedMultiplier, originalMultiplier * multiplier)))
        {
            throw new InvalidOperationException("Worker speed setter is unavailable.");
        }
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
        catch (Exception exception)
        {
            throw new InvalidOperationException("Worker speed setter failed.", exception);
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
        catch (Exception exception)
        {
            _statusTogglesByBeaverId.Remove(beaverId);
            throw new InvalidOperationException("Smoke status toggle application failed.", exception);
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
            TryDeactivate(_coughingStatus);
            TryDeactivate(_chokingStatus);
        }

        private static void TryDeactivate(StatusToggle statusToggle)
        {
            try
            {
                statusToggle.Deactivate();
            }
            catch (Exception exception) when (exception is NullReferenceException or InvalidOperationException)
            {
                // Timberborn may already be tearing down floating status renderers during exception-save unload.
            }
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
    int FailedDecisions,
    int UnsupportedDecisions,
    int RecoveryActions,
    int SmokeExposedSamples,
    int SmokeExposureAccumulatedSamples,
    int SmokeCoughingEntered,
    int SmokeCoughingRecovered,
    int SmokeCoughingSlowdownsApplied,
    int SmokeCoughingSlowdownsRecovered,
    int SmokeRecoveryDecays,
    int SmokeChokingSlowdownsApplied,
    int SmokeChokingSlowdownsRecovered,
    int ToxicSmokeExposedBeavers,
    int ToxicSmokeExposureAccumulatedSamples,
    int ToxicSmokeRecoveryDecays,
    int FireHeatExposedBeavers,
    int FireHeatActiveFlameContacts,
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
