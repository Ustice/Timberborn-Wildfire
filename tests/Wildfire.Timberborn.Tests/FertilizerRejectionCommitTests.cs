using Wildfire.Core;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

public sealed class FertilizerRejectionCommitTests
{
    [Fact]
    public void ReturnedRejectionDispositionKeepsInnerExclusionButHasNoConsumptionPrivilege()
    {
        var guard = new NativeResourceTransaction();
        var sim = new TimberbornIncompleteDispatchTests.Simulator();
        int rejected = 0;
        WorkerStep(guard, sim, receipt =>
        {
            rejected++;
            Assert.Equal(FireSimAshApplicationOutcome.Full, receipt.Outcome);
            Assert.Throws<InvalidOperationException>(guard.ThrowIfSaveUnsafe);
            Assert.Throws<InvalidOperationException>(guard.RequireAshApplicationCommit);
            Assert.Throws<InvalidOperationException>(() => guard.TransferInventory(() => throw new Exception("must not enter")));
            Assert.Throws<InvalidOperationException>(() => guard.CaptureAtRest(() => 1));
        });
        Assert.Equal(1, rejected);
        Assert.Equal(1, sim.Swaps);
        guard.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void NativeDispositionFailureAfterReturnedStepPreservesCauseAndBlocksFurtherWork()
    {
        var guard = new NativeResourceTransaction();
        var sim = new TimberbornIncompleteDispatchTests.Simulator();
        var cause = new IOException("phase observer failed");
        var error = Assert.Throws<FireSimStepInputException>(() => WorkerStep(guard, sim, _ => throw cause));
        Assert.Equal(FireSimStepInputOutcome.Committed, error.Outcome);
        Assert.Same(cause, error.InnerException);
        Assert.True(guard.IsIndeterminate);
        Assert.Equal(1, sim.Swaps);
        Assert.Throws<InvalidOperationException>(guard.ThrowIfSaveUnsafe);
        Assert.Throws<InvalidOperationException>(() => WorkerStep(guard, sim, _ => { }));
        Assert.Equal(1, sim.Swaps);
    }

    [Fact]
    public void FullQueueDoesNotInvokeDispositionOrAdvanceTheSimulator()
    {
        var guard = new NativeResourceTransaction();
        var sim = new TimberbornIncompleteDispatchTests.Simulator();
        for (int i = 0; i < 8; i++) sim.RegisterChange(new(0, AddHeat: 1));
        Assert.Null(WorkerStep(guard, sim, _ => throw new Exception("no rejected step")));
        Assert.Equal(0, sim.Swaps);
        guard.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void CaughtLifecycleInvalidationDuringRejectionStillRejectsSuccess()
    {
        var guard = new NativeResourceTransaction();
        var sim = new TimberbornIncompleteDispatchTests.Simulator();
        var error = Assert.Throws<FireSimStepInputException>(() =>
            WorkerStep(guard, sim, _ => guard.InvalidateAfterLifecycleFailure()));
        Assert.Equal(FireSimStepInputOutcome.Indeterminate, error.Outcome);
        Assert.True(guard.IsIndeterminate);
        Assert.Equal(1, sim.Swaps);
    }

    [Fact]
    public void MisleadingNotAppliedFromDispositionCannotRelabelTheCompletedStep()
    {
        var guard = new NativeResourceTransaction();
        var sim = new TimberbornIncompleteDispatchTests.Simulator();
        var cause = new FireSimStepInputException(FireSimStepInputOutcome.NotApplied, new IOException("actor callback"));
        var error = Assert.Throws<FireSimStepInputException>(() => WorkerStep(guard, sim, _ => throw cause));
        Assert.Equal(FireSimStepInputOutcome.Committed, error.Outcome);
        Assert.Same(cause, error.InnerException);
        Assert.Equal(1, sim.Swaps);
        Assert.Throws<InvalidOperationException>(() => WorkerStep(guard, sim, _ => { }));
        Assert.Equal(1, sim.Swaps);
    }

    [Theory]
    [InlineData(1, 2, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(0, 2, 1)]
    public void ChangedReturnedRejectionCannotPublishDispositionOrReplay(int cell, byte limit, byte added)
    {
        var guard = new NativeResourceTransaction();
        var sim = new ChangedReceiptSimulator(new(cell, limit, added, FireSimAshApplicationOutcome.Full));
        int dispositions = 0;
        var error = Assert.Throws<FireSimStepInputException>(() => WorkerStep(guard, sim, _ => dispositions++));
        Assert.Equal(FireSimStepInputOutcome.Committed, error.Outcome);
        Assert.Equal(0, dispositions);
        Assert.Equal(1, sim.Core.Swaps);
        Assert.Throws<InvalidOperationException>(() => WorkerStep(guard, sim, _ => dispositions++));
        Assert.Equal(1, sim.Core.Swaps);
    }

    private sealed class ChangedReceiptSimulator(FireSimAshApplicationReceipt receipt) : IFireSimAshApplicationSimulator
    {
        internal readonly TimberbornIncompleteDispatchTests.Simulator Core = new();
        public int Width => Core.Width;
        public int Height => Core.Height;
        public int Depth => Core.Depth;
        public void RegisterChange(FireSimChange input) => Core.RegisterChange(input);
        public IDisposable Subscribe(IFireSimListener listener) => Core.Subscribe(listener);
        public GpuFireStepResult Tick() => Core.Tick();
        public GpuFireStepResult? TryTickWithInput(FireSimChange input, Action commit) => Core.TryTickWithInput(input, commit);
        public FireSimAshApplicationStepResult? TryApplyCleanAsh(FireSimAshApplicationInput input,
            Action<FireSimAshApplicationReceipt> commit) => Core.TryApplyCleanAsh(input, commit) is { } result
                ? result with { Receipt = receipt } : null;
    }

    private static FireSimAshApplicationStepResult? WorkerStep(NativeResourceTransaction guard,
        IFireSimAshApplicationSimulator sim, Action<FireSimAshApplicationReceipt> rejected)
    {
        return guard.TryApplyCleanAsh(sim, new(0, 2), _ => throw new Exception("unexpected positive"), rejected);
    }
}
