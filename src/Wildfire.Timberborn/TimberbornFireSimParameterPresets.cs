using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed record TimberbornFireSimParameterPreset(string Name, FireSimParameters Parameters)
{
    public string StatusToken =>
        $"fire_sim_preset={TimberbornQaCommandBridge.FormatToken(Name)} " +
        $"fire_ignition_base_heat={Parameters.FireIgnitionBaseHeat} " +
        $"fire_burning_neighbor_heat_bonus={Parameters.FireBurningNeighborHeatBonus} " +
        $"fire_water_suppression_heat={Parameters.FireWaterSuppressionHeat} " +
        $"fire_fuel_burn_down={Parameters.FireFuelBurnDownPressureNumerator}/{Parameters.FireFuelBurnDownPressureDenominator}";
}

public static class TimberbornFireSimParameterPresets
{
    public const string DefaultName = "default";
    public const string SlowReactableName = "slow-reactable";
    public const string HarshName = "harsh";
    public const string WildfireName = "wildfire";
    public const string ConservativeName = "conservative";

    private static readonly TimberbornFireSimParameterPreset[] Presets =
    {
        new(DefaultName, FireSimParameters.Default),
        new(
            SlowReactableName,
            FireSimParameters.Default with
            {
                FireIgnitionBaseHeat = 12u,
                FireBurningNeighborHeatBonus = 3u,
                FireBurningNeighborDirectHeat = 1u,
                FireFuelBurnDownPressureNumerator = 1u,
                FireFuelBurnDownPressureDenominator = 2u,
                FireCoolingBase = 1u,
                VisualSmokeFuelWeight = 0.6f,
            }),
        new(
            HarshName,
            FireSimParameters.Default with
            {
                FireIgnitionBaseHeat = 9u,
                FireWaterIgnitionPenalty = 1u,
                FireBurningNeighborHeatBonus = 7u,
                FireBurningNeighborDirectHeat = 2u,
                FireWaterSuppressionHeat = 1u,
                FireFuelBurnDownPressureNumerator = 1u,
                FireFuelBurnDownPressureDenominator = 1u,
                VisualFireBaseIntensity = 0.55f,
                VisualSmokeHeatWeight = 0.35f,
            }),
        new(
            WildfireName,
            FireSimParameters.Default with
            {
                FireIgnitionBaseHeat = 8u,
                FireWaterIgnitionPenalty = 1u,
                FireRetainedHeatWeight = 2u,
                FireSpreadHeatWeight = 5u,
                FireBurningNeighborHeatBonus = 16u,
                FireBurningNeighborDirectHeat = 6u,
                FireWaterSuppressionHeat = 1u,
                FireWaterEvaporationHeat = 7u,
                FireFlammabilityBurnPressure = 3u,
                FireBurnHeatBase = 4u,
                FireCoolingBase = 0u,
                FireHeatLossCoolingDivisor = 7u,
                FireFuelBurnDownPressureNumerator = 1u,
                FireFuelBurnDownPressureDenominator = 6u,
                VisualFireBaseIntensity = 0.82f,
                VisualFireHeatWeight = 0.6f,
                VisualSmokeBaseIntensity = 0.34f,
                VisualSmokeFuelWeight = 0.82f,
                VisualSmokeHeatWeight = 0.55f,
                VisualAshBaseIntensity = 0.38f,
                VisualAshFuelWeight = 0.78f,
                VisualAshHeatWeight = 0.5f,
                VisualVisibilityHeatWeight = 0.95f,
                VisualVisibilitySmokeWeight = 1.0f,
                VisualVisibilityAshWeight = 0.9f,
            }),
        new(
            ConservativeName,
            FireSimParameters.Default with
            {
                FireIgnitionBaseHeat = 13u,
                FireWaterIgnitionPenalty = 3u,
                FireBurningNeighborHeatBonus = 2u,
                FireBurningNeighborDirectHeat = 0u,
                FireWaterSuppressionHeat = 3u,
                FireFuelBurnDownPressureNumerator = 1u,
                FireFuelBurnDownPressureDenominator = 4u,
                FireCoolingBase = 2u,
                VisualFireBaseIntensity = 0.35f,
                VisualSmokeBaseIntensity = 0.08f,
            }),
    };

    private static readonly IReadOnlyDictionary<string, TimberbornFireSimParameterPreset> PresetsByName =
        Presets.ToDictionary(static preset => preset.Name, StringComparer.OrdinalIgnoreCase);

    public static TimberbornFireSimParameterPreset Default => PresetsByName[DefaultName];

    public static IReadOnlyList<TimberbornFireSimParameterPreset> All => Presets;

    public static bool TryGet(string? name, out TimberbornFireSimParameterPreset preset)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            preset = Default;
            return false;
        }

        return PresetsByName.TryGetValue(name.Trim(), out preset!);
    }
}

public sealed class TimberbornFireSimParameterPresetState : ITimberbornQaFireSimParameterPresetSelector
{
    public TimberbornFireSimParameterPreset CurrentPreset { get; private set; } =
        TimberbornFireSimParameterPresets.Default;

    public TimberbornQaFireSimParameterPresetResult SelectFireSimParameterPreset(string presetName)
    {
        if (!TimberbornFireSimParameterPresets.TryGet(presetName, out TimberbornFireSimParameterPreset? preset))
        {
            throw new ArgumentException(
                $"Unknown fire sim preset '{presetName}'. Known presets: {string.Join(",", TimberbornFireSimParameterPresets.All.Select(static known => known.Name))}.",
                nameof(presetName));
        }

        CurrentPreset = preset;
        return new TimberbornQaFireSimParameterPresetResult(CurrentPreset.Name, CurrentPreset.Parameters);
    }
}
