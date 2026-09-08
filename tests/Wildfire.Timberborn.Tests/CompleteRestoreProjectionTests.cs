using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Persistence;
using Wildfire.Timberborn.Resources;
using Backend = Wildfire.Timberborn.Tests.OwnedNativeRestoreFixture.Simulator;

namespace Wildfire.Timberborn.Tests;

public sealed class CompleteRestoreProjectionTests
{
    [Fact]
    public void RestoreRebuildsHiddenDefinitionsAndRichBaselineWithoutReplayingInitialFieldsOrAccounting()
    {
        var f = new Fixture();
        var encoded = TimberbornWildfirePersistenceCodec.Encode(f.Saved);
        using var restored = f.Restore();
        Assert.Equal(TimberbornDesiredWorldCapability.CompleteStaged, restored.DesiredWorldCapability);
        Assert.Equal(2, restored.Registry.ResolveCell(0).Contributors.Count);
        Assert.Equal(WildfireMaterialClass.Terrain, restored.Registry.ResolveCell(6).Profile.MaterialClass);
        Assert.Equal(0, PackedCell.Terrain(restored.Registry.ResolveCell(6).PackedDefinition));
        Assert.Equal(WildfireMaterialClass.Water, restored.Registry.ResolveCell(2).Profile.MaterialClass);
        Assert.Equal(WildfireMaterialClass.Badwater, restored.Registry.ResolveCell(3).Profile.MaterialClass);
        Assert.Equal(1, PackedCell.Terrain(restored.Registry.ResolveCell(4).PackedDefinition));
        var simulation = restored.Simulator.CaptureSnapshot();
        Assert.All(simulation.Cells, cell => Assert.Equal(1, PackedCell.Water(cell)));
        Assert.Equal(3, PackedCell.Fuel(checked((ushort)Assert.Single(simulation.MaterialAuthority.Archives).PackedCell)));
        Assert.All(restored.Damage.States.Values, state => Assert.Equal(15, state.DamageCapacity));
        Assert.All(f.Current.RetainedBodies, body => Assert.Equal(3, body.Yields[0].ActualAmount));
        var empty = TimberbornWildfirePersistenceSnapshot.Empty;
        Assert.Equal(encoded, TimberbornWildfirePersistenceCodec.Encode(restored.Capture(empty.AshField, empty.BeaverBehavior)));
        Assert.Empty(f.Native.CropCalls);
        Assert.Empty(f.Native.InventoryCalls);
    }

    [Fact]
    public void DisabledPhysicalInventoryRemainsDesiredMaterialWithoutChangingStockOrSavedAuthority()
    {
        var f = new Fixture(withDisabledStock: true);
        var before = TimberbornWildfirePersistenceCodec.Encode(f.Saved);
        using var restored = f.Restore();
        Assert.Contains(restored.Registry.ResolveCell(0).Contributors,
            contributor => contributor.Parts.Contains(TimberbornMaterialPart.StoredGood("Carrot")));
        Assert.All(f.Current.RetainedBodies, body =>
        {
            var inventory = Assert.Single(body.Inventories);
            Assert.False(inventory.Enabled);
            Assert.Equal(2, Assert.Single(inventory.Stock).Amount);
        });
        var empty = TimberbornWildfirePersistenceSnapshot.Empty;
        Assert.Equal(before, TimberbornWildfirePersistenceCodec.Encode(restored.Capture(empty.AshField, empty.BeaverBehavior)));
        Assert.Empty(f.Native.InventoryCalls);
    }

    [Theory]
    [InlineData("state", 0)]
    [InlineData("state", 3)]
    [InlineData("disabled", 0)]
    [InlineData("disabled", 3)]
    public void VerifiedKnownTreeMaterialRestoresExactHistoryWithoutFreshEligibility(string state, int archivedFuel)
    {
        var f = new Fixture(tree: true);
        f.Current = f.Observe(state);
        var material = f.Saved.OwnedMaterial!;
        var simulation = material.CaptureSimulation();
        var archive = Assert.Single(simulation.MaterialAuthority.Archives);
        simulation = simulation with { MaterialAuthority = simulation.MaterialAuthority with
            { Archives = [archive with { PackedCell = (uint)archivedFuel }] } };
        f.Saved = f.Saved with { OwnedMaterial = new(simulation, material.Bindings, material.History) };
        var before = TimberbornWildfirePersistenceCodec.Encode(f.Saved);
        var ids = f.Current.RetainedBodies.Select(body => body.EntityId).ToArray();
        Assert.Empty(f.Current.CaptureFreshEligibleOwners(ids));
        using var restored = f.Restore();
        Assert.Equal(2, restored.Registry.ResolveCell(0).Contributors.Count);
        Assert.Equal(archivedFuel, PackedCell.Fuel((ushort)Assert.Single(restored.Simulator.CaptureSnapshot().MaterialAuthority.Archives).PackedCell));
        Assert.Equal(simulation.Cells, restored.Simulator.CaptureSnapshot().Cells);
        var empty = TimberbornWildfirePersistenceSnapshot.Empty;
        Assert.Equal(before, TimberbornWildfirePersistenceCodec.Encode(restored.Capture(empty.AshField, empty.BeaverBehavior)));
        Assert.Empty(f.Native.TreeCalls);
        Assert.Empty(f.Native.InventoryCalls);
        Assert.All(restored.Damage.States.Values, value => Assert.Equal(60, value.DamageCapacity));
    }

