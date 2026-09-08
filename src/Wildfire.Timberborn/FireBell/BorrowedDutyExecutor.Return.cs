using Timberborn.BlockingSystem;
using Timberborn.BehaviorSystem;
using Timberborn.GameDistricts;
using Timberborn.InventorySystem;
using Timberborn.Navigation;
using UnityEngine;
using Wildfire.Timberborn.FireResponse;
using Wildfire.Timberborn.Compatibility;

namespace Wildfire.Timberborn.FireBell;

public sealed partial class BorrowedDutyExecutor
{
    private BorrowedDutyBehavior _returnBehavior = null!;
    private Inventory? _returnInventory;
    private bool _returnOnly;
    private bool ReturnEligible => _returnBehavior.RecoveryReady && !_resources.IsIndeterminate &&
        !_mortal.Dead && !_mortal.ShouldDie && !_needs.AnyNeedIsInCriticalState() &&
        _citizen.HasAssignedDistrict && !_carrier.IsCarrying && BorrowedDutyBehavior.HasReturnWater(_equipment.Inventory) && _field.ObservationAvailable;
    internal bool HasReservations => _reserver.StockReservation.Inventory is not null || _reserver.CapacityReservation.Inventory is not null;

    internal bool TryLaunchReturn(Inventory? inventory)
    {
        if (!string.IsNullOrEmpty(_manager.RunningExecutor.Name) || !ReturnEligible || inventory is null ||
            !ReferenceEquals(inventory, _returnBehavior.ReturnDestination)) return false;
        TimberbornOwnedWalker.Verify();
        if (HasReservations || !UsableReturnInventory(inventory, requireCapacity: true)) return false;
        _returnInventory = inventory;
        foreach (var point in ReturnAccesses(inventory))
        {
            if (!_field.SafeRoute(_navigator.CurrentAccessOrPosition(), point, escaping: true)) continue;
            if (!ReturnEligible || HasReservations || !ReferenceEquals(inventory, _returnBehavior.ReturnDestination) ||
                !string.IsNullOrEmpty(_manager.RunningExecutor.Name) || !UsableReturnInventory(inventory, requireCapacity: true)) return false;
            _progress.Begin();
            _progress.Return();
            _returnOnly = true;
            _restored = false;
            _donor = null;
            try { if (Launch(point)) return true; }
            catch { FinishReturn(); throw; }
            FinishReturn();
            return false;
        }
        return false;
    }
    private bool UsableReturnInventory(Inventory inventory, bool requireCapacity)
    {
        if (!inventory || !inventory.Enabled || ReferenceEquals(inventory, _equipment.Inventory) ||
            inventory.GetComponent<DistrictBuilding>() is not { } building ||
            !ReferenceEquals(building.District, _citizen.AssignedDistrict) ||
            inventory.GetComponent<IInventoryValidator>() is not { ValidInventory: true } ||
            inventory.GetComponent<BlockableObject>() is not { IsUnblocked: true }) return false;
        return !requireCapacity || inventory.HasUnreservedCapacity(WardenEquipment.Bucket) &&
            _citizen.AssignedDistrict.GetComponent<DistrictInventoryRegistry>()
                .ActiveInventoriesWithCapacity(WardenEquipment.WaterId).Contains(inventory);
    }
    private static Vector3[] ReturnAccesses(Inventory inventory)
    {
        var accesses = new List<Accessible>();
        inventory.GetComponents(accesses);
        return accesses.Where(access => access.ValidAccessible && access.UnblockedSingleAccess.HasValue)
            .Select(access => access.UnblockedSingleAccess!.Value).ToArray();
    }
    private bool ExactReturnReservation() => OwnReturnCapacity() && _reserver.StockReservation.Inventory is null;
    private bool OwnReturnCapacity()
    {
        var reservation = _reserver.CapacityReservation;
        return _returnInventory is not null && ReferenceEquals(reservation.Inventory, _returnInventory) &&
            reservation.GoodAmount.GoodId == WardenEquipment.WaterId && reservation.GoodAmount.Amount == 1 &&
            reservation.FixedAmount && !reservation.ConsumeGood;
    }
    private void ReleaseReturnReservation()
    {
        if (OwnReturnCapacity()) _reserver.UnreserveCapacity();
    }
    private ExecutorStatus TickReturn(float hours)
    {
        if (_mortal.Dead || _mortal.ShouldDie) return FinishReturn(dying: true);
        if (_resources.IsIndeterminate) { _movement.RejectRoute(); return ExecutorStatus.Running; }
        if (_returnInventory is null || !ReturnEligible ||
            !ReferenceEquals(_returnBehavior.ReturnDestination, _returnInventory)) return FinishReturn();
        if (_restored)
        {
            _restored = false;
            _movement.Stop();
            // No capacity is held while walking. Re-arbitrate instead of trusting a saved endpoint.
            return FinishReturn();
        }
        _progress.Advance(hours);
        if (_progress.Hours >= 2 || HasReservations ||
            !UsableReturnInventory(_returnInventory, requireCapacity: false) || !_movement.RefreshIfNeeded()) return FinishReturn();
        var status = _movement.Tick(hours);
        if (status == ExecutorStatus.Running) return status;
        if (status == ExecutorStatus.Failure || !At(_destination) || !ReturnAccesses(_returnInventory).Any(At) ||
            !_field.SafePosition(_navigator.CurrentAccessOrPosition())) return FinishReturn();
        _movement.Stop();
        TryDepositAtArrival(_returnInventory);
        return FinishReturn();
    }
    private void TryDepositAtArrival(Inventory expected)
    {
        bool reserved = false;
        try
        {
            _resources.TransferInventory(() =>
            {
                if (HasReservations || !expected.HasUnreservedCapacity(WardenEquipment.Bucket)) return;
                RequireReturnOwner(expected);
                _reserver.ReserveCapacity(expected, WardenEquipment.Bucket);
                RequireReturnOwner(expected);
                if (!ExactReturnReservation()) throw new InvalidOperationException("Borrowed return reservation changed during admission.");
                reserved = true;
            });
            if (!reserved) return;
            RequireReturnOwner(expected);
            _equipment.TryReturn(expected, _reserver, () =>
            {
                RequireReturnOwner(expected);
                _returnBehavior.Returned();
                _progress.Finish();
            });
        }
        finally
        {
            if (reserved && !_resources.IsIndeterminate) _resources.TransferInventory(ReleaseReturnReservation);
        }
    }
    private void RequireReturnOwner(Inventory expected)
    {
        if (!_manager.IsRunningExecutor<BorrowedDutyExecutor>() || !_returnOnly || Phase != BorrowedDutyPhase.Returning ||
            !_returnBehavior.ReturnAssigned || !ReferenceEquals(_returnBehavior.ReturnDestination, expected) ||
            !ReferenceEquals(_returnInventory, expected) || _mortal.Dead || _mortal.ShouldDie ||
            !_citizen.HasAssignedDistrict || !UsableReturnInventory(expected, requireCapacity: false) ||
            !At(_destination) || !ReturnAccesses(expected).Any(At) || !_field.SafePosition(_navigator.CurrentAccessOrPosition()))
            throw new InvalidOperationException("Borrowed return lost its native execution or destination.");
    }
    private ExecutorStatus FinishReturn(bool dying = false)
    {
        try
        {
            _movement.Stop();
            if (!_resources.IsIndeterminate || dying) ClearReturn();
        }
        catch when (dying) { _resources.InvalidateAfterLifecycleFailure(); ClearReturn(); }
        if (!dying && !_resources.IsIndeterminate) _movement.ReleasePause();
        return dying ? ExecutorStatus.Failure : ExecutorStatus.Success;
    }
    private void ClearReturn()
    {
        _progress.Finish();
        _returnOnly = false;
        _returnInventory = null;
        _returnBehavior.Defer();
    }
}
