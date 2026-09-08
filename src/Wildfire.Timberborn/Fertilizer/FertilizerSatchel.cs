using Timberborn.BaseComponentSystem;
using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.MortalSystem;
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
    private PersonalInventoryDistrictRegistration _registration = null!;
    private Mortal _mortal = null!;
    public Inventory Inventory { get; private set; } = null!;
    public bool Loaded => FertilizerSatchelStock.HasUnit(Inventory);

    public FertilizerSatchel(NativeResourceCoordinator resources) => _resources = resources;

    public void InitializeInventory(Inventory inventory)
    {
        if (Inventory is not null) throw new InvalidOperationException("Fertilizer satchel inventory is already bound.");
        Inventory = inventory;
    }

    public void Awake()
    {
        _mortal = GetComponent<Mortal>();
        _registration = new(Inventory, _resources, () => !_mortal.Dead && !_mortal.ShouldDie,
            exception => _warn($"wildfire_fertilizer_satchel_lifecycle status=indeterminate operation=unregister_district error={exception}"));
    }

    public void InitializeEntity() => _registration.Restore();
    public void PostLoadEntity() => _registration.Restore();
    public void DeleteEntity() => _registration.Exit();

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

    private bool Live => !_registration.Exited && this && Inventory && Inventory.Enabled && _mortal && !_mortal.Dead && !_mortal.ShouldDie;

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
