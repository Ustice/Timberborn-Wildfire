using Wildfire.Core;

namespace Wildfire.Unity;

public static class FireSimChangeUpload
{
    public const int UInt32WordsPerChange = ComputeBufferGrid.ChangeStrideBytes / sizeof(uint);

    private const uint SetCellMask = 1u << 0;
    private const uint SetWaterMask = 1u << 1;
    private const uint SetFuelMask = 1u << 2;
    private const uint SetHeatMask = 1u << 3;
    private const uint SetFlammabilityMask = 1u << 4;
    private const uint SetBurningLevelMask = 1u << 5;
    private const uint SetTerrainMask = 1u << 6;
    private const uint SetAshMask = 1u << 7;
    private const uint SetAshContaminationMask = 1u << 8;
    private const uint SetSmokeMask = 1u << 9;
    private const uint SetSmokeContaminationMask = 1u << 10;

    public static uint[] Encode(ReadOnlySpan<FireSimChange> changes, int capacity)
    {
        if (capacity < changes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Change buffer capacity cannot be smaller than the uploaded change count.");
        }

        uint[] encoded = new uint[checked(capacity * UInt32WordsPerChange)];

        for (int index = 0; index < changes.Length; index++)
        {
            WriteChange(encoded, index, changes[index]);
        }

        return encoded;
    }

    private static void WriteChange(uint[] encoded, int changeIndex, FireSimChange change)
    {
        int offset = changeIndex * UInt32WordsPerChange;
        encoded[offset] = checked((uint)change.CellIndex);
        encoded[offset + 1] = GetSetMask(change);
        encoded[offset + 2] = GetAddFields(change);
        encoded[offset + 3] = GetSetValues(change);
    }

    private static uint GetSetMask(FireSimChange change)
    {
        uint mask = 0u;
        mask |= change.SetCell.HasValue ? SetCellMask : 0u;
        mask |= change.SetWater.HasValue ? SetWaterMask : 0u;
        mask |= change.SetFuel.HasValue ? SetFuelMask : 0u;
        mask |= change.SetHeat.HasValue ? SetHeatMask : 0u;
        mask |= change.SetFlammability.HasValue ? SetFlammabilityMask : 0u;
        mask |= change.SetBurningLevel.HasValue ? SetBurningLevelMask : 0u;
        mask |= change.SetTerrain.HasValue ? SetTerrainMask : 0u;
        mask |= change.SetAsh.HasValue ? SetAshMask : 0u;
        mask |= change.SetAshContamination.HasValue ? SetAshContaminationMask : 0u;
        mask |= change.SetSmoke.HasValue ? SetSmokeMask : 0u;
        mask |= change.SetSmokeContamination.HasValue ? SetSmokeContaminationMask : 0u;
        return mask;
    }

    private static uint GetAddFields(FireSimChange change)
    {
        return Clamp(change.AddHeat, 15u) |
            (Clamp(change.AddFuel, 15u) << 4) |
            (Clamp(change.AddAsh, 3u) << 8) |
            (Clamp(change.RemoveAsh, 3u) << 10) |
            (Clamp(change.SetAsh, 3u) << 12) |
            (Clamp(change.SetAshContamination, 7u) << 14) |
            (Clamp(change.SetSmoke, 7u) << 17) |
            (Clamp(change.SetSmokeContamination, 7u) << 20);
    }

    private static uint GetSetValues(FireSimChange change)
    {
        return ((uint)(change.SetCell ?? 0) & 0xFFFFu) |
            (Clamp(change.SetWater, 3u) << 16) |
            (Clamp(change.SetFuel, 15u) << 18) |
            (Clamp(change.SetHeat, 15u) << 22) |
            (Clamp(change.SetFlammability, 3u) << 26) |
            (Clamp(change.SetBurningLevel, 7u) << 28) |
            (Clamp(change.SetTerrain, 1u) << 31);
    }

    private static uint Clamp(byte? value, uint max)
    {
        return Math.Min((uint)(value ?? 0), max);
    }
}
