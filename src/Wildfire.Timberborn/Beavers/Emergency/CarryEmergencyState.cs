namespace Wildfire.Timberborn.Beavers.Emergency;

public enum CarryEmergencyPhase { Inactive, Escaping, Holding }
public enum CarryEmergencyReason { None, Escaping, NoEscape, AwaitingField, AwaitingDelivery, CriticalNeeds, ControlRequested }

/// <summary>Only movement ownership is modeled here. Goods and reservations remain native.</summary>
public sealed class CarryEmergencyState
{
    public CarryEmergencyPhase Phase { get; private set; }
    public CarryEmergencyReason Reason { get; private set; }
    public float Hours { get; private set; }
    public bool Active => Phase != CarryEmergencyPhase.Inactive;

    public void Begin()
    {
        if (Active) throw new InvalidOperationException("Emergency already owns this carrying walk.");
        Hours = 0;
        Phase = CarryEmergencyPhase.Holding;
        Hold(CarryEmergencyReason.NoEscape);
    }
    public void Escape() { RequireActive(); Phase = CarryEmergencyPhase.Escaping; Reason = CarryEmergencyReason.Escaping; }
    public void Hold(CarryEmergencyReason reason) { RequireActive(); Phase = CarryEmergencyPhase.Holding; Reason = reason; }
    public void Advance(float hours)
    {
        if (float.IsNaN(hours) || float.IsInfinity(hours) || hours < 0) throw new ArgumentOutOfRangeException(nameof(hours));
        if (Active) Hours = Math.Min(Hours + hours, 100000);
    }
    public bool CanRelease(bool atSafeRefuge, bool reservationMatches, CarryDeliveryReceipt receipt)
    {
        RequireActive();
        if (!atSafeRefuge || !reservationMatches || !receipt.CanResume) return false;
        return true;
    }
    public void Release() { Phase = CarryEmergencyPhase.Inactive; Reason = CarryEmergencyReason.None; Hours = 0; }
    public void Restore(int version, int phase, int reason, float hours)
    {
        if (version != 1 || phase is < 1 or > 2 || !Enum.IsDefined(typeof(CarryEmergencyReason), reason) ||
            float.IsNaN(hours) || float.IsInfinity(hours) || hours < 0 || hours > 100000)
            throw new InvalidOperationException("Invalid saved carrying emergency; refusing to resume movement.");
        if (reason == (int)CarryEmergencyReason.None ||
            (phase == (int)CarryEmergencyPhase.Escaping) != (reason == (int)CarryEmergencyReason.Escaping))
            throw new InvalidOperationException("Saved carrying emergency phase and reason disagree.");
        Phase = (CarryEmergencyPhase)phase;
        Reason = (CarryEmergencyReason)reason;
        Hours = hours;
    }
    private void RequireActive()
    { if (!Active) throw new InvalidOperationException("Emergency does not own a carrying walk."); }
}

/// <summary>A fresh native launch plus actual path/arrival checks; stopped alone is never a receipt.</summary>
public readonly record struct CarryDeliveryReceipt(bool RouteAccepted, bool NativeRunning, bool NativeSuccess, bool PhysicallyAtTarget)
{
    public bool CanResume => RouteAccepted && (NativeRunning || NativeSuccess && PhysicallyAtTarget);
}

/// <summary>Failure boundary for the small manager/movement ownership transition.</summary>
public sealed class CarryEmergencySafety
{
    private bool _inFlight;
    private bool _cleanupAttempted;
    public Exception? Failure { get; private set; }
    public Exception? StopFailure { get; private set; }
    public bool IsPoisoned => Failure is not null;
    public void Transition(Action action, Action stopOnFailure)
    {
        ThrowIfSaveUnsafe();
        _inFlight = true;
        _cleanupAttempted = false;
        try { action(); }
        catch (Exception exception)
        {
            FailMovement(exception, stopOnFailure);
            throw;
        }
        finally { _inFlight = false; _cleanupAttempted = false; }
    }
    public void FailMovement(Exception exception, Action stopOnFailure)
    {
        Failure ??= exception;
        // A path callback can report then rethrow through the enclosing transition. One attempt
        // owns that unwind, including when native cleanup itself throws. Other actors still clean up.
        if (_inFlight && _cleanupAttempted) return;
        _cleanupAttempted = true;
        // Movement may already have launched even if the logical phase was released.
        try { stopOnFailure(); }
        catch (Exception stopFailure) { StopFailure ??= stopFailure; }
    }
    public void ThrowIfSaveUnsafe()
    {
        if (_inFlight || Failure is not null)
            throw new InvalidOperationException("Carrying emergency ownership is indeterminate; reload the last good save.", Failure);
    }
}
