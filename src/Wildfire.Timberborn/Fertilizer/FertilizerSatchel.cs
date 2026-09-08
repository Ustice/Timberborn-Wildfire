using Timberborn.BaseComponentSystem;
using Timberborn.Common;
using Timberborn.Characters;
using Timberborn.EntitySystem;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.MortalSystem;
using Timberborn.ResourceCountingSystem;
using Timberborn.TemplateInstantiation;
using Wildfire.Timberborn.Resources;
using Wildfire.Timberborn.Runtime;

namespace Wildfire.Timberborn.Fertilizer;

/// <summary>One native ash unit. No worker, tool or template admission is registered by this component.</summary>
public sealed class FertilizerSatchel : BaseComponent, IAwakableComponent, IInitializableEntity,
    IPostLoadableEntity, IDeletableEntity
{
    public const string InventoryName = "Wildfire.FertilizerSatchel";
    private readonly NativeResourceCoordinator _resources;
    private readonly Action<string> _warn = new UnityTimberbornFireLogSink().Warning;
    private bool _exited;
    private Citizen _citizen = null!;
    private Mortal _mortal = null!;
    private Character _character = null!;
    private DistrictInventoryRegistry? _registry;
    private DistrictResourceCounter? _counter;
    private SatchelCounter _satchelCounter = null!;
    public Inventory Inventory { get; private set; } = null!;
    public bool Loaded => FertilizerSatchelStock.HasUnit(Inventory);

    public FertilizerSatchel(NativeResourceCoordinator resources) => _resources = resources;

    public void InitializeInventory(Inventory inventory)
    {
        if (Inventory is not null) throw new InvalidOperationException("Fertilizer satchel inventory is already bound.");
        Inventory = inventory;
        _satchelCounter = new(inventory);
    }

    public void Awake()
    {
        _citizen = GetComponent<Citizen>();
        _mortal = GetComponent<Mortal>();
        _citizen.ChangedAssignedDistrict += OnDistrictChanged;
        _character = GetComponent<Character>();
        _character.Died += OnDied;
    }

    public void InitializeEntity() => RestoreRegistration();
    public void PostLoadEntity() => RestoreRegistration();
    public void DeleteEntity() => CleanupDistrictForExit();

    /// <summary>The owning worker must prove arrival and own this exact non-consuming source reservation.</summary>
    public bool TryPickup(Inventory source, GoodReserver reserver, Action commitPhase)
    {
        if (commitPhase is null) throw new ArgumentNullException(nameof(commitPhase));
        if (!Live || !source || ReferenceEquals(source, Inventory) || !source.Enabled ||
            !FertilizerSatchelStock.IsEmpty(Inventory) || !ExactStockReservation(source, reserver)) return false;
        bool moved = false;
        _resources.TransferInventory(() =>
        {
            if (!Live || !source || !ExactStockReservation(source, reserver)) return;
            reserver.UnreserveStock();
            RequireNoReservations(reserver);
            if (!FertilizerSatchelStock.TryMoveUnit(source, Inventory)) return;
            if (!Live || !source || !Loaded)
                throw new InvalidOperationException("Fertilizer pickup lost its live native owner.");
            commitPhase();
            moved = true;
        });
        return moved;
    }

    /// <summary>The owning worker must prove arrival and own this exact destination capacity reservation.</summary>
    public bool TryReturn(Inventory destination, GoodReserver reserver, Action commitPhase)
    {
        if (commitPhase is null) throw new ArgumentNullException(nameof(commitPhase));
        if (!Live || !destination || ReferenceEquals(destination, Inventory) || !destination.Enabled ||
            !Loaded || !ExactCapacityReservation(destination, reserver)) return false;
        bool moved = false;
        _resources.TransferInventory(() =>
        {
            if (!Live || !destination || !ExactCapacityReservation(destination, reserver)) return;
            reserver.UnreserveCapacity();
            RequireNoReservations(reserver);
            if (!FertilizerSatchelStock.TryMoveUnit(Inventory, destination)) return;
            if (!Live || !destination || !FertilizerSatchelStock.IsEmpty(Inventory))
                throw new InvalidOperationException("Fertilizer return lost its live native owner.");
            commitPhase();
            moved = true;
        });
        return moved;
    }

    internal void ConsumeCommittedUnit(Action commitPhase)
    {
        if (commitPhase is null) throw new ArgumentNullException(nameof(commitPhase));
        _resources.RequireAshApplicationCommit();
        if (!Live) throw new InvalidOperationException("Fertilizer application lost its live native owner.");
        FertilizerSatchelStock.ConsumeCommittedUnit(Inventory, _resources, () =>
        {
            if (!Live) throw new InvalidOperationException("Fertilizer owner died during native consumption.");
            commitPhase();
        });
    }

    private bool Live => this && Inventory && Inventory.Enabled && _mortal && !_mortal.Dead && !_mortal.ShouldDie;

    internal static bool ExactStockReservation(Inventory source, GoodReserver reserver)
    {
        var reservation = reserver.StockReservation;
        return ReferenceEquals(reservation.Inventory, source) && IsUnit(reservation.GoodAmount) &&
            !reservation.ConsumeGood && reservation.FixedAmount && reserver.CapacityReservation.Inventory is null;
    }

    internal static bool ExactCapacityReservation(Inventory destination, GoodReserver reserver)
    {
        var reservation = reserver.CapacityReservation;
        return ReferenceEquals(reservation.Inventory, destination) && IsUnit(reservation.GoodAmount) &&
            reservation.FixedAmount && !reservation.ConsumeGood && reserver.StockReservation.Inventory is null;
    }

    private static bool IsUnit(GoodAmount good) => good.GoodId == FertilizerSatchelStock.GoodId && good.Amount == 1;

    private static void RequireNoReservations(GoodReserver reserver)
    {
        if (reserver.StockReservation.Inventory is not null || reserver.CapacityReservation.Inventory is not null)
            throw new InvalidOperationException("Native callback replaced the fertilizer worker's reservation.");
    }

    private void RestoreRegistration() => UpdateRegistration(enableInventory: true);

    private void OnDistrictChanged(object sender, ChangeAssignedDistrictEventArgs args)
    {
        // Native Citizen.OnDied can unassign before the satchel's own Died subscriber runs.
        if (!_character.Alive) CleanupDistrictForExit();
        else UpdateRegistration(enableInventory: false);
    }
    private void OnDied(object sender, EventArgs args) => CleanupDistrictForExit();

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
            _resources.TransferInventory(() =>
            {
                if (enableInventory) Inventory.Enable();
                RegisterDistrictCore();
            });
        }
        catch
        {
            // A caught reentrant native district callback still invalidates its enclosing read/write.
            _resources.InvalidateAfterLifecycleFailure();
            throw;
        }
    }

    private void RegisterDistrictCore()
    {
        if (_resources.IsIndeterminate) _resources.ThrowIfSaveUnsafe();
        UnregisterDistrict();
        if (_resources.IsIndeterminate) _resources.ThrowIfSaveUnsafe();
        if (_exited || !_character.Alive || !_citizen.HasAssignedDistrict || _mortal.Dead || _mortal.ShouldDie) return;
        var district = _citizen.AssignedDistrict;
        _registry = district.GetComponent<DistrictInventoryRegistry>();
        _counter = district.GetComponent<DistrictResourceCounter>();
        _registry.Add(Inventory);
        if (_resources.IsIndeterminate || _exited || !_character.Alive || _mortal.Dead || _mortal.ShouldDie ||
            !ReferenceEquals(_citizen.AssignedDistrict, district))
            throw new InvalidOperationException("Fertilizer district registration changed during its native callback.");
        _counter.Add(_satchelCounter);
    }

    private void CleanupDistrictForExit()
    {
        if (_exited) return;
        _exited = true;
        _citizen.ChangedAssignedDistrict -= OnDistrictChanged;
        _character.Died -= OnDied;
        // Native teardown must continue, without retrying uncertain or unguarded writes.
        if (_resources.IsIndeterminate) return;
        try { _resources.TransferInventory(UnregisterDistrict); }
        catch (Exception exception)
        {
            _resources.InvalidateAfterLifecycleFailure();
            try
            {
                _warn($"wildfire_fertilizer_satchel_lifecycle status=indeterminate operation=unregister_district error={exception}");
            }
            catch
            {
                // One best-effort diagnostic cannot prevent native death/delete or retry cleanup.
            }
        }
    }

    private void UnregisterDistrict()
    {
        _counter?.Remove(_satchelCounter);
        _registry?.Remove(Inventory);
        _counter = null;
        _registry = null;
    }

    private sealed class SatchelCounter : IGoodProcessor
    {
        private static readonly List<GoodAmount> Empty = new();
        internal SatchelCounter(Inventory inventory) => Inventory = inventory;
        public Inventory Inventory { get; }
        public ReadOnlyList<GoodAmount> ProcessedGoods => Empty.AsReadOnlyList();
    }
}

public sealed class FertilizerSatchelInventoryInitializer : IDedicatedDecoratorInitializer<FertilizerSatchel, Inventory>
{
    private readonly InventoryInitializerFactory _factory;
    public FertilizerSatchelInventoryInitializer(InventoryInitializerFactory factory) => _factory = factory;
    public void Initialize(FertilizerSatchel satchel, Inventory inventory)
    {
        var initializer = _factory.Create(inventory, 1, FertilizerSatchel.InventoryName);
        initializer.AddAllowedGood(new StorableGoodAmount(StorableGood.CreateAsGivable(FertilizerSatchelStock.GoodId), 1));
        initializer.Initialize();
        satchel.InitializeInventory(inventory);
    }
}
