using Wildfire.Core;
using Wildfire.Timberborn.FireResponse;

namespace Wildfire.Timberborn.Tests;

public sealed class WardenDeliveryTransactionTests
{
    [Fact]
    public void CapacityRejectionLeavesWaterAndPendingJobUnchanged()
    {
        var transaction = new WardenDeliveryTransaction();
        var consumed = 0;
        var result = transaction.TryDeliver(new StepSimulator { Reject = true }, new(0, AddWater: 3), () => consumed++);
        Assert.Null(result);
        Assert.Equal(0, consumed);
        transaction.ThrowIfSaveUnsafe();
    }

    [Theory]
    [InlineData(FireSimStepInputOutcome.NotApplied, false)]
    [InlineData(FireSimStepInputOutcome.Indeterminate, true)]
    [InlineData(FireSimStepInputOutcome.Committed, false)]
    public void OnlyAmbiguousMutationPoisonsTheSession(FireSimStepInputOutcome outcome, bool poisoned)
    {
        var transaction = new WardenDeliveryTransaction();
        var consumed = 0;
        Assert.Throws<FireSimStepInputException>(() => transaction.TryDeliver(
            new StepSimulator { Failure = outcome }, new(0, AddWater: 3), () => consumed++));
        Assert.Equal(poisoned, transaction.IsIndeterminate);
        Assert.Equal(outcome == FireSimStepInputOutcome.Committed ? 1 : 0, consumed);
        if (poisoned)
        {
            Assert.Throws<InvalidOperationException>(transaction.ThrowIfSaveUnsafe);
            Assert.Throws<InvalidOperationException>(() => transaction.TryDeliver(new StepSimulator(), new(0), () => consumed++));
            Assert.Equal(0, consumed);
        }
        else transaction.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void SaveAndWorldReplacementCannotReenterDelivery()
    {
        var transaction = new WardenDeliveryTransaction();
        transaction.TryDeliver(new StepSimulator(), new(0), () =>
        {
            Assert.Throws<InvalidOperationException>(transaction.ThrowIfSaveUnsafe);
            Assert.Throws<InvalidOperationException>(transaction.ResetForWorldLoad);
        });
        transaction.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void StockMutationFailurePoisonsUntilExplicitWorldLoad()
    {
        var transaction = new WardenDeliveryTransaction();
        Assert.Throws<FireSimStepInputException>(() => transaction.TryDeliver(new StepSimulator(), new(0),
            () => throw new InvalidOperationException("native consume failed")));
        Assert.True(transaction.IsIndeterminate);
        transaction.ResetForWorldLoad();
        transaction.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void PairedTransferFailureAfterSourceMutationIsNotRefundedOrSaveable()
    {
        var transaction = new WardenDeliveryTransaction();
        int source = 1, destination = 0;
        var failure = new InvalidOperationException("inventory event failed after take");
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => transaction.TransferInventory(() =>
        {
            Assert.Throws<InvalidOperationException>(transaction.ThrowIfSaveUnsafe);
            source--;
            throw failure;
        })));
        Assert.Equal(0, source);
        Assert.Equal(0, destination);
        Assert.True(transaction.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(transaction.ThrowIfSaveUnsafe);
    }

    [Fact]
    public void SuccessfulTransferAllowsSavingOnlyAfterBothMutations()
    {
        var transaction = new WardenDeliveryTransaction();
        int source = 1, destination = 0;
        transaction.TransferInventory(() =>
        {
            source--;
            Assert.Throws<InvalidOperationException>(transaction.ThrowIfSaveUnsafe);
            destination++;
        });
        Assert.Equal(0, source);
        Assert.Equal(1, destination);
        transaction.ThrowIfSaveUnsafe();
    }

    private sealed class StepSimulator : IFireSimStepInputSimulator
    {
        public bool Reject { get; init; }
        public FireSimStepInputOutcome? Failure { get; init; }
        public int Width => 1;
        public int Height => 1;
        public int Depth => 1;
        public void RegisterChange(FireSimChange change) => throw new NotSupportedException();
        public IDisposable Subscribe(IFireSimListener listener) => throw new NotSupportedException();
        public GpuFireStepResult Tick() => throw new NotSupportedException();
        public GpuFireStepResult? TryTickWithInput(FireSimChange input, Action commitInput)
        {
            if (Reject) return null;
            if (Failure is FireSimStepInputOutcome.NotApplied or FireSimStepInputOutcome.Indeterminate)
                throw new FireSimStepInputException(Failure.Value, new InvalidOperationException("dispatch failed"));
            try { commitInput(); }
            catch (Exception exception) { throw new FireSimStepInputException(FireSimStepInputOutcome.Indeterminate, exception); }
            if (Failure is FireSimStepInputOutcome.Committed)
                throw new FireSimStepInputException(Failure.Value, new InvalidOperationException("observer failed"));
            return new GpuFireStepResult(Array.Empty<CellDelta>(), 1);
        }
    }
}
