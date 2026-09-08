using Wildfire.Core;

namespace Wildfire.Timberborn.Resources;

public readonly record struct NativeResourceAttempt(bool Prepared, GpuFireStepResult? Result);
public enum NativeResourceProducerKind { Water, AshCollection, FertilizerApplication }

/// <summary>Completed admissions rotate across three concrete kinds. A prepared full batch runs one ordinary step.</summary>
public sealed class NativeResourceStepScheduler
{
    private NativeResourceProducerKind _next;
    public GpuFireStepResult Tick(Func<NativeResourceProducerKind, NativeResourceAttempt> attempt, Func<GpuFireStepResult> ordinaryTick)
    {
        for (int offset = 0; offset < 3; offset++)
        {
            var kind = (NativeResourceProducerKind)(((int)_next + offset) % 3);
            var result = attempt(kind);
            if (result.Prepared) return result.Result ?? ordinaryTick();
        }
        return ordinaryTick();
    }
    public void Completed(NativeResourceProducerKind kind) => _next = (NativeResourceProducerKind)(((int)kind + 1) % 3);
    public void ResetForWorldLoad() => _next = NativeResourceProducerKind.Water;
}
