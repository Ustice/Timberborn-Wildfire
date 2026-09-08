using Wildfire.Core;
using Wildfire.Timberborn.Fertilizer;

namespace Wildfire.Timberborn.Resources;

/// <summary>Pending jobs remain in native executors. Only one resource conversion enters an owned synchronous GPU step.</summary>
public sealed class NativeResourceCoordinator : INativeResourceMutationGuard, ITimberbornFireDispatchHost
{
    private readonly List<INativeWaterApplicationProducer> _waterResponders = new();
    private readonly List<FertilizerExecutor> _fertilizers = new();
    internal void Register(FertilizerExecutor executor) => _fertilizers.Add(executor);
    internal void Unregister(FertilizerExecutor executor) => _fertilizers.Remove(executor);
    private IFireSimAshCollectionSimulator? _simulator;
    private readonly List<AshHarvestExecutor> _harvesters = new();
    private readonly NativeResourceStepScheduler _scheduler = new();
    public void Register(AshHarvestExecutor executor) => _harvesters.Add(executor);
    public void Unregister(AshHarvestExecutor executor) => _harvesters.Remove(executor);
    private readonly NativeResourceTransaction _transaction = new();
    public long FieldRevision { get; private set; }
    public bool IsIndeterminate => _transaction.IsIndeterminate;
    public FireSimAshCollectionReceipt? LastAshReceipt { get; private set; }
    internal void Register(INativeWaterApplicationProducer executor) => _waterResponders.Add(executor);
    internal void Unregister(INativeWaterApplicationProducer executor) => _waterResponders.Remove(executor);

    // The native water-credit boundary calls this after its credit guard exits, before parallel water starts.
    // Each collector revalidates actual arrival and owns its own source-to-inventory conversion guard.
    internal void CollectNaturalWater()
    {
        _transaction.ThrowIfOperationUnsafe();
        foreach (var collector in _waterResponders.OfType<INativeNaturalWaterCollector>().ToArray())
            collector.TryCollectPendingWater();
    }

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
        _transaction.ThrowIfOperationUnsafe();
        var simulator = _simulator ?? throw new InvalidOperationException("Resource simulator has not been attached.");
        LastAshReceipt = null;
        return _scheduler.Tick(kind => kind switch
        {
            NativeResourceProducerKind.Water => TryWater(simulator),
            NativeResourceProducerKind.AshCollection => TryAsh(simulator),
            NativeResourceProducerKind.FertilizerApplication => TryFertilizer(simulator),
            _ => throw new InvalidOperationException("Unknown native resource producer."),
        }, simulator.Tick);
    }

    private NativeResourceAttempt TryWater(IFireSimAshCollectionSimulator simulator)
    {
        foreach (var responder in _waterResponders.ToArray())
        {
            if (!responder.TryPrepareApplication(out var input, out var commit)) continue;
            return new(true, _transaction.TryDeliver(simulator, input, () => { commit(); _scheduler.Completed(NativeResourceProducerKind.Water); }));
        }
        return default;
    }
    private NativeResourceAttempt TryAsh(IFireSimAshCollectionSimulator simulator)
    {
        foreach (var harvester in _harvesters.ToArray())
        {
            if (!harvester.TryPrepareCollection(out var input, out var commit)) continue;
            return new(true, _transaction.TryCollectAsh(simulator, input, receipt => { commit(receipt); LastAshReceipt = receipt; _scheduler.Completed(NativeResourceProducerKind.AshCollection); }));
        }
        return default;
    }

    private NativeResourceAttempt TryFertilizer(IFireSimAshCollectionSimulator simulator)
    {
        foreach (var executor in _fertilizers.ToArray())
        {
            if (!executor.TryPrepareApplication(out var input, out var accepted, out var rejected)) continue;
            var application = simulator as IFireSimAshApplicationSimulator ??
                throw new InvalidOperationException("The attached world simulator lacks conditional ash application support.");
            var result = _transaction.TryApplyCleanAsh(application, input,
                receipt => { accepted(receipt); _scheduler.Completed(NativeResourceProducerKind.FertilizerApplication); },
                receipt => { rejected(receipt); _scheduler.Completed(NativeResourceProducerKind.FertilizerApplication); });
            return new(true, result?.Step);
        }
        return default;
    }

    internal FireSimAshApplicationStepResult? TryApplyCleanAsh(FireSimAshApplicationInput input,
        Action<FireSimAshApplicationReceipt> commitApplication)
    {
        _transaction.ThrowIfOperationUnsafe();
        var simulator = _simulator as IFireSimAshApplicationSimulator ??
            throw new InvalidOperationException("The attached world simulator lacks conditional ash application support.");
        return _transaction.TryApplyCleanAsh(simulator, input, commitApplication);
    }

    public void RequireAshApplicationCommit() => _transaction.RequireAshApplicationCommit();

    public T CaptureAtRest<T>(Func<T> capture) => _transaction.CaptureAtRest(capture);

    public void TransferInventory(Action transfer) => _transaction.TransferInventory(transfer);

    public void ThrowIfSaveUnsafe() => _transaction.ThrowIfSaveUnsafe();

    void ITimberbornFireDispatchHost.ThrowIfStepUnsafe() => _transaction.ThrowIfOperationUnsafe();

    internal void ExcludeSavesDuringDispatch(Action dispatch) => _transaction.ExcludeSavesDuringDispatch(dispatch);

    // A known step without complete host consequences uses the same irreversible session poison.
    public void InvalidateIncompleteDispatch() => _transaction.InvalidateAfterLifecycleFailure();

    public void InvalidateAfterLifecycleFailure() => _transaction.InvalidateAfterLifecycleFailure();

    // Called only by actual world load/unload. Disabling/reinitializing fire must never clear poison.
    public void ResetForWorldLoad()
    {
        _transaction.ResetForWorldLoad();
        _simulator = null;
        _scheduler.ResetForWorldLoad();
        LastAshReceipt = null;
    }
}
