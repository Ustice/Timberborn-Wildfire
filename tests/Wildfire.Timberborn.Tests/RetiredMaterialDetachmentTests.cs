using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Resources;
using P = Wildfire.Timberborn.Tests.RetiredMaterialDetachmentPrototype;

namespace Wildfire.Timberborn.Tests;

public sealed class RetiredMaterialDetachmentTests
{
    [Fact]
    public void RemovesEveryRetiredFootprintSlotAndRetainsExactGpuArchives()
    {
        var f = new Fixture("Empty"); var before = f.Simulator.CaptureSnapshot();
        var plan = f.Plan()!; Assert.Equal(new[] { 0, 1 }, plan.Requests.Select(request => request.CellIndex));
        Assert.All(plan.Requests, request => Assert.Equal(FireSimMaterialHandoffMode.Baseline, request.Mode));
        Assert.Equal(P.Result.Applied, f.Flush());
        Assert.Null(f.Plan()); f.RequireNormal();
        Assert.True(f.Simulator.TryGetMaterialArchive(f.A0, out var archive));
        Assert.Equal((uint)before.Cells[0], archive.PackedCell);
        Assert.Equal(before.CompanionFields[0], archive.Companion);
        Assert.True(f.Registry.TryResolveOrigin(f.A0.TargetId, out var id)); Assert.Equal(Fixture.A, id);
    }

    [Theory]
    [InlineData("Fresh", FireSimMaterialHandoffMode.Fresh)]
    [InlineData("Archived", FireSimMaterialHandoffMode.Archived)]
    public void RevealsLowerOwnerFromCorrectAuthority(string source, FireSimMaterialHandoffMode mode)
    {
        var f = new Fixture(source); Assert.Equal(mode, f.Plan()!.Requests[0].Mode);
        var incoming = f.B0;
        Assert.Equal(P.Result.Applied, f.Flush());
        var after = f.Simulator.CaptureSnapshot();
        Assert.Equal(incoming.TargetId, after.TargetIds[0]);
        if (source == "Archived")
        {
            Assert.Equal(3, PackedCell.Fuel(after.Cells[0])); // Restored receipt bytes, not declared fresh yield.
            Assert.Equal(Fixture.Companion, after.CompanionFields[0]);
            Assert.False(f.Simulator.TryGetMaterialArchive(incoming, out _));
        }
        else Assert.Equal(PackedCell.Fuel(f.Registry.ResolveCell(0).PackedDefinition), PackedCell.Fuel(after.Cells[0]));
    }

