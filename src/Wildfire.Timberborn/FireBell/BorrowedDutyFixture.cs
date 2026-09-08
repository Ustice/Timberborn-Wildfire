using Timberborn.SingletonSystem;
using Timberborn.EntitySystem;
using Wildfire.Timberborn.Qa;
using Timberborn.WorkSystem;
using UnityEngine;

namespace Wildfire.Timberborn.FireBell;

/// <summary>Explicit development admission, deliberately not connected to final player UI or bell policy.</summary>
public sealed class BorrowedDutyFixture : ILoadableSingleton
{
    public const string OptInSwitch = "--wildfire-enable-borrowed-duty";
    public bool AdmissionsEnabled { get; } = Environment.GetCommandLineArgs().Contains(OptInSwitch, StringComparer.Ordinal);
    private readonly List<BorrowedDutyExecutor> _executors = new();
    private Workplace? _donor;
    private Vector3 _point;
    private BorrowedWaterTrip? _waterTrip;
    public void Load() => Disarm(); // Saved actors remain registered and continue their bounded job.
    internal void Register(BorrowedDutyExecutor executor) => _executors.Add(executor);
    internal void Unregister(BorrowedDutyExecutor executor) => _executors.Remove(executor);
    public void Arm(Workplace donor, Vector3 destination)
    {
        if (!AdmissionsEnabled) throw new InvalidOperationException($"Borrowed duty requires {OptInSwitch}.");
        if (_donor is not null) throw new InvalidOperationException("Cancel the existing borrowed duty offer before arming another.");
        if (_executors.Any(executor => executor.Phase != BorrowedDutyPhase.Idle))
            throw new InvalidOperationException("The previous borrowed duty must finish before arming another.");
        if (!donor || !donor.Enabled || !float.IsFinite(destination.x) || !float.IsFinite(destination.y) || !float.IsFinite(destination.z))
            throw new ArgumentException("A live enabled donor and finite destination are required.");
        _donor = donor; _point = destination;
    }
    internal void ArmWater(Workplace donor, BorrowedWaterTrip trip)
    {
        Arm(donor, trip.Shore);
        try
        {
            bool prepared = false;
            foreach (var executor in _executors.ToArray()) prepared |= executor.PrepareWaterOffer(donor);
            if (!prepared) throw new InvalidOperationException("The finite water offer requires a current initialized donor employee.");
            _waterTrip = trip;
        }
        catch { Disarm(); throw; }
    }
    public BorrowedDutyQaStatus CaptureQaStatus()
    {
        bool donorLive = _donor is not null && _donor && !_donor.GetComponent<EntityComponent>().Deleted;
        return new BorrowedDutyQaStatus(AdmissionsEnabled,
            _donor is null ? "none" : donorLive ? "armed" : "unavailable",
            donorLive ? _donor!.GetComponent<EntityComponent>().EntityId : null,
            _executors.Where(executor => executor.Phase != BorrowedDutyPhase.Idle || executor.RetainedWater)
                .Select(executor => new BorrowedDutyQaActor(executor.EntityId, executor.Phase.ToString(), executor.CancellationRequested, executor.NativeExecutionOwned,
                    executor.WaterIntent, executor.WaterStock, executor.WaterSourceId, executor.ReturnAssigned)).ToArray(),
            _donor is null ? null : new BorrowedDutyQaPoint(_point.x, _point.y, _point.z));
    }
    public void Disarm() { _donor = null; _waterTrip = null; }
    public void Cancel()
    {
        Disarm();
        foreach (var executor in _executors.ToArray()) executor.RequestCancel();
    }
    internal bool IsArmedAt(Workplace donor) => AdmissionsEnabled && ReferenceEquals(donor, _donor);
    internal bool TryOffer(Workplace donor, BorrowedDutyExecutor executor)
    {
        if (!IsArmedAt(donor)) return false;
        bool launched = _waterTrip is { } trip ? executor.TryLaunchWater(donor, trip) : executor.TryLaunch(donor, _point);
        if (!launched) return false;
        Disarm(); return true;
    }
}
