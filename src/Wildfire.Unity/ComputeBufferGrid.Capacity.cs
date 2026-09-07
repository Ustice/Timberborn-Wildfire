using Wildfire.Core;

namespace Wildfire.Unity;

public sealed partial class ComputeBufferGrid
{
    public int OrdinaryChangeCapacity => Dimensions.CellCount;

    // Scratch storage may grow for ordered material control; ordinary admission does not.
    public void ReserveStepCapacity(int orderedCommandCount)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int deltaCount = FireSimGpuProtocol.ValidateStepBufferCapacity(CellCount, orderedCommandCount);
        if (QueuedChanges.Count >= orderedCommandCount && Deltas.Count >= deltaCount) return;
        IComputeBufferHandle? commands = null;
        IAppendComputeBufferHandle? deltas = null;
        try
        {
            commands = _allocator.Allocate("wildfire.queued_changes", orderedCommandCount, ChangeStrideBytes);
            deltas = _allocator.AllocateAppend("wildfire.deltas", deltaCount, DeltaStrideBytes);
        }
        catch
        {
            commands?.Dispose();
            deltas?.Dispose();
            throw;
        }
        var oldCommands = QueuedChanges;
        var oldDeltas = Deltas;
        _ownedBuffers.Add(commands);
        _ownedBuffers.Add(deltas);
        QueuedChanges = commands;
        Deltas = deltas;
        _ownedBuffers.Remove(oldCommands);
        _ownedBuffers.Remove(oldDeltas);
        // Dispatch objects are created after preparation and read this complete current pair.
        try { oldCommands.Dispose(); }
        finally { oldDeltas.Dispose(); }
    }
}
