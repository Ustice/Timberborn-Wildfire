using Timberborn.TickSystem;
using Wildfire.Timberborn.Runtime;

namespace Wildfire.Timberborn.Ash;

// Unbound. Construct on the native thread and pass Read to the existing late ticker when adopted.
internal sealed class TimberbornAshGrowthReadiness
{
    private readonly int _nativeThreadId = Environment.CurrentManagedThreadId;
    private readonly TimberbornFireRuntime _runtime;
    private readonly ITickableSingletonService _ticks;

    internal TimberbornAshGrowthReadiness(TimberbornFireRuntime runtime, ITickableSingletonService ticks) =>
        (_runtime, _ticks) = (runtime ?? throw new ArgumentNullException(nameof(runtime)),
            ticks ?? throw new ArgumentNullException(nameof(ticks)));

    internal TimberbornAshGrowthObservation? Read()
    {
        RequireSettled();
        var observation = _runtime.ReadAshGrowthObservation();
        RequireSettled();
        return observation;
    }

    private void RequireSettled()
    {
        if (Environment.CurrentManagedThreadId != _nativeThreadId ||
            _ticks.IsStartingParallelTick || !_ticks.ParalleTicklIsFinished)
            throw new InvalidOperationException("Ash growth requires the settled native singleton tick window.");
    }
}
