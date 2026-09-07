using Wildfire.Core;
using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedTreeDeltaConsumerTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly FireGrid Grid = new(3, 1, 1);

    [Fact]
    public void SameCellOldAndNewOriginsDamageTheirOwnTargetsAndDeduplicatePerOwner()
    {
        var fixture = new Fixture();
        var result = fixture.Consumer.Consume(7, [Delta(fixture.Token(A), 0, 15, 13),
            Delta(fixture.Token(A), 1, 15, 12), Delta(fixture.Token(B), 0, 15, 11)]);
        Assert.Equal(3, fixture.Damage.States[Key(A)].DamageTaken);
        Assert.Equal(4, fixture.Damage.States[Key(B)].DamageTaken);
        Assert.Equal(2, result.Damage.DamageAppliedTargetCount);
        Assert.Equal(1, result.Damage.DuplicateCellSuppressedCount);
        Assert.Equal(new[] { A, B }, fixture.Native.Calls.Select(call => call.EntityId).Distinct().ToArray());
    }

    [Fact]
    public void DelayedHiddenLiveOwnerDoesNotNeedCurrentCellOwnershipOrActiveFootprint()
    {
        var fixture = new Fixture();
        fixture.Damage.SuspendTarget(Key(A));
        fixture.Registry.Reconcile([], [A]); // Retain origin binding while B now wins the cell.
        Assert.Equal(B, fixture.Registry.ResolveCell(0).Owner!.Value.EntityId);
        Assert.Equal(Key(B), fixture.Damage.TargetKeyByCellIndex[0]);
        var result = fixture.Consumer.Consume(8, [Delta(fixture.Token(A), 0, 15, 13)]);
        Assert.Equal(2, fixture.Damage.States[Key(A)].DamageTaken);
        Assert.Equal(0, fixture.Damage.States[Key(B)].DamageTaken);
        Assert.Equal(0, result.NotLiveCount);
        Assert.All(fixture.Native.Calls, call => Assert.Equal(A, call.EntityId));
    }

    [Fact]
    public void DeletedOwnerNeverUsesReplacementEvenIfOldDamageRegistrationWasRemoved()
    {
        var fixture = new Fixture();
        fixture.Native.Live.Remove(A);
        fixture.Damage.RemoveTarget(Key(A));
        fixture.Registry.Reconcile([], [A]);
        var result = fixture.Consumer.Consume(9, [Delta(fixture.Token(A), 0, 15, 0)]);
        Assert.Equal(1, result.NotLiveCount);
        Assert.Equal(0, result.Damage.TotalDamageApplied);
        Assert.Empty(fixture.Native.Calls);
        Assert.Equal(0, fixture.Damage.States[Key(B)].DamageTaken);
    }

    [Fact]
    public void UnknownTrailingOriginFailsWholeBatchBeforeEarlierBodyOrNativeMutation()
    {
        var fixture = new Fixture();
        Assert.Throws<InvalidOperationException>(() => fixture.Consumer.Consume(10,
            [Delta(fixture.Token(A), 0, 15, 0), Delta(99, 0, 15, 0)]));
        Assert.All(fixture.Damage.States.Values, state => Assert.Equal(0, state.DamageTaken));
        Assert.Empty(fixture.Native.Calls);
    }

    [Fact]
    public void UnmigratedTrailingOwnerIsExplicitlyUnavailableWithoutLegacySinkFallback()
    {
        var fixture = new Fixture(registerB: false);
        Assert.Throws<NotSupportedException>(() => fixture.Consumer.Consume(10,
            [Delta(fixture.Token(A), 0, 15, 0), Delta(fixture.Token(B), 0, 15, 0)]));
        Assert.All(fixture.Damage.States.Values, state => Assert.Equal(0, state.DamageTaken));
        Assert.Empty(fixture.Native.Calls);
    }

    [Fact]
    public void ZeroOriginIsUnownedEvenOnRegisteredTreeCell()
    {
        var fixture = new Fixture();
        var result = fixture.Consumer.Consume(10, [Delta(0, 0, 15, 0)]);
        Assert.Equal(1, result.UnownedCount);
        Assert.Equal(0, result.Damage.TotalDamageApplied);
        Assert.Empty(fixture.Native.Calls);
    }

    [Fact]
    public void RestoredBindingsPreserveOldHiddenOriginRatherThanCurrentWinner()
    {
        var fixture = new Fixture();
        fixture.Registry.Reconcile([], [A]);
        var restored = new TimberbornNativeMaterialRegistry(Grid, []);
        restored.RestoreBindings(fixture.Registry.CaptureBindings());
        restored.Reconcile([Projection(B)], []);
        var consumer = new TimberbornOwnedTreeDeltaConsumer(restored, fixture.Damage, fixture.Native, [A, B]);
        consumer.Consume(11, [Delta(fixture.Token(A), 0, 15, 13)]);
        Assert.Equal(2, fixture.Damage.States[Key(A)].DamageTaken);
        Assert.Equal(0, fixture.Damage.States[Key(B)].DamageTaken);
    }

    [Fact]
    public void NewTreeCanBeRegisteredAfterConsumerConstructionWithoutAnInstanceCache()
    {
        var fixture = new Fixture(registerB: false);
        fixture.Consumer.RegisterTree(B);
        fixture.Consumer.Consume(12, [Delta(fixture.Token(B), 0, 15, 13)]);
        Assert.All(fixture.Native.Calls, call => Assert.Equal(B, call.EntityId));
        Assert.NotEmpty(fixture.Native.Calls);
    }

    [Fact]
    public void DeletionDuringFirstNativeActionMakesLaterActionsSkipWithoutFabricatedSuccess()
    {
        var fixture = new Fixture();
        fixture.Native.AfterApply = call => fixture.Native.Live.Remove(call.EntityId);
        var result = fixture.Consumer.Consume(13, [Delta(fixture.Token(A), 0, 15, 0)]);
        Assert.Single(fixture.Native.Applied);
        Assert.True(fixture.Native.Calls.Count > 1); // Separate calls recheck liveness, rather than reuse a batch grant.
        Assert.Equal(0, result.Trees.KilledTreeCount);
        Assert.Equal(0, result.Trees.VisualStateUpdateCount);
    }

    [Fact]
    public void RealCallbackFailurePropagatesWithoutRefundOrSubstitutingReplacement()
    {
        var fixture = new Fixture();
        var failure = new ApplicationException("native callback after mutation");
        fixture.Native.AfterApply = _ => throw failure;
        Assert.Same(failure, Assert.Throws<ApplicationException>(() => fixture.Consumer.Consume(14,
            [Delta(fixture.Token(A), 0, 15, 0)])));
        Assert.Equal(15, fixture.Damage.States[Key(A)].DamageTaken);
        Assert.Single(fixture.Native.Applied);
        Assert.Equal(0, fixture.Damage.States[Key(B)].DamageTaken);
    }

    [Fact]
    public void DefiniteFailedResultRemainsFatalAndDoesNotCallNextOwner()
    {
        var fixture = new Fixture();
        fixture.Native.Fail = true;
        Assert.Throws<InvalidOperationException>(() => fixture.Consumer.Consume(15,
            [Delta(fixture.Token(A), 0, 15, 0), Delta(fixture.Token(B), 0, 15, 0)]));
        Assert.Single(fixture.Native.Calls);
        Assert.Equal(A, fixture.Native.Calls[0].EntityId);
    }

    [Fact]
    public void FailedKillDoesNotProceedToVisualMutation()
    {
        var fixture = new Fixture();
        fixture.Native.FailKind = TimberbornTreeBurnConsequenceKind.KillTree;
        Assert.Throws<InvalidOperationException>(() => fixture.Consumer.Consume(15,
            [Delta(fixture.Token(A), 0, 15, 0)]));
        Assert.DoesNotContain(fixture.Native.Calls, call => call.Kind == TimberbornTreeBurnConsequenceKind.MarkBurnedVisual);
        Assert.Equal(TimberbornTreeBurnConsequenceKind.KillTree, fixture.Native.Calls.Last().Kind);
    }

    [Fact]
    public void ChangingRegistrationDuringNativeDeliveryIsRejected()
    {
        var fixture = new Fixture();
        fixture.Native.AfterApply = _ => fixture.Consumer.RegisterTree(B);
        Assert.Throws<InvalidOperationException>(() => fixture.Consumer.Consume(16,
            [Delta(fixture.Token(A), 0, 15, 13)]));
    }

    [Fact]
    public void NativeIdentityBridgeAcceptsOnlyExactFamilyGuidKeys()
    {
        Assert.True(TimberbornBurnDamageIdentity.TryGetEntity(Key(A).StableId, NativeBurnTargetFamily.Tree, out var id));
        Assert.Equal(A, id);
        foreach (string key in new[] { "tree_cuttable:1234", "tree_cuttable:entity:" + Guid.Empty,
            TimberbornBurnDamageIdentity.ForEntity(A, NativeBurnTargetFamily.Crop), Key(A).StableId + ":suffix" })
            Assert.False(TimberbornBurnDamageIdentity.TryGetEntity(key, NativeBurnTargetFamily.Tree, out _));
    }

    [Fact]
    public void UnavailableYieldNeverAdvancesGoodsLedgerOrUsesVisualCompletionAsReceipt()
    {
        var fixture = new Fixture();
        fixture.Native.StatusForKind = kind => kind == TimberbornTreeBurnConsequenceKind.ReduceYield
            ? TimberbornTreeBurnConsequenceStatus.Unavailable : TimberbornTreeBurnConsequenceStatus.AlreadySatisfied;
        var first = fixture.Consumer.Consume(20, [Delta(fixture.Token(A), 0, 15, 0)]);
        Assert.Equal(0, first.Trees.YieldLost);
        Assert.Equal(0, first.Trees.KilledTreeCount);
        Assert.Equal(0, first.Trees.VisualStateUpdateCount);
        Assert.Equal(1, first.Trees.UnavailableConsequenceCount);
        int requested = fixture.Native.Calls.First(call => call.Kind == TimberbornTreeBurnConsequenceKind.ReduceYield).YieldLost;
        fixture.Native.Calls.Clear();
        fixture.Consumer.Consume(21, [Delta(fixture.Token(A), 0, 15, 0)]);
        var retry = Assert.Single(fixture.Native.Calls);
        Assert.Equal(TimberbornTreeBurnConsequenceKind.ReduceYield, retry.Kind);
        Assert.True(retry.YieldLost >= requested); // Neither terminal visual nor unsupported yield advanced applied goods.
    }

    [Fact]
    public void AlreadyDeadSuppressesAnotherKillButOutstandingVisualCanRetry()
    {
        var fixture = new Fixture();
        fixture.Native.StatusForKind = kind => kind == TimberbornTreeBurnConsequenceKind.MarkBurnedVisual
            ? TimberbornTreeBurnConsequenceStatus.Unavailable : TimberbornTreeBurnConsequenceStatus.AlreadySatisfied;
        var first = fixture.Consumer.Consume(22, [Delta(fixture.Token(A), 0, 15, 9)]);
        Assert.Equal(0, first.Trees.KilledTreeCount);
        Assert.Equal(1, first.Trees.UnavailableConsequenceCount);
        fixture.Native.Calls.Clear();
        fixture.Native.StatusForKind = kind => kind == TimberbornTreeBurnConsequenceKind.MarkBurnedVisual
            ? TimberbornTreeBurnConsequenceStatus.Applied : TimberbornTreeBurnConsequenceStatus.AlreadySatisfied;
        var second = fixture.Consumer.Consume(23, [Delta(fixture.Token(A), 0, 9, 8)]);
        Assert.DoesNotContain(fixture.Native.Calls, call => call.Kind == TimberbornTreeBurnConsequenceKind.KillTree);
        Assert.Equal(0, second.Trees.KilledTreeCount);
        Assert.Equal(1, second.Trees.VisualStateUpdateCount);
    }

    private static TimberbornBurnDamageTargetKey Key(Guid id) => new(TimberbornBurnDamageIdentity.ForEntity(id, NativeBurnTargetFamily.Tree));
    private static TimberbornMaterialProjection Projection(Guid id) => new(id,
        [new(new(0, 0, 0), 0), new(new(1, 0, 0), 1)], [TimberbornMaterialPart.Tree("Pine")]);
    private static CellDelta Delta(uint id, int cell, int oldFuel, int newFuel) => new(cell,
        PackedCell.Pack(oldFuel, 10, 3, 0, 0, 1), PackedCell.Pack(newFuel, 10, 3, 0, 0, 1), id);

    private sealed class Fixture
    {
        internal readonly TimberbornNativeMaterialRegistry Registry = new(Grid, []);
        internal readonly TimberbornBurnDamageService Damage = new(new TimberbornBurnDamageDescriptorCatalog(
            [new("Tree.Pine", TimberbornBurnDamageTargetKind.Tree, TimberbornBurnMaterialKind.Wood,
                resourceYields: [new("Log", 10)])]));
        internal readonly RecordingNative Native = new();
        internal readonly TimberbornOwnedTreeDeltaConsumer Consumer;
        internal Fixture(bool registerB = true)
        {
            Registry.Reconcile([Projection(A), Projection(B)], []);
            Damage.RegisterTargets(Grid, [new(Key(A), "Tree.Pine", [new(0, 0, 0), new(1, 0, 0)], 20),
                new(Key(B), "Tree.Pine", [new(0, 0, 0)], 10)]);
            Consumer = new(Registry, Damage, Native, registerB ? [A, B] : [A]);
        }
        internal uint Token(Guid id) => Registry.CaptureBindings().Entities.Single(binding => binding.EntityId == id).TargetId;
    }

    private sealed class RecordingNative : ITimberbornLiveTreeBurnConsequenceApi
    {
        internal readonly HashSet<Guid> Live = [A, B];
        internal readonly List<TimberbornTreeBurnConsequence> Calls = [];
        internal readonly List<TimberbornTreeBurnConsequence> Applied = [];
        internal Action<TimberbornTreeBurnConsequence>? AfterApply;
        internal Func<TimberbornTreeBurnConsequenceKind, TimberbornTreeBurnConsequenceStatus>? StatusForKind;
        internal bool Fail;
        internal TimberbornTreeBurnConsequenceKind? FailKind;
        public bool IsLive(Guid id) => Live.Contains(id);
        public TimberbornTreeBurnConsequenceResult ApplyConsequence(TimberbornTreeBurnConsequence consequence)
        {
            Calls.Add(consequence);
            if (!IsLive(consequence.EntityId)) return new(TimberbornTreeBurnConsequenceStatus.NotLive);
            if (Fail || consequence.Kind == FailKind) return new(TimberbornTreeBurnConsequenceStatus.Failed);
            var status = StatusForKind?.Invoke(consequence.Kind) ?? TimberbornTreeBurnConsequenceStatus.Applied;
            if (status == TimberbornTreeBurnConsequenceStatus.Applied)
            {
                Applied.Add(consequence);
                AfterApply?.Invoke(consequence);
            }
            return new(status);
        }
    }
}
