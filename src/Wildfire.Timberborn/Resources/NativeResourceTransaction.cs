using Wildfire.Core;

namespace Wildfire.Timberborn.Resources;

/// <summary>Session safety around the synchronous simulator/stock commit. No inventory is duplicated here.</summary>
public sealed class NativeResourceTransaction : INativeResourceMutationGuard
{
    private bool _delivering;
    private bool _dispatching;
    private bool _ashApplicationCommit;
    public bool IsIndeterminate { get; private set; }

    public GpuFireStepResult? TryDeliver(IFireSimStepInputSimulator simulator, FireSimChange input, Action commit)
    {
        return ExecuteStep(() => simulator.TryTickWithInput(input, commit));
    }

    public GpuFireStepResult? TryCollectAsh(IFireSimAshCollectionSimulator simulator, FireSimAshCollectionInput input,
        Action<FireSimAshCollectionReceipt> commit) => ExecuteStep(() => simulator.TryCollectAsh(input, commit));

    public FireSimAshApplicationStepResult? TryApplyCleanAsh(IFireSimAshApplicationSimulator simulator,
        FireSimAshApplicationInput input, Action<FireSimAshApplicationReceipt> commitApplication)
    {
        if (commitApplication is null) throw new ArgumentNullException(nameof(commitApplication));
        return ExecuteStep(() => simulator.TryApplyCleanAsh(input, receipt =>
        {
            if (receipt.CellIndex != input.CellIndex || receipt.Limit != input.Limit ||
                input.Limit is < 1 or > 3 || receipt.Added != 1 || receipt.Outcome != FireSimAshApplicationOutcome.Applied)
                throw new InvalidOperationException("Only an exact accepted ash application can authorize native consumption.");
            _ashApplicationCommit = true;
            try { commitApplication(receipt); }
            finally { _ashApplicationCommit = false; }
        }));
    }

    public void RequireAshApplicationCommit()
    {
        if (!_delivering || !_ashApplicationCommit || IsIndeterminate)
            throw new InvalidOperationException("Native fertilizer consumption requires an accepted ash application commit.");
    }

    private T ExecuteStep<T>(Func<T> step)
    {
        ThrowIfOperationUnsafe();
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
        ThrowIfOperationUnsafe();
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
        if (_dispatching || _delivering || IsIndeterminate)
            throw new InvalidOperationException("Wildfire resource conversion is in progress or indeterminate; reload the last saved world before saving.");
    }

    // Save exclusion is not a mutation privilege: existing inner operations still own their latch.
    internal void ExcludeSavesDuringDispatch(Action dispatch)
    {
        if (dispatch is null) throw new ArgumentNullException(nameof(dispatch));
        ThrowIfSaveUnsafe();
        _dispatching = true;
        try
        {
            dispatch();
            if (IsIndeterminate) ThrowIfSaveUnsafe();
        }
        finally { _dispatching = false; }
    }

    internal void ThrowIfOperationUnsafe()
    {
        if (_delivering || IsIndeterminate)
            throw new InvalidOperationException("Wildfire resource operation is in progress or indeterminate.");
    }

    public void ResetForWorldLoad()
    {
        if (_dispatching || _delivering) throw new InvalidOperationException("Cannot replace the world during resource conversion or dispatch.");
        IsIndeterminate = false;
    }
}
