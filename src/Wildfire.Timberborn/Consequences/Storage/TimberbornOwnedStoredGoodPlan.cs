namespace Wildfire.Timberborn.Consequences;

internal sealed record TimberbornOwnedStoredGoodPlan(TimberbornStoredGoodStack[] Requested,
    IReadOnlyDictionary<string, TimberbornResourceFuelProfile> Profiles, int UnknownResources, int NonBurnableItems)
{
    internal static TimberbornOwnedStoredGoodPlan Create(TimberbornOwnedStorageRegistration owner,
        IReadOnlyList<TimberbornStoredGoodStack> stacks, int budget, TimberbornStoredGoodFuelBudget credit,
        TimberbornResourceFuelCatalog catalog)
    {
        var classified = stacks.Where(stack => stack.Amount > 0)
            .Select(stack => new { Stack = stack, Profile = catalog.Lookup(stack.ResourceId) }).ToArray();
        var hazards = classified.Where(item => item.Profile.Known && (item.Profile.Explosive || item.Profile.Contaminated))
            .OrderByDescending(item => item.Profile.Explosive).ThenByDescending(item => item.Profile.Contaminated)
            .ThenByDescending(item => item.Profile.Flammability).ThenByDescending(item => item.Profile.FuelValue)
            .ThenBy(item => item.Stack.ResourceId, StringComparer.Ordinal).ToArray();
        var selectedHazards = credit.Select(owner.TargetKey.StableId, hazards.Where(item => item.Profile.FuelValue > 0)
            .Select(item => new TimberbornStoredGoodFuelStack(item.Stack, item.Profile.FuelValue)), budget);
        var hazardous = selectedHazards.Stacks.Concat(budget > 0 ? hazards.Where(item => item.Profile.Contaminated && item.Profile.FuelValue == 0)
                .Select(item => item.Stack) : Array.Empty<TimberbornStoredGoodStack>()).ToArray();
        var profiles = classified.ToDictionary(item => item.Stack.ResourceId, item => item.Profile, StringComparer.Ordinal);
        int remaining = selectedHazards.RemainingBudget; // Fractional reservations already own their share.
        var ordinary = classified.Where(item => item.Profile.Known && item.Profile.FuelValue > 0 &&
                !item.Profile.Explosive && !item.Profile.Contaminated)
            .OrderByDescending(item => item.Profile.Flammability).ThenByDescending(item => item.Profile.FuelValue)
            .ThenBy(item => item.Stack.ResourceId, StringComparer.Ordinal);
        var requested = hazardous.Concat(credit.Select(owner.TargetKey.StableId,
            ordinary.Select(item => new TimberbornStoredGoodFuelStack(item.Stack, item.Profile.FuelValue)), remaining).Stacks).ToArray();
        return new TimberbornOwnedStoredGoodPlan(requested, profiles, classified.Count(item => !item.Profile.Known),
            classified.Where(item => item.Profile.FuelValue == 0 && !item.Profile.Contaminated).Sum(item => item.Stack.Amount));
    }

    internal TimberbornStoredGoodHazardStack[] Hazards(IReadOnlyList<TimberbornStoredGoodStack> actuallyRemoved) => actuallyRemoved
        .Where(stack => Profiles[stack.ResourceId].Explosive || Profiles[stack.ResourceId].Contaminated)
        .Select(stack => new TimberbornStoredGoodHazardStack(stack.ResourceId, stack.Amount,
            Profiles[stack.ResourceId].FuelValue, Profiles[stack.ResourceId].Explosive, Profiles[stack.ResourceId].Contaminated)).ToArray();
}
