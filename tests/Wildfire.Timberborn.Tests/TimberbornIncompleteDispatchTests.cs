using Wildfire.Core;
using Wildfire.Timberborn.Resources;
using Wildfire.Timberborn.Runtime;

namespace Wildfire.Timberborn.Tests;

public sealed class TimberbornIncompleteDispatchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ListenerFailureAfterActualCoreSwapBlocksSaveAndAnotherDispatch(bool ordinary)
    {
        using var f = new Fixture(ordinary: ordinary);
        var cause = new IOException("listener after buffer swap");
        using var subscription = f.Simulator.Subscribe(new Listener(() => throw cause));
        var failure = Assert.ThrowsAny<Exception>(f.Dispatch);
        Assert.Equal(1, f.Simulator.Swaps);
        Assert.Equal(0, f.NativeCalls);
        Assert.Equal(ordinary ? 0 : 1, f.Commits);
        var outcome = Assert.IsType<FireSimStepInputException>(failure);
        Assert.Equal(FireSimStepInputOutcome.Committed, outcome.Outcome);
        Assert.Same(cause, outcome.InnerException);
        f.AssertSaveAndContinuationBlocked();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeConsequenceFailureAfterReturnedStepBlocksSaveAndAnotherDispatch(bool misleadingNotApplied)
    {
        using var f = new Fixture();
        Exception cause = new IOException("native consequence mutated then failed");
        if (misleadingNotApplied) cause = new FireSimStepInputException(FireSimStepInputOutcome.NotApplied, cause);
        f.NativeEffect = () => throw cause;
        Assert.Same(cause, Assert.ThrowsAny<Exception>(f.Dispatch));
        Assert.Equal(1, f.Simulator.Swaps);
        Assert.Equal(1, f.Commits);
        Assert.Equal(1, f.NativeCalls);
        Assert.Equal(0, f.RuntimeFollowups);
        f.AssertSaveAndContinuationBlocked();
    }

    [Fact]
    public void FinalFireSystemLogFailureStillPreventsRequiredRuntimeFollowups()
    {
        using var f = new Fixture();
        f.Log.FailPrefix = "wildfire_timberborn_dispatch_completed";
        Assert.Throws<IOException>(f.Dispatch);
        Assert.Equal(1, f.NativeCalls);
        Assert.Equal(0, f.RuntimeFollowups);
        f.AssertSaveAndContinuationBlocked();
    }

    [Fact]
    public void ValidRejectedZeroReceiptStillCompletesExactlyOneDispatchWithoutResourceCommit()
    {
        using var f = new Fixture(rejectedApplication: true);
        f.Dispatch();
        Assert.Equal(1, f.Simulator.Swaps);
        Assert.Equal(1, f.NativeCalls);
        Assert.Equal(1, f.RuntimeFollowups);
        Assert.Equal(0, f.Commits);
        f.Guard.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void OrdinaryDispatchStillAllowsExistingGuardedNativeSinkWithoutNesting()
    {
        using var f = new Fixture(ordinary: true);
        int effects = 0;
        f.NativeEffect = () => f.Guard.TransferInventory(() => effects++);
        f.Dispatch();
        Assert.Equal(1, effects);
        Assert.Equal(1, f.RuntimeFollowups);
        f.Guard.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void DebugPresentationFailureBeforeNativeSinksIsIncompleteDelivery()
    {
        using var f = new Fixture(throwDebug: true);
        f.Simulator.Deltas = [new(0, 0, 16)];
        Assert.Throws<IOException>(f.Dispatch);
        Assert.Equal(0, f.NativeCalls);
        f.AssertSaveAndContinuationBlocked();
    }

    [Fact]
    public void ExistingCaughtVisualEffectFailureDoesNotPreventNativeCompletion()
    {
        using var f = new Fixture(throwVisual: true);
        f.Simulator.Deltas = [new(0, 0, 16)];
        f.Dispatch();
        Assert.Equal(1, f.NativeCalls);
        Assert.Equal(1, f.RuntimeFollowups);
        f.Guard.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void FailedDiagnosticCannotReplaceNativeFailureOrAvoidInvalidation()
    {
        using var f = new Fixture();
        var cause = new IOException("original native cause");
        f.NativeEffect = () => throw cause;
        f.Log.FailWarnings = true;
        Assert.Same(cause, Assert.Throws<IOException>(f.Dispatch));
        f.AssertSaveAndContinuationBlocked();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NotAppliedResetFailureLeavesSaveSafeAndCanDispatchLater(bool ordinary)
    {
        using var f = new Fixture(ordinary: ordinary);
        f.Simulator.ResetFailure = new IOException("reset before mutation");
        var failure = Assert.Throws<FireSimStepInputException>(f.Dispatch);
        Assert.Equal(FireSimStepInputOutcome.NotApplied, failure.Outcome);
        Assert.Equal(0, f.Simulator.Swaps);
        Assert.Equal(0, f.Commits);
        f.Guard.ThrowIfSaveUnsafe();
        f.Simulator.ResetFailure = null;
        f.Dispatch();
        Assert.Equal(1, f.RuntimeFollowups);
    }

    [Fact]
    public void PreStepLoggingFailureDoesNotPoisonAnUnstartedStep()
    {
        using var f = new Fixture();
        f.Log.FailPrefix = "wildfire_timberborn_dispatch_started";
        Assert.Throws<IOException>(f.Dispatch);
        Assert.Equal(0, f.Simulator.Swaps);
        f.Guard.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void PrivateResourceCommitFailureKeepsExistingIndeterminateClassification()
    {
        using var f = new Fixture();
        f.CommitEffect = () => throw new IOException("native resource callback");
        var failure = Assert.Throws<FireSimStepInputException>(f.Dispatch);
        Assert.Equal(FireSimStepInputOutcome.Indeterminate, failure.Outcome);
        Assert.Equal(0, f.NativeCalls);
        f.AssertSaveAndContinuationBlocked();
    }

    private sealed class Fixture : IDisposable, ITimberbornFireDispatchHost
    {
        internal readonly NativeResourceTransaction Guard = new();
        internal readonly Simulator Simulator = new();
        internal readonly Log Log = new();
        private readonly TimberbornFireSystem _system;
        private readonly TimberbornFixedCadenceFireDispatcher _dispatcher;
        private long _update;
        private readonly bool _ordinary, _rejectedApplication;
        internal Action NativeEffect = () => { };
        internal Action CommitEffect = () => { };
        internal int Commits, NativeCalls, RuntimeFollowups;

        internal Fixture(bool ordinary = false, bool rejectedApplication = false, bool throwDebug = false, bool throwVisual = false)
        {
            _system = new(Simulator, new TimberbornFireCellMapper(), Log,
                new(burnDamageSink: new DamageSink(() => { NativeCalls++; NativeEffect(); }),
                    debugVisualSink: throwDebug ? new DebugSink() : null, visualEffectSink: throwVisual ? new VisualSink() : null));
            _ordinary = ordinary;
            _rejectedApplication = rejectedApplication;
            _system.HostDispatch = this;
            _dispatcher = new(_system);
        }
        public GpuFireStepResult Tick()
        {
            Guard.ThrowIfSaveUnsafe();
            if (_rejectedApplication)
                return Guard.TryApplyCleanAsh(Simulator, new(0, 2), _ => { Commits++; CommitEffect(); })!.Value.Step;
            return _ordinary ? Simulator.Tick() : Guard.TryDeliver(Simulator, new(0, AddHeat: 1),
                () => { Commits++; CommitEffect(); })!.Value;
        }
        public void ThrowIfSaveUnsafe() => Guard.ThrowIfSaveUnsafe();
        public void InvalidateIncompleteDispatch() => Guard.InvalidateAfterLifecycleFailure();
        internal void Dispatch()
        {
            var result = _dispatcher.Update(new TimberbornFireUpdate(++_update, TimeSpan.FromSeconds(1)));
            if (result.DidDispatch) RuntimeFollowups++; // Models only whether real UpdateSingleton can reach its next lines.
        }
        internal void AssertSaveAndContinuationBlocked()
        {
            Assert.Throws<InvalidOperationException>(Guard.ThrowIfSaveUnsafe);
            int swaps = Simulator.Swaps;
            _system.SustainedIgnition.Start([new(0, AddHeat: 3)], "blocked-next-preparation");
            int queued = Simulator.RegisteredChanges;
            Assert.Throws<InvalidOperationException>(Dispatch);
            Assert.Equal(swaps, Simulator.Swaps);
            Assert.Equal(queued, Simulator.RegisteredChanges); // Gate runs before QA/repeated-input preparation.

        }
        public void Dispose() => _system.Dispose();
    }

    internal sealed class Simulator : IFireSimAshCollectionSimulator, IFireSimAshApplicationSimulator, IFireSimAshCollectionBackend
    {
        private readonly FireSimStepCoordinator _core = new(1, 8);
        internal Exception? ResetFailure;
        internal int Swaps, RegisteredChanges;
        internal CellDelta[] Deltas = [];
        private FireSimChange[] _changes = [];
        public int Width => 1;
        public int Height => 1;
        public int Depth => 1;
        public GpuFireStepResult Tick() => _core.Tick(this);
        public GpuFireStepResult? TryTickWithInput(FireSimChange input, Action commit) => _core.TryTickWithInput(this, input, commit);
        public GpuFireStepResult? TryCollectAsh(FireSimAshCollectionInput input, Action<FireSimAshCollectionReceipt> commit) =>
            _core.TryCollectAsh(this, input, commit);
        public FireSimAshApplicationStepResult? TryApplyCleanAsh(FireSimAshApplicationInput input,
            Action<FireSimAshApplicationReceipt> commit) => _core.TryApplyCleanAsh(this, input, commit);
        public FireSimGpuChange ReadAppliedChange(int index)
        {
            var c = FireSimGpuProtocol.EncodeChange(_changes[index]);
            return new(c.CellIndex, c.SetMask, c.AddFields | (1u << 29) | ((uint)FireSimAshApplicationOutcome.Full << 30), c.SetValues);
        }
        public IDisposable Subscribe(IFireSimListener listener) => _core.Subscribe(listener);
        public void RegisterChange(FireSimChange change) { RegisteredChanges++; _core.RegisterChange(change); }
        public void ResetDeltaCounter(uint tick) { if (ResetFailure is not null) throw ResetFailure; }
        public void ApplyExternalChanges(uint tick, FireSimChange[] changes) => _changes = changes;
        public void Simulate(uint tick) { }
        public CellDelta[] ReadDeltas(uint tick) => Deltas;
        public void SwapBuffers(uint tick) => Swaps++;
    }
    private sealed class Listener(Action callback) : IFireSimListener
    {
        public void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas) => callback();
    }
    private sealed class DamageSink(Action callback) : ITimberbornBurnDamageSink
    {
        public TimberbornBurnDamageApplySummary ApplyDamage(uint tick, IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
        { callback(); return TimberbornBurnDamageApplySummary.Empty; }
    }
    private sealed class DebugSink : ITimberbornFireDebugVisualSink
    { public void UpdateDebugVisualState(TimberbornFireDebugVisualCellState state) => throw new IOException("debug presentation"); }
    private sealed class VisualSink : ITimberbornFireVisualEffectSink
    { public void UpdateVisualEffect(TimberbornFireVisualEffectEvent effectEvent) => throw new IOException("visual presentation"); }
    private sealed class Log : ITimberbornFireLogSink
    {
        internal string? FailPrefix;
        internal bool FailWarnings;
        public void Info(string message) { if (FailPrefix is not null && message.StartsWith(FailPrefix)) throw new IOException("observational log"); }
        public void Warning(string message) { if (FailWarnings) throw new IOException("warning failed"); }
    }
}