    [Fact]
    public void RetainedTreeLeftoverRemainsExplicitlyUnsupported()
    {
        var f = new Fixture(tree: true);
        f.Current = f.Observe("excluded");
        Assert.Throws<NotSupportedException>(() => f.Restore());
        Assert.Null(f.Backend);
        Assert.Empty(f.Native.TreeCalls);
    }

    [Fact]
    public void CurrentSupportedLiveObservationDerivesFreshEligibilityWithoutSavingIt()
    {
        var f = new Fixture(tree: true);
        var ids = f.Current.RetainedBodies.Select(body => body.EntityId).ToArray();
        Assert.Equal(ids, f.Current.CaptureFreshEligibleOwners(ids));
        f.Current = f.Observe("state");
        Assert.Empty(f.Current.CaptureFreshEligibleOwners(ids));
        f.Current = f.Observe();
        Assert.Equal(ids, f.Current.CaptureFreshEligibleOwners(ids));
    }

    [Fact]
    public void UnprovedOrChangedNativeTreeCompositionCannotBroadenKnownMaterialRestore()
    {
        var f = new Fixture(tree: true);
        f.Current = f.Observe("tree-evidence");
        Assert.Throws<NotSupportedException>(() => f.Restore());
        Assert.Null(f.Backend);
        f.Current = f.Observe("state");
        f.AfterCreate = () => f.Current = f.Observe("tree-evidence");
        Assert.Throws<ArgumentException>(() => f.Restore());
        Assert.Equal(1, f.Backend!.Disposals);
        Assert.False(f.Guard.IsIndeterminate);
        Assert.Empty(f.Native.TreeCalls);
    }

    [Theory]
    [InlineData("membership")]
    [InlineData("quantity")]
    [InlineData("placement")]
    [InlineData("water")]
    [InlineData("state")]
    [InlineData("declaration")]
    public void FactoryCallbackDriftDisposesUnpublishedBackendAndDoesNotPoisonReadScope(string mutation)
    {
        var f = new Fixture();
        f.AfterCreate = () => f.Current = f.Observe(mutation);
        Assert.ThrowsAny<ArgumentException>(() => f.Restore());
        Assert.Equal(1, f.Backend!.Disposals);
        Assert.False(f.Guard.IsIndeterminate);
        f.Guard.ThrowIfSaveUnsafe();
        Assert.Empty(f.Native.CropCalls);
    }

