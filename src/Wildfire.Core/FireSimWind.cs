namespace Wildfire.Core;

public readonly record struct FireSimWind(float DirectionX, float DirectionY, float Strength)
{
    public static FireSimWind None { get; } = new(0f, 0f, 0f);

    public FireSimWind Normalized()
    {
        float length = MathF.Sqrt((DirectionX * DirectionX) + (DirectionY * DirectionY));
        if (length <= 0.0001f || Strength <= 0f)
        {
            return None;
        }

        return new FireSimWind(
            DirectionX / length,
            DirectionY / length,
            Math.Clamp(Strength, 0f, 1f));
    }
}
