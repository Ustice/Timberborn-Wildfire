using Wildfire.Core;

namespace Wildfire.Unity;

public static class FireVisualField
{
    public const int ChannelCount = 4;
    public const int StrideBytes = sizeof(float) * ChannelCount;

    public static FireVisualSample FromPackedCell(ushort cell)
    {
        float fuel = PackedCell.Fuel(cell) / 15f;
        float heat = PackedCell.Heat(cell) / 15f;
        bool terrain = PackedCell.Terrain(cell) == 1;
        bool burning = PackedCell.IsBurning(cell);
        bool ashCandidate = terrain && PackedCell.Fuel(cell) <= 2 && PackedCell.Heat(cell) > 0;

        float fire = burning ? Saturate(0.35f + (heat * 0.65f)) : 0f;
        float smoke = burning ? Saturate(0.2f + (fuel * 0.35f) + (heat * 0.45f)) : 0f;
        float ash = ashCandidate ? Saturate((1f - fuel) * MathF.Max(0.25f, heat)) : 0f;
        float visibility = MathF.Max(heat, MathF.Max(fire, MathF.Max(smoke, ash)));

        return new FireVisualSample(fire, smoke, ash, visibility);
    }

    private static float Saturate(float value)
    {
        return Math.Clamp(value, 0f, 1f);
    }
}

public readonly record struct FireVisualSample(float Fire, float Smoke, float Ash, float Visibility);
