using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class FireSimAshCollectionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void CollectionIsAnExclusiveSixteenByteCommandWithNoUploadedReceipt(byte request)
    {
        var encoded = FireSimGpuProtocol.EncodeChange(new(4, CollectCleanAsh: request));
        Assert.Equal(16, FireSimGpuProtocol.ChangeStrideBytes);
        Assert.Equal(FireSimGpuProtocol.CollectCleanAshMask, encoded.SetMask);
        Assert.Equal((uint)request << 25, encoded.AddFields);
        Assert.Equal(0u, encoded.SetValues);
        Assert.Equal(0u, FireSimGpuProtocol.EncodeChange(new(4)).SetMask);
        Assert.Throws<ArgumentException>(() => FireSimGpuProtocol.EncodeChange(new(4, RemoveAsh: 0, CollectCleanAsh: request)));
        Assert.Throws<ArgumentException>(() => FireSimGpuProtocol.EncodeChange(new(4, SetAsh: 3, CollectCleanAsh: request)));
    }

    [Fact]
    public void OversizedRequestAndUnacknowledgedCollectionAreRejectedBeforeAdmission()
    {
        var step = new FireSimStepCoordinator(1, 1);
        var backend = new Backend();
        Assert.Throws<ArgumentOutOfRangeException>(() => step.TryCollectAsh(backend, new(0, 4), _ => { }));
        Assert.Throws<ArgumentException>(() => step.RegisterChange(new(0, CollectCleanAsh: 0)));
        Assert.Throws<ArgumentException>(() => step.TryTickWithInput(backend, new(0, CollectCleanAsh: 1), () => { }));
        Assert.Equal(0, step.PendingChangeCount);
        Assert.Empty(backend.Events);
    }

    [Fact]
    public void CapacityRejectionDoesNotDispatchReadOrCallTheHost()
    {
        var step = new FireSimStepCoordinator(1, 1);
        step.RegisterChange(new(0, RemoveAsh: 1));
        var backend = new Backend();
        Assert.Null(step.TryCollectAsh(backend, new(0, 1), _ => throw new Exception("must not commit")));
        Assert.Empty(backend.Events);
        Assert.Equal(1, step.PendingChangeCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void HostReceivesOnlyGpuQuantityAfterSwapAndBeforeListeners(byte removed)
    {
        var step = new FireSimStepCoordinator(1, 2);
        var backend = new Backend { Removed = removed };
        step.RegisterChange(new(0, RemoveAsh: 1));
        step.Subscribe(new Listener(() => backend.Events.Add("listener")));
        step.TryCollectAsh(backend, new(0, 3), receipt =>
        {
            Assert.Equal(new FireSimAshCollectionReceipt(0, 3, removed), receipt);
            backend.Events.Add("host");
        });
        Assert.Equal(["reset", "apply", "simulate", "deltas", "swap", "receipt", "host", "listener"], backend.Events);
        Assert.Equal(new FireSimChange(0, RemoveAsh: 1), backend.Changes[0]);
        Assert.Equal(new FireSimChange(0, CollectCleanAsh: 3), backend.Changes[1]);
        Assert.Equal(0, step.PendingChangeCount);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("identity")]
    [InlineData("request")]
    [InlineData("excess")]
    [InlineData("readback")]
    public void InvalidOrUnreadableGpuReceiptIsIndeterminateAndNeverInventsZero(string failure)
    {
        var step = new FireSimStepCoordinator(1, 1);
        var backend = new Backend { ReceiptFailure = failure };
        int callbacks = 0;
        var error = Assert.Throws<FireSimStepInputException>(() =>
            step.TryCollectAsh(backend, new(0, 1), _ => callbacks++));
        Assert.Equal(FireSimStepInputOutcome.Indeterminate, error.Outcome);
        Assert.Equal(0, callbacks);
        Assert.Equal(0, step.PendingChangeCount);
    }

    [Fact]
    public void HostFailureIsIndeterminateButObserverFailureAfterCommitIsCommitted()
    {
        var step = new FireSimStepCoordinator(1, 1);
        var failure = new InvalidOperationException("native inventory event after mutation");
        var error = Assert.Throws<FireSimStepInputException>(() =>
            step.TryCollectAsh(new Backend(), new(0, 1), _ => throw failure));
        Assert.Equal(FireSimStepInputOutcome.Indeterminate, error.Outcome);
        Assert.Same(failure, error.InnerException);
        step.Subscribe(new Listener(() => throw failure));
        int goods = 0;
        error = Assert.Throws<FireSimStepInputException>(() =>
            step.TryCollectAsh(new Backend { Removed = 1 }, new(0, 1), receipt => goods += receipt.Collected));
        Assert.Equal(FireSimStepInputOutcome.Committed, error.Outcome);
        Assert.Equal(1, goods);
    }

    private sealed class Listener(Action notify) : IFireSimListener
    { public void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas) => notify(); }

    private sealed class Backend : IFireSimAshCollectionBackend
    {
        public byte Removed;
        public string? ReceiptFailure;
        public FireSimChange[] Changes = [];
        public List<string> Events = [];
        public void ResetDeltaCounter(uint tick) => Events.Add("reset");
        public void ApplyExternalChanges(uint tick, FireSimChange[] changes) { Events.Add("apply"); Changes = changes; }
        public void Simulate(uint tick) => Events.Add("simulate");
        public CellDelta[] ReadDeltas(uint tick) { Events.Add("deltas"); return []; }
        public void SwapBuffers(uint tick) => Events.Add("swap");
        public FireSimGpuChange ReadAppliedChange(int index)
        {
            Events.Add("receipt");
            if (ReceiptFailure == "readback") throw new IOException("GPU readback failed");
            var encoded = FireSimGpuProtocol.EncodeChange(Changes[index]);
            uint flags = encoded.AddFields | FireSimGpuProtocol.CollectionReceiptValidMask | ((uint)Removed << 27);
            if (ReceiptFailure == "missing") flags &= ~FireSimGpuProtocol.CollectionReceiptValidMask;
            if (ReceiptFailure == "request") flags ^= 1u << 25;
            if (ReceiptFailure == "excess") flags |= 3u << 27;
            return new(encoded.CellIndex + (ReceiptFailure == "identity" ? 1u : 0u), encoded.SetMask, flags, encoded.SetValues);
        }
    }
}
