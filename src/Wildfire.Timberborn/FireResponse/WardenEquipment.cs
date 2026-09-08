using Wildfire.Timberborn.Resources;
using Timberborn.BaseComponentSystem;
using Timberborn.Common;
using Timberborn.Characters;
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
    private readonly NativeResourceCoordinator _delivery;
    public WardenEquipment(NativeResourceCoordinator delivery) => _delivery = delivery;
    private Citizen _citizen = null!;
    private Character _character = null!;
    private bool _exited;
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
        _character = GetComponent<Character>();
        _citizen.ChangedAssignedDistrict += OnDistrictChanged;
        _character.Died += OnDied;
    }

    public void InitializeEntity() => RestoreRegistration();
    public void PostLoadEntity() => RestoreRegistration();
    public void DeleteEntity() => CleanupDistrictForExit();

    private void OnDied(object sender, EventArgs args) => CleanupDistrictForExit();

    private void OnDistrictChanged(object sender, ChangeAssignedDistrictEventArgs args)
    {
        // Citizen.OnDied can unassign the district before our own Died subscriber runs.
        if (!_character.Alive) CleanupDistrictForExit();
        else UpdateRegistration(enableInventory: false);
    }

    private void RestoreRegistration() => UpdateRegistration(enableInventory: true);

    private void UpdateRegistration(bool enableInventory)
    {
        if (_exited) return;
        if (!_character.Alive)
        {
            CleanupDistrictForExit();
            return;
        }
        try
        {
            _delivery.TransferInventory(() =>
            {
                if (enableInventory) Inventory.Enable();
                RegisterDistrictCore();
            });
        }
        catch
        {
            // A native district/lifecycle callback has already begun. Rejected reentry
            // cannot leave a successful enclosing capture or transfer behind.
            _delivery.InvalidateAfterLifecycleFailure();
            throw;
        }
    }

    private void RegisterDistrictCore()
    {
        if (_delivery.IsIndeterminate) _delivery.ThrowIfSaveUnsafe();
        UnregisterDistrict();
        if (_delivery.IsIndeterminate) _delivery.ThrowIfSaveUnsafe();
        if (_exited || !_character.Alive || !_citizen.HasAssignedDistrict) return;
        var district = _citizen.AssignedDistrict;
        _registry = district.GetComponent<DistrictInventoryRegistry>();
        _counter = district.GetComponent<DistrictResourceCounter>();
        // Even private inventory registration notifies native production/consumption observers.
        _registry.Add(Inventory);
        if (_delivery.IsIndeterminate || _exited || !_character.Alive ||
            !ReferenceEquals(_citizen.AssignedDistrict, district))
            throw new InvalidOperationException("Warden district registration changed during its native callback.");
        _counter.Add(_equipmentCounter);
    }

    private void CleanupDistrictForExit()
    {
        if (_exited) return;
        _exited = true;
        _citizen.ChangedAssignedDistrict -= OnDistrictChanged;
        _character.Died -= OnDied;
        // Keep native death/deletion progressing without replaying an uncertain write.
        if (_delivery.IsIndeterminate) return;
        try { _delivery.TransferInventory(UnregisterDistrict); }
        catch { _delivery.InvalidateAfterLifecycleFailure(); }
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
