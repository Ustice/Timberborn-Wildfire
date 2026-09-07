using Wildfire.Core;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Consequences;

/// <summary>
/// One owned batch: complete identity preflight, one body reducer, then concrete family effects under
/// the runtime's shared save guard. No legacy cell-routing or family wrapper is called.
/// </summary>
public sealed partial class TimberbornOwnedDeltaConsumer
{
    private readonly TimberbornOwnedBurnOrigins _origins;
    private readonly TimberbornBurnDamageService _damage;
    private readonly ITimberbornOwnedBodyLiveness _bodies;
    private readonly INativeResourceMutationGuard _guard;
    private readonly TimberbornTreeBurnConsequenceSink _trees;
    private readonly TimberbornCropBurnConsequenceSink _crops;
    private readonly TimberbornOwnedStorageBurnSink _storage;
    private bool _consuming;
    public static readonly TimberbornOwnedConsequenceCapabilities Capabilities = new(true, true, true, false);

    public TimberbornOwnedDeltaConsumer(TimberbornNativeMaterialRegistry origins, TimberbornBurnDamageService damage,
        TimberbornOwnedNativeEffects effects, INativeResourceMutationGuard guard,
        IEnumerable<TimberbornOwnedBodyRegistration> registrations, TimberbornResourceFuelCatalog? catalog = null)
    {
        _origins = new(origins);
        _damage = damage ?? throw new ArgumentNullException(nameof(damage));
        if (effects is null) throw new ArgumentNullException(nameof(effects));
        _bodies = effects.Bodies ?? throw new ArgumentException("Exact native body liveness is required.", nameof(effects));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _trees = new(damage, effects.Trees ?? throw new ArgumentException("Tree effect adapter is required.", nameof(effects)));
        _crops = new(damage, effects.Crops ?? throw new ArgumentException("Crop effect adapter is required.", nameof(effects)));
        _storage = new(effects.Inventory ?? throw new ArgumentException("Storage inventory adapter is required.", nameof(effects)),
            effects.StorageHazards ?? throw new ArgumentException("Storage hazard adapter is required.", nameof(effects)),
            catalog ?? TimberbornResourceFuelCatalog.Default);
        foreach (var registration in registrations) Register(registration);
    }

    public bool HasTransientStorageFuelCredit => _storage.HasTransientFuelCredit;

    public void Register(TimberbornOwnedBodyRegistration registration)
    {
        if (_consuming) throw new InvalidOperationException("Cannot change owned body registrations during delivery.");
        if (registration is null) throw new ArgumentNullException(nameof(registration));
        var key = new TimberbornBurnDamageTargetKey(TimberbornBurnDamageIdentity.ForEntity(registration.EntityId, registration.Family));
        if (!_damage.TryGetState(key, out var state) || !MatchesFamily(registration.Family, state))
            throw new ArgumentException("Owned family requires its exact supported canonical body state.", nameof(registration));
        _origins.Register(registration.EntityId, registration.Family, key);
    }

    public TimberbornOwnedConsequenceBatchResult Consume(uint tick, ReadOnlySpan<CellDelta> deltas)
    {
        if (_consuming) throw new InvalidOperationException("Owned consequence delivery cannot reenter.");
        _guard.ThrowIfSaveUnsafe();
        _consuming = true;
        try
        {
            var batch = _origins.Resolve(deltas);
            var bodyLive = batch.Decisions.Select(item => item.EntityId).Distinct().ToDictionary(id => id, _bodies.IsLive);
            var live = batch.Decisions.Where(item => bodyLive[item.EntityId]).ToArray();
            // Revalidate every required state before ANY body mutation, including zero-damage rows.
            if (live.Any(item => !_damage.TryGetState(item.TargetKey, out var state) || !MatchesFamily(item.Family, state)))
                throw new InvalidOperationException("A live owned body lost its supported canonical registration after preflight.");
            TimberbornOwnedConsequenceBatchResult result = default;
            _guard.TransferInventory(() =>
            {
                var damage = _damage.ApplyOwnedDamage(tick, live);
                var trees = _trees.ApplyOwnedConsequences(tick, live.Where(item => item.Family == NativeBurnTargetFamily.Tree).ToArray());
                var crops = _crops.ApplyOwnedConsequences(tick, live.Where(item => item.Family == NativeBurnTargetFamily.Crop).ToArray());
                var storage = _storage.ApplyOwnedConsequences(tick, live.Where(item =>
                    item.Family is NativeBurnTargetFamily.Stockpile or NativeBurnTargetFamily.Structure).ToArray());
                result = new(batch.UnownedCount, bodyLive.Count(pair => !pair.Value), damage, trees, crops,
                    new(storage.NotLive, storage.Unavailable, storage.Removed, storage.Hazardous, storage.Blasts,
                        storage.Pulses, storage.Unknown, storage.NonBurnable),
                    live.Where(item => item.Family == NativeBurnTargetFamily.Structure).Select(item => item.EntityId).Distinct().Count(),
                    Capabilities);
            });
            return result;
        }
        finally { _consuming = false; }
    }

    private static bool MatchesFamily(NativeBurnTargetFamily family, TimberbornBurnDamageTargetState state) => family switch
    {
        NativeBurnTargetFamily.Tree => TimberbornTreeBurnTargetClassifier.IsTreeOrCuttable(state),
        NativeBurnTargetFamily.Crop => TimberbornCropBurnTargetClassifier.IsCropOrHarvestable(state),
        NativeBurnTargetFamily.Stockpile => state.TargetKind == TimberbornBurnDamageTargetKind.Storage,
        NativeBurnTargetFamily.Structure => state.TargetKind == TimberbornBurnDamageTargetKind.Structure,
        _ => false, // No SelectedCrop alias or unmigrated owner family may silently fall back.
    };
}
