namespace Wildfire.Core.Tests;

public sealed class FireSimMaterialHandoffCoordinatorTests
{
    private static readonly FireSimMaterialIdentity A = new(1, 11), B = new(2, 21);
    private static FireSimMaterialDefinition Material(byte fuel) => new(WildfireMaterialClass.Tree, 7,
        WildfireAshQuality.Fertile, WildfireContaminationBehavior.None, fuel, 2, 1);
    private static FireSimMaterialHandoffBatch Replace(uint token = 1) => new(token,
        [FireSimMaterialHandoffRequest.Fresh(0, A, B, Material(9))]);
    private static FireSimStepCoordinator Coordinator(int changeCapacity = 2) => new(2, changeCapacity, [A, default]);

    [Fact]
    public void CapacityRejectionDoesNotUploadOrConsumeTokenOrOrdinaryInputs()
    {
        var step = Coordinator(1);
        var backend = new Backend();
        step.RegisterChange(new(0, AddWater: 1));
        Assert.Null(step.TryHandoffMaterial(backend, Replace(), _ => throw new Exception()));
        Assert.Equal(0, backend.Uploads);
        Assert.Equal(1, step.PendingChangeCount);
        step.Tick(backend);
        Assert.NotNull(step.TryHandoffMaterial(backend, Replace(), _ => { }));
        Assert.Equal(1, backend.Uploads);
    }

    [Fact]
    public void WholeMaterialCapacityRejectsBeforeUpload()
    {
        var backend = new Backend { MaterialHandoffCapacity = 0 };
        Assert.Null(Coordinator().TryHandoffMaterial(backend, Replace(), _ => throw new Exception()));
        Assert.Equal(0, backend.Uploads);
    }

    [Fact]
    public void CoordinatorOwnsSingleUseArchiveAcrossActualAcceptedCommitCallbacks()
    {
        var step = Coordinator();
        var backend = new Backend();
        step.TryHandoffMaterial(backend, Replace(), receipt => Assert.True(receipt.Accepted));
        Assert.True(step.TryGetMaterialArchive(A, out var archive));
        Assert.Equal(3u, archive.PackedCell & 15u);
        var restore = new FireSimMaterialHandoffBatch(2, [FireSimMaterialHandoffRequest.RestoreArchived(0, B, archive)]);
        backend.Words = [0, 2, 21, Material(9).PackedMaterial, Material(9).CompanionMaterial,
                        1, 11, archive.PackedCell, archive.Companion, 1];
        step.TryHandoffMaterial(backend, restore, _ => { });
        Assert.False(step.TryGetMaterialArchive(A, out _));
        Assert.True(step.TryGetMaterialArchive(B, out _));
        var replay = new FireSimMaterialHandoffBatch(3, [FireSimMaterialHandoffRequest.RestoreArchived(1, default, archive)]);
        Assert.Throws<ArgumentException>(() => step.TryHandoffMaterial(backend, replay, _ => { }));
        Assert.Equal(2, backend.Uploads);
        Assert.Throws<ArgumentException>(() => step.TryHandoffMaterial(backend, Replace(4), _ => { })); // Returning B cannot become fresh.
    }

    [Fact]
    public void RejectedMaterialReceiptStillCommitsEarlierOrdinaryInputsAndTickWithoutNewArchives()
    {
        var step = Coordinator();
        var backend = new Backend { Accepted = false };
        step.RegisterChange(new(0, AddWater: 1));
        var result = step.TryHandoffMaterial(backend, Replace(), receipt => Assert.False(receipt.Accepted));
        Assert.NotNull(result);
        Assert.Equal(1u, step.CurrentTick);
        Assert.Equal(0, step.PendingChangeCount);
        Assert.False(step.TryGetMaterialArchive(A, out _));
        Assert.Equal(2, backend.Changes.Length);
        Assert.Equal((byte)1, backend.Changes[0].AddWater);
        Assert.Throws<ArgumentException>(() => step.TryHandoffMaterial(backend, Replace(), _ => { }));
        backend.Accepted = true;
        Assert.NotNull(step.TryHandoffMaterial(backend, Replace(2), _ => { }));
    }

    [Fact]
    public void StaleHostOwnershipAndUnacknowledgedMarkerRejectBeforeRuntimeCall()
    {
        var step = Coordinator();
        var backend = new Backend();
        var wrongSlot = new FireSimMaterialHandoffBatch(1,
            [FireSimMaterialHandoffRequest.Remove(1, A, true)]);
        Assert.Throws<ArgumentException>(() => step.TryHandoffMaterial(backend, wrongSlot, _ => { }));
        Assert.Equal(0, backend.Uploads);
        Assert.Throws<ArgumentException>(() => step.RegisterChange(new(0, MaterialHandoff: Replace())));
        Assert.Throws<ArgumentException>(() => step.TryTickWithInput(backend, new(0, MaterialHandoff: Replace()), () => { }));
    }

    [Fact]
    public void MissingOrMalformedReceiptMakesAuthorityIndeterminateAndBlocksFurtherTicks()
    {
        var step = Coordinator();
        var backend = new Backend { Words = [0] };
        var exception = Assert.Throws<FireSimStepInputException>(() => step.TryHandoffMaterial(backend, Replace(), _ => { }));
        Assert.Equal(FireSimStepInputOutcome.Indeterminate, exception.Outcome);
        Assert.False(step.TryGetMaterialArchive(A, out _));
        Assert.Throws<InvalidOperationException>(() => step.Tick(backend));
        Assert.Throws<InvalidOperationException>(() => step.TryHandoffMaterial(backend, Replace(2), _ => { }));
    }

