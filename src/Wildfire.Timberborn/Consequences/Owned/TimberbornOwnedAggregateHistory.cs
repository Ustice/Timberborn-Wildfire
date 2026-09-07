using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Consequences;

public sealed partial class TimberbornOwnedDeltaConsumer
{
    public TimberbornOwnedConsequenceSnapshot CaptureHistory() => _guard.CaptureAtRest(CopyHistoryDuringCapture);

    // Called only under this consumer's same runtime guard, including complete world capture.
    internal TimberbornOwnedConsequenceSnapshot CopyHistoryDuringCapture()
    {
        if (_consuming) throw new InvalidOperationException("Cannot capture consequence history during delivery.");
        _consuming = true;
        try
        {
            var owners = _origins.Capture(_damage);
            if (owners.Any(owner => owner.Retention == OwnedBodyRetention.RetiredNativeOwner && _bodies.IsLive(owner.EntityId)))
                throw new InvalidOperationException("A live native owner lost its body definition; it cannot be saved as a tombstone.");
            var natural = owners.Where(owner => owner.Family is NativeBurnTargetFamily.Tree or NativeBurnTargetFamily.Crop)
                .Select(owner => owner.Family == NativeBurnTargetFamily.Tree ? _trees.CaptureProgress(owner) : _crops.CaptureProgress(owner));
            return new(owners, natural, _storage.CaptureCredit(owners));
        }
        finally { _consuming = false; }
    }
    internal static TimberbornOwnedDeltaConsumer CreateFromHistory(TimberbornNativeMaterialRegistry registry,
        TimberbornBurnDamageService damage, TimberbornOwnedNativeEffects effects, INativeResourceMutationGuard guard,
        TimberbornOwnedConsequenceSnapshot history, TimberbornResourceFuelCatalog? catalog = null)
    {
        guard.ThrowIfSaveUnsafe();
        var consumer = new TimberbornOwnedDeltaConsumer(registry, damage, effects, guard, Array.Empty<TimberbornOwnedBodyRegistration>(), catalog);
        foreach (var owner in history.Owners)
        {
            if (owner.Retention == OwnedBodyRetention.RetiredNativeOwner)
            {
                if (effects.Bodies.IsLive(owner.EntityId)) throw new ArgumentException("A retired saved owner cannot authorize a live native body.");
                consumer._origins.RegisterRetired(owner.EntityId, owner.Family, owner.TargetKey);
            }
            else consumer.Register(new(owner.EntityId, owner.Family));
        }
        var owners = history.Owners.ToDictionary(owner => owner.EntityId);
        foreach (var progress in history.Natural)
        {
            var owner = owners[progress.EntityId];
            if (owner.Family == NativeBurnTargetFamily.Tree) consumer._trees.RestoreProgress(owner, progress);
            else consumer._crops.RestoreProgress(owner, progress);
        }
        consumer._storage.RestoreCredit(history);
        guard.ThrowIfSaveUnsafe();
        return consumer;
    }
}
