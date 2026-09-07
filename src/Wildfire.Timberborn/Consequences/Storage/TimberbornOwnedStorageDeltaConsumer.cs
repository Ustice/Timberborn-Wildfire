using Wildfire.Core;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Consequences;

public readonly record struct TimberbornOwnedStorageBatchResult(int UnownedCount, int NotLiveCount, int UnavailableCount,
    TimberbornBurnDamageApplySummary Damage, int DestroyedItems, int HazardousItems, int ExplosiveBlasts,
    int ContaminationPulseCells, int UnknownResources, int NonBurnableItems);

/// <summary>
/// Explicit storage-only owned route. It never delegates an owned delta to a legacy cell-routed sink.
/// Not runtime-bound: transient fractional fuel credit needs a save contract before activation.
/// Runtime must supply the same resource coordinator used by world saves.
/// </summary>
public sealed class TimberbornOwnedStorageDeltaConsumer
{
    private readonly TimberbornOwnedBurnOrigins _origins;
    private readonly TimberbornBurnDamageService _damage;
    private readonly TimberbornOwnedStorageBurnSink _storage;
    private readonly INativeResourceMutationGuard _resources;
    private bool _consuming;

    public TimberbornOwnedStorageDeltaConsumer(TimberbornNativeMaterialRegistry origins, TimberbornBurnDamageService damage,
        ITimberbornOwnedStorageInventoryApi inventory, ITimberbornStoredGoodHazardConsequenceSink hazards,
        INativeResourceMutationGuard resources, IEnumerable<TimberbornOwnedStorageRegistration> registrations,
        TimberbornResourceFuelCatalog? catalog = null)
    {
        _origins = new TimberbornOwnedBurnOrigins(origins);
        _damage = damage ?? throw new ArgumentNullException(nameof(damage));
        _storage = new TimberbornOwnedStorageBurnSink(inventory ?? throw new ArgumentNullException(nameof(inventory)),
            hazards ?? throw new ArgumentNullException(nameof(hazards)), catalog ?? TimberbornResourceFuelCatalog.Default);
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        foreach (var owner in registrations) Register(owner);
    }

    public bool HasTransientFuelCredit => _storage.HasTransientFuelCredit;

    public void Register(TimberbornOwnedStorageRegistration owner)
    {
        if (_consuming) throw new InvalidOperationException("Cannot change storage origins during consequence delivery.");
        if (owner is null) throw new ArgumentNullException(nameof(owner));
        // Both roles belong to a physical constructed body; inventory role does not change its kind.
        if (!_damage.TryGetState(owner.TargetKey, out var state) || state.TargetKind != TimberbornBurnDamageTargetKind.Structure)
            throw new ArgumentException("Storage origin requires its exact canonical body damage registration.", nameof(owner));
        _origins.Register(owner.EntityId, owner.Role == TimberbornOwnedInventoryRole.Stockpile ?
            NativeBurnTargetFamily.Stockpile : NativeBurnTargetFamily.Structure, owner.TargetKey);
    }

    public TimberbornOwnedStorageBatchResult Consume(uint tick, ReadOnlySpan<CellDelta> deltas)
    {
        if (_consuming) throw new InvalidOperationException("Owned storage delivery cannot reenter.");
        _resources.ThrowIfSaveUnsafe();
        _consuming = true;
        try
        {
            var batch = _origins.Resolve(deltas);
            var isLive = batch.Decisions.GroupBy(item => item.EntityId).ToDictionary(group => group.Key, group => _storage.IsLive(group.First()));
            var live = batch.Decisions.Where(item => isLive[item.EntityId]).ToArray();
            TimberbornOwnedStorageBatchResult result = default;
            _resources.TransferInventory(() =>
            {
                var damage = _damage.ApplyOwnedDamage(tick, live);
                var effects = _storage.ApplyOwnedConsequences(tick, live);
                result = new(batch.UnownedCount, isLive.Count(pair => !pair.Value) + effects.NotLive, effects.Unavailable,
                    damage, effects.Removed, effects.Hazardous, effects.Blasts, effects.Pulses, effects.Unknown, effects.NonBurnable);
            });
            return result;
        }
        finally { _consuming = false; }
    }
}
