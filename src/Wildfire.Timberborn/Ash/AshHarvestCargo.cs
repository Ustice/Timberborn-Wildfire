using Timberborn.Carrying;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Ash;

/// <summary>Exact one-unit native ownership. No speculative refund follows a native event failure.</summary>
internal sealed class AshHarvestCargo
{
    internal static readonly GoodAmount Unit = new(TimberbornAshFieldService.FertileAshGoodId, 1);
    private readonly GoodCarrier _carrier;
    private readonly GoodReserver _reserver;
    private readonly NativeResourceCoordinator _resources;
    internal AshHarvestCargo(GoodCarrier carrier, GoodReserver reserver, NativeResourceCoordinator resources)
    { _carrier = carrier; _reserver = reserver; _resources = resources; }
    internal Inventory? Destination => _reserver.HasReservedCapacity ? _reserver.CapacityReservation.Inventory : null;
    internal bool Empty => !_carrier.IsCarrying;
    internal bool HasAnyReservation => _reserver.CapacityReservation.Inventory is not null || _reserver.StockReservation.Inventory is not null;
    internal bool HasExactReservation => _reserver.HasReservedCapacity && IsUnit(_reserver.CapacityReservation.GoodAmount);
    internal bool IsOwnedUnit => _carrier.IsCarrying && _carrier.CarriedGood.Type == CarriedGoodType.Uncountable && IsUnit(_carrier.CarriedGood.GoodAmount);
    internal void Validate(AshHarvestCycle cycle)
    {
        cycle.ValidateCargo(_carrier.IsCarrying, IsOwnedUnit, _carrier.CarriedGood.GoodAmount.Amount);
        if (_reserver.StockReservation.Inventory is not null ||
            (_reserver.CapacityReservation.Inventory is not null && !IsUnit(_reserver.CapacityReservation.GoodAmount)))
            throw new InvalidOperationException("Ash harvest has an unrelated native reservation.");
    }
    internal bool TryReserve(Inventory inventory, Action? commitPhase = null)
    {
        if (!inventory || !inventory.Enabled || !inventory.HasUnreservedCapacity(Unit)) return false;
        bool reserved = false;
        _resources.TransferInventory(() =>
        {
            _reserver.ReserveCapacity(inventory, Unit);
            // Native reservation listeners may disable the target without throwing. Do not create
            // an active phase when admission will return false and no manager will tick this job.
            if (!HasExactReservation) { ReleaseReservation(); return; }
            commitPhase?.Invoke();
            reserved = true;
        });
        return reserved;
    }
    internal void ReleaseReservation()
    {
        // HasReservedCapacity means usable/Enabled, not owned. Native release also handles
        // disabled live inventories and clears stale references to deleted inventories.
        _reserver.UnreserveCapacity();
    }
    // Already inside the shared simulator resource transaction; phase transition is part of the callback.
    internal void Receive(byte collected, AshHarvestCycle cycle)
    {
        if (!Empty || !HasExactReservation) throw new InvalidOperationException("Ash receipt lost its empty hands or exact capacity reservation.");
        if (collected == 1) TimberbornInventoryMutations.CarryHarvest(_carrier, Unit);
        else if (collected == 0) ReleaseReservation();
        cycle.Received(collected);
    }
    internal void Deposit(AshHarvestCycle cycle)
    {
        Validate(cycle);
        var destination = Destination;
        if (!IsOwnedUnit || !HasExactReservation || destination is null || !destination || !destination.Enabled)
            throw new InvalidOperationException("Ash deposit requires live exact native ownership.");
        _resources.TransferInventory(() =>
        {
            var goods = _carrier.CarriedGood.GoodAmount;
            _reserver.UnreserveCapacity();
            TimberbornInventoryMutations.DepositHarvest(destination, goods);
            _carrier.EmptyHands();
            cycle.Finish();
        });
    }
    private static bool IsUnit(GoodAmount amount) => amount.GoodId == Unit.GoodId && amount.Amount == 1;
}
