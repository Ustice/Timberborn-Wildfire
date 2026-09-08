namespace Wildfire.Core;

public readonly record struct FireSimAshApplicationInput(int CellIndex, byte Limit);
public enum FireSimAshApplicationOutcome : byte { Applied, Full, Tainted, InvalidSurface }
public readonly record struct FireSimAshApplicationReceipt(
    int CellIndex, byte Limit, byte Added, FireSimAshApplicationOutcome Outcome);
public readonly record struct FireSimAshApplicationStepResult(
    GpuFireStepResult Step, FireSimAshApplicationReceipt Receipt);

/// <summary>One clean ash unit, conditional on landing surface, zero ash taint, and ash below Limit (1..3).</summary>
public interface IFireSimAshApplicationSimulator : IFireSimStepInputSimulator
{
    /// <summary>
    /// Null means no admission and no step. Otherwise the receipt describes application before
    /// normal simulation, after earlier queued changes. Only Applied/Added=1 invokes the callback,
    /// after readback/swap and before listeners. A valid rejection still completes a normal step.
    /// Failures use FireSimStepInputException: Committed means delivery must not be replayed even
    /// though this paired return value was unavailable. Missing or malformed receipts are Indeterminate.
    /// </summary>
    FireSimAshApplicationStepResult? TryApplyCleanAsh(FireSimAshApplicationInput input,
        Action<FireSimAshApplicationReceipt> commitApplication);
}
