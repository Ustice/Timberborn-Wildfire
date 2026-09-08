using System.Reflection;
using System.Runtime.CompilerServices;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

public sealed class NativePartialYieldTests
{
    [Fact]
    public void NativeFiveToThreePersistsAndNextCycleRetainsOriginalCapacity()
    {
        using var f = new NativePartialYieldFixture();
        int events = 0;
        f.YielderType.GetEvent("YieldAdded")!.AddEventHandler(f.Yielder, (EventHandler)((_, _) => events++));
        f.YielderType.GetEvent("YieldDecreased")!.AddEventHandler(f.Yielder, (EventHandler)((_, _) => events++));
        var result = f.Apply(2);
        Assert.Equal("Applied", f.Status(result)); Assert.Equal(2, f.Removed(result));
        Assert.Equal(3, f.Current); Assert.Equal(5, f.Initial); Assert.Equal(0, events);
        var loaded = f.SaveLoad();
        Assert.Equal(3, f.Quantity(loaded));
        Assert.Equal(5, f.InitialQuantity(loaded));
        f.YielderType.GetMethod("ResetYield")!.Invoke(loaded, null);
        Assert.Equal(5, f.Quantity(loaded));
    }

    [Theory]
    [InlineData(true, true, 2, "DeferredReserved")]
    [InlineData(false, false, 2, "DeferredInactive")]
    [InlineData(false, true, 5, "DepletionRequired")]
    [InlineData(false, true, 6, "DepletionRequired")]
    public void DeferredRequestsPreserveQuantityCapacityAndReservation(bool reserved, bool enabled, int request, string expected)
    {
        using var f = new NativePartialYieldFixture();
        if (reserved) f.Reservable.GetType().GetMethod("Reserve")!.Invoke(f.Reservable, null);
        f.EnabledField.SetValue(f.Yielder, enabled);
        var result = f.Apply(request);
        Assert.Equal(expected, f.Status(result)); Assert.Equal(0, f.Removed(result));
        Assert.Equal(5, f.RawQuantity(f.Yielder)); Assert.Equal(5, f.Initial);
        Assert.Equal(enabled, f.YielderType.GetProperty("Enabled")!.GetValue(f.Yielder));
        Assert.Equal(reserved, f.Reservable.GetType().GetProperty("Reserved")!.GetValue(f.Reservable));
    }

