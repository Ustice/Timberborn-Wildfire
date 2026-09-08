using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using Timberborn.EntitySystem;
using Timberborn.Persistence;
using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using Timberborn.WaterBuildings;
using Timberborn.WorldPersistence;

namespace Wildfire.Timberborn.Compatibility;

internal sealed partial class TimberbornWaterCreditContract
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly FieldInfo _entries, _target, _metric, _metrics, _inputs, _provider, _clean, _dirty, _worldEntities, _serialized, _removal;
    private readonly Type _entryType, _fixed, _pipe, _worldLoader;
    private readonly ConstructorInfo _entryConstructor;
    private readonly ComponentKey _waterKey;
    private readonly PropertyKey<float> _cleanKey, _dirtyKey;
    internal TimberbornWaterCreditContract()
    {
        var tick = typeof(ITickableSingleton).Assembly.GetType("Timberborn.TickSystem.TickableSingletonService", true)!;
        _entryType = tick.GetNestedType("MeteredSingleton", BindingFlags.NonPublic)!;
        _entries = Field(tick, "_tickableSingletons");
        _target = Field(_entryType, "_tickableSingleton", typeof(ITickableSingleton));
        _metric = Field(_entryType, "_metric"); _metrics = Field(_entryType, "_metricsEnabled", typeof(bool));
        _entryConstructor = _entryType.GetConstructor(new[] { typeof(ITickableSingleton), _metric.FieldType, typeof(bool) })!;
        if (_entryConstructor is null || _entries.FieldType.GetGenericTypeDefinition() != typeof(System.Collections.Immutable.ImmutableArray<>) ||
            _entries.FieldType.GenericTypeArguments[0] != _entryType) throw new NotSupportedException("Unexpected water scheduler layout.");
        _removal = Field(typeof(WaterInputService), "_waterRemovalService", typeof(global::Timberborn.WaterSystem.IWaterRemovalService));
        _inputs = Field(typeof(WaterInputService), "_waterInputs", typeof(List<WaterInput>));
        _provider = Field(typeof(WaterInput), "_inputCoordinates");
        _clean = Field(typeof(WaterInput), "_cleanWaterAmount", typeof(float));
        _dirty = Field(typeof(WaterInput), "_contaminatedWaterAmount", typeof(float));
        _fixed = typeof(WaterInput).Assembly.GetType("Timberborn.WaterBuildings.WaterInputFixedCoordinates", true)!;
        _pipe = typeof(WaterInput).Assembly.GetType("Timberborn.WaterBuildings.WaterInputPipeCoordinates", true)!;
        _worldLoader = typeof(IEntityLoader).Assembly.GetType("Timberborn.WorldPersistence.WorldEntitiesLoader", true)!;
        _worldEntities = Field(_worldLoader, "_instantiatedSerializedEntities", typeof(List<InstantiatedSerializedEntity>));
        _serialized = Field(typeof(EntityLoader), "_serializedEntity", typeof(global::Timberborn.WorldSerialization.SerializedEntity));
        _waterKey = Static<ComponentKey>("WaterInputKey");
        _cleanKey = Static<PropertyKey<float>>("CleanWaterAmountKey"); _dirtyKey = Static<PropertyKey<float>>("ContaminatedWaterAmountKey");
        _inputWaterService = Field(typeof(WaterInput), "_waterService");
        _inputMap = Field(typeof(WaterInput), "_threadSafeWaterMap");
        _inputCreditService = Field(typeof(WaterInput), "_waterInputService", typeof(WaterInputService));
        _waterServiceType = typeof(global::Timberborn.WaterSystem.IWaterService).Assembly.GetType("Timberborn.WaterSystem.WaterService", true)!;
        _mapType = typeof(global::Timberborn.WaterSystem.IThreadSafeWaterMap).Assembly.GetType("Timberborn.WaterSystem.ThreadSafeWaterMap", true)!;
        _serviceChanges = Field(_waterServiceType, "_waterChangeService");
        WaterUnit = ReadNativeWaterUnit();
    }
    private static FieldInfo Field(Type owner, string name, Type? expected = null)
    {
        var field = owner.GetField(name, Fields) ?? throw new NotSupportedException($"Missing native field {owner.Name}.{name}.");
        if (field.DeclaringType != owner || (expected is not null && field.FieldType != expected))
            throw new NotSupportedException($"Unexpected native field {owner.Name}.{name}.");
        return field;
    }
    private static T Static<T>(string name) => (T)typeof(WaterInput).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
    internal static bool Supported()
    {
        string directory = Path.GetDirectoryName(typeof(WaterInput).Assembly.Location)!;
        foreach (var pair in Fingerprints)
        {
            if (!File.Exists(Path.Combine(directory, pair.Key))) return false;
            using var stream = File.OpenRead(Path.Combine(directory, pair.Key));
            using var sha = SHA256.Create();
            if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != pair.Value) return false;
        }
        return true;
    }
    internal bool KnownService(WaterInputService service) => service.GetType() == typeof(WaterInputService) &&
        _removal.GetValue(service)?.GetType() == typeof(global::Timberborn.WaterSystem.IWaterService).Assembly.GetType("Timberborn.WaterSystem.WaterChangeService");
    internal WaterInput[] Inputs(WaterInputService service) => ((List<WaterInput>)_inputs.GetValue(service)!).ToArray();
    internal bool Fixed(WaterInput input) => input.GetType() == typeof(WaterInput) && _provider.GetValue(input)?.GetType() == _fixed;
    internal bool Known(WaterInput input) => input.GetType() == typeof(WaterInput) &&
        (_provider.GetValue(input)?.GetType() is { } type && (type == _fixed || type == _pipe));
    internal bool ValidBuffer(WaterInput input) => Valid((float)_clean.GetValue(input)!) && Valid((float)_dirty.GetValue(input)!);
    internal bool ZeroBuffer(WaterInput input) => (float)_clean.GetValue(input)! == 0 && (float)_dirty.GetValue(input)! == 0;
    private static bool Valid(float amount) => float.IsFinite(amount) && amount >= 0;
    internal bool SavedBufferPresent(IEntityLoader loader) => loader.TryGetComponent(_waterKey, out var saved) && Valid(saved.Get(_cleanKey)) && Valid(saved.Get(_dirtyKey));
    internal object? NativeLoadList(ISingletonRepository repository)
    {
        var loaders = repository.GetSingletons<INonSingletonLoader>().Where(value => value.GetType() == _worldLoader).ToArray();
        return loaders.Length == 1 ? _worldEntities.GetValue(loaders[0]) : null;
    }
    internal bool Loading(object? list, EntityComponent entity, IEntityLoader loader) => loader.GetType() == typeof(EntityLoader) &&
        list is List<InstantiatedSerializedEntity> entities && entities.Any(value => ReferenceEquals(value.Entity, entity) &&
            ReferenceEquals(value.SerializedEntity, _serialized.GetValue(loader)));
    internal object? Entries(ITickableSingletonService scheduler) => scheduler.GetType() == _entries.DeclaringType ? _entries.GetValue(scheduler) : null;
    internal bool IsInstalled(ITickableSingletonService scheduler, object? published) => published is not null && Equals(Entries(scheduler), published);
    internal object? Install(ITickableSingletonService scheduler, WaterInputService original, ITickableSingleton wrapper)
    {
        var entries = Entries(scheduler);
        if (entries is not IEnumerable sequence || (bool)entries.GetType().GetProperty("IsDefault")!.GetValue(entries)!) return null;
        int index = -1, current = 0, wrappers = 0;
        object? old = null;
        foreach (var entry in sequence)
        {
            var target = _target.GetValue(entry);
            if (ReferenceEquals(target, original)) { if (index >= 0) return null; index = current; old = entry; }
            if (ReferenceEquals(target, wrapper)) wrappers++;
            current++;
        }
        if (index < 0 || wrappers != 0) return null;
        var replacement = _entryConstructor.Invoke(new[] { wrapper, _metric.GetValue(old), _metrics.GetValue(old) });
        var updated = entries.GetType().GetMethod("SetItem")!.Invoke(entries, new[] { (object)index, replacement })!;
        _entries.SetValue(scheduler, updated);
        if (!IsInstalled(scheduler, updated)) throw new InvalidOperationException("Water credit installation did not publish its exact scheduler entry.");
        return updated;
    }
    private static readonly IReadOnlyDictionary<string, string> Fingerprints = new Dictionary<string, string>
    {
        ["Timberborn.TickSystem.dll"] = "5df66413408a54fc616343668600f114fd33056fafe0bf4b2465be084c55cb9d",
        ["Timberborn.SingletonSystem.dll"] = "231e0464941fc95f44809302c4f4b2e87b61904e1bb16ff937684011582b8aab",
        ["Timberborn.WorldPersistence.dll"] = "831546b1f90af169d6d85076ca26bd885574daf56e0ad27f3b90bc5b1fdb8bb0",
        ["Timberborn.WorldSerialization.dll"] = "706966f455bfee0fa75ad6d9d7e007e0e46d89c1193ac5e7fccd219f7f5952dd",
        ["Timberborn.Persistence.dll"] = "d62b8712b75211682eb0cad07af993cd3b0ff6cbc7d8e080288b6d2cbdd26c5e",
        ["Timberborn.WaterBuildings.dll"] = "f006ae2661e3c3682cb55cba4ee75c58f1ebff7c9028edc775c823490c9106f3",
        ["Timberborn.WaterSystem.dll"] = "0680fd336e1d33ac7107b8a4f7d5e47f0535cd0d998c6654086ade44d8bebe20",
        ["Timberborn.WaterWorkshops.dll"] = "322f0974802d582bf2fcc27ed03ad00a6c6445fdf464e9f5a4a5015688e7ac3a",
        ["Timberborn.EntitySystem.dll"] = "c198cf97f6c57a50117c2593a686aa7874cd8eeb567fd25f3409e18d29a0635e",
        ["Timberborn.BaseComponentSystem.dll"] = "0d479b9533880e1368b5df144f8d83f30d9c8774cfc8f96f071ac53d5851318d",
        ["Timberborn.BlockSystem.dll"] = "49aef9433b68891abbeb9fc12c91f5932220b501d19b63b4e059110abd2baec1",
        ["Timberborn.TemplateInstantiation.dll"] = "259c6e6e6337675c45e480e33eaf8f08e687c70f2d43d0d3af68ee43dfaf4336",
    };
}
