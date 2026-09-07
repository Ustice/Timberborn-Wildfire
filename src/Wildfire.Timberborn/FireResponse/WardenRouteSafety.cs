namespace Wildfire.Timberborn.FireResponse;

public readonly record struct WardenRouteSample(float Distance, int Risk);

public static class WardenRouteSafety
{
    // Safe risk is 0..1. -1 denotes unavailable/out-of-world observation.
    public static bool CanTraverse(IReadOnlyList<WardenRouteSample> samples, bool escaping)
    {
        if (samples.Count == 0 || samples[0].Risk < 0) return false;
        var initialRisk = samples[0].Risk;
        var reachedSafety = initialRisk < 2;
        foreach (var sample in samples)
        {
            if (sample.Risk < 0) return false;
            if (sample.Risk < 2) { reachedSafety = true; continue; }
            if (!escaping || reachedSafety || sample.Distance > 1.5f || sample.Risk > initialRisk) return false;
        }
        return reachedSafety;
    }
}
