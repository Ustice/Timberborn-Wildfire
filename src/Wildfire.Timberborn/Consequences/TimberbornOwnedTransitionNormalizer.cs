using Wildfire.Core;

namespace Wildfire.Timberborn.Consequences;

/// <summary>One delivered batch only. Preserve full packed chains before selecting positive fuel loss.</summary>
internal sealed class TimberbornOwnedTransitionNormalizer
{
    private readonly Dictionary<(uint Target, uint Slot, int Cell), Chain> _chains = new();
    internal int ReplaySuppressedCount { get; private set; }

    internal bool Accept(CellDelta delta)
    {
        var key = (delta.TargetId, delta.SlotId, delta.CellIndex);
        if (!_chains.TryGetValue(key, out var chain))
        {
            _chains.Add(key, new Chain(delta));
            return true;
        }
        // A real cycle may repeat an identical loss after a gain. Continuity wins over membership.
        if (delta.OldCell == chain.PreviousNew)
        {
            chain.Add(delta);
            return true;
        }
        if (chain.Accepted.Contains((delta.OldCell, delta.NewCell)))
        {
            ReplaySuppressedCount++;
            return false;
        }
        throw new InvalidOperationException($"Discontinuous owned material transitions for {key}; batch rejected before consequences.");
    }

    private sealed class Chain
    {
        internal ushort PreviousNew { get; private set; }
        internal HashSet<(ushort Old, ushort New)> Accepted { get; } = new();
        internal Chain(CellDelta first) => Add(first);
        internal void Add(CellDelta delta)
        {
            PreviousNew = delta.NewCell;
            Accepted.Add((delta.OldCell, delta.NewCell));
        }
    }
}
