using Wildfire.Timberborn.FireSafety;
using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.Carrying;
using Timberborn.CharacterNavigation;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.InventorySystem;
using Timberborn.MortalSystem;
using Timberborn.Navigation;
using Timberborn.NeedSystem;
using Timberborn.Persistence;
using Timberborn.WorkSystem;
using Timberborn.WorldPersistence;
using UnityEngine;
using Wildfire.Timberborn.FireResponse;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.FireBell;

/// <summary>Borrowed-duty trips and return-only recovery observe employment without changing it.</summary>
public sealed partial class BorrowedDutyExecutor : BaseComponent, IExecutor, IAwakableComponent, IDeletableEntity
{
    private static readonly ComponentKey Key = new("Wildfire.BorrowedDutyExecutor");
    private static readonly PropertyKey<int> VersionKey = new("Version");
    private static readonly PropertyKey<Inventory> ReturnInventoryKey = new("ReturnInventory");
    private static readonly PropertyKey<bool> WaterIntentKey = new("WaterSortie");
    private static readonly PropertyKey<bool> ReturnOnlyKey = new("ReturnOnly");
    private static readonly PropertyKey<int> PhaseKey = new("Phase");
    private static readonly PropertyKey<float> HoursKey = new("Hours");
    private static readonly PropertyKey<bool> CancelKey = new("Cancel");
    private static readonly PropertyKey<Workplace> DonorKey = new("Donor");
    private static readonly PropertyKey<Vector3> OriginKey = new("Origin");
    private static readonly PropertyKey<Vector3> PointKey = new("Point");
    private static readonly PropertyKey<Vector3> DestinationKey = new("Destination");
    private readonly BorrowedDutyFixture _fixture;
    private readonly FireSafetyField _field;
    private readonly NativeResourceCoordinator _resources;
    private readonly ReferenceSerializer _references;
    private readonly INavigationService _navigation;
    private readonly BorrowedDutyProgress _progress = new();
    private Worker _worker = null!;
    private Citizen _citizen = null!;
    private WorkerWorkingHours _hours = null!;
    private WorkRefuser _refuser = null!;
    private GoodCarrier _carrier = null!;
    private GoodReserver _reserver = null!;
    private WardenEquipment _equipment = null!;
    private Mortal _mortal = null!;
    private NeedManager _needs = null!;
    private BehaviorManager _manager = null!;
    private TimberbornFireWalk _movement = null!;
    private Navigator _navigator = null!;
    private Workplace? _donor;
    private Vector3 _origin, _point, _destination;
    private bool _restored;
    public BorrowedDutyPhase Phase => _progress.Phase;
    public bool CancellationRequested => _progress.CancellationRequested;
    public bool NativeExecutionOwned => _manager is not null && _manager.IsRunningExecutor<BorrowedDutyExecutor>();
    private EntityComponent _entity = null!;
    public Guid EntityId => _entity.EntityId;
    public BorrowedDutyExecutor(BorrowedDutyFixture fixture, FireSafetyField field, NativeResourceCoordinator resources,
        ReferenceSerializer references, INavigationService navigation)
    { _fixture = fixture; _field = field; _resources = resources; _references = references; _navigation = navigation; }
    public void Awake()
    {
        _entity = GetComponent<EntityComponent>();
        _returnBehavior = GetComponent<BorrowedDutyBehavior>();
        _worker = GetComponent<Worker>(); _citizen = GetComponent<Citizen>();
        _hours = GetComponent<WorkerWorkingHours>(); _refuser = GetComponent<WorkRefuser>();
        _carrier = GetComponent<GoodCarrier>(); _reserver = GetComponent<GoodReserver>();
        _equipment = GetComponent<WardenEquipment>(); _mortal = GetComponent<Mortal>();
        _needs = GetComponent<NeedManager>(); _manager = GetComponent<BehaviorManager>();
        _movement = TimberbornFireWalk.Create(this, _field, () => Phase switch
        {
            BorrowedDutyPhase.Outbound or BorrowedDutyPhase.FetchingWater or BorrowedDutyPhase.ApproachingFire => FireWalkMode.Outbound,
            BorrowedDutyPhase.Returning => FireWalkMode.Escape,
            _ => FireWalkMode.Ignore
        });
        _navigator = GetComponent<Navigator>();
        _fixture.Register(this);
        _resources.Register((INativeWaterApplicationProducer)this);
    }
    internal bool TryLaunch(Workplace donor, Vector3 point)
    {
        if (Phase != BorrowedDutyPhase.Idle || !_field.Ready || donor.GetComponent<WardenStation>() is not null) return false;
        var district = donor.GetComponent<DistrictBuilding>();
        var eligibility = new BorrowedDutyEligibility(_worker.Employed && ReferenceEquals(_worker.Workplace, donor),
            district is not null && _citizen.HasAssignedDistrict && ReferenceEquals(_citizen.AssignedDistrict, district.District),
            _hours.AreWorkingHours, _refuser.RefusesWork, _needs.AnyNeedIsInCriticalState(), _mortal.Dead || _mortal.ShouldDie,
            _carrier.IsCarrying, _reserver.CapacityReservation.Inventory is not null || _reserver.StockReservation.Inventory is not null, _equipment.Loaded,
            !string.IsNullOrEmpty(_manager.RunningExecutor.Name), _resources.IsIndeterminate);
        if (!eligibility.CanJoin) return false;
        TimberbornOwnedWalker.Verify();
        _origin = _navigator.CurrentAccessOrPosition();
        if ((point - _origin).sqrMagnitude > 16 * 16 || !_field.SafeRoute(_origin, point)) return false;
        _donor = donor; _point = point; _progress.Begin();
        if (Launch(point)) return true;
        Finish(); return false;
    }
    public void RequestCancel() { if (Phase != BorrowedDutyPhase.Idle) _progress.RequestCancel(); }
    public ExecutorStatus Tick(float hours)
    {
        if (Phase == BorrowedDutyPhase.Idle) return ExecutorStatus.Success;
        if (!_manager.IsRunningExecutor<BorrowedDutyExecutor>()) throw new InvalidOperationException("Borrowed duty does not own native movement.");
        if (_returnOnly) return TickReturn(hours);
        if (_waterIntent) return TickWater(hours);
        if (_mortal.Dead || _mortal.ShouldDie)
        { _movement.Stop(); _progress.Finish(); return ExecutorStatus.Failure; }
        if (_resources.IsIndeterminate) { _movement.RejectRoute(); return ExecutorStatus.Running; }
        bool restoreWalk = _restored;
        _restored = false;
        _progress.Advance(hours);
        if (_progress.Hours >= 2 || !_field.ObservationAvailable) return Finish();
        if (Phase != BorrowedDutyPhase.Returning &&
            (_progress.CancellationRequested || !_field.Ready || _donor is null || !_donor || !_donor.Enabled ||
             !_worker.Employed || !ReferenceEquals(_worker.Workplace, _donor) || !_hours.AreWorkingHours ||
             _refuser.RefusesWork || _needs.AnyNeedIsInCriticalState() || _carrier.IsCarrying ||
             _reserver.StockReservation.Inventory is not null || _reserver.CapacityReservation.Inventory is not null)) return Return();
        if (Phase == BorrowedDutyPhase.AtPoint) return Return();
        if (restoreWalk)
        {
            _movement.Stop();
            if (!Launch(_destination)) return Finish();
        }
        if (!_movement.RefreshIfNeeded()) return Phase == BorrowedDutyPhase.Returning ? Finish() : Return();
        var status = _movement.Tick(hours);
        if (status == ExecutorStatus.Running) return status;
        if (status == ExecutorStatus.Failure || !At(_destination)) return Finish();
        _movement.Stop();
        if (Phase == BorrowedDutyPhase.Returning) return Finish();
        _progress.Arrive(At(_point));
        return ExecutorStatus.Running;
    }
    private ExecutorStatus Return()
    {
        _movement.Stop(); _progress.Return();
        if (!_field.TryRetreat(_navigator.CurrentAccessOrPosition(), new[] { _origin }, out var destination) || !Launch(destination)) return Finish();
        return ExecutorStatus.Running;
    }
    private bool Launch(Vector3 destination)
    {
        if (!_field.SafeRoute(_navigator.CurrentAccessOrPosition(), destination, Phase == BorrowedDutyPhase.Returning)) return false;
        _destination = destination;
        return _movement.Launch(destination);
    }
    private bool At(Vector3 destination) => _navigation.InStoppingProximity(_navigator.CurrentAccessOrPosition(), destination);
    private ExecutorStatus Finish()
    {
        _movement.Stop(); _progress.Finish(); _movement.ReleasePause();
        return ExecutorStatus.Success;
    }
    public void DeleteEntity()
    {
        try { if (_returnOnly) FinishReturn(dying: true); }
        finally { _movement.Dispose(); _fixture.Unregister(this); _resources.Unregister((INativeWaterApplicationProducer)this); }
    }
    public void Save(IEntitySaver saver)
    {
        if (_returnOnly || _waterIntent) _resources.ThrowIfSaveUnsafe();
        var state = saver.GetComponent(Key);
        state.Set(VersionKey, 3);
        state.Set(WaterIntentKey, _waterIntent);
        state.Set(ReturnOnlyKey, _returnOnly);
        if (_returnOnly && _returnInventory is not null && _returnInventory)
            state.Set(ReturnInventoryKey, _returnInventory, _references.Of<Inventory>());
        state.Set(PhaseKey, (int)Phase); state.Set(HoursKey, _progress.Hours); state.Set(CancelKey, _progress.CancellationRequested);
        if (_donor is not null && _donor) state.Set(DonorKey, _donor, _references.Of<Workplace>());
        state.Set(OriginKey, _origin); state.Set(PointKey, _point); state.Set(DestinationKey, _destination);
    }
    public void Load(IEntityLoader loader)
    {
        var state = loader.GetComponent(Key);
        int version = state.Has(VersionKey) ? state.Get(VersionKey) : 1;
        if (state.Has(VersionKey) && version is not (2 or 3))
            throw new InvalidOperationException("Unsupported borrowed executor version.");
        _returnOnly = version >= 2 && state.Get(ReturnOnlyKey);
        _waterIntent = version >= 3 && state.Get(WaterIntentKey);
        _waterTrip = null; _waterRoutePending = false;
        _returnInventory = null;
        if (_returnOnly && state.Has(ReturnInventoryKey))
            state.GetObsoletable(ReturnInventoryKey, _references.Of<Inventory>(), out _returnInventory);
        _progress.Restore(state.Get(PhaseKey), state.Get(HoursKey), state.Get(CancelKey));
        if (_waterIntent && (_returnOnly || Phase is BorrowedDutyPhase.Idle or BorrowedDutyPhase.Outbound or BorrowedDutyPhase.AtPoint) ||
            !_waterIntent && (int)Phase > (int)BorrowedDutyPhase.Returning)
            throw new InvalidOperationException("Borrowed water phases require their own finite intent version.");
        if (_returnOnly && Phase != BorrowedDutyPhase.Returning)
            throw new InvalidOperationException("Borrowed recovery has a non-return phase.");
        if (state.Has(DonorKey)) state.GetObsoletable(DonorKey, _references.Of<Workplace>(), out _donor);
        _origin = state.Get(OriginKey); _point = state.Get(PointKey); _destination = state.Get(DestinationKey);
        _restored = Phase != BorrowedDutyPhase.Idle;
        // No movement or employment changes during Load; the first owned Tick replans.
    }
}
