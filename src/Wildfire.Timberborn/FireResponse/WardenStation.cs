using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.BlueprintSystem;
using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Navigation;
using Timberborn.TemplateInstantiation;
using Timberborn.WorkSystem;
using Timberborn.BehaviorSystem;
using Timberborn.Carrying;
using Timberborn.ResourceCountingSystem;
using Timberborn.Common;

namespace Wildfire.Timberborn.FireResponse;

public sealed record WildfireWardenStationSpec : ComponentSpec;

public sealed class WardenStation : WorkplaceBehavior, IAwakableComponent, IFinishedStateListener, IGoodProcessor
{
    private static readonly List<GoodAmount> NoProcessedGoods = new();
    public ReadOnlyList<GoodAmount> ProcessedGoods => NoProcessedGoods.AsReadOnlyList();
    public Inventory Inventory { get; private set; } = null!;
    public Workplace Workplace { get; private set; } = null!;
    public Accessible Access { get; private set; } = null!;
    private bool _finished;
    public bool Finished => _finished;
    public bool Restocking { get; private set; }
    public const int WaterCapacity = 20;
    public bool Operational => _finished && Workplace.Enabled && Inventory.Enabled;
    public string Status { get; private set; } = "Waiting for construction";

    public void InitializeInventory(Inventory inventory) => Inventory = inventory;
    public void Awake()
    {
        Workplace = GetComponent<Workplace>();
        Access = GetComponent<Accessible>();
    }
    public void OnEnterFinishedState() { _finished = true; Inventory.Enable(); }
    public void OnExitFinishedState() { _finished = false; Inventory.Disable(); }

    public override Decision Decide(BehaviorAgent agent)
    {
        Restocking = false;
        if (!Operational || !Inventory.Enabled) return Decision.ReleaseNow();
        var executor = agent.GetComponent<WardenExecutor>();
        if (!executor.TryLaunch(this))
        {
            Status = executor.Status;
            if (!Inventory.HasUnreservedStock(WardenEquipment.Bucket) &&
                !agent.GetComponent<GoodCarrier>().IsCarrying &&
                agent.GetComponent<GoodReserver>().StockReservation.Inventory is null &&
                agent.GetComponent<GoodReserver>().CapacityReservation.Inventory is null &&
                agent.GetComponent<CarrierInventoryFinder>().TryCarryFromAnyInventoryLimited(WardenEquipment.WaterId, Inventory, 1))
            { Restocking = true; Status = "Fetching reserve water"; }
            return Decision.ReleaseNow();
        }
        Status = "Responding";
        return Decision.ReleaseWhenFinished(executor);
    }
}

public sealed class WardenStationInventoryInitializer : IDedicatedDecoratorInitializer<WardenStation, Inventory>
{
    private readonly InventoryInitializerFactory _factory;
    public WardenStationInventoryInitializer(InventoryInitializerFactory factory) => _factory = factory;
    public void Initialize(WardenStation station, Inventory inventory)
    {
        var initializer = _factory.Create(inventory, WardenStation.WaterCapacity, "Wildfire.WardenStation");
        initializer.AddAllowedGood(new StorableGoodAmount(StorableGood.CreateAsGivable(WardenEquipment.WaterId), WardenStation.WaterCapacity));
        initializer.HasPublicInput();
        initializer.Initialize();
        station.InitializeInventory(inventory);
    }
}
