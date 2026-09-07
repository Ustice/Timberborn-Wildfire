namespace Wildfire.Timberborn.Consequences;

public enum TimberbornOwnedInventoryRole { Stockpile, SimpleOutput }
public enum TimberbornOwnedInventoryStatus { Available, NotLive, Unavailable }

/// <summary>The caller chooses one inventory role and canonical body damage state for a native owner.</summary>
public sealed record TimberbornOwnedStorageRegistration
{
    public TimberbornOwnedStorageRegistration(Guid entityId, TimberbornOwnedInventoryRole role)
    {
        if (entityId == Guid.Empty) throw new ArgumentException("Owned storage requires a native identity.", nameof(entityId));
        if (!Enum.IsDefined(typeof(TimberbornOwnedInventoryRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
        EntityId = entityId;
        Role = role;
        TargetKey = new TimberbornBurnDamageTargetKey(TimberbornBurnDamageIdentity.ForEntity(entityId,
            role == TimberbornOwnedInventoryRole.Stockpile ? NativeBurnTargetFamily.Stockpile : NativeBurnTargetFamily.Structure));
    }
    public Guid EntityId { get; }
    public TimberbornOwnedInventoryRole Role { get; }
    public TimberbornBurnDamageTargetKey TargetKey { get; }
}

public sealed record TimberbornOwnedInventorySnapshot(TimberbornOwnedInventoryStatus Status,
    IReadOnlyList<TimberbornStoredGoodStack> Stacks);
public readonly record struct TimberbornOwnedInventoryRemoval(TimberbornOwnedInventoryStatus Status, int RemovedAmount);

/// <summary>
/// Exact-owner stock access. Consume must run inside the runtime's shared native resource guard;
/// successful amounts are receipts, while native callback exceptions propagate without inferred rollback.
/// No inventory instance crosses this boundary or survives between mutations.
/// </summary>
public interface ITimberbornOwnedStorageInventoryApi
{
    TimberbornOwnedInventorySnapshot Read(TimberbornOwnedStorageRegistration owner);
    TimberbornOwnedInventoryRemoval Consume(TimberbornOwnedStorageRegistration owner, TimberbornStoredGoodStack requested);
}
