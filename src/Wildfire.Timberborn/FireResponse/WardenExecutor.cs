using Wildfire.Timberborn.Compatibility;
using Wildfire.Timberborn.Resources;
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
    private readonly NativeResourceCoordinator _delivery;
    private readonly ReferenceSerializer _references;
    private readonly INavigationService _navigation;
    private readonly WardenSortie _sortie = new();
    private Walker _walker = null!;
    private BehaviorManager _behaviorManager = null!;
    private TimberbornOwnedWalker _movement = null!;
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
    private bool _installedRouteUnsafe;
    private long _routeFieldRevision = -1;
    private Transform _transform = null!;
    private Guid _entityId;
    public WardenPhase Phase => _sortie.Phase;
    public WardenResponseReason ResponseReason { get; private set; }
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

    public WardenExecutor(WardenFireField field, NativeResourceCoordinator delivery,
        ReferenceSerializer references, INavigationService navigation)
    { _field = field; _delivery = delivery; _references = references; _navigation = navigation; }

    public void Awake()
    {
        _entityId = GetComponent<EntityComponent>().EntityId;
        _transform = GetComponent<Transform>();
        _walker = GetComponent<Walker>();
        _behaviorManager = GetComponent<BehaviorManager>();
        _movement = new TimberbornOwnedWalker(_walker, GetComponent<WalkerMover>());
        _walker.StartedNewPath += OnStartedNewPath;
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

    public void DeleteEntity()
    {
        ReleaseReservation();
        _walker.StartedNewPath -= OnStartedNewPath;
        _delivery.Unregister(this);
    }

    public bool TryLaunch(WardenStation station)
    {
        TimberbornOwnedWalker.Verify();
        if (!_field.Ready || _delivery.IsIndeterminate) return Refuse(WardenResponseReason.Unavailable, "Fire simulation unavailable");
        if (_carrier.IsCarrying || _reserver.HasReservedStock || _reserver.HasReservedCapacity)
            return Refuse(WardenResponseReason.OtherWork, "Finishing previous work");
        if (station.Access.Accesses.Count == 0) return Refuse(WardenResponseReason.NoAccess, "Station has no access");
        var start = _navigator.CurrentAccessOrPosition();
        if (!_field.TryFindTarget(start, station.Access.Accesses[0], out _target)) return Refuse(WardenResponseReason.NoSafeFire, "No safely reachable fire");
        if (!_equipment.Loaded && !station.Inventory.HasUnreservedStock(WardenEquipment.Bucket))
            return Refuse(WardenResponseReason.NoWater, "Waiting for water");
        _station = station;
        _sortie.Begin(_equipment.Loaded);
        if (!_equipment.Loaded)
        {
            _reserver.ReserveExactStockAmount(station.Inventory, WardenEquipment.Bucket);
            if (!TryWalkToStation()) { ReleaseReservation(); _sortie.Finish(); _movement.ReleasePause(); return Refuse(WardenResponseReason.UnsafeRoute, "Water access unsafe"); }
        }
        else if (!LaunchWalk(_target.Approach)) { _sortie.Finish(); _movement.ReleasePause(); return Refuse(WardenResponseReason.UnsafeRoute, "Fire approach unsafe"); }
        ResponseReason = WardenResponseReason.None;
        Status = "Responding";
        return true;
    }

    public ExecutorStatus Tick(float deltaTimeInHours)
    {
        if (_sortie.Phase == WardenPhase.Idle) return ExecutorStatus.Success;
        if (!_behaviorManager.IsRunningExecutor<WardenExecutor>())
            throw new InvalidOperationException("Active warden does not own BehaviorManager's executor.");
        if (_mortal.Dead || _mortal.ShouldDie)
        { ReleaseReservation(); _movement.Stop(); _sortie.Finish(); return ExecutorStatus.Failure; }
        if (_delivery.IsIndeterminate) return ExecutorStatus.Running;
        if (!_field.ObservationAvailable) return Finish(WardenResponseReason.Unavailable, "Fire observations unavailable; water retained");
        if (!_field.Ready && _sortie.Phase != WardenPhase.Returning) return Retreat(WardenResponseReason.Disabled, "Wildfire disabled");
        if (_sortie.Phase != WardenPhase.Returning &&
            (_station is null || !_station || !_station.Operational || !_station.Enabled ||
             !_worker.Employed || _worker.Workplace != _station.Workplace || _needs.AnyNeedIsInCriticalState()))
            return Retreat(WardenResponseReason.Interrupted, "Response interrupted");
        if (_sortie.Phase is WardenPhase.Fetching or WardenPhase.Approaching &&
            !_field.IsBurning(_target.CellIndex)) return Retreat(WardenResponseReason.TargetGone, "Fire no longer burning");
        if (_needsReturnRoute) { _needsReturnRoute = false; return Retreat(ResponseReason, "Replanning return route"); }
        if (_restoreWalk)
        {
            // Walker.Load or native PostLoad navigation may have restored a destination after Load.
            // Stop coherently now that the world is ready; waiting for a paused mover would never finish.
            _movement.Stop();
            _restoreWalk = false;
            if (_sortie.Phase is WardenPhase.Fetching or WardenPhase.Approaching or WardenPhase.Returning)
                if (!LaunchWalk(_destination)) return Retreat(WardenResponseReason.UnsafeRoute, "Saved route no longer safe");
        }
        _sortie.Advance(deltaTimeInHours);
        if (_sortie.HoursInPhase > 2) return Finish(WardenResponseReason.TimedOut, "Response timed out; water retained");
        if (_sortie.Phase is WardenPhase.Applying or WardenPhase.AwaitingApplication)
        {
            if (!At(_target.Approach) || !_field.SafePosition(_navigator.CurrentAccessOrPosition()) ||
                !_field.IsBurning(_target.CellIndex) || !_equipment.Loaded) return Retreat(WardenResponseReason.TargetUnavailable, "Target no longer eligible");
            Status = _sortie.AwaitingApplication ? "Applying water" : "Preparing spray";
            return ExecutorStatus.Running;
        }
        // Regenerate on field changes, then inspect the path Walker actually installed (including its prefix).
        if (_routeFieldRevision != _field.Revision && !_walker.Stopped()) _walker.RefreshPath();
        if (_installedRouteUnsafe) return Retreat(WardenResponseReason.UnsafeRoute, "Installed route became unsafe");
        var walkStatus = _walk.Tick(deltaTimeInHours);
        if (walkStatus == ExecutorStatus.Running) return ExecutorStatus.Running;
        if (walkStatus == ExecutorStatus.Failure || !At(_destination)) return Retreat(WardenResponseReason.Interrupted, "Route interrupted before arrival");
        if (_sortie.Phase == WardenPhase.Fetching)
        {
            if (_station is null || !_equipment.TryFill(_station.Inventory, _reserver)) return Retreat(WardenResponseReason.NoWater, "Reserved water unavailable");
            _sortie.Filled();
            if (!LaunchWalk(_target.Approach)) return Retreat(WardenResponseReason.UnsafeRoute, "Fire approach became unsafe");
            Status = "Carrying water to fire";
            return ExecutorStatus.Running;
        }
        if (_sortie.Phase == WardenPhase.Approaching)
        { _sortie.Arrived(At(_target.Approach)); return ExecutorStatus.Running; }
        if (_station is not null && _station && AtStation()) _equipment.TryReturn(_station.Inventory);
        return Finish(_equipment.Loaded ? WardenResponseReason.WaterRetained : WardenResponseReason.Complete, _equipment.Loaded ? "Water retained for next response" : "Response complete");
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
            ResponseReason = WardenResponseReason.Applied;
            _restoreWalk = true;
            // Return routing happens on the next native character tick, outside the GPU commit callback.
            _destination = _navigator.CurrentAccessOrPosition();
            Status = "Water applied";
            _needsReturnRoute = true;
        };
        return true;
    }

    private bool _needsReturnRoute;
    private ExecutorStatus Retreat(WardenResponseReason responseReason, string reason)
    {
        // Set the phase first: a stopped native walk reports Success, not cancellation.
        _sortie.Cancel();
        ReleaseReservation();
        _movement.Stop();
        var accesses = _station is not null && _station ? _station.Access.Accesses : Enumerable.Empty<Vector3>();
        if (!_field.TryRetreat(_navigator.CurrentAccessOrPosition(), accesses, out var safe))
            return Finish(WardenResponseReason.NoSafeReturn, reason + "; no safe route, water retained");
        _destination = safe;
        _restoreWalk = true; // Replan on the next executor tick after the synchronous native stop.
        ResponseReason = responseReason;
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
        if (!_field.SafeRoute(_navigator.CurrentAccessOrPosition(), destination, _sortie.Phase == WardenPhase.Returning)) return false;
        _destination = destination;
        _installedRouteUnsafe = false;
        var status = _walk.Launch(destination);
        if (status == ExecutorStatus.Failure || _installedRouteUnsafe)
        { _movement.Stop(); return false; }
        _movement.ReleasePause();
        return true;
    }
    private void OnStartedNewPath(object sender, StartedNewPathEventArgs args)
    {
        if (_sortie.Phase is not (WardenPhase.Fetching or WardenPhase.Approaching or WardenPhase.Returning)) return;
        _routeFieldRevision = _field.Revision;
        _installedRouteUnsafe = !_field.SafeInstalledPath(_transform.position, _walker.PathCorners,
            _sortie.Phase == WardenPhase.Returning);
        if (_installedRouteUnsafe)
        {
            // FindPath still uses this path after the event. Its late WalkerMover is disabled now,
            // then our Tick/Launch caller synchronously stops the walker after FindPath returns.
            _movement.RejectRoute();
        }
    }

    private bool At(Vector3 destination) => _navigation.InStoppingProximity(_navigator.CurrentAccessOrPosition(), destination);
    private bool AtStation() => _station is not null && _station.Access.Accesses.Any(At);
    private bool Refuse(WardenResponseReason responseReason, string reason) { ResponseReason = responseReason; Status = reason; return false; }
    private ExecutorStatus Finish(WardenResponseReason responseReason, string reason)
    {
        ReleaseReservation();
        _movement.Stop();
        _sortie.Finish();
        _movement.ReleasePause();
        ResponseReason = responseReason;
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
        _restoreWalk = false;
        _needsReturnRoute = false;
        if (_sortie.Phase == WardenPhase.Idle) return; // Never stop or pause somebody else's saved walk.
        if (state.Has(StationKey)) state.GetObsoletable(StationKey, _references.Of<WardenStation>(), out _station);
        _target = new WardenTarget(state.Get(CellKey), state.Get(ApproachKey));
        _destination = state.Get(DestinationKey);
        _restoreWalk = true; // Physical stop is deferred until our first owned tick after world loading.
        _needsReturnRoute = _sortie.Phase == WardenPhase.Returning;
    }
}
