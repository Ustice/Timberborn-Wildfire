using Wildfire.Timberborn.FireResponse;
using Wildfire.Core;

namespace Wildfire.Timberborn.Resources;

/// <summary>Pending jobs remain in native executors. Only one resource conversion enters an owned synchronous GPU step.</summary>
public sealed class NativeResourceCoordinator
{
    private readonly List<WardenExecutor> _wardens = new();
    private IFireSimAshCollectionSimulator? _simulator;
    private readonly List<AshHarvestExecutor> _harvesters = new();
    private readonly NativeResourceStepScheduler _scheduler = new();
    public void Register(AshHarvestExecutor executor) => _harvesters.Add(executor);
    public void Unregister(AshHarvestExecutor executor) => _harvesters.Remove(executor);
    private readonly NativeResourceTransaction _transaction = new();
    public long FieldRevision { get; private set; }
    public bool IsIndeterminate => _transaction.IsIndeterminate;
    public void Register(WardenExecutor executor) => _wardens.Add(executor);
    public void Unregister(WardenExecutor executor) => _wardens.Remove(executor);

    public void Attach(IGpuFireSimulator simulator)
    {
        ThrowIfSaveUnsafe();
        _simulator = simulator as IFireSimAshCollectionSimulator ??
            throw new InvalidOperationException("Native resource conversion requires simulator support for committed water inputs and ash receipts.");
    }

    public GpuFireStepResult Tick()
    {
        try { return TickCore(); }
        finally { FieldRevision++; }
    }

    private GpuFireStepResult TickCore()
    {
        ThrowIfSaveUnsafe();
        var simulator = _simulator ?? throw new InvalidOperationException("Resource simulator has not been attached.");
        return _scheduler.Tick(ash => ash ? TryAsh(simulator) : TryWater(simulator), simulator.Tick);
    }

    private NativeResourceAttempt TryWater(IFireSimAshCollectionSimulator simulator)
    {
        foreach (var warden in _wardens.ToArray())
        {
            if (!warden.TryPrepareApplication(out var input, out var commit)) continue;
            return new(true, _transaction.TryDeliver(simulator, input, () => { commit(); _scheduler.Committed(ash: false); }));
        }
        return default;
    }
    private NativeResourceAttempt TryAsh(IFireSimAshCollectionSimulator simulator)
    {
        foreach (var harvester in _harvesters.ToArray())
        {
            if (!harvester.TryPrepareCollection(out var input, out var commit)) continue;
            return new(true, _transaction.TryCollectAsh(simulator, input, receipt => { commit(receipt); _scheduler.Committed(ash: true); }));
        }
        return default;
    }

    public void TransferInventory(Action transfer) => _transaction.TransferInventory(transfer);

    public void ThrowIfSaveUnsafe() => _transaction.ThrowIfSaveUnsafe();

    // Called only by actual world load/unload. Disabling/reinitializing fire must never clear poison.
    public void ResetForWorldLoad()
    {
        _transaction.ResetForWorldLoad();
        _simulator = null;
        _scheduler.ResetForWorldLoad();
    }
}