    [Theory]
    [InlineData("Cuttable", "Carrot")]
    [InlineData("Gatherable", "Log")]
    public void MismatchedNamedResourceRejectsBeforeAnyMutation(string name, string good)
    {
        using var f = new NativePartialYieldFixture();
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => f.Apply(2, name, good)).InnerException);
        Assert.Equal(5, f.Current);
    }

    [Fact]
    public void NativeVersionMismatchRefusesCompatibilityInsteadOfGuessingLayout()
    {
        using var f = new NativePartialYieldFixture();
        var error = Assert.Throws<TargetInvocationException>(() => f.Helper.GetMethod("RequireFingerprint", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, ["Timberborn.Yielding.dll", "reviewed", "different"]));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Equal(5, f.Current);
    }

    [Fact]
    public void MultipleNativeYieldersSelectExactNameAndRejectAmbiguity()
    {
        using var f = new NativePartialYieldFixture();
        var wood = f.NewYielder("Cuttable", "Log");
        var array = Array.CreateInstance(f.YielderType, 2);
        array.SetValue(wood, 0); array.SetValue(f.Yielder, 1);
        var select = f.Helper.GetMethod("SelectNamed", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.Same(f.Yielder, select.Invoke(null, [array, "Gatherable"]));
        f.Apply(2);
        Assert.Equal(5, f.RawQuantity(wood)); Assert.Equal(3, f.Current);
        array.SetValue(f.NewYielder(), 0);
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => select.Invoke(null, [array, "Gatherable"])).InnerException);
    }

    [Fact]
    public void NativeCropSelectorSupportsCuttableAnnualsAndExactGatherableResource()
    {
        using var f = new NativePartialYieldFixture();
        var annual = f.NewYielder("Cuttable", "Carrot");
        var cuttableType = f.Type("Timberborn.Cutting", "Timberborn.Cutting.Cuttable");
        var cuttable = RuntimeHelpers.GetUninitializedObject(cuttableType);
        NativePartialYieldFixture.Set(cuttable, "<Yielder>k__BackingField", annual);
        var specType = f.Type("Timberborn.Cutting", "Timberborn.Cutting.CuttableSpec");
        var spec = Activator.CreateInstance(specType)!;
        specType.GetProperty("Yielder")!.SetValue(spec, f.YielderType.GetProperty("YielderSpec")!.GetValue(annual));
        NativePartialYieldFixture.Set(cuttable, "_cuttableSpec", spec);
        var selector = f.ModType("Wildfire.Timberborn.Visuals.TimberbornTextureCropBurnConsequenceApi")
            .GetMethod("SelectCropYield", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.Same(annual, selector.Invoke(null, [null, cuttable, "Carrot"]));
        Assert.Null(selector.Invoke(null, [null, cuttable, "Blueberry"]));
        var gatherable = RuntimeHelpers.GetUninitializedObject(f.Type("Timberborn.Gathering", "Timberborn.Gathering.Gatherable"));
        NativePartialYieldFixture.Set(gatherable, "<Yielder>k__BackingField", f.Yielder);
        var gatherSpecType = f.Type("Timberborn.Gathering", "Timberborn.Gathering.GatherableSpec");
        var gatherSpec = Activator.CreateInstance(gatherSpecType)!;
        gatherSpecType.GetProperty("Yielder")!.SetValue(gatherSpec, f.YielderType.GetProperty("YielderSpec")!.GetValue(f.Yielder));
        NativePartialYieldFixture.Set(gatherable, "_gatherableSpec", gatherSpec);
        Assert.Same(f.Yielder, selector.Invoke(null, [gatherable, null, "Carrot"]));
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => selector.Invoke(null, [gatherable, cuttable, "Carrot"])).InnerException);
    }

    [Fact]
    public void FailureAfterActualFieldWritePoisonsSharedGuardWithoutRefundOrReplay()
    {
        using var f = new NativePartialYieldFixture();
        var guard = new NativeResourceTransaction();
        var cause = new ApplicationException("caller failed after native write");
        Assert.Same(cause, Assert.Throws<ApplicationException>(() => guard.TransferInventory(() => { f.Apply(2); throw cause; })));
        Assert.Equal(3, f.Current); Assert.True(guard.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(() => guard.TransferInventory(() => f.Apply(1)));
        Assert.Throws<InvalidOperationException>(guard.ThrowIfSaveUnsafe);
        Assert.Equal(3, f.Current);
        // Deliberate caller fault; this helper emits no native quantity event and claims none.
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExactAdapterRejectsMissingIdentityAndSkipsMissingOriginalWithoutCellFallback(bool tree)
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        string family = tree ? "Tree" : "Crop";
        var apiType = mod.GetType($"Wildfire.Timberborn.Visuals.TimberbornTexture{family}BurnConsequenceApi")!;
        var consequenceType = mod.GetType($"Wildfire.Timberborn.Consequences.Timberborn{family}BurnConsequence")!;
        var api = RuntimeHelpers.GetUninitializedObject(apiType);
        var apply = apiType.GetMethod("ApplyPositivePartialYieldLoss", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var invalid = Assert.Throws<TargetInvocationException>(() => apply.Invoke(api, [Activator.CreateInstance(consequenceType)]));
        Assert.IsType<InvalidOperationException>(invalid.InnerException);
        var registryType = native.LoadNative("Timberborn.EntitySystem").GetType("Timberborn.EntitySystem.EntityRegistry")!;
        NativePartialYieldFixture.Set(api, "_entities", Activator.CreateInstance(registryType)!);
        var id = Guid.NewGuid();
        var keyType = mod.GetType("Wildfire.Timberborn.Consequences.TimberbornBurnDamageTargetKey")!;
        var key = Activator.CreateInstance(keyType, TimberbornBurnDamageIdentity.ForEntity(id,
            tree ? NativeBurnTargetFamily.Tree : NativeBurnTargetFamily.Crop))!;
        var action = Enum.Parse(mod.GetType($"Wildfire.Timberborn.Consequences.Timberborn{family}BurnConsequenceKind")!, "ReduceYield");
        var args = tree ? new object[] { key, "Tree.Pine", action, "Log", 2, 3, 1u, 0, 2, 2, 10, id }
            : new object[] { key, "Crop.Carrot", Enum.Parse(mod.GetType("Wildfire.Timberborn.Consequences.TimberbornBurnDamageTargetKind")!, "Crop"),
                action, "Carrot", 2, 3, 1u, 0, 2, 2, 10, false, Array.Empty<string>(), Array.Empty<string>(), id };
        var result = apply.Invoke(api, [Activator.CreateInstance(consequenceType, args)])!;
        Assert.Equal("NotLive", result.GetType().GetProperty("Status")!.GetValue(result)!.ToString());
    }

    [Fact]
    public void LaterNativeHarvestMovesOnlyRemainingThreeAndRunsRealGatherableHandler()
    {
        using var f = new NativePartialYieldFixture();
        f.Apply(2);
        var gatherableType = f.Type("Timberborn.Gathering", "Timberborn.Gathering.Gatherable");
        var gatherable = RuntimeHelpers.GetUninitializedObject(gatherableType);
        NativePartialYieldFixture.Set(gatherable, "<Yielder>k__BackingField", f.Yielder);
        var stackType = f.Type("Timberborn.GoodStackSystem", "Timberborn.GoodStackSystem.GoodStack");
        NativePartialYieldFixture.Set(gatherable, "_goodStack", RuntimeHelpers.GetUninitializedObject(stackType)); // Empty disabled stack.
        var harvestHandler = gatherableType.GetMethod("<PostInitializeEntity>b__20_0", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(EventHandler), gatherable);
        f.YielderType.GetEvent("YieldDecreased")!.AddEventHandler(f.Yielder, harvestHandler);
        int gathered = 0;
        gatherableType.GetEvent("Gathered")!.AddEventHandler(gatherable, (EventHandler)((_, _) => gathered++));
        var carrierType = f.Type("Timberborn.Carrying", "Timberborn.Carrying.GoodCarrier");
        var carrier = RuntimeHelpers.GetUninitializedObject(carrierType);
        var amount = f.YielderType.GetProperty("Yield")!.GetValue(f.Yielder)!;
        var carried = Activator.CreateInstance(f.Type("Timberborn.Carrying", "Timberborn.Carrying.CarriedGood"), amount,
            Enum.Parse(f.Type("Timberborn.Carrying", "Timberborn.Carrying.CarriedGoodType"), "Uncountable"))!;
        carrierType.GetMethod("PutGoodsInHands")!.Invoke(carrier, [carried]);
        f.YielderType.GetMethod("DecreaseYield")!.Invoke(f.Yielder, [amount]);
        var actualCarried = carrierType.GetProperty("CarriedGood")!.GetValue(carrier)!;
        Assert.Equal(3, f.AmountQuantity(actualCarried.GetType().GetProperty("GoodAmount")!.GetValue(actualCarried)!));
        Assert.Equal(0, f.Current); Assert.Equal(1, gathered);
        Assert.Equal(false, stackType.GetProperty("Enabled")!.GetValue(NativePartialYieldFixture.Get(gatherable, "_goodStack")));
        // Real native carry/decrement/Gatherable callbacks in the native completion order. Navigation,
        // reservable component-cache wiring and full YielderRemover behavior remain game QA.
    }
}
