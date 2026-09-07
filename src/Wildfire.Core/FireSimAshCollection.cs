namespace Wildfire.Core;

public readonly record struct FireSimAshCollectionInput(int CellIndex, byte Requested);
public readonly record struct FireSimAshCollectionReceipt(int CellIndex, byte Requested, byte Collected);

/// <summary>Conditional resource removal, distinct from an unconditional completed step input.</summary>
public interface IFireSimAshCollectionSimulator : IFireSimStepInputSimulator
{
    /// <summary>
    /// Appends one exclusive clean-ash collection after the next queued batch, or returns null
    /// without ticking when full. Requested must be 0..3. The GPU removes min(requested, ash)
    /// only if current ash contamination is zero, before normal simulation. Explicit zero receives
    /// a valid zero receipt. The callback receives the actual removal after readback/swap and before
    /// listeners. Step failures use FireSimStepInputException and its existing ownership outcomes.
    /// Missing/malformed receipts are indeterminate failures, never invented zero collections.
    /// </summary>
    GpuFireStepResult? TryCollectAsh(FireSimAshCollectionInput input, Action<FireSimAshCollectionReceipt> commitCollection);
}

public interface IFireSimAshCollectionBackend : IFireSimStepBackend
{
    FireSimGpuChange ReadAppliedChange(int changeIndex);
}
