using System.Collections.Immutable;
using System.IO.Compression;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Wildfire.Timberborn.Tests;

/// <summary>Actual installed needs and persistence code. No Unity GameObject or actor lifecycle is fabricated.</summary>
internal sealed class NativeInjuryFixture : IDisposable
{
    private readonly NativeManagedTestContext _native = new();
    internal object Manager { get; }
    internal object Need { get; }
    internal object Spec { get; }
    internal JsonElement InstalledSpec { get; }
    internal NativeInjuryFixture(bool hasInjury = true)
    {
        string archivePath = Path.Combine(Path.GetDirectoryName(_native.ManagedPath)!, "StreamingAssets/Modding/Blueprints.zip");
        using var archive = ZipFile.OpenRead(archivePath);
        using var stream = archive.GetEntry("Needs/Need.Beaver.Injury.blueprint.json")!.Open();
        using var document = JsonDocument.Parse(stream);
        InstalledSpec = document.RootElement.GetProperty("NeedSpec").Clone();
        Spec = New("Timberborn.NeedSpecs", "NeedSpec");
        foreach (string key in new[] { "Id", "CharacterType" }) SetProperty(Spec, key, InstalledSpec.GetProperty(key).GetString());
        foreach (string key in new[] { "StartingValue", "MinimumValue", "MaximumValue", "DailyDelta", "Effectiveness", "ImportanceMultiplier", "HoursWarningThreshold" })
            SetProperty(Spec, key, InstalledSpec.GetProperty(key).GetSingle());
        SetProperty(Spec, "Wastable", InstalledSpec.GetProperty("Wastable").GetBoolean());
        SetProperty(Spec, "BackwardCompatibleIds", ImmutableArray<string>.Empty);
        var critical = New("Timberborn.NeedSpecs", "CriticalNeedSpec");
        SetProperty(critical, "CriticalNeedType", Enum.Parse(critical.GetType().GetProperty("CriticalNeedType")!.PropertyType,
            document.RootElement.GetProperty("CriticalNeedSpec").GetProperty("CriticalNeedType").GetString()!));
        _ = document.RootElement.GetProperty("NeedPreventingWorkSpec");
        var refusal = New("Timberborn.WorkSystem", "NeedPreventingWorkSpec");
        var blueprintType = Type("Timberborn.BlueprintSystem", "Blueprint");
        var components = Array.CreateInstance(Type("Timberborn.BlueprintSystem", "ComponentSpec"), 3);
        components.SetValue(Spec, 0); components.SetValue(critical, 1); components.SetValue(refusal, 2);
        object children = typeof(ImmutableArray<>).MakeGenericType(blueprintType).GetField("Empty")!.GetValue(null)!;
        var blueprint = Activator.CreateInstance(blueprintType, "Need.Beaver.Injury", components, children)!;
        Spec = Call(blueprint, "GetSpec", Spec.GetType())!;
        Need = New("Timberborn.NeedSystem", "Need", Spec, 1f);
        var needs = Array.CreateInstance(Need.GetType(), hasInjury ? 1 : 0);
        if (hasInjury) needs.SetValue(Need, 0);
        Manager = RuntimeHelpers.GetUninitializedObject(Type("Timberborn.NeedSystem", "NeedManager"));
        SetField(Manager, "_needs", New("Timberborn.NeedSystem", "Needs", needs));
        SetField(Manager, "_serializedNeedValueSerializer", New("Timberborn.NeedSystem", "SerializedNeedValueSerializer"));
        var specArray = Array.CreateInstance(Spec.GetType(), hasInjury ? 1 : 0);
        if (hasInjury) specArray.SetValue(Spec, 0);
        var createRange = typeof(ImmutableArray).GetMethods().Single(m => m.Name == "CreateRange" && m.GetParameters().Length == 1 &&
            m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        SetProperty(Manager, "NeedSpecs", createRange.MakeGenericMethod(Spec.GetType()).Invoke(null, new object[] { specArray }));
    }
    internal float Points => (float)Call(Manager, "GetNeedPoints", "Injury")!;
    internal void Apply(float points, string needId = "Injury") => Call(Manager, "ApplyEffect", New("Timberborn.Effects", "InstantEffect", needId, points, 1));
    internal object Save()
    {
        var entity = New("Timberborn.WorldSerialization", "SerializedEntity", Guid.NewGuid(), "Fixture.Beaver");
        Call(Manager, "Save", New("Timberborn.WorldPersistence", "EntitySaver", entity));
        return entity;
    }
    internal void Load(object saved) => Call(Manager, "Load", New("Timberborn.WorldPersistence", "EntityLoader", saved));
    internal void On(string eventName, Action action) => On(Manager, eventName, action);
    internal static void On(object owner, string eventName, Action action)
    {
        var info = owner.GetType().GetEvent(eventName)!;
        var parameters = info.EventHandlerType!.GetMethod("Invoke")!.GetParameters().Select(p => Expression.Parameter(p.ParameterType));
        info.AddEventHandler(owner, Expression.Lambda(info.EventHandlerType,
            Expression.Call(Expression.Constant(action), typeof(Action).GetMethod("Invoke")!), parameters).Compile());
    }
    internal object New(string assembly, string type, params object?[] args) => Activator.CreateInstance(Type(assembly, type), args)!;
    internal Type Type(string assembly, string type) => _native.LoadNative(assembly).GetType(assembly + "." + type)!;
    internal static object? Call(object owner, string method, params object?[] args) => owner.GetType().GetMethods(Flags).Single(m =>
        m.Name == method && !m.IsGenericMethod && m.GetParameters().Length == args.Length && m.GetParameters().Select((p, i) =>
            args[i] is null || (p.ParameterType.IsByRef ? p.ParameterType.GetElementType()! : p.ParameterType).IsInstanceOfType(args[i])).All(match => match)).Invoke(owner, args);
    internal static object? Get(object owner, string name) => owner.GetType().GetProperty(name, Flags)!.GetValue(owner);
    internal static void SetField(object owner, string name, object? value) => owner.GetType().GetField(name, Flags)!.SetValue(owner, value);
    private static void SetProperty(object owner, string name, object? value) => owner.GetType().GetProperty(name, Flags)!.SetValue(owner, value);
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    public void Dispose() => _native.Dispose();
}
