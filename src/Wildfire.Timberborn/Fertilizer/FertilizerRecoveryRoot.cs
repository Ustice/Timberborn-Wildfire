using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.EntitySystem;
using Timberborn.InventorySystem;
using Timberborn.TimeSystem;

namespace Wildfire.Timberborn.Fertilizer;

/// <summary>Inactive prototype. Existing satchel stock requests deposit only, independent of employment.</summary>
public sealed class FertilizerRecoveryRoot : RootBehavior, IAwakableComponent, IPostInitializableEntity
{
    private readonly IDayNightCycle _clock;
    private FertilizerSatchel _satchel = null!;
    private FertilizerRecoveryExecutor _executor = null!;
    private BehaviorManager _manager = null!;
    private float _retryAfterDay;
    private bool _installed;
    public bool RecoveryReady
    {
        get
        {
            if (!_installed) return false;
            try { return FertilizerRecoveryOrder.IsInstalled(_manager, this); }
            catch (Exception exception) { UnsupportedReason = exception.Message; return false; }
        }
    }
    public string? UnsupportedReason { get; private set; }
    public FertilizerRecoveryRoot(IDayNightCycle clock) => _clock = clock;
    public void Awake()
    {
        _satchel = GetComponent<FertilizerSatchel>();
        _executor = GetComponent<FertilizerRecoveryExecutor>();
        _manager = GetComponent<BehaviorManager>();
    }
    public void PostInitializeEntity()
    {
        _installed = false;
        try { FertilizerRecoveryOrder.Install(_manager, this); _installed = true; UnsupportedReason = null; }
        catch (Exception exception) { UnsupportedReason = exception.Message; }
    }
    internal static bool HasDepositRequest(Inventory inventory)
    {
        if (inventory.IsEmpty) return false;
        if (!FertilizerSatchelStock.HasUnit(inventory))
            throw new InvalidOperationException("Fertilizer recovery requires exactly one native ash unit.");
        return inventory.Enabled && inventory.HasUnreservedStock(FertilizerSatchelStock.Unit);
    }
    public override Decision Decide(BehaviorAgent agent)
    {
        if (!RecoveryReady || _clock.PartialDayNumber < _retryAfterDay || !HasDepositRequest(_satchel.Inventory))
            return Decision.ReleaseNow();
        Defer(); // A failed search cannot immediately reacquire in this arbitration cycle.
        return _executor.TryLaunch() ? Decision.ReleaseWhenFinished(_executor) : Decision.ReleaseNow();
    }
    internal void Defer() => _retryAfterDay = _clock.PartialDayNumber + .1f / 24f;
}
