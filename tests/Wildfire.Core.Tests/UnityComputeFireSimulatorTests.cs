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
        RecordingFireSimComputeDispatcher dispatcher = new()
        {
            BeforeDispatch = dispatch => Assert.Equal(1, ((RecordingComputeBufferHandle)dispatch.Deltas).ResetAppendCounterCalls),
        };
        UnityComputeFireSimulator simulator = new(grid, dispatcher, seed: 1234);

        GpuFireStepResult result = simulator.Tick();
        RecordingComputeBufferHandle deltas = (RecordingComputeBufferHandle)grid.Deltas;

        Assert.Equal(1u, result.Tick);
        Assert.Empty(result.Deltas);
        Assert.Equal(1, deltas.ResetAppendCounterCalls);
        Assert.Equal(1, deltas.ReadAppendCounterCalls);
        Assert.Empty(deltas.ReadAppendedDataCounts);
        FireSimComputeDispatch dispatch = Assert.Single(dispatcher.Dispatches);
        Assert.Equal(UnityComputeFireSimulator.FullGridKernelName, dispatch.KernelName);
        Assert.Equal(new ComputeGridDimensions(17, 9, 5), dispatch.Dimensions);
        Assert.Equal(1u, dispatch.Tick);
        Assert.Equal(1234u, dispatch.Seed);
        Assert.Same(originalCurrentCells, dispatch.CurrentCells);
        Assert.Same(originalNextCells, dispatch.NextCells);
        Assert.Same(grid.QueuedChanges, dispatch.QueuedChanges);
        Assert.Same(grid.Deltas, dispatch.Deltas);
        Assert.Same(grid.VisualFields, dispatch.VisualFields);
        Assert.Equal(0u, dispatch.ChangeCount);
        Assert.Equal(3, dispatch.ThreadGroupsX);
        Assert.Equal(2, dispatch.ThreadGroupsY);
        Assert.Equal(2, dispatch.ThreadGroupsZ);
        Assert.Same(originalNextCells, grid.CurrentCells);
        Assert.Same(originalCurrentCells, grid.NextCells);
    }

    [Fact]
    public void TickReturnsCompactDeltasAfterReadback()
    {
        RecordingComputeBufferAllocator allocator = new();

        using ComputeBufferGrid grid = ComputeBufferGrid.FromCells(
            width: 3,
            height: 1,
            depth: 1,
            new ushort[3],
            allocator);
        RecordingComputeBufferHandle deltas = (RecordingComputeBufferHandle)grid.Deltas;
        RecordingFireSimComputeDispatcher dispatcher = new()
        {
            AfterDispatch = dispatch =>
            {
                if (dispatch.KernelName != UnityComputeFireSimulator.FullGridKernelName)
                {
                    return;
                }

                deltas.AppendCounter = 2;
                deltas.AppendedData =
                [
                    1u,
                    0x1234u,
                    0x5678u,
                    0u,
                    2u,
                    0x9ABCu,
                    0xDEF0u,
                    0u,
                ];
            },
        };
        UnityComputeFireSimulator simulator = new(grid, dispatcher);

        GpuFireStepResult result = simulator.Tick();

        Assert.Equal(
            [
                new CellDelta(1, 0x1234, 0x5678),
                new CellDelta(2, 0x9ABC, 0xDEF0),
            ],
            result.Deltas);
        Assert.Equal([2], deltas.ReadAppendedDataCounts);
    }

    [Fact]
    public void TickNotifiesListenersAfterDeltaReadback()
    {
        RecordingComputeBufferAllocator allocator = new();

        using ComputeBufferGrid grid = ComputeBufferGrid.FromCells(
            width: 1,
            height: 1,
            depth: 1,
            [0],
            allocator);
        RecordingComputeBufferHandle deltas = (RecordingComputeBufferHandle)grid.Deltas;
        RecordingFireSimComputeDispatcher dispatcher = new()
        {
            AfterDispatch = dispatch =>
            {
                if (dispatch.KernelName != UnityComputeFireSimulator.FullGridKernelName)
                {
                    return;
                }

                deltas.AppendCounter = 1;
                deltas.AppendedData = [0u, 0u, 0x101u, 0u];
            },
        };
        UnityComputeFireSimulator simulator = new(grid, dispatcher);
        RecordingFireSimListener listener = new();

        using IDisposable subscription = simulator.Subscribe(listener);

        simulator.Tick();

        CellDelta[] notification = Assert.Single(listener.Notifications);
        Assert.Equal([new CellDelta(0, 0, 0x101)], notification);

        subscription.Dispose();
        simulator.Tick();

        Assert.Single(listener.Notifications);
    }

    [Fact]
    public void TickReturnsExternalChangeDeltaWhenSimulationDoesNotFurtherChangeCell()
    {
        ushort newCell = PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 1, terrain: 0, heatLoss: 0);
        RecordingComputeBufferAllocator allocator = new();

        using ComputeBufferGrid grid = ComputeBufferGrid.FromCells(
            width: 1,
            height: 1,
            depth: 1,
            [0],
            allocator);
        RecordingComputeBufferHandle deltas = (RecordingComputeBufferHandle)grid.Deltas;
        RecordingFireSimComputeDispatcher dispatcher = new()
        {
            AfterDispatch = dispatch =>
            {
                if (dispatch.KernelName != UnityComputeFireSimulator.ApplyExternalChangesKernelName)
                {
                    return;
                }

                deltas.AppendCounter = 1;
                deltas.AppendedData = [0u, 0u, newCell, 0u];
            },
        };
        UnityComputeFireSimulator simulator = new(grid, dispatcher);

        simulator.RegisterChange(new FireSimChange(CellIndex: 0, SetWater: 1));

        GpuFireStepResult result = simulator.Tick();

        Assert.Equal([new CellDelta(0, 0, newCell)], result.Deltas);
        Assert.Equal(
            [
                UnityComputeFireSimulator.ApplyExternalChangesKernelName,
                UnityComputeFireSimulator.FullGridKernelName,
            ],
            dispatcher.Dispatches.Select(static dispatch => dispatch.KernelName).ToArray());
    }

    [Fact]
    public void TickNotifiesListenersOfExternalChangeOnlyDelta()
    {
        ushort newCell = PackedCell.Pack(fuel: 3, heat: 0, flammability: 0, water: 0, terrain: 0, heatLoss: 0);
        RecordingComputeBufferAllocator allocator = new();

        using ComputeBufferGrid grid = ComputeBufferGrid.FromCells(
            width: 1,
            height: 1,
            depth: 1,
            [0],
            allocator);
        RecordingComputeBufferHandle deltas = (RecordingComputeBufferHandle)grid.Deltas;
        RecordingFireSimComputeDispatcher dispatcher = new()
        {
            AfterDispatch = dispatch =>
            {
                if (dispatch.KernelName != UnityComputeFireSimulator.ApplyExternalChangesKernelName)
                {
                    return;
                }

                deltas.AppendCounter = 1;
                deltas.AppendedData = [0u, 0u, newCell, 0u];
            },
        };
        UnityComputeFireSimulator simulator = new(grid, dispatcher);
        RecordingFireSimListener listener = new();

        using IDisposable subscription = simulator.Subscribe(listener);
        simulator.RegisterChange(new FireSimChange(CellIndex: 0, AddFuel: 3));

        simulator.Tick();

        CellDelta[] notification = Assert.Single(listener.Notifications);
        Assert.Equal([new CellDelta(0, 0, newCell)], notification);
    }

    [Fact]
    public void TickRejectsDeltaCounterPastBufferCapacity()
    {
        RecordingComputeBufferAllocator allocator = new();

        using ComputeBufferGrid grid = ComputeBufferGrid.FromCells(
            width: 1,
            height: 1,
            depth: 1,
            [0],
            allocator);
        RecordingComputeBufferHandle deltas = (RecordingComputeBufferHandle)grid.Deltas;
        RecordingFireSimComputeDispatcher dispatcher = new()
        {
            AfterDispatch = dispatch =>
            {
                if (dispatch.KernelName == UnityComputeFireSimulator.FullGridKernelName)
                {
                    deltas.AppendCounter = 2;
                }
            },
        };
        UnityComputeFireSimulator simulator = new(grid, dispatcher);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => simulator.Tick());

        Assert.Equal("GPU delta counter returned 2, but buffer capacity is 1.", exception.Message);
    }

    [Fact]
    public void TickRequiresGridAndDispatcher()
    {
        UnityComputeFireSimulator simulator = new(width: 1, height: 1, depth: 1);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => simulator.Tick());

        Assert.Equal("GPU compute simulation requires a buffer grid and compute dispatcher.", exception.Message);
    }

    [Fact]
    public void TickUploadsRegisteredChangesBeforeFullGridDispatch()
    {
        ushort setCell = PackedCell.Pack(1, 2, 3, 0, 1, 2);
        RecordingComputeBufferAllocator allocator = new();

        using ComputeBufferGrid grid = ComputeBufferGrid.FromCells(
            width: 2,
            height: 1,
            depth: 1,
            new ushort[2],
            allocator);
        RecordingFireSimComputeDispatcher dispatcher = new();
        UnityComputeFireSimulator simulator = new(grid, dispatcher);

        simulator.RegisterChange(
            new FireSimChange(
                CellIndex: 1,
                SetCell: setCell,
                AddHeat: 3,
                AddFuel: 2,
                SetWater: 1,
                SetFuel: 7,
                SetHeat: 8,
                SetFlammability: 2,
                SetHeatLoss: 6,
                SetTerrain: 1));

        Assert.Equal(1, simulator.PendingChangeCount);
        Assert.Empty(((RecordingComputeBufferHandle)grid.QueuedChanges).UploadedValues);

        simulator.Tick();

        Assert.Equal(0, simulator.PendingChangeCount);
        Assert.Equal(0, simulator.LastIgnoredChangeCount);
        Assert.Equal(1, simulator.LastUploadedChangeCount);
        Assert.Collection(
            dispatcher.Dispatches,
            apply =>
            {
                Assert.Equal(UnityComputeFireSimulator.ApplyExternalChangesKernelName, apply.KernelName);
                Assert.Equal(1u, apply.Tick);
                Assert.Same(grid.QueuedChanges, apply.QueuedChanges);
                Assert.Same(grid.VisualFields, apply.VisualFields);
                Assert.Equal(1u, apply.ChangeCount);
                Assert.Equal(1, apply.ThreadGroupsX);
                Assert.Equal(1, apply.ThreadGroupsY);
                Assert.Equal(1, apply.ThreadGroupsZ);
            },
            simulate =>
            {
                Assert.Equal(UnityComputeFireSimulator.FullGridKernelName, simulate.KernelName);
                Assert.Same(grid.VisualFields, simulate.VisualFields);
                Assert.Equal(0u, simulate.ChangeCount);
            });
        Assert.Equal(
            [
                1u,
                0b111_1111u,
                0x23u,
                (uint)setCell |
                    (1u << 16) |
                    (7u << 18) |
                    (8u << 22) |
                    (2u << 26) |
                    (6u << 28) |
                    (1u << 31),
                0u,
                0u,
                0u,
                0u,
            ],
            ((RecordingComputeBufferHandle)grid.QueuedChanges).UploadedValues);
    }

    [Fact]
    public void TickIgnoresOutOfRangeChangesWithoutUploadingThem()
    {
        RecordingComputeBufferAllocator allocator = new();

        using ComputeBufferGrid grid = ComputeBufferGrid.FromCells(
            width: 2,
            height: 1,
            depth: 1,
            new ushort[2],
            allocator);
        RecordingFireSimComputeDispatcher dispatcher = new();
        UnityComputeFireSimulator simulator = new(grid, dispatcher);

        simulator.RegisterChange(new FireSimChange(CellIndex: -1, AddHeat: 1));
        simulator.RegisterChange(new FireSimChange(CellIndex: 2, AddFuel: 1));

        simulator.Tick();

        Assert.Equal(0, simulator.PendingChangeCount);
        Assert.Equal(2, simulator.LastIgnoredChangeCount);
        Assert.Equal(0, simulator.LastUploadedChangeCount);
        FireSimComputeDispatch dispatch = Assert.Single(dispatcher.Dispatches);
        Assert.Equal(UnityComputeFireSimulator.FullGridKernelName, dispatch.KernelName);
        Assert.Empty(((RecordingComputeBufferHandle)grid.QueuedChanges).UploadedValues);
    }

    [Fact]
    public void TickProcessesQueuedChangesInCapacitySizedChunks()
    {
        RecordingComputeBufferAllocator allocator = new();

        using ComputeBufferGrid grid = ComputeBufferGrid.FromCells(
            width: 2,
            height: 1,
            depth: 1,
            new ushort[2],
            allocator);
        RecordingFireSimComputeDispatcher dispatcher = new();
        UnityComputeFireSimulator simulator = new(grid, dispatcher);

        simulator.RegisterChange(new FireSimChange(CellIndex: 0, AddHeat: 1));
        simulator.RegisterChange(new FireSimChange(CellIndex: 1, AddHeat: 2));
        simulator.RegisterChange(new FireSimChange(CellIndex: 0, AddHeat: 3));

        simulator.Tick();

        RecordingComputeBufferHandle queuedChanges = (RecordingComputeBufferHandle)grid.QueuedChanges;
        Assert.Equal(1, simulator.PendingChangeCount);
        Assert.Equal(2, simulator.LastUploadedChangeCount);
        Assert.Equal(0x1u, queuedChanges.UploadHistory[0][2]);
        Assert.Equal(0x2u, queuedChanges.UploadHistory[0][6]);

        simulator.Tick();

        Assert.Equal(0, simulator.PendingChangeCount);
        Assert.Equal(1, simulator.LastUploadedChangeCount);
        Assert.Equal(0x3u, queuedChanges.UploadHistory[1][2]);
        Assert.Equal(
            [
                UnityComputeFireSimulator.ApplyExternalChangesKernelName,
                UnityComputeFireSimulator.FullGridKernelName,
                UnityComputeFireSimulator.ApplyExternalChangesKernelName,
                UnityComputeFireSimulator.FullGridKernelName,
            ],
            dispatcher.Dispatches.Select(static dispatch => dispatch.KernelName).ToArray());
    }

    [Fact]
    public void TickPreservesQueuedChangesWhenApplyDispatchFails()
    {
        RecordingComputeBufferAllocator allocator = new();

        using ComputeBufferGrid grid = ComputeBufferGrid.FromCells(
            width: 1,
            height: 1,
            depth: 1,
            [0],
            allocator);
        RecordingFireSimComputeDispatcher dispatcher = new()
        {
            ThrowOnKernelName = UnityComputeFireSimulator.ApplyExternalChangesKernelName,
        };
        UnityComputeFireSimulator simulator = new(grid, dispatcher);
        simulator.RegisterChange(new FireSimChange(CellIndex: 0, AddHeat: 1));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => simulator.Tick());

        Assert.Equal($"Dispatch failed for {UnityComputeFireSimulator.ApplyExternalChangesKernelName}.", exception.Message);
        Assert.Equal(1, simulator.PendingChangeCount);
        FireSimComputeDispatch dispatch = Assert.Single(dispatcher.Dispatches);
        Assert.Equal(UnityComputeFireSimulator.ApplyExternalChangesKernelName, dispatch.KernelName);
    }

    [Fact]
    public void TickConsumesAppliedChangesWhenFullGridDispatchFails()
    {
        RecordingComputeBufferAllocator allocator = new();

        using ComputeBufferGrid grid = ComputeBufferGrid.FromCells(
            width: 1,
            height: 1,
            depth: 1,
            [0],
            allocator);
        RecordingFireSimComputeDispatcher dispatcher = new()
        {
            ThrowOnKernelName = UnityComputeFireSimulator.FullGridKernelName,
        };
        UnityComputeFireSimulator simulator = new(grid, dispatcher);
        simulator.RegisterChange(new FireSimChange(CellIndex: 0, AddHeat: 1));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => simulator.Tick());

        Assert.Equal($"Dispatch failed for {UnityComputeFireSimulator.FullGridKernelName}.", exception.Message);
        Assert.Equal(0, simulator.PendingChangeCount);
        Assert.Equal(
            [
                UnityComputeFireSimulator.ApplyExternalChangesKernelName,
                UnityComputeFireSimulator.FullGridKernelName,
            ],
            dispatcher.Dispatches.Select(static dispatch => dispatch.KernelName).ToArray());

        dispatcher.ThrowOnKernelName = null;
        simulator.Tick();

        Assert.Equal(
            [
                UnityComputeFireSimulator.ApplyExternalChangesKernelName,
                UnityComputeFireSimulator.FullGridKernelName,
                UnityComputeFireSimulator.FullGridKernelName,
            ],
            dispatcher.Dispatches.Select(static dispatch => dispatch.KernelName).ToArray());
        Assert.Equal(2u, dispatcher.Dispatches[^1].Tick);
    }

    [Fact]
    public void ChangeUploadClampsFieldValuesToPackedLimits()
    {
        uint[] encoded = FireSimChangeUpload.Encode(
            [
                new FireSimChange(
                    CellIndex: 0,
                    AddHeat: 16,
                    AddFuel: 17,
                    SetWater: 4,
                    SetFuel: 16,
                    SetHeat: 17,
                    SetFlammability: 4,
                    SetHeatLoss: 8,
                    SetTerrain: 2),
            ],
            capacity: 1);

        Assert.Equal(0xFFu, encoded[2]);
        Assert.Equal(
            (3u << 16) |
                (15u << 18) |
                (15u << 22) |
                (3u << 26) |
                (7u << 28) |
                (1u << 31),
            encoded[3]);
    }

    [Fact]
    public void TickKeepsRegisteredChangesWhenChangeUploadFails()
    {
        RecordingComputeBufferAllocator allocator = new();

        using ComputeBufferGrid grid = ComputeBufferGrid.FromCells(
            width: 1,
            height: 1,
            depth: 1,
            [0],
            allocator);
        ((RecordingComputeBufferHandle)grid.QueuedChanges).FailUpload = true;
        RecordingFireSimComputeDispatcher dispatcher = new();
        UnityComputeFireSimulator simulator = new(grid, dispatcher);
        simulator.RegisterChange(new FireSimChange(CellIndex: 0, AddHeat: 1));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => simulator.Tick());

        Assert.Equal("Upload failed for wildfire.queued_changes.", exception.Message);
        Assert.Equal(1, simulator.PendingChangeCount);
        Assert.Empty(dispatcher.Dispatches);
    }

    private sealed class RecordingFireSimComputeDispatcher : IFireSimComputeDispatcher
    {
        public List<FireSimComputeDispatch> Dispatches { get; } = [];

        public string? ThrowOnKernelName { get; set; }

        public Action<FireSimComputeDispatch>? BeforeDispatch { get; init; }

        public Action<FireSimComputeDispatch>? AfterDispatch { get; init; }

        public void Dispatch(FireSimComputeDispatch dispatch)
        {
            BeforeDispatch?.Invoke(dispatch);
            Dispatches.Add(dispatch);

            if (dispatch.KernelName == ThrowOnKernelName)
            {
                throw new InvalidOperationException($"Dispatch failed for {dispatch.KernelName}.");
            }

            AfterDispatch?.Invoke(dispatch);
        }
    }

    private sealed class RecordingComputeBufferAllocator : IComputeBufferAllocator
    {
        public IComputeBufferHandle Allocate(string name, int count, int strideBytes)
        {
            return new RecordingComputeBufferHandle(name, count, strideBytes);
        }

        public IAppendComputeBufferHandle AllocateAppend(string name, int count, int strideBytes)
        {
            return new RecordingComputeBufferHandle(name, count, strideBytes);
        }
    }

    private sealed class RecordingComputeBufferHandle(string name, int count, int strideBytes) : IAppendComputeBufferHandle
    {
        public string Name { get; } = name;

        public int Count { get; } = count;

        public int StrideBytes { get; } = strideBytes;

        public uint[] UploadedValues { get; private set; } = [];

        public List<uint[]> UploadHistory { get; } = [];

        public bool FailUpload { get; set; }

        public int ResetAppendCounterCalls { get; private set; }

        public int ReadAppendCounterCalls { get; private set; }

        public int AppendCounter { get; set; }

        public uint[] AppendedData { get; set; } = [];

        public List<int> ReadAppendedDataCounts { get; } = [];

        public void Upload(ReadOnlySpan<uint> values)
        {
            if (FailUpload)
            {
                throw new InvalidOperationException($"Upload failed for {Name}.");
            }

            UploadedValues = values.ToArray();
            UploadHistory.Add(UploadedValues);
        }

        public void ResetAppendCounter()
        {
            ResetAppendCounterCalls++;
            AppendCounter = 0;
        }

        public int ReadAppendCounter()
        {
            ReadAppendCounterCalls++;
            return AppendCounter;
        }

        public uint[] ReadAppendedData(int elementCount)
        {
            ReadAppendedDataCounts.Add(elementCount);
            return AppendedData;
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingFireSimListener : IFireSimListener
    {
        public List<CellDelta[]> Notifications { get; } = [];

        public void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas)
        {
            Notifications.Add(deltas.ToArray());
        }
    }
}
