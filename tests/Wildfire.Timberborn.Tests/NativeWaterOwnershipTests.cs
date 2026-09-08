using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed partial class NativeWaterOwnershipTests
{
    [Fact]
    public void ExactInstalledContractAndNativeBufferRules()
    {
        using var f = new F();
        Assert.True((bool)f.Call(f.ContractType, "Supported")!);
        Assert.True((bool)f.Call(f.Contract, "Fixed", f.Water.Source)!);
        Assert.True((bool)f.Call(f.Contract, "ZeroBuffer", f.Water.Source)!);
        f.Water.SupplyCompletedNativeReceipt(.2f); f.Water.CreditNativeInputs();
        Assert.False((bool)f.Call(f.Contract, "ZeroBuffer", f.Water.Source)!);
        Assert.True((bool)f.Call(f.Contract, "ValidBuffer", f.Water.Source)!);
        NativeShorelineWaterFixture.Set(f.Water.Source, "_cleanWaterAmount", float.NaN);
        Assert.False((bool)f.Call(f.Contract, "ValidBuffer", f.Water.Source)!);
    }
    [Fact]
    public void ExactSchedulerPublicationPreservesNativeMetricsOrderAndService()
    {
        using var f = new F(); f.Load();
        var before = f.Entries();
        f.Track();
        var after = f.Entries();
        Assert.Equal(before.Length, after.Length);
        Assert.Same(NativeShorelineWaterFixture.Get(f.Boundary, "_tick"), NativeShorelineWaterFixture.Get(after[0], "_tickableSingleton"));
        foreach (var name in new[] { "_metric", "_metricsEnabled" })
            Assert.Equal(NativeShorelineWaterFixture.Get(before[0], name), NativeShorelineWaterFixture.Get(after[0], name));
        Assert.Equal(before[1], after[1]);
        Assert.True((bool)f.Call(f.Boundary, "Install")!);
        f.Call(f.Source, "Refuse");
        f.Water.SupplyCompletedNativeReceipt(.2f); f.Tick();
        Assert.Equal(.2f, f.Water.Buffer(f.Water.Source));
        Assert.Equal(new[] { "resume", "pause", "resume", "later", "pause" }, f.Events);
    }
    [Fact]
    public void NoSourcesDoesNotInspectOrMutateUnsupportedSchedulerAndPassesThrough()
    {
        using var f = new F(); f.Load(); f.ReplaceOriginal();
        var before = f.Entries();
        f.Call(f.Boundary, "PostLoad");
        Assert.Null(f.Property(f.Boundary, "Contract"));
        Assert.Equal(before, f.Entries());
        f.Water.SupplyCompletedNativeReceipt(.2f);
        f.Call(f.Boundary, "Tick");
        Assert.Equal(.2f, f.Water.Buffer(f.Water.Source));
        f.Call(f.Coordinator, "ThrowIfSaveUnsafe");
    }
    [Fact]
    public void ExpectedForeignSchedulerRefusesSourceWithoutPoisonOrRewrite()
    {
        using var f = new F(); f.Load(); f.ReplaceOriginal();
        var before = f.Entries(); f.Track();
        Assert.True((bool)f.Property(f.Source, "Tainted")!);
        Assert.Equal(before, f.Entries());
        f.Call(f.Coordinator, "ThrowIfSaveUnsafe");
    }
    [Fact]
    public void NativeCreditBeforeArmingPermanentlyRefusesEvenWhenBufferLaterEmpty()
    {
        using var f = new F(); f.Load(); f.Track();
        f.Water.SupplyCompletedNativeReceipt(.2f); f.Tick();
        Assert.True((bool)f.Property(f.Source, "Tainted")!);
        NativeShorelineWaterFixture.Call(f.Water.Source, "RemoveCleanWater", .2f);
        Assert.False((bool)f.Call(f.Source, "TryArmNewSource")!);
        f.Call(f.Coordinator, "ThrowIfSaveUnsafe");
    }
    [Fact]
    public void PositiveInheritedBufferCannotArmOrBeCleared()
    {
        using var f = new F(); f.Load();
        f.Water.SupplyCompletedNativeReceipt(.2f); f.Water.CreditNativeInputs(); f.Track();
        Assert.False((bool)f.Call(f.Source, "TryArmNewSource")!);
        Assert.True((bool)f.Property(f.Source, "Tainted")!);
        Assert.Equal(.2f, f.Water.Buffer(f.Water.Source));
    }
    [Fact]
    public void SchedulerReplacementCannotRearmObservedSources()
    {
        using var f = new F(); f.Load(); f.Track(); f.Load();
        var before = f.Entries();
        Assert.False((bool)f.Call(f.Boundary, "Install")!);
        Assert.Equal(before, f.Entries());
        Assert.True((bool)f.Property(f.Source, "Tainted")!);
        f.Call(f.Coordinator, "ThrowIfSaveUnsafe");
    }
    [Fact]
    public void UnknownCoordinateProviderRefusesAndStillCallsOriginalOnce()
    {
        using var f = new F(); f.Load(); f.Track();
        // A null provider is unsupported. Original Tick throws; refusal itself is save-safe.
        NativeShorelineWaterFixture.Set(f.Water.Source, "_inputCoordinates", null!);
        Assert.Throws<TargetInvocationException>(() => f.Tick());
        Assert.True((bool)f.Property(f.Source, "Tainted")!);
        f.Call(f.Coordinator, "ThrowIfSaveUnsafe");
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeCreditFailurePoisonsAndPreventsRetry(bool forgetLastSource)
    {
        using var f = new F(); f.Load(); f.Track();
        // Native WaterChangeService fails reading deliberately corrupted backing state.
        // No substitute coordinate provider or receipt reader earns support.
        var dictionary = NativeShorelineWaterFixture.Get(f.Water.Changes, "RemovedWaterUnsafe");
        NativeShorelineWaterFixture.Set(f.Water.Changes, "RemovedWaterUnsafe", null!);
        Assert.Throws<TargetInvocationException>(() => f.Tick());
        Assert.Equal(0f, f.Water.Buffer(f.Water.Source));
        Assert.True((bool)f.Property(f.Coordinator, "IsIndeterminate")!);
        Assert.Throws<TargetInvocationException>(() => f.Call(f.Coordinator, "ThrowIfSaveUnsafe"));
        NativeShorelineWaterFixture.Set(f.Water.Changes, "RemovedWaterUnsafe", dictionary!);
        f.Water.SupplyCompletedNativeReceipt(.2f); // Retrying now succeeds unless the guard really excludes it.
        if (forgetLastSource) f.Call(f.Boundary, "Forget", f.Source);
        Assert.Throws<TargetInvocationException>(() => f.Tick());
        Assert.Equal(0f, f.Water.Buffer(f.Water.Source));
    }

    [Fact]
    public void ForeignReceiptServiceRefusesButPreservesItsOrdinaryNativeTick()
    {
        using var f = new F(); f.Load(); f.Track();
        int calls = 0;
        var foreign = NativePersistenceProxy.Create(f.T("Timberborn.WaterSystem", "Timberborn.WaterSystem.IWaterRemovalService"), (_, _) =>
        { calls++; return Activator.CreateInstance(f.T("Timberborn.WaterSystem", "Timberborn.WaterSystem.WaterAmountChange"), .2f, 0f); });
        NativeShorelineWaterFixture.Set(f.Water.InputService, "_waterRemovalService", foreign);
        f.Tick();
        Assert.Equal(1, calls);
        Assert.Equal(.2f, f.Water.Buffer(f.Water.Source));
        Assert.True((bool)f.Property(f.Source, "Tainted")!);
        f.Call(f.Coordinator, "ThrowIfSaveUnsafe");
    }

    [Fact]
    public void ActualNativeSingletonDiscoveryCannotScheduleBoundaryBesideOriginalService()
    {
        using var f = new F();
        var listener = Activator.CreateInstance(f.T("Timberborn.SingletonSystem", "Timberborn.SingletonSystem.SingletonListener"))!;
        f.Call(listener, "Bindito.Core.IProvisionListener.Listen", f.Water.InputService);
        f.Call(listener, "Bindito.Core.IProvisionListener.Listen", f.Boundary);
        f.Call(listener, "Bindito.Core.IInjectionListener.Listen", f.Boundary);
        var repository = Activator.CreateInstance(f.T("Timberborn.SingletonSystem", "Timberborn.SingletonSystem.SingletonRepository"), listener)!;
        var get = repository.GetType().GetMethod("GetSingletons")!;
        var ticks = ((IEnumerable)get.MakeGenericMethod(f.T("Timberborn.TickSystem", "Timberborn.TickSystem.ITickableSingleton")).Invoke(repository, null)!).Cast<object>().ToArray();
        Assert.Equal(new[] { f.Water.InputService }, ticks);
        var postLoad = ((IEnumerable)get.MakeGenericMethod(f.T("Timberborn.SingletonSystem", "Timberborn.SingletonSystem.IPostLoadableSingleton")).Invoke(repository, null)!).Cast<object>().ToArray();
        Assert.Equal(new[] { f.Boundary }, postLoad);
    }

    private sealed partial class F : IDisposable
    {
        private static readonly NativeManagedTestContext N = NativeManagedTestContext.ProxyContracts;
        internal readonly NativeShorelineWaterFixture Water = new(N);
        internal readonly object Contract, Coordinator, Boundary, Source, Scheduler;
        internal readonly Type ContractType;
        internal readonly List<string> Events = new();
        private readonly List<object> _ticks = new();
        private readonly List<object> _loaders = new();
        internal Type T(string a, string n) => N.LoadNative(a).GetType(n)!;
        internal F()
        {
            var mod = N.LoadMod();
            ContractType = mod.GetType("Wildfire.Timberborn.Compatibility.TimberbornWaterCreditContract")!;
            Contract = Activator.CreateInstance(ContractType, true)!;
            Coordinator = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.Resources.NativeResourceCoordinator")!)!;
            var tick = T("Timberborn.TickSystem", "Timberborn.TickSystem.ITickableSingleton");
            var repository = NativePersistenceProxy.Create(T("Timberborn.SingletonSystem", "Timberborn.SingletonSystem.ISingletonRepository"), (m, _) =>
            {
                var element = m.GetGenericArguments().Single();
                var values = element == tick ? _ticks.ToArray() : element.Name == "INonSingletonLoader" ? _loaders.ToArray() : Array.Empty<object>();
                var array = Array.CreateInstance(element, values.Length);
                for (int i = 0; i < values.Length; i++) array.SetValue(values[i], i);
                return array;
            });
            Repository = repository;
            var mode = NativePersistenceProxy.Create(T("Timberborn.TickSystem", "Timberborn.TickSystem.ITickingMode"), (_, _) => true);
            var metrics = NativePersistenceProxy.Create(T("Timberborn.Metrics", "Timberborn.Metrics.IMetricsService"), (m, _) =>
                m.Name == "get_MetricsEnabled" ? true : NativePersistenceProxy.Create(T("Timberborn.Metrics", "Timberborn.Metrics.ITimerMetric"), (c, _) => { Events.Add(c.Name.ToLowerInvariant()); return null; }));
            Scheduler = Activator.CreateInstance(T("Timberborn.TickSystem", "Timberborn.TickSystem.TickableSingletonService"), repository, mode, metrics, null, null)!;
            Boundary = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.FireBell.TimberbornWaterCreditBoundary")!, BindingFlags.Instance | BindingFlags.NonPublic, null, [Water.InputService, Scheduler, repository, Coordinator], null)!;
            Source = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.FireBell.TimberbornNaturalWaterSource")!, BindingFlags.Instance | BindingFlags.NonPublic, null, [Boundary], null)!;
            NativeShorelineWaterFixture.Set(Source, "<Input>k__BackingField", Water.Source);
            _ticks.Add(Water.InputService);
            _ticks.Add(NativePersistenceProxy.Create(tick, (_, _) => { Events.Add("later"); return null; }));

        }
        internal object? Call(object target, string name, params object?[] args) => (target as Type ?? target.GetType())
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Single(m => m.Name == name && !m.ContainsGenericParameters && m.GetParameters().Length == args.Length &&
                m.GetParameters().Select((p, i) => args[i] is null || p.ParameterType.IsInstanceOfType(args[i])).All(v => v))
            .Invoke(target is Type ? null : target, args);
        internal object? Property(object target, string name) => target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target);
        internal object[] Entries() => ((IEnumerable)NativeShorelineWaterFixture.Get(Scheduler, "_tickableSingletons")!).Cast<object>().ToArray();
        internal void Load() { Call(Scheduler, "Load"); NativeShorelineWaterFixture.Set(Scheduler, "<ParalleTicklIsFinished>k__BackingField", true); }
        internal void Track() => Call(Boundary, "Track", Source);
        internal void Tick() => Call(Scheduler, "TickSingletons");
        internal void ReplaceOriginal() { _ticks.RemoveAt(0); Load(); }
        public void Dispose() => Water.Dispose();
    }
}
