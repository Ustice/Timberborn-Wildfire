namespace Wildfire.Timberborn.Ash;

public enum AshHarvestPhase { Idle, Approaching, Harvesting, ReadyToCollect, Returning, HoldingForDeposit }

/// <summary>Work progress only. Native GoodCarrier and GoodReserver remain the sole cargo/capacity owners.</summary>
public sealed class AshHarvestCycle
{
    public AshHarvestPhase Phase { get; private set; }
    public float Hours { get; private set; }
    public bool HasCargo => Phase is AshHarvestPhase.Returning or AshHarvestPhase.HoldingForDeposit;
    public void Begin() => Set(AshHarvestPhase.Approaching);
    public void Arrived(bool physicallyPresent)
    {
        if (Phase != AshHarvestPhase.Approaching || !physicallyPresent) throw new InvalidOperationException("Ash harvest requires actual arrival.");
        Set(AshHarvestPhase.Harvesting);
    }
    public void Advance(float hours)
    {
        if (float.IsNaN(hours) || float.IsInfinity(hours) || hours < 0) throw new ArgumentOutOfRangeException(nameof(hours));
        Hours += hours;
        if (Phase == AshHarvestPhase.Harvesting && Hours >= .1f) Set(AshHarvestPhase.ReadyToCollect);
    }
    public void Received(byte collected)
    {
        if (Phase != AshHarvestPhase.ReadyToCollect || collected > 1) throw new InvalidOperationException("Unexpected one-unit ash receipt.");
        Set(collected == 0 ? AshHarvestPhase.Idle : AshHarvestPhase.Returning);
    }
    public void Hold() => Set(AshHarvestPhase.HoldingForDeposit);
    public void Return() => Set(AshHarvestPhase.Returning);
    public void Finish() => Set(AshHarvestPhase.Idle);
    public void Restore(int phase, float hours)
    {
        if (!Enum.IsDefined(typeof(AshHarvestPhase), phase) || float.IsNaN(hours) || float.IsInfinity(hours) || hours < 0)
            throw new InvalidOperationException("Invalid ash harvest progress.");
        Phase = (AshHarvestPhase)phase;
        Hours = hours;
    }
    public void ValidateCargo(bool carrying, bool ownAsh, int amount)
    {
        if (HasCargo ? !carrying || !ownAsh || amount != 1 : carrying)
            throw new InvalidOperationException("Ash harvest phase does not match native carried goods.");
    }
    private void Set(AshHarvestPhase phase) { Phase = phase; Hours = 0; }
}
