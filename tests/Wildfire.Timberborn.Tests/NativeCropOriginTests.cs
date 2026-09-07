using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeCropOriginTests
{
    [Fact]
    public void WholeYieldRemovalUsesActualNativeZeroWithoutHarvestCallbackAndRepeatsAsZero()
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        var type = native.LoadNative("Timberborn.Yielding").GetType("Timberborn.Yielding.Yielder")!;
        var goods = native.LoadNative("Timberborn.Goods").GetType("Timberborn.Goods.GoodAmount")!;
        var yielder = RuntimeHelpers.GetUninitializedObject(type);
        Field(type, "_yield").SetValue(yielder, Activator.CreateInstance(goods, "Carrot", 5));
        Field(type, "_initialYield").SetValue(yielder, Activator.CreateInstance(goods, "Carrot", 5));
        Field(type.BaseType!, "<Enabled>k__BackingField").SetValue(yielder, true);
        int harvestCallbacks = 0;
        EventHandler callback = (_, _) => harvestCallbacks++;
        type.GetEvent("YieldDecreased")!.AddEventHandler(yielder, callback);
        var remove = mod.GetType("Wildfire.Timberborn.Visuals.TimberbornTextureCropBurnConsequenceApi")!
            .GetMethod("RemoveWholeYield", BindingFlags.Static | BindingFlags.NonPublic)!;
        Assert.Equal(5, remove.Invoke(null, [yielder]));
        Assert.Equal(0, remove.Invoke(null, [yielder]));
        Assert.Equal(0, harvestCallbacks);
        var remaining = type.GetProperty("Yield")!.GetValue(yielder)!;
        Assert.Equal(0, goods.GetProperty("Amount")!.GetValue(remaining));
        Assert.Equal("Carrot", goods.GetProperty("GoodId")!.GetValue(remaining));
    }

    [Fact]
    public void NativePartialDecreaseReallyInvokesHarvestCallbackAfterChangingQuantity()
    {
        using var native = new NativeManagedTestContext();
        var type = native.LoadNative("Timberborn.Yielding").GetType("Timberborn.Yielding.Yielder")!;
        var goods = native.LoadNative("Timberborn.Goods").GetType("Timberborn.Goods.GoodAmount")!;
        var yielder = RuntimeHelpers.GetUninitializedObject(type);
        Field(type, "_yield").SetValue(yielder, Activator.CreateInstance(goods, "Carrot", 5));
        Field(type.BaseType!, "<Enabled>k__BackingField").SetValue(yielder, true);
        int seen = -1;
        EventHandler callback = (_, _) => seen = (int)goods.GetProperty("Amount")!.GetValue(type.GetProperty("Yield")!.GetValue(yielder))!;
        type.GetEvent("YieldDecreased")!.AddEventHandler(yielder, callback);
        type.GetMethod("DecreaseYield")!.Invoke(yielder, [Activator.CreateInstance(goods, "Carrot", 2)]);
        Assert.Equal(3, seen); // Not a safe silent partial-loss API; installed Gatherable subscribes harvest behavior.
    }

    [Fact]
    public void NativeDeathReceiptCountsOnlyFirstMutationAndRepeatedDeathIsAlreadySatisfied()
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        var livingType = native.LoadNative("Timberborn.NaturalResourcesLifecycle")
            .GetType("Timberborn.NaturalResourcesLifecycle.LivingNaturalResource")!;
        var living = RuntimeHelpers.GetUninitializedObject(livingType);
        int callbacks = 0;
        livingType.GetEvent("Died")!.AddEventHandler(living, (EventHandler)((_, _) => callbacks++));
        var apply = mod.GetType("Wildfire.Timberborn.Visuals.TimberbornTextureCropBurnConsequenceApi")!
            .GetMethod("ApplyNativeDeath", BindingFlags.Static | BindingFlags.NonPublic)!;
        var first = apply.Invoke(null, [living])!;
        var repeated = apply.Invoke(null, [living])!;
        Assert.Equal("Applied", first.GetType().GetProperty("Status")!.GetValue(first)!.ToString());
        Assert.Equal(true, first.GetType().GetProperty("KilledCrop")!.GetValue(first));
        Assert.Equal("AlreadySatisfied", repeated.GetType().GetProperty("Status")!.GetValue(repeated)!.ToString());
        Assert.Equal(false, repeated.GetType().GetProperty("KilledCrop")!.GetValue(repeated));
        Assert.Equal(1, callbacks);
    }

    [Fact]
    public void SharedLegacyGuardPoisonsOnActualNativeDeathCallbackAndPreventsReplay()
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        var livingType = native.LoadNative("Timberborn.NaturalResourcesLifecycle")
            .GetType("Timberborn.NaturalResourcesLifecycle.LivingNaturalResource")!;
        var living = RuntimeHelpers.GetUninitializedObject(livingType);
        int calls = 0;
        var cause = new ApplicationException("Died observer failed");
        EventHandler died = (_, _) => { calls++; throw cause; };
        livingType.GetEvent("Died")!.AddEventHandler(living, died);
        var apply = mod.GetType("Wildfire.Timberborn.Visuals.TimberbornTextureCropBurnConsequenceApi")!
            .GetMethod("ApplyNativeDeath", BindingFlags.Static | BindingFlags.NonPublic)!;
        var guard = new NativeResourceTransaction();
        var api = new CallbackCropApi(() => apply.Invoke(null, [living]));
        var wrapped = new TimberbornLegacyCropMutationGuard(guard, api);
        var error = Assert.Throws<TargetInvocationException>(() => wrapped.ApplyConsequence(default));
        Assert.Same(cause, error.InnerException);
        Assert.True(guard.IsIndeterminate);
        Assert.Equal(true, livingType.GetProperty("IsDead")!.GetValue(living));
        Assert.Throws<InvalidOperationException>(() => wrapped.ApplyConsequence(default));
        Assert.Throws<InvalidOperationException>(guard.ThrowIfSaveUnsafe);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ExactNativeCropLookupRejectsMissingDeletedAndNotInitializedWithoutSceneFallback()
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        var entities = native.LoadNative("Timberborn.EntitySystem");
        var registryType = entities.GetType("Timberborn.EntitySystem.EntityRegistry")!;
        var entityType = entities.GetType("Timberborn.EntitySystem.EntityComponent")!;
        var registry = Activator.CreateInstance(registryType)!;
        var apiType = mod.GetType("Wildfire.Timberborn.Visuals.TimberbornTextureCropBurnConsequenceApi")!;
        var api = RuntimeHelpers.GetUninitializedObject(apiType);
        Field(apiType, "_entities").SetValue(api, registry);
        var id = Guid.NewGuid();
        bool IsLive() => (bool)apiType.GetMethod("IsLive")!.Invoke(api, [id])!;
        Assert.False(IsLive());
        var entity = RuntimeHelpers.GetUninitializedObject(entityType);
        Field(entityType, "<EntityId>k__BackingField").SetValue(entity, id);
        ((IDictionary)Field(registryType, "_entities").GetValue(registry)!).Add(id, entity);
        Assert.False(IsLive());
        var state = Field(entityType, "_entityState");
        state.SetValue(entity, Enum.Parse(state.FieldType, "Deleted"));
        Assert.False(IsLive());
        // Actual native lookup/state getters. Positive Unity liveness remains live QA, not simulated here.
    }

    [Fact]
    public void LegacyTreeGuardUsesSameFailStopSemanticsWithoutAnIndependentLedger()
    {
        var guard = new NativeResourceTransaction();
        int calls = 0;
        var cause = new ApplicationException("tree callback");
        var tree = new TimberbornLegacyTreeMutationGuard(guard, new CallbackTreeApi(() => { calls++; throw cause; }));
        Assert.Same(cause, Assert.Throws<ApplicationException>(() => tree.ApplyConsequence(default)));
        Assert.Throws<InvalidOperationException>(() => tree.ApplyConsequence(default));
        Assert.True(guard.IsIndeterminate);
        Assert.Equal(1, calls);
    }

    private sealed class CallbackCropApi(Action call) : ITimberbornCropBurnConsequenceApi
    {
        public TimberbornCropBurnConsequenceResult ApplyConsequence(TimberbornCropBurnConsequence value)
        { call(); return new(TimberbornCropBurnConsequenceStatus.Applied); }
    }
    private sealed class CallbackTreeApi(Action call) : ITimberbornTreeBurnConsequenceApi
    {
        public TimberbornTreeBurnConsequenceResult ApplyConsequence(TimberbornTreeBurnConsequence value)
        { call(); return new(TimberbornTreeBurnConsequenceStatus.Applied); }
    }
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
