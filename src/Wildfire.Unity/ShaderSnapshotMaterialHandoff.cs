using System.Text.Json;
using Wildfire.Core;

namespace Wildfire.Unity;

/// <summary>Exact production request words; raw fixtures may deliberately exercise GPU rejection.</summary>
public sealed record ShaderSnapshotMaterialHandoff(int Tick, uint[] Requests)
{
    public static ShaderSnapshotMaterialHandoff Encode(int tick, FireSimMaterialHandoffBatch batch) =>
        new(tick, FireSimMaterialHandoffProtocol.EncodeRequests(batch));

    internal static ShaderSnapshotMaterialHandoff[]? Read(JsonElement root) =>
        root.TryGetProperty("materialHandoffs", out var values) && values.ValueKind != JsonValueKind.Null
            ? values.EnumerateArray().Select(value => new ShaderSnapshotMaterialHandoff(
                value.GetProperty("tick").GetInt32(), ReadWords(value, "requests") ?? [])).ToArray() : null;

    internal static uint[]? ReadWords(JsonElement root, string name) =>
        root.TryGetProperty(name, out var words) && words.ValueKind != JsonValueKind.Null
            ? words.EnumerateArray().Select(word => word.GetUInt32()).ToArray() : null;

    internal static void Validate(ShaderSnapshotFixture fixture)
    {
        if ((fixture.InitialTargetIds is null) != (fixture.InitialSlotIds is null))
            throw new InvalidDataException("Fixture owner and slot arrays must be supplied together.");
        if (fixture.InitialTargetIds is { } targets && fixture.InitialSlotIds is { } slots)
        {
            ComputeGridValidation.RequireCellCount(fixture.Grid, targets.Length, "initialTargetIds");
            ComputeGridValidation.RequireCellCount(fixture.Grid, slots.Length, "initialSlotIds");
            for (int i = 0; i < targets.Length; i++) _ = new FireSimMaterialIdentity(targets[i], slots[i]);
        }
        HashSet<int> ticks = [];
        foreach (var batch in fixture.MaterialHandoffs ?? [])
        {
            if (batch.Tick <= 0 || !ticks.Add(batch.Tick) || batch.Requests.Length == 0 ||
                batch.Requests.Length % FireSimMaterialHandoffProtocol.RequestWords != 0 ||
                batch.Requests.Length / FireSimMaterialHandoffProtocol.RequestWords > fixture.Grid.CellCount)
                throw new InvalidDataException("Material fixture requires one bounded complete request batch per positive tick.");
        }
    }
}
