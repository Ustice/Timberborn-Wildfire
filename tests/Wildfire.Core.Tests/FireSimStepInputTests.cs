using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class FireSimStepInputTests
{
    [Fact]
    public void FullBatchRejectsInputWithoutDispatchAndOrdinaryFallbackDoesNotLeakIt()
    {
        FireSimStepCoordinator step = new(1, 1);
        FireSimChange queued = new(0, SetWater: 1);
        step.RegisterChange(queued);
        Backend backend = new();
        int commits = 0;

        Assert.Null(step.TryTickWithInput(backend, new(0, AddWater: 1), () => commits++));

        Assert.Empty(backend.Events);
        Assert.Equal(0u, step.CurrentTick);
        Assert.Equal(1, step.PendingChangeCount);
        step.Tick(backend);
        Assert.Equal([queued], Assert.Single(backend.Uploads));
        Assert.Equal(0, commits);
        Assert.Equal(1u, step.CurrentTick);
    }

    [Fact]
    public void InputFollowsQueuedCommandsAndCommitsAfterSwapBeforeObserversEvenWithoutDeltas()
    {
        FireSimStepCoordinator step = new(1, 2);
        FireSimChange queued = new(0, SetWater: 3);
        FireSimChange input = new(0, AddWater: 1);
        step.RegisterChange(queued);
        Backend backend = new();
        int commits = 0;
        using IDisposable listener = step.Subscribe(new Listener(() =>
        {
            Assert.Equal(1, commits);
            backend.Events.Add("notify");
        }));

        GpuFireStepResult? result = step.TryTickWithInput(backend, input, () =>
        {
            commits++;
            Assert.Equal(1u, step.CurrentTick);
            Assert.Equal(0, step.PendingChangeCount);
            Assert.Equal("swap", backend.Events.Last());
            backend.Events.Add("commit");
        });

        Assert.Equal([queued, input], Assert.Single(backend.Uploads));
        Assert.Equal(2, step.LastUploadedChangeCount);
        Assert.Equal(["reset", "apply", "simulate", "read", "swap", "commit", "notify"], backend.Events);
        Assert.Empty(result!.Value.Deltas);
        listener.Dispose();
        step.Tick(backend);
        Assert.Single(backend.Uploads);
        Assert.Equal(1, commits);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void InvalidInputCannotConsumeWaterOrDispatch(int cell)
    {
        FireSimStepCoordinator step = new(1, 1);
        Backend backend = new();
        int commits = 0;

        Assert.Throws<ArgumentOutOfRangeException>(() => step.TryTickWithInput(backend, new(cell, AddWater: 1), () => commits++));

        Assert.Empty(backend.Events);
        Assert.Equal(0, commits);
        Assert.Equal(0, step.PendingChangeCount);
    }

    [Theory]
    [InlineData("reset", FireSimStepInputOutcome.NotApplied)]
    [InlineData("apply", FireSimStepInputOutcome.Indeterminate)]
    [InlineData("simulate", FireSimStepInputOutcome.Indeterminate)]
    [InlineData("read", FireSimStepInputOutcome.Indeterminate)]
    [InlineData("swap", FireSimStepInputOutcome.Indeterminate)]
    [InlineData("commit", FireSimStepInputOutcome.Indeterminate)]
    [InlineData("notify", FireSimStepInputOutcome.Committed)]
    public void FailureReportsOutcomeAndNeverLeavesInputQueued(string stage, FireSimStepInputOutcome expected)
    {
        FireSimStepCoordinator step = new(1, 1);
        Backend backend = new() { FailingStage = stage };
        int commits = 0;
        using IDisposable listener = step.Subscribe(new Listener(() => backend.Record("notify")));

        FireSimStepInputException error = Assert.Throws<FireSimStepInputException>(() =>
            step.TryTickWithInput(backend, new(0, AddWater: 1), () =>
            {
                backend.Record("commit");
                commits++;
            }));

        Assert.Equal(expected, error.Outcome);
        Assert.Same(backend.Failure, error.InnerException);
        Assert.Equal(stage == "notify" ? 1 : 0, commits);
        Assert.Equal(0, step.PendingChangeCount);
        Assert.Equal(stage is "reset" or "apply" ? 0u : 1u, step.CurrentTick);
        int uploadCount = backend.Uploads.Count;
        listener.Dispose();
        backend.FailingStage = null;
        // Test queue ownership only: the production host must stop after Indeterminate.
        step.Tick(backend);
        Assert.Equal(uploadCount, backend.Uploads.Count);
    }

    [Fact]
    public void InvalidQueuedChangesDoNotStealAdmissionCapacity()
    {
        FireSimStepCoordinator step = new(1, 1);
        step.RegisterChange(new(-1));
        Backend backend = new();
        int commits = 0;

        Assert.NotNull(step.TryTickWithInput(backend, new(0, AddWater: 1), () => commits++));

        Assert.Equal(1, step.LastIgnoredChangeCount);
        Assert.Equal(1, step.LastUploadedChangeCount);
        Assert.Equal(0, step.PendingChangeCount);
        Assert.Equal(1, commits);
    }

    [Fact]
    public void CommitCanQueueNextStepChangesButCannotReenterSimulation()
    {
        FireSimStepCoordinator step = new(1, 1);
        Backend backend = new();
        FireSimChange next = new(0, AddHeat: 1);

        step.TryTickWithInput(backend, new(0, AddWater: 1), () =>
        {
            Assert.Throws<InvalidOperationException>(() => step.Tick(backend));
            Assert.Throws<InvalidOperationException>(() => step.TryTickWithInput(backend, new(0), () => { }));
            step.RegisterChange(next);
        });
        step.Tick(backend);

        Assert.Equal([next], backend.Uploads[1]);
        Assert.Equal(2u, step.CurrentTick);
    }

    private sealed class Backend : IFireSimStepBackend
    {
        public List<string> Events { get; } = [];
        public List<FireSimChange[]> Uploads { get; } = [];
        public string? FailingStage { get; set; }
        public Exception Failure { get; } = new InvalidOperationException("Injected failure.");
        public void ResetDeltaCounter(uint tick) => Record("reset");
        public void ApplyExternalChanges(uint tick, FireSimChange[] changes)
        {
            Uploads.Add(changes);
            Record("apply");
        }
        public void Simulate(uint tick) => Record("simulate");
        public CellDelta[] ReadDeltas(uint tick)
        {
            Record("read");
            return [];
        }
        public void SwapBuffers(uint tick) => Record("swap");
        public void Record(string stage)
        {
            Events.Add(stage);
            if (stage == FailingStage)
            {
                throw Failure;
            }
        }
    }

    private sealed class Listener(Action notify) : IFireSimListener
    {
        public void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas) => notify();
    }
}
