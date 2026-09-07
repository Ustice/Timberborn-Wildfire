using System.Runtime.InteropServices;

namespace Wildfire.Core;

public static class FireSimGpuProtocol
{
    public const int UInt32WordsPerChange = 4;
    public const int ChangeStrideBytes = sizeof(uint) * UInt32WordsPerChange;
    public const int UInt32WordsPerDelta = 5;
    public const int DeltaStrideBytes = sizeof(uint) * UInt32WordsPerDelta;

    // Each uploaded command and each simulated cell may independently append one delta.
    public static int GetDeltaCapacity(int cellCount, int changeCapacity)
    {
        if (cellCount <= 0 || changeCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellCount), "Cell count must be positive and change capacity nonnegative.");
        }

        return checked(cellCount + changeCapacity);
    }

    public static FireSimGpuChange[] EncodeChanges(ReadOnlySpan<FireSimChange> changes)
    {
        FireSimGpuChange[] encoded = new FireSimGpuChange[changes.Length];
        for (int index = 0; index < changes.Length; index++)
        {
            encoded[index] = EncodeChange(changes[index]);
        }

        return encoded;
    }

    public static FireSimGpuChange EncodeChange(FireSimChange change)
    {
        if (change.MaterialHandoff is { } material)
        {
            if (change.CellIndex != 0 || change with { MaterialHandoff = null } != new FireSimChange(0))
                throw new ArgumentException("Material batch marker cannot share an ordinary command.", nameof(change));
            return FireSimMaterialHandoffProtocol.EncodeMarker(material);
        }
        ValidateCollection(change);
        return new FireSimGpuChange(
            checked((uint)change.CellIndex),
            GetSetMask(change),
            GetAddFields(change),
            GetSetValues(change));
    }

    public const uint CollectCleanAshMask = 1u << 11;
    public const int CollectionRequestedShift = 25;
    public const int CollectionRemovedShift = 27;
    public const uint CollectionReceiptValidMask = 1u << 29;
    public const uint CollectionReceiptMask = (3u << CollectionRemovedShift) | CollectionReceiptValidMask;

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

    public static uint[] EncodeWords(ReadOnlySpan<FireSimChange> changes, int capacity)
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
        FireSimGpuChange value = EncodeChange(change);
        int offset = changeIndex * UInt32WordsPerChange;
        encoded[offset] = value.CellIndex;
        encoded[offset + 1] = value.SetMask;
        encoded[offset + 2] = value.AddFields;
        encoded[offset + 3] = value.SetValues;
    }

    private static uint GetSetMask(FireSimChange change)
    {
        uint mask = change.CollectCleanAsh.HasValue ? CollectCleanAshMask : 0u;
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
            (Clamp(change.SetSmokeContamination, 7u) << 20) |
            (Clamp(change.AddWater, 3u) << 23) |
            (Clamp(change.CollectCleanAsh, 3u) << CollectionRequestedShift);
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

    private static void ValidateCollection(FireSimChange change)
    {
        if (!change.CollectCleanAsh.HasValue) return;
        if (change.CollectCleanAsh.Value > 3)
            throw new ArgumentOutOfRangeException(nameof(change), "Clean ash request must be between zero and three.");
        if (change with { CollectCleanAsh = null } != new FireSimChange(change.CellIndex))
            throw new ArgumentException("Clean ash collection cannot share a command with another mutation.", nameof(change));
    }

    public static FireSimAshCollectionReceipt DecodeCollectionReceipt(FireSimGpuChange applied, FireSimAshCollectionInput input)
    {
        var expected = EncodeChange(new FireSimChange(input.CellIndex, CollectCleanAsh: input.Requested));
        uint removed = (applied.AddFields >> CollectionRemovedShift) & 3u;
        if ((applied.AddFields & CollectionReceiptValidMask) == 0 ||
            applied.CellIndex != expected.CellIndex || applied.SetMask != expected.SetMask ||
            applied.SetValues != expected.SetValues ||
            (applied.AddFields & ~CollectionReceiptMask) != expected.AddFields || removed > input.Requested)
            throw new InvalidOperationException("Missing or invalid GPU ash collection receipt.");
        return new FireSimAshCollectionReceipt(input.CellIndex, input.Requested, (byte)removed);
    }

    private static uint Clamp(byte? value, uint max)
    {
        return Math.Min((uint)(value ?? 0), max);
    }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct FireSimGpuChange
{
    public readonly uint CellIndex;
    public readonly uint SetMask;
    public readonly uint AddFields;
    public readonly uint SetValues;

    public FireSimGpuChange(uint cellIndex, uint setMask, uint addFields, uint setValues)
    {
        CellIndex = cellIndex;
        SetMask = setMask;
        AddFields = addFields;
        SetValues = setValues;
    }
}
