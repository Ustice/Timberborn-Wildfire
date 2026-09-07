using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Persistence;

public enum OwnedNativeCompatibilityCapability
{
    Unavailable, Complete
}

/// <summary>Static named role, never current yield, availability, or a growth-generation cursor.</summary>
public sealed record OwnedNamedYieldDefinition
{
    public OwnedNamedYieldDefinition(TimberbornCapturedYieldRole role, string componentName, string goodId, int amount, bool removeOnCut)
    {
        if (!Enum.IsDefined(typeof(TimberbornCapturedYieldRole), role) || string.IsNullOrWhiteSpace(componentName) ||
            string.IsNullOrWhiteSpace(goodId) || amount < 0 || (removeOnCut && role != TimberbornCapturedYieldRole.Cuttable))
            throw new ArgumentException("Invalid static named yield definition.");
        Role = role;
        ComponentName = componentName;
        GoodId = goodId;
        Amount = amount;
        RemoveOnCut = removeOnCut;
    }
    public TimberbornCapturedYieldRole Role { get; }
    public string ComponentName { get; }
    public string GoodId { get; }
    public int Amount { get; }
    public bool RemoveOnCut { get; }
}

/// <summary>Compatibility evidence captured at formation, separate from the body's saved accounting definition.</summary>
public sealed class OwnedNativeDefinitionWitness
{
    public OwnedNativeDefinitionWitness(Guid entityId, string specId, TimberbornInitialBodyShape shape,
        TimberbornBurnableProfile bodyProfile, IEnumerable<TimberbornCellCoordinates> footprint,
        IEnumerable<OwnedNamedYieldDefinition> yields, IEnumerable<TimberbornBurnDamageResourceStack>? buildingCost)
    {
        if (entityId == Guid.Empty || string.IsNullOrWhiteSpace(specId) ||
            shape is not (TimberbornInitialBodyShape.Tree or TimberbornInitialBodyShape.Crop or TimberbornInitialBodyShape.Vegetation or
                TimberbornInitialBodyShape.Structure or TimberbornInitialBodyShape.Stockpile) ||
            string.IsNullOrWhiteSpace(bodyProfile.SpecId) || string.IsNullOrWhiteSpace(bodyProfile.Type) ||
            bodyProfile.FuelValue > 15 || bodyProfile.Flammability > 3 || bodyProfile.DestructionThreshold < 0)
            throw new ArgumentException("Witness requires one exact supported static native body.");
        bool building = shape is TimberbornInitialBodyShape.Structure or TimberbornInitialBodyShape.Stockpile;
        if (building != (buildingCost is not null))
            throw new ArgumentException("Building witness requires its exact static cost, including empty cost.");
        var cells = footprint.OrderBy(c => c.Z).ThenBy(c => c.Y).ThenBy(c => c.X).ToArray();
        if (cells.Length == 0 || cells.Any(c => c.X < 0 || c.Y < 0 || c.Z < 0) || cells.Distinct().Count() != cells.Length)
            throw new ArgumentException("Static native footprint must be complete and unique.");
        var named = yields.OrderBy(y => y.ComponentName, StringComparer.Ordinal).ToArray();
        if (named.Select(y => y.ComponentName).Distinct(StringComparer.Ordinal).Count() != named.Length)
            throw new ArgumentException("Static named roles must be unique.");
        var cost = buildingCost?.OrderBy(s => s.ResourceId, StringComparer.Ordinal).ToArray();
        if (cost is not null && (cost.Any(s => string.IsNullOrWhiteSpace(s.ResourceId) || s.Amount < 0) ||
            cost.Select(s => s.ResourceId).Distinct(StringComparer.Ordinal).Count() != cost.Length))
            throw new ArgumentException("Static construction costs must have unique resource ids.");
        EntityId = entityId;
        SpecId = specId;
        Shape = shape;
        BodyProfile = bodyProfile;
        LocalFootprint = Array.AsReadOnly(cells);
        Yields = Array.AsReadOnly(named);
        BuildingCost = cost is null ? null : Array.AsReadOnly(cost);
    }
    public Guid EntityId { get; }
    public string SpecId { get; }
    public TimberbornInitialBodyShape Shape { get; }
    public TimberbornBurnableProfile BodyProfile { get; }
    public IReadOnlyList<TimberbornCellCoordinates> LocalFootprint { get; }
    public IReadOnlyList<OwnedNamedYieldDefinition> Yields { get; }
    public IReadOnlyList<TimberbornBurnDamageResourceStack>? BuildingCost { get; }
    public NativeBurnTargetFamily Family => Shape switch
    {
        TimberbornInitialBodyShape.Tree => NativeBurnTargetFamily.Tree,
        TimberbornInitialBodyShape.Structure => NativeBurnTargetFamily.Structure,
        TimberbornInitialBodyShape.Stockpile => NativeBurnTargetFamily.Stockpile,
        _ => NativeBurnTargetFamily.Crop,
    };
    internal static OwnedNativeDefinitionWitness Capture(TimberbornInitialMaterialBody body) => new(body.EntityId, body.SpecId, body.Shape,
        body.BodyProfile, body.Footprint.Select(slot => slot.LocalCoordinates), body.Yields.Select(y => new OwnedNamedYieldDefinition(
            y.Role, y.ComponentName, y.DeclaredGoodId, y.DeclaredAmount, y.RemoveOnCut)), body.ConstructionResources);

    internal bool Matches(OwnedNativeDefinitionWitness other) => EntityId == other.EntityId && SpecId == other.SpecId &&
        Shape == other.Shape && BodyProfile == other.BodyProfile && LocalFootprint.SequenceEqual(other.LocalFootprint) &&
        Yields.SequenceEqual(other.Yields) && (BuildingCost is null ? other.BuildingCost is null :
            other.BuildingCost is not null && BuildingCost.SequenceEqual(other.BuildingCost));
}

/// <summary>Exactly the retained owners' static evidence; absence of this collection means unavailable legacy evidence.</summary>
public sealed class OwnedNativeDefinitionSet
{
    private readonly IReadOnlyDictionary<Guid, OwnedNativeDefinitionWitness> _byId;
    public OwnedNativeDefinitionSet(IEnumerable<OwnedNativeDefinitionWitness> definitions)
    {
        var values = definitions.OrderBy(d => d.EntityId).ToArray();
        if (values.Select(d => d.EntityId).Distinct().Count() != values.Length)
            throw new ArgumentException("Duplicate native witness.");
        Definitions = Array.AsReadOnly(values);
        _byId = values.ToDictionary(d => d.EntityId);
    }
    public IReadOnlyList<OwnedNativeDefinitionWitness> Definitions { get; }
    internal OwnedNativeDefinitionWitness Get(Guid id) => _byId.TryGetValue(id, out var witness) ? witness :
        throw new ArgumentException("Retained owner has no static native witness.");
    internal void Validate(IReadOnlyList<OwnedConsequenceOwner> owners)
    {
        var retained = owners.Where(o => o.Retention == OwnedBodyRetention.RetainedBody).ToArray();
        if (retained.Length != Definitions.Count || retained.Any(o => !_byId.TryGetValue(o.EntityId, out var d) ||
            d.SpecId != o.Profile!.SpecId || d.Family != o.Family))
            throw new ArgumentException("Native witnesses must match exactly the retained owner identities.");
    }
    internal OwnedNativeDefinitionSet ForRetained(IReadOnlyList<OwnedConsequenceOwner> owners) =>
        new(owners.Where(o => o.Retention == OwnedBodyRetention.RetainedBody).Select(o => Get(o.EntityId)));
}
