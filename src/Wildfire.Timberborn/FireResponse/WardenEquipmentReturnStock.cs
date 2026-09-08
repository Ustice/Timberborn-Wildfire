using Timberborn.InventorySystem;

namespace Wildfire.Timberborn.FireResponse;

/// <summary>Concrete Water transfer after reservation release, inside the equipment's existing guard.</summary>
internal static class WardenEquipmentReturnStock
{
    internal static bool IsDedicatedInventory(Inventory inventory) =>
        inventory.ComponentName == "Wildfire.WardenEquipment" && inventory.Capacity == 1 &&
        !inventory.PublicInput && !inventory.PublicOutput && inventory.InputGoods.Count == 1 &&
        inventory.InputGoods.Contains(WardenEquipment.WaterId) && inventory.OutputGoods.Count == 0;

    internal static bool HasUnit(Inventory source) => source.AmountInStock(WardenEquipment.WaterId) == 1 &&
        source.Stock.Sum(good => good.Amount) == 1 && source.HasUnreservedStock(WardenEquipment.Bucket);

    internal static bool ExactCapacity(Inventory destination, GoodReserver reserver)
    {
        var reservation = reserver.CapacityReservation;
        return ReferenceEquals(reservation.Inventory, destination) && reservation.GoodAmount.GoodId == WardenEquipment.WaterId &&
            reservation.GoodAmount.Amount == 1 && reservation.FixedAmount && !reservation.ConsumeGood &&
            reserver.StockReservation.Inventory is null;
    }

    internal static void RequireReleased(GoodReserver reserver)
    {
        if (reserver.CapacityReservation.Inventory is not null || reserver.StockReservation.Inventory is not null)
            throw new InvalidOperationException("Native callback replaced the Warden return reservation.");
    }

    internal static bool TryMove(Inventory source, Inventory destination, Action afterDebit, Action commitReturned)
    {
        if (ReferenceEquals(source, destination) || !source.Enabled || !destination.Enabled ||
            !HasUnit(source) || !destination.HasUnreservedCapacity(WardenEquipment.Bucket)) return false;
        int before = destination.AmountInStock(WardenEquipment.WaterId);
        source.TakeExisting(WardenEquipment.Bucket);
        afterDebit();
        if (!source.IsEmpty || !destination.Enabled || !destination.HasUnreservedCapacity(WardenEquipment.Bucket) ||
            destination.AmountInStock(WardenEquipment.WaterId) != before)
            throw new InvalidOperationException("Warden return stock changed after native debit.");
        destination.GiveExisting(WardenEquipment.Bucket);
        void RequireTransferred()
        {
            if (!source.IsEmpty || !destination.Enabled || destination.AmountInStock(WardenEquipment.WaterId) != before + 1)
                throw new InvalidOperationException("Warden return stock changed after native credit.");
        }
        RequireTransferred();
        commitReturned();
        RequireTransferred();
        return true;
    }
}
