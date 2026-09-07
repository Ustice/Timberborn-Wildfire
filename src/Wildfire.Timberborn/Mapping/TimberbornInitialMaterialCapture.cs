using Wildfire.Core;

namespace Wildfire.Timberborn.Mapping;

public enum TimberbornCapturedYieldRole { Cuttable, Gatherable, Unclassified }
public enum TimberbornCapturedInventoryRole { Stockpile, SimpleOutput, GoodStack }
public enum TimberbornInitialBodyShape { Structure, Stockpile, Tree, Crop, Vegetation, Infrastructure, GoodStack, Unknown }
public enum TimberbornInitialCompositionGap
{
    UnknownBodyProfile, UnknownGoodProfile, UnsupportedBodyFamily, MultipleInventoryRoles,
    NaturalBudgetUnresolved, MultipleNamedYields, UnclassifiedYield, VegetationProfileMismatch,
    InventoryUnavailable, YieldUnavailable, PhysicalReconstructionUnavailable,
}

/// <summary>Native material input, not current GPU fuel. Zero actual yield never becomes the declared yield.</summary>
public sealed record TimberbornNamedYieldMaterial
{
    public TimberbornNamedYieldMaterial(TimberbornCapturedYieldRole role, string componentName, string actualGoodId,
        int actualAmount, string declaredGoodId, int declaredAmount, bool removeOnCut, bool yieldEnabled)
    {
        if (!Enum.IsDefined(typeof(TimberbornCapturedYieldRole), role) || string.IsNullOrWhiteSpace(componentName) ||
            string.IsNullOrWhiteSpace(declaredGoodId) || actualAmount < 0 || declaredAmount < 0 ||
            ((actualAmount > 0 || !string.IsNullOrEmpty(actualGoodId)) && actualGoodId != declaredGoodId) ||
            (removeOnCut && role != TimberbornCapturedYieldRole.Cuttable))
            throw new ArgumentException("Named yield must retain a valid native identity and actual amount.");
        Role = role; ComponentName = componentName; ActualGoodId = actualGoodId ?? ""; ActualAmount = actualAmount;
        DeclaredGoodId = declaredGoodId; DeclaredAmount = declaredAmount; RemoveOnCut = removeOnCut; YieldEnabled = yieldEnabled;
        GoodProfile = TimberbornResourceFuelCatalog.Default.Lookup(declaredGoodId);
    }
    public TimberbornCapturedYieldRole Role { get; }
    public string ComponentName { get; }
    public string ActualGoodId { get; }
    public int ActualAmount { get; }
    public string DeclaredGoodId { get; }
    public int DeclaredAmount { get; }
    public bool RemoveOnCut { get; }
    /// <summary>When false, native Yield reports zero without proving that retained physical yield is empty.</summary>
    public bool YieldEnabled { get; }
    public TimberbornResourceFuelProfile GoodProfile { get; }
}

