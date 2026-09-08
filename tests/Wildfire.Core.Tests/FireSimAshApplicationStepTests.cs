using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class FireSimAshApplicationStepTests
{
    [Theory]
    [InlineData(-1, 1)] [InlineData(1, 1)] [InlineData(0, 0)] [InlineData(0, 4)]
    public void InvalidInputRejectsBeforeBackendWork(int cell, byte limit)
    {
        var backend = new Backend();
        Assert.Throws<ArgumentOutOfRangeException>(() => new FireSimStepCoordinator(1, 1)
            .TryApplyCleanAsh(backend, new(cell, limit), _ => { }));
        Assert.Empty(backend.Events);
    }

    [Fact]
    public void GenericQueueAndUnconditionalCommitCannotAdmitApplication()
    {
        var step = new FireSimStepCoordinator(1, 1);
        var backend = new Backend();
        Assert.Throws<ArgumentException>(() => step.RegisterChange(new(0, ApplyCleanAshLimit: 1)));
        Assert.Throws<ArgumentException>(() => step.TryTickWithInput(backend, new(0, ApplyCleanAshLimit: 1), () => { }));
        Assert.Equal(0, step.PendingChangeCount);
        Assert.Empty(backend.Events);
    }

    [Fact]
    public void FullBatchReturnsNullWithoutDispatchOrDroppingQueuedInputs()
    {
        var step = new FireSimStepCoordinator(1, 1);
        step.RegisterChange(new(0, RemoveAsh: 1));
        var backend = new Backend();
        Assert.Null(step.TryApplyCleanAsh(backend, new(0, 2), _ => throw new Exception("no commit")));
        Assert.Empty(backend.Events);
        Assert.Equal(1, step.PendingChangeCount);
        Assert.Equal(0u, step.CurrentTick);
    }

    [Theory]
    [InlineData(FireSimAshApplicationOutcome.Applied)] [InlineData(FireSimAshApplicationOutcome.Full)]
    [InlineData(FireSimAshApplicationOutcome.Tainted)] [InlineData(FireSimAshApplicationOutcome.InvalidSurface)]
    public void ReceiptIsReadAfterSwapAndOnlyPositiveApplicationCommitsBeforeListeners(FireSimAshApplicationOutcome outcome)
    {
        var step = new FireSimStepCoordinator(1, 2);
        var backend = new Backend { Outcome = outcome };
        step.RegisterChange(new(0, RemoveAsh: 1));
        step.Subscribe(new Listener(() => backend.Events.Add("listener")));
        int applied = 0;
        var result = step.TryApplyCleanAsh(backend, new(0, 3), receipt =>
        {
            applied += receipt.Added;
            backend.Events.Add("host");
        });
        Assert.NotNull(result);
        Assert.Equal(outcome, result.Value.Receipt.Outcome);
        Assert.Equal(outcome == FireSimAshApplicationOutcome.Applied ? 1 : 0, applied);
        Assert.Equal((byte)applied, result.Value.Receipt.Added);
        Assert.Equal(1u, result.Value.Step.Tick);
        var expected = new List<string> { "reset", "apply", "simulate", "deltas", "swap", "receipt" };
        if (applied == 1) expected.Add("host");
        expected.Add("listener");
        Assert.Equal(expected, backend.Events);
        Assert.Equal(new FireSimChange(0, RemoveAsh: 1), backend.Changes[0]);
        Assert.Equal(new FireSimChange(0, ApplyCleanAshLimit: 3), backend.Changes[1]);
        Assert.Equal(0, step.PendingChangeCount);
    }

    [Theory]
    [InlineData("missing")] [InlineData("identity")] [InlineData("readback")] [InlineData("rejected-one")]
    public void ReceiptFailurePoisonsStepAndNeverCallsHost(string failure)
    {
        var step = new FireSimStepCoordinator(1, 1);
        var backend = new Backend { Failure = failure };
        int applied = 0;
        var error = Assert.Throws<FireSimStepInputException>(() => step.TryApplyCleanAsh(backend, new(0, 2), _ => applied++));
        Assert.Equal(FireSimStepInputOutcome.Indeterminate, error.Outcome);
        Assert.Equal(0, applied);
        Assert.Equal(0, step.PendingChangeCount);
        Assert.Throws<InvalidOperationException>(() => step.Tick(backend));
    }

    [Fact]
    public void CallbackFailureIsIndeterminateButLaterListenerFailureNeverReplaysApplication()
    {
        var step = new FireSimStepCoordinator(1, 1);
        var failure = new IOException("native mutation callback");
        var error = Assert.Throws<FireSimStepInputException>(() => step.TryApplyCleanAsh(new Backend(), new(0, 1), _ => throw failure));
        Assert.Equal(FireSimStepInputOutcome.Indeterminate, error.Outcome);
        Assert.Same(failure, error.InnerException);
        step = new FireSimStepCoordinator(1, 1);
        using var listener = step.Subscribe(new Listener(() => throw failure));
        int applications = 0;
        error = Assert.Throws<FireSimStepInputException>(() => step.TryApplyCleanAsh(new Backend(), new(0, 1), _ => applications++));
        Assert.Equal(FireSimStepInputOutcome.Committed, error.Outcome);
        listener.Dispose();
        step.Tick(new Backend());
        Assert.Equal(1, applications);
        Assert.Equal(0, step.PendingChangeCount);
    }

    private sealed class Listener(Action action) : IFireSimListener
    { public void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas) => action(); }

    private sealed class Backend : IFireSimAshCollectionBackend
    {
        public FireSimAshApplicationOutcome Outcome;
        public string? Failure;
        public List<string> Events = [];
        public FireSimChange[] Changes = [];
        public void ResetDeltaCounter(uint tick) => Events.Add("reset");
        public void ApplyExternalChanges(uint tick, FireSimChange[] changes) { Events.Add("apply"); Changes = changes; }
        public void Simulate(uint tick) => Events.Add("simulate");
        public CellDelta[] ReadDeltas(uint tick) { Events.Add("deltas"); return []; }
        public void SwapBuffers(uint tick) => Events.Add("swap");
        public FireSimGpuChange ReadAppliedChange(int index)
        {
            Events.Add("receipt");
            if (Failure == "readback") throw new IOException("GPU readback");
            var c = FireSimGpuProtocol.EncodeChange(Changes[index]);
            uint fields = c.AddFields | (1u << 29) | ((uint)Outcome << 30) |
                (Outcome == FireSimAshApplicationOutcome.Applied ? 1u << 27 : 0);
            if (Failure == "missing") fields &= ~(1u << 29);
            if (Failure == "rejected-one") fields |= 1u << 30;
            return new(c.CellIndex + (Failure == "identity" ? 1u : 0), c.SetMask, fields, c.SetValues);
        }
    }
}
