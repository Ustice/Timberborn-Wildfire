namespace Wildfire.Core.Tests;

public sealed class FireSimSnapshotValidationTests
{
    [Fact]
    public void CompleteSnapshotCloneSeparatesAllMutableArraysAndKeepsOrderedPendingInputs()
    {
        var input = Valid();
        var copy = FireSimSnapshotValidation.ValidateAndClone(input);
        input.Cells[0] = 15; input.TargetIds[0] = 88; input.MaterialAuthority.KnownSlots[0] = new(99, 99);
        input.MaterialAuthority.Archives[0] = input.MaterialAuthority.Archives[0] with { PackedCell = 15 };
        input.PendingChanges[0] = new(0, AddHeat: 9);
        Assert.Equal((ushort)0, copy.Cells[0]);
        Assert.Equal(2u, copy.TargetIds[0]);
        Assert.Equal(new FireSimMaterialIdentity(1, 11), copy.MaterialAuthority.KnownSlots[0]);
        Assert.Equal(0u, copy.MaterialAuthority.Archives[0].PackedCell);
        Assert.Equal(new FireSimChange[] { new(0, AddWater: 1), new(0, SetWater: 0) }, copy.PendingChanges);
    }

    [Fact]
    public void MalformedGraphsRejectBeforeAnyRestoreCanConstructBuffers()
    {
        var value = Valid();
        var a = value.MaterialAuthority;
        var malformed = new[]
        {
            value with { Version = 99 }, value with { Grid = new(int.MaxValue, 2, 1) },
            value with { SlotIds = [] }, value with { SlotIds = [0, 0] },
            value with { TargetIds = [2, 2], SlotIds = [21, 21] },
            value with { CompanionFields = [0xf0000000u, 0] }, value with { CompanionFields = [255, 0] },
            value with { TransportFields = [0x10000, 0] },
            value with { MaterialAuthority = a with { KnownSlots = [new(2, 21)] } },
            value with { MaterialAuthority = a with { KnownSlots = [new(1, 11), new(2, 21), new(3, 31)] } },
            value with { MaterialAuthority = a with { Archives = [a.Archives[0] with { Identity = new(2, 21) }] } },
            value with { MaterialAuthority = a with { Archives = [a.Archives[0] with { CaptureToken = 2 }] } },
            value with { MaterialAuthority = a with { Archives = [a.Archives[0] with { SourceCellIndex = 2 }] } },
            value with { PendingChanges = [new(0, CollectCleanAsh: 1)] },
            value with { Parameters = value.Parameters with { VisualFireBaseIntensity = float.NaN } },
        };
        foreach (var snapshot in malformed) Assert.Throws<ArgumentException>(() => FireSimSnapshotValidation.ValidateAndClone(snapshot));
    }

    internal static FireSimSnapshot Valid() => new(1, new(2, 1, 1), 4, FireSimParameters.Default, 89,
        [0, 0], [0, 0], [0, 0], [2, 0], [21, 0],
        new(1, [new(1, 11), new(2, 21)], [new(new(1, 11), 1, 0, 0, 0)]),
        [new(0, AddWater: 1), new(0, SetWater: 0)]);
}
