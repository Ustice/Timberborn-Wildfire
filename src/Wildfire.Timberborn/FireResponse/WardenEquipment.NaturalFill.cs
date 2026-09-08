using Timberborn.EntitySystem;
using Timberborn.InventorySystem;
using Timberborn.MortalSystem;
using Timberborn.WaterBuildings;
using Wildfire.Timberborn.Compatibility;
using Wildfire.Timberborn.FireBell;

namespace Wildfire.Timberborn.FireResponse;

internal enum NaturalWaterFillResult { Unavailable, AwaitingCredit, Filled }

public sealed partial class WardenEquipment
{
    // Caller owns current duty, exact shoreline arrival and bounded waiting. Never called from parallel actor ticks.
    internal NaturalWaterFillResult TryFillFromNaturalSource(TimberbornNaturalWaterSource source,
        Action validatePickup, Action commitLoaded)
    {
        if (validatePickup is null) throw new ArgumentNullException(nameof(validatePickup));
        if (commitLoaded is null) throw new ArgumentNullException(nameof(commitLoaded));
        var inventory = Inventory;
        if (source is null || !this || !inventory || _registration is null ||
            !source.TryCaptureCleanIntake(_delivery, out var intake)) return NaturalWaterFillResult.Unavailable;
        var owner = GetComponent<EntityComponent>();
        var mortal = GetComponent<Mortal>();
        bool Live() => this && inventory && owner && owner.Initialized && !owner.Deleted && mortal && !mortal.Dead && !mortal.ShouldDie &&
            !_registration.Exited && _registration.OwnsInventory(inventory) && ReferenceEquals(Inventory, inventory) &&
            ReferenceEquals(GetComponent<EntityComponent>(), owner) && ReferenceEquals(GetComponent<Mortal>(), mortal) &&
            ReferenceEquals(inventory.GetComponent<EntityComponent>(), owner) && inventory.Enabled &&
            WardenEquipmentReturnStock.IsDedicatedInventory(inventory);
        if (!Live() || !EmptyBucket(inventory)) return NaturalWaterFillResult.Unavailable;
        void RequireNative()
        {
            if (!Live() || !source.MatchesCleanIntake(_delivery, intake))
                throw new InvalidOperationException("Native source or bucket ownership changed during pickup.");
        }
        var result = NaturalWaterFillResult.Unavailable;
        _delivery.TransferInventory(() =>
        {
            RequireNative();
            result = ConvertCreditedUnit(inventory, intake.Input, intake.Contract, () =>
            {
                RequireNative(); validatePickup(); RequireNative();
            }, commitLoaded);
            RequireNative(); // Caller phase changed; this checks only native identities after commit.
        });
        return result;
    }
    private static bool EmptyBucket(Inventory inventory) => inventory.Enabled && WardenEquipmentReturnStock.IsDedicatedInventory(inventory) &&
        inventory.IsEmpty && inventory.ReservedCapacity(WaterId) == 0 && inventory.UnreservedAmountInStock(WaterId) == 0 && inventory.HasUnreservedCapacity(Bucket);

    // Native accounting seam. The operation above supplies its existing guard and current source/actor admission.
    internal static NaturalWaterFillResult ConvertCreditedUnit(Inventory inventory, WaterInput input,
        TimberbornWaterCreditContract contract, Action validatePickup, Action commitLoaded)
    {
        validatePickup();
        if (!EmptyBucket(inventory)) return NaturalWaterFillResult.Unavailable;
        var before = contract.Buffer(input);
        if (!contract.ValidBuffer(input)) throw new InvalidOperationException("Invalid credited native water.");
        float quantum = contract.WaterUnit;
        if (before.Clean < quantum)
        {
            input.DemandCleanWaterAmount(quantum); // At most one demand; no fluid tick, epsilon or invented credit.
            validatePickup();
            if (!EmptyBucket(inventory) || contract.Buffer(input) != before)
                throw new InvalidOperationException("Native demand changed credited resources.");
            return NaturalWaterFillResult.AwaitingCredit;
        }
        input.RemoveCleanWater(quantum);
        var expected = (Clean: before.Clean - quantum, before.Dirty);
        void RequireBuffer()
        {
            if (contract.Buffer(input) != expected) throw new InvalidOperationException("Native water changed after bucket debit.");
        }
        RequireBuffer();
        inventory.GiveProduced(Bucket);
        void RequireFilled()
        {
            RequireBuffer();
            if (!WardenEquipmentReturnStock.IsDedicatedInventory(inventory) || !WardenEquipmentReturnStock.HasUnit(inventory) ||
                !inventory.Enabled || inventory.ReservedCapacity(WaterId) != 0)
                throw new InvalidOperationException("Native bucket changed after production.");
        }
        validatePickup(); RequireFilled();
        commitLoaded();
        RequireFilled();
        return NaturalWaterFillResult.Filled;
    }
}
