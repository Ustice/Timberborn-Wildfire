namespace Wildfire.Core;

public static class PackedCell
{
    private const ushort FuelMask = 0b0000_0000_0000_1111;
    private const ushort HeatMask = 0b0000_0000_1111_0000;
    private const ushort FlammabilityMask = 0b0000_0011_0000_0000;
    private const ushort WaterMask = 0b0000_1100_0000_0000;
    private const ushort TerrainMask = 0b0001_0000_0000_0000;
    private const ushort HeatLossMask = 0b1110_0000_0000_0000;

    public static ushort Pack(int fuel, int heat, int flammability, int water, int terrain, int heatLoss)
    {
        return (ushort)(
            ((fuel & 0b1111) << 0) |
            ((heat & 0b1111) << 4) |
            ((flammability & 0b11) << 8) |
            ((water & 0b11) << 10) |
            ((terrain & 0b1) << 12) |
            ((heatLoss & 0b111) << 13));
    }

    public static int Fuel(ushort cell) => (cell >> 0) & 0b1111;

    public static int Heat(ushort cell) => (cell >> 4) & 0b1111;

    public static int Flammability(ushort cell) => (cell >> 8) & 0b11;

    public static int Water(ushort cell) => (cell >> 10) & 0b11;

    public static int Terrain(ushort cell) => (cell >> 12) & 0b1;

    public static int HeatLoss(ushort cell) => (cell >> 13) & 0b111;

    public static ushort SetFuel(ushort cell, int fuel)
    {
        return (ushort)((cell & ~FuelMask) | ((fuel & 0b1111) << 0));
    }

    public static ushort SetHeat(ushort cell, int heat)
    {
        return (ushort)((cell & ~HeatMask) | ((heat & 0b1111) << 4));
    }

    public static ushort SetFlammability(ushort cell, int flammability)
    {
        return (ushort)((cell & ~FlammabilityMask) | ((flammability & 0b11) << 8));
    }

    public static ushort SetWater(ushort cell, int water)
    {
        return (ushort)((cell & ~WaterMask) | ((water & 0b11) << 10));
    }

    public static ushort SetTerrain(ushort cell, int terrain)
    {
        return (ushort)((cell & ~TerrainMask) | ((terrain & 0b1) << 12));
    }

    public static ushort SetHeatLoss(ushort cell, int heatLoss)
    {
        return (ushort)((cell & ~HeatLossMask) | ((heatLoss & 0b111) << 13));
    }

    public static bool IsBurning(ushort cell)
    {
        int ignitionThreshold = 12 - Flammability(cell) + Water(cell);
        return Terrain(cell) == 1 && Fuel(cell) > 0 && Heat(cell) >= ignitionThreshold;
    }
}
