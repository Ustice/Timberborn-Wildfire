using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Planner = Wildfire.Timberborn.Mapping.TimberbornDesiredMaterialReconciliation;

namespace Wildfire.Timberborn.Tests;

public sealed class DesiredMaterialReconciliationTests
{
    private static readonly Guid A = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid B = new("00000000-0000-0000-0000-000000000002");
    private static readonly uint Tree = new WildfireMaterialFieldState(WildfireMaterialClass.Tree, 12, 5, 7,
        WildfireAshQuality.Fertile, WildfireContaminationBehavior.None, 6).Pack();

    [Fact]
    public void SurvivingOwnerMovementNeedsOneCoherentPlan()
    {
        var registry = Registry(2); registry.Reconcile([Projection(A, 0)], []);
        var simulator = Sim(2, [5, 0], [Tree, 0], [1, 0], [1, 0], [new(1, 1)]);
        registry.Reconcile([Projection(A, 1)], []);
        var owners = Owners(A);
        Assert.Null(RetiredMaterialDetachmentPrototype.Plan(registry, owners, simulator.CaptureSnapshot(), simulator));
        var plan = Planner.Plan(registry, owners, simulator, [])!;
        Assert.Equal(new[] { 0, 1 }, plan.Requests.Select(request => request.CellIndex));
        Assert.Equal(FireSimMaterialHandoffMode.CapturedSource, plan.Requests[1].Mode);
        Apply(simulator, plan);
        var after = simulator.CaptureSnapshot();
        Assert.Equal(new uint[] { 0, 1 }, after.TargetIds);
        Assert.Equal(5, PackedCell.Fuel(after.Cells[1]));
        Assert.False(simulator.TryGetMaterialArchive(new(1, 1), out _));
        Assert.Null(Planner.Plan(registry, owners, simulator, []));
    }

    [Fact]
    public void UnownedWaterToBadwaterNeedsPlanEvenWithoutRetiredOwner()
    {
        var registry = Registry(1, [new(0, FireSimBaselineDefinition.Badwater)]);
        var simulator = Sim(1, [0], [FireSimBaselineDefinition.Water.CompanionMaterial], [0], [0], []);
        Assert.Null(RetiredMaterialDetachmentPrototype.Plan(registry, Owners(), simulator.CaptureSnapshot(), simulator));
        var plan = Planner.Plan(registry, Owners(), simulator, [])!;
        Assert.Equal(FireSimMaterialHandoffMode.Baseline, Assert.Single(plan.Requests).Mode);
        Apply(simulator, plan);
        Assert.Equal(FireSimBaselineDefinition.Badwater.CompanionMaterial, simulator.CaptureSnapshot().CompanionFields[0]);
        Assert.Empty(simulator.CaptureSnapshot().MaterialAuthority.KnownSlots);
        Assert.Null(Planner.Plan(registry, Owners(), simulator, []));
    }

    [Fact]
    public void SwapsCaptureEachDistinctSlotBeforeAnyReplacementAndKeepDestinationAmbient()
    {
        var registry = Registry(2); registry.Reconcile([Projection(A, 0, 1)], []);
        var simulator = Sim(2, [PackedCell.Pack(0, 9, 3, 1, 1, 5), PackedCell.Pack(3, 2, 3, 2, 1, 0)],
            [Tree, Tree ^ (3u << 12)], [1, 1], [1, 2], [new(1, 1), new(1, 2)]);
        registry.Reconcile([Projection(A, 1, 0)], []);
        var plan = Planner.Plan(registry, Owners(A), simulator, [])!;
        Assert.All(plan.Requests, request => Assert.Equal(FireSimMaterialHandoffMode.CapturedSource, request.Mode));
        Apply(simulator, plan); var after = simulator.CaptureSnapshot();
        Assert.Equal(new uint[] { 2, 1 }, after.SlotIds);
        Assert.Equal(new[] { 3, 0 }, after.Cells.Select(cell => PackedCell.Fuel(cell)));
        Assert.Equal(new[] { 9, 2 }, after.Cells.Select(cell => PackedCell.Heat(cell)));
        Assert.Equal(new[] { 1, 2 }, after.Cells.Select(cell => PackedCell.Water(cell)));
        Assert.Equal(new uint[] { (Tree ^ (3u << 12)) & 0xf000, Tree & 0xf000 }, after.CompanionFields.Select(value => value & 0xf000));
        Assert.Empty(after.MaterialAuthority.Archives);
        Assert.Null(Planner.Plan(registry, Owners(A), simulator, []));
    }

