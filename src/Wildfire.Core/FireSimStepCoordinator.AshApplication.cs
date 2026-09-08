namespace Wildfire.Core;

public sealed partial class FireSimStepCoordinator
{
    // Collection's backend is the existing shared in-place command receipt readback, not a new buffer.
    public FireSimAshApplicationStepResult? TryApplyCleanAsh(IFireSimAshCollectionBackend backend,
        FireSimAshApplicationInput input, Action<FireSimAshApplicationReceipt> commitApplication)
    {
        if (input.CellIndex < 0 || input.CellIndex >= _cellCount)
            throw new ArgumentOutOfRangeException(nameof(input), "Application must address a cell in this grid.");
        if (commitApplication is null) throw new ArgumentNullException(nameof(commitApplication));
        var change = new FireSimChange(input.CellIndex, ApplyCleanAshLimit: input.Limit);
        FireSimGpuProtocol.EncodeChange(change);
        FireSimAshApplicationReceipt receipt = default;
        var step = TryTickWithInputCore(backend, change, () =>
        {
            receipt = FireSimGpuProtocol.DecodeApplicationReceipt(
                backend.ReadAppliedChange(LastUploadedChangeCount - 1), input);
            if (receipt.Outcome == FireSimAshApplicationOutcome.Applied) commitApplication(receipt);
        });
        return step.HasValue ? new FireSimAshApplicationStepResult(step.Value, receipt) : null;
    }
}
