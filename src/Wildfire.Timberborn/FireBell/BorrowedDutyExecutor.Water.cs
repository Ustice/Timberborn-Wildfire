using Timberborn.BehaviorSystem;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.WorkSystem;
using UnityEngine;
using Wildfire.Core;
using Wildfire.Timberborn.FireResponse;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.FireBell;

public sealed partial class BorrowedDutyExecutor : INativeWaterApplicationProducer, INativeNaturalWaterCollector
{
    private BorrowedWaterTrip? _waterTrip;
    private bool _waterIntent, _waterRoutePending;
    internal bool WaterIntent => _waterIntent;
    internal int? WaterStock => _equipment?.Inventory?.AmountInStock(WardenEquipment.WaterId);
    internal Guid? WaterSourceId => _waterTrip?.Source.Entity.EntityId;
    internal bool ReturnAssigned => _returnBehavior is not null && _returnBehavior.ReturnAssigned;
    internal bool RetainedWater => ReturnAssigned && WaterStock > 0;

    internal bool PrepareWaterOffer(Workplace donor)
    {
        if (!_entity || !_entity.Initialized || _entity.Deleted || !_worker.Employed ||
            !ReferenceEquals(_worker.Workplace, donor) || Phase != BorrowedDutyPhase.Idle) return false;
        _returnBehavior.PrepareRecovery();
        return true;
    }

    internal bool TryLaunchWater(Workplace donor, BorrowedWaterTrip trip)
    {
        if (Phase != BorrowedDutyPhase.Idle || !_returnBehavior.RecoveryPrepared || !_equipment.Inventory.Enabled || _equipment.Inventory.Stock.Any(good => good.Amount > 0) || !CanBorrow(donor) || !trip.SourceUsable || !trip.TargetUsable(_field) ||
            !UsableReturnInventory(trip.ReturnInventory, requireCapacity: true)) return false;
        TimberbornOwnedWalker.Verify();
        var origin = _navigator.CurrentAccessOrPosition();
        if ((trip.Shore - origin).sqrMagnitude > 16 * 16 || (trip.Approach - origin).sqrMagnitude > 16 * 16 ||
            !_field.SafeRoute(origin, trip.Shore)) return false;
        // The native workplace has not yet transferred executor ownership. Install recovery before any pickup.
        _returnBehavior.BindReturn(trip.ReturnInventory);
        if (!CanBorrow(donor) || !trip.SourceUsable || !trip.TargetUsable(_field) || !_returnBehavior.RecoveryReady ||
            !ReferenceEquals(_returnBehavior.ReturnDestination, trip.ReturnInventory)) return false;
        _origin = origin; _donor = donor; _waterTrip = trip;
        _waterIntent = true; _waterRoutePending = false; _restored = false;
        _progress.BeginWater();
        try { if (Launch(trip.Shore)) return true; }
        catch { FinishWater(); throw; }
        FinishWater(); return false;
    }

    private bool CanBorrow(Workplace donor)
    {
        if (!_field.Ready || !donor || !donor.Enabled || donor.GetComponent<WardenStation>() is not null) return false;
        var district = donor.GetComponent<DistrictBuilding>();
        return new BorrowedDutyEligibility(_worker.Employed && ReferenceEquals(_worker.Workplace, donor),
            district is not null && _citizen.HasAssignedDistrict && ReferenceEquals(_citizen.AssignedDistrict, district.District),
            _hours.AreWorkingHours, _refuser.RefusesWork, _needs.AnyNeedIsInCriticalState(), _mortal.Dead || _mortal.ShouldDie,
            _carrier.IsCarrying, HasReservations, _equipment.Loaded, !string.IsNullOrEmpty(_manager.RunningExecutor.Name),
            _resources.IsIndeterminate).CanJoin;
    }

    private bool CurrentWaterActor(BorrowedWaterTrip trip) => _entity && _entity.Initialized && !_entity.Deleted &&
        _waterIntent && ReferenceEquals(_waterTrip, trip) && !_restored && NativeExecutionOwned &&
        !_resources.IsIndeterminate && !_mortal.Dead && !_mortal.ShouldDie && !_needs.AnyNeedIsInCriticalState() &&
        !_carrier.IsCarrying && !HasReservations && !_progress.CancellationRequested && _progress.Hours < 2 &&
        _donor is not null && _donor && _donor.Enabled && _worker.Employed && ReferenceEquals(_worker.Workplace, _donor) &&
        _hours.AreWorkingHours && !_refuser.RefusesWork && _citizen.HasAssignedDistrict &&
        _donor.GetComponent<DistrictBuilding>() is { } district && ReferenceEquals(district.District, _citizen.AssignedDistrict) &&
        _returnBehavior.RecoveryReady && ReferenceEquals(_returnBehavior.ReturnDestination, trip.ReturnInventory);

    private bool AtWaterPoint(Vector3 point)
    {
        var actual = Transform.position;
        return _navigation.InStoppingProximity(actual, point) && _field.SafePosition(actual);
    }
    private bool CanCollect(BorrowedWaterTrip trip) => Phase == BorrowedDutyPhase.AwaitingCredit && CurrentWaterActor(trip) &&
        trip.SourceUsable && trip.TargetUsable(_field) && AtWaterPoint(trip.Shore);
    private void RequirePickupOwner(BorrowedWaterTrip trip)
    {
        if (!CanCollect(trip)) throw new InvalidOperationException("Borrowed water pickup lost its current actor, source or physical arrival.");
    }

