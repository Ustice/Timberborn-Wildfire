using Wildfire.Timberborn.Resources;
using Wildfire.Timberborn.Runtime;
using Timberborn.BaseComponentSystem;
using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.MortalSystem;
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
        if (Loaded || !Inventory.Enabled || !reserver.HasReservedStock || reserver.StockReservation.Inventory != source ||
            !source.Enabled || !Inventory.HasUnreservedCapacity(Bucket)) return false;
        var filled = false;
        _delivery.TransferInventory(() =>
        {
            reserver.UnreserveStock();
            if (!source.HasUnreservedStock(Bucket)) return;
            source.TakeExisting(Bucket);
            if (!Inventory.Enabled || !Inventory.HasUnreservedCapacity(Bucket))
                throw new InvalidOperationException("Warden equipment changed after native water pickup.");
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
            if (!destination.Enabled || !destination.HasUnreservedCapacity(Bucket))
                throw new InvalidOperationException("Warden return destination changed after native water pickup.");
            destination.GiveExisting(Bucket);
        });
        return true;
    }

    /// <summary>The caller owns arrival and validates its return phase before clearing it in commitReturned.</summary>
    public bool TryReturn(Inventory destination, GoodReserver reserver, Action commitReturned)
    {
        if (commitReturned is null) throw new ArgumentNullException(nameof(commitReturned));
        var source = Inventory;
        if (source is null || destination is null || reserver is null ||
            !WardenEquipmentReturnStock.ExactCapacity(destination, reserver)) return false;
        if (!this || !source || !reserver || _registration is null) return false;
        var owner = GetComponent<EntityComponent>();
        var mortal = GetComponent<Mortal>();
        var destinationOwner = destination ? destination.GetComponent<EntityComponent>() : null;
        bool Live() => this && source && reserver && !_registration.Exited && source.Enabled &&
            ReferenceEquals(Inventory, source) && _registration.OwnsInventory(source) && WardenEquipmentReturnStock.IsDedicatedInventory(source) && owner && owner.Initialized && !owner.Deleted &&
            mortal && !mortal.Dead && !mortal.ShouldDie && ReferenceEquals(GetComponent<Mortal>(), mortal) && ReferenceEquals(GetComponent<EntityComponent>(), owner) &&
            ReferenceEquals(source.GetComponent<EntityComponent>(), owner) && ReferenceEquals(reserver.GetComponent<EntityComponent>(), owner);
        bool DestinationLive() => destination && destination.Enabled && destinationOwner && destinationOwner.Initialized && !destinationOwner.Deleted &&
            ReferenceEquals(destination.GetComponent<EntityComponent>(), destinationOwner);
        if (!Live() || !DestinationLive() || ReferenceEquals(source, destination) ||
            !WardenEquipmentReturnStock.HasUnit(source) || !WardenEquipmentReturnStock.ExactCapacity(destination, reserver)) return false;
        bool returned = false;
        _delivery.TransferInventory(() =>
        {
            if (!Live() || !DestinationLive() || !WardenEquipmentReturnStock.ExactCapacity(destination, reserver)) return;
            using (var release = new WardenCapacityRelease(destination, reserver))
            {
                reserver.UnreserveCapacity();
                release.RequireComplete();
            }
            if (!Live() || !DestinationLive()) return;
            void Validate()
            {
                WardenEquipmentReturnStock.RequireReleased(reserver);
                if (!Live() || !DestinationLive()) throw new InvalidOperationException("Warden return lost its live native owner or destination.");
            }
            returned = WardenEquipmentReturnStock.TryMove(source, destination, Validate, () =>
            {
                Validate();
                commitReturned();
                Validate();
            });
        });
        return returned;
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