    [Fact]
    public void OverlappingFootprintMoveClosesEverySourceBeforeAnySlotIsReplaced()
    {
        var registry = Registry(3); registry.Reconcile([Projection(A, 0, 1)], []);
        var simulator = Sim(3, [0, 4, 0], [Tree, Tree, 0], [1, 1, 0], [1, 2, 0], [new(1, 1), new(1, 2)]);
        registry.Reconcile([Projection(A, 1, 2)], []);
        var plan = Planner.Plan(registry, Owners(A), simulator, [])!;
        Assert.Equal(new[] { 0, 1, 2 }, plan.Requests.Select(request => request.CellIndex));
        Assert.Equal(new[] { -1, 0, 1 }, plan.Requests.Select(request => request.SourceCellIndex));
        simulator.MaterialHandoffCapacity = 2;
        Assert.Null(simulator.TryHandoffMaterial(plan, _ => throw new Exception("Unadmitted footprint cannot commit")));
        Assert.Equal(0, simulator.Uploads);
        simulator.MaterialHandoffCapacity = 3;
        Apply(simulator, plan);
        var after = simulator.CaptureSnapshot();
        Assert.Equal(new uint[] { 0, 1, 2 }, after.SlotIds);
        Assert.Equal(new[] { 0, 0, 4 }, after.Cells.Select(cell => PackedCell.Fuel(cell)));
        Assert.Empty(after.MaterialAuthority.Archives);
        Assert.Null(Planner.Plan(registry, Owners(A), simulator, []));
    }

    [Fact]
    public void HiddenExhaustedArchiveRevealsWithoutFreshFuelAndRetiredOwnerIsArchived()
    {
        var registry = Registry(1); registry.Reconcile([Projection(A, 0), Projection(B, 0)], []);
        var archived = new FireSimMaterialArchiveSnapshot(new(2, 1), 1, 0, 0, Tree);
        var simulator = Sim(1, [5], [Tree], [1], [1], [new(1, 1), new(2, 1)], [archived]);
        registry.Reconcile([], [A]);
        var owners = Owners(A, B); owners[A] = OwnedBodyRetention.RetiredNativeOwner;
        var plan = Planner.Plan(registry, owners, simulator, [])!;
        var request = Assert.Single(plan.Requests);
        Assert.True(simulator.TryGetMaterialArchive(new(2, 1), out var exact));
        Assert.Same(exact, request.Archive);
        Apply(simulator, plan);
        Assert.Equal(0, PackedCell.Fuel(simulator.CaptureSnapshot().Cells[0]));
        Assert.True(simulator.TryGetMaterialArchive(new(1, 1), out _));
        Assert.False(simulator.TryGetMaterialArchive(new(2, 1), out _));
        Assert.Null(Planner.Plan(registry, owners, simulator, []));
    }

    [Fact]
    public void FirstExposureUsesAlreadyBoundLocalSlotThenNeverRefillsIt()
    {
        var registry = Registry(2); registry.Reconcile([Projection(A, 0, 1)], []);
        var simulator = Sim(2, [0, 0], [Tree, 0], [1, 0], [1, 0], [new(1, 1)]);
        var beforeBindings = registry.CaptureBindings();
        var plan = Planner.Plan(registry, Owners(A), simulator, [A])!;
        Assert.Equal(1, Assert.Single(plan.Requests).CellIndex);
        Assert.Equal(FireSimMaterialHandoffMode.Fresh, plan.Requests[0].Mode);
        Apply(simulator, plan);
        Assert.Equal(beforeBindings.NextTargetId, registry.CaptureBindings().NextTargetId);
        Assert.Equal(beforeBindings.Entities[0].NextSlotId, registry.CaptureBindings().Entities[0].NextSlotId);
        Assert.Equal(0, PackedCell.Fuel(simulator.CaptureSnapshot().Cells[0]));
        simulator.RegisterChange(new(1, SetFuel: 0)); simulator.Tick();
        Assert.Null(Planner.Plan(registry, Owners(A), simulator, []));
        Assert.Equal(0, PackedCell.Fuel(simulator.CaptureSnapshot().Cells[1]));
    }

