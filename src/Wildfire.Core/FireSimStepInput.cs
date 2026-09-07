namespace Wildfire.Core;

/// <summary>Optional simulator capability for one input finalized in the same synchronous step.</summary>
public interface IFireSimStepInputSimulator : IGpuFireSimulator
{
    /// <summary>
    /// Returns null without ticking when the next queued batch has no room. Otherwise appends
    /// the input to that batch without storing it in the queue, then calls commitInput after
    /// readback and buffer swap, before listeners. A no-op input still commits.
    /// Invalid arguments or unavailable backend fail before admission. Admitted-step failures
    /// throw FireSimStepInputException; hosts must not retry an indeterminate input.
    /// </summary>
    GpuFireStepResult? TryTickWithInput(FireSimChange input, Action commitInput);
}

public enum FireSimStepInputOutcome
{
    // No call to ApplyExternalChanges began. The one-step input was not queued.
    NotApplied,
    // ApplyExternalChanges began, but commitInput did not complete. Stop and reconcile at the host boundary.
    Indeterminate,
    // Readback, swap, and commitInput completed. A subsequent listener failed; do not replay the input.
    Committed,
}

public sealed class FireSimStepInputException : Exception
{
    public FireSimStepInputException(FireSimStepInputOutcome outcome, Exception innerException)
        : base($"Fire simulator step input failed with outcome {outcome}.", innerException)
    {
        Outcome = outcome;
    }

    public FireSimStepInputOutcome Outcome { get; }
}
