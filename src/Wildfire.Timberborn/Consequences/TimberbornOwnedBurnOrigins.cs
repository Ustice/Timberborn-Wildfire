using Wildfire.Core;

namespace Wildfire.Timberborn.Consequences;

internal readonly record struct TimberbornOwnedBurnDecision(TimberbornFireCellDeltaDecision Decision,
    Guid EntityId, TimberbornBurnDamageTargetKey TargetKey, NativeBurnTargetFamily Family);
internal sealed record TimberbornOwnedBurnBatch(int UnownedCount, int ReplaySuppressedCount, TimberbornOwnedBurnDecision[] Decisions);

/// <summary>One canonical body identity per Guid; validates an entire delta batch before any family effects.</summary>
internal sealed class TimberbornOwnedBurnOrigins
{
    private readonly TimberbornNativeMaterialRegistry _origins;
    private readonly Dictionary<Guid, Binding> _bindings = new();
    internal TimberbornOwnedBurnOrigins(TimberbornNativeMaterialRegistry origins) =>
        _origins = origins ?? throw new ArgumentNullException(nameof(origins));

    internal void Register(Guid entityId, NativeBurnTargetFamily family, TimberbornBurnDamageTargetKey key)
        => Register(entityId, family, key, OwnedBodyRetention.RetainedBody);

    internal void RegisterRetired(Guid entityId, NativeBurnTargetFamily family, TimberbornBurnDamageTargetKey key)
        => Register(entityId, family, key, OwnedBodyRetention.RetiredNativeOwner);

    private void Register(Guid entityId, NativeBurnTargetFamily family, TimberbornBurnDamageTargetKey key, OwnedBodyRetention retention)
    {
        if (!string.Equals(TimberbornBurnDamageIdentity.ForEntity(entityId, family), key.StableId, StringComparison.Ordinal))
            throw new ArgumentException("Owned consequence registration requires its exact native Guid/family key.", nameof(key));
        var next = new Binding(key, family, retention);
        if (_bindings.TryGetValue(entityId, out var previous) && previous != next)
            throw new ArgumentException("One native owner cannot change its canonical family or resurrect a retired body.", nameof(entityId));
        _bindings[entityId] = next; // Kept when an entity dies: old origins must never select replacement owners.
    }

    internal OwnedConsequenceOwner[] Capture(TimberbornBurnDamageService damage) => _bindings.Select(pair =>
    {
        ValidateBodyState(pair.Value, damage);
        return new OwnedConsequenceOwner(pair.Key, pair.Value.Family, pair.Value.Retention,
            pair.Value.Retention == OwnedBodyRetention.RetainedBody ? damage.CaptureOwnedProfile(pair.Value.Key) : null);
    }).ToArray();

    internal bool IsRetired(Guid entityId) => _bindings[entityId].Retention == OwnedBodyRetention.RetiredNativeOwner;

    internal bool PreflightRetirement(Guid entityId, TimberbornBurnDamageService damage)
    {
        if (!_bindings.TryGetValue(entityId, out var binding)) throw new ArgumentException("Cannot retire an unknown native owner.", nameof(entityId));
        ValidateBodyState(binding, damage);
        return binding.Retention == OwnedBodyRetention.RetainedBody;
    }

    // Caller owns the same resource guard as native consequences/save and has proven exact registry absence.
    internal void CommitRetirement(Guid entityId, TimberbornBurnDamageService damage)
    {
        var binding = _bindings[entityId];
        _origins.Reconcile(Array.Empty<TimberbornMaterialProjection>(), new[] { entityId });
        if (!damage.RemoveTarget(binding.Key)) throw new InvalidOperationException("Retiring body state disappeared after preflight.");
        _bindings[entityId] = binding with { Retention = OwnedBodyRetention.RetiredNativeOwner };
    }

    private static void ValidateBodyState(Binding binding, TimberbornBurnDamageService damage)
    {
        bool hasBody = damage.TryGetState(binding.Key, out _);
        if (hasBody != (binding.Retention == OwnedBodyRetention.RetainedBody))
            throw new InvalidOperationException("Canonical owner retention disagrees with its body state; explicit retirement is required.");
    }

    internal TimberbornOwnedBurnBatch Resolve(ReadOnlySpan<CellDelta> deltas)
    {
        var resolved = new List<TimberbornOwnedBurnDecision>();
        int unowned = 0;
        var transitions = new TimberbornOwnedTransitionNormalizer();
        foreach (var delta in deltas)
        {
            _origins.ValidateOriginCellIndex(delta.CellIndex);
            var decision = TimberbornFireCellDeltaDecision.FromDelta(delta);
            if (decision.TargetId == 0)
            {
                if (decision.SlotId != 0) throw new InvalidOperationException("Material origin has a slot without a target.");
                unowned++; continue;
            }
            if (decision.SlotId == 0)
                throw new NotSupportedException("Owned consequences require originating SlotId; legacy provenance is unavailable.");
            if (!_origins.TryResolveOrigin(decision.TargetId, out Guid id))
                throw new InvalidOperationException($"Unknown material origin {decision.TargetId}; native consequences require reconciliation.");
            if (!_origins.IsSlotBound(decision.TargetId, decision.SlotId))
                throw new InvalidOperationException("Material origin slot has no retained native footprint binding.");
            if (!_bindings.TryGetValue(id, out var binding))
                throw new NotSupportedException($"Native material origin {id:D} is not registered for owned consequences.");
            if (!transitions.Accept(delta)) continue;
            resolved.Add(new TimberbornOwnedBurnDecision(decision, id, binding.Key, binding.Family));
        }
        return new TimberbornOwnedBurnBatch(unowned, transitions.ReplaySuppressedCount, resolved.ToArray());
    }
    private readonly record struct Binding(TimberbornBurnDamageTargetKey Key, NativeBurnTargetFamily Family, OwnedBodyRetention Retention);
}
