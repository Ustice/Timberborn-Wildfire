using Wildfire.Core;

namespace Wildfire.Unity;

public sealed partial class ComputeBufferGrid : IDisposable
{
    public const int PackedCellStrideBytes = sizeof(uint);
    public const int ChangeStrideBytes = FireSimGpuProtocol.ChangeStrideBytes;
    public const int DeltaStrideBytes = FireSimGpuProtocol.DeltaStrideBytes;
    public const int GenerationStrideBytes = sizeof(uint);
    public const int VisualFieldStrideBytes = FireVisualField.StrideBytes;
    public const int TransportFieldStrideBytes = sizeof(uint);
    public const int MaterialTargetIdStrideBytes = sizeof(uint);
    public const int MaterialFieldStrideBytes = sizeof(uint);
    public const int AtmosphericFieldStrideBytes = TransportFieldStrideBytes;
    public const int CompanionTargetIdStrideBytes = MaterialTargetIdStrideBytes;
    public const int CompanionFieldStrideBytes = MaterialFieldStrideBytes;

    private readonly List<IComputeBufferHandle> _ownedBuffers;
    private readonly IComputeBufferAllocator _allocator;
    private bool _disposed;

    public ComputeBufferGrid(
        ComputeGridDimensions dimensions,
        ReadOnlySpan<ushort> initialCells,
        IComputeBufferAllocator allocator)
        : this(dimensions, initialCells, ReadOnlySpan<WildfireMaterialField>.Empty, allocator)
    {
    }

