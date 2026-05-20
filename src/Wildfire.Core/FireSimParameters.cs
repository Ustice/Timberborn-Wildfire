namespace Wildfire.Core;

public readonly record struct FireSimParameters(
    float VisualFireBaseIntensity,
    float VisualFireHeatWeight,
    float VisualSmokeBaseIntensity,
    float VisualSmokeFuelWeight,
    float VisualSmokeHeatWeight,
    float AshPresentationBaseIntensity,
    float AshPresentationFuelWeight,
    float AshPresentationHeatWeight,
    float VisualVisibilityHeatWeight,
    float VisualVisibilitySmokeWeight,
    float AshPresentationVisibilityWeight,
    uint IgnitionPoint,
    uint FireWaterIgnitionPenalty,
    uint FireBurnHeatBase,
    uint FireFuelHeatWeight,
    uint FireFuelBurnDownPressureNumerator,
    uint FireFuelBurnDownPressureDenominator,
    uint FireFuelBurnDownRollSeed,
    uint FireCellStepIntervalTicks)
{
    public static readonly FireSimParameters Default = new(
        VisualFireBaseIntensity: 0.45f,
        VisualFireHeatWeight: 0.55f,
        VisualSmokeBaseIntensity: 0.12f,
        VisualSmokeFuelWeight: 0.52f,
        VisualSmokeHeatWeight: 0.24f,
        AshPresentationBaseIntensity: 0.18f,
        AshPresentationFuelWeight: 0.5f,
        AshPresentationHeatWeight: 0.32f,
        VisualVisibilityHeatWeight: 0.55f,
        VisualVisibilitySmokeWeight: 0.9f,
        AshPresentationVisibilityWeight: 0.8f,
        IgnitionPoint: 5u,
        FireWaterIgnitionPenalty: 2u,
        FireBurnHeatBase: 5u,
        FireFuelHeatWeight: 5u,
        FireFuelBurnDownPressureNumerator: 3u,
        FireFuelBurnDownPressureDenominator: 4u,
        FireFuelBurnDownRollSeed: 0x9E3779B9u,
        FireCellStepIntervalTicks: 1u);

    public FireSimParameters WithFuelBurnDown(uint numerator, uint denominator)
    {
        if (denominator == 0u)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator), denominator, "Fuel burn-down denominator must be positive.");
        }

        return this with
        {
            FireFuelBurnDownPressureNumerator = numerator,
            FireFuelBurnDownPressureDenominator = denominator,
        };
    }
}
