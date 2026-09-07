using Wildfire.Timberborn.FireResponse;
using Wildfire.Core;

namespace Wildfire.Timberborn.Resources;

/// <summary>Pending jobs remain in native executors. Only one dose enters an owned synchronous GPU step.</summary>
public sealed class NativeResourceCoordinator
{
    private readonly List<WardenExecutor> _wardens = new();
    private IFireSimStepInputSimulator? _simulator;
    private readonly NativeResourceTransaction _transaction = new();
    public long FieldRevision { get; private set; }
    public bool IsIndeterminate => _transaction.IsIndeterminate;
    public void Register(WardenExecutor executor) => _wardens.Add(executor);
    public void Unregister(WardenExecutor executor) => _wardens.Remove(executor);

    public void Attach(IGpuFireSimulator simulator)
    {
        ThrowIfSaveUnsafe();
        _simulator = simulator as IFireSimStepInputSimulator ??
            throw new InvalidOperationException("Warden response requires simulator support for committed step inputs.");
    }

    public GpuFireStepResult Tick()
    {
        try { return TickCore(); }
        finally { FieldRevision++; }
    }

    private GpuFireStepResult TickCore()
    {
        ThrowIfSaveUnsafe();
        var simulator = _simulator ?? throw new InvalidOperationException("Warden simulator has not been attached.");
        foreach (var warden in _wardens.ToArray())
        {
            if (!warden.TryPrepareApplication(out var input, out var commit)) continue;
            var result = _transaction.TryDeliver(simulator, input, commit);
            // Capacity rejection has not queued a dose, changed the worker, or advanced the simulation.
            return result ?? simulator.Tick();
        }
        return simulator.Tick();
    }

    public void TransferInventory(Action transfer) => _transaction.TransferInventory(transfer);

    public void ThrowIfSaveUnsafe() => _transaction.ThrowIfSaveUnsafe();

    // Called only by actual world load/unload. Disabling/reinitializing fire must never clear poison.
    public void ResetForWorldLoad()
    {
        _simulator = null;
        _transaction.ResetForWorldLoad();
    }
}
