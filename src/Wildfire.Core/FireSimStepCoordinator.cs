namespace Wildfire.Core;

public interface IFireSimStepBackend
{
    void ResetDeltaCounter(uint tick);
    void ApplyExternalChanges(uint tick, FireSimChange[] changes);
    void Simulate(uint tick);
    CellDelta[] ReadDeltas(uint tick);
    void SwapBuffers(uint tick);
}

public sealed class FireSimStepCoordinator
{
    private readonly int _cellCount;
    private readonly int _changeCapacity;
    private readonly FireSimChangeQueue _changes = new();
    private readonly List<IFireSimListener> _listeners = new();

    public FireSimStepCoordinator(int cellCount, int changeCapacity)
    {
        if (cellCount <= 0 || changeCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(changeCapacity), "Grid and change capacities must be positive.");
        }

        _cellCount = cellCount;
        _changeCapacity = changeCapacity;
    }

    public uint CurrentTick { get; private set; }
    public int PendingChangeCount => _changes.Count;
    public int ListenerCount => _listeners.Count;
    public int LastIgnoredChangeCount { get; private set; }
    public int LastUploadedChangeCount { get; private set; }

    public void RegisterChange(FireSimChange change)
    {
        _changes.Add(change);
    }

    public void RestoreTick(uint tick)
    {
        CurrentTick = tick;
    }

    public GpuFireStepResult Tick(IFireSimStepBackend backend)
    {
        if (backend is null)
        {
            throw new ArgumentNullException(nameof(backend));
        }

        FireSimChangeQueue.Batch batch = _changes.PrepareBatch(_cellCount, _changeCapacity);
        LastIgnoredChangeCount = batch.IgnoredCount;
        LastUploadedChangeCount = batch.Changes.Length;
        uint dispatchTick = CurrentTick + 1;
        backend.ResetDeltaCounter(dispatchTick);
        if (batch.Changes.Length > 0)
        {
            backend.ApplyExternalChanges(dispatchTick, batch.Changes);
        }

        // Once external changes reached the GPU, retrying a later failed stage must not replay them.
        _changes.Consume(batch);
        CurrentTick = dispatchTick;
        backend.Simulate(dispatchTick);
        CellDelta[] deltas = backend.ReadDeltas(dispatchTick);
        backend.SwapBuffers(dispatchTick);
        foreach (IFireSimListener listener in _listeners.ToArray())
        {
            listener.OnFireSimDeltas(deltas);
        }

        return new GpuFireStepResult(deltas, dispatchTick);
    }

    public IDisposable Subscribe(IFireSimListener listener)
    {
        if (listener is null)
        {
            throw new ArgumentNullException(nameof(listener));
        }

        _listeners.Add(listener);
        return new Subscription(_listeners, listener);
    }

    private sealed class Subscription : IDisposable
    {
        private readonly List<IFireSimListener> _listeners;
        private readonly IFireSimListener _listener;
        private bool _disposed;

        public Subscription(List<IFireSimListener> listeners, IFireSimListener listener)
        {
            _listeners = listeners;
            _listener = listener;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _listeners.Remove(_listener);
            _disposed = true;
        }
    }
}
