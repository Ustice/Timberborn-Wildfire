namespace Wildfire.Core;

public interface IFireSimStepBackend
{
    void ResetDeltaCounter(uint tick);
    void ApplyExternalChanges(uint tick, FireSimChange[] changes);
    void Simulate(uint tick);
    CellDelta[] ReadDeltas(uint tick);
    void SwapBuffers(uint tick);
}

public sealed partial class FireSimStepCoordinator
{
    private readonly int _cellCount;
    private readonly int _changeCapacity;
    private readonly FireSimChangeQueue _changes = new();
    private readonly List<IFireSimListener> _listeners = new();
    private bool _isTicking;
    private bool _isCapturingSnapshot;
    private bool _stateUncertain;
    private bool _hasStarted;
    private readonly FireSimMaterialHandoffSession _material;

    public FireSimStepCoordinator(int cellCount, int changeCapacity, IReadOnlyList<FireSimMaterialIdentity>? initialMaterial = null)
    {
        if (cellCount <= 0 || changeCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(changeCapacity), "Grid and change capacities must be positive.");
        }

        _cellCount = cellCount;
        _changeCapacity = changeCapacity;
        _material = new(cellCount, initialMaterial);
    }

    public uint CurrentTick { get; private set; }
    public int PendingChangeCount => _changes.Count;
    public int ListenerCount => _listeners.Count;
    public int LastIgnoredChangeCount { get; private set; }
    public int LastUploadedChangeCount { get; private set; }

    public void RegisterChange(FireSimChange change)
    {
        ThrowIfSnapshotInProgress();
        RejectUnacknowledgedCollection(change);
        _changes.Add(change);
    }

    public void RestoreTick(uint tick)
    {
        ThrowIfSnapshotInProgress();
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
        RejectUnacknowledgedCollection(input);
        return TryTickWithInputCore(backend, input, commitInput);
    }

    public GpuFireStepResult? TryCollectAsh(IFireSimAshCollectionBackend backend, FireSimAshCollectionInput input,
        Action<FireSimAshCollectionReceipt> commitCollection)
    {
        if (input.CellIndex < 0 || input.CellIndex >= _cellCount)
            throw new ArgumentOutOfRangeException(nameof(input), "Collection must address a cell in this grid.");
        if (commitCollection is null) throw new ArgumentNullException(nameof(commitCollection));
        var change = new FireSimChange(input.CellIndex, CollectCleanAsh: input.Requested);
        FireSimGpuProtocol.EncodeChange(change); // Reject range errors before admission.
        return TryTickWithInputCore(backend, change, () => commitCollection(
            FireSimGpuProtocol.DecodeCollectionReceipt(backend.ReadAppliedChange(LastUploadedChangeCount - 1), input)));
    }

    public bool IsSlotKnown(FireSimMaterialIdentity identity)
    {
        ThrowIfSnapshotInProgress();
        if (_isTicking || _stateUncertain || !_completeMaterialHistory)
            throw new InvalidOperationException("Material authority is unavailable, changing, or indeterminate.");
        return _material.IsSlotKnown(identity);
    }

    public bool TryGetMaterialArchive(FireSimMaterialIdentity identity, out FireSimMaterialArchive archive) =>
        _material.TryGetArchive(identity, out archive);

    public GpuFireStepResult? TryHandoffMaterial(IFireSimMaterialHandoffBackend backend, FireSimMaterialHandoffBatch batch,
        Action<FireSimMaterialHandoffReceipt> commit)
    {
        ValidateBackend(backend);
        if (commit is null) throw new ArgumentNullException(nameof(commit));
        if (!_material.Prepare(batch, backend.MaterialHandoffCapacity)) return null;
        return TryTickWithInputCore(backend, new FireSimChange(0, MaterialHandoff: batch), () =>
        {
            FireSimMaterialHandoffReceipt receipt = FireSimMaterialHandoffProtocol.DecodeReceipt(batch,
                backend.ReadMaterialHandoffHeader(), backend.ReadMaterialHandoffReceipts(batch.Requests.Count));
            _material.Commit(batch, receipt);
            commit(receipt);
        }, () =>
        {
            _material.BeginAttempt(batch.Token);
            backend.UploadMaterialHandoff(batch);
        });
    }

    private static void RejectUnacknowledgedCollection(FireSimChange change)
    {
        if (change.MaterialHandoff is not null)
            throw new ArgumentException("Material handoff requires acknowledged batch admission.", nameof(change));
        if (change.CollectCleanAsh.HasValue)
            throw new ArgumentException("Clean ash collection requires TryCollectAsh and its GPU receipt.", nameof(change));
    }

    private GpuFireStepResult? TryTickWithInputCore(IFireSimStepBackend backend, FireSimChange input, Action commitInput, Action? prepareInput = null)
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
        return RunStep(backend, batch, changes, commitInput, prepareInput);
    }

    private void ValidateBackend(IFireSimStepBackend backend)
    {
        if (backend is null)
        {
            throw new ArgumentNullException(nameof(backend));
        }

        ThrowIfSnapshotInProgress();
        if (_stateUncertain) throw new InvalidOperationException("Simulator state is indeterminate; load a complete snapshot before further simulation.");
        if (_isTicking)
        {
            throw new InvalidOperationException("A simulator step cannot reenter another step.");
        }
    }

    private GpuFireStepResult RunStep(
        IFireSimStepBackend backend, FireSimChangeQueue.Batch batch, FireSimChange[] changes, Action? commitInput, Action? prepareInput = null)
    {
        _isTicking = true;
        _hasStarted = true;
        FireSimStepInputOutcome outcome = FireSimStepInputOutcome.NotApplied;
        try
        {
            prepareInput?.Invoke();
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
            outcome = FireSimStepInputOutcome.Indeterminate;
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
        catch (Exception exception)
        {
            if (outcome == FireSimStepInputOutcome.Indeterminate) _stateUncertain = true;
            if (commitInput is not null) throw new FireSimStepInputException(outcome, exception);
            throw;
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
