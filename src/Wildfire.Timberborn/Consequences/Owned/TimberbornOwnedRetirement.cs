namespace Wildfire.Timberborn.Consequences;

public sealed partial class TimberbornOwnedDeltaConsumer
{
    /// <summary>
    /// Retire a completed native removal; preserve origin/history while deliberately dropping body accounting.
    /// This does not detach GPU material. Activation requires a separate detach-before-step lifecycle gate.
    /// Returns false only for an already retired, still absent owner with consistent body state.
    /// </summary>
    public bool RetireNativeOwner(Guid entityId)
    {
        _guard.ThrowIfSaveUnsafe();
        if (_consuming) throw new InvalidOperationException("Cannot retire an owner during consequence delivery or capture.");
        if (entityId == Guid.Empty) throw new ArgumentException("Retirement requires an exact native Guid.", nameof(entityId));
        _consuming = true;
        try
        {
            bool changed = _guard.CaptureAtRest(() =>
            {
                bool retained = _origins.PreflightRetirement(entityId, _damage);
                if (_bodies.ObservePresence(entityId) != TimberbornOwnedBodyPresence.Absent)
                    throw new InvalidOperationException("Retirement requires exact native registry absence, not death or incomplete deletion.");
                return retained;
            });
            if (!changed) return false;
            _guard.TransferInventory(() =>
            {
                _origins.CommitRetirement(entityId, _damage);
                if (_bodies.ObservePresence(entityId) != TimberbornOwnedBodyPresence.Absent)
                    throw new InvalidOperationException("Native owner reappeared during retirement publication.");
            });
            return true;
        }
        finally { _consuming = false; }
    }
}
