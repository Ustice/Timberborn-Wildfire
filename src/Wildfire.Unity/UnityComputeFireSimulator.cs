using System.Diagnostics;
using Wildfire.Core;

namespace Wildfire.Unity;

public sealed class UnityComputeFireSimulator : IGpuFireSimulator
{
    public const string ApplyExternalChangesKernelName = "ApplyExternalChanges";
    public const string FullGridKernelName = "SimulateFullGrid";
    public const int ThreadGroupSizeX = 8;
    public const int ThreadGroupSizeY = 8;
    public const int ThreadGroupSizeZ = 4;
    public const string Status = "External change upload, full-grid shader dispatch, compact delta readback, and GPU visual field output baseline ready.";

    private readonly List<FireSimChange> _queuedChanges = [];
    private readonly List<IFireSimListener> _listeners = [];
    private readonly IFireSimComputeDispatcher? _dispatcher;
    private readonly IFireSimDiagnosticSink _diagnostics;
    private readonly FireSimParameters _parameters;
    private readonly uint _seed;
    private uint _tick;

    public UnityComputeFireSimulator(int width, int height, int depth)
        : this(width, height, depth, NullFireSimDiagnosticSink.Instance)
    {
    }

    public UnityComputeFireSimulator(int width, int height, int depth, IFireSimDiagnosticSink diagnostics)
    {
        Dimensions = new ComputeGridDimensions(width, height, depth);
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

    public int PendingChangeCount => _queuedChanges.Count;

    public FireSimWind Wind { get; set; } = FireSimWind.None;

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

        BufferGrid.Deltas.ResetAppendCounter();

        QueuedChangeBatch changeBatch = CreateQueuedChangeBatch(BufferGrid.QueuedChanges.Count);
        uint dispatchTick = _tick + 1;
        _diagnostics.Info(
            $"wildfire_gpu_simulator_queued_changes tick={dispatchTick} queued_changes={_queuedChanges.Count} upload_capacity={BufferGrid.QueuedChanges.Count} valid_changes={changeBatch.ValidChanges.Length} ignored_changes={changeBatch.InvalidIndices.Count}");

        LastIgnoredChangeCount = changeBatch.InvalidIndices.Count;
        LastUploadedChangeCount = changeBatch.ValidChanges.Length;

        if (changeBatch.ValidChanges.Length > 0)
        {
            BufferGrid.QueuedChanges.Upload(FireSimChangeUpload.Encode(changeBatch.ValidChanges, BufferGrid.QueuedChanges.Count));
        }

        if (changeBatch.ValidChanges.Length > 0)
        {
            DispatchWithDiagnostics(CreateApplyExternalChangesDispatch(changeBatch.ValidChanges.Length, dispatchTick));
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
            BufferGrid.VisualFields,
            BufferGrid.CurrentAtmosphericFields,
            BufferGrid.NextAtmosphericFields,
            BufferGrid.CompanionFields,
            _parameters,
            Wind.Normalized(),
            0u,
            GetThreadGroups(Dimensions.Width, ThreadGroupSizeX),
            GetThreadGroups(Dimensions.Height, ThreadGroupSizeY),
            GetThreadGroups(Dimensions.Depth, ThreadGroupSizeZ));

        DispatchWithDiagnostics(dispatch);

        _diagnostics.Info($"wildfire_gpu_simulator_readback_started tick={dispatchTick}");
        Stopwatch readbackStopwatch = Stopwatch.StartNew();
        CellDelta[] deltas = FireSimDeltaReadback.Read(BufferGrid.Deltas);
        readbackStopwatch.Stop();
        BufferGrid.SwapCellBuffers();
        _diagnostics.Info(
            $"wildfire_gpu_simulator_readback_completed tick={dispatchTick} delta_count={deltas.Length} elapsed_ms={readbackStopwatch.Elapsed.TotalMilliseconds:F3}");

        NotifyListeners(deltas);

        return new GpuFireStepResult(deltas, dispatchTick);
    }

    public IDisposable Subscribe(IFireSimListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        _listeners.Add(listener);
        return new ListenerSubscription(_listeners, listener);
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
            BufferGrid.CurrentAtmosphericFields,
            BufferGrid.NextAtmosphericFields,
            BufferGrid.CompanionFields,
            _parameters,
            Wind.Normalized(),
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

    private void NotifyListeners(ReadOnlySpan<CellDelta> deltas)
    {
        IFireSimListener[] listeners = _listeners.ToArray();

        foreach (IFireSimListener listener in listeners)
        {
            listener.OnFireSimDeltas(deltas);
        }

        _diagnostics.Info(
            $"wildfire_gpu_simulator_listeners_notified tick={_tick} listener_count={listeners.Length} delta_count={deltas.Length}");
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

    private sealed record QueuedChangeBatch(
        IReadOnlyList<int> ValidIndices,
        FireSimChange[] ValidChanges,
        IReadOnlyList<int> InvalidIndices);

    private sealed class ListenerSubscription(List<IFireSimListener> listeners, IFireSimListener listener) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            listeners.Remove(listener);
            _disposed = true;
        }
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
