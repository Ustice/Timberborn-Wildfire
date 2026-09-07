using Wildfire.Core;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Consequences;

public readonly record struct TimberbornOwnedCropBatchResult(int UnownedCount, int NotLiveCount,
    TimberbornBurnDamageApplySummary Damage, TimberbornCropBurnConsequenceSummary Crops);

/// <summary>Bounded crop-only vertical using shared identity preflight. Never chain family consumers for a mixed batch.</summary>
public sealed class TimberbornOwnedCropDeltaConsumer
{
    private readonly TimberbornOwnedBurnOrigins _origins;
    private readonly TimberbornBurnDamageService _damage;
    private readonly ITimberbornLiveCropBurnConsequenceApi _native;
    private readonly INativeResourceMutationGuard _guard;
    private readonly TimberbornCropBurnConsequenceSink _crops;
    private bool _consuming;
    public TimberbornOwnedCropDeltaConsumer(TimberbornNativeMaterialRegistry origins, TimberbornBurnDamageService damage,
        ITimberbornLiveCropBurnConsequenceApi native, INativeResourceMutationGuard guard, IEnumerable<Guid> registeredCrops)
    {
        _origins = new(origins);
        _damage = damage ?? throw new ArgumentNullException(nameof(damage));
        _native = native ?? throw new ArgumentNullException(nameof(native));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _crops = new(damage, native);
        foreach (var id in registeredCrops) RegisterCrop(id);
    }
    public void RegisterCrop(Guid id)
    {
        if (_consuming) throw new InvalidOperationException("Cannot register crop origins during delivery.");
        var key = new TimberbornBurnDamageTargetKey(TimberbornBurnDamageIdentity.ForEntity(id, NativeBurnTargetFamily.Crop));
        if (!_damage.TryGetState(key, out var state) || !TimberbornCropBurnTargetClassifier.IsCropOrHarvestable(state))
            throw new ArgumentException("Owned crop requires its canonical Guid crop damage state.", nameof(id));
        _origins.Register(id, NativeBurnTargetFamily.Crop, key);
    }
    public TimberbornOwnedCropBatchResult Consume(uint tick, ReadOnlySpan<CellDelta> deltas)
    {
        if (_consuming) throw new InvalidOperationException("Crop consequence delivery cannot reenter.");
        _guard.ThrowIfSaveUnsafe();
        _consuming = true;
        try
        {
            var batch = _origins.Resolve(deltas);
            var live = batch.Decisions.Where(item => _native.IsLive(item.EntityId)).ToArray();
            if (live.Any(item => !_damage.TryGetState(item.TargetKey, out _)))
                throw new InvalidOperationException("Live crop origin lost its registered body damage state.");
            TimberbornOwnedCropBatchResult result = default;
            _guard.TransferInventory(() =>
            {
                var damage = _damage.ApplyOwnedDamage(tick, live);
                var crops = _crops.ApplyOwnedConsequences(tick, live);
                result = new(batch.UnownedCount, batch.Decisions.Length - live.Length, damage, crops);
            });
            return result;
        }
        finally { _consuming = false; }
    }
}
