using Wildfire.Core;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Persistence;

/// <summary>Unpublished complete owned session. The caller publishes this bundle once after native loading settles.</summary>
public sealed partial class TimberbornOwnedWorldSession<TSimulator> : IDisposable
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
    {
        Simulator = simulator;
        Registry = registry;
        Damage = damage;
        Consumer = consumer;
        _guard = guard;
    }

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
        Func<FireGrid, IReadOnlyList<Guid>, IReadOnlyList<TimberbornInitialMaterialBody>> captureRetainedBodies,
        TimberbornOwnedNativeEffects effects, INativeResourceMutationGuard guard, TimberbornResourceFuelCatalog? catalog = null)
    {
        return guard.CaptureAtRest(() => PrepareRestoreDuringCapture(snapshot, solidTerrain, createSimulator, captureRetainedBodies, effects, guard, catalog));
    }

    private static TimberbornOwnedWorldSession<TSimulator> PrepareRestoreDuringCapture(TimberbornWildfirePersistenceSnapshot snapshot,
        IEnumerable<int> solidTerrain, Func<FireSimSnapshot, TSimulator> createSimulator,
        Func<FireGrid, IReadOnlyList<Guid>, IReadOnlyList<TimberbornInitialMaterialBody>> captureRetainedBodies,
        TimberbornOwnedNativeEffects effects, INativeResourceMutationGuard guard, TimberbornResourceFuelCatalog? catalog)
    {
        if (snapshot.PersistenceVersion != 2 || snapshot.FireSim is not null)
            throw new ArgumentException("Complete restore requires WF2 with no legacy FIRE payload.");
        var material = snapshot.OwnedMaterial ?? throw new ArgumentException("Restore requires complete owned material state.");
        var history = material.History ?? throw new NotSupportedException("OWNED1 has no authoritative consequence history.");
        if (history.NativeDefinitions is null)
            throw new NotSupportedException("OWNED2 has no saved native static-definition evidence.");
        var simulation = material.CaptureSimulation();
        history.ValidateAssociation(simulation, material.Bindings, snapshot.Consequences);
        var registry = new TimberbornNativeMaterialRegistry(simulation.Grid, solidTerrain);
        registry.RestoreBindings(material.Bindings);
        // Native provider captures exact required Guids under this same scope; initial eligibility is irrelevant.
        var ids = history.Owners.Where(owner => owner.Retention == OwnedBodyRetention.RetainedBody).Select(owner => owner.EntityId).ToArray();
        var facts = (captureRetainedBodies(simulation.Grid, Array.AsReadOnly(ids)) ??
            throw new InvalidOperationException("No retained native body facts returned.")).ToArray();
        var damage = TimberbornBurnDamageService.CreateFromSavedOwnedDefinitions(simulation.Grid, history, snapshot.Consequences, facts);
        TSimulator? simulator = null;
        try
        {
            simulator = createSimulator(simulation) ?? throw new InvalidOperationException("No new simulator returned.");
            if (simulator.Width != simulation.Grid.Width || simulator.Height != simulation.Grid.Height || simulator.Depth != simulation.Grid.Depth)
                throw new ArgumentException("Restored simulator dimensions do not match the paired native world.");
            var consumer = TimberbornOwnedDeltaConsumer.CreateFromHistory(registry, damage, effects, guard, history, catalog);
            if (ids.Any(id => effects.Bodies.ObservePresence(id) != TimberbornOwnedBodyPresence.Live))
                throw new ArgumentException("A required native owner disappeared during restore staging.");
            // Backend construction and native observers may invoke callbacks. Presence alone cannot
            // certify that the definitions, placement and quantities used for staging are still current.
            var finalFacts = (captureRetainedBodies(simulation.Grid, Array.AsReadOnly(ids)) ??
                throw new InvalidOperationException("No final retained native body facts returned.")).ToArray();
            if (facts.Length != finalFacts.Length || !facts.OrderBy(body => body.EntityId)
                    .Zip(finalFacts.OrderBy(body => body.EntityId), (before, after) => before.SameReadings(after)).All(same => same))
                throw new ArgumentException("Native body facts changed during restore staging.");
            return new(simulator, registry, damage, consumer, guard);
        }
        catch
        {
            simulator?.Dispose();
            throw;
        }
    }
    private void RequireUsable()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TimberbornOwnedWorldSession<TSimulator>));
        _guard.ThrowIfSaveUnsafe();
    }
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Simulator.Dispose();
    }
}
