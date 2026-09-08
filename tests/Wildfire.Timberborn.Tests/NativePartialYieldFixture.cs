using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

internal sealed class NativePartialYieldFixture : IDisposable
{
    private readonly NativeManagedTestContext _native = new();
    internal readonly Type YielderType, AmountType, Helper;
    internal readonly FieldInfo EnabledField;
    internal readonly object Yielder, Reservable;
    internal object LastNativeLoader = null!, LastNativeSaver = null!;
    internal NativePartialYieldFixture()
    {
        Helper = _native.LoadMod().GetType("Wildfire.Timberborn.Compatibility.TimberbornPartialYieldLoss")!;
        YielderType = Type("Timberborn.Yielding", "Timberborn.Yielding.Yielder");
        AmountType = Type("Timberborn.Goods", "Timberborn.Goods.GoodAmount");
        EnabledField = YielderType.BaseType!.GetField("<Enabled>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Yielder = NewYielder();
        Reservable = YielderType.GetProperty("Reservable")!.GetValue(Yielder)!;
    }
    internal Type ModType(string name) => _native.LoadMod().GetType(name)!;
    internal Type Type(string assembly, string name) => _native.LoadNative(assembly).GetType(name)!;
    internal object NewYielder(string name = "Gatherable", string good = "Carrot")
    {
        var yielder = RuntimeHelpers.GetUninitializedObject(YielderType);
        var amountSpecType = Type("Timberborn.Goods", "Timberborn.Goods.GoodAmountSpec");
        var amountSpec = Activator.CreateInstance(amountSpecType)!;
        amountSpecType.GetProperty("Id")!.SetValue(amountSpec, good);
        amountSpecType.GetProperty("Amount")!.SetValue(amountSpec, 5);
        var specType = Type("Timberborn.Yielding", "Timberborn.Yielding.YielderSpec");
        var spec = Activator.CreateInstance(specType)!;
        specType.GetProperty("YielderComponentName")!.SetValue(spec, name);
        specType.GetProperty("Yield")!.SetValue(spec, amountSpec);
        YielderType.GetMethod("Initialize")!.Invoke(yielder, [spec, Activator.CreateInstance(AmountType, good, 5), null, "Gather"]);
        Set(yielder, "<Reservable>k__BackingField", Activator.CreateInstance(Type("Timberborn.ReservableSystem", "Timberborn.ReservableSystem.Reservable"))!);
        EnabledField.SetValue(yielder, true);
        return yielder;
    }
    internal object Apply(int requested, string name = "Gatherable", string good = "Carrot") => Helper
        .GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [Yielder, name, good, requested])!;
    internal string Status(object value) => value.GetType().GetProperty("Status")!.GetValue(value)!.ToString()!;
    internal int Removed(object value) => (int)value.GetType().GetProperty("Removed")!.GetValue(value)!;
    internal int Current => Quantity(Yielder);
    internal int Initial => InitialQuantity(Yielder);
    internal int Quantity(object yielder) => AmountQuantity(YielderType.GetProperty("Yield")!.GetValue(yielder)!);
    internal int RawQuantity(object yielder) => AmountQuantity(Get(yielder, "_yield"));
    internal int InitialQuantity(object yielder) => AmountQuantity(Get(yielder, "_initialYield"));
    internal int AmountQuantity(object amount) => (int)AmountType.GetProperty("Amount")!.GetValue(amount)!;
    internal object SaveLoad()
    {
        var goodSpecType = Type("Timberborn.Goods", "Timberborn.Goods.GoodSpec");
        var spec = Activator.CreateInstance(goodSpecType)!;
        goodSpecType.GetProperty("Id")!.SetValue(spec, "Carrot");
        var service = RuntimeHelpers.GetUninitializedObject(Type("Timberborn.Goods", "Timberborn.Goods.GoodService"));
        var dictionary = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), goodSpecType))!;
        dictionary.Add("Carrot", spec);
        Set(service, "_goodSpecsById", dictionary);
        var goodSerializer = Activator.CreateInstance(Type("Timberborn.Goods", "Timberborn.Goods.SerializedGoodValueSerializer"), service)!;
        var amountSerializer = Activator.CreateInstance(Type("Timberborn.Goods", "Timberborn.Goods.GoodAmountSerializer"), goodSerializer)!;
        Set(Yielder, "_goodAmountSerializer", amountSerializer);
        var serialized = Activator.CreateInstance(Type("Timberborn.WorldSerialization", "Timberborn.WorldSerialization.SerializedEntity"),
            Guid.NewGuid(), "Fixture.Crop")!;
        var saver = Activator.CreateInstance(Type("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.EntitySaver"), serialized)!;
        LastNativeSaver = saver;
        YielderType.GetMethod("Save")!.Invoke(Yielder, [saver]);
        var loaded = NewYielder();
        Set(loaded, "_goodAmountSerializer", amountSerializer);
        var loader = Activator.CreateInstance(Type("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.EntityLoader"), serialized)!;
        LastNativeLoader = loader;
        YielderType.GetMethod("Load")!.Invoke(loaded, [loader]);
        return loaded;
    }
    internal static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    internal static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    public void Dispose() => _native.Dispose();
}
