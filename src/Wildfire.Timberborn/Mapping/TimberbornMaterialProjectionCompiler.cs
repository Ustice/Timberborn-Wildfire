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
        TimberbornInventoryDeclarationCapture.RequireMaterialSupport(body.Shape,
            body.Inventories.Select(inventory => inventory.Declaration).ToArray());
        // Material denotes each present good type once; selected accounting retains exact named quantities.
        var parts = new List<TimberbornMaterialPart> { bodyPart };
        foreach (var goodId in body.Inventories.SelectMany(inventory => inventory.Stock).Select(good => good.ResourceId)
            .Distinct(StringComparer.Ordinal).OrderBy(goodId => goodId, StringComparer.Ordinal))
        {
            RequireGood(goodId);
            parts.Add(TimberbornMaterialPart.StoredGood(goodId));
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
