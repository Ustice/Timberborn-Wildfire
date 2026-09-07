using System.Text.Json.Nodes;
using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed class ShaderSnapshotMaterialHandoffTests
{
    [Fact]
    public void OwnerSlotsAndExactBatchWordsRoundTripWithoutReencoding()
    {
        var batch = new FireSimMaterialHandoffBatch(1, [FireSimMaterialHandoffRequest.Remove(0, new(1, 11), true)]);
        var fixture = new ShaderSnapshotFixture(1, "material-protocol", 1, new(1, 1, 1), new(0, 0, 1), [0],
            InitialTargetIds: [1], InitialSlotIds: [11], MaterialHandoffs: [ShaderSnapshotMaterialHandoff.Encode(1, batch)]);
        var restored = ShaderSnapshotFixtureLoader.Load(ShaderSnapshotJson.SerializeFixture(fixture));
        Assert.Equal(fixture.InitialTargetIds, restored.InitialTargetIds);
        Assert.Equal(fixture.InitialSlotIds, restored.InitialSlotIds);
        Assert.Equal(fixture.MaterialHandoffs[0].Requests, restored.MaterialHandoffs![0].Requests);
        Assert.Throws<InvalidDataException>(() => ShaderSnapshotJson.SerializeFixture(fixture with { InitialSlotIds = null }));
    }

    [Fact]
    public void CaptureRetainsOriginatingOwnerAndRawReceiptsAndComparisonUsesSuppliedEvidence()
    {
        var expected = Capture();
        var restored = ShaderSnapshotJson.Load(ShaderSnapshotJson.Serialize(expected));
        Assert.Equal(1u, Assert.Single(Assert.Single(restored.Ticks).Deltas).TargetId);
        Assert.True(ShaderSnapshotComparison.Create(expected, restored).Matches);
        foreach (var modified in new[]
        {
            restored with { FinalTargetIds = [2] },
            restored with { FinalSlotIds = [12] },
            restored with { Ticks = [restored.Ticks[0] with { MaterialHeader = [2, 1, 1, uint.MaxValue] }] },
            restored with { Ticks = [restored.Ticks[0] with { MaterialReceipts = [9] }] },
            restored with { Ticks = [restored.Ticks[0] with { Deltas = [new(0, 0, 1, 2)] }] },
        }) Assert.False(ShaderSnapshotComparison.Create(expected, modified).Matches);
    }

    [Fact]
    public void LegacyMissingMaterialMetadataIsUnspecifiedAndMissingDeltaOwnerDefaultsToZero()
    {
        var capture = Capture() with { Ticks = [new(1, 1, [new(0, 0, 1)])] };
        var json = JsonNode.Parse(ShaderSnapshotJson.Serialize(capture))!.AsObject();
        json.Remove("finalTargetIds"); json.Remove("finalSlotIds");
        json["perTickDeltas"]![0]!["deltas"]![0]!.AsObject().Remove("targetId");
        var legacy = ShaderSnapshotJson.Load(json.ToJsonString());
        Assert.Equal(0u, legacy.Ticks[0].Deltas[0].TargetId);
        Assert.True(ShaderSnapshotComparison.Create(legacy, capture).Matches);
    }

    private static ShaderSnapshotCapture Capture() => new("material-protocol", 1, new(1, 1, 1), 1, [1],
        [new(1, 1, [new(0, 0, 1, 1)], MaterialHeader: [1, 1, 1, uint.MaxValue], MaterialReceipts: [0, 1, 11, 0, 0, 2, 21, 1, 0, 1])],
        FinalTargetIds: [2], FinalSlotIds: [21]);
}
