using Wildfire.Core;
using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed partial class ComputeBufferGridTests
{
    [Fact]
    public void FailedSecondAllocationLeavesCompleteOldPairAndDisposesOnlyUnpublishedCandidate()
    {
        var allocator = new RecordingComputeBufferAllocator();
        var grid = ComputeBufferGrid.FromCells(1, 1, 1, [0], allocator);
        var oldCommands = (RecordingComputeBufferHandle)grid.QueuedChanges;
        var oldDeltas = (RecordingComputeBufferHandle)grid.Deltas;
        int allocated = allocator.Handles.Count;
        allocator.FailAllocationName = "wildfire.deltas";
        Assert.Throws<InvalidOperationException>(() => grid.ReserveStepCapacity(4));
        Assert.Same(oldCommands, grid.QueuedChanges);
        Assert.Same(oldDeltas, grid.Deltas);
        Assert.Equal(0, oldCommands.DisposeCalls);
        Assert.Equal(0, oldDeltas.DisposeCalls);
        Assert.Equal(1, allocator.Handles[allocated].DisposeCalls);
        allocator.FailAllocationName = null;
        grid.ReserveStepCapacity(4);
        Assert.Equal(4, grid.QueuedChanges.Count);
        Assert.Equal(5, grid.Deltas.Count);
        Assert.Equal(1, grid.OrdinaryChangeCapacity);
        Assert.Equal(1, oldCommands.DisposeCalls);
        Assert.Equal(1, oldDeltas.DisposeCalls);
        grid.ReserveStepCapacity(2); // Existing pair is reused, not shrunk.
        Assert.Equal(4, grid.QueuedChanges.Count);
        grid.Dispose(); grid.Dispose();
        Assert.All(allocator.Handles, buffer => Assert.Equal(1, buffer.DisposeCalls));
    }

    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(134217728)] // 16-byte command product exceeds signed native allocation byte count.
    [InlineData(107374182)] // (cell1 + commands) * 20-byte deltas overflows first.
    public void OversizedControlScratchFailsBeforeAllocation(int commands)
    {
        var allocator = new RecordingComputeBufferAllocator();
        using var grid = ComputeBufferGrid.FromCells(1, 1, 1, [0], allocator);
        int count = allocator.Handles.Count;
        Assert.Throws<OverflowException>(() => grid.ReserveStepCapacity(commands));
        Assert.Equal(count, allocator.Handles.Count);
        Assert.Equal(1, grid.QueuedChanges.Count);
    }
}
