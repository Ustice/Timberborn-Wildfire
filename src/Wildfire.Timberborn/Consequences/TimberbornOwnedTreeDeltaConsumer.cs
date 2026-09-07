using Wildfire.Core;

namespace Wildfire.Timberborn.Consequences;

/// <summary>A tree API that can distinguish known native disappearance before applying body damage.</summary>
public interface ITimberbornLiveTreeBurnConsequenceApi : ITimberbornTreeBurnConsequenceApi
{
    bool IsLive(Guid entityId);
}

public readonly record struct TimberbornOwnedTreeBatchResult(
    int UnownedCount, int NotLiveCount, TimberbornBurnDamageApplySummary Damage,
    TimberbornTreeBurnConsequenceSummary Trees);

/// <summary>
/// Deliberately bounded owned-material consumer. It cannot call legacy cell-routed owner sinks.
/// Production material lifecycle activation remains separate from this tree-only vertical.
/// </summary>
public sealed class TimberbornOwnedTreeDeltaConsumer
{
    private readonly TimberbornOwnedBurnOrigins _origins;
    private readonly TimberbornBurnDamageService _damage;
    private readonly ITimberbornLiveTreeBurnConsequenceApi _native;
    private readonly TimberbornTreeBurnConsequenceSink _trees;
    private bool _consuming;

    public TimberbornOwnedTreeDeltaConsumer(TimberbornNativeMaterialRegistry origins,
        TimberbornBurnDamageService damage, ITimberbornLiveTreeBurnConsequenceApi native,
        IEnumerable<Guid> registeredTrees)
    {
        _origins = new TimberbornOwnedBurnOrigins(origins);
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
        _origins.Register(entityId, NativeBurnTargetFamily.Tree, key);
    }

    public TimberbornOwnedTreeBatchResult Consume(uint tick, ReadOnlySpan<CellDelta> deltas)
    {
        if (_consuming) throw new InvalidOperationException("Owned tree consequence delivery cannot reenter.");
        _consuming = true;
        try
        {
            var batch = _origins.Resolve(deltas);
            var resolved = batch.Decisions;
            var live = resolved.Where(item => _native.IsLive(item.EntityId)).ToArray();
            if (live.Any(item => !_damage.TryGetState(item.TargetKey, out _)))
                throw new InvalidOperationException("Live tree origin has no registered damage state.");
            var damage = _damage.ApplyOwnedDamage(tick, live);
            var trees = _trees.ApplyOwnedConsequences(tick, live);
            return new TimberbornOwnedTreeBatchResult(batch.UnownedCount, resolved.Length - live.Length, damage, trees);
        }
        finally { _consuming = false; }
    }
}
