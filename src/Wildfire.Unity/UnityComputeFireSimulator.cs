using Wildfire.Core;

namespace Wildfire.Unity;

public sealed class UnityComputeFireSimulator : IGpuFireSimulator
{
    public const string FullGridKernelName = "SimulateFullGrid";
    public const int ThreadGroupSizeX = 8;
    public const int ThreadGroupSizeY = 8;
    public const int ThreadGroupSizeZ = 4;
    public const string Status = "Full-grid shader dispatch baseline ready; change upload and delta readback are not implemented.";

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

    public static string Describe()
    {
        return "Unity GPU simulator dispatches Wildfire rules through FireSim.compute.";
    }

    public void RegisterChange(FireSimChange change)
    {
        throw new NotImplementedException("GPU change upload is not implemented yet.");
    }

    public GpuFireStepResult Tick()
    {
        if (BufferGrid is null || _dispatcher is null)
        {
            throw new InvalidOperationException("GPU compute simulation requires a buffer grid and compute dispatcher.");
        }

        _tick++;
        FireSimComputeDispatch dispatch = new(
            FullGridKernelName,
            Dimensions,
            _tick,
            _seed,
            BufferGrid.CurrentCells,
            BufferGrid.NextCells,
            BufferGrid.Deltas,
            GetThreadGroups(Dimensions.Width, ThreadGroupSizeX),
            GetThreadGroups(Dimensions.Height, ThreadGroupSizeY),
            GetThreadGroups(Dimensions.Depth, ThreadGroupSizeZ));

        _dispatcher.Dispatch(dispatch);
        BufferGrid.SwapCellBuffers();

        return new GpuFireStepResult(Array.Empty<CellDelta>(), _tick);
    }

    public IDisposable Subscribe(IFireSimListener listener)
    {
        throw new NotImplementedException("GPU delta subscription is not implemented yet.");
    }

    private static int GetThreadGroups(int dimension, int threadGroupSize)
    {
        return (dimension + threadGroupSize - 1) / threadGroupSize;
    }
}
