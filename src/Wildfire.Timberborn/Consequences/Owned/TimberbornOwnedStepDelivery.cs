using System.Runtime.ExceptionServices;
using Wildfire.Core;

namespace Wildfire.Timberborn.Consequences;

internal readonly record struct TimberbornOwnedStepDelivery(GpuFireStepResult Step,
    TimberbornOwnedConsequenceBatchResult Consequences);

public sealed partial class TimberbornOwnedDeltaConsumer
{
    /// <summary>
    /// Own one synchronous simulator step and its complete native delivery under the same save guard.
    /// The trusted caller must reject an unsuccessful material receipt before returning its step.
    /// No transaction capability or raw delivery delegate escapes this boundary.
    /// </summary>
    internal TimberbornOwnedStepDelivery? ConsumeStep(Func<GpuFireStepResult?> step)
    {
        if (step is null) throw new ArgumentNullException(nameof(step));
        if (_consuming) throw new InvalidOperationException("Owned consequence delivery cannot reenter.");
        _guard.ThrowIfSaveUnsafe();
        _consuming = true;
        try
        {
            TimberbornOwnedStepDelivery? delivered = null;
            FireSimStepInputException? notApplied = null;
            _guard.TransferInventory(() =>
            {
                GpuFireStepResult? result;
                try { result = step(); }
                catch (FireSimStepInputException exception) when (exception.Outcome == FireSimStepInputOutcome.NotApplied)
                { notApplied = exception; return; }
                if (result is not { } completed) return;
                // Simulation may already have mutated: even preflight failures now belong inside the guard.
                var prepared = PrepareDelivery(completed.Deltas.ToArray());
                delivered = new(completed, ApplyDelivery(completed.Tick, prepared));
            });
            // Only the step's explicit NotApplied classification is safe; consequence failures never enter this catch.
            if (notApplied is not null) ExceptionDispatchInfo.Capture(notApplied).Throw();
            return delivered;
        }
        finally { _consuming = false; }
    }
}
