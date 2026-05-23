using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed record TimberbornFireSimParameterPreset(string Name, FireSimParameters Parameters)
{
    public string StatusToken =>
        $"fire_sim_preset={TimberbornQaCommandBridge.FormatToken(Name)} " +
        $"fire_ignition_point={Parameters.IgnitionPoint} " +
        $"fire_water_ignition_penalty={Parameters.FireWaterIgnitionPenalty} " +
        $"fire_fuel_heat_weight={Parameters.FireFuelHeatWeight} " +
        $"fire_fuel_burn_down={Parameters.FireFuelBurnDownPressureNumerator}/{Parameters.FireFuelBurnDownPressureDenominator} " +
        $"fire_step_interval_ticks={Parameters.FireCellStepIntervalTicks}";
}

public static class TimberbornFireSimParameterPresets
{
    public const string DefaultName = "default";
    public const string SlowReactableName = "slow-reactable";
    public const string HarshName = "harsh";
    public const string WildfireName = "wildfire";
    public const string ConservativeName = "conservative";
    public const string HighThresholdHighBonusName = "high-threshold-high-bonus";

    private static readonly TimberbornFireSimParameterPreset[] Presets =
    {
        new(DefaultName, FireSimParameters.Default),
        new(
            SlowReactableName,
            FireSimParameters.Default with
            {
                FireFuelBurnDownPressureNumerator = 1u,
                FireFuelBurnDownPressureDenominator = 2u,
                FireCellStepIntervalTicks = 6u,
                VisualSmokeFuelWeight = 0.6f,
            }),
        new(
            HarshName,
            FireSimParameters.Default with
            {
                IgnitionPoint = 9u,
                FireWaterIgnitionPenalty = 1u,
                FireFuelBurnDownPressureNumerator = 1u,
                FireFuelBurnDownPressureDenominator = 1u,
                VisualFireBaseIntensity = 0.55f,
                VisualSmokeHeatWeight = 0.35f,
            }),
        new(
            WildfireName,
            FireSimParameters.Default with
            {
                IgnitionPoint = 6u, //8u
                FireWaterIgnitionPenalty = 1u,
                FireBurnHeatBase = 4u,
                FireFuelBurnDownPressureNumerator = 1u,
                FireFuelBurnDownPressureDenominator = 10u, //6u
                VisualFireBaseIntensity = 0.82f,
                VisualFireHeatWeight = 0.6f,
                VisualSmokeBaseIntensity = 0.34f,
                VisualSmokeFuelWeight = 0.82f,
                VisualSmokeHeatWeight = 0.55f,
                AshPresentationBaseIntensity = 0.38f,
                AshPresentationFuelWeight = 0.78f,
                AshPresentationHeatWeight = 0.5f,
                VisualVisibilityHeatWeight = 0.95f,
                VisualVisibilitySmokeWeight = 1.0f,
                AshPresentationVisibilityWeight = 0.9f,
            }),
        new(
            ConservativeName,
            FireSimParameters.Default with
            {
                IgnitionPoint = 13u,
                FireWaterIgnitionPenalty = 3u,
                FireFuelBurnDownPressureNumerator = 1u,
                FireFuelBurnDownPressureDenominator = 4u,
                VisualFireBaseIntensity = 0.35f,
                VisualSmokeBaseIntensity = 0.08f,
            }),
        new(
            HighThresholdHighBonusName,
            FireSimParameters.Default with
            {
                IgnitionPoint = 5u,
                FireWaterIgnitionPenalty = 0u,
                FireBurnHeatBase = 0u,
                FireFuelHeatWeight = 6u,
                FireFuelBurnDownPressureNumerator = 2u,
                FireFuelBurnDownPressureDenominator = 1u,
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
