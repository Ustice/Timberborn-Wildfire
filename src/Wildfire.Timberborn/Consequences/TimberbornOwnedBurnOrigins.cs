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
    {
        if (!string.Equals(TimberbornBurnDamageIdentity.ForEntity(entityId, family), key.StableId, StringComparison.Ordinal))
            throw new ArgumentException("Owned consequence registration requires its exact native Guid/family key.", nameof(key));
        var next = new Binding(key, family);
        if (_bindings.TryGetValue(entityId, out var previous) && previous != next)
            throw new ArgumentException("One native owner cannot have conflicting canonical body damage families.", nameof(entityId));
        _bindings[entityId] = next; // Kept when an entity dies: old origins must never select replacement owners.
    }

    internal OwnedConsequenceOwner[] Capture(TimberbornBurnDamageService damage) => _bindings.Select(pair =>
        damage.TryGetState(pair.Value.Key, out _) ? new OwnedConsequenceOwner(pair.Key, pair.Value.Family,
            OwnedBodyRetention.RetainedBody, damage.CaptureOwnedProfile(pair.Value.Key)) :
            new OwnedConsequenceOwner(pair.Key, pair.Value.Family, OwnedBodyRetention.RetiredNativeOwner, null)).ToArray();

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
    private readonly record struct Binding(TimberbornBurnDamageTargetKey Key, NativeBurnTargetFamily Family);
}
