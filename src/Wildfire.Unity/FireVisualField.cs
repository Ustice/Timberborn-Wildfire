using Wildfire.Core;

namespace Wildfire.Unity;

public static class FireVisualField
{
    public const int ChannelCount = 4;
    public const int StrideBytes = sizeof(float) * ChannelCount;
    public const float FireBaseIntensity = 0.45f;
    public const float FireHeatWeight = 0.55f;
    public const float SmokeBaseIntensity = 0.12f;
    public const float SmokeFuelWeight = 0.52f;
    public const float SmokeHeatWeight = 0.24f;
    public const float AshBaseIntensity = 0.18f;
    public const float AshFuelWeight = 0.5f;
    public const float AshHeatWeight = 0.32f;
    public const float VisibilityHeatWeight = 0.55f;
    public const float VisibilitySmokeWeight = 0.9f;
    public const float VisibilityAshWeight = 0.8f;

    public static FireVisualSample FromPackedCell(ushort cell)
    {
        float fuel = PackedCell.Fuel(cell) / 15f;
        float heat = PackedCell.Heat(cell) / 15f;
        bool terrain = PackedCell.Terrain(cell) == 1;
        bool burning = PackedCell.IsBurning(cell);
        bool ashCandidate = terrain && PackedCell.Fuel(cell) <= 2 && PackedCell.Heat(cell) > 0;

        float fire = burning ? Saturate(FireBaseIntensity + (heat * FireHeatWeight)) : 0f;
        float smoke = burning ? Saturate(SmokeBaseIntensity + (fuel * SmokeFuelWeight) + (heat * SmokeHeatWeight)) : 0f;
        float ash = ashCandidate ? Saturate(AshBaseIntensity + ((1f - fuel) * AshFuelWeight) + (heat * AshHeatWeight)) : 0f;
        float visibility = MathF.Max(
            heat * VisibilityHeatWeight,
            MathF.Max(fire, MathF.Max(smoke * VisibilitySmokeWeight, ash * VisibilityAshWeight)));

        return new FireVisualSample(fire, smoke, ash, visibility);
    }

    private static float Saturate(float value)
    {
        return Math.Clamp(value, 0f, 1f);
    }
}

public readonly record struct FireVisualSample(float Fire, float Smoke, float Ash, float Visibility);
