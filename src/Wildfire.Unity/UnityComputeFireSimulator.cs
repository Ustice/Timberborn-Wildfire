using System.Diagnostics;
using Wildfire.Core;

namespace Wildfire.Unity;

public sealed class UnityComputeFireSimulator : IFireSimStepInputSimulator, IFireSimStepBackend
{
    public const string ApplyExternalChangesKernelName = "ApplyExternalChanges";
    public const string FullGridKernelName = "SimulateFullGrid";
    public const int ThreadGroupSizeX = 8;
    public const int ThreadGroupSizeY = 8;
    public const int ThreadGroupSizeZ = 4;
    public const string Status = "External change upload, full-grid shader dispatch, compact delta readback, and GPU visual field output baseline ready.";

    private readonly FireSimStepCoordinator _step;
    private readonly IFireSimComputeDispatcher? _dispatcher;
    private readonly IFireSimDiagnosticSink _diagnostics;
    private readonly FireSimParameters _parameters;
    private readonly uint _seed;

    public UnityComputeFireSimulator(int width, int height, int depth)
        : this(width, height, depth, NullFireSimDiagnosticSink.Instance)
    {
    }

    public UnityComputeFireSimulator(int width, int height, int depth, IFireSimDiagnosticSink diagnostics)
    {
        Dimensions = new ComputeGridDimensions(width, height, depth);
        _step = new FireSimStepCoordinator(Dimensions.CellCount, Dimensions.CellCount);
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        LogInitialized();
    }

    public UnityComputeFireSimulator(ComputeBufferGrid grid)
        : this(grid, NullFireSimDiagnosticSink.Instance)
    {
    }

    public UnityComputeFireSimulator(ComputeBufferGrid grid, IFireSimDiagnosticSink diagnostics)
    {
        ArgumentNullException.ThrowIfNull(grid);
        BufferGrid = grid;
        Dimensions = grid.Dimensions;
        _step = new FireSimStepCoordinator(Dimensions.CellCount, grid.QueuedChanges.Count);
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        LogInitialized();
    }

    public UnityComputeFireSimulator(ComputeBufferGrid grid, IFireSimComputeDispatcher dispatcher, uint seed = 0)
        : this(grid, dispatcher, NullFireSimDiagnosticSink.Instance, seed)
    {
    }

    public UnityComputeFireSimulator(
        ComputeBufferGrid grid,
        IFireSimComputeDispatcher dispatcher,
        IFireSimDiagnosticSink diagnostics,
        uint seed = 0,
        FireSimParameters? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(dispatcher);

        BufferGrid = grid;
        Dimensions = grid.Dimensions;
        _step = new FireSimStepCoordinator(Dimensions.CellCount, grid.QueuedChanges.Count);
        _dispatcher = dispatcher;
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _parameters = parameters ?? FireSimParameters.Default;
        _seed = seed;
        LogInitialized();
    }

    public ComputeGridDimensions Dimensions { get; }

    public ComputeBufferGrid? BufferGrid { get; }

    public int Width => Dimensions.Width;

    public int Height => Dimensions.Height;

    public int Depth => Dimensions.Depth;

    public int PendingChangeCount => _step.PendingChangeCount;

    public FireSimWind Wind { get; set; } = FireSimWind.None;

    public int LastIgnoredChangeCount => _step.LastIgnoredChangeCount;

    public int LastUploadedChangeCount => _step.LastUploadedChangeCount;

    public static string Describe()
    {
        return "Unity GPU simulator dispatches Wildfire rules through FireSim.compute.";
    }

    public void RegisterChange(FireSimChange change)
    {
        _step.RegisterChange(change);
    }

    public GpuFireStepResult Tick()
    {
        if (BufferGrid is null || _dispatcher is null)
        {
            throw new InvalidOperationException("GPU compute simulation requires a buffer grid and compute dispatcher.");
        }

        GpuFireStepResult result = _step.Tick(this);
        _diagnostics.Info(
            $"wildfire_gpu_simulator_listeners_notified tick={result.Tick} listener_count={_step.ListenerCount} delta_count={result.Deltas.Count}");
        return result;
    }

    public GpuFireStepResult? TryTickWithInput(FireSimChange input, Action commitInput)
    {
        if (BufferGrid is null || _dispatcher is null)
        {
            throw new InvalidOperationException("GPU compute simulation requires a buffer grid and compute dispatcher.");
        }

        return _step.TryTickWithInput(this, input, commitInput);
    }

