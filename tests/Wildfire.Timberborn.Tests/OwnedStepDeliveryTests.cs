using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Persistence;
using F = Wildfire.Timberborn.Tests.OwnedConsequenceBatchTests.Fixture;
using P = Wildfire.Timberborn.Tests.RetiredMaterialDetachmentPrototype;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedStepDeliveryTests
{
    private static readonly Guid Tree = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Crop = new("00000000-0000-0000-0000-000000000002");

    [Fact]
    public void OneTransactionExcludesSaveAndReentryFromStepPreflightAndNativeCallbacks()
    {
        var f = new F(); int stepCalls = 0;
        void CheckExcluded()
        {
            Assert.Throws<InvalidOperationException>(f.Guard.ThrowIfSaveUnsafe);
            Assert.Throws<InvalidOperationException>(() => f.Consumer.CaptureHistory());
            Assert.Throws<InvalidOperationException>(() => f.Consumer.Consume(1, []));
            Assert.Throws<InvalidOperationException>(() => f.Consumer.ConsumeStep(() => null));
            Assert.Throws<InvalidOperationException>(() => f.Consumer.Register(f.Registrations[0]));
            Assert.Throws<InvalidOperationException>(() => f.Consumer.RetireNativeOwner(Tree));
        }
        f.Native.DuringIsLive = CheckExcluded;
        f.Native.AfterTree = CheckExcluded;
        var delivered = f.Consumer.ConsumeStep(() =>
        {
            stepCalls++; CheckExcluded();
            return new([f.Delta(Tree, 15)], 9);
        });
        Assert.Equal(1, stepCalls); Assert.Equal(9u, delivered!.Value.Step.Tick);
        Assert.Equal(1, delivered.Value.Consequences.Damage.DamageAppliedTargetCount);
        Assert.Equal(1, f.Native.DamagePasses);
        f.Native.DuringIsLive = null; f.Guard.ThrowIfSaveUnsafe();
        Assert.NotNull(f.Consumer.CaptureHistory());
    }

    [Fact]
    public void NullStepHasNoEffectsAndLeavesOrdinaryPublicDeliveryAvailable()
    {
        var f = new F();
        Assert.Null(f.Consumer.ConsumeStep(() => null));
        Assert.Equal(0, f.Native.DamagePasses); f.Guard.ThrowIfSaveUnsafe();
        var result = f.Consumer.Consume(2, [f.Delta(Crop, 2)]);
        Assert.Equal(1, result.Damage.DamageAppliedTargetCount); Assert.False(f.Guard.IsIndeterminate);
    }

    [Fact]
    public void UnknownTrailingOriginBeforePublicDeliveryIsSafeButAfterStepPoisons()
    {
        var f = new F(); CellDelta[] deltas = [f.Delta(Tree, 15), new(0, 0xffff, 0, 999, 1)];
        Assert.Throws<InvalidOperationException>(() => f.Consumer.Consume(2, deltas));
        Assert.False(f.Guard.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(() => f.Consumer.ConsumeStep(() => new(deltas, 2)));
        Assert.True(f.Guard.IsIndeterminate); Assert.Equal(0, f.Native.DamagePasses);
        Assert.Empty(f.Native.TreeCalls);
    }

    [Fact]
    public void TypedNoApplyFromStepReleasesGuardAndPreservesOriginalException()
    {
        var f = new F(); var failure = new FireSimStepInputException(FireSimStepInputOutcome.NotApplied, new Exception("upload"));
        Assert.Same(failure, Assert.Throws<FireSimStepInputException>(() => f.Consumer.ConsumeStep(() => throw failure)));
        f.Guard.ThrowIfSaveUnsafe(); Assert.Equal(0, f.Native.DamagePasses);
        Assert.NotNull(f.Consumer.ConsumeStep(() => new([f.Delta(Crop, 2)], 2)));
    }

    [Theory]
    [InlineData(FireSimStepInputOutcome.Indeterminate)]
    [InlineData(FireSimStepInputOutcome.Committed)]
    public void UncertainStepOrCommittedListenerFailurePoisonsWithoutEffects(FireSimStepInputOutcome outcome)
    {
        var f = new F(); var failure = new FireSimStepInputException(outcome, new Exception("native step"));
        Assert.Same(failure, Assert.Throws<FireSimStepInputException>(() => f.Consumer.ConsumeStep(() => throw failure)));
        Assert.True(f.Guard.IsIndeterminate); Assert.Equal(0, f.Native.DamagePasses);
        Assert.Throws<InvalidOperationException>(() => f.Consumer.ConsumeStep(() => null));
    }

    [Fact]
    public void ConsequenceThrowingTypedNoApplyCannotEvadePoisonAfterMutation()
    {
        var f = new F(); var failure = new FireSimStepInputException(FireSimStepInputOutcome.NotApplied, new Exception("native callback"));
        f.Native.AfterTree = () => throw failure;
        Assert.Same(failure, Assert.Throws<FireSimStepInputException>(() => f.Consumer.ConsumeStep(() => new([f.Delta(Tree, 15)], 1))));
        Assert.Equal(1, f.Native.TreeMutations); Assert.True(f.Guard.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(f.Guard.ThrowIfSaveUnsafe);
    }

    [Fact]
    public void ExistingOuterGuardCannotNestConsumerStepOrPublicConsume()
    {
        var f = new F(); int invoked = 0;
        f.Guard.TransferInventory(() =>
        {
            Assert.Throws<InvalidOperationException>(() => f.Consumer.ConsumeStep(() => { invoked++; return null; }));
            Assert.Throws<InvalidOperationException>(() => f.Consumer.Consume(1, []));
        });
        Assert.Equal(0, invoked); Assert.False(f.Guard.IsIndeterminate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ActualDetachmentStepUsesRealConsumerAndPreservesOldAndLowerOrigins(bool accepted)
    {
        var f = new F(); var bindings = f.Registry.CaptureBindings();
        uint targetA = bindings.Entities.Single(owner => owner.EntityId == Tree).TargetId;
        uint targetB = bindings.Entities.Single(owner => owner.EntityId == Crop).TargetId;
        var a = new FireSimMaterialIdentity(targetA, 1); var b = new FireSimMaterialIdentity(targetB, 1);
        var simulator = new RetiredDetachmentSimulatorFixture(new(1, new(4, 2, 1), 0, FireSimParameters.Default, 0,
            [6, 5, 0, 0, 0, 0, 0, 0], new uint[8], new uint[8], [targetA, targetB, 0, 0, 0, 0, 0, 0],
            [1, 1, 0, 0, 0, 0, 0, 0], new(0, [a, b], []), []));
        f.Native.Live.Remove(Tree); f.Consumer.RetireNativeOwner(Tree);
        f.Registry.Reconcile([new(Crop, [new(new(0, 0, 0), 0), new(new(1, 0, 0), 5)], [TimberbornMaterialPart.Crop("Carrot")])], []);
        var owners = f.Consumer.CaptureHistory().Owners.ToDictionary(owner => owner.EntityId, owner => owner.Retention);
        var batch = f.Guard.CaptureAtRest(() => P.Plan(f.Registry, owners, simulator.CaptureSnapshot(), simulator))!;
        simulator.RegisterChange(new(0, SetFuel: 2)); // Retired A output must not damage replacement B.
        simulator.RegisterChange(new(1, SetFuel: 3)); // Still-live B output must apply once after its slot moves.
        simulator.Accepted = accepted;
        int listenerCalls = 0;
        using var listener = simulator.Subscribe(new CallbackListener(() =>
        {
            listenerCalls++;
            Assert.Throws<InvalidOperationException>(() => f.Consumer.Consume(1, []));
            Assert.Throws<InvalidOperationException>(() => f.Consumer.Register(f.Registrations[0]));
            Assert.Throws<InvalidOperationException>(() => f.Consumer.RetireNativeOwner(Tree));
            Assert.Throws<InvalidOperationException>(() => f.Consumer.CaptureHistory());
        }));
        TimberbornOwnedStepDelivery? Run() => f.Consumer.ConsumeStep(() =>
        {
            FireSimMaterialHandoffReceipt? receipt = null;
            var step = simulator.TryHandoffMaterial(batch, value => receipt = value);
            if (step is not null && receipt?.Accepted != true) throw new InvalidOperationException("Material reconciliation failed.");
            return step;
        });
        if (!accepted)
        {
            Assert.Throws<InvalidOperationException>(() => Run());
            Assert.True(f.Guard.IsIndeterminate); Assert.Equal(0, f.Native.DamagePasses);
            Assert.Throws<InvalidOperationException>(() => Run());
            return;
        }
        var delivered = Run()!.Value;
        Assert.Equal(1, listenerCalls); Assert.Equal(2, delivered.Step.Deltas.Count);
        Assert.Equal(1, delivered.Consequences.NotLiveOwners);
        Assert.Equal(1, delivered.Consequences.Damage.DamageAppliedTargetCount);
        Assert.Equal(2, f.Damage.States[new(TimberbornBurnDamageIdentity.ForEntity(Crop, NativeBurnTargetFamily.Crop))].DamageTaken);
        Assert.Empty(f.Native.TreeCalls); Assert.Equal(1, f.Native.DamagePasses);
        Assert.True(simulator.TryGetMaterialArchive(a, out _));
        Assert.Equal(targetB, simulator.CaptureSnapshot().TargetIds[0]);
        f.Guard.ThrowIfSaveUnsafe();
    }

    private sealed class CallbackListener(Action callback) : IFireSimListener
    { public void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas) => callback(); }
}
