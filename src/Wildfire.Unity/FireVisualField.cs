using Wildfire.Core;

namespace Wildfire.Unity;

public static class FireVisualField
{
    public const int ChannelCount = 4;
    public const int StrideBytes = sizeof(float) * ChannelCount;

    public static FireVisualSample FromPackedCell(ushort cell)
    {
        return FromPackedCell(cell, FireSimParameters.Default);
    }

    public static FireVisualSample FromPackedCell(ushort cell, FireSimParameters parameters)
    {
        float fuel = PackedCell.Fuel(cell) / 15f;
        float heat = PackedCell.Heat(cell) / 15f;
        bool terrain = PackedCell.Terrain(cell) == 1;
        bool burning = PackedCell.IsBurning(cell);
        bool hotFuel = terrain && PackedCell.Fuel(cell) > 0 && PackedCell.Heat(cell) > 0;
        bool hotWetEdge = terrain && PackedCell.Water(cell) > 0 && PackedCell.Heat(cell) > 0;
        bool ashCandidate = terrain && PackedCell.Fuel(cell) <= 2 && PackedCell.Heat(cell) > 0;

        float fire = burning
            ? Saturate(parameters.VisualFireBaseIntensity + (heat * parameters.VisualFireHeatWeight))
            : 0f;
        float smoke = burning || hotFuel || hotWetEdge
            ? Saturate(parameters.VisualSmokeBaseIntensity +
                (fuel * parameters.VisualSmokeFuelWeight * (burning ? 1f : 0.45f)) +
                (heat * parameters.VisualSmokeHeatWeight))
            : 0f;
        float ash = ashCandidate
            ? Saturate(parameters.VisualAshBaseIntensity + ((1f - fuel) * parameters.VisualAshFuelWeight) + (heat * parameters.VisualAshHeatWeight))
            : 0f;
        float visibility = MathF.Max(
            heat * parameters.VisualVisibilityHeatWeight,
            MathF.Max(fire, MathF.Max(smoke * parameters.VisualVisibilitySmokeWeight, ash * parameters.VisualVisibilityAshWeight)));

        return new FireVisualSample(fire, smoke, ash, visibility);
    }

    private static float Saturate(float value)
    {
        return Math.Clamp(value, 0f, 1f);
    }
}

public readonly record struct FireVisualSample(float Fire, float Smoke, float Ash, float Visibility);
