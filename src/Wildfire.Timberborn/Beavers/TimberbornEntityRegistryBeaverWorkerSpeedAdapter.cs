using System.Reflection;
using Timberborn.Beavers;
using Timberborn.EntitySystem;
using Timberborn.StatusSystem;
using Timberborn.WorkSystem;

namespace Wildfire.Timberborn.Beavers;

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
        entity.TryGetComponent(out Worker worker);
        entity.TryGetComponent(out StatusSubject statusSubject);
        MethodInfo? setter = WorkerSpeedMultiplierProperty?.GetSetMethod(nonPublic: true);
        if (worker is not null && setter is null)
            return UnsupportedSetter(); // No status has changed yet.
        float current = worker is null ? 1f : worker.WorkingSpeedMultiplier;
        float original = _originalWorkingSpeedMultiplierByBeaverId.TryGetValue(beaverId, out float saved) ? saved : current;
        bool register = !_statusTogglesByBeaverId.TryGetValue(beaverId, out var toggles);
        if (statusSubject is not null && register) toggles = TimberbornBeaverSmokeStatusToggles.Create();

        bool nativeStarted = false;
        try
        {
            if (statusSubject is not null)
            {
                if (register) _statusTogglesByBeaverId.Add(beaverId, toggles!);
                nativeStarted = true;
                if (register) toggles!.Register(statusSubject);
                toggles!.Apply(action);
            }
            if (worker is not null)
            {
                _originalWorkingSpeedMultiplierByBeaverId.TryAdd(beaverId, original);
                nativeStarted = true;
                setter!.Invoke(worker, new object[] { Math.Min(current, original * multiplier) });
            }
            return TimberbornBeaverWorkerSpeedResult.Applied;
        }
        catch (Exception exception) when (nativeStarted)
        {
            // Native status/speed writes precede their callbacks. Never infer rollback.
            throw new TimberbornBeaverFieldDeliveryException(beaverId, exception);
        }
    }

    public TimberbornBeaverWorkerSpeedResult RecoverSmokeReaction(string beaverId)
    {
        EntityComponent entity = GetEntityOrThrow(beaverId);
        entity.TryGetComponent(out Worker worker);
        float original = _originalWorkingSpeedMultiplierByBeaverId.TryGetValue(beaverId, out float saved) ? saved : 1f;
        bool restore = worker is not null && worker.WorkingSpeedMultiplier <=
            original * TimberbornWorkerSpeedBeaverFieldBehaviorActuator.CoughingWorkingSpeedMultiplier + RestoreTolerance;
        MethodInfo? setter = WorkerSpeedMultiplierProperty?.GetSetMethod(nonPublic: true);
        if (restore && setter is null) return UnsupportedSetter();
        _statusTogglesByBeaverId.TryGetValue(beaverId, out var toggles);

        bool nativeStarted = false;
        try
        {
            if (toggles is not null)
            {
                nativeStarted = true;
                toggles.DeactivateAll();
            }
            if (restore)
            {
                nativeStarted = true;
                setter!.Invoke(worker, new object[] { original });
            }
            _originalWorkingSpeedMultiplierByBeaverId.Remove(beaverId);
            return TimberbornBeaverWorkerSpeedResult.Applied;
        }
        catch (Exception exception) when (nativeStarted)
        {
            throw new TimberbornBeaverFieldDeliveryException(beaverId, exception);
        }
    }

    private static TimberbornBeaverWorkerSpeedResult UnsupportedSetter() => new(
        TimberbornBeaverFieldBehaviorActuatorStatus.Unsupported, "worker_speed_setter_unavailable");

    public void Clear()
    {
        _originalWorkingSpeedMultiplierByBeaverId.Clear();
        _statusTogglesByBeaverId.Values
            .ToList()
            .ForEach(static toggles => toggles.DeactivateForTeardown());
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

        public static TimberbornBeaverSmokeStatusToggles Create()
        {
            StatusToggle coughingStatus = StatusToggle.CreateNormalStatusWithFloatingIcon(
                CoughingStatusIcon,
                "Coughing from smoke",
                delayInHours: 0f);
            StatusToggle chokingStatus = StatusToggle.CreatePriorityStatusWithFloatingIcon(
                ChokingStatusIcon,
                "Choking on smoke",
                delayInHours: 0f);
            return new TimberbornBeaverSmokeStatusToggles(coughingStatus, chokingStatus);
        }

        public void Register(StatusSubject statusSubject)
        {
            statusSubject.RegisterStatus(_coughingStatus);
            statusSubject.RegisterStatus(_chokingStatus);
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

        public void DeactivateForTeardown()
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
