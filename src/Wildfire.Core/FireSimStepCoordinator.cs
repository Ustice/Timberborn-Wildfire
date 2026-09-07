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
    private bool _isTicking;

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
        ValidateBackend(backend);
        FireSimChangeQueue.Batch batch = _changes.PrepareBatch(_cellCount, _changeCapacity);
        return RunStep(backend, batch, batch.Changes, commitInput: null);
    }

    public GpuFireStepResult? TryTickWithInput(IFireSimStepBackend backend, FireSimChange input, Action commitInput)
    {
        ValidateBackend(backend);
        if (commitInput is null)
        {
            throw new ArgumentNullException(nameof(commitInput));
        }

        if (input.CellIndex < 0 || input.CellIndex >= _cellCount)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Step input must address a cell in this grid.");
        }

        FireSimChangeQueue.Batch batch = _changes.PrepareBatch(_cellCount, _changeCapacity);
        if (batch.Changes.Length == _changeCapacity)
        {
            return null;
        }

        FireSimChange[] changes = new FireSimChange[batch.Changes.Length + 1];
        batch.Changes.CopyTo(changes, 0);
        changes[changes.Length - 1] = input;
        return RunStep(backend, batch, changes, commitInput);
    }

    private void ValidateBackend(IFireSimStepBackend backend)
    {
        if (backend is null)
        {
            throw new ArgumentNullException(nameof(backend));
        }

        if (_isTicking)
        {
            throw new InvalidOperationException("A simulator step cannot reenter another step.");
        }
    }

    private GpuFireStepResult RunStep(
        IFireSimStepBackend backend, FireSimChangeQueue.Batch batch, FireSimChange[] changes, Action? commitInput)
    {
        _isTicking = true;
        FireSimStepInputOutcome outcome = FireSimStepInputOutcome.NotApplied;
        try
        {
            LastIgnoredChangeCount = batch.IgnoredCount;
            LastUploadedChangeCount = changes.Length;
            uint dispatchTick = CurrentTick + 1;
            backend.ResetDeltaCounter(dispatchTick);
            if (changes.Length > 0)
            {
                outcome = FireSimStepInputOutcome.Indeterminate;
                backend.ApplyExternalChanges(dispatchTick, changes);
            }

            // The one-step input never enters this queue, even if apply throws.
            // Once external changes reached the GPU, later failures must not replay queued inputs.
            _changes.Consume(batch);
            CurrentTick = dispatchTick;
            backend.Simulate(dispatchTick);
            CellDelta[] deltas = backend.ReadDeltas(dispatchTick);
            backend.SwapBuffers(dispatchTick);
            commitInput?.Invoke();
            outcome = FireSimStepInputOutcome.Committed;
            foreach (IFireSimListener listener in _listeners.ToArray())
            {
                listener.OnFireSimDeltas(deltas);
            }

            return new GpuFireStepResult(deltas, dispatchTick);
        }
        catch (Exception exception) when (commitInput is not null)
        {
            throw new FireSimStepInputException(outcome, exception);
        }
        finally
        {
            _isTicking = false;
        }
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
