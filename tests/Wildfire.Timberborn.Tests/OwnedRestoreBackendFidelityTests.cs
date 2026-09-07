using Wildfire.Core;
using Wildfire.Timberborn.Persistence;
using F = Wildfire.Timberborn.Tests.OwnedNativeRestoreFixture;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedRestoreBackendFidelityTests
{
    [Theory]
    [InlineData("fuel")]
    [InlineData("pending")]
    [InlineData("archive")]
    [InlineData("legacy")]
    [InlineData("factory-input")]
    public void SameSizedBackendCannotReplaceSavedStateOrRewriteFactoryEvidence(string change)
    {
        var f = new F();
        var saved = f.Snapshot();
        var original = saved.OwnedMaterial!.CaptureSimulation() with
        {
            TargetIds = [0], SlotIds = [0],
            MaterialAuthority = new(1, [new(1, 1)], [new(new(1, 1), 1, 0, 2, 0)]),
            PendingChanges = [new(0, SetWater: 2)],
        };
        saved = saved with { OwnedMaterial = new(original, saved.OwnedMaterial.Bindings, saved.OwnedMaterial.History) };
        string before = TimberbornWildfirePersistenceCodec.Encode(saved);
        Simulator? backend = null;
        Assert.Throws<ArgumentException>(() => TimberbornOwnedWorldSession<Simulator>.PrepareRestore(saved, [], input =>
        {
            var actual = FireSimSnapshotValidation.ValidateAndClone(input);
            if (change == "fuel") actual.MaterialAuthority.Archives[0] = actual.MaterialAuthority.Archives[0] with { PackedCell = 3 };
            if (change == "pending") actual = actual with { PendingChanges = [] };
            if (change == "archive") actual = actual with { MaterialAuthority = actual.MaterialAuthority with { Archives = [] } };
            if (change == "factory-input")
            {
                input.Cells[0] = PackedCell.SetWater(input.Cells[0], 1);
                actual = input;
            }
            return backend = new(actual, change == "legacy");
        }, (_, _) => [F.Facts()], f.Effects, f.Guard));
        Assert.Equal(1, backend!.Disposals);
        Assert.False(f.Guard.IsIndeterminate);
        Assert.Equal(before, TimberbornWildfirePersistenceCodec.Encode(saved));
    }

    private sealed class Simulator(FireSimSnapshot snapshot, bool legacy) : IGpuFireSimulator, IFireSimSnapshotSimulator, IDisposable
    {
        internal int Disposals;
        public int Width => snapshot.Grid.Width;
        public int Height => snapshot.Grid.Height;
        public int Depth => snapshot.Grid.Depth;
        public FireSimSnapshotCapability SnapshotCapability => legacy ? FireSimSnapshotCapability.LegacyMaterialHistoryUnavailable : FireSimSnapshotCapability.CompleteMaterialHistory;
        public FireSimSnapshot CaptureSnapshot() => snapshot;
        public void Dispose() => Disposals++;
        public void RegisterChange(FireSimChange change) => throw new NotSupportedException();
        public GpuFireStepResult Tick() => throw new NotSupportedException();
        public IDisposable Subscribe(IFireSimListener listener) => throw new NotSupportedException();
    }
}
