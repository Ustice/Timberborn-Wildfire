using Wildfire.Timberborn.FireSafety;
using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.BlockingSystem;
using Timberborn.Carrying;
using Timberborn.CharacterNavigation;
using Timberborn.EntitySystem;
using Timberborn.InventorySystem;
using Timberborn.GameDistricts;
using Timberborn.MortalSystem;
using Timberborn.Navigation;
using Timberborn.NeedSystem;
using Timberborn.Persistence;
using Timberborn.SimpleOutputBuildings;
using Timberborn.WorkSystem;
using Timberborn.WorldPersistence;
using UnityEngine;
using Wildfire.Core;
using Wildfire.Timberborn.FireResponse;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Ash;

/// <summary>One receipted ash unit, from actual arrival through guarded native deposit.</summary>
public sealed class AshHarvestExecutor : BaseComponent, IExecutor, IAwakableComponent, IDeletableEntity
{
    private static readonly ComponentKey Key = new("Wildfire.AshHarvestExecutor");
    private static readonly PropertyKey<int> PhaseKey = new("Phase");
    private static readonly PropertyKey<float> HoursKey = new("Hours");
    private static readonly PropertyKey<Workplace> WorkplaceKey = new("Workplace");
    private static readonly PropertyKey<int> CellKey = new("Cell");
    private static readonly PropertyKey<Vector3> TargetKey = new("Target");
    private readonly TimberbornFireRuntime _runtime;
    private readonly FireSafetyField _field;
    private readonly NativeResourceCoordinator _resources;
    private readonly ReferenceSerializer _references;
    private readonly INavigationService _navigation;
    private readonly AshHarvestCycle _cycle = new();
    private AshHarvestCargo _cargo = null!;
    private TimberbornFireWalk _ownedWalk = null!;
    private Navigator _navigator = null!;
    private Citizen _citizen = null!;
    private BehaviorManager _behavior = null!;
    private Worker _worker = null!;
    private NeedManager _needs = null!;
    private Mortal _mortal = null!;
    private Workplace? _workplace;
    private int _cell;
    private Vector3 _target;
    private Vector3 _destination;
    private bool _replan;
    private bool _restored;
    public AshHarvestPhase Phase => _cycle.Phase;
    public string StatusKey { get; private set; } = "Wildfire.Ash.Ready";

    public AshHarvestExecutor(TimberbornFireRuntime runtime, FireSafetyField field,
        NativeResourceCoordinator resources, ReferenceSerializer references, INavigationService navigation)
    { _runtime = runtime; _field = field; _resources = resources; _references = references; _navigation = navigation; }

    public void Awake()
    {
        _ownedWalk = TimberbornFireWalk.Create(this, _field, () => Phase is AshHarvestPhase.Approaching or AshHarvestPhase.Returning
            ? (_cycle.HasCargo ? FireWalkMode.Escape : FireWalkMode.Outbound)
            : FireWalkMode.Ignore);
        _navigator = GetComponent<Navigator>();
        _citizen = GetComponent<Citizen>();
        _behavior = GetComponent<BehaviorManager>();
        _worker = GetComponent<Worker>();
        _needs = GetComponent<NeedManager>();
        _mortal = GetComponent<Mortal>();
        _cargo = new AshHarvestCargo(GetComponent<GoodCarrier>(), GetComponent<GoodReserver>(), _resources);
        _resources.Register(this);
    }

    public bool TryLaunch(Workplace workplace, Inventory inventory, TimberbornFertileAshFieldHarvestTarget target)
    {
        if (Phase != AshHarvestPhase.Idle || !_field.Ready || _resources.IsIndeterminate || !_cargo.Empty || _cargo.HasAnyReservation)
            return false;
        TimberbornOwnedWalker.Verify();
        if (!_field.SafeRoute(_navigator.CurrentAccessOrPosition(), target.WalkPosition)) return false;
        _workplace = workplace;
        _cell = target.CellIndex;
        _target = target.WalkPosition;
        if (!_cargo.TryReserve(inventory, _cycle.Begin)) return false;
        if (!LaunchWalk(_target)) { CancelBeforeReceipt(); return false; }
        StatusKey = "Wildfire.Ash.Approaching";
        return true;
    }

