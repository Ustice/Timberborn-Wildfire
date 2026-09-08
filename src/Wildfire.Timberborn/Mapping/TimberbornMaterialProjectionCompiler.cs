namespace Wildfire.Timberborn.Mapping;

/// <summary>
/// Current native material definitions and complete local footprint only. This neither reconstructs
/// body accounting nor supplies authoritative remaining GPU fuel for a previously activated slot.
/// </summary>
internal static class TimberbornMaterialProjectionCompiler
{
    internal static TimberbornMaterialProjection Compile(TimberbornInitialMaterialBody body)
    {
        if (body is null) throw new ArgumentNullException(nameof(body));
        if (!body.BodyProfile.Known) throw new NotSupportedException("Native body has no known material profile.");
        var bodyPart = body.Shape switch
        {
            TimberbornInitialBodyShape.Tree => TimberbornMaterialPart.Tree(body.SpecId),
            TimberbornInitialBodyShape.Crop => TimberbornMaterialPart.Crop(body.SpecId),
            TimberbornInitialBodyShape.Vegetation => TimberbornMaterialPart.Vegetation(body.SpecId),
            TimberbornInitialBodyShape.Structure or TimberbornInitialBodyShape.Stockpile => TimberbornMaterialPart.Building(body.SpecId),
            _ => throw new NotSupportedException("Native body family has no complete owned consequence route."),
        };
        if (body.Inventories.Count > 1)
            throw new NotSupportedException("Material composition does not yet support multiple native inventory roles.");
        if (body.Inventories.Any(inventory => inventory.Declaration.Role is not (TimberbornNativeInventoryRole.Stockpile or
            TimberbornNativeInventoryRole.SimpleOutput or TimberbornNativeInventoryRole.GoodStack)))
            throw new NotSupportedException("Captured inventory role has no admitted material/effect route.");
        // Physical stock is material even when a separate initial accounting selection excludes it.
        var parts = new List<TimberbornMaterialPart> { bodyPart };
        foreach (var inventory in body.Inventories)
            foreach (var good in inventory.Stock)
            {
                RequireGood(good.ResourceId);
                parts.Add(TimberbornMaterialPart.StoredGood(good.ResourceId));
            }
        return new(body.EntityId, body.Footprint, parts);
    }

    internal static void RequireGood(string good)
    {
        var profile = TimberbornResourceFuelCatalog.Default.Lookup(good);
        if (!profile.Known || profile.ResourceId != good)
            throw new NotSupportedException("Captured native resource has no exact known material definition.");
    }
}
