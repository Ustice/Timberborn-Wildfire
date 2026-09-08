using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed partial class BorrowedDutyRecoveryTests
{
    [Fact]
    public void OwnCapacityCleanupDoesNotLoseIdentityWhenForeignStockReservationAppears()
    {
        using var f = new Fixture();
        using var water = new NativeShorelineWaterFixture(NativeManagedTestContext.ProxyContracts);
        var destination = water.Bucket();
        f.AttachInventory(destination);
        var foreign = water.Bucket();
        f.AttachInventory(foreign);
        f.Call(destination, "Enable");
        f.Call(foreign, "Enable");
        var unit = Activator.CreateInstance(f.Type("Timberborn.Goods", "GoodAmount"), "Water", 1)!;
        f.Call(foreign, "GiveExisting", unit);
        var reserver = RuntimeHelpers.GetUninitializedObject(f.Type("Timberborn.InventorySystem", "GoodReserver"));
        f.Call(reserver, "ReserveCapacity", destination, unit);
        // Native reservation can appear after our return reserved capacity.
        f.Call(reserver, "ReserveExactStockAmount", foreign, unit);
        var executor = Activator.CreateInstance(f.Mod("BorrowedDutyExecutor"), new object?[5])!;
        Set(executor, "_reserver", reserver);
        Set(executor, "_returnInventory", destination);
        Assert.False((bool)f.Call(executor, "ExactReturnReservation")!);
        Assert.True((bool)f.Call(executor, "OwnReturnCapacity")!);
        var resources = Get(f.Behavior, "_resources")!;
        f.Call(resources, "TransferInventory", (Action)(() => f.Call(executor, "ReleaseReturnReservation")));
        var capacity = f.Call(reserver, "get_CapacityReservation")!;
        var stock = f.Call(reserver, "get_StockReservation")!;
        Assert.Null(f.Call(capacity, "get_Inventory"));
        Assert.Same(foreign, f.Call(stock, "get_Inventory"));
        Assert.Equal(1, water.Stock(foreign));
        f.Call(resources, "ThrowIfSaveUnsafe");
        // Positive Unity truth/native reserved-amount decrement remains an engine boundary;
        // this executes the actual GoodReserver reference cleanup with no fabricated liveness.
    }

    [Fact]
    public void CleanupLeavesForeignCapacityReservationUnchanged()
    {
        using var f = new Fixture();
        using var water = new NativeShorelineWaterFixture(NativeManagedTestContext.ProxyContracts);
        var ours = water.Bucket();
        var foreign = water.Bucket();
        f.AttachInventory(foreign);
        f.Call(foreign, "Enable");
        var unit = Activator.CreateInstance(f.Type("Timberborn.Goods", "GoodAmount"), "Water", 1)!;
        var reserver = RuntimeHelpers.GetUninitializedObject(f.Type("Timberborn.InventorySystem", "GoodReserver"));
        f.Call(reserver, "ReserveCapacity", foreign, unit);
        var executor = Activator.CreateInstance(f.Mod("BorrowedDutyExecutor"), new object?[5])!;
        Set(executor, "_reserver", reserver);
        Set(executor, "_returnInventory", ours);
        f.Call(executor, "ReleaseReturnReservation");
        Assert.Same(foreign, f.Call(f.Call(reserver, "get_CapacityReservation")!, "get_Inventory"));
    }
}
