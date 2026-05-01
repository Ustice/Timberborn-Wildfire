namespace Wildfire.Core;

public static class FireRandom
{
    public static uint Hash(uint cellIndex, uint tick, uint seed)
    {
        uint x = cellIndex ^ (tick * 747796405u) ^ seed;
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return x;
    }
}
