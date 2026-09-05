using System.Text.Json;
using Wildfire.Core;

namespace Wildfire.Unity;

// Carries the exact words uploaded by production, so Unity need not reimplement command encoding.
public sealed record ShaderSnapshotExternalChanges(int Tick, uint[] Words)
{
    public static ShaderSnapshotExternalChanges Encode(int tick, params FireSimChange[] changes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tick);
        return new ShaderSnapshotExternalChanges(tick, FireSimGpuProtocol.EncodeWords(changes, changes.Length));
    }

    internal static ShaderSnapshotExternalChanges[]? Read(JsonElement root, int cellCount)
    {
        if (!root.TryGetProperty("externalChanges", out JsonElement batches) || batches.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        ShaderSnapshotExternalChanges[] changes = batches.EnumerateArray()
            .Select(batch => new ShaderSnapshotExternalChanges(
                batch.GetProperty("tick").GetInt32(),
                batch.GetProperty("words").EnumerateArray().Select(word => word.GetUInt32()).ToArray()))
            .ToArray();
        Validate(changes, cellCount);
        return changes;
    }

    internal static void Validate(ShaderSnapshotExternalChanges[]? batches, int cellCount)
    {
        HashSet<int> ticks = [];
        foreach (ShaderSnapshotExternalChanges batch in batches ?? [])
        {
            if (batch.Tick <= 0 || !ticks.Add(batch.Tick))
            {
                throw new InvalidDataException("External changes require one batch per positive simulation tick.");
            }

            if (batch.Words.Length % FireSimGpuProtocol.UInt32WordsPerChange != 0 ||
                batch.Words.Length / FireSimGpuProtocol.UInt32WordsPerChange > cellCount)
            {
                throw new InvalidDataException("External change words must contain complete commands within the grid upload capacity.");
            }

            for (int offset = 0; offset < batch.Words.Length; offset += FireSimGpuProtocol.UInt32WordsPerChange)
            {
                if (batch.Words[offset] >= cellCount)
                {
                    throw new InvalidDataException("External change cell index must be inside the fixture grid.");
                }
            }
        }
    }
}
