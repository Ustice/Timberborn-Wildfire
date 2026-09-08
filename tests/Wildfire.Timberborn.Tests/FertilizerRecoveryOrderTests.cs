using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class FertilizerRecoveryOrderTests
{
    [Theory]
    [InlineData("CriticalNeederRootBehavior")]
    [InlineData("StrandedRootBehavior")]
    [InlineData("FertilizerRecoveryRoot")]
    public void InstalledSequenceDrivesActualNativeArbitrationBeforeWork(string winner)
    {
        using var inventory = new NativeFertilizerSatchelFixture();
        inventory.Give(inventory.Inventory);
        using var f = new Fixture();
        f.PostInitialize();
        var trace = new List<string>();
        bool Loaded() => (bool)inventory.Mod("FertilizerRecoveryRoot").GetMethod("HasDepositRequest",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [inventory.Inventory])!;
        Assert.Equal(winner, f.Arbitrate(name => name == winner ||
            name == "FertilizerRecoveryRoot" && Loaded() || name == "WorkerRootBehavior", trace));
        Assert.DoesNotContain("WorkerRootBehavior", trace);
        if (winner != "FertilizerRecoveryRoot") Assert.DoesNotContain("FertilizerRecoveryRoot", trace);
        Assert.Equal(1, inventory.Quantity(inventory.Inventory));
    }

    [Fact]
    public void RecoveryDeclineLetsOrdinaryWorkProceedAndNoReturnFlagBypassesUrgentRoots()
    {
        using var f = new Fixture(); f.PostInitialize();
        var trace = new List<string>();
        Assert.Equal("WorkerRootBehavior", f.Arbitrate(name => name == "WorkerRootBehavior", trace));
        Assert.True(trace.IndexOf("FertilizerRecoveryRoot") < trace.IndexOf("WorkerRootBehavior"));
        trace.Clear();
        Assert.Equal("CriticalNeederRootBehavior", f.Arbitrate(name => name is "CriticalNeederRootBehavior" or "FertilizerRecoveryRoot", trace));
        Assert.DoesNotContain("FertilizerRecoveryRoot", trace);
    }

    [Fact]
    public void PostInitializeInsertsAfterUrgentRootsBeforeWorkAndPreservesForeignEntriesWithoutRepeatedMutation()
    {
        using var f = new Fixture();
        var original = f.Roots.Cast<object>().ToArray();
        f.PostInitialize();
        Assert.True(f.Ready);
        Assert.Equal(original, f.Roots.Cast<object>().Where(root => !ReferenceEquals(root, f.Recovery)));
        int recovery = f.Roots.IndexOf(f.Recovery);
        Assert.Equal("Timberborn.WorkSystem.WorkerRootBehavior", f.Roots[recovery + 1]!.GetType().FullName);
        Assert.True(f.Roots.IndexOf(f.Stranded) < recovery);
        Assert.True(f.Roots.IndexOf(f.Critical) < f.Roots.IndexOf(f.Stranded));
        int version = f.Version;
        f.PostInitialize();
        Assert.True(f.Ready);
        Assert.Equal(version, f.Version);
        // Later foreign list changes cannot leave new pickup admission reporting ready.
        f.Roots.Remove(f.Stranded);
        Assert.False(f.Ready);
        Assert.DoesNotContain(f.Roots.Cast<object>(), root => ReferenceEquals(root, f.Stranded));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("reordered")]
    [InlineData("foreignRecovery")]
    public void UnsupportedOrderDoesNotAppendOrRewriteExistingRoots(string kind)
    {
        using var f = new Fixture();
        if (kind == "missing") f.Roots.Remove(f.Critical);
        if (kind == "duplicate") f.Roots.Add(f.Critical);
        if (kind == "reordered") { f.Roots.Remove(f.Stranded); f.Roots.Insert(0, f.Stranded); }
        if (kind == "foreignRecovery") f.Roots.Add(Activator.CreateInstance(f.Recovery.GetType(), new object?[] { null }));
        var before = f.Roots.Cast<object>().ToArray();
        f.PostInitialize();
        Assert.False(f.Ready);
        Assert.NotEmpty((string)f.Call(f.Recovery, "get_UnsupportedReason")!);
        Assert.Equal(before, f.Roots.Cast<object>());
    }

    [Fact]
    public void RealLoadedNativeInventoryRecreatesOnlyDepositRequestAfterSaveWithoutEmployerOrApplicationIntent()
    {
        using var f = new NativeFertilizerSatchelFixture();
        bool Request(object inventory) => (bool)f.Mod("FertilizerRecoveryRoot")
            .GetMethod("HasDepositRequest", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [inventory])!;
        Assert.False(Request(f.Inventory));
        f.Give(f.Inventory);
        Assert.True(Request(f.Inventory));
        var restored = f.SaveLoadInventory();
        f.Call(restored, "Enable");
        Assert.True(Request(restored)); // No Worker, employer or old target was created in this fixture.
        f.Call(restored, "ReserveStock", f.Amount());
        Assert.False(Request(restored));
        f.Call(restored, "UnreserveStock", f.Amount());
        f.Call(restored, "Disable");
        Assert.False(Request(restored));
        f.Call(restored, "Enable");
        f.Transfer(() => Assert.True(f.Move(restored, f.Source)));
        Assert.False(Request(restored));
        Assert.Equal(1, f.Quantity(f.Source));
        Assert.Equal(0, f.Consumption);
        Assert.Equal(0, f.Production);
    }

    [Fact]
    public void NativeRecoveryRequestRejectsMalformedCargoWithoutCreatingAnyReceipt()
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Inventory, 2);
        Assert.Throws<TargetInvocationException>(() => f.Mod("FertilizerRecoveryRoot")
            .GetMethod("HasDepositRequest", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [f.Inventory]));
        Assert.Equal(2, f.Quantity(f.Inventory));
        Assert.False(f.Poisoned); // Read-only invalid request, no native transfer happened.
    }

    internal sealed class Fixture : IDisposable
    {
        private readonly NativeManagedTestContext _native = NativeManagedTestContext.ProxyContracts;
        internal readonly object Manager;
        private readonly object _entity;
        internal readonly IList Roots;
        internal readonly object Recovery, Critical, Stranded;
        internal Fixture(Func<float>? time = null)
        {
            var rootType = _native.LoadNative("Timberborn.BehaviorSystem").GetType("Timberborn.BehaviorSystem.RootBehavior")!;
            Roots = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(rootType))!;
            var managerType = _native.LoadNative("Timberborn.BehaviorSystem").GetType("Timberborn.BehaviorSystem.BehaviorManager")!;
            var manager = RuntimeHelpers.GetUninitializedObject(managerType);
            Manager = manager;
            managerType.GetField("_rootBehaviors", Flags)!.SetValue(manager, Roots);
            string[] names = ["CharacterControlSystem.CharacterControlRootBehavior", "MortalSystem.DeadRootBehavior",
                "Carrying.CarryRootBehavior", "DeathSystem.DieRootBehavior", "BeaverContaminationSystem.ContaminateRootBehavior",
                "NeedBehaviorSystem.CriticalNeederRootBehavior", "Wandering.StrandedRootBehavior", "WorkSystem.WorkerRootBehavior",
                "NeedBehaviorSystem.NeederRootBehavior", "Wandering.WanderRootBehavior"];
            var components = new List<object> { manager };
            foreach (string name in names)
            {
                var root = RuntimeHelpers.GetUninitializedObject(_native.LoadNative("Timberborn." + name.Split('.')[0]).GetType("Timberborn." + name)!);
                components.Add(root);
            }
            object Create(string assembly, string name) => RuntimeHelpers.GetUninitializedObject(_native.LoadNative(assembly).GetType(assembly + "." + name)!);
            var initializer = Create("Timberborn.BeaverBehavior", "BeaverBehaviorInitializer");
            components.Add(initializer);
            components.Add(Create("Timberborn.BeaverBehavior", "BeaverNeedBehaviorPicker"));
            components.Add(Create("Timberborn.SleepSystem", "SleepNeedBehavior"));
            AttachCache(components);
            initializer.GetType().GetMethod("InitializeBehaviors", Flags)!.Invoke(initializer, [true]);
            Critical = Roots[5]!; Stranded = Roots[6]!;
            var foreign = RuntimeHelpers.GetUninitializedObject(_native.LoadNative("Timberborn.Beavers").GetType("Timberborn.Beavers.ChildRootBehavior")!);
            Roots.Insert(5, foreign);
            var type = _native.LoadMod().GetType("Wildfire.Timberborn.Fertilizer.FertilizerRecoveryRoot")!;
            var clock = NativePersistenceProxy.Create(_native.LoadNative("Timberborn.TimeSystem").GetType("Timberborn.TimeSystem.IDayNightCycle")!, (_, _) => time?.Invoke() ?? 0f);
            Recovery = Activator.CreateInstance(type, clock)!;
            type.GetField("_manager", Flags)!.SetValue(Recovery, manager);
            _entity = Create("Timberborn.EntitySystem", "EntityComponent");
            AttachCache([_entity, Recovery], _native.LoadNative("Timberborn.EntitySystem").GetType("Timberborn.EntitySystem.IPostInitializableEntity")!);
        }
        private void AttachCache(List<object> components, params Type[] extraTypes)
        {
            var assembly = _native.LoadNative("Timberborn.BaseComponentSystem");
            var cache = RuntimeHelpers.GetUninitializedObject(assembly.GetType("Timberborn.BaseComponentSystem.ComponentCache")!);
            var mapType = assembly.GetType("Timberborn.BaseComponentSystem.TypeIndexMap")!;
            var map = Activator.CreateInstance(mapType)!;
            var readOnlyType = _native.LoadNative("Timberborn.Common").GetType("Timberborn.Common.ReadOnlyList`1")!.MakeGenericType(typeof(object));
            var readOnly = Activator.CreateInstance(readOnlyType, Flags, null, [components], null)!;
            foreach (var type in components.Select(component => component.GetType()).Concat(extraTypes).Distinct())
                mapType.GetMethod("CacheType")!.MakeGenericMethod(type).Invoke(map, [readOnly]);
            cache.GetType().GetField("_components", Flags)!.SetValue(cache, components);
            cache.GetType().GetField("_typeIndexMap", Flags)!.SetValue(cache, map);
            foreach (var component in components)
                assembly.GetType("Timberborn.BaseComponentSystem.BaseComponent")!.GetField("_componentCache", Flags)!.SetValue(component, cache);
        }
        internal bool Ready => (bool)Call(Recovery, "get_RecoveryReady")!;
        internal int Version => (int)Roots.GetType().GetField("_version", Flags)!.GetValue(Roots)!;
        internal void PostInitialize() => Call(_entity, "PostInitialize");
        internal string Arbitrate(Func<string, bool> accept, List<string> trace)
        {
            // Real native arbiter, with decision probes at the positions produced by the
            // verified installer. Native needs/Unity behavior internals are not fabricated.
            var originalRoots = Roots.Cast<object>().ToArray();
            var names = originalRoots.Select(root => root.GetType().Name).ToArray();
            var rootType = _native.LoadNative("Timberborn.BehaviorSystem").GetType("Timberborn.BehaviorSystem.RootBehavior")!;
            var decisionType = _native.LoadNative("Timberborn.BehaviorSystem").GetType("Timberborn.BehaviorSystem.Decision")!;
            var module = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("RecoveryDecisionProbe" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run).DefineDynamicModule("Main");
            var builder = module.DefineType("RecoveryDecisionProbe", TypeAttributes.Public, rootType);
            var callback = builder.DefineField("Callback", typeof(Func<object>), FieldAttributes.Public);
            var baseMethod = rootType.GetMethod("Decide")!;
            var method = builder.DefineMethod("Decide", MethodAttributes.Public | MethodAttributes.Virtual, decisionType, baseMethod.GetParameters().Select(p => p.ParameterType).ToArray());
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, callback);
            il.Emit(OpCodes.Callvirt, typeof(Func<object>).GetMethod("Invoke")!);
            il.Emit(OpCodes.Unbox_Any, decisionType); il.Emit(OpCodes.Ret);
            builder.DefineMethodOverride(method, baseMethod);
            var probeType = builder.CreateType()!;
            Roots.Clear(); var identities = new Dictionary<object, string>();
            foreach (var name in names)
            {
                var probe = RuntimeHelpers.GetUninitializedObject(probeType);
                probeType.GetField("Callback")!.SetValue(probe, (Func<object>)(() =>
                {
                    trace.Add(name);
                    return decisionType.GetMethod(accept(name) ? "ReleaseNextTick" : "ReleaseNow")!.Invoke(null, null)!;
                }));
                Roots.Add(probe); identities.Add(probe, name);
            }
            object Proxy(string assembly, string name, object value) => NativePersistenceProxy.Create(
                _native.LoadNative(assembly).GetType(assembly + "." + name)!, (_, _) => value);
            var managerType = Manager.GetType();
            managerType.GetField("_metricsService", Flags)!.SetValue(Manager, Proxy("Timberborn.Metrics", "IMetricsService", false));
            managerType.GetField("_dayNightCycle", Flags)!.SetValue(Manager, Proxy("Timberborn.TimeSystem", "IDayNightCycle", 0f));
            var logType = _native.LoadNative("Timberborn.Common").GetType("Timberborn.Common.CyclicBuffer`1")!.MakeGenericType(typeof(string));
            managerType.GetField("_timestampedBehaviorLog", Flags)!.SetValue(Manager, Activator.CreateInstance(logType, 16));
            managerType.GetField("_returnToBehavior", Flags)!.SetValue(Manager, false);
            Call(Manager, "ProcessBehaviors");
            Assert.Equal(false, managerType.GetField("_returnToBehavior", Flags)!.GetValue(Manager));
            string winner = identities[managerType.GetField("_runningBehavior", Flags)!.GetValue(Manager)!];
            // Restore native instances for any subsequent arbitration/ordering scenario.
            Roots.Clear();
            foreach (var root in originalRoots) Roots.Add(root);
            return winner;
        }
        internal object? Call(object target, string name) => target.GetType().GetMethod(name, Flags)!.Invoke(target, null);
        public void Dispose() => _native.Dispose();
    }
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
}
