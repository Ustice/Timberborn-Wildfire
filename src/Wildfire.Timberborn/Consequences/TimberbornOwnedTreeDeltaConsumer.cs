using Wildfire.Core;

namespace Wildfire.Timberborn.Consequences;

/// <summary>A tree API that can distinguish known native disappearance before applying body damage.</summary>
public interface ITimberbornLiveTreeBurnConsequenceApi : ITimberbornTreeBurnConsequenceApi
{
    bool IsLive(Guid entityId);
}

internal readonly record struct TimberbornOwnedBurnDecision(
    TimberbornFireCellDeltaDecision Decision, Guid EntityId, TimberbornBurnDamageTargetKey TargetKey);

public readonly record struct TimberbornOwnedTreeBatchResult(
    int UnownedCount, int NotLiveCount, TimberbornBurnDamageApplySummary Damage,
    TimberbornTreeBurnConsequenceSummary Trees);

/// <summary>
/// Deliberately bounded owned-material consumer. It cannot call legacy cell-routed owner sinks.
/// Production material lifecycle activation remains separate from this tree-only vertical.
/// </summary>
public sealed class TimberbornOwnedTreeDeltaConsumer
{
    private readonly TimberbornNativeMaterialRegistry _origins;
    private readonly TimberbornBurnDamageService _damage;
    private readonly ITimberbornLiveTreeBurnConsequenceApi _native;
    private readonly TimberbornTreeBurnConsequenceSink _trees;
    private readonly Dictionary<Guid, TimberbornBurnDamageTargetKey> _registeredTrees = new();
    private bool _consuming;

    public TimberbornOwnedTreeDeltaConsumer(TimberbornNativeMaterialRegistry origins,
        TimberbornBurnDamageService damage, ITimberbornLiveTreeBurnConsequenceApi native,
        IEnumerable<Guid> registeredTrees)
    {
        _origins = origins ?? throw new ArgumentNullException(nameof(origins));
        _damage = damage ?? throw new ArgumentNullException(nameof(damage));
        _native = native ?? throw new ArgumentNullException(nameof(native));
        _trees = new TimberbornTreeBurnConsequenceSink(damage, native);
        foreach (Guid entityId in registeredTrees) RegisterTree(entityId);
    }

    /// <summary>Bind after exact native registration; keep this binding when native death removes the live target.</summary>
    public void RegisterTree(Guid entityId)
    {
        if (_consuming) throw new InvalidOperationException("Cannot change origin registrations during consequence delivery.");
        var key = new TimberbornBurnDamageTargetKey(TimberbornBurnDamageIdentity.ForEntity(entityId, NativeBurnTargetFamily.Tree));
        if (!_damage.TryGetState(key, out var state) || !TimberbornTreeBurnTargetClassifier.IsTreeOrCuttable(state))
            throw new ArgumentException("Tree origin requires its exact registered tree damage state.", nameof(entityId));
        _registeredTrees[entityId] = key;
    }

    public TimberbornOwnedTreeBatchResult Consume(uint tick, ReadOnlySpan<CellDelta> deltas)
    {
        if (_consuming) throw new InvalidOperationException("Owned tree consequence delivery cannot reenter.");
        _consuming = true;
        try
        {
            var resolved = new List<TimberbornOwnedBurnDecision>();
            int unowned = 0;
            // Identity preflight is whole-batch: a late unknown/unmigrated row must not follow earlier mutation.
            foreach (CellDelta delta in deltas)
            {
                var decision = TimberbornFireCellDeltaDecision.FromDelta(delta);
                if (decision.TargetId == 0) { unowned++; continue; }
                if (!_origins.TryResolveOrigin(decision.TargetId, out Guid entityId))
                    throw new InvalidOperationException($"Unknown material origin {decision.TargetId}; native consequences require reconciliation.");
                if (!_registeredTrees.TryGetValue(entityId, out var key))
                    throw new NotSupportedException($"Native material origin {entityId:D} is not registered for owned tree consequences.");
                resolved.Add(new TimberbornOwnedBurnDecision(decision, entityId, key));
            }
            var live = resolved.Where(item => _native.IsLive(item.EntityId)).ToArray();
            if (live.Any(item => !_damage.TryGetState(item.TargetKey, out _)))
                throw new InvalidOperationException("Live tree origin has no registered damage state.");
            var damage = _damage.ApplyOwnedDamage(tick, live);
            var trees = _trees.ApplyOwnedConsequences(tick, live);
            return new TimberbornOwnedTreeBatchResult(unowned, resolved.Count - live.Length, damage, trees);
        }
        finally { _consuming = false; }
    }
}
