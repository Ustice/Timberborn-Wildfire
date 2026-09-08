using System.Collections;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace Wildfire.Timberborn.Tests;

// Actual native task over exclusive supplied columns; no Unity world, scheduler or source ownership claim.
internal sealed class NativeWaterRemovalTaskFixture : IDisposable
{
    internal readonly NativeShorelineWaterFixture Shore = new(NativeManagedTestContext.ProxyContracts, sourceZ: 4);
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly Array _columns;
    private readonly object _task;
    private readonly int _index, _selected;
    private readonly IDictionary _receipts;
    internal int Top { get; }
    internal float Pressure { get; }
    internal int ReceiptCount => _receipts.Count;
    internal float Depth => FieldFloat(_selected, "WaterDepth");
    internal float Overflow => FieldFloat(_selected, "Overflow");
    internal float Contamination => FieldFloat(_selected, "Contamination");
    internal float LowerDepth => FieldFloat(_index, "WaterDepth");

    internal NativeWaterRemovalTaskFixture(float depth = 1, float contamination = 0,
        float overflow = 0, int? ceiling = null, bool secondColumn = false)
    {
        using var zip = ZipFile.OpenRead(Path.Combine(NativeManagedTestContext.ProxyContracts.ManagedPath,
            "../StreamingAssets/Modding/Blueprints.zip"));
        var map = New("Timberborn.MapStateSystem.MapSize", null, null);
        NativeShorelineWaterFixture.Set(map, "_mapSizeSpec", ReadSpec(zip, "MapSize", "Timberborn.MapStateSystem.MapSizeSpec"));
        Call(map, "Initialize", New("UnityEngine.Vector2Int", 8, 8));
        var indices = New("Timberborn.MapIndexSystem.MapIndexService", map);
        Call(indices, "Load");
        int stride = (int)Property(indices, "VerticalStride");
        Top = (int)Property(Property(indices, "TotalSize"), "z");
        _index = (int)Call(indices, "CellToIndex", New("UnityEngine.Vector2Int", 4, 5))!;
        _selected = _index + (secondColumn ? stride : 0);
        var spec = ReadSpec(zip, "WaterSimulator", "Timberborn.WaterSystem.WaterSimulatorSpec");
        Pressure = (float)Property(spec, "OverflowPressureFactor");
        var provider = DispatchProxy.Create(T("Timberborn.BlueprintSystem.ISpecService"), typeof(WaterTaskSpecProvider));
        ((WaterTaskSpecProvider)provider).Spec = spec;
        var calculator = New("Timberborn.WaterSystem.WaterOverflowCalculator", indices, provider);
        Call(calculator, "Load");
        _columns = Array.CreateInstance(T("Timberborn.WaterSystem.WaterColumn"), stride * (secondColumn ? 2 : 1));
        PutColumn(_selected, 4, ceiling ?? Top, depth, contamination, overflow);
        if (secondColumn) PutColumn(_index, 0, 3, 2, 0, 0);
        var counts = new byte[stride];
        counts[_index] = (byte)(secondColumn ? 2 : 1);
        var readCounts = Activator.CreateInstance(T("Timberborn.Common.ReadOnlyArray`1").MakeGenericType(typeof(byte)), counts)!;
        _receipts = (IDictionary)NativeShorelineWaterFixture.Get(Shore.Changes, "RemovedWaterUnsafe")!;
        _task = New("Timberborn.WaterSystem.UpdateWaterChangesTask", indices,
            New("Timberborn.WaterSystem.WaterDepthSetter", calculator),
            New("Timberborn.WaterSystem.MutableWaterColumnRetriever"), _receipts, _columns, readCounts,
            Property(Shore.Changes, "ThreadSafeWaterChanges"), stride, Pressure, Property(spec, "MaxWaterContamination"));
    }

    internal void Remove(float amount, bool dirty = false, int z = 4)
    {
        var service = New("Timberborn.WaterSystem.WaterService", null, Shore.Changes, null, null);
        Call(service, dirty ? "RemoveContaminatedWater" : "RemoveCleanWater", New("UnityEngine.Vector3Int", 4, 5, z), amount);
    }
    internal (float Clean, float Dirty) Complete(int z = 4)
    {
        Call(Shore.Changes, "Tick");
        Call(_task, "Run"); // Synchronous native task completion, not an authored worker result.
        var receipt = Call(Shore.Changes, "GetWaterChangeUnsafe", New("UnityEngine.Vector3Int", 4, 5, z))!;
        return ((float)Property(receipt, "CleanWaterChange"), (float)Property(receipt, "ContaminatedWaterChange"));
    }
    internal float Demand(float amount) => (float)Call(Shore.Source, "DemandCleanWaterAmount", amount)!;
    private void PutColumn(int index, int floor, int ceiling, float depth, float contamination, float overflow)
    {
        var column = New("Timberborn.WaterSystem.WaterColumn", floor, ceiling);
        foreach (var (name, value) in new[] { ("WaterDepth", depth), ("OldWaterDepth", depth),
                     ("Contamination", contamination), ("Overflow", overflow) })
            column.GetType().GetField(name)!.SetValue(column, value);
        _columns.SetValue(column, index);
    }
    private float FieldFloat(int index, string name)
    {
        var column = _columns.GetValue(index)!;
        return (float)column.GetType().GetField(name)!.GetValue(column)!;
    }
    private object ReadSpec(ZipArchive zip, string name, string type)
    {
        using var json = JsonDocument.Parse(zip.GetEntry($"Configurations/{name}.blueprint.json")!.Open());
        var spec = New(type);
        foreach (var member in json.RootElement.GetProperty(name + "Spec").EnumerateObject())
        {
            var property = spec.GetType().GetProperty(member.Name)!;
            object value = property.PropertyType == typeof(int) ? (object)member.Value.GetInt32()
                : property.PropertyType == typeof(float) ? member.Value.GetSingle()
                : New("UnityEngine.Vector2Int", member.Value.GetProperty("X").GetInt32(), member.Value.GetProperty("Y").GetInt32());
            property.SetValue(spec, value);
        }
        return spec;
    }
    private Type T(string name) => Shore.Type(name.StartsWith("UnityEngine.", StringComparison.Ordinal)
        ? "UnityEngine.CoreModule" : name[..name.LastIndexOf('.')], name);
    private object New(string name, params object?[] args) => Activator.CreateInstance(T(name), Flags, null, args, null)!;
    private static object Property(object instance, string name) => instance.GetType().GetProperty(name, Flags)!.GetValue(instance)!;
    private static object? Call(object instance, string method, params object?[] args) => instance.GetType().GetMethods(Flags)
        .Single(m => m.Name == method && !m.IsGenericMethodDefinition && m.GetParameters().Length == args.Length &&
            m.GetParameters().Select((p, i) => args[i] is null || p.ParameterType.IsInstanceOfType(args[i])).All(value => value))
        .Invoke(instance, args);
    public void Dispose() => Shore.Dispose(); // Managed arrays; no outstanding task or native container allocation.
}

public class WaterTaskSpecProvider : DispatchProxy
{
    internal object Spec = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) =>
        method!.Name == "GetSingleSpec" && method.ReturnType.IsInstanceOfType(Spec)
            ? Spec : throw new NotSupportedException(method.Name);
}
