using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.SimpleOutputBuildings;
using Timberborn.Stockpiles;

namespace Wildfire.Timberborn.Consequences;

/// <summary>Native storage access by Guid and explicit inventory role, with no spatial or instance-cache fallback.</summary>
public sealed class TimberbornOwnedStorageInventoryApi : ITimberbornOwnedStorageInventoryApi
{
    private readonly EntityRegistry _entities;
    public TimberbornOwnedStorageInventoryApi(EntityRegistry entities) =>
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));

    public TimberbornOwnedInventorySnapshot Read(TimberbornOwnedStorageRegistration owner)
    {
        var inventory = Resolve(owner, out var status);
        return new TimberbornOwnedInventorySnapshot(status, inventory is null ? Array.Empty<TimberbornStoredGoodStack>() :
            inventory.UnreservedTakeableStock().Where(good => good.Amount > 0)
                .Select(good => new TimberbornStoredGoodStack(good.GoodId, good.Amount)).ToArray());
    }

    public TimberbornOwnedInventoryRemoval Consume(TimberbornOwnedStorageRegistration owner, TimberbornStoredGoodStack requested)
    {
        if (requested is null || string.IsNullOrWhiteSpace(requested.ResourceId) || requested.Amount <= 0)
            throw new ArgumentException("Consumption requires one positive identified good amount.", nameof(requested));
        // Re-resolve immediately before every native mutation: earlier stock callbacks can delete
        // the entity, change its inventory, disable it, or reserve the remaining goods.
        var inventory = Resolve(owner, out var status);
        if (inventory is null) return new TimberbornOwnedInventoryRemoval(status, 0);
        int amount = ConsumeUnreserved(inventory, requested);
        return new TimberbornOwnedInventoryRemoval(TimberbornOwnedInventoryStatus.Available, amount);
    }

    private static int ConsumeUnreserved(Inventory inventory, TimberbornStoredGoodStack requested)
    {
        int available = inventory.UnreservedTakeableStock()
            .Where(good => string.Equals(good.GoodId, requested.ResourceId, StringComparison.Ordinal))
            .Sum(good => Math.Max(0, good.Amount));
        int amount = Math.Min(requested.Amount, available);
        if (amount > 0) TimberbornInventoryMutations.Consume(inventory, new GoodAmount(requested.ResourceId, amount));
        return amount;
    }

    private Inventory? Resolve(TimberbornOwnedStorageRegistration owner, out TimberbornOwnedInventoryStatus status)
    {
        if (owner is null) throw new ArgumentNullException(nameof(owner));
        status = TimberbornOwnedInventoryStatus.NotLive;
        EntityComponent? entity = _entities.GetEntity(owner.EntityId);
        if (entity is null || entity.Deleted || !entity.Initialized || !entity) return null;
        if (entity.EntityId != owner.EntityId)
            throw new InvalidOperationException("Native storage registry returned a different entity identity.");
        status = TimberbornOwnedInventoryStatus.Unavailable;
        Inventory? inventory = null;
        if (owner.Role == TimberbornOwnedInventoryRole.Stockpile && entity.TryGetComponent<Stockpile>(out var stockpile))
            inventory = stockpile.Inventory;
        else if (owner.Role == TimberbornOwnedInventoryRole.SimpleOutput && entity.TryGetComponent<SimpleOutputInventory>(out var output))
            inventory = output.Inventory;
        if (inventory is null) return null;
        if (!ReferenceEquals(inventory.GetComponent<EntityComponent>(), entity))
            throw new InvalidOperationException("Owned storage component references another native entity's inventory.");
        if (!inventory || !inventory.Enabled) return null;
        status = TimberbornOwnedInventoryStatus.Available;
        return inventory;
    }
}
