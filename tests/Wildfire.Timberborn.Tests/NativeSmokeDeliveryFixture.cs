using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

// Executes native managed setters/status events on supplied component caches.
// No Unity GameObject truth, animation, status rendering, or complete beaver lifecycle is modeled.
internal sealed class NativeSmokeDeliveryFixture
{
    private static readonly NativeManagedTestContext Native = NativeManagedTestContext.ProxyContracts;
    internal const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    internal readonly object Registry, Adapter, Actuator, Dispatcher;
    internal readonly Dictionary<string, object> Coughs = new();
    internal readonly Dictionary<string, object> Workers = new();
    internal Action<string>? OnLog;
    internal const string A = "00000000-0000-0000-0000-000000000001";
    internal const string B = "00000000-0000-0000-0000-000000000002";

    internal NativeSmokeDeliveryFixture()
    {
        Registry = Activator.CreateInstance(T("Timberborn.EntitySystem", "EntityRegistry"))!;
        Adapter = New("Beavers.TimberbornEntityRegistryBeaverWorkerSpeedAdapter", Registry);
        Actuator = New("Beavers.TimberbornWorkerSpeedBeaverFieldBehaviorActuator", Adapter);
        var log = Proxy("Runtime.ITimberbornFireLogSink", (_, args) => { OnLog?.Invoke((string)args[0]!); return null; });
        var options = Record("Beavers.TimberbornBeaverFieldBehaviorOptions", new() { ["SmokeCoughingThresholdSamples"] = 1 });
        Dispatcher = New("Beavers.TimberbornBeaverFieldBehaviorDispatcher", Actuator, log, options);
    }

    internal void Add(string id, bool status = true, bool worker = false)
    {
        var entity = Blank(T("Timberborn.EntitySystem", "EntityComponent"));
        Set(entity, "<EntityId>k__BackingField", Guid.Parse(id));
        List<object> components = [entity, Blank(T("Timberborn.Beavers", "Beaver"))];
        if (worker)
        {
            var nativeWorker = Blank(T("Timberborn.WorkSystem", "Worker"));
            Set(nativeWorker, "_workingSpeedMultiplier", 1f);
            components.Add(nativeWorker); Workers.Add(id, nativeWorker);
        }
        if (status)
        {
            components.Add(Blank(T("Timberborn.StatusSystem", "StatusSubject")));
            var cough = Blank(T("Timberborn.StatusSystem", "StatusToggle"));
            var choke = Blank(T("Timberborn.StatusSystem", "StatusToggle"));
            Coughs.Add(id, cough);
            Type toggles = Adapter.GetType().GetNestedType("TimberbornBeaverSmokeStatusToggles", BindingFlags.NonPublic)!;
            var pair = Activator.CreateInstance(toggles, Flags, null, [cough, choke], null)!;
            ((IDictionary)GetField(Adapter, "_statusTogglesByBeaverId")!).Add(id, pair);
        }
        Bind(components);
        ((IDictionary)GetField(Registry, "_entities")!).Add(Guid.Parse(id), entity);
    }

    internal void OnToggle(string id, Action action)
    {
        EventHandler<EventArgs> handler = (_, _) => action();
        Coughs[id].GetType().GetEvent("StatusToggled")!.AddEventHandler(Coughs[id], handler);
    }
    internal bool Active(string id) => (bool)Get(Coughs[id], "IsActive")!;
    internal float Speed(string id) => (float)Get(Workers[id], "WorkingSpeedMultiplier")!;
    internal void Dispatch(uint tick, bool exposed, params string[] ids) => Call(Dispatcher, "Dispatch", Snapshot(exposed, ids), tick);
    internal object Snapshot(bool exposed, params string[] ids)
    {
        Array values = Array.CreateInstance(M("Beavers.TimberbornBeaverFieldExposureClassification"), ids.Length);
        for (int i = 0; i < ids.Length; i++) values.SetValue(Record("Beavers.TimberbornBeaverFieldExposureClassification",
            new() { ["BeaverId"] = ids[i], ["RespiratoryExposureCells"] = exposed ? 1 : 0 }), i);
        return M("Beavers.TimberbornBeaverFieldExposureSnapshot").GetMethod("FromClassifications")!.Invoke(null, [ids.Length, 0, 0, values])!;
    }
    internal object[] History() => ((IEnumerable)Get(Call(Dispatcher, "CaptureState")!, "Entries")!).Cast<object>().ToArray();
    internal static Type T(string assembly, string name) => Native.LoadNative(assembly).GetType(assembly + "." + name)!;
    internal static Type M(string name) => Native.LoadMod().GetType("Wildfire.Timberborn." + name)!;
    internal static object New(string name, params object?[] args) => Activator.CreateInstance(M(name), args)!;
    internal static object Blank(Type type) => RuntimeHelpers.GetUninitializedObject(type);
    internal static object? Get(object value, string property) => value.GetType().GetProperty(property)!.GetValue(value);
    internal static object? GetField(object value, string field) => value.GetType().GetField(field, Flags)!.GetValue(value);
    internal static void Set(object value, string field, object? data) => value.GetType().GetField(field, Flags)!.SetValue(value, data);
    internal static object? Call(object value, string method, params object?[] args) => value.GetType().GetMethod(method, Flags)!.Invoke(value, args);
    internal static object Proxy(string name, Func<MethodInfo, object?[], object?> call) => NativePersistenceProxy.Create(M(name), call);
    internal static object Record(string name, Dictionary<string, object?> values)
    {
        var constructor = M(name).GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        return constructor.Invoke(constructor.GetParameters().Select(p => values.TryGetValue(p.Name!, out var value) ? value :
            p.HasDefaultValue ? p.DefaultValue : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray());
    }
    private static void Bind(List<object> components)
    {
        var cache = Blank(T("Timberborn.BaseComponentSystem", "ComponentCache")); Set(cache, "_components", components);
        var index = Activator.CreateInstance(T("Timberborn.BaseComponentSystem", "TypeIndexMap"))!;
        var list = Activator.CreateInstance(T("Timberborn.Common", "ReadOnlyList`1").MakeGenericType(typeof(object)), Flags, null, [components], null)!;
        foreach (Type type in new[] { T("Timberborn.Beavers", "Beaver"), T("Timberborn.WorkSystem", "Worker"), T("Timberborn.StatusSystem", "StatusSubject") })
            index.GetType().GetMethod("CacheType")!.MakeGenericMethod(type).Invoke(index, [list]);
        Set(cache, "_typeIndexMap", index);
        foreach (object component in components) T("Timberborn.BaseComponentSystem", "BaseComponent").GetField("_componentCache", Flags)!.SetValue(component, cache);
    }
}
