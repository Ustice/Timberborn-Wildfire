using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.BlockingSystem;
using Timberborn.Carrying;
using Timberborn.CharacterNavigation;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.InventorySystem;
using Timberborn.MortalSystem;
using Timberborn.Navigation;
using Timberborn.NeedSystem;
using Timberborn.Persistence;
using Timberborn.WorldPersistence;
using UnityEngine;
using Wildfire.Timberborn.Compatibility;
using Wildfire.Timberborn.FireSafety;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Fertilizer;

/// <summary>One bounded existing-good return. No employment or cancelled application target is stored.</summary>
public sealed class FertilizerRecoveryExecutor : BaseComponent, IExecutor, IAwakableComponent, IDeletableEntity
{
    private static readonly ComponentKey Key = new("Wildfire.FertilizerRecovery");
    private static readonly PropertyKey<bool> ActiveKey = new("Active");
    private static readonly PropertyKey<float> HoursKey = new("Hours");
    private static readonly PropertyKey<Inventory> DestinationKey = new("Destination");
    private readonly NativeResourceCoordinator _resources;
    private readonly FireSafetyField _field;
    private readonly INavigationService _navigation;
    private readonly ReferenceSerializer _references;
    private FertilizerRecoveryRoot _root = null!;
    private FertilizerSatchel _satchel = null!;
    private BehaviorManager _manager = null!;
    private GoodReserver _reserver = null!;
    private GoodCarrier _carrier = null!;
    private Citizen _citizen = null!;
    private Mortal _mortal = null!;
    private NeedManager _needs = null!;
    private Navigator _navigator = null!;
    private TimberbornFireWalk _walk = null!;
    private Inventory? _destination;
    private Vector3 _point;
    private float _hours;
    private bool _restored;
    public bool Active { get; private set; }

