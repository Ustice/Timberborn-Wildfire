using Wildfire.Core;
using Wildfire.Timberborn.Persistence;
using Wildfire.Timberborn.Resources;
using Wildfire.Timberborn.Runtime;

namespace Wildfire.Timberborn.Tests;

public sealed class DispatchSaveScopeTests
{
    [Theory]
    [InlineData("ordinary", 0)]
    [InlineData("water", 1)]
    [InlineData("rejected_ash", 0)]
    public void ScopeAllowsExactlyOneExistingStepAndKeepsSavesExcludedAfterInnerReturn(string kind, int expectedCommits)
    {
        var guard = new NativeResourceTransaction();
        var simulator = new TimberbornIncompleteDispatchTests.Simulator();
        int commits = 0;
        int notifications = 0;
        using var subscription = simulator.Subscribe(new Listener(() =>
        {
            notifications++;
            Assert.Throws<InvalidOperationException>(guard.ThrowIfSaveUnsafe);
        }));
        guard.ExcludeSavesDuringDispatch(() =>
        {
            if (kind == "ordinary") simulator.Tick();
            else if (kind == "water") guard.TryDeliver(simulator, new(0, AddWater: 1), () => commits++);
            else
            {
                var result = guard.TryApplyCleanAsh(simulator, new(0, 2), _ => commits++);
                Assert.Equal(FireSimAshApplicationOutcome.Full, result!.Value.Receipt.Outcome);
                Assert.Equal(0, result.Value.Receipt.Added);
            }
            Assert.Throws<InvalidOperationException>(guard.ThrowIfSaveUnsafe);
            Assert.Throws<InvalidOperationException>(guard.RequireAshApplicationCommit);
        });
        Assert.Equal(expectedCommits, commits);
        Assert.Equal(1, notifications);
        Assert.Equal(1, simulator.Swaps);
        guard.ThrowIfSaveUnsafe();
    }

    [Theory]
    [InlineData(FireSimStepInputOutcome.NotApplied, false)]
    [InlineData(FireSimStepInputOutcome.Committed, false)]
    [InlineData(FireSimStepInputOutcome.Indeterminate, true)]
    public void ExclusionDoesNotReplaceExistingOperationOutcomeOrOwnAnotherPoisonPolicy(FireSimStepInputOutcome outcome, bool poison)
    {
        var guard = new NativeResourceTransaction();
        var simulator = new TimberbornIncompleteDispatchTests.Simulator();
        var cause = new IOException("original boundary failure");
        if (outcome == FireSimStepInputOutcome.NotApplied) simulator.ResetFailure = cause;
        using var subscription = simulator.Subscribe(new Listener(() =>
        {
            if (outcome == FireSimStepInputOutcome.Committed) throw cause;
        }));
        var error = Assert.Throws<FireSimStepInputException>(() => guard.ExcludeSavesDuringDispatch(() =>
            guard.TryDeliver(simulator, new(0, AddWater: 1), () =>
            {
                if (outcome == FireSimStepInputOutcome.Indeterminate) throw cause;
            })));
        Assert.Equal(outcome, error.Outcome);
        Assert.Same(cause, error.InnerException);
        Assert.Equal(poison, guard.IsIndeterminate);
        if (!poison) guard.ThrowIfSaveUnsafe(); // Whole-world Committed failure remains the host observer's responsibility.
    }

    [Fact]
    public void GuardedCaptureBlocksDispatchBeforeCallbackButOrdinaryReadFailureDoesNotPoison()
    {
        var guard = new NativeResourceTransaction();
        int calls = 0;
        var cause = new IOException("read failed");
        Assert.Same(cause, Assert.Throws<IOException>(() => guard.CaptureAtRest<int>(() =>
        {
            Assert.Throws<InvalidOperationException>(() => guard.ExcludeSavesDuringDispatch(() => calls++));
            throw cause;
        })));
        Assert.Equal(0, calls);
        guard.ExcludeSavesDuringDispatch(() => calls++);
        Assert.Equal(1, calls);
        guard.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void SingleSimulatorReadModelObservationRemainsAvailableWithoutAdmittingWorldCapture()
    {
        var guard = new NativeResourceTransaction();
        var simulator = new ReadableSimulator();
        using var system = new TimberbornFireSystem(simulator);
        guard.ExcludeSavesDuringDispatch(() =>
        {
            Assert.Same(simulator.Snapshot, system.CapturePersistentFireSimState());
            Assert.Throws<InvalidOperationException>(() => guard.CaptureAtRest(system.CapturePersistentFireSimState));
        });
        Assert.Equal(1, simulator.Reads);
    }

    private sealed class Listener(Action callback) : IFireSimListener
    {
        public void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas) => callback();
    }

    private sealed class ReadableSimulator : IGpuFireSimulator, ITimberbornFireSimPersistenceState
    {
        internal readonly TimberbornFireSimPersistenceSnapshot Snapshot = new(1, 1, 1, 7, [0], [0]);
        internal int Reads;
        public int Width => 1;
        public int Height => 1;
        public int Depth => 1;
        public TimberbornFireSimPersistenceSnapshot CaptureFireSimState() { Reads++; return Snapshot; }
        public void RestoreFireSimState(TimberbornFireSimPersistenceSnapshot snapshot) => throw new NotSupportedException();
        public void RegisterChange(FireSimChange change) => throw new NotSupportedException();
        public GpuFireStepResult Tick() => throw new NotSupportedException();
        public IDisposable Subscribe(IFireSimListener listener) => throw new NotSupportedException();
    }
}
