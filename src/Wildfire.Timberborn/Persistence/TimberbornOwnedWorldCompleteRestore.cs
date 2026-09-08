using Wildfire.Core;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Persistence;

public sealed partial class TimberbornOwnedWorldSession<TSimulator>
    where TSimulator : class, IGpuFireSimulator, IFireSimSnapshotSimulator, IDisposable
{
    /// <summary>
    /// Stage supported current desired material beneath exact saved simulator/accounting authority.
    /// This executes no handoff and does not authorize ordinary Tick before material reconciliation.
    /// </summary>
    internal static TimberbornOwnedWorldSession<TSimulator> PrepareCompleteRestore(TimberbornWildfirePersistenceSnapshot snapshot,
        Func<FireSimSnapshot, TSimulator> createSimulator,
        Func<FireGrid, IReadOnlyList<Guid>, TimberbornOwnedRestoreObservation> captureDuringScope,
        TimberbornOwnedNativeEffects effects, INativeResourceMutationGuard guard, TimberbornResourceFuelCatalog? catalog = null)
    {
        if (createSimulator is null) throw new ArgumentNullException(nameof(createSimulator));
        if (captureDuringScope is null) throw new ArgumentNullException(nameof(captureDuringScope));
        if (guard is null) throw new ArgumentNullException(nameof(guard));
        return guard.CaptureAtRest(() =>
        {
            var material = RequireSavedRestoreAuthority(snapshot);
            var history = material.History!;
            if (!history.NativeDefinitions!.HasInventoryDeclarations)
                throw new NotSupportedException("Complete restore requires OWNED4 original inventory declaration evidence.");
            var simulation = material.CaptureSimulation();
            var ids = Array.AsReadOnly(history.Owners.Where(owner => owner.Retention == OwnedBodyRetention.RetainedBody)
                .Select(owner => owner.EntityId).OrderBy(id => id).ToArray());
            var observation = captureDuringScope(simulation.Grid, ids)
                ?? throw new InvalidOperationException("No complete native restore observation returned.");
            if (observation.CurrentWorld.Grid != simulation.Grid)
                throw new ArgumentException("Native observation belongs to a different restored grid.");
            observation.RequireSupported(ids);
            var damage = TimberbornBurnDamageService.CreateFromCompleteSavedOwnedDefinitions(simulation.Grid, history,
                snapshot.Consequences, observation.RetainedBodies, observation.InventoryDeclarations);
            // Only current baseline material and definition parts are reconstructed. Saved ambient
            // fields, body accounting and GPU active/archive history are not initial inputs again.
            var environment = TimberbornInitialEnvironmentProjection.Project(observation.CurrentWorld.Environment);
            var registry = new TimberbornNativeMaterialRegistry(environment.MaterialBaseline);
            registry.RestoreBindings(material.Bindings);
            var projections = observation.RetainedBodies.Select(TimberbornMaterialProjectionCompiler.Compile).ToArray();
            registry.Reconcile(projections, Array.Empty<Guid>());
            RequireUnchangedBindings(material.Bindings, registry.CaptureBindings());
            TSimulator? simulator = null;
            try
            {
                simulator = CreateExactRestoredSimulator(simulation, createSimulator);
                var consumer = TimberbornOwnedDeltaConsumer.CreateFromHistory(registry, damage, effects, guard, history, catalog);
                consumer.CopyHistoryDuringCapture().ValidateAssociation(simulator.CaptureSnapshot(), registry.CaptureBindings(),
                    TimberbornWildfirePersistenceCodec.CaptureConsequences(damage));
                var final = captureDuringScope(simulation.Grid, ids)
                    ?? throw new InvalidOperationException("No final complete native restore observation returned.");
                if (!observation.SameReadings(final))
                    throw new ArgumentException("Native world changed during complete restore staging.");
                final.RequireSupported(ids);
                TimberbornOwnedSimulationValidation.RequireUnchanged(simulation,
                    FireSimSnapshotValidation.ValidateAndClone(simulator.CaptureSnapshot()));
                return new TimberbornOwnedWorldSession<TSimulator>(simulator, registry, damage, consumer, guard, TimberbornDesiredWorldCapability.CompleteStaged);
            }
            catch
            {
                simulator?.Dispose();
                throw;
            }
        });
    }

    private static TimberbornOwnedMaterialSnapshot RequireSavedRestoreAuthority(TimberbornWildfirePersistenceSnapshot snapshot)
    {
        if (snapshot.PersistenceVersion != 2 || snapshot.FireSim is not null)
            throw new ArgumentException("Complete restore requires WF2 with no legacy FIRE payload.");
        var material = snapshot.OwnedMaterial ?? throw new ArgumentException("Restore requires complete owned material state.");
        var history = material.History ?? throw new NotSupportedException("OWNED1 has no authoritative consequence history.");
        if (history.NativeDefinitions is null)
            throw new NotSupportedException("OWNED2 has no saved native static-definition evidence.");
        history.ValidateAssociation(material.CaptureSimulation(), material.Bindings, snapshot.Consequences);
        return material;
    }

    private static TSimulator CreateExactRestoredSimulator(FireSimSnapshot simulation, Func<FireSimSnapshot, TSimulator> createSimulator)
    {
        var simulator = createSimulator(FireSimSnapshotValidation.ValidateAndClone(simulation))
            ?? throw new InvalidOperationException("No new simulator returned.");
        try
        {
            if (simulator.Width != simulation.Grid.Width || simulator.Height != simulation.Grid.Height || simulator.Depth != simulation.Grid.Depth ||
                simulator.SnapshotCapability != FireSimSnapshotCapability.CompleteMaterialHistory)
                throw new ArgumentException("Restored simulator must retain complete material history on the paired native grid.");
            var actual = FireSimSnapshotValidation.ValidateAndClone(simulator.CaptureSnapshot());
            TimberbornOwnedSimulationValidation.RequireUnchanged(simulation, actual);
            return simulator;
        }
        catch
        {
            simulator.Dispose();
            throw;
        }
    }

    private static void RequireUnchangedBindings(TimberbornMaterialBindingSnapshot before, TimberbornMaterialBindingSnapshot after)
    {
        if (before.Version != after.Version || before.NextTargetId != after.NextTargetId || before.Entities.Count != after.Entities.Count ||
            !before.Entities.Zip(after.Entities, (a, b) => a.EntityId == b.EntityId && a.TargetId == b.TargetId &&
                a.NextSlotId == b.NextSlotId && a.Slots.SequenceEqual(b.Slots)).All(equal => equal))
            throw new ArgumentException("Restore cannot allocate native bindings; explicit creation admission is required.");
    }
}
