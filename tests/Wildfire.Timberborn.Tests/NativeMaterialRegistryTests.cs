using System.Text.Json;
using Wildfire.Core;
using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeMaterialRegistryTests
{
    private static readonly FireGrid Grid = new(12, 12, 4);
    private static readonly Guid TreeId = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly Guid BuildingId = Guid.Parse("00000000-0000-0000-0000-000000000022");

    [Fact]
    public void OneGuidOwnsCompositeProfileAndPackedDefinitionWhileLowerContributorSurvives()
    {
        var registry = new TimberbornNativeMaterialRegistry(Grid, new[] { 10 });
        var structure = TimberbornMaterialPart.Building("LumberMill.Folktails");
        var stock = TimberbornMaterialPart.StoredGood("Log");
        registry.Reconcile(new[] { Project(TreeId, 10, TimberbornMaterialPart.Tree("Pine")), Project(BuildingId, 10, structure, stock) }, Array.Empty<Guid>());
        var cell = registry.ResolveCell(10);
        Assert.Equal(BuildingId, cell.Owner!.Value.EntityId);
        Assert.Equal(structure.MaterialClass, cell.Profile.MaterialClass);
        Assert.Equal(Math.Min(15, structure.InitialFuel + stock.InitialFuel), PackedCell.Fuel(cell.PackedDefinition));
        Assert.True(cell.HasSameEntityStorageComposite);
        Assert.Equal(2, cell.Contributors.Count);
        var treeOwner = cell.Contributors.Single(contributor => contributor.Owner.EntityId == TreeId).Owner;
        Assert.NotEqual(cell.Owner.Value.TargetId, treeOwner.TargetId);
        registry.Reconcile(Array.Empty<TimberbornMaterialProjection>(), new[] { BuildingId });
        Assert.Equal(treeOwner, registry.ResolveCell(10).Owner);
        registry.Reconcile(Array.Empty<TimberbornMaterialProjection>(), new[] { TreeId });
        Assert.Null(registry.ResolveCell(10).Owner);
        Assert.Equal(1, PackedCell.Terrain(registry.ResolveCell(10).PackedDefinition));
        Assert.Equal(WildfireMaterialClass.Terrain, registry.ResolveCell(10).Profile.MaterialClass);
        Assert.Equal(WildfireMaterialClass.Empty, registry.ResolveCell(11).Profile.MaterialClass);
        Assert.Equal(0, PackedCell.Terrain(registry.ResolveCell(11).PackedDefinition));
    }

    [Fact]
    public void CompositePreservesInitialStorageAdditionWithoutChangingStructureProfile()
    {
        var registry = new TimberbornNativeMaterialRegistry(Grid, Array.Empty<int>());
        var infrastructure = TimberbornMaterialPart.Infrastructure();
        var stock = TimberbornMaterialPart.StoredGood("Log");
        registry.Reconcile(new[] { Project(BuildingId, 10, infrastructure, stock) }, Array.Empty<Guid>());
        var resolved = registry.ResolveCell(10);
        Assert.True(stock.InitialFuel > 0);
        Assert.Equal(stock.InitialFuel, PackedCell.Fuel(resolved.PackedDefinition));
        Assert.Equal(infrastructure.MaterialClass, resolved.Profile.MaterialClass);
        Assert.True(resolved.HasSameEntityStorageComposite);
    }

    [Fact]
    public void MaterialClassComesFromSameFuelWinnerInsteadOfLargestTargetToken()
    {
        var registry = new TimberbornNativeMaterialRegistry(Grid, Array.Empty<int>());
        var tree = TimberbornMaterialPart.Tree("Pine");
        var crop = TimberbornMaterialPart.Crop("Carrot");
        Assert.True(tree.InitialFuel > crop.InitialFuel);
        registry.Reconcile(new[] { Project(TreeId, 10, tree), Project(BuildingId, 10, crop) }, Array.Empty<Guid>());
        var resolved = registry.ResolveCell(10);
        Assert.Equal(TreeId, resolved.Owner!.Value.EntityId);
        Assert.Equal(tree.InitialFuel, PackedCell.Fuel(resolved.PackedDefinition));
        Assert.Equal(tree.MaterialClass, resolved.Profile.MaterialClass);
        Assert.True(resolved.Contributors.Single(contributor => contributor.Owner.EntityId == BuildingId).Owner.TargetId > resolved.Owner.Value.TargetId);
    }

    [Fact]
    public void AddingOrHidingLocalFootprintSlotsDoesNotRenumberKnownSlots()
    {
        var registry = new TimberbornNativeMaterialRegistry(Grid, Array.Empty<int>());
        var parts = new[] { TimberbornMaterialPart.Tree("Pine") };
        registry.Reconcile(new[] { new TimberbornMaterialProjection(TreeId, new[] { Slot(1, 10) }, parts) }, Array.Empty<Guid>());
        var known = registry.ResolveCell(10).Owner!.Value;
        registry.Reconcile(new[] { new TimberbornMaterialProjection(TreeId, new[] { Slot(0, 11), Slot(1, 12) }, parts) }, Array.Empty<Guid>());
        Assert.Equal(known, registry.ResolveCell(12).Owner);
        var added = registry.ResolveCell(11).Owner!.Value;
        Assert.Equal(known.TargetId, added.TargetId);
        Assert.NotEqual(known.SlotId, added.SlotId);
        registry.Reconcile(new[] { new TimberbornMaterialProjection(TreeId, new[] { Slot(0, 11) }, parts) }, Array.Empty<Guid>());
        Assert.Equal(2, registry.CaptureBindings().Entities.Single().Slots.Count);
        // New binding is NOT proof of a Fresh GPU activation for this previously known target.
    }

    [Fact]
    public void RejectedCrossOwnerStockMixCannotPartiallyMoveRemoveOrAllocate()
    {
        var registry = new TimberbornNativeMaterialRegistry(Grid, Array.Empty<int>());
        registry.Reconcile(new[] { Project(BuildingId, 10, TimberbornMaterialPart.Building("LumberMill.Folktails")),
            Project(TreeId, 11, TimberbornMaterialPart.Tree("Pine")) }, Array.Empty<Guid>());
        string before = JsonSerializer.Serialize(registry.CaptureBindings());
        Assert.Throws<InvalidOperationException>(() => registry.Reconcile(new[] {
            Project(BuildingId, 20, TimberbornMaterialPart.Building("LumberMill.Folktails")),
            Project(Guid.NewGuid(), 20, TimberbornMaterialPart.StoredGood("Log")) }, new[] { TreeId }));
        Assert.Equal(before, JsonSerializer.Serialize(registry.CaptureBindings()));
        foreach (var binding in registry.CaptureBindings().Entities)
        {
            Assert.True(registry.TryResolveOrigin(binding.TargetId, out Guid origin));
            Assert.Equal(binding.EntityId, origin);
        }
        Assert.False(registry.TryResolveOrigin(registry.CaptureBindings().NextTargetId, out _));
        Assert.Equal(BuildingId, registry.ResolveCell(10).Owner!.Value.EntityId);
        Assert.Equal(TreeId, registry.ResolveCell(11).Owner!.Value.EntityId);
        Assert.Null(registry.ResolveCell(20).Owner);
    }

    [Fact]
    public void BindingsRoundTripRetainsHiddenRemovedAndReorderedLocalSlots()
    {
        var registry = new TimberbornNativeMaterialRegistry(Grid, Array.Empty<int>());
        var parts = new[] { TimberbornMaterialPart.Tree("Pine") };
        var footprint = new[] { Slot(0, 10), Slot(1, 11) };
        registry.Reconcile(new[] { new TimberbornMaterialProjection(TreeId, footprint, parts) }, Array.Empty<Guid>());
        var first = registry.ResolveCell(10).Owner;
        var second = registry.ResolveCell(11).Owner;
        registry.Reconcile(Array.Empty<TimberbornMaterialProjection>(), new[] { TreeId });
        var snapshot = JsonSerializer.Deserialize<TimberbornMaterialBindingSnapshot>(JsonSerializer.Serialize(registry.CaptureBindings()))!;
        var restored = new TimberbornNativeMaterialRegistry(Grid, Array.Empty<int>());
        restored.RestoreBindings(snapshot);
        restored.Reconcile(new[] { new TimberbornMaterialProjection(TreeId, new[] { Slot(1, 30), Slot(0, 31) }, parts) }, Array.Empty<Guid>());
        Assert.Equal(first, restored.ResolveCell(31).Owner);
        Assert.Equal(second, restored.ResolveCell(30).Owner);
        Assert.NotEqual(first!.Value.SlotId, second!.Value.SlotId);
        // This identity round-trip contains no current fuel/archive and cannot authorize GPU Fresh.
    }

    [Fact]
    public void EquivalentContributorEnumerationSelectsSameGuidProfileAndBits()
    {
        var projections = new[] { Project(TreeId, 10, TimberbornMaterialPart.Tree("Pine")),
            Project(BuildingId, 10, TimberbornMaterialPart.Tree("Pine")) };
        var first = new TimberbornNativeMaterialRegistry(Grid, Array.Empty<int>());
        var second = new TimberbornNativeMaterialRegistry(Grid, Array.Empty<int>());
        first.Reconcile(projections, Array.Empty<Guid>());
        second.Reconcile(projections.Reverse(), Array.Empty<Guid>());
        Assert.Equal(first.ResolveCell(10).Owner, second.ResolveCell(10).Owner);
        Assert.Equal(first.ResolveCell(10).Profile, second.ResolveCell(10).Profile);
        Assert.Equal(first.ResolveCell(10).PackedDefinition, second.ResolveCell(10).PackedDefinition);
    }

    [Fact]
    public void NewEntityAtSameCellGetsNewTokenAndInvalidFootprintLeavesPriorOwner()
    {
        var registry = new TimberbornNativeMaterialRegistry(Grid, Array.Empty<int>());
        registry.Reconcile(new[] { Project(TreeId, 10, TimberbornMaterialPart.Tree("Pine")) }, Array.Empty<Guid>());
        uint prior = registry.ResolveCell(10).Owner!.Value.TargetId;
        Assert.Throws<ArgumentOutOfRangeException>(() => registry.Reconcile(new[] {
            Project(BuildingId, Grid.CellCount, TimberbornMaterialPart.Crop("Carrot")) }, new[] { TreeId }));
        Assert.Equal(prior, registry.ResolveCell(10).Owner!.Value.TargetId);
        registry.Reconcile(new[] { Project(BuildingId, 10, TimberbornMaterialPart.Crop("Carrot")) }, new[] { TreeId });
        Assert.NotEqual(prior, registry.ResolveCell(10).Owner!.Value.TargetId);
    }

    [Fact]
    public void ConflictingPersistedTokensFailBeforeRestorePublishes()
    {
        var registry = new TimberbornNativeMaterialRegistry(Grid, Array.Empty<int>());
        var slots = new[] { new TimberbornMaterialSlotBinding(new(0, 0, 0), 1) };
        Assert.Throws<ArgumentException>(() => registry.RestoreBindings(new TimberbornMaterialBindingSnapshot(1, 2,
            new[] { new TimberbornMaterialEntityBinding(TreeId, 1, 2, slots), new TimberbornMaterialEntityBinding(BuildingId, 1, 2, slots) })));
        Assert.Empty(registry.CaptureBindings().Entities);
        Assert.False(registry.TryResolveOrigin(1, out _));
    }

    [Fact]
    public void OriginLookupRetainsHiddenAndRemovedOwnersAcrossRestore()
    {
        var registry = new TimberbornNativeMaterialRegistry(Grid, Array.Empty<int>());
        registry.Reconcile(new[] { Project(TreeId, 10, TimberbornMaterialPart.Tree("Pine")),
            Project(BuildingId, 10, TimberbornMaterialPart.Building("LumberMill.Folktails")) }, Array.Empty<Guid>());
        var tree = registry.ResolveCell(10).Contributors.Single(value => value.Owner.EntityId == TreeId).Owner;
        Assert.Equal(BuildingId, registry.ResolveCell(10).Owner!.Value.EntityId);
        Assert.True(registry.TryResolveOrigin(tree.TargetId, out Guid hidden));
        Assert.Equal(TreeId, hidden);
        registry.Reconcile(Array.Empty<TimberbornMaterialProjection>(), new[] { TreeId });
        var restored = new TimberbornNativeMaterialRegistry(Grid, Array.Empty<int>());
        restored.RestoreBindings(registry.CaptureBindings());
        Assert.True(restored.TryResolveOrigin(tree.TargetId, out Guid removed));
        Assert.Equal(TreeId, removed);
        Assert.Null(restored.ResolveCell(10).Owner);
        Assert.False(restored.TryResolveOrigin(0, out _));
        Assert.False(restored.TryResolveOrigin(registry.CaptureBindings().NextTargetId, out _));
    }

    private static TimberbornMaterialFootprintSlot Slot(int localX, int cell) => new(new(localX, 0, 0), cell);
    private static TimberbornMaterialProjection Project(Guid id, int cell, params TimberbornMaterialPart[] parts) => new(id, new[] { Slot(0, cell) }, parts);
}
