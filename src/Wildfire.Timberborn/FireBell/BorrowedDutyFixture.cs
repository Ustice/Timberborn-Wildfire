using Timberborn.SingletonSystem;
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
    public void Load() => Disarm(); // Saved actors remain registered and continue their bounded job.
    internal void Register(BorrowedDutyExecutor executor) => _executors.Add(executor);
    internal void Unregister(BorrowedDutyExecutor executor) => _executors.Remove(executor);
    public void Arm(Workplace donor, Vector3 destination)
    {
        if (!AdmissionsEnabled) throw new InvalidOperationException($"Borrowed duty requires {OptInSwitch}.");
        if (_executors.Any(executor => executor.Phase != BorrowedDutyPhase.Idle))
            throw new InvalidOperationException("The previous borrowed duty must finish before arming another.");
        if (!donor || !donor.Enabled || !float.IsFinite(destination.x) || !float.IsFinite(destination.y) || !float.IsFinite(destination.z))
            throw new ArgumentException("A live enabled donor and finite destination are required.");
        _donor = donor; _point = destination;
    }
    public void Disarm() => _donor = null;
    public void Cancel()
    {
        Disarm();
        foreach (var executor in _executors.ToArray()) executor.RequestCancel();
    }
    internal bool IsArmedAt(Workplace donor) => AdmissionsEnabled && ReferenceEquals(donor, _donor);
    internal bool TryOffer(Workplace donor, BorrowedDutyExecutor executor)
    {
        if (!IsArmedAt(donor) || !executor.TryLaunch(donor, _point)) return false;
        Disarm(); return true;
    }
}
