namespace Wildfire.Core.Tests;

public sealed class FireSimSnapshotCoordinatorTests
{
    [Fact]
    public void RestorePreservesArchiveAuthorityAttemptTokenAndPendingOrder()
    {
        var snapshot = FireSimSnapshotValidationTests.Valid();
        var step = new FireSimStepCoordinator(snapshot, 2);
        var backend = new Backend(snapshot);
        var capture = step.CaptureSnapshot(backend);
        Assert.Equal(4u, capture.Tick);
        Assert.Equal(snapshot.PendingChanges, capture.PendingChanges);
        Assert.True(step.TryGetMaterialArchive(new(1, 11), out var archive));
        Assert.Equal(0u, archive.PackedCell);
        Assert.True(step.IsSlotKnown(new(1, 11))); // Archived, exhausted identity remains known after restore.
        Assert.True(step.IsSlotKnown(new(2, 21)));
        Assert.False(step.IsSlotKnown(new(2, 22)));
        var stale = new FireSimMaterialHandoffBatch(1, [FireSimMaterialHandoffRequest.RestoreArchived(0, new(2, 21), archive)]);
        Assert.Throws<ArgumentException>(() => step.TryHandoffMaterial(backend, stale, _ => { }));
        step.Tick(backend);
        Assert.Equal(snapshot.PendingChanges, backend.Applied);
    }

    [Fact]
    public void CaptureBlocksReentrantStepsQueuedInputAndTickMutationWithoutChangingState()
    {
        var snapshot = FireSimSnapshotValidationTests.Valid();
        var step = new FireSimStepCoordinator(snapshot, 2);
        var backend = new Backend(snapshot);
        backend.OnCapture = () =>
        {
            Assert.Throws<InvalidOperationException>(() => step.Tick(backend));
            Assert.Throws<InvalidOperationException>(() => step.CaptureSnapshot(backend));
            Assert.Throws<InvalidOperationException>(() => step.RegisterChange(new(0, AddWater: 3)));
            Assert.Throws<InvalidOperationException>(() => step.RestoreTick(99));
            Assert.Throws<InvalidOperationException>(() => step.IsSlotKnown(new(2, 22)));
        };
        var capture = step.CaptureSnapshot(backend);
        Assert.Equal(snapshot.PendingChanges, capture.PendingChanges);
        Assert.Equal(4u, capture.Tick);
    }

    [Theory]
    [InlineData("simulate")]
    [InlineData("read")]
    [InlineData("swap")]
    public void ZeroInputUnknownGpuFailureBlocksSnapshotAndFurtherTicks(string stage)
    {
        var snapshot = FireSimSnapshotValidationTests.Valid() with { PendingChanges = [] };
        var step = new FireSimStepCoordinator(snapshot, 2);
        var backend = new Backend(snapshot) { FailingStage = stage };
        Assert.Throws<InvalidOperationException>(() => step.Tick(backend));
        backend.FailingStage = null;
        Assert.Throws<InvalidOperationException>(() => step.CaptureSnapshot(backend));
        Assert.Throws<InvalidOperationException>(() => step.Tick(backend));
        Assert.Equal(0, backend.Captures);
        Assert.Throws<InvalidOperationException>(() => step.IsSlotKnown(new(2, 22)));
    }

    [Fact]
    public void ReadOnlyCaptureFailureCanRetryButConfirmedOwnershipDivergenceCannot()
    {
        var snapshot = FireSimSnapshotValidationTests.Valid();
        var step = new FireSimStepCoordinator(snapshot, 2);
        var backend = new Backend(snapshot) { FailingStage = "capture" };
        Assert.Throws<InvalidOperationException>(() => step.CaptureSnapshot(backend));
        backend.FailingStage = null;
        Assert.Equal(snapshot.Cells, step.CaptureSnapshot(backend).Cells);
        backend.Targets = [9, 0];
        Assert.Throws<InvalidOperationException>(() => step.CaptureSnapshot(backend));
        Assert.Throws<InvalidOperationException>(() => step.Tick(backend));
    }

    [Fact]
    public void LegacyInitializationIsOnceOnlyAndFailedPartialUploadCannotClearUncertainty()
    {
        var step = new FireSimStepCoordinator(2, 2);
        var empty = FireSimSnapshotValidationTests.Valid() with
        {
            TargetIds = [0, 0], SlotIds = [0, 0], PendingChanges = [], MaterialAuthority = new(0, [], []),
        };
        var backend = new Backend(empty);
        step.InitializeLegacyBuffers(0, () => { });
        Assert.Throws<InvalidOperationException>(() => step.InitializeLegacyBuffers(0, () => { }));
        Assert.Equal(FireSimSnapshotCapability.LegacyMaterialHistoryUnavailable, step.SnapshotCapability);
        Assert.Throws<InvalidOperationException>(() => step.CaptureSnapshot(backend));
        Assert.NotNull(step.CaptureLegacySnapshot(backend));
        Assert.Throws<InvalidOperationException>(() => step.IsSlotKnown(new(2, 22)));
        var failed = new FireSimStepCoordinator(2, 2);
        Assert.Throws<InvalidOperationException>(() => failed.InitializeLegacyBuffers(4, () => throw new InvalidOperationException("upload failed")));
        Assert.Throws<InvalidOperationException>(() => failed.InitializeLegacyBuffers(4, () => { }));
        Assert.Throws<InvalidOperationException>(() => failed.CaptureSnapshot(backend));
        Assert.Throws<InvalidOperationException>(() => failed.CaptureLegacySnapshot(backend));
        Assert.Throws<InvalidOperationException>(() => failed.Tick(backend));
    }

    private sealed class Backend(FireSimSnapshot snapshot) : IFireSimSnapshotBackend, IFireSimMaterialHandoffBackend
    {
        public string? FailingStage;
        public Action? OnCapture;
        public int Captures;
        public uint[] Targets = snapshot.TargetIds;
        public FireSimChange[] Applied = [];
        public FireGrid SnapshotGrid => snapshot.Grid;
        public FireSimParameters SnapshotParameters => snapshot.Parameters;
        public uint SnapshotSeed => snapshot.Seed;
        public FireSimSnapshotBuffers ReadSnapshotBuffers()
        {
            Captures++; OnCapture?.Invoke(); Fail("capture");
            return new(snapshot.Cells, snapshot.TransportFields, snapshot.CompanionFields, Targets, snapshot.SlotIds);
        }
        public void ResetDeltaCounter(uint tick) => Fail("reset");
        public void ApplyExternalChanges(uint tick, FireSimChange[] changes) { Fail("apply"); Applied = changes; }
        public void Simulate(uint tick) => Fail("simulate");
        public CellDelta[] ReadDeltas(uint tick) { Fail("read"); return []; }
        public void SwapBuffers(uint tick) => Fail("swap");
        private void Fail(string stage) { if (stage == FailingStage) throw new InvalidOperationException(stage); }
        public int MaterialHandoffCapacity => 2;
        public void PrepareMaterialHandoff(FireSimMaterialHandoffBatch batch, int orderedCommandCount) => throw new NotSupportedException();
        public uint[] ReadMaterialHandoffHeader() => throw new NotSupportedException();
        public uint[] ReadMaterialHandoffReceipts(int count) => throw new NotSupportedException();
    }
}
