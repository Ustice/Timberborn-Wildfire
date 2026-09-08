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
    private readonly Func<TimberbornOwnedBurnDecision, TimberbornOwnedStorageRegistration?> _owner;
    internal TimberbornOwnedStorageBurnSink(ITimberbornOwnedStorageInventoryApi inventory,
        ITimberbornStoredGoodHazardConsequenceSink hazards, TimberbornResourceFuelCatalog catalog,
        Func<TimberbornOwnedBurnDecision, TimberbornOwnedStorageRegistration?> owner)
    { _inventory = inventory; _hazards = hazards; _catalog = catalog; _owner = owner; }
    internal OwnedStorageCredit[] CaptureCredit(IReadOnlyList<OwnedConsequenceOwner> owners) => _credit.Capture(owners, _catalog);
    internal void RestoreCredit(TimberbornOwnedConsequenceSnapshot history) => _credit.Restore(history, _catalog);
    internal bool HasTransientFuelCredit => _credit.HasCredit;
    internal bool IsLive(TimberbornOwnedBurnDecision item) =>
        _owner(item) is not { } owner || ReadOwner(owner).Status != TimberbornOwnedInventoryStatus.NotLive;
    internal void Preflight(IEnumerable<TimberbornOwnedBurnDecision> decisions)
    {
        foreach (var item in decisions.GroupBy(item => item.EntityId).Select(group => group.First()))
            if (_owner(item) is { } owner) _ = ReadOwner(owner);
    }

    internal TimberbornOwnedStorageEffects ApplyOwnedConsequences(uint tick, IReadOnlyList<TimberbornOwnedBurnDecision> decisions)
    {
        // Stable spatial order, retaining emission order within a cell. Each completion spends
        // actual current stock and attributes its hazards to this contribution's originating cell.
        var results = decisions.OrderBy(item => item.Decision.CellIndex)
            .Select(item => ApplyOwner(tick, item)).ToArray();
        return new(results.Sum(r => r.NotLive), results.Sum(r => r.Unavailable), results.Sum(r => r.Removed),
            results.Sum(r => r.Hazardous), results.Sum(r => r.Blasts), results.Sum(r => r.Pulses),
            results.Sum(r => r.Unknown), results.Sum(r => r.NonBurnable));
    }

    private TimberbornOwnedStorageEffects ApplyOwner(uint tick, TimberbornOwnedBurnDecision item)
    {
        var consequence = TimberbornStoredGoodBurnConsequence.FromDecision(tick, item.Decision);
        if (!consequence.ShouldBurnStoredGoods) return default;
        var owner = _owner(item);
        if (owner is null) return new(0, Unavailable: 1, 0, 0, 0, 0, 0, 0);
        // Earlier owner callbacks may have removed this entity or changed its stock.
        var inventory = ReadOwner(owner);
        if (inventory.Status == TimberbornOwnedInventoryStatus.NotLive) return new(NotLive: 1, 0, 0, 0, 0, 0, 0, 0);
        if (inventory.Status != TimberbornOwnedInventoryStatus.Available) return new(0, Unavailable: 1, 0, 0, 0, 0, 0, 0);
        var available = inventory.Inventories.Where(row => row.Status == TimberbornOwnedInventoryStatus.Available).ToArray();
        var stacks = available.SelectMany(row => row.Stacks).GroupBy(stack => stack.ResourceId, StringComparer.Ordinal)
            .Select(group => new TimberbornStoredGoodStack(group.Key, group.Aggregate(0, (sum, stack) => checked(sum + stack.Amount)))).ToArray();
        var plan = TimberbornOwnedStoredGoodPlan.Create(owner, stacks, consequence.BurnBudget, _credit, _catalog);
        var withdrawals = SplitWithdrawals(plan.Requested, available);
        var actual = new List<TimberbornStoredGoodStack>();
        int notLive = 0, unavailable = inventory.Inventories.Count(row => row.Status != TimberbornOwnedInventoryStatus.Available);
        foreach (var withdrawal in withdrawals)
        {
            var requested = withdrawal.Stack;
            var removal = _inventory.Consume(owner, withdrawal.Declaration, requested);
            if (!Enum.IsDefined(typeof(TimberbornOwnedInventoryStatus), removal.Status) || removal.RemovedAmount < 0 || removal.RemovedAmount > requested.Amount ||
                (removal.Status != TimberbornOwnedInventoryStatus.Available && removal.RemovedAmount != 0))
                throw new InvalidOperationException("Native storage returned an inconsistent consumption receipt.");
            if (removal.RemovedAmount > 0) actual.Add(requested with { Amount = removal.RemovedAmount });
            if (removal.Status == TimberbornOwnedInventoryStatus.NotLive) { notLive++; break; }
            // Original topology is revalidated by Consume. A newly disabled inventory cannot
            // prevent later eligible withdrawals that already own part of this fixed plan.
            if (removal.Status == TimberbornOwnedInventoryStatus.Unavailable) unavailable++;
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

    private TimberbornOwnedInventorySnapshot ReadOwner(TimberbornOwnedStorageRegistration owner)
    {
        var snapshot = _inventory.Read(owner);
        if (!Enum.IsDefined(typeof(TimberbornOwnedInventoryStatus), snapshot.Status) ||
            (snapshot.Status != TimberbornOwnedInventoryStatus.Available && snapshot.Inventories.Count != 0))
            throw new InvalidOperationException("Native inventory returned an invalid owner status.");
        if (snapshot.Status != TimberbornOwnedInventoryStatus.Available) return snapshot;
        if (!owner.Declarations.SequenceEqual(snapshot.Inventories.Select(row => row.Declaration)) ||
            snapshot.Inventories.Any(row => row.Status is not (TimberbornOwnedInventoryStatus.Available or TimberbornOwnedInventoryStatus.Unavailable) ||
                (row.Status != TimberbornOwnedInventoryStatus.Available && row.Stacks.Count != 0) ||
                row.Stacks.Any(stack => string.IsNullOrWhiteSpace(stack.ResourceId) || stack.Amount <= 0) ||
                row.Stacks.Select(stack => stack.ResourceId).Distinct(StringComparer.Ordinal).Count() != row.Stacks.Count))
            throw new InvalidOperationException("Native inventory rows differ from the exact owner declarations or contain invalid stock.");
        return snapshot;
    }

    internal static (TimberbornInventoryDeclaration Declaration, TimberbornStoredGoodStack Stack)[] SplitWithdrawals(
        IReadOnlyList<TimberbornStoredGoodStack> selected, IReadOnlyList<TimberbornOwnedInventoryRow> inventories)
    {
        var result = new List<(TimberbornInventoryDeclaration, TimberbornStoredGoodStack)>();
        foreach (var good in selected)
        {
            int remaining = good.Amount;
            foreach (var row in inventories.OrderBy(row => row.Declaration.Role).ThenBy(row => row.Declaration.ComponentName, StringComparer.Ordinal))
            {
                int amount = Math.Min(remaining, row.Stacks.Where(stack => stack.ResourceId == good.ResourceId).Sum(stack => stack.Amount));
                if (amount > 0) result.Add((row.Declaration, good with { Amount = amount }));
                remaining -= amount;
                if (remaining == 0) break;
            }
            if (remaining != 0) throw new InvalidOperationException("Selected owner stock exceeds captured named inventories.");
        }
        return result.ToArray();
    }
}
