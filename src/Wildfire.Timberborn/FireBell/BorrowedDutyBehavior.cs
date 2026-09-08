using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.EntitySystem;
using Timberborn.InventorySystem;
using Timberborn.Persistence;
using Timberborn.TimeSystem;
using Timberborn.WorldPersistence;
using Wildfire.Timberborn.FireResponse;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.FireBell;

/// <summary>Borrowed labor and return-only recovery never count as the donor's productive job.</summary>
public sealed class BorrowedDutyBehavior : RootBehavior, IPersistentEntity, IAwakableComponent, IPostInitializableEntity
{
    private static readonly ComponentKey Key = new("Wildfire.BorrowedReturn");
    private static readonly PropertyKey<int> Version = new("Version");
    private static readonly PropertyKey<bool> Assigned = new("Assigned");
    private static readonly PropertyKey<Inventory> Destination = new("Destination");
    private readonly NativeResourceCoordinator _resources;
    private readonly ReferenceSerializer _references;
    private readonly IDayNightCycle _clock;
    private BehaviorManager _manager = null!;
    private BorrowedDutyExecutor _executor = null!;
    private WardenEquipment _equipment = null!;
    private bool _initialized;
    private float _retryAfterDay;
    internal bool ReturnAssigned { get; private set; }
    internal Inventory? ReturnDestination { get; private set; }
    public string? UnsupportedReason { get; private set; }
    internal bool RecoveryReady => ReturnAssigned && RecoveryPrepared;
    internal bool RecoveryPrepared
    {
        get
        {
            if (!_initialized) return false;
            try { return BorrowedDutyRecoveryOrder.IsInstalled(_manager, this); }
            catch (Exception exception) { UnsupportedReason = exception.Message; return false; }
        }
    }

    public BorrowedDutyBehavior(NativeResourceCoordinator resources, ReferenceSerializer references, IDayNightCycle clock)
    { _resources = resources; _references = references; _clock = clock; }
    public void Awake()
    {
        _manager = GetComponent<BehaviorManager>();
        _executor = GetComponent<BorrowedDutyExecutor>();
        _equipment = GetComponent<WardenEquipment>();
    }
    public void PostInitializeEntity()
    {
        _initialized = true;
        if (!ReturnAssigned) return;
        try { BorrowedDutyRecoveryOrder.Install(_manager, this); UnsupportedReason = null; }
        catch (Exception exception) { UnsupportedReason = exception.Message; }
    }
    // QA prepares current employees outside native root enumeration. An unassigned root stays inert.
    internal void PrepareRecovery()
    {
        if (!_initialized) throw new InvalidOperationException("Borrowed recovery preparation requires an initialized actor.");
        BorrowedDutyRecoveryOrder.RequireInstallable(_manager, this);
        _resources.TransferInventory(() => BorrowedDutyRecoveryOrder.Install(_manager, this));
    }
    // Called during workplace arbitration: never change the list currently enumerated by BehaviorManager.
    internal void BindReturn(Inventory destination)
    {
        if (!_initialized || !destination || ReferenceEquals(destination, _equipment.Inventory) ||
            !string.IsNullOrEmpty(_manager.RunningExecutor.Name) || _executor.HasReservations || !RecoveryPrepared)
            throw new InvalidOperationException("Borrowed return binding requires a prepared idle actor and destination.");
        _resources.TransferInventory(() =>
        {
            if (!RecoveryPrepared) throw new InvalidOperationException("Prepared borrowed recovery order changed before binding.");
            ReturnAssigned = true;
            ReturnDestination = destination;
        });
    }
    public override Decision Decide(BehaviorAgent agent)
    {
        if (!ReturnAssigned || !RecoveryReady || !HasReturnWater(_equipment.Inventory) || _clock.PartialDayNumber < _retryAfterDay ||
            !string.IsNullOrEmpty(_manager.RunningExecutor.Name)) return Decision.ReleaseNow();
        Defer();
        return _executor.TryLaunchReturn(ReturnDestination) ? Decision.ReleaseWhenFinished(_executor) : Decision.ReleaseNow();
    }
    internal static bool HasReturnWater(Inventory inventory) => inventory.Enabled &&
        inventory.AmountInStock(WardenEquipment.WaterId) == 1 && inventory.Stock.Sum(good => good.Amount) == 1 &&
        inventory.HasUnreservedStock(WardenEquipment.Bucket);
    internal void Defer() => _retryAfterDay = _clock.PartialDayNumber + .1f / 24f;
    internal void Returned()
    {
        ReturnAssigned = false;
        ReturnDestination = null;
    }
    internal Decision Own(BorrowedDutyExecutor executor)
    {
        var decision = Decision.ReleaseWhenFinished(executor);
        return Decision.TransferNow(this, in decision);
    }
    public void Save(IEntitySaver saver)
    {
        _resources.ThrowIfSaveUnsafe();
        var state = saver.GetComponent(Key);
        state.Set(Version, 1);
        state.Set(Assigned, ReturnAssigned);
        if (ReturnDestination is not null && ReturnDestination)
            state.Set(Destination, ReturnDestination, _references.Of<Inventory>());
    }
    public void Load(IEntityLoader loader)
    {
        ReturnAssigned = false;
        ReturnDestination = null;
        if (!loader.TryGetComponent(Key, out var state)) return;
        if (state.Get(Version) != 1) throw new InvalidOperationException("Unsupported borrowed return binding.");
        ReturnAssigned = state.Get(Assigned);
        if (ReturnAssigned && state.Has(Destination))
        {
            state.GetObsoletable(Destination, _references.Of<Inventory>(), out var destination);
            ReturnDestination = destination;
        }
    }
}
