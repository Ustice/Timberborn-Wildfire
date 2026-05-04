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
