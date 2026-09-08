using Timberborn.Growing;
using Timberborn.NaturalResourcesLifecycle;

namespace Wildfire.Timberborn.Ash;

internal static class TimberbornAshGrowthRate
{
    internal static void Validate(float elapsedDays, float multiplier, float duration)
    {
        if (float.IsNaN(elapsedDays) || float.IsInfinity(elapsedDays) || elapsedDays < 0 ||
            float.IsNaN(multiplier) || float.IsInfinity(multiplier) ||
            float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0)
            throw new InvalidOperationException("Ash growth requires finite elapsed time, multiplier, and positive native duration.");
        if (float.IsInfinity(elapsedDays / duration))
            throw new InvalidOperationException("Ash growth interval exceeds the native duration range.");
    }

    internal static float ReadDuration(Growable growable)
    {
        if (growable.GetComponent<LivingNaturalResource>() is null || growable.GetComponent<DyingNaturalResource>() is null)
            throw new InvalidOperationException("Ash growth candidate lacks native lifecycle components.");
        float duration = growable.GrowthTimeInDays;
        Validate(0, 1, duration);
        return duration;
    }

    // The caller owns exact entity/mapping validation and the existing native mutation transaction.
    internal static bool Advance(Growable growable, float elapsedDays, float multiplier)
    {
        float duration = ReadDuration(growable);
        Validate(elapsedDays, multiplier, duration);
        var living = growable.GetComponent<LivingNaturalResource>();
        var dying = growable.GetComponent<DyingNaturalResource>();
        if (elapsedDays == 0 || multiplier <= 1 || growable.IsGrown || !growable.GrowthInProgress || living.IsDead || dying.IsDying)
            return false;
        float fraction = Math.Clamp(multiplier - 1f, 0f, .10f) * (elapsedDays / duration);
        if (fraction <= 0) return false;
        growable.IncreaseGrowthProgress(fraction);
        return true;
    }
}
