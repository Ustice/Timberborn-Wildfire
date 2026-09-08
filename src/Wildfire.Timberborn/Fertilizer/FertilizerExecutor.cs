using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.BlockingSystem;
using Timberborn.BlockSystem;
using Timberborn.Carrying;
using Timberborn.CharacterNavigation;
using Timberborn.Characters;
using Timberborn.Demolishing;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.InventorySystem;
using Timberborn.MapIndexSystem;
using Timberborn.MortalSystem;
using Timberborn.Navigation;
using Timberborn.NeedSystem;
using Timberborn.Persistence;
using Timberborn.Planting;
using Timberborn.TerrainSystem;
using Timberborn.WorkSystem;
using Timberborn.WorldPersistence;
using UnityEngine;
using Wildfire.Core;
using Wildfire.Timberborn.FireSafety;
using Wildfire.Timberborn.Resources;
using Wildfire.Timberborn.Runtime;

namespace Wildfire.Timberborn.Fertilizer;

internal enum FertilizerJobPhase { Idle, Fetching, Approaching, Ready, Ending }

/// <summary>Inactive finite native job. Reload cancels intent; native inventory independently requests recovery.</summary>
public sealed class FertilizerExecutor : BaseComponent, IExecutor, IAwakableComponent, IDeletableEntity
{
    private static readonly ComponentKey Key = new("Wildfire.FertilizerJob");
    private static readonly PropertyKey<int> PhaseKey = new("Phase");
    private static readonly PropertyKey<Inventory> SourceKey = new("Source");
    private readonly NativeResourceCoordinator _resources;
    private readonly FireSafetyField _field;
    private readonly INavigationService _navigation;
    private readonly ReferenceSerializer _references;
    private readonly MapIndexService _indices;
    private readonly IThreadSafeColumnTerrainMap _columns;
    private readonly Action<string> _warn = new UnityTimberbornFireLogSink().Warning;
    private FertilizerSatchel _satchel = null!;
    private FertilizerRecoveryRoot _recovery = null!;
    private BehaviorManager _manager = null!;
    private Worker _worker = null!;
    private WorkerWorkingHours _hours = null!;
    private WorkRefuser _refuser = null!;
    private Mortal _mortal = null!;
    private NeedManager _needs = null!;
    private Citizen _citizen = null!;
    private GoodCarrier _carrier = null!;
    private GoodReserver _reserver = null!;
    private Navigator _navigator = null!;
    private Character _character = null!;
    private Planter _planter = null!;
    private Demolisher _demolisher = null!;
    private TimberbornFireWalk _walk = null!;
    private FertilizerJobTarget? _target;
    private Inventory? _source;
    private Vector3 _destination;
    private bool _launchApproach, _restored, _exited, _disposed;
    private float _elapsedHours;
    internal FertilizerJobPhase Phase { get; private set; }
    public FireSimAshApplicationOutcome? LastOutcome { get; private set; }

    public FertilizerExecutor(NativeResourceCoordinator resources, FireSafetyField field, INavigationService navigation,
        ReferenceSerializer references, MapIndexService indices, IThreadSafeColumnTerrainMap columns)
    {
        _resources = resources;
        _field = field;
        _navigation = navigation;
        _references = references;
        _indices = indices;
        _columns = columns;
    }
    public void Awake()
    {
        _satchel = GetComponent<FertilizerSatchel>();
        _recovery = GetComponent<FertilizerRecoveryRoot>();
        _manager = GetComponent<BehaviorManager>();
        _worker = GetComponent<Worker>();
        _hours = GetComponent<WorkerWorkingHours>();
        _refuser = GetComponent<WorkRefuser>();
        _mortal = GetComponent<Mortal>();
        _needs = GetComponent<NeedManager>();
        _citizen = GetComponent<Citizen>();
        _carrier = GetComponent<GoodCarrier>();
        _reserver = GetComponent<GoodReserver>();
        _navigator = GetComponent<Navigator>();
        _character = GetComponent<Character>();
        _planter = GetComponent<Planter>();
        _demolisher = GetComponent<Demolisher>();
        _walk = TimberbornFireWalk.Create(this, _field,
            () => !_exited && Phase is (FertilizerJobPhase.Fetching or FertilizerJobPhase.Approaching) ? FireWalkMode.Outbound : FireWalkMode.Ignore);
        _character.Died += OnDied;
        _resources.Register(this);
    }
    private bool Eligible => !_exited && !_mortal.Dead && !_mortal.ShouldDie && _recovery.RecoveryReady &&
        _worker.Employed && _hours.AreWorkingHours && !_refuser.RefusesWork && !_needs.AnyNeedIsInCriticalState() &&
        _citizen.HasAssignedDistrict && !_carrier.IsCarrying && !_planter.PlantingCoordinates.HasValue &&
        !_demolisher.HasReservedDemolishable;
    private bool CurrentJob => Eligible && _target is not null &&
        ReferenceEquals(_worker.Workplace, _target.Employer) &&
        ReferenceEquals(_target.Employer.GetComponent<DistrictBuilding>()?.District, _citizen.AssignedDistrict) &&
        _target.IsCurrent(_indices, _columns);
    private bool NoReservations => _reserver.StockReservation.Inventory is null && _reserver.CapacityReservation.Inventory is null;