    [Fact]
    public void EvolvedUnownedFieldsDoNotCauseMaterialReset()
    {
        var definition = FireSimBaselineDefinition.OpenSoil;
        var registry = Registry(1, [new(0, definition)]);
        var companion = definition.CompanionMaterial | (9u << 12) | (15u << 16) | (6u << 25);
        var simulator = Sim(1, [PackedCell.Pack(7, 11, 0, 2, 0, 6)], [companion], [0], [0], []);
        Assert.Null(Planner.Plan(registry, Owners(), simulator, []));
        Assert.Equal(0, simulator.Uploads);
    }

    [Theory]
    [InlineData(0x1000u, 1u)] // Solid versus open soil.
    [InlineData(0x100u, 1u)] // Noncanonical flammability.
    [InlineData(0u, 0x101u)] // Capacity.
    [InlineData(0u, 0x100001u)] // Ash quality.
    [InlineData(0u, 0x400001u)] // Contamination behavior.
    public void StaticUnownedDefinitionDifferencesRequireBaseline(uint packed, uint companion)
    {
        var registry = Registry(1, [new(0, FireSimBaselineDefinition.OpenSoil)]);
        var simulator = Sim(1, [(ushort)packed], [companion], [0], [0], []);
        var plan = Planner.Plan(registry, Owners(), simulator, [])!;
        Assert.Equal(FireSimMaterialHandoffMode.Baseline, Assert.Single(plan.Requests).Mode);
    }

    [Fact]
    public void OverfullEarlierQueueStaysBeforeRelocationAndOriginSlotsStayExact()
    {
        var registry = Registry(2); registry.Reconcile([Projection(A, 0)], []);
        var simulator = Sim(2, [6, 0], [Tree, 0], [1, 0], [1, 0], [new(1, 1)]);
        registry.Reconcile([Projection(A, 1)], []);
        var pending = Enumerable.Range(0, 7).Select(value => new FireSimChange(0, SetFuel: (byte)(6 - value))).ToArray();
        foreach (var change in pending) simulator.RegisterChange(change);
        var plan = Planner.Plan(registry, Owners(A), simulator, [])!;
        var result = simulator.TryHandoffMaterial(plan, receipt => Assert.True(receipt.Accepted))!.Value;
        Assert.Equal(pending, simulator.AppliedChanges.Take(pending.Length));
        Assert.NotNull(simulator.AppliedChanges[^1].MaterialHandoff);
        Assert.All(result.Deltas, delta => { Assert.Equal(1u, delta.TargetId); Assert.Equal(1u, delta.SlotId); });
        Assert.Equal(0, PackedCell.Fuel(simulator.CaptureSnapshot().Cells[1]));
        Assert.Equal(1, simulator.Simulations);
    }

    [Fact]
    public void MissingCanonicalHiddenOwnerOrRetiredDesiredContributorRejectsWithoutUpload()
    {
        var registry = Registry(1); registry.Reconcile([Projection(A, 0), Projection(B, 0)], []);
        var simulator = Sim(1, [5], [Tree], [1], [1], [new(1, 1)]);
        Assert.Throws<ArgumentException>(() => Planner.Plan(registry, Owners(A), simulator, []));
        var owners = Owners(A, B); owners[B] = OwnedBodyRetention.RetiredNativeOwner;
        Assert.Throws<InvalidOperationException>(() => Planner.Plan(registry, owners, simulator, []));
        owners[B] = (OwnedBodyRetention)99;
        Assert.Throws<ArgumentException>(() => Planner.Plan(registry, owners, simulator, []));
        Assert.Equal(0, simulator.Uploads);
    }

    [Fact]
    public void DiagnosticBindingOnlyRestoreCannotDetachRetainedActiveMaterial()
    {
        var source = Registry(1); source.Reconcile([Projection(A, 0)], []);
        var diagnostic = Registry(1); diagnostic.RestoreBindings(source.CaptureBindings());
        var simulator = Sim(1, [5], [Tree], [1], [1], [new(1, 1)]);
        Assert.Throws<InvalidOperationException>(() => Planner.Plan(diagnostic, Owners(A), simulator, []));
        Assert.Equal(0, simulator.Uploads);
        Assert.Equal(new uint[] { 1 }, simulator.CaptureSnapshot().TargetIds);
    }

