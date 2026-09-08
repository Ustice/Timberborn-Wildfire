using Timberborn.Goods;
using Timberborn.InventorySystem;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Fertilizer;

/// <summary>Native stock operations inside the caller's existing resource mutation scope.</summary>
internal static class FertilizerSatchelStock
{
    internal const string GoodId = "FertileAsh";
    internal static readonly GoodAmount Unit = new(GoodId, 1);

    internal static bool IsEmpty(Inventory inventory) => inventory.IsEmpty;

    internal static bool HasUnit(Inventory inventory) =>
        inventory.AmountInStock(GoodId) == 1 && inventory.Stock.Sum(good => good.Amount) == 1;

    internal static bool TryMoveUnit(Inventory source, Inventory destination)
    {
        if (ReferenceEquals(source, destination) || !source.Enabled || !destination.Enabled ||
            !source.HasUnreservedStock(Unit) || !destination.HasUnreservedCapacity(Unit)) return false;
        source.TakeExisting(Unit);
        // A callback may disable/fill the destination after source removal. Failure is
        // indeterminate under the outer guard; it must never become a successful return.
        if (!destination.Enabled || !destination.HasUnreservedCapacity(Unit))
            throw new InvalidOperationException("Fertilizer destination changed after native pickup.");
        destination.GiveExisting(Unit);
        return true;
    }

    internal static void ConsumeCommittedUnit(Inventory inventory, NativeResourceCoordinator resources, Action commitPhase)
    {
        resources.RequireAshApplicationCommit();
        if (commitPhase is null) throw new ArgumentNullException(nameof(commitPhase));
        if (!inventory.Enabled || !HasUnit(inventory) || !inventory.HasUnreservedStock(Unit))
            throw new InvalidOperationException("Fertilizer application requires exactly one unreserved native ash unit.");
        inventory.TakeConsumed(Unit);
        if (!IsEmpty(inventory))
            throw new InvalidOperationException("Fertilizer stock changed during the consumption callback.");
        commitPhase();
    }
}
