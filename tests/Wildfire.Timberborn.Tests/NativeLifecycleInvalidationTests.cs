using Wildfire.Timberborn.Resources;
using Wildfire.Core;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeLifecycleInvalidationTests
{
    [Fact]
    public void CaughtReentrantTeardownInvalidatesOriginalCaptureAndEveryLaterSave()
    {
        var transaction = new NativeResourceTransaction();
        bool nativeDeathContinued = false;
        bool captured = false;
        Assert.Throws<InvalidOperationException>(() =>
        {
            transaction.CaptureAtRest(() =>
            {
                try { transaction.TransferInventory(() => throw new Exception("must never enter")); }
                catch (InvalidOperationException) { transaction.InvalidateAfterLifecycleFailure(); }
                nativeDeathContinued = true;
                return 1;
            });
            captured = true;
        });
        Assert.True(nativeDeathContinued);
        Assert.False(captured);
        Assert.True(transaction.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(transaction.ThrowIfSaveUnsafe);
        Assert.Throws<InvalidOperationException>(() => transaction.CaptureAtRest(() => 2));
        // The capture latch still clears in finally; only the real world-load reset may recover.
        transaction.ResetForWorldLoad();
        Assert.Equal(3, transaction.CaptureAtRest(() => 3));
    }

    [Fact]
    public void OrdinaryReadFailureDoesNotPoisonButExplicitLifecycleInvalidationSurvivesIt()
    {
        var transaction = new NativeResourceTransaction();
        var failure = new IOException("native read failed");
        Assert.Same(failure, Assert.Throws<IOException>(() => transaction.CaptureAtRest<int>(() => throw failure)));
        Assert.False(transaction.IsIndeterminate);
        transaction.ThrowIfSaveUnsafe();
        Assert.Same(failure, Assert.Throws<IOException>(() => transaction.CaptureAtRest<int>(() =>
        {
            transaction.InvalidateAfterLifecycleFailure();
            throw failure;
        })));
        Assert.True(transaction.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(transaction.ThrowIfSaveUnsafe);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LifecycleInvalidationRejectsReturnedStepAndNullAdmission(bool noAdmission)
    {
        var transaction = new NativeResourceTransaction();
        var simulator = new LifecycleSimulator(transaction.InvalidateAfterLifecycleFailure, noAdmission);
        var error = Assert.Throws<FireSimStepInputException>(() => transaction.TryDeliver(simulator, new(0), () => { }));
        Assert.Equal(FireSimStepInputOutcome.Indeterminate, error.Outcome);
        Assert.True(transaction.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(transaction.ThrowIfSaveUnsafe);
        transaction.ResetForWorldLoad();
        transaction.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void LifecycleInvalidationPreventsTransferSuccessAndPreservesAlreadyThrownCause()
    {
        var transaction = new NativeResourceTransaction();
        int stock = 1;
        Assert.Throws<InvalidOperationException>(() => transaction.TransferInventory(() =>
        {
            stock--;
            transaction.InvalidateAfterLifecycleFailure();
        }));
        Assert.Equal(0, stock);
        Assert.True(transaction.IsIndeterminate);
        transaction.ResetForWorldLoad();
        var original = new IOException("original native callback");
        Assert.Same(original, Assert.Throws<IOException>(() => transaction.TransferInventory(() =>
        {
            transaction.InvalidateAfterLifecycleFailure();
            throw original;
        })));
    }

    private sealed class LifecycleSimulator(Action onAttempt, bool noAdmission) : IFireSimStepInputSimulator
    {
        public int Width => 1;
        public int Height => 1;
        public int Depth => 1;
        public GpuFireStepResult? TryTickWithInput(FireSimChange input, Action commit)
        {
            onAttempt();
            if (noAdmission) return null;
            commit();
            return new([], 1);
        }
        public void RegisterChange(FireSimChange change) => throw new NotSupportedException();
        public IDisposable Subscribe(IFireSimListener listener) => throw new NotSupportedException();
        public GpuFireStepResult Tick() => throw new NotSupportedException();
    }

}