    [Fact]
    public void MissingWhollyHiddenRetainedProjectionIsNotImplicitSuspension()
    {
        var registry = Registry(1); registry.Reconcile([Projection(A, 0), Projection(B, 0)], []);
        var simulator = Sim(1, [5], [Tree], [1], [1], [new(1, 1)]);
        registry.Reconcile([], [B]); // Native desired input was omitted, but canonical retention still requires it.
        Assert.Throws<InvalidOperationException>(() => Planner.Plan(registry, Owners(A, B), simulator, []));
        Assert.Equal(0, simulator.Uploads);
        Assert.Equal(2, registry.CaptureBindings().Entities.Count);
    }

    [Fact]
    public void EqualCellCountDifferentGridShapeRejects()
    {
        var registry = new TimberbornNativeMaterialRegistry(new FireGrid(1, 2, 1), []);
        var simulator = Sim(2, [0, 0], [0, 0], [0, 0], [0, 0], []);
        Assert.Throws<ArgumentException>(() => Planner.Plan(registry, Owners(), simulator, []));
    }

    [Fact]
    public void ExactlyOneCompleteSnapshotIsCapturedAndLegacyCapabilityCannotInventFreshHistory()
    {
        var registry = Registry(1); registry.Reconcile([Projection(A, 0)], []);
        var simulator = Sim(1, [0], [0], [0], [0], []);
        var probe = new DesiredMaterialAuthorityProbe(simulator);
        Assert.Equal(FireSimMaterialHandoffMode.Fresh, Assert.Single(Planner.Plan(registry, Owners(A), probe, [A])!.Requests).Mode);
        Assert.Equal(1, probe.Captures);
        probe.SnapshotCapability = FireSimSnapshotCapability.LegacyMaterialHistoryUnavailable;
        Assert.Throws<NotSupportedException>(() => Planner.Plan(registry, Owners(A), probe, []));
        Assert.Equal(1, probe.Captures);
        Assert.Equal(0, simulator.Uploads);
    }

    [Fact]
    public void MissingOrChangedArchiveAuthorityRejectsBeforeFreshFallback()
    {
        var registry = Registry(1); registry.Reconcile([Projection(A, 0)], []);
        var simulator = Sim(1, [0], [0], [0], [0], [new(1, 1)], [new(new(1, 1), 1, 0, 0, Tree)]);
        var probe = new DesiredMaterialAuthorityProbe(simulator) { HideArchives = true };
        Assert.Throws<InvalidOperationException>(() => Planner.Plan(registry, Owners(A), probe, []));
        probe.HideArchives = false; probe.DenyKnown = true;
        Assert.Throws<InvalidOperationException>(() => Planner.Plan(registry, Owners(A), probe, []));
        probe.DenyKnown = false;
        var other = Sim(1, [0], [0], [0], [0], [new(1, 1)], [new(new(1, 1), 1, 0, 3, Tree)]);
        Assert.True(other.TryGetMaterialArchive(new(1, 1), out var different));
        probe.SubstituteArchive = different;
        Assert.Throws<InvalidOperationException>(() => Planner.Plan(registry, Owners(A), probe, []));
        Assert.Equal(0, simulator.Uploads);
    }

    [Fact]
    public void UnknownKnownSlotAndMissingMaterialHistoryAreRejectedAsCorruption()
    {
        var registry = Registry(1); registry.Reconcile([Projection(A, 0)], []);
        var simulator = Sim(1, [3], [Tree], [1], [1], [new(1, 1)]);
        var probe = new DesiredMaterialAuthorityProbe(simulator)
        {
            TransformSnapshot = snapshot => snapshot with { MaterialAuthority = new(0, [new(1, 1), new(1, 2)], []) }
        };
        Assert.Throws<ArgumentException>(() => Planner.Plan(registry, Owners(A), probe, []));
        probe.TransformSnapshot = snapshot => snapshot with { TargetIds = [2], MaterialAuthority = new(0, [new(2, 1)], []) };
        Assert.Throws<ArgumentException>(() => Planner.Plan(registry, Owners(A), probe, []));
        Assert.Equal(0, simulator.Uploads);
    }

