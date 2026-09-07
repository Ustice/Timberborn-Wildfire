using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeGatherableYieldLoadTests
{
    [Fact]
    public void NativeRipeGrowerLoadRefillsButPreInitializeRestoresSameSavedYieldBeforeLaterConsumers()
    {
        using var f = new Fixture();
        f.Native.Apply(2);
        var loaded = f.Native.SaveLoad();
        var savedGrower = f.Grower(f.Native.Yielder);
        f.Call(savedGrower, "FastForwardGrowth", 1f);
        f.Call(savedGrower, "Save", f.Native.LastNativeSaver);
        int added = 0;
        f.Native.YielderType.GetEvent("YieldAdded")!.AddEventHandler(loaded, (EventHandler)((_, _) => added++));
        var grower = f.Grower(loaded);
        var correction = f.Correction(loaded);
        f.Call(correction, "Load", f.Native.LastNativeLoader);
        f.Call(grower, "Load", f.Native.LastNativeLoader);
        Assert.Equal(5, f.Native.RawQuantity(loaded)); // The installed native bug, not a fake grower.
        Assert.Equal(1, added);
        f.Call(correction, "PreInitializeEntity");
        Assert.Equal(3, f.Native.RawQuantity(loaded));
        Assert.Equal(5, f.Native.InitialQuantity(loaded));
        Assert.Equal(1f, grower.GetType().GetProperty("GrowthProgress")!.GetValue(grower));
        Assert.Equal(1, added); // Native Yielder.Load re-read has no additional reset/event.
        Assert.Null(f.Field(correction, "_loader"));
        f.Call(loaded, "DecreaseYield", Activator.CreateInstance(f.Native.AmountType, "Carrot", 1)!);
        f.Call(correction, "PreInitializeEntity");
        Assert.Equal(2, f.Native.RawQuantity(loaded)); // No later reapplication of saved3.
    }

    [Fact]
    public void NewEntityAndMissingSavedYielderKeepNativeInitialization()
    {
        using var f = new Fixture();
        var correction = f.Correction(f.Native.Yielder);
        f.Call(correction, "PreInitializeEntity");
        Assert.Equal(5, f.Native.Current);
        var serialized = Activator.CreateInstance(f.Native.Type("Timberborn.WorldSerialization", "Timberborn.WorldSerialization.SerializedEntity"), Guid.NewGuid(), "Fixture.New")!;
        var loader = Activator.CreateInstance(f.Native.Type("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.EntityLoader"), serialized)!;
        f.Call(correction, "Load", loader);
        f.Call(correction, "PreInitializeEntity");
        Assert.Equal(5, f.Native.Current);
        Assert.Null(f.Field(correction, "_loader"));
    }

    [Fact]
    public void RestorePreservesNativeReservationAndDisabledState()
    {
        using var f = new Fixture();
        f.Native.Apply(2);
        var loaded = f.Native.SaveLoad();
        var reservable = f.Native.YielderType.GetProperty("Reservable")!.GetValue(loaded)!;
        f.Call(reservable, "Reserve");
        f.Native.EnabledField.SetValue(loaded, false);
        NativePartialYieldFixture.Set(loaded, "_yield", Activator.CreateInstance(f.Native.AmountType, "Carrot", 5)!);
        var correction = f.Correction(loaded);
        f.Call(correction, "Load", f.Native.LastNativeLoader);
        f.Call(correction, "PreInitializeEntity");
        Assert.Equal(3, f.Native.RawQuantity(loaded));
        Assert.Equal(0, f.Native.Quantity(loaded));
        Assert.True((bool)reservable.GetType().GetProperty("Reserved")!.GetValue(reservable)!);
    }

    [Fact]
    public void WrongNamedReferenceAbortsAndClearsPendingLoaderWithoutRetry()
    {
        using var f = new Fixture();
        f.Native.Apply(2);
        var loaded = f.Native.SaveLoad();
        var correction = f.Correction(loaded);
        var gatherable = f.Field(correction, "_gatherable")!;
        NativePartialYieldFixture.Set(gatherable, "<Yielder>k__BackingField", f.Native.NewYielder("Cuttable"));
        f.Call(correction, "Load", f.Native.LastNativeLoader);
        var error = Assert.Throws<TargetInvocationException>(() => f.Call(correction, "PreInitializeEntity"));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Null(f.Field(correction, "_loader"));
        f.Call(correction, "PreInitializeEntity");
        Assert.Equal(3, f.Native.RawQuantity(loaded));
    }

    [Fact]
    public void NativeResetObserverFailureIsNotSwallowedOrConvertedIntoSuccessfulLoad()
    {
        using var f = new Fixture();
        var crop = f.Native.Yielder;
        var cause = new InvalidOperationException("observer failed after reset");
        f.Native.YielderType.GetEvent("YieldAdded")!.AddEventHandler(crop, (EventHandler)((_, _) => throw cause));
        var grower = f.Grower(crop);
        var error = Assert.Throws<TargetInvocationException>(() => f.Call(grower, "FastForwardGrowth", 1f));
        Assert.Same(cause, error.InnerException?.InnerException); // Reset invoked by fixture's Action through reflection.
        Assert.Equal(5, f.Native.Current);
        Assert.Equal(1f, grower.GetType().GetProperty("GrowthProgress")!.GetValue(grower));
    }

    [Fact]
    public void NativeSavedDataReadFailurePropagatesAndReleasesOriginalLoader()
    {
        using var f = new Fixture();
        f.Native.Apply(2);
        var loaded = f.Native.SaveLoad();
        var correction = f.Correction(loaded);
        f.Call(correction, "Load", f.Native.LastNativeLoader);
        loaded.GetType().GetField("_goodAmountSerializer", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(loaded, null);
        var error = Assert.Throws<TargetInvocationException>(() => f.Call(correction, "PreInitializeEntity"));
        Assert.IsType<NullReferenceException>(error.InnerException);
        Assert.Null(f.Field(correction, "_loader"));
        f.Call(correction, "PreInitializeEntity"); // No retained loader to retry after the native failure.
        Assert.Equal(3, f.Native.RawQuantity(loaded));
    }

    [Fact]
    public void ActualNativeTemplateModulesPlaceNamedYielderBeforeGrowerAndIncludeCorrection()
    {
        using var f = new Fixture();
        var config = f.Native.Type("Timberborn.Gathering", "Timberborn.Gathering.GatheringConfigurator");
        var gathering = config.GetMethod("ProvideTemplateModule", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        var provider = RuntimeHelpers.GetUninitializedObject(f.Native.Type("Timberborn.Yielding", "Timberborn.Yielding.YieldingConfigurator+TemplateModuleProvider"));
        NativePartialYieldFixture.Set(provider, "_yielderInitializer", RuntimeHelpers.GetUninitializedObject(f.Native.Type("Timberborn.Yielding", "Timberborn.Yielding.YielderInitializer")));
        var yielding = provider.GetType().GetMethod("Get")!.Invoke(provider, null)!;
        var correctionProvider = Activator.CreateInstance(f.Native.ModType("Wildfire.Timberborn.Compatibility.TimberbornGatherableYieldLoadConfigurator+ModuleProvider"), true)!;
        var correction = correctionProvider.GetType().GetMethod("Get")!.Invoke(correctionProvider, null)!;
        var modules = Array.CreateInstance(f.Native.Type("Timberborn.TemplateInstantiation", "Timberborn.TemplateInstantiation.TemplateModule"), 3);
        modules.SetValue(gathering, 0); modules.SetValue(yielding, 1); modules.SetValue(correction, 2);
        var factory = Activator.CreateInstance(f.Native.Type("Timberborn.TemplateInstantiation", "Timberborn.TemplateInstantiation.TemplateInstantiatorProvider"), null, null, modules)!;
        var instantiator = factory.GetType().GetMethod("Get")!.Invoke(factory, null)!;
        var spec = f.GatherableSpec(f.Native.Yielder);
        var specs = Array.CreateInstance(f.Native.Type("Timberborn.BlueprintSystem", "Timberborn.BlueprintSystem.ComponentSpec"), 1); specs.SetValue(spec, 0);
        var blueprintType = f.Native.Type("Timberborn.BlueprintSystem", "Timberborn.BlueprintSystem.Blueprint");
        var children = typeof(System.Collections.Immutable.ImmutableArray<>).MakeGenericType(blueprintType).GetField("Empty")!.GetValue(null)!;
        var blueprint = Activator.CreateInstance(blueprintType, "Fixture.Gatherable", specs, children)!;
        object?[] args = [blueprint, null, null];
        instantiator.GetType().GetMethod("GetInstanceComponents", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(instantiator, args);
        var types = ((IEnumerable)args[2]!).Cast<Type>().ToArray();
        int yielder = Array.FindIndex(types, t => t.Name == "Yielder");
        int grower = Array.FindIndex(types, t => t.Name == "GatherableYieldGrower");
        Assert.True(yielder >= 0 && grower > yielder);
        Assert.Single(types, t => t.Name == "TimberbornGatherableYieldLoadCorrection");
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly NativePartialYieldFixture Native = new();
        internal object? Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value);
        internal void Call(object value, string name, params object?[] args) => value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(value, args);
        internal object Grower(object yielder)
        {
            var triggerType = Native.Type("Timberborn.TimeSystem", "Timberborn.TimeSystem.TimeTrigger");
            var trigger = Activator.CreateInstance(triggerType, null, null, (Action)(() => Call(yielder, "ResetYield")), 5f)!;
            var grower = RuntimeHelpers.GetUninitializedObject(Native.Type("Timberborn.Gathering", "Timberborn.Gathering.GatherableYieldGrower"));
            NativePartialYieldFixture.Set(grower, "_timeTrigger", trigger);
            return grower;
        }
        internal object GatherableSpec(object yielder)
        {
            var specType = Native.Type("Timberborn.Gathering", "Timberborn.Gathering.GatherableSpec");
            var spec = Activator.CreateInstance(specType)!;
            specType.GetProperty("Yielder")!.SetValue(spec, Native.YielderType.GetProperty("YielderSpec")!.GetValue(yielder));
            return spec;
        }
        internal object Correction(object yielder)
        {
            var gatherable = RuntimeHelpers.GetUninitializedObject(Native.Type("Timberborn.Gathering", "Timberborn.Gathering.Gatherable"));
            NativePartialYieldFixture.Set(gatherable, "_gatherableSpec", GatherableSpec(yielder));
            NativePartialYieldFixture.Set(gatherable, "<Yielder>k__BackingField", yielder);
            var correction = Activator.CreateInstance(Native.ModType("Wildfire.Timberborn.Compatibility.TimberbornGatherableYieldLoadCorrection"))!;
            NativePartialYieldFixture.Set(correction, "_gatherable", gatherable);
            return correction;
        }
        public void Dispose() => Native.Dispose();
    }
}
