using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

// Native managed resource-boundary experiment, not production shoreline/actor admission.
internal sealed class NativeShorelineWaterFixture : IDisposable
{
    private readonly NativeManagedTestContext _native;
    private readonly bool _ownsNative;
    internal readonly object Changes, InputService, Source;
    internal readonly float BucketVolume;
    internal readonly NativeResourceTransaction Transaction = new();
    internal Type Type(string assembly, string name) => _native.LoadNative(assembly).GetType(name)!;
    internal NativeShorelineWaterFixture(NativeManagedTestContext? native = null)
    {
        _native = native ?? new NativeManagedTestContext();
        _ownsNative = native is null;
        Changes = Activator.CreateInstance(Type("Timberborn.WaterSystem", "Timberborn.WaterSystem.WaterChangeService"))!;
        InputService = Activator.CreateInstance(Type("Timberborn.WaterBuildings", "Timberborn.WaterBuildings.WaterInputService"), Changes)!;
        Source = NewInput();
        var specType = Type("Timberborn.Goods", "Timberborn.Goods.GoodAmountSpec");
        var spec = Activator.CreateInstance(specType)!;
        specType.GetProperty("Id")!.SetValue(spec, "Water");
        specType.GetProperty("Amount")!.SetValue(spec, 1);
        var specs = Array.CreateInstance(specType, 1); specs.SetValue(spec, 0);
        var converter = Type("Timberborn.WaterWorkshops", "Timberborn.WaterWorkshops.WaterGoodToWaterAmountConverter");
        BucketVolume = (float)converter.GetMethod("GetWaterAmount")!.Invoke(null, [specs])!;
    }
    internal object NewInput(bool register = true, int x = 4)
    {
        var waterService = RuntimeHelpers.GetUninitializedObject(Type("Timberborn.WaterSystem", "Timberborn.WaterSystem.WaterService"));
        Set(waterService, "_waterChangeService", Changes);
        var input = Activator.CreateInstance(Type("Timberborn.WaterBuildings", "Timberborn.WaterBuildings.WaterInput"), waterService, null, InputService)!;
        var coordinates = RuntimeHelpers.GetUninitializedObject(Type("Timberborn.WaterBuildings", "Timberborn.WaterBuildings.WaterInputFixedCoordinates"));
        Set(coordinates, "<Coordinates>k__BackingField", Activator.CreateInstance(Type("UnityEngine.CoreModule", "UnityEngine.Vector3Int"), x, 5, 0)!);
        Set(input, "_inputCoordinates", coordinates);
        if (register) Call(input, "OnEnterFinishedState");
        return input;
    }
    internal float Buffer(object input) => (float)Get(input, "_cleanWaterAmount")!;
    internal float Demand(object input) => (float)Call(input, "DemandCleanWaterAmount", BucketVolume)!;
    internal int PendingRequests => ((IList)Get(Changes, "_waterChanges")!).Count;
    internal void SupplyCompletedNativeReceipt(float clean, float dirty = 0)
    {
        // Supplies the completed worker result; this fixture does not run native fluid simulation.
        var dictionary = (IDictionary)Get(Changes, "RemovedWaterUnsafe")!;
        dictionary[Source.GetType().GetProperty("Coordinates")!.GetValue(Source)!] =
            Activator.CreateInstance(Type("Timberborn.WaterSystem", "Timberborn.WaterSystem.WaterAmountChange"), clean, dirty)!;
    }
    internal void CreditNativeInputs() => Call(InputService, "Tick");
    internal void RequireExclusiveInput(object source)
    {
        var inputs = ((IEnumerable)Get(InputService, "_waterInputs")!).Cast<object>().ToArray();
        var coordinate = source.GetType().GetProperty("Coordinates")!.GetValue(source)!;
        if (inputs.Count(value => ReferenceEquals(value, source)) != 1 ||
            inputs.Count(value => value.GetType().GetProperty("Coordinates")!.GetValue(value)!.Equals(coordinate)) != 1)
            throw new InvalidOperationException("Shoreline source has no exclusive native input registration.");
    }
    internal object Bucket()
    {
        var inventory = RuntimeHelpers.GetUninitializedObject(Type("Timberborn.InventorySystem", "Timberborn.InventorySystem.Inventory"));
        var registry = Type("Timberborn.Goods", "Timberborn.Goods.GoodRegistry");
        foreach (var field in new[] { "_storage", "_reservedStock", "_reservedCapacity" }) Set(inventory, field, Activator.CreateInstance(registry)!);
        Set(inventory, "<Capacity>k__BackingField", 1);
        Set(inventory, "_goodDisallower", Activator.CreateInstance(Type("Timberborn.InventorySystem", "Timberborn.InventorySystem.NullGoodDisallower"))!);
        var allowedType = Type("Timberborn.Goods", "Timberborn.Goods.StorableGoodRegistry");
        var allowed = Activator.CreateInstance(allowedType)!;
        var storable = Type("Timberborn.Goods", "Timberborn.Goods.StorableGood").GetMethod("CreateAsTakeable")!.Invoke(null, ["Water"]);
        var amountType = Type("Timberborn.Goods", "Timberborn.Goods.StorableGoodAmount");
        var array = Array.CreateInstance(amountType, 1); array.SetValue(Activator.CreateInstance(amountType, storable, 1), 0);
        Call(allowed, "Add", array); Set(inventory, "_allowedGoods", allowed);
        return inventory;
    }
    internal int Stock(object bucket) => (int)Call(bucket, "AmountInStock", "Water")!;
    internal bool TryFill(object source, object bucket, Action? commitPhase = null)
    {
        Transaction.ThrowIfSaveUnsafe();
        RequireExclusiveInput(source); // No water demand/debit before exact shared-coordinate admission.
        if (Stock(bucket) != 0 || (int)Call(bucket, "UnreservedCapacity", "Water")! < 1) return false;
        float available = Demand(source); // May queue future water; never count that request as cargo.
        if (!float.IsFinite(available) || available < BucketVolume) return false;
        Transaction.TransferInventory(() =>
        {
            RequireExclusiveInput(source);
            // No asynchronous boundary, callback or cached reservation between buffer admission and debit.
            if (Buffer(source) < BucketVolume) throw new InvalidOperationException("Native buffer changed before debit.");
            Call(source, "RemoveCleanWater", BucketVolume);
            Call(bucket, "GiveProduced", Activator.CreateInstance(Type("Timberborn.Goods", "Timberborn.Goods.GoodAmount"), "Water", 1)!);
            commitPhase?.Invoke();
        });
        return true;
    }
    internal object SaveLoadInput(object input)
    {
        var serialized = Activator.CreateInstance(Type("Timberborn.WorldSerialization", "Timberborn.WorldSerialization.SerializedEntity"), Guid.NewGuid(), "Fixture.Intake")!;
        var saver = Activator.CreateInstance(Type("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.EntitySaver"), serialized)!;
        Call(input, "Save", saver);
        var loader = Activator.CreateInstance(Type("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.EntityLoader"), serialized)!;
        var restored = NewInput(false);
        Call(restored, "Load", loader);
        return restored;
    }
    internal void OnInventoryChanged(object bucket, Action action)
    {
        var eventInfo = bucket.GetType().GetEvent("InventoryChanged")!;
        var parameters = eventInfo.EventHandlerType!.GetMethod("Invoke")!.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
        var callback = Expression.Lambda(eventInfo.EventHandlerType, Expression.Invoke(Expression.Constant(action)), parameters).Compile();
        eventInfo.AddEventHandler(bucket, callback);
    }
    internal static object? Call(object value, string method, params object?[] args) => value.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public)!.Invoke(value, args);
    internal static object? Get(object value, string field) => value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value);
    internal static void Set(object value, string field, object data) => value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(value, data);
    public void Dispose() { if (_ownsNative) _native.Dispose(); }
}
