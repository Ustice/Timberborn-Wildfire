using Wildfire.Core;

namespace Wildfire.Unity;

public sealed class ComputeBufferGrid : IDisposable
{
    public const int PackedCellStrideBytes = sizeof(uint);
    public const int ChangeStrideBytes = sizeof(uint) * 4;
    public const int DeltaStrideBytes = sizeof(uint) * 4;
    public const int GenerationStrideBytes = sizeof(uint);
    public const int VisualFieldStrideBytes = FireVisualField.StrideBytes;
    public const int CompanionTargetIdStrideBytes = sizeof(uint);
    public const int CompanionFieldStrideBytes = sizeof(uint);

    private readonly List<IComputeBufferHandle> _ownedBuffers;
    private bool _disposed;

    public ComputeBufferGrid(
        ComputeGridDimensions dimensions,
        ReadOnlySpan<ushort> initialCells,
        IComputeBufferAllocator allocator)
        : this(dimensions, initialCells, ReadOnlySpan<WildfireCompanionField>.Empty, allocator)
    {
    }

    public ComputeBufferGrid(
        ComputeGridDimensions dimensions,
        ReadOnlySpan<ushort> initialCells,
        ReadOnlySpan<WildfireCompanionField> initialCompanionFields,
        IComputeBufferAllocator allocator)
    {
        ArgumentNullException.ThrowIfNull(allocator);
        Dimensions = dimensions;

        ComputeGridValidation.RequireCellCount(dimensions, initialCells.Length, nameof(initialCells));
        if (!initialCompanionFields.IsEmpty)
        {
            ComputeGridValidation.RequireCellCount(dimensions, initialCompanionFields.Length, nameof(initialCompanionFields));
        }

        List<IComputeBufferHandle> ownedBuffers = [];

        try
        {
            IComputeBufferHandle currentCells = AllocateTracked(allocator, ownedBuffers, "wildfire.current_cells", dimensions.CellCount, PackedCellStrideBytes);
            IComputeBufferHandle nextCells = AllocateTracked(allocator, ownedBuffers, "wildfire.next_cells", dimensions.CellCount, PackedCellStrideBytes);
            IComputeBufferHandle queuedChanges = AllocateTracked(allocator, ownedBuffers, "wildfire.queued_changes", dimensions.CellCount, ChangeStrideBytes);
            IAppendComputeBufferHandle deltas = AllocateAppendTracked(allocator, ownedBuffers, "wildfire.deltas", dimensions.CellCount, DeltaStrideBytes);
            IComputeBufferHandle generations = AllocateTracked(allocator, ownedBuffers, "wildfire.generations", dimensions.CellCount, GenerationStrideBytes);
            IComputeBufferHandle visualFields = AllocateTracked(allocator, ownedBuffers, "wildfire.visual_fields", dimensions.CellCount, VisualFieldStrideBytes);
            IComputeBufferHandle companionTargetIds = AllocateTracked(allocator, ownedBuffers, "wildfire.companion_target_ids", dimensions.CellCount, CompanionTargetIdStrideBytes);
            IComputeBufferHandle companionFields = AllocateTracked(allocator, ownedBuffers, "wildfire.companion_fields", dimensions.CellCount, CompanionFieldStrideBytes);

            uint[] packedCells = initialCells.ToArray().Select(static cell => (uint)cell).ToArray();
            WildfireCompanionField[] companionValues = initialCompanionFields.IsEmpty
                ? Enumerable.Repeat(WildfireCompanionField.Empty, dimensions.CellCount).ToArray()
                : initialCompanionFields.ToArray();
            currentCells.Upload(packedCells);
            nextCells.Upload(packedCells);
            companionTargetIds.Upload(companionValues.Select(static field => field.TargetId).ToArray());
            companionFields.Upload(companionValues.Select(static field => field.State.Pack()).ToArray());

            CurrentCells = currentCells;
            NextCells = nextCells;
            QueuedChanges = queuedChanges;
            Deltas = deltas;
            Generations = generations;
            VisualFields = visualFields;
            CompanionTargetIds = companionTargetIds;
            CompanionFields = companionFields;
            _ownedBuffers = ownedBuffers;
        }
        catch
        {
            ownedBuffers.ForEach(static buffer => buffer.Dispose());
            throw;
        }
    }

    public ComputeGridDimensions Dimensions { get; }

    public int Width => Dimensions.Width;

    public int Height => Dimensions.Height;

    public int Depth => Dimensions.Depth;

    public int CellCount => Dimensions.CellCount;

    public IComputeBufferHandle CurrentCells { get; private set; }

    public IComputeBufferHandle NextCells { get; private set; }

    public IComputeBufferHandle QueuedChanges { get; }

    public IAppendComputeBufferHandle Deltas { get; }

    public IComputeBufferHandle Generations { get; }

    public IComputeBufferHandle VisualFields { get; }

    public IComputeBufferHandle CompanionTargetIds { get; }

    public IComputeBufferHandle CompanionFields { get; }

    public static ComputeBufferGrid FromCells(
        int width,
        int height,
        int depth,
        ReadOnlySpan<ushort> initialCells,
        IComputeBufferAllocator allocator)
    {
        return new ComputeBufferGrid(new ComputeGridDimensions(width, height, depth), initialCells, allocator);
    }

    public static ComputeBufferGrid FromCells(
        int width,
        int height,
        int depth,
        ReadOnlySpan<ushort> initialCells,
        ReadOnlySpan<WildfireCompanionField> initialCompanionFields,
        IComputeBufferAllocator allocator)
    {
        return new ComputeBufferGrid(
            new ComputeGridDimensions(width, height, depth),
            initialCells,
            initialCompanionFields,
            allocator);
    }

    public void SwapCellBuffers()
    {
        (CurrentCells, NextCells) = (NextCells, CurrentCells);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _ownedBuffers.ForEach(static buffer => buffer.Dispose());
        _disposed = true;
    }

    private static IComputeBufferHandle AllocateTracked(
        IComputeBufferAllocator allocator,
        List<IComputeBufferHandle> ownedBuffers,
        string name,
        int count,
        int strideBytes)
    {
        IComputeBufferHandle buffer = allocator.Allocate(name, count, strideBytes);
        ownedBuffers.Add(buffer);
        return buffer;
    }

    private static IAppendComputeBufferHandle AllocateAppendTracked(
        IComputeBufferAllocator allocator,
        List<IComputeBufferHandle> ownedBuffers,
        string name,
        int count,
        int strideBytes)
    {
        IAppendComputeBufferHandle buffer = allocator.AllocateAppend(name, count, strideBytes);
        ownedBuffers.Add(buffer);
        return buffer;
    }
}

public readonly record struct ComputeGridDimensions
{
    public ComputeGridDimensions(int width, int height, int depth)
    {
        ComputeGridValidation.RequirePositiveDimension(width, nameof(width));
        ComputeGridValidation.RequirePositiveDimension(height, nameof(height));
        ComputeGridValidation.RequirePositiveDimension(depth, nameof(depth));

        Width = width;
        Height = height;
        Depth = depth;
        CellCount = ComputeGridValidation.GetCheckedCellCount(width, height, depth);
    }

    public int Width { get; }

    public int Height { get; }

    public int Depth { get; }

    public int CellCount { get; }

    public FireGrid ToFireGrid()
    {
        return new FireGrid(Width, Height, Depth);
    }
}