    [Fact]
    public void UploadFailureIsNotAppliedAndCanRetryWithANewToken()
    {
        var step = Coordinator();
        var backend = new Backend { FailUpload = true };
        var exception = Assert.Throws<FireSimStepInputException>(() => step.TryHandoffMaterial(backend, Replace(), _ => { }));
        Assert.Equal(FireSimStepInputOutcome.NotApplied, exception.Outcome);
        Assert.Equal(0u, step.CurrentTick);
        Assert.Throws<ArgumentException>(() => step.TryHandoffMaterial(backend, Replace(), _ => { }));
        backend.FailUpload = false;
        Assert.NotNull(step.TryHandoffMaterial(backend, Replace(2), _ => { }));
    }

    [Fact]
    public void OriginatingTargetSurvivesListenerDeliveryAcrossOwnershipCommit()
    {
        var step = Coordinator();
        var backend = new Backend();
        var listener = new Listener();
        using var subscription = step.Subscribe(listener);
        step.TryHandoffMaterial(backend, Replace(), _ => { });
        Assert.Equal(new uint[] { 1, 2 }, listener.Deltas.Select(delta => delta.TargetId));
    }

    [Fact]
    public void HostCallbackFailureAfterAuthorityCommitIsIndeterminateAndCannotReplay()
    {
        var step = Coordinator();
        var backend = new Backend();
        var exception = Assert.Throws<FireSimStepInputException>(() => step.TryHandoffMaterial(backend, Replace(), receipt =>
        {
            Assert.True(step.TryGetMaterialArchive(A, out _));
            throw new InvalidOperationException("native publication failed");
        }));
        Assert.Equal(FireSimStepInputOutcome.Indeterminate, exception.Outcome);
        Assert.True(step.TryGetMaterialArchive(A, out var archive));
        Assert.Equal(3u, archive.PackedCell & 15u);
        Assert.Throws<InvalidOperationException>(() => step.Tick(backend));
        Assert.Throws<InvalidOperationException>(() => step.TryHandoffMaterial(backend, Replace(2), _ => { }));
        Assert.Equal(1, backend.Uploads);
    }

    [Fact]
    public void ListenerFailureAfterHostCallbackIsCommittedAndAuthorityPublishesOnce()
    {
        var step = Coordinator();
        var backend = new Backend();
        using var subscription = step.Subscribe(new ThrowingListener());
        int callbacks = 0;
        var exception = Assert.Throws<FireSimStepInputException>(() =>
            step.TryHandoffMaterial(backend, Replace(), _ => callbacks++));
        Assert.Equal(FireSimStepInputOutcome.Committed, exception.Outcome);
        Assert.True(step.TryGetMaterialArchive(A, out var archive));
        Assert.Equal(1, callbacks);
        Assert.Throws<ArgumentException>(() => step.TryHandoffMaterial(backend, Replace(), _ => callbacks++));
        subscription.Dispose();
        step.Tick(backend); // Committed listener errors do not poison material authority.
        Assert.True(step.TryGetMaterialArchive(A, out var sameArchive));
        Assert.Same(archive, sameArchive);
        Assert.Equal(1, backend.Uploads);
    }

    private sealed class ThrowingListener : IFireSimListener
    {
        public void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas) => throw new InvalidOperationException("listener failed");
    }

    private sealed class Listener : IFireSimListener
    {
        public CellDelta[] Deltas = [];
        public void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas) => Deltas = deltas.ToArray();
    }
    private sealed class Backend : IFireSimMaterialHandoffBackend
    {
        private FireSimMaterialHandoffBatch? batch;
        public int MaterialHandoffCapacity { get; set; } = 2;
        public int Uploads;
        public bool Accepted = true, FailUpload;
        public uint[]? Words;
        public FireSimChange[] Changes = [];
        public void UploadMaterialHandoff(FireSimMaterialHandoffBatch value)
        {
            Uploads++;
            if (FailUpload) throw new InvalidOperationException("allocation failed");
            batch = value;
        }
        public uint[] ReadMaterialHandoffHeader() => [batch!.Token, Accepted ? 1u : 2u, 1, Accepted ? uint.MaxValue : 0];
        public uint[] ReadMaterialHandoffReceipts(int count)
        {
            if (Words is not null) return Words;
            uint prior = Material(3).PackedMaterial;
            uint companion = Material(3).CompanionMaterial | 5u << 12;
            return Accepted
                ? [0, 1, 11, prior, companion, 2, 21, Material(9).PackedMaterial, Material(9).CompanionMaterial, 1]
                : [0, 1, 11, prior, companion, 1, 11, prior, companion, 3];
        }
        public void ResetDeltaCounter(uint tick) { }
        public void ApplyExternalChanges(uint tick, FireSimChange[] changes) => Changes = changes;
        public void Simulate(uint tick) { }
        public CellDelta[] ReadDeltas(uint tick) => [new(0, 1, 2, 1), new(0, 3, 4, 2)];
        public void SwapBuffers(uint tick) { }
    }
}
