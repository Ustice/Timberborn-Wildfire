namespace Wildfire.Core;

public sealed class FireSimChangeQueue
{
    private readonly List<FireSimChange> _changes = new();
    private int _generation;

    public int Count => _changes.Count;

    public void Add(FireSimChange change)
    {
        _changes.Add(change);
    }

    public Batch PrepareBatch(int cellCount, int uploadCapacity)
    {
        if (cellCount <= 0 || uploadCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(uploadCapacity), "Grid and upload capacities must be positive.");
        }

        List<FireSimChange> validChanges = new();
        List<int> consumedIndices = new();
        int ignoredCount = 0;
        for (int index = 0; index < _changes.Count; index++)
        {
            FireSimChange change = _changes[index];
            if (change.CellIndex < 0 || change.CellIndex >= cellCount)
            {
                ignoredCount++;
                consumedIndices.Add(index);
            }
            else if (validChanges.Count < uploadCapacity)
            {
                validChanges.Add(change);
                consumedIndices.Add(index);
            }
        }

        return new Batch(this, _generation, validChanges.ToArray(), consumedIndices.ToArray(), ignoredCount);
    }

    // A failed upload/dispatch leaves the batch queued; commit only after external changes were applied.
    public void Consume(Batch batch)
    {
        if (batch.Owner != this || batch.Generation != _generation)
        {
            throw new InvalidOperationException("The change batch is stale or belongs to another queue.");
        }

        for (int index = batch.ConsumedIndices.Length - 1; index >= 0; index--)
        {
            _changes.RemoveAt(batch.ConsumedIndices[index]);
        }

        _generation++;
    }

    public sealed class Batch
    {
        internal Batch(FireSimChangeQueue owner, int generation, FireSimChange[] changes, int[] consumedIndices, int ignoredCount)
        {
            Owner = owner;
            Generation = generation;
            Changes = changes;
            ConsumedIndices = consumedIndices;
            IgnoredCount = ignoredCount;
        }

        internal FireSimChangeQueue Owner { get; }
        internal int Generation { get; }
        internal int[] ConsumedIndices { get; }
        public FireSimChange[] Changes { get; }
        public int IgnoredCount { get; }
    }
}
