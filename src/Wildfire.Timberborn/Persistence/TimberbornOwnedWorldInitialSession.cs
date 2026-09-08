using Wildfire.Core;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Persistence;

public sealed partial class TimberbornOwnedWorldSession<TSimulator>
    where TSimulator : class, IGpuFireSimulator, IFireSimSnapshotSimulator, IDisposable
{
    /// <summary>Stage a genuinely new fire world. Never use this entry as fallback after failed restore.</summary>
    internal static TimberbornOwnedWorldSession<TSimulator> PrepareInitial(FireGrid grid,
        Func<FireGrid, TimberbornInitialWorldCapture> captureDuringScope,
        Func<TimberbornInitialWorldCapture, TimberbornInitialOwnedPlan> compose,
        Func<FireSimSnapshot, TSimulator> createSimulator, TimberbornOwnedNativeEffects effects,
        INativeResourceMutationGuard guard)
    {
        if (captureDuringScope is null) throw new ArgumentNullException(nameof(captureDuringScope));
        if (compose is null) throw new ArgumentNullException(nameof(compose));
        if (createSimulator is null) throw new ArgumentNullException(nameof(createSimulator));
        if (guard is null) throw new ArgumentNullException(nameof(guard));
        return guard.CaptureAtRest(() =>
        {
            var capture = captureDuringScope(grid) ?? throw new InvalidOperationException("No initial native capture returned.");
            if (capture.Grid != grid) throw new ArgumentException("Initial capture belongs to a different grid.");
            var inventories = capture.InventoryDeclarations ?? throw new NotSupportedException("Initial formation requires explicit original inventory declaration evidence.");
            inventories.RequireSupportedMaterialBodies(capture.Bodies);
            var plan = compose(capture) ?? throw new InvalidOperationException("No explicit initial accounting plan returned.");
            var compiled = TimberbornInitialBodyCompiler.Compile(capture, plan.Bodies);
            var environment = TimberbornInitialEnvironmentProjection.Project(capture.Environment);
            var registry = new TimberbornNativeMaterialRegistry(environment.MaterialBaseline);
            registry.Reconcile(compiled.Projections, Array.Empty<Guid>());
            var damage = compiled.CreateDamage(grid);
            var consumer = TimberbornOwnedDeltaConsumer.CreateWithCompleteNativeDefinitionsDuringCapture(registry, damage, effects, guard, capture.Bodies, inventories);
            var initial = TimberbornInitialSimulationAssembly.Build(registry, environment, plan);
            var bindings = registry.CaptureBindings();
            var consequences = TimberbornWildfirePersistenceCodec.CaptureConsequences(damage);
            var history = consumer.CopyHistoryDuringCapture();
            history.ValidateAssociation(initial, bindings, consequences);
            _ = new TimberbornOwnedMaterialSnapshot(initial, bindings, history);
            TSimulator? simulator = null;
            try
            {
                // A factory receives its own mutable array copy; it cannot rewrite our expected initial authority.
                simulator = createSimulator(FireSimSnapshotValidation.ValidateAndClone(initial))
                    ?? throw new InvalidOperationException("No new initial simulator returned.");
                if (simulator.Width != grid.Width || simulator.Height != grid.Height || simulator.Depth != grid.Depth ||
                    simulator.SnapshotCapability != FireSimSnapshotCapability.CompleteMaterialHistory)
                    throw new ArgumentException("Initial simulator must retain complete material history on the captured grid.");
                var actual = FireSimSnapshotValidation.ValidateAndClone(simulator.CaptureSnapshot());
                TimberbornOwnedSimulationValidation.RequireUnchanged(initial, actual);
                // This invokes native liveness callbacks; the complete reread below must follow it.
                consumer.CopyHistoryDuringCapture().ValidateAssociation(actual, bindings, consequences);
                var final = captureDuringScope(grid) ?? throw new InvalidOperationException("No final native capture returned.");
                if (!capture.SameReadings(final)) throw new ArgumentException("Native world changed during initial staging; no session was published.");
                TimberbornOwnedSimulationValidation.RequireUnchanged(initial,
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
}
