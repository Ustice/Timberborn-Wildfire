using Wildfire.Core;

namespace Wildfire.Timberborn.Resources;

/// <summary>Session safety around the synchronous simulator/stock commit. No inventory is duplicated here.</summary>
public sealed class NativeResourceTransaction : INativeResourceMutationGuard
{
    private bool _delivering;
    public bool IsIndeterminate { get; private set; }

    public GpuFireStepResult? TryDeliver(IFireSimStepInputSimulator simulator, FireSimChange input, Action commit)
    {
        return ExecuteStep(() => simulator.TryTickWithInput(input, commit));
    }

    public GpuFireStepResult? TryCollectAsh(IFireSimAshCollectionSimulator simulator, FireSimAshCollectionInput input,
        Action<FireSimAshCollectionReceipt> commit) => ExecuteStep(() => simulator.TryCollectAsh(input, commit));

    private GpuFireStepResult? ExecuteStep(Func<GpuFireStepResult?> step)
    {
        ThrowIfSaveUnsafe();
        _delivering = true;
        try
        {
            var result = step();
            if (IsIndeterminate)
                throw new FireSimStepInputException(FireSimStepInputOutcome.Indeterminate,
                    new InvalidOperationException("Native lifecycle cleanup invalidated the resource step."));
            return result;
        }
        catch (FireSimStepInputException exception)
        {
            if (exception.Outcome == FireSimStepInputOutcome.Indeterminate) IsIndeterminate = true;
            throw;
        }
        finally { _delivering = false; }
    }

    public T CaptureAtRest<T>(Func<T> capture)
    {
        ThrowIfSaveUnsafe();
        if (capture is null) throw new ArgumentNullException(nameof(capture));
        _delivering = true; // Same exclusion latch; read failures do not imply resource mutation.
        try
        {
            T result = capture();
            if (IsIndeterminate) ThrowIfSaveUnsafe();
            return result;
        }
        finally { _delivering = false; }
    }

    // Irreversible native teardown must continue even when its guarded cleanup cannot.
    // Invalidation never authorizes a write or clears existing poison.
    public void InvalidateAfterLifecycleFailure() => IsIndeterminate = true;

    public void TransferInventory(Action transfer)
    {
        ThrowIfSaveUnsafe();
        _delivering = true;
        try
        {
            transfer();
            if (IsIndeterminate) ThrowIfSaveUnsafe();
        }
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
            throw new InvalidOperationException("Wildfire resource conversion is in progress or indeterminate; reload the last saved world before saving.");
    }

    public void ResetForWorldLoad()
    {
        if (_delivering) throw new InvalidOperationException("Cannot replace the world during resource conversion.");
        IsIndeterminate = false;
    }
}
