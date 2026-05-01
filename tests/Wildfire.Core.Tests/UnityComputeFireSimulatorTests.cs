using Wildfire.Core;
using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed class UnityComputeFireSimulatorTests
{
    [Fact]
    public void TickDispatchesFullGridKernelAndSwapsCellBuffers()
    {
        RecordingComputeBufferAllocator allocator = new();

        using ComputeBufferGrid grid = ComputeBufferGrid.FromCells(
            width: 17,
            height: 9,
            depth: 5,
            new ushort[17 * 9 * 5],
            allocator);
        IComputeBufferHandle originalCurrentCells = grid.CurrentCells;
        IComputeBufferHandle originalNextCells = grid.NextCells;
        RecordingFireSimComputeDispatcher dispatcher = new();
        UnityComputeFireSimulator simulator = new(grid, dispatcher, seed: 1234);

        GpuFireStepResult result = simulator.Tick();

        Assert.Equal(1u, result.Tick);
        Assert.Empty(result.Deltas);
        FireSimComputeDispatch dispatch = Assert.Single(dispatcher.Dispatches);
        Assert.Equal(UnityComputeFireSimulator.FullGridKernelName, dispatch.KernelName);
        Assert.Equal(new ComputeGridDimensions(17, 9, 5), dispatch.Dimensions);
        Assert.Equal(1u, dispatch.Tick);
        Assert.Equal(1234u, dispatch.Seed);
        Assert.Same(originalCurrentCells, dispatch.CurrentCells);
        Assert.Same(originalNextCells, dispatch.NextCells);
        Assert.Same(grid.Deltas, dispatch.Deltas);
        Assert.Equal(3, dispatch.ThreadGroupsX);
        Assert.Equal(2, dispatch.ThreadGroupsY);
        Assert.Equal(2, dispatch.ThreadGroupsZ);
        Assert.Same(originalNextCells, grid.CurrentCells);
        Assert.Same(originalCurrentCells, grid.NextCells);
    }

    [Fact]
    public void TickRequiresGridAndDispatcher()
    {
        UnityComputeFireSimulator simulator = new(width: 1, height: 1, depth: 1);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => simulator.Tick());

        Assert.Equal("GPU compute simulation requires a buffer grid and compute dispatcher.", exception.Message);
    }

    private sealed class RecordingFireSimComputeDispatcher : IFireSimComputeDispatcher
    {
        public List<FireSimComputeDispatch> Dispatches { get; } = [];

        public void Dispatch(FireSimComputeDispatch dispatch)
        {
            Dispatches.Add(dispatch);
        }
    }

    private sealed class RecordingComputeBufferAllocator : IComputeBufferAllocator
    {
        public IComputeBufferHandle Allocate(string name, int count, int strideBytes)
        {
            return new RecordingComputeBufferHandle(name, count, strideBytes);
        }
    }

    private sealed class RecordingComputeBufferHandle(string name, int count, int strideBytes) : IComputeBufferHandle
    {
        public string Name { get; } = name;

        public int Count { get; } = count;

        public int StrideBytes { get; } = strideBytes;

        public void Upload(ReadOnlySpan<uint> values)
        {
        }

        public void Dispose()
        {
        }
    }
}
