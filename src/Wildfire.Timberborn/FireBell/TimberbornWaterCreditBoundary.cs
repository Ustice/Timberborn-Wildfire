using Timberborn.SingletonSystem;
using Timberborn.TickSystem;
using Timberborn.WaterBuildings;
using Timberborn.WorldPersistence;
using UnityEngine;
using Wildfire.Timberborn.Compatibility;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.FireBell;

// Deliberately no configurator: native creation and complete load must supply this exact boundary.
internal sealed class TimberbornWaterCreditBoundary : IPostLoadableSingleton
{
    private readonly ITickableSingleton _tick;
    private readonly WaterInputService original;
    private readonly ITickableSingletonService scheduler;
    private readonly ISingletonRepository repository;
    private readonly NativeResourceCoordinator resources;
    internal TimberbornWaterCreditBoundary(WaterInputService original, ITickableSingletonService scheduler,
        ISingletonRepository repository, NativeResourceCoordinator resources)
    { this.original = original; this.scheduler = scheduler; this.repository = repository; this.resources = resources; _tick = new CreditTick(this); }
    private readonly HashSet<TimberbornNaturalWaterSource> _sources = new();
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private object? _published, _loadList;
    private bool _restoreClosed, _attempted, _unavailable;
    internal TimberbornWaterCreditContract? Contract { get; private set; }
    internal bool Installed => !_unavailable && Contract is not null && Contract.IsInstalled(scheduler, _published);
    internal void Track(TimberbornNaturalWaterSource source)
    {
        _sources.Add(source);
        // Registration precedes any ordinary native credit. Creation inside another protected
        // operation cannot install a scheduler here and never earns new-source trust later.
        try { resources.ThrowIfSaveUnsafe(); }
        catch (InvalidOperationException) { source.Refuse(); return; }
        if (!Install()) source.Refuse();
    }
    internal void Forget(TimberbornNaturalWaterSource source) => _sources.Remove(source);
    internal void CheckSave()
    {
        resources.ThrowIfSaveUnsafe();
        if (Environment.CurrentManagedThreadId != _thread)
            throw new InvalidOperationException("Native water ownership saving requires the owning native thread.");
    }
    internal bool CanRead => Environment.CurrentManagedThreadId == _thread && !resources.IsIndeterminate;
    private bool ThreadReady => Environment.CurrentManagedThreadId == _thread && !scheduler.IsStartingParallelTick && scheduler.ParalleTicklIsFinished;
    private void RefuseAll() { foreach (var source in _sources) source.Refuse(); }
    private bool EnsureContract()
    {
        if (_unavailable) return false;
        if (Contract is not null) return true;
        if (!TimberbornWaterCreditContract.Supported()) { _unavailable = true; RefuseAll(); return false; }
        try { Contract = new(); }
        catch (NotSupportedException) { _unavailable = true; RefuseAll(); return false; }
        return true;
    }
    internal bool CanRestore(TimberbornNaturalWaterSource source, IEntityLoader loader)
    {
        if (_restoreClosed || !EnsureContract()) return false;
        var list = Contract!.NativeLoadList(repository);
        if (!Contract.Loading(list, source.Entity, loader)) return false;
        if (_loadList is not null && !ReferenceEquals(_loadList, list)) return false;
        _loadList = list;
        return true;
    }
    public void PostLoad()
    {
        if (_restoreClosed) return;
        try
        {
            if (_sources.Count == 0) return;
            bool inLoad = EnsureContract() && _loadList is not null && ReferenceEquals(_loadList, Contract!.NativeLoadList(repository));
            Install();
            foreach (var source in _sources) source.CompleteRestore(inLoad);
        }
        finally { _restoreClosed = true; _loadList = null; }
    }
    internal bool Install()
    {
        resources.ThrowIfSaveUnsafe();
        if (_sources.Count == 0 || !EnsureContract()) return false;
        if (Installed) return true;
        // An armed source cannot recover trust by re-wrapping a scheduler after an unobserved interval.
        if (_attempted) { _unavailable = true; RefuseAll(); return false; }
        _attempted = true;
        try { _published = Contract!.Install(scheduler, original, _tick); }
        catch { resources.InvalidateAfterLifecycleFailure(); throw; }
        if (_published is not null) return true;
        _unavailable = true; RefuseAll(); return false;
    }
    internal bool Exclusive(TimberbornNaturalWaterSource source, Vector3Int coordinate)
    {
        var inputs = Contract!.Inputs(original);
        if (!Contract.KnownService(original) || inputs.Any(input => !Contract.Known(input))) return false;
        return inputs.Count(input => ReferenceEquals(input, source.Input)) == 1 && inputs.Count(input => input.Coordinates == coordinate) == 1;
    }
    internal void Tick()
    {
        // Removing the last component cannot turn a previously installed failed boundary into a retry.
        if (_published is not null && resources.IsIndeterminate) resources.ThrowIfSaveUnsafe();
        if (_sources.Count == 0) { original.Tick(); return; }
        resources.ThrowIfSaveUnsafe();
        if (_sources.All(source => source.Tainted)) { original.Tick(); return; }
        // Expected topology refusals do not put the ordinary native water tick inside a failing guard.
        if (!ThreadReady || !Installed) { RefuseAll(); original.Tick(); return; }
        var inputs = Contract!.Inputs(original);
        if (!Contract.KnownService(original) || inputs.Any(input => !Contract.Known(input))) { RefuseAll(); original.Tick(); return; }
        resources.TransferInventory(() =>
        {
            foreach (var source in _sources)
            {
                if (!source.Armed) { source.Refuse(); continue; } // First unobserved credit forbids later zero-buffer adoption.
                if (!source.Active || !source.MatchesIdentity(Contract) || inputs.Count(input => ReferenceEquals(input, source.Input)) != 1 ||
                    inputs.Count(input => input.Coordinates == source.Coordinate) != 1) source.Refuse();
            }
            original.Tick(); // Same native service, exactly once; exceptions retain partial native state and poison.
        });
    }
    // Never provisioned/injected: SingletonListener must not discover a second scheduled invocation.
    private sealed class CreditTick : ITickableSingleton
    {
        private readonly TimberbornWaterCreditBoundary _owner;
        internal CreditTick(TimberbornWaterCreditBoundary owner) => _owner = owner;
        public void Tick() => _owner.Tick();
    }

}