    [Fact]
    public void MovingKnownLowerSlotAddsItsSourceAndReplacementToWholeBatch()
    {
        var f = new Fixture("Captured"); var plan = f.Plan()!;
        Assert.Equal(new[] { 0, 1, 2 }, plan.Requests.Select(request => request.CellIndex));
        Assert.Equal(FireSimMaterialHandoffMode.CapturedSource, plan.Requests[0].Mode);
        Assert.Equal(2, plan.Requests[0].SourceCellIndex);
        Assert.Equal(FireSimMaterialHandoffMode.Fresh, plan.Requests[2].Mode);
        f.Flush(); var after = f.Simulator.CaptureSnapshot();
        Assert.Equal(f.B0.TargetId, after.TargetIds[0]); Assert.Equal(f.C0.TargetId, after.TargetIds[2]);
        Assert.Equal(3, PackedCell.Fuel(after.Cells[0])); Assert.Equal(Fixture.Companion, after.CompanionFields[0]);
        Assert.False(f.Simulator.TryGetMaterialArchive(f.B0, out _)); // Same slot moved, never cloned into archive.
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void FullAndOverfullOrdinaryQueueDetachesAfterEveryOldInputWithoutGhostDrainStep(int queued)
    {
        var f = new Fixture("Empty", changeCapacity: 1);
        var pending = Enumerable.Range(0, queued).Select(i => new FireSimChange(0, SetFuel: (byte)(6 - 2 * i))).ToArray();
        foreach (var change in pending) f.Simulator.RegisterChange(change);
        Assert.Throws<InvalidOperationException>(f.RequireNormal);
        int delivered = 0;
        Assert.Equal(P.Result.Applied, f.Flush(step =>
        {
            Assert.Throws<InvalidOperationException>(f.Guard.ThrowIfSaveUnsafe);
            Assert.Equal(queued, step.Deltas.Count);
            Assert.All(step.Deltas, delta => { Assert.Equal(f.A0.TargetId, delta.TargetId); Assert.Equal(f.A0.SlotId, delta.SlotId); });
            Assert.Equal(pending.Select(change => (int)change.SetFuel!.Value), step.Deltas.Select(delta => PackedCell.Fuel(delta.NewCell)));
            delivered++;
        }));
        Assert.Equal(pending, f.Simulator.AppliedChanges.Take(queued));
        Assert.NotNull(f.Simulator.AppliedChanges[^1].MaterialHandoff);
        Assert.Equal(queued + 1, f.Simulator.AppliedChanges.Length);
        Assert.Equal(0, f.Simulator.Coordinator.PendingChangeCount);
        Assert.Equal(1, f.Simulator.Uploads); Assert.Equal(1, f.Simulator.Simulations); Assert.Equal(1, delivered);
        Assert.Null(f.Plan()); Assert.False(f.Guard.IsIndeterminate); f.RequireNormal();
        Assert.True(f.Simulator.TryGetMaterialArchive(f.A0, out var archive));
        Assert.Equal(pending[^1].SetFuel!.Value, PackedCell.Fuel((ushort)archive.PackedCell));
    }

    [Fact]
    public void WholeFootprintCapacityCannotAdmitOnlyOneCell()
    {
        var f = new Fixture("Empty"); f.Simulator.MaterialHandoffCapacity = 1;
        Assert.Equal(P.Result.CapacityBlocked, f.Flush()); Assert.Equal(0, f.Simulator.Uploads);
        Assert.Equal(2, f.Plan()!.Requests.Count); Assert.Throws<InvalidOperationException>(f.RequireNormal);
    }

    [Fact]
    public void EarlierOldOwnerOutputsAreConsumedUnderGuardBeforeCompletion()
    {
        var f = new Fixture("Fresh"); f.Simulator.RegisterChange(new(0, SetFuel: 2));
        int consumed = 0;
        Assert.Equal(P.Result.Applied, f.Flush(step =>
        {
            Assert.Throws<InvalidOperationException>(f.Guard.ThrowIfSaveUnsafe);
            Assert.Throws<InvalidOperationException>(() => f.Guard.CaptureAtRest(f.Simulator.CaptureSnapshot));
            var delta = Assert.Single(step.Deltas); Assert.Equal(f.A0.TargetId, delta.TargetId);
            Assert.Equal(f.A0.SlotId, delta.SlotId);
            Assert.True(f.Registry.TryResolveOrigin(delta.TargetId, out var nativeId));
            Assert.Equal(Fixture.A, nativeId); Assert.NotEqual(Fixture.B, nativeId);
            consumed++;
        }));
        Assert.Equal(1, consumed); f.Guard.ThrowIfSaveUnsafe(); Assert.Null(f.Plan());
    }

    [Fact]
    public void RejectedBatchAfterSimulationPoisonsSaveWithoutPublishingNativeDelivery()
    {
        var f = new Fixture("Empty"); f.Simulator.Accepted = false;
        f.Simulator.RegisterChange(new(0, SetFuel: 2)); int consumed = 0;
        Assert.Throws<InvalidOperationException>(() => f.Flush(_ => consumed++));
        Assert.Equal(0, consumed); Assert.Equal(0, f.Simulator.Coordinator.PendingChangeCount);
        Assert.Equal(1, f.Simulator.Simulations); Assert.True(f.Guard.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(f.Guard.ThrowIfSaveUnsafe);
        Assert.Throws<InvalidOperationException>(f.RequireNormal);
        Assert.Throws<InvalidOperationException>(() => f.Flush());
    }

    [Fact]
    public void ProvenNoApplyFailureKeepsNativeSaveSafeButRequiresNewAttemptToken()
    {
        var f = new Fixture("Empty"); f.Simulator.FailAt = "Upload";
        var error = Assert.Throws<FireSimStepInputException>(() => f.Flush());
        Assert.Equal(FireSimStepInputOutcome.NotApplied, error.Outcome); Assert.False(f.Guard.IsIndeterminate);
        Assert.Equal(2u, f.Plan()!.Token); Assert.Throws<InvalidOperationException>(f.RequireNormal);
        f.Simulator.FailAt = null; Assert.Equal(P.Result.Applied, f.Flush());
    }

    [Theory]
    [InlineData("Apply")]
    [InlineData("Receipt")]
    [InlineData("Listener")]
    [InlineData("Consumer")]
    public void UncertainOrLostOutputDeliveryBlocksSaveAndNormalTicks(string stage)
    {
        var f = new Fixture("Empty"); f.Simulator.FailAt = stage;
        using var subscription = stage == "Listener" ? f.Simulator.Subscribe(new ThrowingListener()) : null;
        int consumed = 0;
        var error = Record.Exception(() => f.Flush(_ => { consumed++; if (stage == "Consumer") throw new ApplicationException("native effect failed"); }));
        Assert.NotNull(error); Assert.True(f.Guard.IsIndeterminate);
        Assert.Equal(stage == "Consumer" ? 1 : 0, consumed);
        if (stage == "Listener") Assert.Equal(FireSimStepInputOutcome.Committed, Assert.IsType<FireSimStepInputException>(error).Outcome);
        Assert.Throws<InvalidOperationException>(f.Guard.ThrowIfSaveUnsafe);
        Assert.Throws<InvalidOperationException>(f.RequireNormal);
        Assert.Throws<InvalidOperationException>(() => f.Flush());
    }

    [Fact]
    public void RetiredDesiredProjectionIsNotFreshened()
    {
        var f = new Fixture("Fresh"); f.Owners[Fixture.B] = OwnedBodyRetention.RetiredNativeOwner;
        Assert.Throws<InvalidOperationException>(() => f.Plan()); Assert.Equal(0, f.Simulator.Uploads);
    }

    private sealed class ThrowingListener : IFireSimListener
    { public void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas) => throw new ApplicationException("downstream listener failed"); }

    private sealed class Fixture
    {
        internal static readonly Guid A = new("00000000-0000-0000-0000-000000000001"), B = new("00000000-0000-0000-0000-000000000002"), C = new("00000000-0000-0000-0000-000000000003");
        internal static readonly uint Companion = new WildfireMaterialFieldState(WildfireMaterialClass.Tree, 7, 5, 0, WildfireAshQuality.None, WildfireContaminationBehavior.None).Pack();
        internal readonly TimberbornNativeMaterialRegistry Registry = new(new(4, 1, 1), []);
        internal readonly Dictionary<Guid, OwnedBodyRetention> Owners = new() { [A] = OwnedBodyRetention.RetiredNativeOwner, [B] = OwnedBodyRetention.RetainedBody, [C] = OwnedBodyRetention.RetainedBody };
        internal readonly NativeResourceTransaction Guard = new();
        internal readonly RetiredDetachmentSimulatorFixture Simulator;
        internal readonly FireSimMaterialIdentity A0, A1, B0, C0;
        internal Fixture(string reveal, int changeCapacity = 4)
        {
            Registry.Reconcile([Projection(A, 0, 1), Projection(B, 2), Projection(C, 3)], []);
            A0 = Identity(A, 1); A1 = Identity(A, 2); B0 = Identity(B, 1); C0 = Identity(C, 1);
            var targets = new[] { A0.TargetId, A1.TargetId, reveal == "Captured" ? B0.TargetId : 0u, 0u };
            var slots = new[] { A0.SlotId, A1.SlotId, reveal == "Captured" ? B0.SlotId : 0u, 0u };
            var known = new List<FireSimMaterialIdentity> { A0, A1 };
            if (reveal is "Captured" or "Archived") known.Add(B0);
            var archive = reveal == "Archived" ? new[] { new FireSimMaterialArchiveSnapshot(B0, 1, 2, 3, Companion) } : [];
            Simulator = new(new(1, new(4, 1, 1), 0, FireSimParameters.Default, 0, [6, 5, 3, 0], new uint[4],
                [Companion, Companion, Companion, 0], targets, slots, new(reveal == "Archived" ? 1u : 0u, known.ToArray(), archive), []), changeCapacity);
            var desired = reveal == "Empty" ? Array.Empty<TimberbornMaterialProjection>() :
                reveal == "Captured" ? new[] { Projection(B, 0), Projection(C, 2) } : new[] { Projection(B, 0) };
            Registry.Reconcile(desired, reveal == "Empty" ? [A, B, C] : reveal == "Captured" ? [A] : [A, C]);
        }
        private static TimberbornMaterialProjection Projection(Guid id, params int[] cells) => new(id,
            cells.Select((cell, local) => new TimberbornMaterialFootprintSlot(new(local, 0, 0), cell)).ToArray(),
            [TimberbornMaterialPart.Tree("Pine")]);
        private FireSimMaterialIdentity Identity(Guid id, uint slot) => new(Registry.CaptureBindings().Entities.Single(entry => entry.EntityId == id).TargetId, slot);
        internal FireSimMaterialHandoffBatch? Plan() => Guard.CaptureAtRest(() => P.Plan(Registry, Owners, Simulator.CaptureSnapshot(), Simulator));
        internal P.Result Flush(Action<GpuFireStepResult>? consume = null)
        {
            Guard.ThrowIfSaveUnsafe();
            return P.Flush(Guard, Simulator, Plan(), consume ?? (_ => Simulator.Delivered++));
        }
        internal void RequireNormal() => P.RequireNormalStepReady(Guard, Plan);
    }
}
