using System.Text.Json;
using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Persistence;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeFirstMaterialActivationTests
{
    private static readonly FireGrid Grid = new(4, 1, 1);
    private static readonly Guid Tree = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly Guid Blocker = Guid.Parse("00000000-0000-0000-0000-000000000022");
    private static readonly TimberbornMaterialFootprintSlot[] Footprint = [new(new(0, 0, 0), 0), new(new(1, 0, 0), 1)];
    private static TimberbornMaterialProjection Project(Guid entity, IEnumerable<TimberbornMaterialFootprintSlot> footprint,
        TimberbornMaterialPart part) => new(entity, footprint, [part]);
    private static FireSimMaterialIdentity Identity(TimberbornMaterialOwner owner) => new(owner.TargetId, owner.SlotId);

    [Fact]
    public void HiddenNeverActivatedSlotUsesCurrentProjectionWithoutAllocatingOrRefreshingKnownSibling()
    {
        var registry = new TimberbornNativeMaterialRegistry(Grid, []);
        registry.Reconcile([Project(Tree, Footprint, TimberbornMaterialPart.Tree("Pine")),
            Project(Blocker, [Footprint[1]], TimberbornMaterialPart.Building("LumberMill.Folktails"))], []);
        var first = Identity(registry.ResolveCell(0).Owner!.Value);
        var blocker = Identity(registry.ResolveCell(1).Owner!.Value);
        var hidden = Identity(registry.ResolveCell(1).Contributors.Single(c => c.Owner.EntityId == Tree).Owner);
        var authority = new Authority(Snapshot(Grid, (0, first), (1, blocker)));
        Assert.Throws<InvalidOperationException>(() => registry.CreateFirstActivationRequest(1, blocker, authority));
        registry.Reconcile([Project(Tree, Footprint, TimberbornMaterialPart.Crop("Carrot"))], [Blocker]);
        string bindings = JsonSerializer.Serialize(registry.CaptureBindings());
        var request = registry.CreateFirstActivationRequest(1, blocker, authority);
        Assert.Equal(hidden, request.Incoming);
        Assert.Equal(first.TargetId, request.Incoming.TargetId);
        Assert.Equal((uint)TimberbornMaterialPart.Crop("Carrot").InitialFuel, request.PackedMaterial & 15);
        Assert.NotEqual((uint)TimberbornMaterialPart.Tree("Pine").InitialFuel, request.PackedMaterial & 15);
        Assert.Throws<InvalidOperationException>(() => registry.CreateFirstActivationRequest(0, first, authority));
        Assert.Equal(bindings, JsonSerializer.Serialize(registry.CaptureBindings()));
        Assert.False(authority.IsSlotKnown(hidden)); // Building a request does not publish GPU authority.
    }

    [Fact]
    public void PairedSaveBeforeFirstActivationRetainsBindingButRequiresCurrentProjectionAndCompleteAuthority()
    {
        var registry = new TimberbornNativeMaterialRegistry(Grid, []);
        registry.Reconcile([Project(Tree, Footprint, TimberbornMaterialPart.Tree("Pine"))], []);
        var first = Identity(registry.ResolveCell(0).Owner!.Value);
        var second = Identity(registry.ResolveCell(1).Owner!.Value);
        var simulation = Snapshot(Grid, (0, first));
        var envelope = TimberbornWildfirePersistenceSnapshot.Empty with { PersistenceVersion = 2,
            OwnedMaterial = new TimberbornOwnedMaterialSnapshot(simulation, registry.CaptureBindings()) };
        var saved = TimberbornWildfirePersistenceCodec.Decode(TimberbornWildfirePersistenceCodec.Encode(envelope)).OwnedMaterial!;
        var restored = new TimberbornNativeMaterialRegistry(Grid, []);
        restored.RestoreBindings(saved.Bindings);
        var authority = new Authority(saved.CaptureSimulation());
        Assert.Throws<InvalidOperationException>(() => restored.CreateFirstActivationRequest(1, default, authority));
        restored.Reconcile([Project(Tree, Footprint.Reverse(), TimberbornMaterialPart.Tree("Pine"))], []);
        Assert.Equal(second, restored.CreateFirstActivationRequest(1, default, authority).Incoming);
        Assert.True(authority.IsSlotKnown(first));
        Assert.False(authority.IsSlotKnown(second));
        Assert.Throws<InvalidOperationException>(() => restored.CreateFirstActivationRequest(1, default, new Authority(Grid)));
        Assert.Throws<ArgumentException>(() => new TimberbornOwnedMaterialSnapshot(simulation with {
            MaterialAuthority = simulation.MaterialAuthority with { KnownSlots = [] } }, saved.Bindings));
        var entity = saved.Bindings.Entities.Single();
        Assert.Throws<ArgumentException>(() => new TimberbornOwnedMaterialSnapshot(simulation, saved.Bindings with {
            Entities = [entity with { Slots = [entity.Slots[0], entity.Slots[1] with { SlotId = entity.Slots[0].SlotId }] }] }));
    }

    [Fact]
    public void ExhaustedKnownSlotCannotBeFreshAndNewGuidDoesNotRetargetItsArchive()
    {
        var registry = new TimberbornNativeMaterialRegistry(Grid, []);
        registry.Reconcile([Project(Tree, [Footprint[0]], TimberbornMaterialPart.Tree("Pine"))], []);
        var old = Identity(registry.ResolveCell(0).Owner!.Value);
        var snapshot = Snapshot(Grid) with { MaterialAuthority = new(1, [old], [new(old, 1, 0, 0, 9u << 12)]) };
        var authority = new Authority(snapshot);
        Assert.Throws<InvalidOperationException>(() => registry.CreateFirstActivationRequest(0, default, authority));
        Assert.True(authority.TryGetMaterialArchive(old, out var archive));
        Assert.Equal(0u, archive.PackedCell & 15);
        registry.Reconcile([Project(Blocker, [Footprint[0]], TimberbornMaterialPart.Tree("Pine"))], [Tree]);
        var replacement = registry.CreateFirstActivationRequest(0, default, authority);
        Assert.NotEqual(old.TargetId, replacement.Incoming.TargetId);
        Assert.True(registry.TryResolveOrigin(old.TargetId, out Guid retainedOrigin));
        Assert.Equal(Tree, retainedOrigin);
        Assert.True(authority.TryGetMaterialArchive(old, out var retained));
        Assert.Same(archive, retained);
    }

    [Fact]
    public void ActualNativeRotationsAndFlipsCannotMintFreshSlotsForKnownLocalCoordinates()
    {
        using var fixture = new NativeMaterialFootprintTests.NativeFootprintFixture();
        var grid = new FireGrid(20, 20, 5);
        var registry = new TimberbornNativeMaterialRegistry(grid, []);
        var initial = fixture.Project("Cw0", false, grid);
        registry.Reconcile([Project(Tree, initial, TimberbornMaterialPart.Tree("Pine"))], []);
        var authority = new Authority(Snapshot(grid, initial.Select(slot =>
            (slot.CellIndex, Identity(registry.ResolveCell(slot.CellIndex).Owner!.Value))).ToArray()));
        string bindings = JsonSerializer.Serialize(registry.CaptureBindings());
        foreach (string rotation in new[] { "Cw0", "Cw90", "Cw180", "Cw270" })
        foreach (bool flip in new[] { false, true })
        {
            var current = fixture.Project(rotation, flip, grid);
            registry.Reconcile([Project(Tree, current.Reverse(), TimberbornMaterialPart.Tree("Pine"))], []);
            foreach (var slot in current)
                Assert.Throws<InvalidOperationException>(() => registry.CreateFirstActivationRequest(slot.CellIndex, default, authority));
            Assert.Equal(bindings, JsonSerializer.Serialize(registry.CaptureBindings()));
        }
    }

    private static FireSimSnapshot Snapshot(FireGrid grid, params (int Cell, FireSimMaterialIdentity Identity)[] active)
    {
        var targets = new uint[grid.CellCount]; var slots = new uint[grid.CellCount];
        foreach (var entry in active) { targets[entry.Cell] = entry.Identity.TargetId; slots[entry.Cell] = entry.Identity.SlotId; }
        return new(1, grid, 0, FireSimParameters.Default, 1, new ushort[grid.CellCount], new uint[grid.CellCount],
            new uint[grid.CellCount], targets, slots, new(0, active.Select(entry => entry.Identity).ToArray(), []), []);
    }

    // Managed admission fixture uses the production snapshot validator and session; it never claims GPU execution.
    private sealed class Authority : IFireSimMaterialHandoffSimulator
    {
        private readonly FireSimStepCoordinator _coordinator;
        private readonly FireGrid _grid;
        public Authority(FireSimSnapshot snapshot) { _grid = snapshot.Grid; _coordinator = new(snapshot, _grid.CellCount); }
        public Authority(FireGrid grid) { _grid = grid; _coordinator = new(grid.CellCount, grid.CellCount); _coordinator.InitializeLegacyBuffers(0, () => { }); }
        public int Width => _grid.Width; public int Height => _grid.Height; public int Depth => _grid.Depth;
        public bool IsSlotKnown(FireSimMaterialIdentity identity) => _coordinator.IsSlotKnown(identity);
        public bool TryGetMaterialArchive(FireSimMaterialIdentity identity, out FireSimMaterialArchive archive) => _coordinator.TryGetMaterialArchive(identity, out archive);
        public GpuFireStepResult? TryHandoffMaterial(FireSimMaterialHandoffBatch batch, Action<FireSimMaterialHandoffReceipt> commit) => throw new NotSupportedException();
        public GpuFireStepResult? TryTickWithInput(FireSimChange input, Action commit) => throw new NotSupportedException();
        public void RegisterChange(FireSimChange change) => throw new NotSupportedException();
        public GpuFireStepResult Tick() => throw new NotSupportedException();
        public IDisposable Subscribe(IFireSimListener listener) => throw new NotSupportedException();
    }
}
