using Wildfire.Timberborn.FireResponse;

namespace Wildfire.Timberborn.Tests;

public sealed class WardenPresentationTests
{
    private static readonly WardenStationViewState Ready = new(false, true, true, true, 1, false,
        WardenPhase.Idle, WardenResponseReason.None);

    [Fact]
    public void RecoveryInstructionOverridesPausedUnstaffedOrReturningStatus()
    {
        var state = Ready with { UnsafeWaterState = true, Operational = false, AssignedWorkers = 0,
            Phase = WardenPhase.Returning, Reason = WardenResponseReason.UnsafeRoute };
        Assert.Equal("Wildfire.Warden.Recovery", WardenStationPresentation.StatusKey(state));
    }

    [Fact]
    public void DisablingNewResponsesDoesNotHideAnActiveReturn()
    {
        Assert.Equal("Wildfire.Warden.Returning", WardenStationPresentation.StatusKey(
            Ready with { Operational = false, ResponseEnabled = false, Phase = WardenPhase.Returning }));
        Assert.Equal("Wildfire.Warden.Disabled", WardenStationPresentation.StatusKey(
            Ready with { ResponseEnabled = false }));
    }

    [Fact]
    public void NoWorkerDoesNotShowThePreviousWorkersResponse()
    {
        Assert.Equal("Wildfire.Warden.NoWorker", WardenStationPresentation.StatusKey(
            Ready with { AssignedWorkers = 0, Reason = WardenResponseReason.Applied }));
    }

    [Fact]
    public void PendingAndCommittedWaterHaveDistinctPlayerMessages()
    {
        Assert.Equal("Wildfire.Warden.Applying", WardenStationPresentation.StatusKey(
            Ready with { Phase = WardenPhase.AwaitingApplication }));
        Assert.Equal("Wildfire.Warden.Applied", WardenStationPresentation.ReasonKey(WardenResponseReason.Applied, "Ready"));
        Assert.Equal("Wildfire.Warden.Complete", WardenStationPresentation.StatusKey(
            Ready with { Reason = WardenResponseReason.Complete }));
    }

    [Fact]
    public void RestockingExplainsNativeCarryWorkInsteadOfStaleTargetFailure()
    {
        Assert.Equal("Wildfire.Warden.Restocking", WardenStationPresentation.StatusKey(
            Ready with { Restocking = true, Reason = WardenResponseReason.NoSafeFire }));
    }

    [Fact]
    public void ThrowingOrReentrantNotificationCannotRepeatOrClearUnsafeWaterState()
    {
        var transaction = new WardenDeliveryTransaction();
        var notice = new WardenRecoveryNotice();
        var inventoryError = new InvalidOperationException("native event after mutation");
        Assert.Same(inventoryError, Assert.Throws<InvalidOperationException>(() =>
            transaction.TransferInventory(() => throw inventoryError)));
        int attempts = 0;
        Assert.Throws<InvalidOperationException>(() => notice.ShowIfNeeded(transaction.IsIndeterminate, () =>
        {
            attempts++;
            notice.ShowIfNeeded(transaction.IsIndeterminate, () => attempts++);
            throw new InvalidOperationException("notification subscriber failed");
        }));
        notice.ShowIfNeeded(transaction.IsIndeterminate, () => attempts++);
        Assert.Equal(1, attempts);
        Assert.True(transaction.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(transaction.ThrowIfSaveUnsafe);
    }

    [Fact]
    public void OnlyWorldLoadRearmsTheRecoveryNotice()
    {
        var notice = new WardenRecoveryNotice();
        int attempts = 0;
        notice.ShowIfNeeded(false, () => attempts++);
        notice.ShowIfNeeded(true, () => attempts++);
        notice.ShowIfNeeded(false, () => attempts++);
        notice.ShowIfNeeded(true, () => attempts++);
        Assert.Equal(1, attempts);
        notice.ResetForWorldLoad();
        notice.ShowIfNeeded(true, () => attempts++);
        Assert.Equal(2, attempts);
    }
}
