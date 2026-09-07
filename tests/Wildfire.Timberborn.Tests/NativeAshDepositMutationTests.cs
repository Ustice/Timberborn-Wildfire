using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeAshDepositMutationTests
{
    [Theory]
    [InlineData(null, 0, 1)]
    [InlineData("InventoryChanged", 1, 0)]
    [InlineData("InventoryStockChanged", 1, 1)]
    [InlineData("CarriedGoodsChanged", 0, 1)]
    public void InstalledNativeDepositEventsCannotLeavePartialConversionSaveable(string? failEvent, int expectedHands, int expectedStock)
    {
        using var native = new NativeManagedTestContext();
        var inventories = native.LoadNative("Timberborn.InventorySystem");
        var goods = native.LoadNative("Timberborn.Goods");
        var carrying = native.LoadNative("Timberborn.Carrying");
        var inventoryType = inventories.GetType("Timberborn.InventorySystem.Inventory")!;
        var carrierType = carrying.GetType("Timberborn.Carrying.GoodCarrier")!;
        var inventory = RuntimeHelpers.GetUninitializedObject(inventoryType);
        var carrier = RuntimeHelpers.GetUninitializedObject(carrierType);
        var registryType = goods.GetType("Timberborn.Goods.GoodRegistry")!;
        foreach (var field in new[] { "_storage", "_reservedStock", "_reservedCapacity" })
            Field(inventoryType, field).SetValue(inventory, Activator.CreateInstance(registryType));
        Field(inventoryType, "<Capacity>k__BackingField").SetValue(inventory, 1);
        Field(inventoryType, "_goodDisallower").SetValue(inventory, Activator.CreateInstance(inventories.GetType("Timberborn.InventorySystem.NullGoodDisallower")!));
        var allowedType = goods.GetType("Timberborn.Goods.StorableGoodRegistry")!;
        var allowed = Activator.CreateInstance(allowedType)!;
        var storable = goods.GetType("Timberborn.Goods.StorableGood")!.GetMethod("CreateAsTakeable")!.Invoke(null, new[] { "FertileAsh" });
        var storableAmountType = goods.GetType("Timberborn.Goods.StorableGoodAmount")!;
        var allowedArray = Array.CreateInstance(storableAmountType, 1);
        allowedArray.SetValue(Activator.CreateInstance(storableAmountType, storable, 1), 0);
        allowedType.GetMethod("Add")!.Invoke(allowed, new object[] { allowedArray });
        Field(inventoryType, "_allowedGoods").SetValue(inventory, allowed);
        var amount = Activator.CreateInstance(goods.GetType("Timberborn.Goods.GoodAmount")!, "FertileAsh", 1)!;
        var carried = Activator.CreateInstance(carrying.GetType("Timberborn.Carrying.CarriedGood")!, amount,
            Enum.Parse(carrying.GetType("Timberborn.Carrying.CarriedGoodType")!, "Uncountable"))!;
        carrierType.GetMethod("PutGoodsInHands")!.Invoke(carrier, new[] { carried });
        inventoryType.GetMethod("ReserveCapacity")!.Invoke(inventory, new[] { amount });
        Assert.Equal(0, inventoryType.GetMethod("UnreservedCapacity")!.Invoke(inventory, new[] { "FertileAsh" }));
        var transaction = new NativeResourceTransaction();
        var failure = new InvalidOperationException("native subscriber failed after mutation");
        if (failEvent is not null)
        {
            var target = failEvent.StartsWith("Inventory") ? inventory : carrier;
            var eventInfo = target.GetType().GetEvent(failEvent)!;
            var invoke = eventInfo.EventHandlerType!.GetMethod("Invoke")!;
            var parameters = invoke.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
            var callback = Expression.Lambda(eventInfo.EventHandlerType, Expression.Throw(Expression.Constant(failure)), parameters).Compile();
            eventInfo.AddEventHandler(target, callback);
        }
        bool phaseFinished = false;
        void Deposit() => transaction.TransferInventory(() =>
        {
            inventoryType.GetMethod("UnreserveCapacity")!.Invoke(inventory, new[] { amount });
            Assert.Throws<InvalidOperationException>(transaction.ThrowIfSaveUnsafe);
            inventoryType.GetMethod("GiveProduced")!.Invoke(inventory, new[] { amount });
            carrierType.GetMethod("EmptyHands")!.Invoke(carrier, null);
            phaseFinished = true;
        });
        if (failEvent is null) { Deposit(); transaction.ThrowIfSaveUnsafe(); }
        else
        {
            Assert.Same(failure, Assert.Throws<TargetInvocationException>(Deposit).InnerException);
            Assert.Throws<InvalidOperationException>(transaction.ThrowIfSaveUnsafe);
            Assert.Throws<InvalidOperationException>(Deposit); // no speculative second deposit
        }
        Assert.Equal(expectedStock, inventoryType.GetMethod("AmountInStock")!.Invoke(inventory, new[] { "FertileAsh" }));
        Assert.Equal(expectedHands == 1, carrierType.GetProperty("IsCarrying")!.GetValue(carrier));
        Assert.Equal(failEvent is null, phaseFinished);
        Assert.Equal(failEvent is not null, transaction.IsIndeterminate);
    }
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
