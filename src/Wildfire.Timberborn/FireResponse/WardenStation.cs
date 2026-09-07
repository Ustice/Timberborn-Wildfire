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
    public bool Operational { get; private set; }
    public string Status { get; private set; } = "Waiting for construction";

    public void InitializeInventory(Inventory inventory) => Inventory = inventory;
    public void Awake()
    {
        Workplace = GetComponent<Workplace>();
        Access = GetComponent<Accessible>();
    }
    public void OnEnterFinishedState() { Operational = true; Inventory.Enable(); }
    public void OnExitFinishedState() { Operational = false; Inventory.Disable(); }

    public override Decision Decide(BehaviorAgent agent)
    {
        if (!Operational || !Inventory.Enabled) return Decision.ReleaseNow();
        var executor = agent.GetComponent<WardenExecutor>();
        if (!executor.TryLaunch(this))
        {
            Status = executor.Status;
            if (!Inventory.HasUnreservedStock(WardenEquipment.Bucket) &&
                !agent.GetComponent<GoodCarrier>().IsCarrying &&
                !agent.GetComponent<GoodReserver>().HasReservedStock &&
                !agent.GetComponent<GoodReserver>().HasReservedCapacity &&
                agent.GetComponent<CarrierInventoryFinder>().TryCarryFromAnyInventoryLimited(WardenEquipment.WaterId, Inventory, 1))
                Status = "Fetching reserve water";
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
        var initializer = _factory.Create(inventory, 20, "Wildfire.WardenStation");
        initializer.AddAllowedGood(new StorableGoodAmount(StorableGood.CreateGiveableAndTakeable(WardenEquipment.WaterId), 20));
        initializer.HasPublicInput();
        initializer.Initialize();
        station.InitializeInventory(inventory);
    }
}