    internal bool TryLaunch(Workplace employer, BlockObject plant, Inventory source, byte limit)
    {
        if (Phase != FertilizerJobPhase.Idle || !Eligible || !_field.Ready || _resources.IsIndeterminate ||
            !_satchel.Inventory.IsEmpty || !NoReservations || !string.IsNullOrEmpty(_manager.RunningExecutor.Name) ||
            !ReferenceEquals(_worker.Workplace, employer) || !_field.TryObserve(out var observation) ||
            !FertilizerJobTarget.TryCreate(employer, plant, source, limit, observation, _indices, _columns, out var target)) return false;
        _target = target;
        _source = source;
        if (!CurrentJob || !SourceValid() || !source.HasUnreservedStock(FertilizerSatchelStock.Unit)) return AbandonOffer();
        foreach (var point in SourceAccesses().ToArray())
        {
            if (!_field.SafeRoute(_navigator.CurrentAccessOrPosition(), point)) continue;
            _destination = point;
            _resources.TransferInventory(() =>
            {
                if (!CurrentJob || !SourceValid() || !NoReservations || !source.HasUnreservedStock(FertilizerSatchelStock.Unit)) return;
                _reserver.ReserveExactStockAmount(source, FertilizerSatchelStock.Unit);
                if (!CurrentJob || !SourceValid() || !FertilizerSatchel.ExactStockReservation(source, _reserver))
                    throw new InvalidOperationException("Fertilizer source reservation changed during admission.");
                Phase = FertilizerJobPhase.Fetching;
                _elapsedHours = 0;
                LastOutcome = null;
            });
            if (Phase != FertilizerJobPhase.Fetching) return AbandonOffer();
            try { if (_walk.Launch(point)) return true; }
            catch { Finish(); throw; }
            Finish();
            return false;
        }
        return AbandonOffer();
    }
    private bool AbandonOffer() { _target = null; _source = null; return false; }
    private bool SourceValid()
    {
        if (_source is null || !FertilizerJobTarget.Live(_source) || !_source.Enabled || !_source.PublicOutput ||
            ReferenceEquals(_source, _satchel.Inventory) || !_source.Gives(FertilizerSatchelStock.GoodId) || !_citizen.HasAssignedDistrict)
            return false;
        var district = _citizen.AssignedDistrict;
        return ReferenceEquals(_source.GetComponent<DistrictBuilding>()?.District, district) &&
            district.GetComponent<DistrictInventoryRegistry>().Inventories.Contains(_source) &&
            _source.GetComponent<IInventoryValidator>() is { ValidInventory: true } &&
            _source.GetComponent<BlockableObject>() is { IsUnblocked: true };
    }
    private IEnumerable<Vector3> SourceAccesses()
    {
        if (_source is null) yield break;
        var accesses = new List<Accessible>();
        _source.GetComponents(accesses);
        foreach (var access in accesses)
            if (access.ValidAccessible && access.UnblockedSingleAccess is { } point) yield return point;
    }
    private bool At(Vector3 point) => _navigation.InStoppingProximity(_navigator.CurrentAccessOrPosition(), point);
    private bool AtTarget(FertilizerJobTarget target) => target.TreeApproach is { } native
        ? _walk.HasArrivedAt(native) : At(target.Standing);
    public ExecutorStatus Tick(float hours)
    {
        if (_exited || _mortal.Dead || _mortal.ShouldDie) { Exit(); return ExecutorStatus.Failure; }
        if (Phase == FertilizerJobPhase.Idle) return ExecutorStatus.Success;
        if (!_manager.IsRunningExecutor<FertilizerExecutor>()) throw new InvalidOperationException("Fertilizer does not own native movement.");
        if (_resources.IsIndeterminate) { _walk.RejectRoute(); return ExecutorStatus.Running; }
        if (_restored || Phase == FertilizerJobPhase.Ending) return Finish();
        if (!float.IsFinite(hours) || hours < 0) throw new ArgumentOutOfRangeException(nameof(hours));
        _elapsedHours += hours;
        if (_elapsedHours > 4 || !CurrentJob || !_field.Ready) return Finish();
        if (Phase == FertilizerJobPhase.Ready) return AtTarget(_target!) ? ExecutorStatus.Running : Finish();
        if (_launchApproach)
        {
            _launchApproach = false;
            _destination = _target!.Standing;
            if (_target.TreeApproach is { } native)
            {
                if (!_walk.Launch(native)) return Finish();
            }
            else if (!_field.SafeRoute(_navigator.CurrentAccessOrPosition(), _destination) || !_walk.Launch(_destination)) return Finish();
        }
        if (!_walk.RefreshIfNeeded()) return Finish();
        var status = _walk.Tick(hours);
        if (status == ExecutorStatus.Running) return status;
        if (status == ExecutorStatus.Failure ||
            !(Phase == FertilizerJobPhase.Approaching ? AtTarget(_target!) : At(_destination)) || !_field.SafePosition(_navigator.CurrentAccessOrPosition())) return Finish();
        _walk.Stop();
        if (Phase == FertilizerJobPhase.Fetching)
        {
            if (!SourceValid() || !SourceAccesses().Any(At)) return Finish();
            if (!_satchel.TryPickup(_source!, _reserver, () =>
            {
                if (!CurrentJob) throw new InvalidOperationException("Fertilizer employment or target changed during pickup.");
                Phase = FertilizerJobPhase.Approaching;
                _launchApproach = true;
            })) return Finish();
        }
        else _resources.TransferInventory(() => Phase = FertilizerJobPhase.Ready);
        return ExecutorStatus.Running;
    }
    internal bool TryPrepareApplication(out FireSimAshApplicationInput input,
        out Action<FireSimAshApplicationReceipt> accepted, out Action<FireSimAshApplicationReceipt> rejected)
    {
        input = default; accepted = null!; rejected = null!;
        if (Phase != FertilizerJobPhase.Ready || !_manager.IsRunningExecutor<FertilizerExecutor>() ||
            !CurrentJob || !NoReservations || !_satchel.Loaded || !_field.TryObserve(out var observation) ||
            !_target!.Matches(observation) || !AtTarget(_target) || !_field.SafePosition(_target.Standing)) return false;
        var target = _target;
        input = new(target.CellIndex, target.Limit);
        accepted = receipt =>
        {
            RequirePreparedActor(target);
            if (!CurrentJob || !ReferenceEquals(_target, target) || !AtTarget(target) || !NoReservations)
                throw new InvalidOperationException("Fertilizer job changed before consumption.");
            _satchel.ConsumeCommittedUnit(() =>
            {
                RequirePreparedActor(target);
                if (!CurrentJob || !ReferenceEquals(_target, target) || !AtTarget(target) || !NoReservations)
                    throw new InvalidOperationException("Fertilizer job changed during consumption.");
                Complete(receipt.Outcome);
            });
        };
        rejected = receipt =>
        {
            RequirePreparedActor(target);
            Complete(receipt.Outcome);
        };
        return true;
    }
    private void RequirePreparedActor(FertilizerJobTarget target)
    {
        if (_exited || Phase != FertilizerJobPhase.Ready || !_manager.IsRunningExecutor<FertilizerExecutor>() ||
            !ReferenceEquals(_target, target))
            throw new InvalidOperationException("Fertilizer disposition lost its prepared actor.");
    }
    private void Complete(FireSimAshApplicationOutcome outcome) { LastOutcome = outcome; Phase = FertilizerJobPhase.Ending; }
    private void ReleaseSource()
    {
        var r = _reserver.StockReservation;
        if (_source is not null && ReferenceEquals(r.Inventory, _source) && r.FixedAmount && !r.ConsumeGood &&
            r.GoodAmount.GoodId == FertilizerSatchelStock.GoodId && r.GoodAmount.Amount == 1) _reserver.UnreserveStock();
    }
    private ExecutorStatus Finish()
    {
        _resources.TransferInventory(() =>
        {
            Phase = FertilizerJobPhase.Ending;
            _walk.Stop();
            ReleaseSource();
            Phase = FertilizerJobPhase.Idle;
            _target = null;
            _source = null;
            _restored = false;
            if (!_exited) _walk.ReleasePause();
        });
        return ExecutorStatus.Success;
    }
    private void OnDied(object sender, EventArgs args) => Exit();
    private void Exit()
    {
        if (_exited) return;
        _exited = true;
        try { Finish(); }
        catch (Exception error)
        {
            _resources.InvalidateAfterLifecycleFailure();
            try { _warn($"wildfire_fertilizer_job_lifecycle operation=release_source error={error}"); } catch { }
        }
    }
    public void DeleteEntity()
    {
        if (_disposed) return;
        _disposed = true;
        try { Exit(); }
        finally { _character.Died -= OnDied; _walk.Dispose(); _resources.Unregister(this); }
    }
    public void Save(IEntitySaver saver)
    {
        _resources.ThrowIfSaveUnsafe();
        var state = saver.GetComponent(Key);
        state.Set(PhaseKey, (int)Phase);
        if (_source is not null && _source) state.Set(SourceKey, _source, _references.Of<Inventory>());
    }
    public void Load(IEntityLoader loader)
    {
        var state = loader.GetComponent(Key);
        int phase = state.Get(PhaseKey);
        if (phase is < 0 or > (int)FertilizerJobPhase.Ending) throw new InvalidOperationException("Invalid fertilizer job phase.");
        Phase = (FertilizerJobPhase)phase;
        _source = null;
        if (state.Has(SourceKey)) state.GetObsoletable(SourceKey, _references.Of<Inventory>(), out _source);
        _target = null;
        _restored = Phase != FertilizerJobPhase.Idle;
    }
}
