using Wildfire.Timberborn.Resources;
using Wildfire.Timberborn.Runtime;
using Timberborn.BaseComponentSystem;
using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.TemplateInstantiation;

namespace Wildfire.Timberborn.FireResponse;

/// <summary>A private native inventory, not a second water counter or a carrier delivery job.</summary>
public sealed class WardenEquipment : BaseComponent, IAwakableComponent, IInitializableEntity,
    IPostLoadableEntity, IDeletableEntity
{
    public const string WaterId = "Water";
    public static readonly GoodAmount Bucket = new(WaterId, 1);
    private readonly NativeResourceCoordinator _delivery;
    private readonly Action<string> _warn = new UnityTimberbornFireLogSink().Warning;
    public WardenEquipment(NativeResourceCoordinator delivery) => _delivery = delivery;
    private PersonalInventoryDistrictRegistration _registration = null!;
    public Inventory Inventory { get; private set; } = null!;
    public bool Loaded => Inventory.AmountInStock(WaterId) == 1;

    public void InitializeInventory(Inventory inventory) => Inventory = inventory;

    public void Awake() => _registration = new(Inventory, _delivery, static () => true,
        exception => _warn($"wildfire_warden_equipment_lifecycle status=indeterminate operation=unregister_district error={exception}"));

    public void InitializeEntity() => _registration.Restore();
    public void PostLoadEntity() => _registration.Restore();
    public void DeleteEntity() => _registration.Exit();

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
