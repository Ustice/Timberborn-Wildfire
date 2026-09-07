using Wildfire.Core;

namespace Wildfire.Timberborn.Resources;

public readonly record struct NativeResourceAttempt(bool Prepared, GpuFireStepResult? Result);

/// <summary>Two producer kinds alternate committed admissions. A full batch falls through to one ordinary tick.</summary>
public sealed class NativeResourceStepScheduler
{
    private bool _preferAsh;
    public GpuFireStepResult Tick(Func<bool, NativeResourceAttempt> attempt, Func<GpuFireStepResult> ordinaryTick)
    {
        var first = _preferAsh;
        var result = attempt(first);
        if (!result.Prepared) result = attempt(!first);
        return result.Result ?? ordinaryTick();
    }
    public void Committed(bool ash) => _preferAsh = !ash;
    public void ResetForWorldLoad() => _preferAsh = false;
}
