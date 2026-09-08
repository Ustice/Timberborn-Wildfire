using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;

namespace Wildfire.Timberborn.Consequences;

/// <summary>Exact Guid/original named-inventory access, without family-derived inventory guesses.</summary>
public sealed class TimberbornOwnedStorageInventoryApi : ITimberbornOwnedStorageInventoryApi
{
    private readonly EntityRegistry _entities;
    public TimberbornOwnedStorageInventoryApi(EntityRegistry entities) =>
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));

    public TimberbornOwnedInventorySnapshot Read(TimberbornOwnedStorageRegistration owner)
    {
        var bindings = Resolve(owner, out var status);
        return new(status, bindings is null ? Array.Empty<TimberbornOwnedInventoryRow>() :
            bindings.Select(binding => new TimberbornOwnedInventoryRow(binding.Declaration,
                binding.Inventory.Enabled ? TimberbornOwnedInventoryStatus.Available : TimberbornOwnedInventoryStatus.Unavailable,
                binding.Inventory.Enabled ? ReadStock(binding.Inventory) : Array.Empty<TimberbornStoredGoodStack>())).ToArray());
    }

    public TimberbornOwnedInventoryRemoval Consume(TimberbornOwnedStorageRegistration owner,
        TimberbornInventoryDeclaration declaration, TimberbornStoredGoodStack requested)
    {
        if (owner is null) throw new ArgumentNullException(nameof(owner));
        if (declaration is null || !owner.Declarations.Contains(declaration))
            throw new ArgumentException("Requested inventory is not an original owner declaration.", nameof(declaration));
        if (requested is null || string.IsNullOrWhiteSpace(requested.ResourceId) || requested.Amount <= 0)
            throw new ArgumentException("Consumption requires one positive identified good amount.", nameof(requested));
        // Native callbacks may change topology, reservations, availability or entity liveness between withdrawals.
        var bindings = Resolve(owner, out var status);
        if (bindings is null) return new(status, 0);
        var inventory = bindings.Single(binding => binding.Declaration == declaration).Inventory;
        if (!inventory.Enabled) return new(TimberbornOwnedInventoryStatus.Unavailable, 0);
        return new(TimberbornOwnedInventoryStatus.Available, ConsumeUnreserved(inventory, requested));
    }

    private static TimberbornStoredGoodStack[] ReadStock(Inventory inventory) => inventory.UnreservedStock()
        .Where(good => good.Amount > 0).Select(good => new TimberbornStoredGoodStack(good.GoodId, good.Amount)).ToArray();

    private static int ConsumeUnreserved(Inventory inventory, TimberbornStoredGoodStack requested)
    {
        int amount = Math.Min(requested.Amount, Math.Max(0, inventory.UnreservedAmountInStock(requested.ResourceId)));
        if (amount > 0) TimberbornInventoryMutations.Consume(inventory, new GoodAmount(requested.ResourceId, amount));
        return amount;
    }

    private IReadOnlyList<TimberbornDeclaredInventory>? Resolve(TimberbornOwnedStorageRegistration owner,
        out TimberbornOwnedInventoryStatus status)
    {
        if (owner is null) throw new ArgumentNullException(nameof(owner));
        status = TimberbornOwnedInventoryStatus.NotLive;
        EntityComponent? entity = _entities.GetEntity(owner.EntityId);
        if (entity is null || entity.Deleted || !entity.Initialized || !entity) return null;
        if (entity.EntityId != owner.EntityId) throw new InvalidOperationException("Native registry returned another owner.");
        var bindings = TimberbornNativeInventoryRoles.Capture(entity);
        RequireOriginalDeclarations(owner, bindings.Select(binding => binding.Declaration).ToArray());
        status = TimberbornOwnedInventoryStatus.Unavailable;
        if (owner.Declarations.Any(declaration => declaration.Role is not (TimberbornNativeInventoryRole.Stockpile or
            TimberbornNativeInventoryRole.SimpleOutput or TimberbornNativeInventoryRole.Manufactory))) return null;
        status = TimberbornOwnedInventoryStatus.Available;
        return bindings;
    }

    internal static void RequireOriginalDeclarations(TimberbornOwnedStorageRegistration owner,
        IReadOnlyList<TimberbornInventoryDeclaration> actual)
    {
        if (!owner.Declarations.SequenceEqual(actual))
            throw new InvalidOperationException("Native inventory topology differs from the original owner declarations.");
    }
}
