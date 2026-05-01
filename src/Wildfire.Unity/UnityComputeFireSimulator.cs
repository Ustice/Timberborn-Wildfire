using Wildfire.Core;

namespace Wildfire.Unity;

public sealed class UnityComputeFireSimulator : IGpuFireSimulator
{
    public const string ApplyExternalChangesKernelName = "ApplyExternalChanges";
    public const string FullGridKernelName = "SimulateFullGrid";
    public const int ThreadGroupSizeX = 8;
    public const int ThreadGroupSizeY = 8;
    public const int ThreadGroupSizeZ = 4;
    public const string Status = "External change upload and full-grid shader dispatch baseline ready; delta readback is not implemented.";

    private readonly List<FireSimChange> _queuedChanges = [];
    private readonly IFireSimComputeDispatcher? _dispatcher;
    private readonly uint _seed;
    private uint _tick;

    public UnityComputeFireSimulator(int width, int height, int depth)
    {
        Dimensions = new ComputeGridDimensions(width, height, depth);
    }

    public UnityComputeFireSimulator(ComputeBufferGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        BufferGrid = grid;
        Dimensions = grid.Dimensions;
    }

    public UnityComputeFireSimulator(ComputeBufferGrid grid, IFireSimComputeDispatcher dispatcher, uint seed = 0)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(dispatcher);

        BufferGrid = grid;
        Dimensions = grid.Dimensions;
        _dispatcher = dispatcher;
        _seed = seed;
    }

    public ComputeGridDimensions Dimensions { get; }

    public ComputeBufferGrid? BufferGrid { get; }

    public int Width => Dimensions.Width;

    public int Height => Dimensions.Height;

    public int Depth => Dimensions.Depth;

    public int PendingChangeCount => _queuedChanges.Count;

    public int LastIgnoredChangeCount { get; private set; }

    public int LastUploadedChangeCount { get; private set; }

    public static string Describe()
    {
        return "Unity GPU simulator dispatches Wildfire rules through FireSim.compute.";
    }

    public void RegisterChange(FireSimChange change)
    {
        _queuedChanges.Add(change);
    }

    public GpuFireStepResult Tick()
    {
        if (BufferGrid is null || _dispatcher is null)
        {
            throw new InvalidOperationException("GPU compute simulation requires a buffer grid and compute dispatcher.");
        }

        QueuedChangeBatch changeBatch = CreateQueuedChangeBatch(BufferGrid.QueuedChanges.Count);

        LastIgnoredChangeCount = changeBatch.InvalidIndices.Count;
        LastUploadedChangeCount = changeBatch.ValidChanges.Length;

        if (changeBatch.ValidChanges.Length > 0)
        {
            BufferGrid.QueuedChanges.Upload(FireSimChangeUpload.Encode(changeBatch.ValidChanges, BufferGrid.QueuedChanges.Count));
        }

        uint dispatchTick = _tick + 1;

        if (changeBatch.ValidChanges.Length > 0)
        {
            _dispatcher.Dispatch(CreateApplyExternalChangesDispatch(changeBatch.ValidChanges.Length, dispatchTick));
        }

        ConsumeQueuedChanges(changeBatch);
        _tick = dispatchTick;

        FireSimComputeDispatch dispatch = new(
            FullGridKernelName,
            Dimensions,
            dispatchTick,
            _seed,
            BufferGrid.CurrentCells,
            BufferGrid.NextCells,
            BufferGrid.QueuedChanges,
            BufferGrid.Deltas,
            0u,
            GetThreadGroups(Dimensions.Width, ThreadGroupSizeX),
            GetThreadGroups(Dimensions.Height, ThreadGroupSizeY),
            GetThreadGroups(Dimensions.Depth, ThreadGroupSizeZ));

        _dispatcher.Dispatch(dispatch);
        BufferGrid.SwapCellBuffers();

        return new GpuFireStepResult(Array.Empty<CellDelta>(), dispatchTick);
    }

    public IDisposable Subscribe(IFireSimListener listener)
    {
        throw new NotImplementedException("GPU delta subscription is not implemented yet.");
    }

    private static int GetThreadGroups(int dimension, int threadGroupSize)
    {
        return (dimension + threadGroupSize - 1) / threadGroupSize;
    }

    private FireSimComputeDispatch CreateApplyExternalChangesDispatch(int changeCount, uint dispatchTick)
    {
        return new FireSimComputeDispatch(
            ApplyExternalChangesKernelName,
            Dimensions,
            dispatchTick,
            _seed,
            BufferGrid!.CurrentCells,
            BufferGrid.NextCells,
            BufferGrid.QueuedChanges,
            BufferGrid.Deltas,
            checked((uint)changeCount),
            1,
            1,
            1);
    }

    private bool IsValidCellIndex(int cellIndex)
    {
        return cellIndex >= 0 && cellIndex < Dimensions.CellCount;
    }

    private QueuedChangeBatch CreateQueuedChangeBatch(int uploadCapacity)
    {
        List<int> validIndices = [];
        List<FireSimChange> validChanges = [];
        List<int> invalidIndices = [];

        for (int index = 0; index < _queuedChanges.Count; index++)
        {
            FireSimChange change = _queuedChanges[index];

            if (!IsValidCellIndex(change.CellIndex))
            {
                invalidIndices.Add(index);
            }
            else if (validChanges.Count < uploadCapacity)
            {
                validIndices.Add(index);
                validChanges.Add(change);
            }
        }

        return new QueuedChangeBatch(validIndices, validChanges.ToArray(), invalidIndices);
    }

    private void ConsumeQueuedChanges(QueuedChangeBatch changeBatch)
    {
        changeBatch.ValidIndices
            .Concat(changeBatch.InvalidIndices)
            .OrderByDescending(static index => index)
            .ToList()
            .ForEach(index => _queuedChanges.RemoveAt(index));
    }

    private sealed record QueuedChangeBatch(
        IReadOnlyList<int> ValidIndices,
        FireSimChange[] ValidChanges,
        IReadOnlyList<int> InvalidIndices);
}
