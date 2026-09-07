namespace Wildfire.Timberborn.Consequences;

internal readonly record struct TimberbornOwnedStorageEffects(int NotLive, int Unavailable, int Removed,
    int Hazardous, int Blasts, int Pulses, int Unknown, int NonBurnable);

/// <summary>Storage effects after shared body damage. Caller owns the runtime resource guard for the whole operation.</summary>
internal sealed class TimberbornOwnedStorageBurnSink
{
    private readonly ITimberbornOwnedStorageInventoryApi _inventory;
    private readonly ITimberbornStoredGoodHazardConsequenceSink _hazards;
    private readonly TimberbornResourceFuelCatalog _catalog;
    private readonly TimberbornStoredGoodFuelBudget _credit = new();
    internal TimberbornOwnedStorageBurnSink(ITimberbornOwnedStorageInventoryApi inventory,
        ITimberbornStoredGoodHazardConsequenceSink hazards, TimberbornResourceFuelCatalog catalog)
    { _inventory = inventory; _hazards = hazards; _catalog = catalog; }
    internal OwnedStorageCredit[] CaptureCredit(IReadOnlyList<OwnedConsequenceOwner> owners) => _credit.Capture(owners, _catalog);
    internal void RestoreCredit(TimberbornOwnedConsequenceSnapshot history) => _credit.Restore(history, _catalog);
    internal bool HasTransientFuelCredit => _credit.HasCredit;
    internal bool IsLive(TimberbornOwnedBurnDecision item) =>
        _inventory.Read(Owner(item)).Status != TimberbornOwnedInventoryStatus.NotLive;

    internal TimberbornOwnedStorageEffects ApplyOwnedConsequences(uint tick, IReadOnlyList<TimberbornOwnedBurnDecision> decisions)
    {
        var results = decisions.GroupBy(item => item.EntityId).Select(group => group.OrderByDescending(item =>
            Math.Max(0, item.Decision.OldFuel - item.Decision.NewFuel)).ThenByDescending(item => item.Decision.NewHeat)
            .ThenBy(item => item.Decision.CellIndex).First()).Select(item => ApplyOwner(tick, item)).ToArray();
        return new(results.Sum(r => r.NotLive), results.Sum(r => r.Unavailable), results.Sum(r => r.Removed),
            results.Sum(r => r.Hazardous), results.Sum(r => r.Blasts), results.Sum(r => r.Pulses),
            results.Sum(r => r.Unknown), results.Sum(r => r.NonBurnable));
    }

    private TimberbornOwnedStorageEffects ApplyOwner(uint tick, TimberbornOwnedBurnDecision item)
    {
        var consequence = TimberbornStoredGoodBurnConsequence.FromDecision(tick, item.Decision);
        if (!consequence.ShouldBurnStoredGoods) return default;
        var owner = Owner(item);
        // Earlier owner callbacks may have removed this entity or changed its stock.
        var inventory = _inventory.Read(owner);
        if (inventory.Status == TimberbornOwnedInventoryStatus.NotLive) return new(NotLive: 1, 0, 0, 0, 0, 0, 0, 0);
        if (inventory.Status != TimberbornOwnedInventoryStatus.Available) return new(0, Unavailable: 1, 0, 0, 0, 0, 0, 0);
        var plan = TimberbornOwnedStoredGoodPlan.Create(owner, inventory.Stacks, consequence.BurnBudget, _credit, _catalog);
        var actual = new List<TimberbornStoredGoodStack>();
        int notLive = 0, unavailable = 0;
        foreach (var requested in plan.Requested)
        {
            var removal = _inventory.Consume(owner, requested);
            if (removal.RemovedAmount < 0 || removal.RemovedAmount > requested.Amount ||
                (removal.Status != TimberbornOwnedInventoryStatus.Available && removal.RemovedAmount != 0))
                throw new InvalidOperationException("Native storage returned an inconsistent consumption receipt.");
            if (removal.RemovedAmount > 0) actual.Add(requested with { Amount = removal.RemovedAmount });
            if (removal.Status == TimberbornOwnedInventoryStatus.NotLive) { notLive++; break; }
            if (removal.Status != TimberbornOwnedInventoryStatus.Available) { unavailable++; break; }
        }
        var hazardous = plan.Hazards(actual);
        var hazards = TimberbornStoredGoodHazardConsequenceResult.Empty;
        if (hazardous.Length > 0)
        {
            var target = new TimberbornStoredGoodBurnTarget(owner.TargetKey.StableId, actual, CanMutateInventory: true);
            hazards = _hazards.ApplyHazards(target, consequence, hazardous);
        }
        return new(notLive, unavailable, actual.Sum(stack => stack.Amount), hazardous.Sum(stack => stack.Amount),
            hazards.ExplosiveBlastTriggeredCount, hazards.ContaminationPulseCellCount, plan.UnknownResources, plan.NonBurnableItems);
    }

    private static TimberbornOwnedStorageRegistration Owner(TimberbornOwnedBurnDecision item)
    {
        var role = item.Family switch
        {
            NativeBurnTargetFamily.Stockpile => TimberbornOwnedInventoryRole.Stockpile,
            NativeBurnTargetFamily.Structure => TimberbornOwnedInventoryRole.SimpleOutput,
            _ => throw new InvalidOperationException("Non-storage family reached the owned storage sink."),
        };
        var owner = new TimberbornOwnedStorageRegistration(item.EntityId, role);
        if (owner.TargetKey != item.TargetKey) throw new InvalidOperationException("Storage body identity differs from its inventory owner.");
        return owner;
    }
}