    public FertilizerRecoveryExecutor(NativeResourceCoordinator resources, FireSafetyField field,
        INavigationService navigation, ReferenceSerializer references)
    { _resources = resources; _field = field; _navigation = navigation; _references = references; }
    public void Awake()
    {
        _root = GetComponent<FertilizerRecoveryRoot>(); _satchel = GetComponent<FertilizerSatchel>();
        _manager = GetComponent<BehaviorManager>(); _reserver = GetComponent<GoodReserver>();
        _carrier = GetComponent<GoodCarrier>(); _citizen = GetComponent<Citizen>();
        _mortal = GetComponent<Mortal>(); _needs = GetComponent<NeedManager>(); _navigator = GetComponent<Navigator>();
        _walk = TimberbornFireWalk.Create(this, _field, () => Active ? FireWalkMode.Escape : FireWalkMode.Ignore);
    }
    private bool Dying => _mortal.Dead || _mortal.ShouldDie;
    private bool Eligible => _root.RecoveryReady && !_resources.IsIndeterminate && !Dying &&
        !_needs.AnyNeedIsInCriticalState() && _citizen.HasAssignedDistrict && !_carrier.IsCarrying &&
        FertilizerRecoveryRoot.HasDepositRequest(_satchel.Inventory) && _field.ObservationAvailable;
    internal bool TryLaunch()
    {
        if (Active || !Eligible || _reserver.StockReservation.Inventory is not null ||
            _reserver.CapacityReservation.Inventory is not null || !string.IsNullOrEmpty(_manager.RunningExecutor.Name)) return false;
        TimberbornOwnedWalker.Verify();
        _hours = 0;
        return PlanReturn();
    }
    private bool PlanReturn()
    {
        if (!Eligible || _reserver.StockReservation.Inventory is not null || _reserver.CapacityReservation.Inventory is not null) return false;
        var start = _navigator.CurrentAccessOrPosition();
        var district = _citizen.AssignedDistrict;
        var registry = district.GetComponent<DistrictInventoryRegistry>();
        var closest = district.GetComponent<DistrictInventoryPicker>().ClosestInventoryWithCapacity(start, FertilizerSatchelStock.Unit, out _);
        var available = new List<Inventory>();
        foreach (var inventory in registry.ActiveInventoriesWithCapacity(FertilizerSatchelStock.GoodId)) available.Add(inventory);
        var candidates = available
            .OrderBy(inventory => ReferenceEquals(inventory, closest) ? 0 : 1)
            .ThenBy(inventory => inventory ? Vector3.Distance(start, inventory.Transform.position) : float.PositiveInfinity).ToArray();
        foreach (var inventory in candidates)
        {
            if (!Eligible || !ReferenceEquals(_citizen.AssignedDistrict, district)) return false;
            if (!Usable(inventory, registry) || !inventory.HasUnreservedCapacity(FertilizerSatchelStock.Unit)) continue;
            foreach (var access in Accesses(inventory).ToArray())
            {
                if (!_field.SafeRoute(start, access, escaping: true)) continue;
                _destination = inventory; _point = access;
                _resources.TransferInventory(() =>
                {
                    if (!Eligible || !ReferenceEquals(_citizen.AssignedDistrict, district) ||
                        !Usable(inventory, registry) || _reserver.StockReservation.Inventory is not null ||
                        _reserver.CapacityReservation.Inventory is not null || !inventory.HasUnreservedCapacity(FertilizerSatchelStock.Unit)) return;
                    _reserver.ReserveCapacity(inventory, FertilizerSatchelStock.Unit);
                    if (Eligible && ValidDestination()) Active = true;
                    else ReleaseOwnedReservation();
                });
                if (!Active) { _destination = null; continue; }
                try { if (_walk.Launch(_point)) return true; }
                catch { Finish(); throw; }
                Finish();
                return false; // Yield/back off rather than spin after a native installed-route failure.
            }
        }
        return false;
    }
    private bool Usable(Inventory inventory, DistrictInventoryRegistry registry) => inventory && inventory.Enabled &&
        !ReferenceEquals(inventory, _satchel.Inventory) && registry.ActiveInventoriesWithCapacity(FertilizerSatchelStock.GoodId).Contains(inventory) &&
        inventory.GetComponent<IInventoryValidator>() is { } validator && validator.ValidInventory &&
        inventory.GetComponent<BlockableObject>() is { } blockable && blockable.IsUnblocked;
    private bool ValidDestination()
    {
        if (!_citizen.HasAssignedDistrict || _destination is null || !_destination || !_destination.Enabled ||
            !FertilizerSatchel.ExactCapacityReservation(_destination, _reserver)) return false;
        // A fully reserved one-unit destination may leave the active-capacity set. Membership
        // was checked before reservation; now exact district ownership plus native validity wins.
        var district = _destination.GetComponent<DistrictBuilding>();
        return district is not null && ReferenceEquals(district.District, _citizen.AssignedDistrict) &&
            _destination.GetComponent<IInventoryValidator>() is { } validator && validator.ValidInventory &&
            _destination.GetComponent<BlockableObject>() is { } blockable && blockable.IsUnblocked;
    }
    private static IEnumerable<Vector3> Accesses(Inventory inventory)
    {
        var accesses = new List<Accessible>(); inventory.GetComponents(accesses);
        return accesses.Where(access => access.ValidAccessible).SelectMany(access => access.Accesses);
    }
    public ExecutorStatus Tick(float hours)
    {
        if (!Active) return ExecutorStatus.Success;
        if (!_manager.IsRunningExecutor<FertilizerRecoveryExecutor>())
            throw new InvalidOperationException("Fertilizer recovery does not own native movement.");
        if (Dying) return Finish(dying: true);
        if (_resources.IsIndeterminate) { _walk.RejectRoute(); return ExecutorStatus.Running; }
        if (!Eligible || !ValidDestination()) return Finish();
        if (float.IsNaN(hours) || float.IsInfinity(hours) || hours < 0) throw new ArgumentOutOfRangeException(nameof(hours));
        _hours += hours;
        if (_hours > 1) return Finish();
        if (_restored)
        {
            _restored = false; _walk.Stop();
            _resources.TransferInventory(ReleaseOwnedReservation);
            Active = false;
            return PlanReturn() ? ExecutorStatus.Running : Finish();
        }
        if (!_walk.RefreshIfNeeded()) return Finish();
        var status = _walk.Tick(hours);
        if (status == ExecutorStatus.Running) return status;
        if (status == ExecutorStatus.Failure || !At(_point) || !Accesses(_destination!).Any(At) ||
            !_field.SafePosition(_navigator.CurrentAccessOrPosition())) return Finish();
        _walk.Stop();
        _satchel.TryReturn(_destination!, _reserver, () => { Active = false; _destination = null; });
        return Finish(); // A released reservation with false receipt yields, preserving existing stock.
    }
    private bool At(Vector3 point) => _navigation.InStoppingProximity(_navigator.CurrentAccessOrPosition(), point);
    private void ReleaseOwnedReservation()
    {
        var reservation = _reserver.CapacityReservation;
        if (_destination is not null && ReferenceEquals(reservation.Inventory, _destination) &&
            reservation.GoodAmount.GoodId == FertilizerSatchelStock.GoodId && reservation.GoodAmount.Amount == 1 &&
            reservation.FixedAmount && !reservation.ConsumeGood)
            _reserver.UnreserveCapacity();
    }
    private ExecutorStatus Finish(bool dying = false)
    {
        try
        {
            _walk.Stop();
            if (!_resources.IsIndeterminate) _resources.TransferInventory(() => { ReleaseOwnedReservation(); ClearReturn(); });
            else if (dying) ClearReturn();
        }
        catch when (dying) { _resources.InvalidateAfterLifecycleFailure(); ClearReturn(); }
        if (!dying && !_resources.IsIndeterminate) _walk.ReleasePause();
        return dying ? ExecutorStatus.Failure : ExecutorStatus.Success;
    }
    private void ClearReturn() { Active = false; _destination = null; _root.Defer(); }
    public void DeleteEntity()
    {
        try { if (Active) Finish(dying: true); }
        finally { _walk.Dispose(); }
    }
    public void Save(IEntitySaver saver)
    {
        _resources.ThrowIfSaveUnsafe();
        var state = saver.GetComponent(Key);
        state.Set(ActiveKey, Active); state.Set(HoursKey, _hours);
        if (_destination is not null && _destination) state.Set(DestinationKey, _destination, _references.Of<Inventory>());
    }
    public void Load(IEntityLoader loader)
    {
        var state = loader.GetComponent(Key);
        Active = state.Get(ActiveKey); _hours = state.Get(HoursKey);
        if (float.IsNaN(_hours) || float.IsInfinity(_hours) || _hours < 0) throw new InvalidOperationException("Invalid recovery duration.");
        _destination = null;
        if (state.Has(DestinationKey)) state.GetObsoletable(DestinationKey, _references.Of<Inventory>(), out _destination);
        _restored = Active; // No movement, inventory writes or application intent during Load.
    }
}
