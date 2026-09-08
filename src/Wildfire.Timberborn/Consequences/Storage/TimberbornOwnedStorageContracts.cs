namespace Wildfire.Timberborn.Consequences;

public enum TimberbornOwnedInventoryStatus { Available, NotLive, Unavailable }

/// <summary>One canonical body and its complete original native inventory declarations.</summary>
public sealed record TimberbornOwnedStorageRegistration
{
    public TimberbornOwnedStorageRegistration(Guid entityId, NativeBurnTargetFamily family,
        IReadOnlyList<TimberbornInventoryDeclaration> declarations)
    {
        if (family is not (NativeBurnTargetFamily.Stockpile or NativeBurnTargetFamily.Structure))
            throw new ArgumentException("Storage effects require a supported constructed body.", nameof(family));
        var original = new TimberbornBodyInventoryDeclarations(entityId, declarations);
        EntityId = entityId; Family = family; Declarations = original.Declarations;
        TargetKey = new(TimberbornBurnDamageIdentity.ForEntity(entityId, family));
    }
    public Guid EntityId { get; }
    public NativeBurnTargetFamily Family { get; }
    public IReadOnlyList<TimberbornInventoryDeclaration> Declarations { get; }
    public TimberbornBurnDamageTargetKey TargetKey { get; }
}

public sealed record TimberbornOwnedInventoryRow(TimberbornInventoryDeclaration Declaration,
    TimberbornOwnedInventoryStatus Status, IReadOnlyList<TimberbornStoredGoodStack> Stacks);
public sealed record TimberbornOwnedInventorySnapshot(TimberbornOwnedInventoryStatus Status,
    IReadOnlyList<TimberbornOwnedInventoryRow> Inventories);
public readonly record struct TimberbornOwnedInventoryRemoval(TimberbornOwnedInventoryStatus Status, int RemovedAmount);

/// <summary>
/// Exact original topology and named stock access. Consume runs under the caller's existing resource guard.
/// Completed native calls produce receipts; callback exceptions propagate without inferred rollback.
/// No native inventory reference crosses this boundary or survives between mutations.
/// </summary>
public interface ITimberbornOwnedStorageInventoryApi
{
    TimberbornOwnedInventorySnapshot Read(TimberbornOwnedStorageRegistration owner);
    TimberbornOwnedInventoryRemoval Consume(TimberbornOwnedStorageRegistration owner,
        TimberbornInventoryDeclaration declaration, TimberbornStoredGoodStack requested);
}
