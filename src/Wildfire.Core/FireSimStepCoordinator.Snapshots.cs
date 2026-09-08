namespace Wildfire.Core;

public sealed partial class FireSimStepCoordinator
{
    private bool _completeMaterialHistory = true;
    public FireSimSnapshotCapability SnapshotCapability => _completeMaterialHistory
        ? FireSimSnapshotCapability.CompleteMaterialHistory : FireSimSnapshotCapability.LegacyMaterialHistoryUnavailable;
    /// <summary>Restore only into a new unpublished coordinator, never independently over live GPU state.</summary>
    public FireSimStepCoordinator(FireSimSnapshot snapshot, int changeCapacity)
    {
        var validated = FireSimSnapshotValidation.ValidateAndClone(snapshot);
        if (changeCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(changeCapacity));
        _cellCount = validated.Cells.Length;
        _changeCapacity = changeCapacity;
        _material = new(validated);
        CurrentTick = validated.Tick;
        _hasStarted = true;
        foreach (var change in validated.PendingChanges) _changes.Add(change);
    }

    public FireSimSnapshot CaptureSnapshot(IFireSimSnapshotBackend backend)
    {
        if (!_completeMaterialHistory) throw new InvalidOperationException("Complete material history is unavailable after legacy restore.");
        return CaptureSnapshotCore(backend);
    }

    public FireSimLegacySnapshot CaptureLegacySnapshot(IFireSimSnapshotBackend backend)
    {
        var snapshot = CaptureSnapshotCore(backend);
        return new(snapshot.Tick, snapshot.Cells, snapshot.TransportFields);
    }

    private FireSimSnapshot CaptureSnapshotCore(IFireSimSnapshotBackend backend)
    {
        ValidateBackend(backend);
        _isCapturingSnapshot = true;
        try
        {
            var grid = backend.SnapshotGrid;
            var parameters = backend.SnapshotParameters;
            var seed = backend.SnapshotSeed;
            FireSimSnapshotBuffers buffers = backend.ReadSnapshotBuffers();
            if (!_material.MatchesActive(buffers.TargetIds, buffers.SlotIds))
            {
                _stateUncertain = true;
                throw new InvalidOperationException("GPU ownership does not match coordinator authority; a complete snapshot cannot be captured.");
            }
            return FireSimSnapshotValidation.ValidateAndClone(new(FireSimSnapshot.CurrentVersion, grid, CurrentTick,
                parameters, seed, buffers.Cells, buffers.TransportFields, buffers.CompanionFields,
                buffers.TargetIds, buffers.SlotIds, _material.CaptureAuthority(), _changes.CapturePending()));
        }
        finally { _isCapturingSnapshot = false; }
    }

    /// <summary>Legacy buffer-only restore is allowed once, before any simulation/admission; failure cannot be cleared in place.</summary>
    public void InitializeLegacyBuffers(uint tick, Action initializeBuffers)
    {
        if (initializeBuffers is null) throw new ArgumentNullException(nameof(initializeBuffers));
        ThrowIfSnapshotInProgress();
        if (_stateUncertain || _isTicking || _hasStarted || CurrentTick != 0 || PendingChangeCount != 0)
            throw new InvalidOperationException("Legacy restore is initialization-only; construct a new simulator.");
        _hasStarted = true;
        _completeMaterialHistory = false;
        _stateUncertain = true;
        _isCapturingSnapshot = true; // Blocks input/step/capture reentry through backend callbacks.
        try
        {
            initializeBuffers();
            CurrentTick = tick;
            _stateUncertain = false;
        }
        finally { _isCapturingSnapshot = false; }
    }

    /// <summary>Backends also use this guard for mutable simulator parameters during synchronous capture.</summary>
    public void ThrowIfSnapshotInProgress()
    {
        if (_isCapturingSnapshot) throw new InvalidOperationException("Simulator input mutation and stepping cannot reenter snapshot capture.");
    }
}
