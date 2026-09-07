using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.Carrying;
using Timberborn.CharacterNavigation;
using Timberborn.EntitySystem;
using Timberborn.InventorySystem;
using Timberborn.MortalSystem;
using Timberborn.Navigation;
using Timberborn.NeedSystem;
using Timberborn.Persistence;
using Timberborn.WalkingSystem;
using Timberborn.WorkSystem;
using Timberborn.WorldPersistence;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.FireResponse;

/// <summary>A registered native executor owns one sortie, including its pending application after reload.</summary>
public sealed class WardenExecutor : BaseComponent, IExecutor, IAwakableComponent, IDeletableEntity
{
    private static readonly ComponentKey Key = new("Wildfire.WardenExecutor");
    private static readonly PropertyKey<int> PhaseKey = new("Phase");
    private static readonly PropertyKey<float> HoursKey = new("Hours");
    private static readonly PropertyKey<WardenStation> StationKey = new("Station");
    private static readonly PropertyKey<int> CellKey = new("Cell");
    private static readonly PropertyKey<Vector3> ApproachKey = new("Approach");
    private static readonly PropertyKey<Vector3> DestinationKey = new("Destination");
    private readonly WardenFireField _field;
    private readonly WardenDeliveryService _delivery;
    private readonly ReferenceSerializer _references;
    private readonly INavigationService _navigation;
    private readonly WardenSortie _sortie = new();
    private Walker _walker = null!;
    private WalkToPositionExecutor _walk = null!;
    private Navigator _navigator = null!;
    private WardenEquipment _equipment = null!;
    private GoodReserver _reserver = null!;
    private GoodCarrier _carrier = null!;
    private Worker _worker = null!;
    private NeedManager _needs = null!;
    private Mortal _mortal = null!;
    private WardenStation? _station;
    private WardenTarget _target;
    private Vector3 _destination;
    private bool _restoreWalk;
    private Guid _entityId;
    private string _status = "Ready";
    public string Status
    {
        get => _status;
        private set
        {
            if (_status == value) return;
            _status = value;
            Debug.Log($"wildfire_warden beaver={_entityId} phase={_sortie.Phase} cell={_target.CellIndex} status=\"{value}\"");
        }
    }

    public WardenExecutor(WardenFireField field, WardenDeliveryService delivery,
        ReferenceSerializer references, INavigationService navigation)
    { _field = field; _delivery = delivery; _references = references; _navigation = navigation; }

    public void Awake()
    {
        _entityId = GetComponent<EntityComponent>().EntityId;
        _walker = GetComponent<Walker>();
        _walk = GetComponent<WalkToPositionExecutor>();
        _navigator = GetComponent<Navigator>();
        _equipment = GetComponent<WardenEquipment>();
        _reserver = GetComponent<GoodReserver>();
        _carrier = GetComponent<GoodCarrier>();
        _worker = GetComponent<Worker>();
        _needs = GetComponent<NeedManager>();
        _mortal = GetComponent<Mortal>();
        _delivery.Register(this);
    }

    public void DeleteEntity() { ReleaseReservation(); _delivery.Unregister(this); }

    public bool TryLaunch(WardenStation station)
    {
        if (!_field.Ready || _delivery.IsIndeterminate) return Refuse("Fire simulation unavailable");
        if (_carrier.IsCarrying || _reserver.HasReservedStock || _reserver.HasReservedCapacity)
            return Refuse("Finishing previous work");
        if (station.Access.Accesses.Count == 0) return Refuse("Station has no access");
        var start = _navigator.CurrentAccessOrPosition();
        if (!_field.TryFindTarget(start, station.Access.Accesses[0], out _target)) return Refuse("No safely reachable fire");
        if (!_equipment.Loaded && !station.Inventory.HasUnreservedStock(WardenEquipment.Bucket))
            return Refuse("Waiting for water");
        _station = station;
        _sortie.Begin(_equipment.Loaded);
        if (!_equipment.Loaded)
        {
            _reserver.ReserveExactStockAmount(station.Inventory, WardenEquipment.Bucket);
            if (!TryWalkToStation()) { ReleaseReservation(); _sortie.Finish(); return Refuse("Water access unsafe"); }
        }
        else if (!LaunchWalk(_target.Approach)) { _sortie.Finish(); return Refuse("Fire approach unsafe"); }
        Status = "Responding";
        return true;
    }