    [Fact]
    public void HiddenUnknownSlotNeedsNoPermissionUntilAVisibleFreshRequestWouldBeMade()
    {
        var registry = Registry(3);
        registry.Reconcile([Projection(A, 1), Projection(B, 0, 1)], []);
        var simulator = Sim(3, [3, 5, 0], [Tree, Tree, 0], [2, 1, 0], [1, 1, 0], [new(1, 1), new(2, 1)]);
        Assert.Null(Planner.Plan(registry, Owners(A, B), simulator, []));
        Assert.False(simulator.IsSlotKnown(new(2, 2)));

        registry.Reconcile([Projection(B, 2, 1)], [A]);
        var owners = Owners(A, B);
        owners[A] = OwnedBodyRetention.RetiredNativeOwner;
        var before = simulator.CaptureSnapshot();
        Assert.Throws<NotSupportedException>(() => Planner.Plan(registry, owners, simulator, []));
        Assert.Equal(0, simulator.Uploads);
        Assert.Equal(before.Cells, simulator.CaptureSnapshot().Cells);
        Assert.False(simulator.IsSlotKnown(new(2, 2)));

        var plan = Planner.Plan(registry, owners, simulator, [B])!;
        Assert.Contains(plan.Requests, request => request.Mode == FireSimMaterialHandoffMode.Fresh && request.CellIndex == 1);
        Assert.Contains(plan.Requests, request => request.Mode == FireSimMaterialHandoffMode.CapturedSource && request.CellIndex == 2);
        Apply(simulator, plan);
        Assert.Equal(3, PackedCell.Fuel(simulator.CaptureSnapshot().Cells[2]));
    }

    [Fact]
    public void FreshEligibilityIsCopiedBeforeBackendCallbacksAndCannotBeInventedByThem()
    {
        var registry = Registry(1); registry.Reconcile([Projection(A, 0)], []);
        var simulator = Sim(1, [0], [0], [0], [0], []);
        var eligible = new List<Guid>();
        var probe = new DesiredMaterialAuthorityProbe(simulator)
        {
            TransformSnapshot = snapshot => { eligible.Add(A); return snapshot; }
        };
        Assert.Throws<NotSupportedException>(() => Planner.Plan(registry, Owners(A), probe, eligible));
        Assert.Equal(0, simulator.Uploads);
        probe.TransformSnapshot = snapshot => { eligible.Clear(); return snapshot; };
        Assert.Equal(FireSimMaterialHandoffMode.Fresh,
            Assert.Single(Planner.Plan(registry, Owners(A), probe, eligible)!.Requests).Mode);
        Assert.Empty(eligible);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("retired")]
    public void InvalidFreshEligibilityRejectsBeforeAnyBackendRead(string kind)
    {
        var registry = Registry(1); registry.Reconcile([Projection(A, 0)], []);
        var simulator = Sim(1, [3], [Tree], [1], [1], [new(1, 1)]);
        var probe = new DesiredMaterialAuthorityProbe(simulator);
        var owners = Owners(A);
        if (kind == "retired") owners[A] = OwnedBodyRetention.RetiredNativeOwner;
        Guid[] eligible = kind == "duplicate" ? [A, A] : kind == "unknown" ? [B] : [A];
        Assert.Throws<ArgumentException>(() => Planner.Plan(registry, owners, probe, eligible));
        Assert.Equal(0, probe.Captures);
    }

    private static void Apply(RetiredDetachmentSimulatorFixture simulator, FireSimMaterialHandoffBatch plan) =>
        Assert.NotNull(simulator.TryHandoffMaterial(plan, receipt => Assert.True(receipt.Accepted)));

    private static TimberbornNativeMaterialRegistry Registry(int cells,
        KeyValuePair<int, FireSimBaselineDefinition>[]? baseline = null) => new(new TimberbornMaterialBaseline(new(cells, 1, 1), baseline ?? []));
    private static Dictionary<Guid, OwnedBodyRetention> Owners(params Guid[] ids) => ids.ToDictionary(id => id, _ => OwnedBodyRetention.RetainedBody);
    private static TimberbornMaterialProjection Projection(Guid id, params int[] cells) => new(id,
        cells.Select((cell, slot) => new TimberbornMaterialFootprintSlot(new(slot, 0, 0), cell)).ToArray(), [TimberbornMaterialPart.Tree("Pine")]);
    private static RetiredDetachmentSimulatorFixture Sim(int cells, ushort[] packed, uint[] companions,
        uint[] targets, uint[] slots, FireSimMaterialIdentity[] known, FireSimMaterialArchiveSnapshot[]? archives = null) =>
        new(new(1, new(cells, 1, 1), 0, FireSimParameters.Default, 17, packed, new uint[cells], companions,
            targets, slots, new(archives?.Length > 0 ? 1u : 0u, known, archives ?? []), []));
}