    // Called only by the settled native credit boundary through the existing coordinator responder list.
    // Entity bucket ticks run while native water work can be parallel; they never read/debit that buffer here.
    void INativeNaturalWaterCollector.TryCollectPendingWater() => TryCollectPendingWater();
    internal void TryCollectPendingWater()
    {
        if (_waterTrip is not { } trip || !CanCollect(trip)) return;
        _equipment.TryFillFromNaturalSource(trip.Source, () => RequirePickupOwner(trip), () =>
        {
            RequirePickupOwner(trip);
            if (!BorrowedDutyBehavior.HasReturnWater(_equipment.Inventory))
                throw new InvalidOperationException("Native pickup did not produce exactly one unreserved Water good.");
            _progress.Loaded();
            _waterRoutePending = true;
            if (!CurrentWaterActor(trip) || Phase != BorrowedDutyPhase.ApproachingFire)
                throw new InvalidOperationException("Borrowed water pickup could not publish its loaded phase.");
        });
    }

    private ExecutorStatus TickWater(float hours)
    {
        if (_resources.IsIndeterminate) { _movement.RejectRoute(); return ExecutorStatus.Running; }
        if (_restored)
        {
            _restored = false;
            // Application intent is deliberately canceled on load. The independently saved return binding owns recovery.
            return FinishWater();
        }
        if (_mortal.Dead || _mortal.ShouldDie) return FinishWater();
        _progress.Advance(hours);
        if (Phase == BorrowedDutyPhase.Returning) return TickWaterReturn(hours);
        if (_waterTrip is not { } trip || !CurrentWaterActor(trip) || !trip.TargetUsable(_field)) return AbortWater();
        if (Phase is BorrowedDutyPhase.FetchingWater or BorrowedDutyPhase.AwaitingCredit)
        {
            if (!trip.SourceUsable) return AbortWater();
            if (Phase == BorrowedDutyPhase.AwaitingCredit) return AtWaterPoint(trip.Shore) ? ExecutorStatus.Running : AbortWater();
        }
        else if (!BorrowedDutyBehavior.HasReturnWater(_equipment.Inventory)) return AbortWater();
        if (Phase == BorrowedDutyPhase.AwaitingApplication) return AtWaterPoint(trip.Approach) ? ExecutorStatus.Running : AbortWater();
        if (_waterRoutePending)
        {
            _waterRoutePending = false;
            if (!Launch(trip.Approach)) return AbortWater();
        }
        if (!_movement.RefreshIfNeeded()) return AbortWater();
        var status = _movement.Tick(hours);
        if (status == ExecutorStatus.Running) return status;
        var expected = Phase == BorrowedDutyPhase.FetchingWater ? trip.Shore : trip.Approach;
        if (status == ExecutorStatus.Failure || !AtWaterPoint(expected)) return AbortWater();
        _movement.Stop();
        if (Phase == BorrowedDutyPhase.FetchingWater) _progress.ArriveSource(true);
        else _progress.ArriveFire(true);
        return ExecutorStatus.Running;
    }

    bool INativeWaterApplicationProducer.TryPrepareApplication(out FireSimChange input, out Action commit) =>
        TryPrepareWaterApplication(out input, out commit);
    internal bool TryPrepareWaterApplication(out FireSimChange input, out Action commit)
    {
        input = default; commit = null!;
        if (_waterTrip is not { } trip || !CanApply(trip)) return false;
        input = new FireSimChange(trip.FireCell, AddWater: 3);
        commit = () =>
        {
            if (!CanApply(trip)) throw new InvalidOperationException("Borrowed water application lost its current ready owner.");
            _equipment.ConsumeBucket();
            if (!CurrentWaterActor(trip) || Phase != BorrowedDutyPhase.AwaitingApplication || !AtWaterPoint(trip.Approach) ||
                _equipment.Inventory.Stock.Sum(good => good.Amount) != 0)
                throw new InvalidOperationException("Borrowed water application changed during native consumption.");
            _progress.Applied();
            _returnBehavior.Returned(); // Empty equipment no longer needs retained-cargo recovery.
            _waterRoutePending = true;
        };
        return true;
    }
    private bool CanApply(BorrowedWaterTrip trip) => Phase == BorrowedDutyPhase.AwaitingApplication && CurrentWaterActor(trip) &&
        trip.TargetUsable(_field) && AtWaterPoint(trip.Approach) && BorrowedDutyBehavior.HasReturnWater(_equipment.Inventory);
    private ExecutorStatus AbortWater()
    {
        // A carried unit belongs to the independent return behavior. Never revive this application after cancellation.
        if (_equipment.Inventory.Stock.Any(good => good.Amount > 0)) return FinishWater();
        _progress.Return(); _waterRoutePending = true;
        return ExecutorStatus.Running;
    }
    private ExecutorStatus TickWaterReturn(float hours)
    {
        if (_progress.Hours >= 2 || !_field.ObservationAvailable) return FinishWater();
        if (_waterRoutePending)
        {
            _waterRoutePending = false;
            _movement.Stop();
            if (!_field.TryRetreat(_navigator.CurrentAccessOrPosition(), new[] { _origin }, out var point) || !Launch(point)) return FinishWater();
        }
        if (!_movement.RefreshIfNeeded()) return FinishWater();
        var status = _movement.Tick(hours);
        return status == ExecutorStatus.Running ? status : FinishWater();
    }
    private ExecutorStatus FinishWater()
    {
        _movement.Stop(); _progress.Finish();
        _waterIntent = false; _waterTrip = null; _waterRoutePending = false;
        _movement.ReleasePause();
        return ExecutorStatus.Success;
    }
}