    void IFireSimStepBackend.ResetDeltaCounter(uint dispatchTick)
    {
        BufferGrid!.Deltas.ResetAppendCounter();
        _diagnostics.Info(
            $"wildfire_gpu_simulator_queued_changes tick={dispatchTick} queued_changes={_step.PendingChangeCount} upload_capacity={BufferGrid.QueuedChanges.Count} valid_changes={LastUploadedChangeCount} ignored_changes={LastIgnoredChangeCount}");
    }

    void IFireSimStepBackend.ApplyExternalChanges(uint dispatchTick, FireSimChange[] changes)
    {
        BufferGrid!.QueuedChanges.Upload(FireSimChangeUpload.Encode(changes, BufferGrid.QueuedChanges.Count));
        DispatchWithDiagnostics(CreateApplyExternalChangesDispatch(changes.Length, dispatchTick));
    }

    void IFireSimStepBackend.Simulate(uint dispatchTick)
    {
        FireSimComputeDispatch dispatch = new(
            FullGridKernelName,
            Dimensions,
            dispatchTick,
            _seed,
            BufferGrid!.CurrentCells,
            BufferGrid.NextCells,
            BufferGrid.QueuedChanges,
            BufferGrid.Deltas,
            BufferGrid.VisualFields,
            BufferGrid.CurrentTransportFields,
            BufferGrid.NextTransportFields,
            BufferGrid.MaterialFields,
            _parameters,
            Wind.Normalized(),
            0u,
            GetThreadGroups(Dimensions.Width, ThreadGroupSizeX),
            GetThreadGroups(Dimensions.Height, ThreadGroupSizeY),
            GetThreadGroups(Dimensions.Depth, ThreadGroupSizeZ));

        DispatchWithDiagnostics(dispatch);
    }

    CellDelta[] IFireSimStepBackend.ReadDeltas(uint dispatchTick)
    {
        _diagnostics.Info($"wildfire_gpu_simulator_readback_started tick={dispatchTick}");
        Stopwatch readbackStopwatch = Stopwatch.StartNew();
        CellDelta[] deltas = FireSimDeltaReadback.Read(BufferGrid!.Deltas);
        readbackStopwatch.Stop();
        _diagnostics.Info(
            $"wildfire_gpu_simulator_readback_completed tick={dispatchTick} delta_count={deltas.Length} elapsed_ms={readbackStopwatch.Elapsed.TotalMilliseconds:F3}");

        return deltas;
    }

    void IFireSimStepBackend.SwapBuffers(uint tick)
    {
        BufferGrid!.SwapCellBuffers();
    }

    public IDisposable Subscribe(IFireSimListener listener)
    {
        return _step.Subscribe(listener);
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
            BufferGrid.VisualFields,
            BufferGrid.CurrentTransportFields,
            BufferGrid.NextTransportFields,
            BufferGrid.MaterialFields,
            _parameters,
            Wind.Normalized(),
            checked((uint)changeCount),
            1,
            1,
            1);
    }

    private void DispatchWithDiagnostics(FireSimComputeDispatch dispatch)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        _diagnostics.Info(
            $"wildfire_gpu_simulator_dispatch_started kernel={dispatch.KernelName} tick={dispatch.Tick} groups={dispatch.ThreadGroupsX}x{dispatch.ThreadGroupsY}x{dispatch.ThreadGroupsZ} change_count={dispatch.ChangeCount}");
        _dispatcher!.Dispatch(dispatch);
        stopwatch.Stop();
        _diagnostics.Info(
            $"wildfire_gpu_simulator_dispatch_completed kernel={dispatch.KernelName} tick={dispatch.Tick} elapsed_ms={stopwatch.Elapsed.TotalMilliseconds:F3}");
    }

    private void LogInitialized()
    {
        _diagnostics.Info(
            $"wildfire_gpu_simulator_initialized width={Dimensions.Width} height={Dimensions.Height} depth={Dimensions.Depth} cell_count={Dimensions.CellCount}");
    }

}

public interface IFireSimDiagnosticSink
{
    void Info(string message);
}

public sealed class NullFireSimDiagnosticSink : IFireSimDiagnosticSink
{
    public static readonly NullFireSimDiagnosticSink Instance = new();

    private NullFireSimDiagnosticSink()
    {
    }

    public void Info(string message)
    {
    }
}
