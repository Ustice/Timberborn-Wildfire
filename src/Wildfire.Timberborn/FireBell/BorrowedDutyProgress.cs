namespace Wildfire.Timberborn.FireBell;

public enum BorrowedDutyPhase { Idle, Outbound, AtPoint, Returning, FetchingWater, AwaitingCredit, ApproachingFire, AwaitingApplication }

/// <summary>Actor phases and elapsed deadline; native inventory remains the only carried quantity.</summary>
public sealed class BorrowedDutyProgress
{
    public BorrowedDutyPhase Phase { get; private set; }
    public float Hours { get; private set; }
    public bool CancellationRequested { get; private set; }
    public void Begin() { Phase = BorrowedDutyPhase.Outbound; Hours = 0; CancellationRequested = false; }
    public void Arrive(bool physicallyPresent)
    {
        if (Phase != BorrowedDutyPhase.Outbound || !physicallyPresent) throw new InvalidOperationException("Borrowed duty requires physical arrival.");
        Phase = BorrowedDutyPhase.AtPoint;
    }
    public void BeginWater() { Begin(); Phase = BorrowedDutyPhase.FetchingWater; }
    public void ArriveSource(bool physicallyPresent) => Transition(BorrowedDutyPhase.FetchingWater, BorrowedDutyPhase.AwaitingCredit, physicallyPresent);
    public void Loaded() => Transition(BorrowedDutyPhase.AwaitingCredit, BorrowedDutyPhase.ApproachingFire);
    public void ArriveFire(bool physicallyPresent) => Transition(BorrowedDutyPhase.ApproachingFire, BorrowedDutyPhase.AwaitingApplication, physicallyPresent);
    public void Applied() => Transition(BorrowedDutyPhase.AwaitingApplication, BorrowedDutyPhase.Returning);
    private void Transition(BorrowedDutyPhase expected, BorrowedDutyPhase next, bool permitted = true)
    {
        if (Phase != expected || !permitted) throw new InvalidOperationException("Borrowed water phase or physical arrival changed.");
        Phase = next;
    }
    public void Advance(float hours)
    {
        if (!float.IsFinite(hours) || hours < 0) throw new ArgumentOutOfRangeException(nameof(hours));
        Hours += hours;
    }
    public void RequestCancel() => CancellationRequested = true;
    public void Return() => Phase = BorrowedDutyPhase.Returning;
    public void Finish() => Phase = BorrowedDutyPhase.Idle;
    public void Restore(int phase, float hours, bool cancel)
    {
        if (!Enum.IsDefined(typeof(BorrowedDutyPhase), phase) || !float.IsFinite(hours) || hours < 0)
            throw new InvalidOperationException("Invalid borrowed duty save state.");
        Phase = (BorrowedDutyPhase)phase; Hours = hours; CancellationRequested = cancel;
    }
}

public readonly record struct BorrowedDutyEligibility(bool EmployedAtDonor, bool SameDistrict,
    bool DuringWorkHours, bool RefusesWork, bool CriticalNeed, bool Mortal, bool CarriesGoods,
    bool HasReservation, bool HoldsResponseWater, bool HasRunningExecutor, bool ResourceStateUnsafe)
{
    public bool CanJoin => EmployedAtDonor && SameDistrict && DuringWorkHours && !RefusesWork &&
        !CriticalNeed && !Mortal && !CarriesGoods && !HasReservation && !HoldsResponseWater &&
        !HasRunningExecutor && !ResourceStateUnsafe;
}
