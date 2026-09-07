using Wildfire.Core;
using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Persistence;

/// <summary>One validated association of complete simulator state and retained native identity bindings.</summary>
public sealed class TimberbornOwnedMaterialSnapshot
{
    private readonly FireSimSnapshot _simulation;
    private readonly TimberbornMaterialBindingSnapshot _bindings;

    public TimberbornOwnedMaterialSnapshot(FireSimSnapshot simulation, TimberbornMaterialBindingSnapshot bindings)
    {
        _simulation = FireSimSnapshotValidation.ValidateAndClone(simulation);
        var registry = new TimberbornNativeMaterialRegistry(_simulation.Grid, Array.Empty<int>());
        registry.RestoreBindings(bindings);
        _bindings = registry.CaptureBindings();
        var available = _bindings.Entities.SelectMany(entity => entity.Slots.Select(slot =>
            new FireSimMaterialIdentity(entity.TargetId, slot.SlotId))).ToHashSet();
        if (_simulation.MaterialAuthority.KnownSlots.Any(identity => !available.Contains(identity)))
            throw new ArgumentException("Every simulator material identity requires an exact retained native Guid/local-slot binding.");
    }

    public FireSimSnapshot CaptureSimulation() => FireSimSnapshotValidation.ValidateAndClone(_simulation);
    public TimberbornMaterialBindingSnapshot Bindings => _bindings;

    public static TimberbornOwnedMaterialSnapshot Capture(IFireSimSnapshotSimulator simulator, TimberbornNativeMaterialRegistry registry) =>
        new(simulator.CaptureSnapshot(), registry.CaptureBindings());

    /// <summary>
    /// Stage a new pair without touching a running simulator or publishing registry bindings.
    /// The factory owns cleanup on construction failure; the caller owns the returned simulator's lifetime.
    /// Runtime publication still requires settled provider projection and exact-origin consequence routing.
    /// </summary>
    public TimberbornOwnedMaterialSession PrepareRestore(IEnumerable<int> solidTerrainCells, Func<FireSimSnapshot, IGpuFireSimulator> createNewSimulator)
    {
        if (createNewSimulator is null) throw new ArgumentNullException(nameof(createNewSimulator));
        var simulation = CaptureSimulation();
        var registry = new TimberbornNativeMaterialRegistry(simulation.Grid, solidTerrainCells);
        registry.RestoreBindings(_bindings);
        var simulator = createNewSimulator(simulation) ?? throw new InvalidOperationException("Snapshot factory returned no simulator.");
        return new TimberbornOwnedMaterialSession(simulator, registry);
    }
}

public sealed record TimberbornOwnedMaterialSession(IGpuFireSimulator Simulator, TimberbornNativeMaterialRegistry Registry);
