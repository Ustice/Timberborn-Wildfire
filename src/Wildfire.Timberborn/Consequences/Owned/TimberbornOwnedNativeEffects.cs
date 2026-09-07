namespace Wildfire.Timberborn.Consequences;

/// <summary>Registry absence is distinct from an entity that native persistence may still serialize.</summary>
public enum TimberbornOwnedBodyPresence { Absent, Live, Uninitialized, Deleted, InvalidNativeReference }

/// <summary>Physical body existence is independent of whether its inventory or other effect is currently available.</summary>
public interface ITimberbornOwnedBodyLiveness
{
    bool IsLive(Guid entityId);
    TimberbornOwnedBodyPresence ObservePresence(Guid entityId);
}

public sealed record TimberbornOwnedNativeEffects(ITimberbornOwnedBodyLiveness Bodies,
    ITimberbornLiveTreeBurnConsequenceApi Trees, ITimberbornLiveCropBurnConsequenceApi Crops,
    ITimberbornOwnedStorageInventoryApi Inventory, ITimberbornStoredGoodHazardConsequenceSink StorageHazards);

public sealed record TimberbornOwnedBodyRegistration(Guid EntityId, NativeBurnTargetFamily Family);

public readonly record struct TimberbornOwnedStorageEffectSummary(int NotLiveOwners, int UnavailableInventories,
    int DestroyedItems, int HazardousItems, int ExplosiveBlasts, int ContaminationPulseCells,
    int UnknownResources, int NonBurnableItems);

/// <summary>Configured effects are attempted; each family summary still reports unsupported native actions.</summary>
public readonly record struct TimberbornOwnedConsequenceCapabilities(bool TreeEffectsConfigured,
    bool CropEffectsConfigured, bool StorageInventoryEffectsConfigured, bool StructureRollbackSupported);

public readonly record struct TimberbornOwnedConsequenceBatchResult(int UnownedDeltas, int NotLiveOwners,
    TimberbornBurnDamageApplySummary Damage, TimberbornTreeBurnConsequenceSummary Trees,
    TimberbornCropBurnConsequenceSummary Crops, TimberbornOwnedStorageEffectSummary Storage,
    int StructureRollbackUnavailableOwners, TimberbornOwnedConsequenceCapabilities Capabilities);
