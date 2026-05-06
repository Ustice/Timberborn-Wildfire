namespace Wildfire.Core;

public readonly record struct FireSimParameters(
    float VisualFireBaseIntensity,
    float VisualFireHeatWeight,
    float VisualSmokeBaseIntensity,
    float VisualSmokeFuelWeight,
    float VisualSmokeHeatWeight,
    float VisualAshBaseIntensity,
    float VisualAshFuelWeight,
    float VisualAshHeatWeight,
    float VisualVisibilityHeatWeight,
    float VisualVisibilitySmokeWeight,
    float VisualVisibilityAshWeight,
    uint FireIgnitionBaseHeat,
    uint FireWaterIgnitionPenalty,
    uint FireWaterFuelLock,
    uint FireWaterEvaporationHeat,
    uint FireFlammabilityBurnPressure,
    uint FireWaterBurnPressurePenalty,
    uint FireBurnHeatBase,
    uint FireFuelHeatWeight,
    uint FireCoolingBase,
    uint FireHeatLossCoolingDivisor,
    uint FireFuelBurnDownPressureNumerator,
    uint FireFuelBurnDownPressureDenominator,
    uint FireFuelBurnDownRollSeed)
{
    public static readonly FireSimParameters Default = new(
        VisualFireBaseIntensity: 0.45f,
        VisualFireHeatWeight: 0.55f,
        VisualSmokeBaseIntensity: 0.12f,
        VisualSmokeFuelWeight: 0.52f,
        VisualSmokeHeatWeight: 0.24f,
        VisualAshBaseIntensity: 0.18f,
        VisualAshFuelWeight: 0.5f,
        VisualAshHeatWeight: 0.32f,
        VisualVisibilityHeatWeight: 0.55f,
        VisualVisibilitySmokeWeight: 0.9f,
        VisualVisibilityAshWeight: 0.8f,
        FireIgnitionBaseHeat: 11u,
        FireWaterIgnitionPenalty: 2u,
        FireWaterFuelLock: 5u,
        FireWaterEvaporationHeat: 10u,
        FireFlammabilityBurnPressure: 2u,
        FireWaterBurnPressurePenalty: 3u,
        FireBurnHeatBase: 1u,
        FireFuelHeatWeight: 2u,
        FireCoolingBase: 1u,
        FireHeatLossCoolingDivisor: 3u,
        FireFuelBurnDownPressureNumerator: 3u,
        FireFuelBurnDownPressureDenominator: 4u,
        FireFuelBurnDownRollSeed: 0x9E3779B9u);

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