    public ExecutorStatus Tick(float hours)
    {
        if (Phase == AshHarvestPhase.Idle) return ExecutorStatus.Success;
        if (!_behavior.IsRunningExecutor<AshHarvestExecutor>())
            throw new InvalidOperationException("Ash movement requires native executor ownership.");
        if (_mortal.Dead || _mortal.ShouldDie)
        {
            _ownedWalk.Stop();
            if (!_resources.IsIndeterminate)
                _resources.TransferInventory(() => { _cargo.ReleaseReservation(); _cycle.Finish(); });
            else _cycle.Finish(); // The existing unsafe-save guard remains; mortality must still win.
            return ExecutorStatus.Failure; // Native mortality owns its ordinary carried-good loss; no refund or mint.
        }
        if (_resources.IsIndeterminate)
        {
            // Keep the installed path intact, but do not let an unmonitored owned mover keep walking.
            _ownedWalk.RejectRoute();
            return ExecutorStatus.Running;
        }
        if (_restored)
        {
            _restored = false;
            _cargo.Validate(_cycle);
            _ownedWalk.Stop();
            _replan = true;
        }
        if (!_cycle.HasCargo && (!JobActive || !_field.Ready || !LiveDestination || !_runtime.IsCleanAshAvailable(_cell)))
            return CancelBeforeReceipt();
        if (!_field.ObservationAvailable)
            return _cycle.HasCargo ? Hold("Wildfire.Ash.NoObservation") : CancelBeforeReceipt();
        _cycle.Advance(hours);
        if (_cycle.HasCargo)
        {
            if (Phase == AshHarvestPhase.HoldingForDeposit || _replan || !LiveDestination)
            {
                _replan = false;
                if (!PlanReturn()) return Hold("Wildfire.Ash.Holding");
            }
            return WalkAndDeposit(hours);
        }
        if (_replan)
        {
            _replan = false;
            if (Phase == AshHarvestPhase.Approaching && !LaunchWalk(_target)) return CancelBeforeReceipt();
        }
        if (Phase is AshHarvestPhase.Harvesting or AshHarvestPhase.ReadyToCollect)
        {
            if (!At(_target) || !_field.SafePosition(_navigator.CurrentAccessOrPosition()) || !_cargo.HasExactReservation)
                return CancelBeforeReceipt();
            StatusKey = Phase == AshHarvestPhase.ReadyToCollect ? "Wildfire.Ash.WaitingReceipt" : "Wildfire.Ash.Harvesting";
            return ExecutorStatus.Running;
        }
        if (_cycle.Hours > 4 || !AdvanceWalk(hours, out var status))
        { _runtime.RecordFertileAshFieldHarvestWalkFailure(_cell); return CancelBeforeReceipt(); }
        if (status == ExecutorStatus.Running) return status;
        _ownedWalk.Stop();
        _cycle.Arrived(At(_target));
        return ExecutorStatus.Running;
    }

    internal bool TryPrepareCollection(out FireSimAshCollectionInput input, out Action<FireSimAshCollectionReceipt> commit)
    {
        input = default; commit = null!;
        if (Phase != AshHarvestPhase.ReadyToCollect || !_behavior.IsRunningExecutor<AshHarvestExecutor>() ||
            !JobActive || !_field.Ready || !_cargo.Empty || !_cargo.HasExactReservation || !LiveDestination ||
            !At(_target) || !_field.SafePosition(_target) || !_runtime.IsCleanAshAvailable(_cell)) return false;
        input = new FireSimAshCollectionInput(_cell, 1);
        commit = receipt =>
        {
            if (receipt.CellIndex != _cell || receipt.Requested != 1 || receipt.Collected > 1)
                throw new InvalidOperationException("Unexpected ash receipt identity.");
            _cargo.Receive(receipt.Collected, _cycle);
            _replan = receipt.Collected == 1;
            if (receipt.Collected == 0) _ownedWalk.ReleasePause();
        };
        return true;
    }

