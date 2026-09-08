using Wildfire.Timberborn.FireSafety;
using Wildfire.Timberborn.Compatibility;
using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.Carrying;
using Timberborn.CharacterControlSystem;
using Timberborn.Characters;
using Timberborn.EnterableSystem;
using Timberborn.EntitySystem;
using Timberborn.InventorySystem;
using Timberborn.Navigation;
using Timberborn.NeedSystem;
using Timberborn.Persistence;
using Timberborn.WalkingSystem;
using Timberborn.WorldPersistence;
using UnityEngine;
using Wildfire.Timberborn.FireResponse;

namespace Wildfire.Timberborn.Beavers.Emergency;

/// <summary>Development-only ownership of one native loaded carrying walk. Never owns goods or reservations.</summary>
public sealed class WildfireCarryEmergencyExecutor : BaseComponent, IExecutor, IAwakableComponent,
    IInitializableEntity, IPostLoadableEntity, IDeletableEntity
{
    private static readonly ComponentKey Key = new("Wildfire.CarryEmergency");
    private static readonly PropertyKey<int> VersionKey = new("Version");
    private static readonly PropertyKey<int> PhaseKey = new("Phase");
    private static readonly PropertyKey<int> ReasonKey = new("Reason");
    private static readonly PropertyKey<float> HoursKey = new("Hours");
    private readonly CarryEmergencySession _session;
    private readonly FireSafetyField _field;
    private readonly INavigationService _navigation;
    private readonly CarryEmergencyState _state = new();
    private BehaviorManager _manager = null!;
    private CarryRootBehavior _carryBehavior = null!;
    private WalkToAccessibleExecutor _deliveryWalk = null!;
    private WalkToPositionExecutor _escapeWalk = null!;
    private Walker _walker = null!;
    private TimberbornOwnedWalker _movement = null!;
    private GoodCarrier _carrier = null!;
    private GoodReserver _reserver = null!;
    private Character _character = null!;
    private ControllableCharacter _control = null!;
    private Enterer _enterer = null!;
    private NeedManager _needs = null!;
    private Transform _transform = null!;
    private Guid _id;
    private Vector3 _refuge;
    private bool _ready;
    private bool _restore;
    private bool _routeRejected;
    private bool _launchingDelivery;
    private long _routeRevision = -1;
    private string _lastStatus = "";

    public CarryEmergencyPhase Phase => _state.Phase;
    public string Status => _session.Safety.IsPoisoned ? "Carrying emergency stopped; reload last good save" :
        _state.Reason switch
        {
            CarryEmergencyReason.Escaping => "Escaping fire with carried goods",
            CarryEmergencyReason.NoEscape => "Trapped with carried goods; no safe escape",
            CarryEmergencyReason.AwaitingField => "Carrying emergency waiting for fire observations",
            CarryEmergencyReason.AwaitingDelivery => "Stranded while carrying; destination or route unsafe",
            CarryEmergencyReason.CriticalNeeds => "Stranded while carrying; critical needs blocked (development prototype)",
            CarryEmergencyReason.ControlRequested => "Player control overrides carrying emergency",
            _ => "Native carrying behavior"
        };

    public WildfireCarryEmergencyExecutor(CarryEmergencySession session, FireSafetyField field, INavigationService navigation)
    { _session = session; _field = field; _navigation = navigation; }

    public void Awake()
    {
        _manager = GetComponent<BehaviorManager>();
        _carryBehavior = GetComponent<CarryRootBehavior>();
        _deliveryWalk = GetComponent<WalkToAccessibleExecutor>();
        _escapeWalk = GetComponent<WalkToPositionExecutor>();
        _walker = GetComponent<Walker>();
        _movement = new TimberbornOwnedWalker(_walker, GetComponent<WalkerMover>());
        _carrier = GetComponent<GoodCarrier>();
        _reserver = GetComponent<GoodReserver>();
        _character = GetComponent<Character>();
        _control = GetComponent<ControllableCharacter>();
        _enterer = GetComponent<Enterer>();
        _needs = GetComponent<NeedManager>();
        _transform = Transform;
        _id = GetComponent<EntityComponent>().EntityId;
        _walker.StartedNewPath += OnStartedNewPath;
        if (_session.AdmissionsEnabled) VerifyIdentity();
    }
    public void InitializeEntity() => _ready = true;
    public void PostLoadEntity() => _ready = true;
    public void DeleteEntity() => _walker.StartedNewPath -= OnStartedNewPath;
    private void VerifyIdentity() => _session.Access.VerifyExecutorIdentity(GetComponentsAllocating<IExecutor>(), this);

    // Ordinary pre-manager transitions and exact existing-carry path-refresh callbacks may claim ownership.
    // Never replace from IExecutor.Tick then return Success/Failure: native code clears the replacement.
    public void BeforeBehaviorTick()
    {
        if (!_ready || !_state.Active && !_session.AdmissionsEnabled) return;
        if (_session.Safety.IsPoisoned)
        {
            // The guard is shared by every actor. Pause other owned escapes too, without retrying
            // the failed native stop or changing their still-owned path/reservation state.
            if (_state.Active) _movement.RejectRoute();
            return;
        }
        if (_state.Active) _session.Safety.Transition(AdvanceOwnership, FreezeFailedMovement);
        else TryAdmit();
        if (_lastStatus == Status) return;
        _lastStatus = Status;
        Debug.Log($"wildfire_carry_emergency beaver={_id} phase={Phase} status=\"{Status}\"");
    }

    private void AdvanceOwnership()
    {
        if (!_session.Access.Owns(_manager, _carryBehavior, this)) throw new InvalidOperationException("Carrying emergency lost native executor ownership.");
        if (!_character.Alive || _control.UnderControl)
        {
            StopMovement();
            if (_character.Alive) _movement.ReleasePause(); // Explicit player control; dead movers remain disabled.
            _session.Access.Replace(_manager, _carryBehavior, this, null, returnToCarry: false);
            _state.Release();
            return; // Native roots handle death or explicit player control, including native goods semantics.
        }
        if (!_field.ObservationAvailable)
        { StopMovement(); _state.Hold(CarryEmergencyReason.AwaitingField); return; }
        if (_restore)
        {
            StopMovement();
            _restore = false;
            _state.Hold(CarryEmergencyReason.AwaitingDelivery);
        }
        if (!_field.SafePosition(_transform.position))
        {
            if (_state.Phase != CarryEmergencyPhase.Escaping || _routeRejected || _walker.Stopped()) TryEscape();
            else CheckEscapeRoute();
            return;
        }
        if (_state.Phase == CarryEmergencyPhase.Escaping)
        {
            CheckEscapeRoute();
            if (_state.Phase == CarryEmergencyPhase.Escaping && !At(_refuge)) return;
            StopMovement();
            _state.Hold(CarryEmergencyReason.AwaitingDelivery);
        }
        if (!TryResumeDelivery())
            _state.Hold(_needs.AnyNeedIsInCriticalState() ? CarryEmergencyReason.CriticalNeeds : CarryEmergencyReason.AwaitingDelivery);
    }

    private bool CanAdmit() =>
        _session.AdmissionsEnabled && _ready && !_session.Safety.IsPoisoned &&
        _character.Alive && !_control.UnderControl && !_enterer.IsInside && !_walker.Stopped() &&
        !_needs.AnyNeedIsInCriticalState() &&
        _session.Access.IsNativeDelivery(_manager, _carryBehavior, _deliveryWalk) &&
        ReservationMatches() && _field.Ready;

    private void TryAdmit()
    {
        if (!CanAdmit()) return;
        var remainingPath = _session.Access.ReadRemainingPath(_walker.PathFollower);
        if (remainingPath is null) return;
        if (_field.SafeInstalledPath(_transform.position, remainingPath, escaping: false)) return;
        _session.Safety.Transition(() =>
        {
            ClaimNativeDelivery();
            StopMovement();
            TryEscape();
        }, FreezeFailedMovement);
    }
    private void ClaimNativeDelivery()
    {
        // Recheck exact native owner immediately before mutation, including callback-time admission.
        if (!CanAdmit()) throw new InvalidOperationException("Native carrying admission changed before ownership transfer.");
        _state.Begin();
        _session.Access.Replace(_manager, _carryBehavior, _deliveryWalk, this, returnToCarry: false);
    }
    private void TryAdmitRefreshedPath()
    {
        if (!CanAdmit() || _field.SafeInstalledPath(_transform.position, _walker.PathCorners, escaping: false)) return;
        _session.Safety.Transition(() =>
        {
            ClaimNativeDelivery();
            _routeRejected = true;
            _movement.RejectRoute(); // Keep FindPath's path intact; next ordinary tick stops/replans.
        }, FreezeFailedMovement);
    }

    private bool ReservationMatches()
    {
        if (!_carrier.IsCarrying || _carrier.CarriedGood.Type is not (CarriedGoodType.Available or CarriedGoodType.Unavailable) ||
            _reserver.StockReservation.Inventory is not null || !_reserver.HasReservedCapacity) return false;
        var carried = _carrier.CarriedGood.GoodAmount;
        var reserved = _reserver.CapacityReservation.GoodAmount;
        return carried.GoodId == reserved.GoodId && carried.Amount > 0 && carried.Amount == reserved.Amount;
    }
    private Accessible? DeliveryAccess()
    {
        if (!ReservationMatches()) return null;
        var access = _reserver.CapacityReservation.Inventory.GetEnabledComponent<Accessible>();
        return access && access.ValidAccessible ? access : null;
    }
    private bool HasSafeDeliveryCandidate(Accessible access) =>
        access.Accesses.Any(position => _field.SafeRoute(_transform.position, position));

    private void TryEscape()
    {
        StopMovement();
        if (!_field.TryRetreat(_transform.position, Array.Empty<Vector3>(), out _refuge))
        { _state.Hold(CarryEmergencyReason.NoEscape); return; }
        _state.Escape();
        _routeRejected = false;
        var result = _escapeWalk.Launch(_refuge);
        if (_routeRejected || result == ExecutorStatus.Failure || result == ExecutorStatus.Success && !At(_refuge))
        { StopMovement(); _state.Hold(CarryEmergencyReason.NoEscape); }
        else _movement.ReleasePause();
    }
    private void CheckEscapeRoute()
    {
        if (_routeRevision != _field.Revision && !_walker.Stopped()) _walker.RefreshPath();
        if (_routeRejected || _walker.Stopped() && !At(_refuge))
        { StopMovement(); _state.Hold(CarryEmergencyReason.NoEscape); }
    }
    private bool TryResumeDelivery()
    {
        var target = DeliveryAccess();
        if (target is null || !HasSafeDeliveryCandidate(target)) return false;
        _routeRejected = false;
        _launchingDelivery = true;
        ExecutorStatus result;
        try { result = _deliveryWalk.Launch(target); }
        finally { _launchingDelivery = false; }
        var receipt = new CarryDeliveryReceipt(!_routeRejected, result == ExecutorStatus.Running,
            result == ExecutorStatus.Success, target.Accesses.Any(At));
        if (!_state.CanRelease(_field.SafePosition(_transform.position), ReservationMatches(), receipt))
        { StopMovement(); return false; }
        _movement.ReleasePause();
        _session.Access.Replace(_manager, _carryBehavior, this, _deliveryWalk, returnToCarry: true);
        _state.Release();
        return true;
    }
    private void OnStartedNewPath(object sender, StartedNewPathEventArgs args)
    {
        if (!_state.Active)
        {
            // A native NavMeshObserver may run after our ordinary interceptor. Exact current carry
            // ownership admits it here; a new Carry.Decide launch has no running executor and fails admission.
            TryAdmitRefreshedPath();
            return;
        }
        try { ValidateInstalledRoute(); }
        catch (Exception exception)
        {
            // Native navigation refresh can call this outside the pre-manager transition.
            _session.Safety.FailMovement(exception, FreezeFailedMovement);
            throw;
        }
    }
    private void ValidateInstalledRoute()
    {
        if (!_session.Access.Owns(_manager, _carryBehavior, this))
            throw new InvalidOperationException("Another executor replaced an active carrying emergency.");
        _routeRevision = _field.Revision;
        _routeRejected = _restore || !_ready || _session.Safety.IsPoisoned ||
            !_launchingDelivery && _state.Phase != CarryEmergencyPhase.Escaping ||
            !_field.SafeInstalledPath(_transform.position, _walker.PathCorners, escaping: !_launchingDelivery);
        if (_routeRejected) _movement.RejectRoute();
        // FindPath still reads this path and assigns its destination AFTER the callback. Disable the
        // late mover now; clear native movement after FindPath returns. This also intercepts a native
        // NavMeshObserver refresh before its later WalkerMover tick, regardless of ordinary tick order.
    }
    private bool At(Vector3 position) => _navigation.InStoppingProximity(_transform.position, position);
    private void StopMovement() => _movement.Stop();
    private void FreezeFailedMovement()
    {
        _movement.RejectRoute(); // Always stop late motion, even after a partial handoff released local phase.
        // Clearing a foreign executor's destination can make its next Tick mistake cancellation for
        // arrival. Preserve that path while poisoned; only clear a walk the emergency still owns.
        if (_session.Access.Owns(_manager, _carryBehavior, this)) _movement.Stop();
    }

    public ExecutorStatus Tick(float deltaTimeInHours)
    {
        if (_session.Safety.IsPoisoned) return ExecutorStatus.Running;
        _state.Advance(deltaTimeInHours);
        return ExecutorStatus.Running;
    }
    public void Save(IEntitySaver saver)
    {
        _session.Safety.ThrowIfSaveUnsafe();
        if (!_state.Active || !_session.Access.Owns(_manager, _carryBehavior, this))
            throw new InvalidOperationException("Emergency save does not match native carrying ownership.");
        var state = saver.GetComponent(Key);
        state.Set(VersionKey, 1);
        state.Set(PhaseKey, (int)_state.Phase);
        state.Set(ReasonKey, (int)_state.Reason);
        state.Set(HoursKey, _state.Hours);
    }
    public void Load(IEntityLoader loader)
    {
        VerifyIdentity(); // Also version-checks restored ownership when new admissions are disabled.
        var state = loader.GetComponent(Key);
        _state.Restore(state.Get(VersionKey), state.Get(PhaseKey), state.Get(ReasonKey), state.Get(HoursKey));
        _restore = true; // First owned tick stops/replans after native entity loading settles.
        // Native GoodReserver.PostLoadEntity restores or invalidates its own reservations.
        // No movement is allowed until ordinary ticks begin after all entity post-load work.
    }
}
