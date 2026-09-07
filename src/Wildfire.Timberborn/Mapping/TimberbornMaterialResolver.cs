using Wildfire.Core;

namespace Wildfire.Timberborn.Mapping;

internal static class TimberbornMaterialResolver
{
    internal static TimberbornResolvedMaterialCell Resolve(int cellIndex, bool solidTerrain,
        IEnumerable<TimberbornMaterialContributor> contributors)
    {
        var retained = contributors.OrderBy(contributor => contributor.Owner.EntityId).ToArray();
        var candidates = retained.Select(Compose).OrderByDescending(candidate => candidate.Priority)
            .ThenByDescending(candidate => candidate.Fuel).ThenByDescending(candidate => candidate.Flammability)
            .ThenBy(candidate => candidate.Owner.EntityId).ToArray();
        if (candidates.Length == 0)
            return new TimberbornResolvedMaterialCell(cellIndex, null, PackedCell.Pack(0, 0, 0, 0, solidTerrain ? 1 : 0, 0),
                WildfireMaterialFieldSchema.Default.Lookup(solidTerrain ? WildfireMaterialClass.Terrain : WildfireMaterialClass.Empty), false, Array.AsReadOnly(retained));
        var selected = candidates[0];
        if (selected.Priority >= 3 && retained.Any(contributor => contributor.Owner.EntityId != selected.Owner.EntityId &&
                contributor.Parts.Any(part => part.IsStorage)))
            throw new InvalidOperationException($"Cell {cellIndex} would mix structure and stored fuel from different native owners.");
        return new TimberbornResolvedMaterialCell(cellIndex, selected.Owner,
            PackedCell.Pack(selected.Fuel, 0, selected.Flammability, 0, 1, 0),
            WildfireMaterialFieldSchema.Default.Lookup(selected.MaterialClass), selected.Composite, Array.AsReadOnly(retained));
    }

    private static Candidate Compose(TimberbornMaterialContributor contributor)
    {
        var ordered = contributor.Parts.OrderByDescending(part => part.Priority).ThenByDescending(part => part.InitialFuel)
            .ThenByDescending(part => part.Flammability).ThenBy(part => part.Key, StringComparer.Ordinal).ToArray();
        var main = ordered[0];
        var storage = ordered.Where(part => part.IsStorage).ToArray();
        bool composite = main.Priority >= 3 && storage.Length > 0;
        // Preserve current INITIAL scalar structure+storage semantics only for the same native entity.
        // This does not divide already-burnt fuel by inventory good or permit a live stock/profile refill.
        int fuel = composite ? Math.Min(15, main.InitialFuel + storage.Sum(part => (int)part.InitialFuel)) : main.InitialFuel;
        int flammability = composite ? Math.Max(main.Flammability, storage.Max(part => part.Flammability)) : main.Flammability;
        return new Candidate(contributor.Owner, main.Priority, fuel, flammability, main.MaterialClass, composite);
    }

    private sealed record Candidate(TimberbornMaterialOwner Owner, int Priority, int Fuel, int Flammability,
        WildfireMaterialClass MaterialClass, bool Composite);
}