/// <summary>All physical stock in one exact native role, including reserved stock. Never a mutation allowance.</summary>
public sealed class TimberbornInventoryMaterial
{
    public TimberbornInventoryMaterial(TimberbornCapturedInventoryRole role, bool enabled,
        IEnumerable<TimberbornStoredGoodStack> stock)
    {
        if (!Enum.IsDefined(typeof(TimberbornCapturedInventoryRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
        var values = stock.ToArray();
        if (values.Any(good => string.IsNullOrWhiteSpace(good.ResourceId) || good.Amount < 0))
            throw new ArgumentException("Native inventory capture contains malformed stock.", nameof(stock));
        Role = role; Enabled = enabled;
        Stock = Array.AsReadOnly(values.Where(good => good.Amount > 0).OrderBy(good => good.ResourceId, StringComparer.Ordinal).ToArray());
        if (Stock.Select(good => good.ResourceId).Distinct(StringComparer.Ordinal).Count() != Stock.Count)
            throw new ArgumentException("Native inventory contains duplicate resource entries.", nameof(stock));
        Goods = Array.AsReadOnly(Stock.Select(good => TimberbornResourceFuelCatalog.Default.Lookup(good.ResourceId)).ToArray());
    }
    public TimberbornCapturedInventoryRole Role { get; }
    public bool Enabled { get; }
    public IReadOnlyList<TimberbornStoredGoodStack> Stock { get; }
    public IReadOnlyList<TimberbornResourceFuelProfile> Goods { get; }
}

/// <summary>One physical body with all captured native parts. No token allocation, fuel composition or registration.</summary>
public sealed class TimberbornInitialMaterialBody
{
    public TimberbornInitialMaterialBody(Guid entityId, string specId, TimberbornInitialBodyShape shape,
        IEnumerable<TimberbornMaterialFootprintSlot> footprint, IEnumerable<TimberbornNamedYieldMaterial> yields,
        IEnumerable<TimberbornInventoryMaterial> inventories, IEnumerable<TimberbornBurnDamageResourceStack>? constructionResources)
    {
        if (entityId == Guid.Empty || string.IsNullOrWhiteSpace(specId) || !Enum.IsDefined(typeof(TimberbornInitialBodyShape), shape))
            throw new ArgumentException("Initial material capture requires an exact native body identity.");
        if (constructionResources is null && shape is TimberbornInitialBodyShape.Structure or TimberbornInitialBodyShape.Stockpile)
            throw new ArgumentException("A native building requires captured construction costs, including an explicitly empty cost.");
        ConstructionResources = constructionResources is null ? null : CopyConstruction(constructionResources);
        var slots = footprint.ToArray(); var yieldParts = yields.OrderBy(yield => yield.ComponentName, StringComparer.Ordinal).ToArray();
        var inventoryParts = inventories.OrderBy(inventory => inventory.Role).ToArray();
        if (slots.Length == 0 || slots.Any(slot => slot.CellIndex < 0 || slot.LocalCoordinates.X < 0 || slot.LocalCoordinates.Y < 0 || slot.LocalCoordinates.Z < 0) ||
            slots.Select(slot => slot.LocalCoordinates).Distinct().Count() != slots.Length || slots.Select(slot => slot.CellIndex).Distinct().Count() != slots.Length)
            throw new ArgumentException("Initial body requires a complete bijective native footprint.");
        if (yieldParts.Select(yield => yield.ComponentName).Distinct(StringComparer.Ordinal).Count() != yieldParts.Length ||
            inventoryParts.Select(inventory => inventory.Role).Distinct().Count() != inventoryParts.Length)
            throw new ArgumentException("Initial body contains ambiguous native component roles.");
        EntityId = entityId; SpecId = specId; Shape = shape;
        Footprint = Array.AsReadOnly(slots.OrderBy(slot => slot.LocalCoordinates.Z).ThenBy(slot => slot.LocalCoordinates.Y).ThenBy(slot => slot.LocalCoordinates.X).ToArray());
        Yields = Array.AsReadOnly(yieldParts); Inventories = Array.AsReadOnly(inventoryParts);
        BodyProfile = TimberbornBurnableCatalog.Default.Lookup(specId);
        Family = shape switch { TimberbornInitialBodyShape.Structure => NativeBurnTargetFamily.Structure,
            TimberbornInitialBodyShape.Stockpile => NativeBurnTargetFamily.Stockpile, TimberbornInitialBodyShape.Tree => NativeBurnTargetFamily.Tree,
            TimberbornInitialBodyShape.Crop or TimberbornInitialBodyShape.Vegetation => NativeBurnTargetFamily.Crop,
            TimberbornInitialBodyShape.Infrastructure => TimberbornInfrastructureNameClassifier.IsPowerInfrastructureName(specId) ? NativeBurnTargetFamily.PowerInfrastructure :
                TimberbornInfrastructureNameClassifier.IsWaterInfrastructureName(specId) ? NativeBurnTargetFamily.WaterInfrastructure : NativeBurnTargetFamily.PathInfrastructure,
            _ => (NativeBurnTargetFamily?)null };
        PhysicalBodyKind = shape switch { TimberbornInitialBodyShape.Structure or TimberbornInitialBodyShape.Stockpile => TimberbornBurnDamageTargetKind.Structure,
            TimberbornInitialBodyShape.Tree => TimberbornBurnDamageTargetKind.Tree, TimberbornInitialBodyShape.Crop => TimberbornBurnDamageTargetKind.Crop,
            TimberbornInitialBodyShape.Vegetation => TimberbornBurnDamageTargetKind.Resource, TimberbornInitialBodyShape.Infrastructure => TimberbornBurnDamageTargetKind.Infrastructure,
            TimberbornInitialBodyShape.GoodStack => TimberbornBurnDamageTargetKind.Storage, _ => TimberbornBurnDamageTargetKind.Unknown };
        CompositionGaps = Array.AsReadOnly(FindGaps().Distinct().ToArray());
    }
    public Guid EntityId { get; }
    public string SpecId { get; }
    public TimberbornInitialBodyShape Shape { get; }
    public NativeBurnTargetFamily? Family { get; }
    public TimberbornBurnDamageTargetKind PhysicalBodyKind { get; }
    public TimberbornBurnableProfile BodyProfile { get; }
    public IReadOnlyList<TimberbornMaterialFootprintSlot> Footprint { get; }
    public IReadOnlyList<TimberbornNamedYieldMaterial> Yields { get; }
    public IReadOnlyList<TimberbornInventoryMaterial> Inventories { get; }
    public IReadOnlyList<TimberbornInitialCompositionGap> CompositionGaps { get; }
    public IReadOnlyList<TimberbornBurnDamageResourceStack>? ConstructionResources { get; }

    // Compare one staging operation's native reads, never current quantities against saved accounting.
    internal bool SameReadings(TimberbornInitialMaterialBody other) =>
        EntityId == other.EntityId && SpecId == other.SpecId && Shape == other.Shape &&
        Footprint.SequenceEqual(other.Footprint) && Yields.SequenceEqual(other.Yields) &&
        (ConstructionResources is null ? other.ConstructionResources is null :
            other.ConstructionResources is not null && ConstructionResources.SequenceEqual(other.ConstructionResources)) &&
        Inventories.Count == other.Inventories.Count && Inventories.Select((inventory, index) =>
            inventory.Role == other.Inventories[index].Role && inventory.Enabled == other.Inventories[index].Enabled &&
            inventory.Stock.SequenceEqual(other.Inventories[index].Stock)).All(equal => equal);

    private static IReadOnlyList<TimberbornBurnDamageResourceStack> CopyConstruction(IEnumerable<TimberbornBurnDamageResourceStack> values)
    {
        var costs = values.OrderBy(value => value.ResourceId, StringComparer.Ordinal).ToArray();
        if (costs.Any(value => string.IsNullOrWhiteSpace(value.ResourceId) || value.Amount < 0) ||
            costs.Select(value => value.ResourceId).Distinct(StringComparer.Ordinal).Count() != costs.Length)
            throw new ArgumentException("Native building costs require unique valid goods.");
        return Array.AsReadOnly(costs);
    }

    private IEnumerable<TimberbornInitialCompositionGap> FindGaps()
    {
        if (!BodyProfile.Known) yield return TimberbornInitialCompositionGap.UnknownBodyProfile;
        if (Family is null || Shape == TimberbornInitialBodyShape.Infrastructure) yield return TimberbornInitialCompositionGap.UnsupportedBodyFamily;
        if (Inventories.Count > 1) yield return TimberbornInitialCompositionGap.MultipleInventoryRoles;
        if (Yields.Count > 0) yield return TimberbornInitialCompositionGap.NaturalBudgetUnresolved;
        if (Yields.Any(yield => !yield.YieldEnabled)) yield return TimberbornInitialCompositionGap.YieldUnavailable;
        if (Yields.Count > 1) yield return TimberbornInitialCompositionGap.MultipleNamedYields;
        if (Yields.Any(yield => yield.Role == TimberbornCapturedYieldRole.Unclassified)) yield return TimberbornInitialCompositionGap.UnclassifiedYield;
        if (Yields.Any(yield => !yield.GoodProfile.Known) || Inventories.SelectMany(inventory => inventory.Goods).Any(good => !good.Known))
            yield return TimberbornInitialCompositionGap.UnknownGoodProfile;
        if (Shape == TimberbornInitialBodyShape.Vegetation) yield return TimberbornInitialCompositionGap.VegetationProfileMismatch;
        if (Inventories.Any(inventory => !inventory.Enabled)) yield return TimberbornInitialCompositionGap.InventoryUnavailable;
        if (PhysicalBodyKind == TimberbornBurnDamageTargetKind.Structure) yield return TimberbornInitialCompositionGap.PhysicalReconstructionUnavailable;
    }
}

/// <summary>Native source footprint retained separately for later unowned environmental initialization.</summary>
public sealed class TimberbornInitialWaterSource
{
    public TimberbornInitialWaterSource(Guid entityId, string specId, bool badwater, IEnumerable<TimberbornMaterialFootprintSlot> footprint)
    {
        var slots = footprint.ToArray();
        if (entityId == Guid.Empty || string.IsNullOrWhiteSpace(specId) || slots.Length == 0 ||
            slots.Any(slot => slot.CellIndex < 0 || slot.LocalCoordinates.X < 0 || slot.LocalCoordinates.Y < 0 || slot.LocalCoordinates.Z < 0) ||
            slots.Select(slot => slot.LocalCoordinates).Distinct().Count() != slots.Length || slots.Select(slot => slot.CellIndex).Distinct().Count() != slots.Length)
            throw new ArgumentException("Water source requires a complete native footprint and identity.");
        EntityId = entityId; SpecId = specId; Badwater = badwater; Footprint = Array.AsReadOnly(slots);
    }
    public Guid EntityId { get; }
    public string SpecId { get; }
    public bool Badwater { get; }
    public IReadOnlyList<TimberbornMaterialFootprintSlot> Footprint { get; }
}

public enum TimberbornInitialCaptureExclusion { Uninitialized, Deleted, Unpositioned, Unfinished, Preview, TreeLeftover, EnvironmentalWaterSource }
public sealed record TimberbornInitialExcludedEntity(Guid EntityId, string SpecId, TimberbornInitialCaptureExclusion Reason);
public sealed class TimberbornInitialWorldCapture
{
    public TimberbornInitialWorldCapture(FireGrid grid, IEnumerable<TimberbornInitialMaterialBody> bodies,
        IEnumerable<TimberbornInitialExcludedEntity> excluded, IEnumerable<TimberbornInitialWaterSource> waterSources,
        TimberbornInitialEnvironmentCapture environment)
    {
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        if (environment.Grid != grid) throw new ArgumentException("Body and environmental captures must share a grid.");
        var captured = bodies.OrderBy(body => body.EntityId).ToArray(); var omitted = excluded.OrderBy(entity => entity.EntityId).ToArray();
        var water = waterSources.OrderBy(source => source.EntityId).ToArray();
        if (captured.Select(body => body.EntityId).Concat(omitted.Select(entity => entity.EntityId)).Concat(water.Select(source => source.EntityId)).Distinct().Count() != captured.Length + omitted.Length + water.Length)
            throw new ArgumentException("Native snapshot contains duplicate entity identities.");
        if (captured.SelectMany(body => body.Footprint).Concat(water.SelectMany(source => source.Footprint)).Any(slot => slot.CellIndex >= grid.CellCount))
            throw new ArgumentOutOfRangeException(nameof(bodies), "A native footprint is outside the complete grid.");
        Grid = grid; Bodies = Array.AsReadOnly(captured); Excluded = Array.AsReadOnly(omitted); WaterSources = Array.AsReadOnly(water);
    }
    public FireGrid Grid { get; }
    public IReadOnlyList<TimberbornInitialMaterialBody> Bodies { get; }
    public IReadOnlyList<TimberbornInitialExcludedEntity> Excluded { get; }
    public IReadOnlyList<TimberbornInitialWaterSource> WaterSources { get; }
    public TimberbornInitialEnvironmentCapture Environment { get; }
    internal bool SameReadings(TimberbornInitialWorldCapture other) => Grid == other.Grid &&
        Bodies.Count == other.Bodies.Count && Bodies.Select((body, index) => body.SameReadings(other.Bodies[index])).All(equal => equal) &&
        Excluded.SequenceEqual(other.Excluded) && WaterSources.Count == other.WaterSources.Count &&
        WaterSources.Select((source, index) => source.EntityId == other.WaterSources[index].EntityId &&
            source.SpecId == other.WaterSources[index].SpecId && source.Badwater == other.WaterSources[index].Badwater &&
            source.Footprint.SequenceEqual(other.WaterSources[index].Footprint)).All(equal => equal) &&
        Environment.SameReadings(other.Environment);

}