    [Theory]
    [InlineData("membership")]
    [InlineData("excluded")]
    [InlineData("state")]
    [InlineData("disabled")]
    [InlineData("new-role")]
    public void UnsupportedCurrentStateCannotBecomeSuspensionFreshMaterialOrNewOwner(string mutation)
    {
        var f = new Fixture();
        f.Current = f.Observe(mutation);
        Assert.Throws<NotSupportedException>(() => f.Restore());
        Assert.Null(f.Backend);
        f.Guard.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void EveryReadProducerRejectsMutationReentryAndSuccessfulCaptureCallbacksCannotSplitTheSnapshot()
    {
        var f = new Fixture();
        void Check() => Assert.Throws<InvalidOperationException>(() => f.Guard.TransferInventory(() => throw new Exception("unreachable")));
        f.DuringCapture = Check;
        f.AfterCreate = Check;
        f.Native.DuringIsLive = Check;
        using var restored = f.Restore();
        Assert.False(f.Guard.IsIndeterminate);
        f.Guard.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void MissingOriginalDeclarationEvidenceCannotBeReconstructedFromCurrentlyEmptyRoles()
    {
        var legacy = new OwnedNativeRestoreFixture();
        int calls = 0;
        Assert.Throws<NotSupportedException>(() => TimberbornOwnedWorldSession<Backend>.PrepareCompleteRestore(legacy.Snapshot(),
            value => { calls++; return new(value); }, (_, _) => { calls++; throw new Exception("no observed upgrade"); }, legacy.Effects, legacy.Guard));
        Assert.Equal(0, calls);
        using var diagnostic = legacy.Restore(legacy.Snapshot(), [OwnedNativeRestoreFixture.Facts()], out _);
        Assert.Equal(TimberbornDesiredWorldCapability.DiagnosticOnly, diagnostic.DesiredWorldCapability);
        Assert.Null(diagnostic.Registry.ResolveCell(0).Owner);
    }

    [Fact]
    public void MissingNeverActivatedLocalBindingCannotBeAllocatedAsAnIncidentalRestore()
    {
        var f = new Fixture();
        var material = f.Saved.OwnedMaterial!;
        // Keep the GPU-known visible owner bound but remove the hidden owner's never-active slot.
        var hidden = material.Bindings.Entities[1];
        var bindings = material.Bindings with { Entities = [material.Bindings.Entities[0], hidden with { Slots = [] }] };
        var original = material.CaptureSimulation();
        var activeOnly = original with { MaterialAuthority = new(0, [new(1, 1)], []) };
        f.Saved = f.Saved with { OwnedMaterial = new(activeOnly, bindings, material.History) };
        Assert.Throws<ArgumentException>(() => f.Restore());
        Assert.Null(f.Backend);
        Assert.Empty(bindings.Entities[1].Slots);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CallbackCannotAlterSavedSimulatorAuthorityAfterFactoryFidelityCheck(bool duringNativeObserver)
    {
        var f = new Fixture();
        void ChangeSavedFields()
        {
            if (f.Backend is { } backend)
                backend.CaptureSnapshot().Cells[2] = PackedCell.SetWater(backend.CaptureSnapshot().Cells[2], 2);
        }
        if (duringNativeObserver) f.AfterCreate = () => f.Native.DuringIsLive = ChangeSavedFields;
        else f.DuringCapture = ChangeSavedFields;
        Assert.Throws<ArgumentException>(() => f.Restore());
        Assert.Equal(1, f.Backend!.Disposals);
        Assert.False(f.Guard.IsIndeterminate);
    }

    [Fact]
    public void NewCurrentInventoryDeclarationCannotBeAcceptedAgainstOriginalEmptyEvidence()
    {
        var f = new Fixture();
        f.Current = f.Observe("declaration");
        Assert.Throws<ArgumentException>(() => f.Restore());
        Assert.Null(f.Backend);
        f.Guard.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void LegacyTerrainOnlyObservationCannotClaimCompleteRestoreOrRewriteSavedGrid()
    {
        var f = new Fixture(); var world = f.Current.CurrentWorld; var env = world.Environment;
        var saved = TimberbornWildfirePersistenceCodec.Encode(f.Saved);
        var legacy = new TimberbornInitialWorldCapture(world.Grid, world.Bodies, world.Excluded, world.WaterSources,
            new(env.Grid, env.SolidVoxelIndices, env.SoilSurfaces, env.WaterColumns), world.InventoryDeclarations!);
        f.Current = new(legacy, f.Current.RetainedBodies, f.Current.States);
        Assert.Throws<NotSupportedException>(() => f.Restore());
        Assert.Null(f.Backend); f.Guard.ThrowIfSaveUnsafe();
        Assert.Equal(saved, TimberbornWildfirePersistenceCodec.Encode(TimberbornWildfirePersistenceCodec.Decode(saved)));
    }

    private sealed class Fixture
    {
        private static readonly Guid A = new("00000000-0000-0000-0000-000000000001");
        private static readonly Guid B = new("00000000-0000-0000-0000-000000000002");
        private static readonly FireGrid Grid = new(5, 1, 2);
        internal readonly NativeResourceTransaction Guard = new();
        internal readonly OwnedConsequenceBatchTests.NativeFake Native = new();
        internal TimberbornWildfirePersistenceSnapshot Saved;
        internal TimberbornOwnedRestoreObservation Current;
        internal Backend? Backend;
        internal Action? AfterCreate, DuringCapture;
        private readonly bool _withDisabledStock;
        private readonly bool _tree;
        internal Fixture(bool withDisabledStock = false, bool tree = false)
        {
            _withDisabledStock = withDisabledStock;
            _tree = tree;
            var original = Observe(actual: 5);
            var compiled = TimberbornInitialBodyCompiler.Compile(original.CurrentWorld, original.RetainedBodies.Select(body =>
                new TimberbornInitialBodySelection(body.EntityId, TimberbornInitialAccountingBasis.NativeResourceAmounts,
                    [new(_tree ? "Cuttable" : "Gatherable", _tree ? TimberbornCapturedYieldRole.Cuttable : TimberbornCapturedYieldRole.Gatherable, TimberbornInitialYieldUse.Actual)],
                    withDisabledStock ? [new(new(TimberbornNativeInventoryRole.GoodStack, "HarvestStack"), TimberbornInitialInventoryUse.Excluded)] : [])));
            var registry = new TimberbornNativeMaterialRegistry(Grid, []);
            registry.Reconcile(compiled.Projections, []);
            var damage = compiled.CreateDamage(Grid);
            var consumer = Guard.CaptureAtRest(() => TimberbornOwnedDeltaConsumer.CreateWithCompleteNativeDefinitionsDuringCapture(
                registry, damage, new(Native, Native, Native, Native, Native), Guard, original.RetainedBodies, original.InventoryDeclarations));
            ushort[] cells = Enumerable.Range(0, Grid.CellCount).Select(_ => PackedCell.Pack(0, 1, 0, 1, 0, 0)).ToArray();
            cells[0] = PackedCell.SetFuel(cells[0], 7);
            var targets = new uint[Grid.CellCount];
            targets[0] = 1;
            var simulation = new FireSimSnapshot(1, Grid, 37, FireSimParameters.Default, 4, cells, new uint[Grid.CellCount], new uint[Grid.CellCount],
                targets, targets.ToArray(), new(1, [new(1, 1), new(2, 1)],
                    [new(new(2, 1), 1, 0, 3, 0)]), [new FireSimChange(2, SetWater: 2)]);
            Saved = TimberbornWildfirePersistenceSnapshot.Empty with { PersistenceVersion = 2,
                OwnedMaterial = new(simulation, registry.CaptureBindings(), consumer.CaptureHistory()),
                Consequences = TimberbornWildfirePersistenceCodec.CaptureConsequences(damage) };
            Current = Observe();
        }
        internal TimberbornOwnedRestoreObservation Observe(string? mutation = null, int actual = 3)
        {
            TimberbornInitialMaterialBody Body(Guid id) => new(id, _tree ? "Pine" : "Carrot", _tree ? TimberbornInitialBodyShape.Tree : TimberbornInitialBodyShape.Crop,
                [new(new(0, 0, 0), mutation == "placement" && id == B ? 1 : 0)],
                [new(_tree ? TimberbornCapturedYieldRole.Cuttable : TimberbornCapturedYieldRole.Gatherable,
                    _tree ? "Cuttable" : "Gatherable", _tree ? "Log" : "Carrot",
                    _tree && mutation == "disabled" ? 0 : mutation == "quantity" ? 2 : actual,
                    _tree ? "Log" : "Carrot", 5, false, mutation != "disabled")],
                _withDisabledStock ? [new(new(TimberbornNativeInventoryRole.GoodStack, "HarvestStack"), false, [new("Carrot", 2)])] : [], null);
            var bodies = new[] { Body(A), Body(B) };
            var states = bodies.Select(body => new TimberbornRetainedBodyObservation(body.EntityId, null, mutation is "state" or "tree-evidence", _tree && mutation != "tree-evidence",
                mutation is "declaration" or "new-role" ? [new(mutation == "new-role" ? TimberbornNativeInventoryRole.Manufactory : TimberbornNativeInventoryRole.GoodStack, "HarvestStack")] : _withDisabledStock ? [new(TimberbornNativeInventoryRole.GoodStack, "HarvestStack")] : [])).ToArray();
            var declarations = new TimberbornInventoryDeclarationCapture(states.Select(state => new TimberbornBodyInventoryDeclarations(state.EntityId, state.Inventories)));
            var worldBodies = mutation is "membership" or "excluded" ? bodies.Take(1).ToArray() : bodies;
            var world = new TimberbornInitialWorldCapture(Grid, worldBodies,
                mutation == "excluded" ? [new(B, _tree ? "Pine" : "Carrot", TimberbornInitialCaptureExclusion.TreeLeftover)] : [], [],
                TimberbornInitialEnvironmentCapture.ForOwnedDomain(new TimberbornWorldDomain(Grid, new(Grid.Width, Grid.Height, 1)), [1, 4], [new(6, .2f, .1f, true, true)],
                    [new(2, 0, 0, 1, mutation == "water" ? .9f : .5f, 0, 0), new(3, 0, 0, 1, .5f, .5f, 0)]),
                new(declarations.Bodies.Where(body => worldBodies.Any(value => value.EntityId == body.EntityId))));
            return new(world, bodies, states);
        }
        internal TimberbornOwnedWorldSession<Backend> Restore() => TimberbornOwnedWorldSession<Backend>.PrepareCompleteRestore(Saved,
            snapshot => { Backend = new(snapshot); AfterCreate?.Invoke(); return Backend; },
            (_, _) => { DuringCapture?.Invoke(); return Current; }, new(Native, Native, Native, Native, Native), Guard);
    }
}
