using Timberborn.BaseComponentSystem;
using Timberborn.Common;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.ResourceCountingSystem;
using Timberborn.TemplateInstantiation;

namespace Wildfire.Timberborn.FireResponse;

/// <summary>A private native inventory, not a second water counter or a carrier delivery job.</summary>
public sealed class WardenEquipment : BaseComponent, IAwakableComponent, IInitializableEntity,
    IPostLoadableEntity, IDeletableEntity
{
    public const string WaterId = "Water";
    public static readonly GoodAmount Bucket = new(WaterId, 1);
    private readonly WardenDeliveryService _delivery;
    public WardenEquipment(WardenDeliveryService delivery) => _delivery = delivery;
    private Citizen _citizen = null!;
    private DistrictInventoryRegistry? _registry;
    private DistrictResourceCounter? _counter;
    public Inventory Inventory { get; private set; } = null!;
    private EquipmentCounter _equipmentCounter = null!;
    public bool Loaded => Inventory.AmountInStock(WaterId) == 1;

    public void InitializeInventory(Inventory inventory)
    { Inventory = inventory; _equipmentCounter = new EquipmentCounter(inventory); }

    public void Awake()
    {
        _citizen = GetComponent<Citizen>();
        _citizen.ChangedAssignedDistrict += OnDistrictChanged;
    }

    public void InitializeEntity() { Inventory.Enable(); RegisterDistrict(); }
    public void PostLoadEntity() { Inventory.Enable(); RegisterDistrict(); }
    public void DeleteEntity()
    {
        _citizen.ChangedAssignedDistrict -= OnDistrictChanged;
        UnregisterDistrict();
    }

    private void OnDistrictChanged(object sender, ChangeAssignedDistrictEventArgs args) => RegisterDistrict();

    private void RegisterDistrict()
    {
        UnregisterDistrict();
        if (!_citizen.HasAssignedDistrict) return;
        _registry = _citizen.AssignedDistrict.GetComponent<DistrictInventoryRegistry>();
        _counter = _citizen.AssignedDistrict.GetComponent<DistrictResourceCounter>();
        // Even private inventory registration notifies native production/consumption observers.
        _registry.Add(Inventory);
        _counter.Add(_equipmentCounter);
    }

    private void UnregisterDistrict()
    {
        _counter?.Remove(_equipmentCounter);
        _registry?.Remove(Inventory);
        _counter = null;
        _registry = null;
    }

    public bool TryFill(Inventory source, GoodReserver reserver)
    {
        if (Loaded || !reserver.HasReservedStock || reserver.StockReservation.Inventory != source ||
            !source.Enabled || !Inventory.HasUnreservedCapacity(Bucket)) return false;
        var filled = false;
        _delivery.TransferInventory(() =>
        {
            reserver.UnreserveStock();
            if (!source.HasUnreservedStock(Bucket)) return;
            source.TakeExisting(Bucket);
            Inventory.GiveExisting(Bucket);
            filled = true;
        });
        return filled;
    }

    public bool TryReturn(Inventory destination)
    {
        if (!Loaded || !destination.Enabled || !destination.HasUnreservedCapacity(Bucket)) return false;
        _delivery.TransferInventory(() =>
        {
            Inventory.TakeExisting(Bucket);
            destination.GiveExisting(Bucket);
        });
        return true;
    }

    public void ConsumeBucket() => Inventory.TakeConsumed(Bucket);

    // A plain proxy avoids the native IGoodProcessor component decorator, which requires a DistrictBuilding.
    private sealed class EquipmentCounter : IGoodProcessor
    {
        private static readonly List<GoodAmount> Empty = new();
        public EquipmentCounter(Inventory inventory) => Inventory = inventory;
        public Inventory Inventory { get; }
        public ReadOnlyList<GoodAmount> ProcessedGoods => Empty.AsReadOnlyList();
    }
}

public sealed class WardenEquipmentInventoryInitializer : IDedicatedDecoratorInitializer<WardenEquipment, Inventory>
{
    private readonly InventoryInitializerFactory _factory;
    public WardenEquipmentInventoryInitializer(InventoryInitializerFactory factory) => _factory = factory;
    public void Initialize(WardenEquipment equipment, Inventory inventory)
    {
        var initializer = _factory.Create(inventory, 1, "Wildfire.WardenEquipment");
        initializer.AddAllowedGood(new StorableGoodAmount(StorableGood.CreateAsGivable(WardenEquipment.WaterId), 1));
        initializer.Initialize();
        equipment.InitializeInventory(inventory);
    }
}
