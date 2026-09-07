using System.Collections;
using System.Reflection;

namespace Wildfire.Timberborn.Tests;

// The invocation decorator and marker are test-only. Native scheduler/lifecycle/resource methods run unchanged.
internal sealed class NativeWaterCreditSchedulerFixture : IDisposable
{
    private static readonly NativeManagedTestContext Native = new(collectible: false);
    internal readonly NativeShorelineWaterFixture Water = new(Native);
    internal readonly List<string> Events = new();
    internal bool Tainted;
    internal int Observations, MetricStarts, MetricStops;
    internal Action? LaterAction, BeforeObservation;
    private readonly object _repository, _scheduler, _lifecycle, _wrapper, _later;
    private readonly List<object> _ticks = new();
    private readonly Type _tickType, _entryType;
    internal object? OriginalMetric;
    internal NativeWaterCreditSchedulerFixture()
    {
        _tickType = T("Timberborn.TickSystem", "Timberborn.TickSystem.ITickableSingleton");
        _entryType = T("Timberborn.TickSystem", "Timberborn.TickSystem.TickableSingletonService+MeteredSingleton");
        _later = Proxy(_tickType, () => { Events.Add("later"); LaterAction?.Invoke(); });
        _wrapper = Proxy(_tickType, () =>
        {
            Events.Add("observe"); BeforeObservation?.Invoke();
            Observations++;
            try { Water.RequireExclusiveInput(Water.Source); }
            catch (InvalidOperationException) { Tainted = true; }
            Water.CreditNativeInputs();
            Events.Add("credited");
        });
        var postLoad = Proxy(T("Timberborn.SingletonSystem", "Timberborn.SingletonSystem.IPostLoadableSingleton"), Install);
        _repository = NativePersistenceProxy.Create(T("Timberborn.SingletonSystem", "Timberborn.SingletonSystem.ISingletonRepository"), (m, _) =>
        {
            var type = m.GetGenericArguments().Single();
            var values = type == _tickType ? _ticks.ToArray() : type.Name == "ILoadableSingleton" ? new[] { _scheduler! } :
                type.Name == "IPostLoadableSingleton" ? new[] { postLoad } : Array.Empty<object>();
            var array = Array.CreateInstance(type, values.Length);
            for (int i = 0; i < values.Length; i++) array.SetValue(values[i], i);
            return array;
        });
        var mode = NativePersistenceProxy.Create(T("Timberborn.TickSystem", "Timberborn.TickSystem.ITickingMode"), (_, _) => true);
        var metrics = NativePersistenceProxy.Create(T("Timberborn.Metrics", "Timberborn.Metrics.IMetricsService"), (m, _) =>
        {
            if (m.Name == "get_MetricsEnabled") return true;
            if (m.Name == "GetTimerMetric") return NativePersistenceProxy.Create(T("Timberborn.Metrics", "Timberborn.Metrics.ITimerMetric"), (call, _) =>
            {
                if (call.Name == "Resume") MetricStarts++;
                else if (call.Name == "Pause") MetricStops++;
                else throw new NotSupportedException(call.Name);
                return null;
            });
            throw new NotSupportedException(m.Name);
        });
        _scheduler = Activator.CreateInstance(T("Timberborn.TickSystem", "Timberborn.TickSystem.TickableSingletonService"), _repository, mode, metrics, null, null)!;
        _ticks.Add(Water.InputService); _ticks.Add(_later);
        _lifecycle = Activator.CreateInstance(T("Timberborn.SingletonSystem", "Timberborn.SingletonSystem.SingletonLifecycleService"), _repository)!;
    }
    private static Type T(string assembly, string type) => Native.LoadNative(assembly).GetType(type)!;
    private static object Proxy(Type type, Action action) => NativePersistenceProxy.Create(type, (_, _) => { action(); return null; });
    private object Entries => NativeShorelineWaterFixture.Get(_scheduler, "_tickableSingletons")!;
    internal object[] Targets => ((IEnumerable)Entries).Cast<object>().Select(value => NativeShorelineWaterFixture.Get(value, "_tickableSingleton")!).ToArray();
    internal void LoadAll() => NativeShorelineWaterFixture.Call(_lifecycle, "LoadAll");
    internal void ReloadSchedulerOnly() => NativeShorelineWaterFixture.Call(_scheduler, "Load");
    internal void Install()
    {
        var entries = Entries;
        var values = ((IEnumerable)entries).Cast<object>().ToArray();
        var originals = values.Select((value, index) => (value, index)).Where(pair => ReferenceEquals(NativeShorelineWaterFixture.Get(pair.value, "_tickableSingleton"), Water.InputService)).ToArray();
        var wrappers = values.Count(value => ReferenceEquals(NativeShorelineWaterFixture.Get(value, "_tickableSingleton"), _wrapper));
        if (originals.Length == 0 && wrappers == 1) return; // Exact same installer, already installed.
        if (originals.Length != 1 || wrappers != 0) throw new InvalidOperationException("Expected exactly one undecorated native water input tick.");
        var (old, index) = originals[0];
        OriginalMetric = NativeShorelineWaterFixture.Get(old, "_metric");
        var metrics = NativeShorelineWaterFixture.Get(old, "_metricsEnabled");
        var replacement = Activator.CreateInstance(_entryType, _wrapper, OriginalMetric, metrics)!;
        var updated = entries.GetType().GetMethod("SetItem")!.Invoke(entries, [index, replacement])!;
        NativeShorelineWaterFixture.Set(_scheduler, "_tickableSingletons", updated);
    }
    internal void RunNativeSingletonTicks() => _scheduler.GetType().GetMethod("TickSingletons", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_scheduler, null);
    internal bool TryFillWithMarker(object bucket) => !Tainted && Water.TryFill(Water.Source, bucket);
    internal void DuplicateOriginal() { _ticks.Insert(0, Water.InputService); ReloadSchedulerOnly(); }
    internal void ForeignReplacement() { _ticks[0] = Proxy(_tickType, () => Water.CreditNativeInputs()); ReloadSchedulerOnly(); }
    internal bool MarkerRoundTrip()
    {
        var world = Native.LoadNative("Timberborn.WorldPersistence");
        var key = Activator.CreateInstance(world.GetType("Timberborn.WorldPersistence.ComponentKey")!, "Fixture.ShorelineOwnership")!;
        var property = Activator.CreateInstance(T("Timberborn.Persistence", "Timberborn.Persistence.PropertyKey`1").MakeGenericType(typeof(bool)), "Tainted")!;
        var serialized = Activator.CreateInstance(T("Timberborn.WorldSerialization", "Timberborn.WorldSerialization.SerializedEntity"), Guid.NewGuid(), "Fixture.Intake")!;
        var saver = Activator.CreateInstance(world.GetType("Timberborn.WorldPersistence.EntitySaver")!, serialized)!;
        var objectSaver = saver.GetType().GetMethod("GetComponent", [key.GetType()])!.Invoke(saver, [key])!;
        objectSaver.GetType().GetMethod("Set", [property.GetType(), typeof(bool)])!.Invoke(objectSaver, [property, Tainted]);
        var loader = Activator.CreateInstance(world.GetType("Timberborn.WorldPersistence.EntityLoader")!, serialized)!;
        var tryGet = loader.GetType().GetMethods().Single(m => m.Name == "TryGetComponent" && m.GetParameters().Length == 2);
        object?[] args = [key, null];
        Assert.True((bool)tryGet.Invoke(loader, args)!);
        var objectLoader = args[1]!;
        return (bool)objectLoader.GetType().GetMethod("Get", [property.GetType()])!.Invoke(objectLoader, [property])!;
    }
    internal void AssertPreservedSchedulerEntry()
    {
        var entries = ((IEnumerable)Entries).Cast<object>().ToArray();
        Assert.Same(_wrapper, Targets[0]); Assert.Same(_later, Targets[1]);
        Assert.Same(OriginalMetric, NativeShorelineWaterFixture.Get(entries[0], "_metric"));
        Assert.True((bool)NativeShorelineWaterFixture.Get(entries[0], "_metricsEnabled")!);
    }
    public void Dispose() => Water.Dispose();
}
