using Wildfire.Core;
using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed class ComputeBufferGridTests
{
    [Fact]
    public void DimensionsCalculateCellCount()
    {
        ComputeGridDimensions dimensions = new(width: 4, height: 3, depth: 2);

        Assert.Equal(4, dimensions.Width);
        Assert.Equal(3, dimensions.Height);
        Assert.Equal(2, dimensions.Depth);
        Assert.Equal(24, dimensions.CellCount);
        Assert.Equal(new FireGrid(4, 3, 2), dimensions.ToFireGrid());
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 0)]
    public void DimensionsRejectNonPositiveValues(int width, int height, int depth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ComputeGridDimensions(width, height, depth));
    }

    [Fact]
    public void DimensionsRejectOverflowingCellCounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ComputeGridDimensions(int.MaxValue, 2, 2));
    }

    [Fact]
    public void GridRejectsInitialCellCountMismatch()
    {
        RecordingComputeBufferAllocator allocator = new();

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => ComputeBufferGrid.FromCells(width: 2, height: 2, depth: 1, [1, 2, 3], allocator));

        Assert.Contains("Expected 4, got 3", exception.Message);
        Assert.Empty(allocator.Handles);
    }

    [Fact]
    public void GridAllocatesExpectedBuffersAndUploadsPackedCellsAsUInts()
    {
        ushort[] cells =
        [
            PackedCell.Pack(1, 2, 3, 0, 1, 2),
            PackedCell.Pack(4, 5, 2, 1, 1, 3),
        ];
        RecordingComputeBufferAllocator allocator = new();

        using ComputeBufferGrid grid = ComputeBufferGrid.FromCells(width: 2, height: 1, depth: 1, cells, allocator);

        Assert.Equal(2, grid.CellCount);
        Assert.Equal(
            [
                "wildfire.current_cells",
                "wildfire.next_cells",
                "wildfire.queued_changes",
                "wildfire.deltas",
                "wildfire.generations",
                "wildfire.visual_fields",
            ],
            allocator.Handles.Select(static handle => handle.Name).ToArray());
        Assert.All(allocator.Handles, static handle => Assert.Equal(2, handle.Count));
        Assert.Equal(ComputeBufferGrid.PackedCellStrideBytes, grid.CurrentCells.StrideBytes);
        Assert.Equal(ComputeBufferGrid.PackedCellStrideBytes, grid.NextCells.StrideBytes);
        Assert.Equal(ComputeBufferGrid.ChangeStrideBytes, grid.QueuedChanges.StrideBytes);
        Assert.Equal(ComputeBufferGrid.DeltaStrideBytes, grid.Deltas.StrideBytes);
        Assert.True(((RecordingComputeBufferHandle)grid.Deltas).IsAppend);
        Assert.Equal(ComputeBufferGrid.GenerationStrideBytes, grid.Generations.StrideBytes);
        Assert.Equal(ComputeBufferGrid.VisualFieldStrideBytes, grid.VisualFields.StrideBytes);
        Assert.Equal(cells.Select(static cell => (uint)cell).ToArray(), ((RecordingComputeBufferHandle)grid.CurrentCells).UploadedValues);
        Assert.Equal(cells.Select(static cell => (uint)cell).ToArray(), ((RecordingComputeBufferHandle)grid.NextCells).UploadedValues);
    }

    [Fact]
    public void GridDisposesOwnedBuffers()
    {
        RecordingComputeBufferAllocator allocator = new();

        ComputeBufferGrid grid = ComputeBufferGrid.FromCells(width: 1, height: 1, depth: 1, [42], allocator);
        grid.Dispose();
        grid.Dispose();

        Assert.All(allocator.Handles, static handle => Assert.Equal(1, handle.DisposeCalls));
    }

    [Fact]
    public void GridDisposesAllocatedBuffersWhenAllocationFails()
    {
        RecordingComputeBufferAllocator allocator = new()
        {
            FailAllocationName = "wildfire.deltas",
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ComputeBufferGrid.FromCells(width: 1, height: 1, depth: 1, [42], allocator));

        Assert.Equal("Allocation failed for wildfire.deltas.", exception.Message);
        Assert.Equal(
            [
                "wildfire.current_cells",
                "wildfire.next_cells",
                "wildfire.queued_changes",
            ],
            allocator.Handles.Select(static handle => handle.Name).ToArray());
        Assert.All(allocator.Handles, static handle => Assert.Equal(1, handle.DisposeCalls));
    }

    [Fact]
    public void GridDisposesAllocatedBuffersWhenUploadFails()
    {
        RecordingComputeBufferAllocator allocator = new()
        {
            FailUploadName = "wildfire.next_cells",
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ComputeBufferGrid.FromCells(width: 1, height: 1, depth: 1, [42], allocator));

        Assert.Equal("Upload failed for wildfire.next_cells.", exception.Message);
        Assert.Equal(
            [
                "wildfire.current_cells",
                "wildfire.next_cells",
                "wildfire.queued_changes",
                "wildfire.deltas",
                "wildfire.generations",
                "wildfire.visual_fields",
            ],
            allocator.Handles.Select(static handle => handle.Name).ToArray());
        Assert.All(allocator.Handles, static handle => Assert.Equal(1, handle.DisposeCalls));
    }

    private sealed class RecordingComputeBufferAllocator : IComputeBufferAllocator
    {
        public List<RecordingComputeBufferHandle> Handles { get; } = [];

        public string? FailAllocationName { get; init; }

        public string? FailUploadName { get; init; }

        public IComputeBufferHandle Allocate(string name, int count, int strideBytes)
        {
            if (name == FailAllocationName)
            {
                throw new InvalidOperationException($"Allocation failed for {name}.");
            }

            RecordingComputeBufferHandle handle = new(name, count, strideBytes);
            handle.FailUpload = name == FailUploadName;
            Handles.Add(handle);
            return handle;
        }

        public IAppendComputeBufferHandle AllocateAppend(string name, int count, int strideBytes)
        {
            if (name == FailAllocationName)
            {
                throw new InvalidOperationException($"Allocation failed for {name}.");
            }

            RecordingComputeBufferHandle handle = new(name, count, strideBytes)
            {
                FailUpload = name == FailUploadName,
                IsAppend = true,
            };
            Handles.Add(handle);
            return handle;
        }
    }

    private sealed class RecordingComputeBufferHandle(string name, int count, int strideBytes) : IAppendComputeBufferHandle
    {
        public string Name { get; } = name;

        public int Count { get; } = count;

        public int StrideBytes { get; } = strideBytes;

        public int DisposeCalls { get; private set; }

        public uint[] UploadedValues { get; private set; } = [];

        public bool FailUpload { get; set; }

        public bool IsAppend { get; init; }

        public void Upload(ReadOnlySpan<uint> values)
        {
            if (FailUpload)
            {
                throw new InvalidOperationException($"Upload failed for {Name}.");
            }

            if (values.Length != Count)
            {
                throw new ArgumentException($"Upload length must match buffer count. Expected {Count}, got {values.Length}.", nameof(values));
            }

            UploadedValues = values.ToArray();
        }

        public void ResetAppendCounter()
        {
        }

        public int ReadAppendCounter()
        {
            return 0;
        }

        public uint[] ReadAppendedData(int elementCount)
        {
            return new uint[elementCount * (StrideBytes / sizeof(uint))];
        }

        public void Dispose()
        {
            DisposeCalls++;
        }
    }
}
