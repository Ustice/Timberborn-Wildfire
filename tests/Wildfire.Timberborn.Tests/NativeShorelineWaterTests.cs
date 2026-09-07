using System.Reflection;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeShorelineWaterTests
{
    [Fact]
    public void NativeCompletedCoordinateCreditFeedsOnlyOneOfTwoSequentialBuckets()
    {
        using var f = new NativeShorelineWaterFixture();
        Assert.Equal(.2f, f.BucketVolume); // Actual installed converter: one Water good, not one fire band.
        var first = f.Bucket(); var second = f.Bucket();
        Assert.False(f.TryFill(f.Source, first));
        Assert.False(f.TryFill(f.Source, second));
        Assert.Equal(2, f.PendingRequests);
        Assert.Equal(0, f.Stock(first) + f.Stock(second));
        f.SupplyCompletedNativeReceipt(f.BucketVolume);
        f.CreditNativeInputs();
        Assert.True(f.TryFill(f.Source, first));
        Assert.False(f.TryFill(f.Source, second));
        Assert.False(f.TryFill(f.Source, first)); // Already loaded actor cannot claim another bucket.
        Assert.Equal(1, f.Stock(first) + f.Stock(second));
        Assert.Equal(0f, f.Buffer(f.Source));
        f.Transaction.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void NativeCoordinateCreditIsNonConsumingAndDuplicatesAcrossInputs()
    {
        using var f = new NativeShorelineWaterFixture();
        var secondInput = f.NewInput();
        f.SupplyCompletedNativeReceipt(f.BucketVolume);
        f.CreditNativeInputs();
        Assert.Equal(f.BucketVolume, f.Buffer(f.Source));
        Assert.Equal(f.BucketVolume, f.Buffer(secondInput));
        var bucket = f.Bucket();
        Assert.Throws<InvalidOperationException>(() => f.TryFill(f.Source, bucket));
        Assert.Equal(0, f.Stock(bucket));
        Assert.Equal(0, f.PendingRequests);
    }

    [Fact]
    public void DuplicateRegistrationOrUnregisteredSourceCannotAdmitCargo()
    {
        using var f = new NativeShorelineWaterFixture();
        NativeShorelineWaterFixture.Call(f.Source, "OnEnterFinishedState");
        Assert.Throws<InvalidOperationException>(() => f.TryFill(f.Source, f.Bucket()));
        Assert.Throws<InvalidOperationException>(() => f.TryFill(f.NewInput(false, 7), f.Bucket()));
        Assert.Equal(0, f.PendingRequests);
    }

    [Fact]
    public void AdmissionOnlyUniquenessCannotDetectPreviouslyDuplicatedCredit()
    {
        using var f = new NativeShorelineWaterFixture();
        var competingInput = f.NewInput();
        var competingProduct = f.Bucket();
        f.SupplyCompletedNativeReceipt(f.BucketVolume);
        f.CreditNativeInputs(); // Native coordinate receipt is copied into both buffers.
        NativeShorelineWaterFixture.Call(competingInput, "RemoveCleanWater", f.BucketVolume);
        NativeShorelineWaterFixture.Call(competingProduct, "GiveProduced", Activator.CreateInstance(f.Type("Timberborn.Goods", "Timberborn.Goods.GoodAmount"), "Water", 1)!);
        NativeShorelineWaterFixture.Call(competingInput, "OnExitFinishedState");
        var firefighter = f.Bucket();
        Assert.True(f.TryFill(f.Source, firefighter)); // This local check passes after the competitor leaves.
        Assert.Equal(2, f.Stock(competingProduct) + f.Stock(firefighter));
        // Counterexample: safe source lifetime/credit-boundary ownership is a separate prerequisite.
    }

    [Theory]
    [InlineData(.1f, 0f)]
    [InlineData(0f, .2f)]
    public void PartialOrContaminatedOnlyCreditCannotCreateCleanBucket(float clean, float contaminated)
    {
        using var f = new NativeShorelineWaterFixture();
        var bucket = f.Bucket();
        f.SupplyCompletedNativeReceipt(clean, contaminated);
        f.CreditNativeInputs();
        Assert.False(f.TryFill(f.Source, bucket));
        Assert.Equal(0, f.Stock(bucket));
        Assert.Equal(clean, f.Buffer(f.SaveLoadInput(f.Source)));
    }

    [Fact]
    public void SharedNativeInputPersistsUnusedBufferAndDebitWithoutAnotherWaterLedger()
    {
        using var f = new NativeShorelineWaterFixture();
        f.SupplyCompletedNativeReceipt(f.BucketVolume * 2);
        f.CreditNativeInputs();
        var first = f.Bucket(); var second = f.Bucket();
        Assert.True(f.TryFill(f.Source, first));
        Assert.Equal(f.BucketVolume, f.Buffer(f.SaveLoadInput(f.Source)));
        Assert.True(f.TryFill(f.Source, second));
        Assert.Equal(0f, f.Buffer(f.SaveLoadInput(f.Source)));
        Assert.Equal(2, f.Stock(first) + f.Stock(second));
    }

    [Fact]
    public void PendingUncreditedRemovalCanBeAbandonedOnReloadWithoutPhantomCargo()
    {
        using var f = new NativeShorelineWaterFixture();
        var bucket = f.Bucket();
        Assert.False(f.TryFill(f.Source, bucket));
        f.SupplyCompletedNativeReceipt(f.BucketVolume); // Native worker finished; input has not credited it yet.
        var restored = f.SaveLoadInput(f.Source);
        Assert.Equal(0f, f.Buffer(restored));
        Assert.Equal(0, f.Stock(bucket));
        // The native credit dictionary is not part of WaterInput.Save. No refund or forced credit is invented.
        Assert.Equal(0f, f.Demand(restored));
        Assert.Equal(0, f.Stock(bucket));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeStockCallbackOrPostGivePhaseFailurePoisonsWithoutRetry(bool failPhase)
    {
        using var f = new NativeShorelineWaterFixture();
        f.SupplyCompletedNativeReceipt(f.BucketVolume);
        f.CreditNativeInputs();
        var bucket = f.Bucket();
        var cause = new InvalidOperationException("failure after native stock mutation");
        f.OnInventoryChanged(bucket, () =>
        {
            Assert.Throws<InvalidOperationException>(f.Transaction.ThrowIfSaveUnsafe);
            if (!failPhase) throw cause;
        });
        if (failPhase) Assert.Same(cause, Assert.Throws<InvalidOperationException>(() => f.TryFill(f.Source, bucket, () => throw cause)));
        else Assert.Same(cause, Assert.Throws<TargetInvocationException>(() => f.TryFill(f.Source, bucket)).InnerException);
        Assert.Equal(0f, f.Buffer(f.Source));
        Assert.Equal(1, f.Stock(bucket));
        Assert.True(f.Transaction.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(f.Transaction.ThrowIfSaveUnsafe);
        Assert.Throws<InvalidOperationException>(() => f.TryFill(f.Source, bucket));
        Assert.Equal(1, f.Stock(bucket));
    }
}
