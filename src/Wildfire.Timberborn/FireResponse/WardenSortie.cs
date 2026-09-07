namespace Wildfire.Timberborn.FireResponse;

public enum WardenPhase { Idle, Fetching, Approaching, Applying, AwaitingApplication, Returning }

/// <summary>Explicit arrival and delivery states prevent a stopped native walk from becoming a spray.</summary>
public sealed class WardenSortie
{
    public WardenPhase Phase { get; private set; }
    public float HoursInPhase { get; private set; }
    public bool AwaitingApplication => Phase == WardenPhase.AwaitingApplication;
    public void Begin(bool alreadyLoaded) => Set(alreadyLoaded ? WardenPhase.Approaching : WardenPhase.Fetching);
    public void Filled() => Set(WardenPhase.Approaching);
    public void Arrived(bool physicallyAtApproach)
    {
        if (Phase != WardenPhase.Approaching) return;
        Set(physicallyAtApproach ? WardenPhase.Applying : WardenPhase.Returning);
    }
    public void Advance(float hours)
    {
        HoursInPhase += Math.Max(0, hours);
        if (Phase == WardenPhase.Applying && HoursInPhase >= .05f) Set(WardenPhase.AwaitingApplication);
    }
    public void Cancel() => Set(WardenPhase.Returning);
    public void Applied()
    {
        if (Phase != WardenPhase.AwaitingApplication) throw new InvalidOperationException("Warden has no pending application.");
        Set(WardenPhase.Returning);
    }
    public void Finish() => Set(WardenPhase.Idle);
    public void Restore(int phase, float hours)
    {
        if (!Enum.IsDefined(typeof(WardenPhase), phase) || !float.IsFinite(hours) || hours < 0)
            throw new InvalidOperationException("Invalid saved warden sortie.");
        Phase = (WardenPhase)phase;
        HoursInPhase = hours;
    }
    private void Set(WardenPhase phase) { Phase = phase; HoursInPhase = 0; }
}