    private bool JobActive => _workplace is not null && _workplace && _workplace.Enabled &&
        _worker.Employed && _worker.Workplace == _workplace && !_needs.AnyNeedIsInCriticalState() && !_mortal.Dead && !_mortal.ShouldDie;
    private bool LiveDestination => _cargo.Destination is { } inventory && inventory && inventory.Enabled && _cargo.HasExactReservation;

    private bool PlanReturn()
    {
        _ownedWalk.Stop();
        _cycle.Return();
        StatusKey = "Wildfire.Ash.Returning";
        if (LiveDestination && TryWalkToInventory(_cargo.Destination!)) return true;
        _resources.TransferInventory(_cargo.ReleaseReservation);
        Inventory? candidate = _workplace is not null && _workplace ? _workplace.GetComponent<SimpleOutputInventory>().Inventory : null;
        if (TryReserveAndWalk(candidate)) return true;
        if (!_citizen.HasAssignedDistrict) return false;
        var picker = _citizen.AssignedDistrict.GetComponent<DistrictInventoryPicker>();
        candidate = picker.ClosestInventoryWithCapacity(_navigator.CurrentAccessOrPosition(), AshHarvestCargo.Unit, out _);
        if (TryReserveAndWalk(candidate)) return true;
        return TryOtherDistrictInventories(candidate);
    }
    private bool TryOtherDistrictInventories(Inventory? rejected)
    {
        if (!_citizen.HasAssignedDistrict) return false;
        var start = _navigator.CurrentAccessOrPosition();
        var district = _citizen.AssignedDistrict;
        var registry = district.GetComponent<DistrictInventoryRegistry>();
        // Snapshot membership before reservation callbacks. Native capacity, validity and blocking
        // are rechecked for each attempt; an unsafe nearest inventory must not hide another route.
        var candidates = new List<Inventory>();
        foreach (var inventory in registry.ActiveInventoriesWithCapacity(AshHarvestCargo.Unit.GoodId))
            if (inventory && inventory != rejected) candidates.Add(inventory);
        var alternatives = candidates.OrderBy(inventory => Vector3.Distance(start, inventory.Transform.position)).ToArray();
        foreach (var inventory in alternatives)
        {
            if (!_citizen.HasAssignedDistrict || !ReferenceEquals(_citizen.AssignedDistrict, district)) return false;
            if (!inventory || !inventory.Enabled || !inventory.HasUnreservedCapacity(AshHarvestCargo.Unit) ||
                !registry.ActiveInventoriesWithCapacity(AshHarvestCargo.Unit.GoodId).Contains(inventory) ||
                !inventory.GetComponent<IInventoryValidator>().ValidInventory ||
                !inventory.GetComponent<BlockableObject>().IsUnblocked) continue;
            var access = inventory.GetEnabledComponent<Accessible>();
            if (access is null || !access.IsReachableUnlimitedRange(start)) continue;
            if (TryReserveAndWalk(inventory)) return true;
        }
        return false;
    }
    private bool TryReserveAndWalk(Inventory? inventory)
    {
        if (inventory is null || !inventory || !inventory.Enabled || !inventory.HasUnreservedCapacity(AshHarvestCargo.Unit)) return false;
        if (!TrySafeAccess(inventory, out var position) || !_cargo.TryReserve(inventory)) return false;
        if (LaunchWalk(position)) return true;
        _resources.TransferInventory(_cargo.ReleaseReservation);
        return false;
    }
    private bool TryWalkToInventory(Inventory inventory) => TrySafeAccess(inventory, out var position) && LaunchWalk(position);
    private bool TrySafeAccess(Inventory inventory, out Vector3 position)
    {
        foreach (var access in Accesses(inventory))
            if (_field.SafeRoute(_navigator.CurrentAccessOrPosition(), access, escaping: true)) { position = access; return true; }
        position = default; return false;
    }
    private static IEnumerable<Vector3> Accesses(Inventory inventory)
    {
        var accessibles = new List<Accessible>();
        inventory.GetComponents(accessibles);
        return accessibles.Where(access => access.ValidAccessible).SelectMany(access => access.Accesses);
    }
    private ExecutorStatus WalkAndDeposit(float hours)
    {
        if (_cycle.Hours > 4 || !AdvanceWalk(hours, out var status)) return Hold("Wildfire.Ash.Holding");
        if (status == ExecutorStatus.Running) return status;
        if (!LiveDestination || !Accesses(_cargo.Destination!).Any(At) || !_field.SafePosition(_navigator.CurrentAccessOrPosition()))
            return Hold("Wildfire.Ash.Holding");
        _ownedWalk.Stop();
        _cargo.Deposit(_cycle);
        _ownedWalk.ReleasePause();
        StatusKey = "Wildfire.Ash.Ready";
        return ExecutorStatus.Success;
    }
    private bool AdvanceWalk(float hours, out ExecutorStatus status)
    {
        if (!_ownedWalk.RefreshIfNeeded()) { _ownedWalk.Stop(); status = ExecutorStatus.Failure; return false; }
        status = _ownedWalk.Tick(hours);
        return status != ExecutorStatus.Failure && (status == ExecutorStatus.Running || At(_destination));
    }
    private bool LaunchWalk(Vector3 destination)
    {
        if (!_field.SafeRoute(_navigator.CurrentAccessOrPosition(), destination, _cycle.HasCargo)) return false;
        _destination = destination;
        return _ownedWalk.Launch(destination);
    }
    private bool At(Vector3 position) => _navigation.InStoppingProximity(_navigator.CurrentAccessOrPosition(), position);
    private ExecutorStatus CancelBeforeReceipt()
    {
        if (_cycle.HasCargo) throw new InvalidOperationException("Receipted ash must be deposited before release.");
        _ownedWalk.Stop();
        _resources.TransferInventory(() => { _cargo.ReleaseReservation(); _cycle.Finish(); });
        _ownedWalk.ReleasePause();
        StatusKey = "Wildfire.Ash.Ready";
        return ExecutorStatus.Failure;
    }
    private ExecutorStatus Hold(string reason)
    {
        _ownedWalk.Stop(); _cycle.Hold(); StatusKey = reason;
        return ExecutorStatus.Running;
    }
    public void DeleteEntity()
    {
        if (Phase != AshHarvestPhase.Idle && !_resources.IsIndeterminate)
        {
            _resources.TransferInventory(_cargo.ReleaseReservation);
        }
        _ownedWalk.Dispose();
        _resources.Unregister(this);
    }
    public void Save(IEntitySaver saver)
    {
        _resources.ThrowIfSaveUnsafe();
        _cargo.Validate(_cycle);
        var state = saver.GetComponent(Key);
        state.Set(PhaseKey, (int)Phase); state.Set(HoursKey, _cycle.Hours);
        if (_workplace is not null && _workplace) state.Set(WorkplaceKey, _workplace, _references.Of<Workplace>());
        state.Set(CellKey, _cell); state.Set(TargetKey, _target);
    }
    public void Load(IEntityLoader loader)
    {
        var state = loader.GetComponent(Key);
        _cycle.Restore(state.Get(PhaseKey), state.Get(HoursKey));
        if (state.Has(WorkplaceKey)) state.GetObsoletable(WorkplaceKey, _references.Of<Workplace>(), out _workplace);
        _cell = state.Get(CellKey); _target = state.Get(TargetKey);
        _restored = Phase != AshHarvestPhase.Idle;
        // Carrier/reservation entities may load later. Validate and touch movement only on the first owned Tick.
    }
}
