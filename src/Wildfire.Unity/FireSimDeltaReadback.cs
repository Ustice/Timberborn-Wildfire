using Wildfire.Core;

namespace Wildfire.Unity;

public static class FireSimDeltaReadback
{
    public const int UInt32WordsPerDelta = ComputeBufferGrid.DeltaStrideBytes / sizeof(uint);

    public static CellDelta[] Read(IAppendComputeBufferHandle deltas)
    {
        ArgumentNullException.ThrowIfNull(deltas);

        int deltaCount = deltas.ReadAppendCounter();

        if (deltaCount < 0 || deltaCount > deltas.Count)
        {
            throw new InvalidOperationException($"GPU delta counter returned {deltaCount}, but buffer capacity is {deltas.Count}.");
        }

        if (deltaCount == 0)
        {
            return [];
        }

        uint[] encoded = deltas.ReadAppendedData(deltaCount);
        return Decode(encoded, deltaCount);
    }

    public static CellDelta[] Decode(ReadOnlySpan<uint> encoded, int deltaCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(deltaCount);

        int expectedWords = checked(deltaCount * UInt32WordsPerDelta);

        if (encoded.Length < expectedWords)
        {
            throw new ArgumentException($"Delta readback contained {encoded.Length} words, expected at least {expectedWords}.", nameof(encoded));
        }

        CellDelta[] decoded = new CellDelta[deltaCount];

        for (int index = 0; index < deltaCount; index++)
        {
            int offset = index * UInt32WordsPerDelta;
            uint cellIndex = encoded[offset];

            if (cellIndex > int.MaxValue)
            {
                throw new InvalidOperationException($"GPU delta cell index {cellIndex} does not fit the C# CellDelta contract.");
            }

            decoded[index] = new CellDelta(
                checked((int)cellIndex),
                ToPackedCell(encoded[offset + 1], "old"),
                ToPackedCell(encoded[offset + 2], "new"));
        }

        return decoded;
    }

    private static ushort ToPackedCell(uint value, string fieldName)
    {
        if (value > ushort.MaxValue)
        {
            throw new InvalidOperationException($"GPU delta {fieldName} cell value {value} does not fit the packed 16-bit cell contract.");
        }

        return checked((ushort)value);
    }
}