    public ComputeBufferGrid(
        ComputeGridDimensions dimensions,
        ReadOnlySpan<ushort> initialCells,
        ReadOnlySpan<WildfireMaterialField> initialMaterialFields,
        IComputeBufferAllocator allocator,
        ReadOnlySpan<uint> initialSlotIds = default)
    {
        ArgumentNullException.ThrowIfNull(allocator);
        Dimensions = dimensions;
        _allocator = allocator;

        ComputeGridValidation.RequireCellCount(dimensions, initialCells.Length, nameof(initialCells));
        if (!initialMaterialFields.IsEmpty)
        {
            ComputeGridValidation.RequireCellCount(dimensions, initialMaterialFields.Length, nameof(initialMaterialFields));
        }

        List<IComputeBufferHandle> ownedBuffers = [];

        try
        {
            IComputeBufferHandle currentCells = AllocateTracked(allocator, ownedBuffers, "wildfire.current_cells", dimensions.CellCount, PackedCellStrideBytes);
            IComputeBufferHandle nextCells = AllocateTracked(allocator, ownedBuffers, "wildfire.next_cells", dimensions.CellCount, PackedCellStrideBytes);
            IComputeBufferHandle queuedChanges = AllocateTracked(allocator, ownedBuffers, "wildfire.queued_changes", dimensions.CellCount, ChangeStrideBytes);
            IAppendComputeBufferHandle deltas = AllocateAppendTracked(allocator, ownedBuffers, "wildfire.deltas", FireSimGpuProtocol.GetDeltaCapacity(dimensions.CellCount, queuedChanges.Count), DeltaStrideBytes);
            IComputeBufferHandle generations = AllocateTracked(allocator, ownedBuffers, "wildfire.generations", dimensions.CellCount, GenerationStrideBytes);
            IComputeBufferHandle visualFields = AllocateTracked(allocator, ownedBuffers, "wildfire.visual_fields", dimensions.CellCount, VisualFieldStrideBytes);
            IComputeBufferHandle currentTransportFields = AllocateTracked(allocator, ownedBuffers, "wildfire.current_transport_fields", dimensions.CellCount, TransportFieldStrideBytes);
            IComputeBufferHandle nextTransportFields = AllocateTracked(allocator, ownedBuffers, "wildfire.next_transport_fields", dimensions.CellCount, TransportFieldStrideBytes);
            IComputeBufferHandle materialTargetIds = AllocateTracked(allocator, ownedBuffers, "wildfire.material_target_ids", dimensions.CellCount, MaterialTargetIdStrideBytes);
            IComputeBufferHandle materialSlots = AllocateTracked(allocator, ownedBuffers, "wildfire.material_slot_ids", dimensions.CellCount, sizeof(uint));
            IComputeBufferHandle materialFields = AllocateTracked(allocator, ownedBuffers, "wildfire.material_fields", dimensions.CellCount, MaterialFieldStrideBytes);

            uint[] packedCells = initialCells.ToArray().Select(static cell => (uint)cell).ToArray();
            WildfireMaterialField[] materialValues = initialMaterialFields.IsEmpty
                ? Enumerable.Repeat(WildfireMaterialField.Empty, dimensions.CellCount).ToArray()
                : initialMaterialFields.ToArray();
            if (!initialSlotIds.IsEmpty && initialSlotIds.Length != dimensions.CellCount)
                throw new ArgumentException("Initial slot IDs must match the grid.", nameof(initialSlotIds));
            uint[] slotIds = initialSlotIds.IsEmpty
                ? materialValues.Select((field, cell) => field.TargetId == 0 ? 0u : checked((uint)cell + 1)).ToArray()
                : initialSlotIds.ToArray();
            InitialMaterialIdentities = Array.AsReadOnly(materialValues.Select((field, cell) => new FireSimMaterialIdentity(field.TargetId, slotIds[cell])).ToArray());
            materialSlots.Upload(slotIds);
            currentCells.Upload(packedCells);
            nextCells.Upload(packedCells);
            currentTransportFields.Upload(Enumerable.Repeat(0u, dimensions.CellCount).ToArray());
            nextTransportFields.Upload(Enumerable.Repeat(0u, dimensions.CellCount).ToArray());
            materialTargetIds.Upload(materialValues.Select(static field => field.TargetId).ToArray());
            materialFields.Upload(materialValues.Select(static field => field.State.Pack()).ToArray());

            CurrentCells = currentCells;
            NextCells = nextCells;
            QueuedChanges = queuedChanges;
            Deltas = deltas;
            Generations = generations;
            VisualFields = visualFields;
            CurrentTransportFields = currentTransportFields;
            NextTransportFields = nextTransportFields;
            MaterialTargetIds = materialTargetIds;
            MaterialFields = materialFields;
            MaterialSlotIds = materialSlots;
            MaterialHandoff = new MaterialHandoffBuffers(allocator, dimensions.CellCount);
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

    public IComputeBufferHandle QueuedChanges { get; private set; }

    public IAppendComputeBufferHandle Deltas { get; private set; }

    public IComputeBufferHandle Generations { get; }

    public IComputeBufferHandle VisualFields { get; }

    public IComputeBufferHandle CurrentTransportFields { get; private set; }

    public IComputeBufferHandle NextTransportFields { get; private set; }

    public IComputeBufferHandle MaterialTargetIds { get; }

    public IComputeBufferHandle MaterialFields { get; }
    public IComputeBufferHandle MaterialSlotIds { get; }
    public IReadOnlyList<FireSimMaterialIdentity> InitialMaterialIdentities { get; }
    public MaterialHandoffBuffers MaterialHandoff { get; }

    public IComputeBufferHandle CurrentAtmosphericFields => CurrentTransportFields;

    public IComputeBufferHandle NextAtmosphericFields => NextTransportFields;

    public IComputeBufferHandle CompanionTargetIds => MaterialTargetIds;

    public IComputeBufferHandle CompanionFields => MaterialFields;

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
        ReadOnlySpan<WildfireMaterialField> initialMaterialFields,
        IComputeBufferAllocator allocator)
    {
        return new ComputeBufferGrid(
            new ComputeGridDimensions(width, height, depth),
            initialCells,
            initialMaterialFields,
            allocator);
    }

    public void SwapCellBuffers()
    {
        (CurrentCells, NextCells) = (NextCells, CurrentCells);
        (CurrentTransportFields, NextTransportFields) = (NextTransportFields, CurrentTransportFields);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        MaterialHandoff.Dispose();
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
