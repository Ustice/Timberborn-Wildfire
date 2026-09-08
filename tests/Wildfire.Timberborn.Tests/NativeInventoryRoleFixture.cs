using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Wildfire.Timberborn.Tests;

// Actual native role/Inventory methods over supplied managed state; no world entity or Unity object is spawned.
internal sealed class NativeInventoryRoleFixture : IDisposable
{
    internal readonly NativeReservationFixture Stock = new("Log");
    private readonly NativeManagedTestContext _native;
    internal readonly object Balance;
    internal object Inventory => Stock.Inventory;
    internal NativeInventoryRoleFixture(bool input)
    {
        _native = (NativeManagedTestContext)AssemblyLoadContext.GetLoadContext(Inventory.GetType().Assembly)!;
        var allowed = Activator.CreateInstance(T("Timberborn.Goods", "StorableGoodRegistry"))!;
        var good = T("Timberborn.Goods", "StorableGood").GetMethod(input ? "CreateAsGivable" : "CreateAsTakeable")!.Invoke(null, ["Log"]);
        var values = Array.CreateInstance(T("Timberborn.Goods", "StorableGoodAmount"), 1);
        values.SetValue(Activator.CreateInstance(values.GetType().GetElementType()!, good, 10), 0);
        Call(allowed, "Add", values); Set(Inventory, "_allowedGoods", allowed);
        Balance = Activator.CreateInstance(T("Timberborn.ResourceCountingSystem", "DistrictGoodsBalance"), new object?[] { null })!;
        Call(Balance, "OnInventoryRegistered", null, Inventory); // Actual native statistics subscriber, no simulated counter.
    }
    internal Type T(string assembly, string name) => _native.LoadNative(assembly).GetType(assembly + "." + name)!;
    internal object Role(string kind)
    {
        var type = kind switch
        {
            "Manufactory" => T("Timberborn.Workshops", "Manufactory"),
            "Recovered" => T("Timberborn.RecoveredGoodSystem", "RecoveredGoodStack"),
            _ => T("Timberborn.SimpleOutputBuildings", "SimpleOutputInventory"),
        };
        var role = RuntimeHelpers.GetUninitializedObject(type);
        Call(role, "InitializeInventory", Inventory);
        return role;
    }
    internal void InitializeNamedInventory(string name)
    {
        var empty = Array.CreateInstance(T("Timberborn.Goods", "StorableGoodAmount"), 0);
        Call(Inventory, "Initialize", name, 10, empty, false, true, false, GetField(Inventory, "_goodDisallower"));
    }
    internal string NativeInventoryName(string role) => (string)T(role == "Manufactory" ? "Timberborn.Workshops" : "Timberborn.GoodStackSystem",
        role == "Manufactory" ? "ManufactoryInventoryInitializer" : "GoodStackInventoryInitializer")
        .GetField("InventoryComponentName", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
    internal int Physical => (int)Call(Inventory, "AmountInStock", "Log")!;
    internal int Unreserved => (int)Call(Inventory, "UnreservedAmountInStock", "Log")!;
    internal int Consumption => (int)Call(Balance, "GetConsumption", "Log")!;
    internal int Production => (int)Call(Balance, "GetProduction", "Log")!;
    internal int Listed(string method) => ((IEnumerable)Call(Inventory, method)!).Cast<object>().Sum(amount => (int)Stock.Get(amount, "Amount")!);
    internal void On(string name, Action callback)
    {
        var info = Inventory.GetType().GetEvent(name)!;
        var parameters = info.EventHandlerType!.GetMethod("Invoke")!.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
        info.AddEventHandler(Inventory, Expression.Lambda(info.EventHandlerType,
            Expression.Invoke(Expression.Constant(callback)), parameters).Compile());
    }
    internal void Give() => Call(Inventory, "GiveExistingIgnoringCapacity", Stock.Amount);
    internal int ConsumeOne()
    {
        int removed = Math.Min(1, Unreserved);
        if (removed > 0) Call(Inventory, "TakeConsumed", Stock.Amount);
        return removed; // No receipt if any native callback threw.
    }
    internal object? Call(object owner, string method, params object?[] args) => Stock.Call(owner, method, args);
    internal object? Property(object owner, string name) => Stock.Get(owner, name);
    internal static object GetField(object owner, string name) => owner.GetType().GetField(name, Flags)!.GetValue(owner)!;
    internal static void Set(object owner, string name, object? value) => owner.GetType().GetField(name, Flags)!.SetValue(owner, value);
    internal const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    public void Dispose() => Stock.Dispose();
}