    public ExecutorStatus Tick(float deltaTimeInHours)
    {
        if (_delivery.IsIndeterminate) return ExecutorStatus.Running;
        if (!_field.Ready) return ExecutorStatus.Running;
        if (_mortal.Dead || _mortal.ShouldDie)
        { ReleaseReservation(); _walker.StopNextTick(); _sortie.Finish(); return ExecutorStatus.Failure; }
        if (_sortie.Phase == WardenPhase.Idle) return ExecutorStatus.Success;
        if (_sortie.Phase != WardenPhase.Returning &&
            (_station is null || !_station || !_station.Operational || !_station.Enabled ||
             !_worker.Employed || _worker.Workplace != _station.Workplace || _needs.AnyNeedIsInCriticalState()))
            return Retreat("Response interrupted");
        if (_needsReturnRoute) { _needsReturnRoute = false; return Retreat("Returning after application"); }
        if (_restoreWalk)
        {
            if (!_walker.Stopped()) return ExecutorStatus.Running;
            _restoreWalk = false;
            if (_sortie.Phase is WardenPhase.Fetching or WardenPhase.Approaching or WardenPhase.Returning)
                if (!LaunchWalk(_destination)) return Retreat("Saved route no longer safe");
        }
        _sortie.Advance(deltaTimeInHours);
        if (_sortie.HoursInPhase > 2) return Finish("Response timed out; water retained");
        if (_sortie.Phase is WardenPhase.Applying or WardenPhase.AwaitingApplication)
        {
            if (!At(_target.Approach) || !_field.SafePosition(_navigator.CurrentAccessOrPosition()) ||
                !_field.IsBurning(_target.CellIndex) || !_equipment.Loaded) return Retreat("Target no longer eligible");
            Status = _sortie.AwaitingApplication ? "Applying water" : "Preparing spray";
            return ExecutorStatus.Running;
        }
        // Query the remaining route afresh. Walker.PathCorners can include already-traversed corners.
        if (!_field.SafeRoute(_navigator.CurrentAccessOrPosition(), _destination)) return Retreat("Route became unsafe");
        var walkStatus = _walk.Tick(deltaTimeInHours);
        if (walkStatus == ExecutorStatus.Running) return ExecutorStatus.Running;
        if (walkStatus == ExecutorStatus.Failure || !At(_destination)) return Retreat("Route interrupted before arrival");
        if (_sortie.Phase == WardenPhase.Fetching)
        {
            if (_station is null || !_equipment.TryFill(_station.Inventory, _reserver)) return Retreat("Reserved water unavailable");
            _sortie.Filled();
            if (!LaunchWalk(_target.Approach)) return Retreat("Fire approach became unsafe");
            Status = "Carrying water to fire";
            return ExecutorStatus.Running;
        }
        if (_sortie.Phase == WardenPhase.Approaching)
        { _sortie.Arrived(At(_target.Approach)); return ExecutorStatus.Running; }
        if (_station is not null && _station && AtStation()) _equipment.TryReturn(_station.Inventory);
        return Finish(_equipment.Loaded ? "Water retained for next response" : "Response complete");
    }

    internal bool TryPrepareApplication(out FireSimChange input, out Action commit)
    {
        input = default;
        commit = null!;
        if (!_sortie.AwaitingApplication || !_equipment.Loaded || !_field.Ready ||
            _station is null || !_station || !_station.Operational || !_station.Enabled ||
            !_worker.Employed || _worker.Workplace != _station.Workplace ||
            _mortal.Dead || _mortal.ShouldDie || _needs.AnyNeedIsInCriticalState() ||
            !At(_target.Approach) || !_field.SafePosition(_target.Approach) || !_field.IsBurning(_target.CellIndex)) return false;
        input = new FireSimChange(_target.CellIndex, AddWater: 3);
        commit = () =>
        {
            _equipment.ConsumeBucket();
            _sortie.Applied();
            _restoreWalk = true;
            // Return routing happens on the next native character tick, outside the GPU commit callback.
            _destination = _navigator.CurrentAccessOrPosition();
            Status = "Water applied";
            _needsReturnRoute = true;
        };
        return true;
    }

    private bool _needsReturnRoute;
    private ExecutorStatus Retreat(string reason)
    {
        // Set the phase first: a stopped native walk reports Success, not cancellation.
        _sortie.Cancel();
        ReleaseReservation();
        _walker.StopNextTick();
        var accesses = _station is not null && _station ? _station.Access.Accesses : Enumerable.Empty<Vector3>();
        if (!_field.TryRetreat(_navigator.CurrentAccessOrPosition(), accesses, out var safe))
            return Finish(reason + "; no safe route, water retained");
        _destination = safe;
        _restoreWalk = true; // Walker's pending StopNextTick must run before relaunching.
        Status = reason + "; withdrawing";
        return ExecutorStatus.Running;
    }

    private bool TryWalkToStation()
    {
        foreach (var access in _station!.Access.Accesses)
            if (LaunchWalk(access)) return true;
        return false;
    }
    private bool LaunchWalk(Vector3 destination)
    {
        if (!_field.SafeRoute(_navigator.CurrentAccessOrPosition(), destination)) return false;
        _destination = destination;
        return _walk.Launch(destination) != ExecutorStatus.Failure;
    }
    private bool At(Vector3 destination) => _navigation.InStoppingProximity(_navigator.CurrentAccessOrPosition(), destination);
    private bool AtStation() => _station is not null && _station.Access.Accesses.Any(At);
    private bool Refuse(string reason) { Status = reason; return false; }
    private ExecutorStatus Finish(string reason)
    {
        ReleaseReservation();
        _walker.StopNextTick();
        _sortie.Finish();
        Status = reason;
        return ExecutorStatus.Success;
    }
    private void ReleaseReservation()
    {
        if (_station is not null && _reserver is not null && _reserver.HasReservedStock &&
            _reserver.StockReservation.Inventory == _station.Inventory) _reserver.UnreserveStock();
    }

    public void Save(IEntitySaver saver)
    {
        _delivery.ThrowIfSaveUnsafe();
        var state = saver.GetComponent(Key);
        state.Set(PhaseKey, (int)_sortie.Phase);
        state.Set(HoursKey, _sortie.HoursInPhase);
        if (_station is not null && _station) state.Set(StationKey, _station, _references.Of<WardenStation>());
        state.Set(CellKey, _target.CellIndex);
        state.Set(ApproachKey, _target.Approach);
        state.Set(DestinationKey, _destination);
    }
    public void Load(IEntityLoader loader)
    {
        var state = loader.GetComponent(Key);
        _sortie.Restore(state.Get(PhaseKey), state.Get(HoursKey));
        if (state.Has(StationKey)) state.GetObsoletable(StationKey, _references.Of<WardenStation>(), out _station);
        _target = new WardenTarget(state.Get(CellKey), state.Get(ApproachKey));
        _destination = state.Get(DestinationKey);
        _walker.StopNextTick();
        _restoreWalk = true;
        _needsReturnRoute = _sortie.Phase == WardenPhase.Returning;
    }
}
