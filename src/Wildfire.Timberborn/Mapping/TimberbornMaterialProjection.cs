using Wildfire.Core;

namespace Wildfire.Timberborn.Mapping;

/// <summary>Initial material definition. Fuel here is never the simulator's remaining fuel.</summary>
public sealed record TimberbornMaterialPart
{
    private TimberbornMaterialPart(string key, int priority, byte fuel, byte flammability, WildfireMaterialClass materialClass)
    { Key = key; Priority = priority; InitialFuel = fuel; Flammability = flammability; MaterialClass = materialClass; }
    public string Key { get; }
    public int Priority { get; }
    public byte InitialFuel { get; }
    public byte Flammability { get; }
    public WildfireMaterialClass MaterialClass { get; }
    public bool IsStorage => MaterialClass == WildfireMaterialClass.Storage;

    public static TimberbornMaterialPart Building(string specId) => From("structure", new TimberbornBuildingAdapter().CreateBuildingSource(0, 0, 0, specId));
    public static TimberbornMaterialPart Infrastructure() => From("infrastructure", new TimberbornBuildingAdapter().CreateNonBurnableSource(0, 0, 0));
    public static TimberbornMaterialPart Tree(string specId) => From("tree", new TimberbornResourceAdapter().CreateTreeSource(0, 0, 0, specId));
    public static TimberbornMaterialPart Crop(string specId) => From("crop", new TimberbornResourceAdapter().CreateCropSource(0, 0, 0, specId));
    public static TimberbornMaterialPart StoredGood(string goodId) => From("stored:" + goodId, new TimberbornResourceAdapter().CreateStockpileResourceSource(0, 0, 0, goodId));

    public static TimberbornMaterialPart Vegetation(string specId)
    {
        var profile = TimberbornBurnableCatalog.Default.Lookup(specId);
        if (!profile.Known || profile.Type != "bush")
            throw new ArgumentException("Vegetation requires a known native bush material profile.", nameof(specId));
        return new TimberbornMaterialPart("vegetation", 2, profile.FuelValue, profile.Flammability,
            WildfireMaterialClass.Vegetation);
    }

    private static TimberbornMaterialPart From(string key, TimberbornCellSource source)
    {
        if (source.Building is { } building)
            return new TimberbornMaterialPart(key, 3, building.Fuel, building.Flammability, source.MaterialClass);
        if (source.Resource is { } resource)
            return new TimberbornMaterialPart(key, 2, resource.Fuel, resource.Flammability, source.MaterialClass);
        throw new InvalidOperationException("Native material part requires an existing building or resource definition.");
    }
}

public sealed class TimberbornMaterialProjection
{
    public TimberbornMaterialProjection(Guid entityId, IEnumerable<TimberbornMaterialFootprintSlot> footprint,
        IEnumerable<TimberbornMaterialPart> parts)
    {
        if (entityId == Guid.Empty) throw new ArgumentException("Native entity identity is required.", nameof(entityId));
        EntityId = entityId;
        var slots = footprint.ToArray();
        var definitions = parts.ToArray();
        if (slots.Length == 0 || slots.Select(slot => slot.LocalCoordinates).Distinct().Count() != slots.Length ||
            slots.Select(slot => slot.CellIndex).Distinct().Count() != slots.Length)
            throw new ArgumentException("Projection requires a nonempty bijective footprint.", nameof(footprint));
        if (definitions.Length == 0 || definitions.Select(part => part.Key).Distinct(StringComparer.Ordinal).Count() != definitions.Length)
            throw new ArgumentException("Projection requires unique material part keys.", nameof(parts));
        if (definitions.Count(part => !part.IsStorage) > 1)
            throw new ArgumentException("Multiple non-storage definitions for one native entity are unsupported.", nameof(parts));
        Footprint = Array.AsReadOnly(slots);
        Parts = Array.AsReadOnly(definitions);
    }
    public Guid EntityId { get; }
    public IReadOnlyList<TimberbornMaterialFootprintSlot> Footprint { get; }
    public IReadOnlyList<TimberbornMaterialPart> Parts { get; }
}

public readonly record struct TimberbornMaterialOwner(Guid EntityId, uint TargetId, uint SlotId);
public sealed record TimberbornMaterialContributor(TimberbornMaterialOwner Owner, IReadOnlyList<TimberbornMaterialPart> Parts);

/// <summary>One coherent desired material, plus every hidden contributor. Not a GPU receipt or current fuel state.</summary>
public sealed record TimberbornResolvedMaterialCell(int CellIndex, TimberbornMaterialOwner? Owner,
    ushort PackedDefinition, WildfireMaterialFieldProfile Profile, bool HasSameEntityStorageComposite,
    IReadOnlyList<TimberbornMaterialContributor> Contributors);
