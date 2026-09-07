using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class FireSimStepCoordinatorTests
{
    [Fact]
    public void StepAppliesSimulatesReadsSwapsThenNotifiesInOrder()
    {
        FireSimStepCoordinator coordinator = new(cellCount: 1, changeCapacity: 1);
        RecordingBackend backend = new();
        coordinator.RegisterChange(new FireSimChange(0, SetSmoke: 5));
        using IDisposable subscription = coordinator.Subscribe(new Listener(deltas =>
        {
            backend.Events.Add("notify");
            Assert.Equal(backend.Deltas, deltas);
            Assert.Equal(0, coordinator.PendingChangeCount);
            Assert.Equal(1u, coordinator.CurrentTick);
        }));

        GpuFireStepResult result = coordinator.Tick(backend);

        Assert.Equal(["reset:1", "apply:1", "simulate:1", "read:1", "swap:1", "notify"], backend.Events);
        Assert.Equal(1u, result.Tick);
        Assert.Same(backend.Deltas, result.Deltas);
    }

    [Theory]
    [InlineData("reset")]
    [InlineData("apply")]
    public void FailureBeforeExternalChangesCompleteRetainsQueueAndTick(string failingStage)
    {
        FireSimStepCoordinator coordinator = new(1, 1);
        FireSimChange change = new(0, AddHeat: 2);
        coordinator.RegisterChange(change);
        RecordingBackend backend = new() { FailingStage = failingStage };

        Assert.Throws<InvalidOperationException>(() => coordinator.Tick(backend));

        Assert.Equal(1, coordinator.PendingChangeCount);
        Assert.Equal(0u, coordinator.CurrentTick);
        Assert.DoesNotContain("simulate:1", backend.Events);
        backend.FailingStage = null;
        backend.Events.Clear();

        if (failingStage == "apply")
        {
            Assert.Throws<InvalidOperationException>(() => coordinator.Tick(backend));
            Assert.Empty(backend.Events);
            return;
        }
        coordinator.Tick(backend);

        Assert.Equal([change], backend.AppliedChanges);
        Assert.Equal(["reset:1", "apply:1", "simulate:1", "read:1", "swap:1"], backend.Events);
        Assert.Equal(0, coordinator.PendingChangeCount);
        Assert.Equal(1u, coordinator.CurrentTick);
    }

    [Theory]
    [InlineData("simulate")]
    [InlineData("read")]
    [InlineData("swap")]
    public void FailureAfterApplyingChangesDoesNotReplayCommandsOrNotify(string failingStage)
    {
        FireSimStepCoordinator coordinator = new(1, 1);
        coordinator.RegisterChange(new FireSimChange(0, AddHeat: 2));
        RecordingBackend backend = new() { FailingStage = failingStage };
        using IDisposable subscription = coordinator.Subscribe(new Listener(_ => backend.Events.Add("notify")));

        Assert.Throws<InvalidOperationException>(() => coordinator.Tick(backend));

        Assert.Equal(0, coordinator.PendingChangeCount);
        Assert.Equal(1u, coordinator.CurrentTick);
        Assert.DoesNotContain("notify", backend.Events);
        if (failingStage != "swap")
        {
            Assert.DoesNotContain("swap:1", backend.Events);
        }

        backend.FailingStage = null;
        backend.Events.Clear();
        Assert.Throws<InvalidOperationException>(() => coordinator.Tick(backend));
        Assert.Empty(backend.Events);
    }

    [Fact]
    public void RestoredTickIsUsedByEveryBackendStage()
    {
        FireSimStepCoordinator coordinator = new(1, 1);
        coordinator.RestoreTick(42);
        RecordingBackend backend = new();

        GpuFireStepResult result = coordinator.Tick(backend);

        Assert.Equal(43u, result.Tick);
        Assert.Equal(["reset:43", "simulate:43", "read:43", "swap:43"], backend.Events);
    }

    [Fact]
    public void ListenerFailureHappensAfterBufferSwapAndDoesNotReplayCommands()
    {
        FireSimStepCoordinator coordinator = new(1, 1);
        coordinator.RegisterChange(new FireSimChange(0, AddHeat: 2));
        RecordingBackend backend = new();
        using IDisposable subscription = coordinator.Subscribe(new Listener(_ => throw new InvalidOperationException("Listener failed.")));

        Assert.Throws<InvalidOperationException>(() => coordinator.Tick(backend));

        Assert.Equal("swap:1", backend.Events.Last());
        Assert.Equal(1u, coordinator.CurrentTick);
        Assert.Equal(0, coordinator.PendingChangeCount);
    }

    private sealed class RecordingBackend : IFireSimStepBackend
    {
        public List<string> Events { get; } = [];
        public CellDelta[] Deltas { get; } = [new CellDelta(0, 0, 16)];
        public FireSimChange[] AppliedChanges { get; private set; } = [];
        public string? FailingStage { get; set; }

        public void ResetDeltaCounter(uint tick) => Record("reset", tick);

        public void ApplyExternalChanges(uint tick, FireSimChange[] changes)
        {
            Record("apply", tick);
            AppliedChanges = changes;
        }

        public void Simulate(uint tick) => Record("simulate", tick);

        public CellDelta[] ReadDeltas(uint tick)
        {
            Record("read", tick);
            return Deltas;
        }

        public void SwapBuffers(uint tick) => Record("swap", tick);

        private void Record(string stage, uint tick)
        {
            Events.Add($"{stage}:{tick}");
            if (FailingStage == stage)
            {
                throw new InvalidOperationException($"Failure at {stage}.");
            }
        }
    }

    private sealed class Listener(Action<CellDelta[]> onDeltas) : IFireSimListener
    {
        public void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas) => onDeltas(deltas.ToArray());
    }
}
