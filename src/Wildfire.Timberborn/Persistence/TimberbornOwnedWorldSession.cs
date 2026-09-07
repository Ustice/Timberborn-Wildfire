using Wildfire.Core;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Persistence;

/// <summary>Unpublished complete owned session. The caller publishes this bundle once after native loading settles.</summary>
public sealed class TimberbornOwnedWorldSession<TSimulator> : IDisposable
    where TSimulator : class, IGpuFireSimulator, IFireSimSnapshotSimulator, IDisposable
{
    private readonly INativeResourceMutationGuard _guard;
    private bool _disposed;
    public TSimulator Simulator { get; }
    public TimberbornNativeMaterialRegistry Registry { get; }
    public TimberbornBurnDamageService Damage { get; }
    public TimberbornOwnedDeltaConsumer Consumer { get; }
    private TimberbornOwnedWorldSession(TSimulator simulator, TimberbornNativeMaterialRegistry registry,
        TimberbornBurnDamageService damage, TimberbornOwnedDeltaConsumer consumer, INativeResourceMutationGuard guard)
    { Simulator = simulator; Registry = registry; Damage = damage; Consumer = consumer; _guard = guard; }

    public TimberbornWildfirePersistenceSnapshot Capture(TimberbornAshFieldSnapshot ash, TimberbornBeaverFieldBehaviorSnapshot beavers)
    {
        RequireUsable();
        return _guard.CaptureAtRest(() =>
        {
            var simulation = Simulator.CaptureSnapshot();
            var history = Consumer.CopyHistoryDuringCapture();
            var result = new TimberbornWildfirePersistenceSnapshot(2, null, ash, beavers,
                TimberbornWildfirePersistenceCodec.CaptureConsequences(Damage), new(simulation, Registry.CaptureBindings(), history));
            history.ValidateAssociation(simulation, result.OwnedMaterial!.Bindings, result.Consequences);
            return result;
        });
    }

    public static TimberbornOwnedWorldSession<TSimulator> PrepareRestore(TimberbornWildfirePersistenceSnapshot snapshot,
        IEnumerable<int> solidTerrain, Func<FireSimSnapshot, TSimulator> createSimulator,
        Func<TimberbornNativeMaterialRegistry, TimberbornBurnDamageService> createBodyDefinitions,
        TimberbornOwnedNativeEffects effects, INativeResourceMutationGuard guard, TimberbornResourceFuelCatalog? catalog = null)
    {
        guard.ThrowIfSaveUnsafe();
        if (snapshot.PersistenceVersion != 2 || snapshot.FireSim is not null)
            throw new ArgumentException("Complete restore requires WF2 with no legacy FIRE payload.");
        var material = snapshot.OwnedMaterial ?? throw new ArgumentException("Restore requires complete owned material state.");
        var history = material.History ?? throw new NotSupportedException("OWNED1 has no authoritative consequence history.");
        var simulation = material.CaptureSimulation();
        history.ValidateAssociation(simulation, material.Bindings, snapshot.Consequences);
        var registry = new TimberbornNativeMaterialRegistry(simulation.Grid, solidTerrain);
        registry.RestoreBindings(material.Bindings);
        // Build definitions from settled native world, then isolate all mutable damage before restoration.
        var definitions = createBodyDefinitions(registry) ?? throw new InvalidOperationException("No body definitions returned.");
        var damage = definitions.CreateOwnedRestoredCopy(history, snapshot.Consequences);
        TSimulator? simulator = null;
        try
        {
            simulator = createSimulator(simulation) ?? throw new InvalidOperationException("No new simulator returned.");
            if (simulator.Width != simulation.Grid.Width || simulator.Height != simulation.Grid.Height || simulator.Depth != simulation.Grid.Depth)
                throw new ArgumentException("Restored simulator dimensions do not match the paired native world.");
            var consumer = TimberbornOwnedDeltaConsumer.CreateFromHistory(registry, damage, effects, guard, history, catalog);
            guard.ThrowIfSaveUnsafe();
            return new(simulator, registry, damage, consumer, guard);
        }
        catch { simulator?.Dispose(); throw; }
    }
    private void RequireUsable()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TimberbornOwnedWorldSession<TSimulator>));
        _guard.ThrowIfSaveUnsafe();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Simulator.Dispose();
    }
}
