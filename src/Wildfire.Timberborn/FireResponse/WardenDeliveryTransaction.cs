using Wildfire.Core;

namespace Wildfire.Timberborn.FireResponse;

/// <summary>Session safety around the synchronous simulator/stock commit. No inventory is duplicated here.</summary>
public sealed class WardenDeliveryTransaction
{
    private bool _delivering;
    public bool IsIndeterminate { get; private set; }

    public GpuFireStepResult? TryDeliver(IFireSimStepInputSimulator simulator, FireSimChange input, Action commit)
    {
        ThrowIfSaveUnsafe();
        _delivering = true;
        try { return simulator.TryTickWithInput(input, commit); }
        catch (FireSimStepInputException exception)
        {
            if (exception.Outcome == FireSimStepInputOutcome.Indeterminate) IsIndeterminate = true;
            throw;
        }
        finally { _delivering = false; }
    }

    public void TransferInventory(Action transfer)
    {
        ThrowIfSaveUnsafe();
        _delivering = true;
        try { transfer(); }
        catch
        {
            // Native inventory calls mutate before raising events. No rollback is inferred from an exception.
            IsIndeterminate = true;
            throw;
        }
        finally { _delivering = false; }
    }

    public void ThrowIfSaveUnsafe()
    {
        if (_delivering || IsIndeterminate)
            throw new InvalidOperationException("Warden water delivery is in progress or indeterminate; reload the last saved world before saving.");
    }

    public void ResetForWorldLoad()
    {
        if (_delivering) throw new InvalidOperationException("Cannot replace the world during water delivery.");
        IsIndeterminate = false;
    }
}
