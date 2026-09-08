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

