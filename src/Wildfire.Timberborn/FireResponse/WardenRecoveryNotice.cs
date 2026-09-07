namespace Wildfire.Timberborn.FireResponse;

/// <summary>One notification attempt per loaded world; never invoked from a water operation.</summary>
public sealed class WardenRecoveryNotice
{
    private bool _attempted;
    public void ShowIfNeeded(bool indeterminate, Action show)
    {
        if (!indeterminate || _attempted) return;
        _attempted = true; // External notification listeners may throw or reenter.
        show();
    }
    public void ResetForWorldLoad() => _attempted = false;
}
