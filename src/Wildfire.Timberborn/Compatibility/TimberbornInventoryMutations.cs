using Timberborn.Carrying;
using Timberborn.Goods;
using Timberborn.InventorySystem;

namespace Wildfire.Timberborn.Compatibility;

/// <summary>
/// Maps Wildfire inventory effects to Timberborn's stock accounting categories.
/// Native 1.1 distinguishes consumption/production from movement of existing goods.
/// </summary>
internal static class TimberbornInventoryMutations
{
    public static void Consume(Inventory inventory, GoodAmount goods) => inventory.TakeConsumed(goods);

    // Surviving construction materials and QA fixture stock already represent existing goods.
    // Do not report them as newly manufactured or harvested output.
    public static void RestoreExisting(Inventory inventory, GoodAmount goods) =>
        inventory.GiveExistingIgnoringCapacity(goods);

    // Native yield collection carries uncountable goods until their first inventory deposit.
    public static void CarryHarvest(GoodCarrier carrier, GoodAmount goods) =>
        carrier.PutGoodsInHands(new CarriedGood(goods, CarriedGoodType.Uncountable));

    public static void DepositHarvest(Inventory inventory, GoodAmount goods) => inventory.GiveProduced(goods);
}
