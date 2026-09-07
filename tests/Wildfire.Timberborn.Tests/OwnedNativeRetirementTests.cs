using Wildfire.Core;
using F = Wildfire.Timberborn.Tests.OwnedConsequenceBatchTests.Fixture;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedNativeRetirementTests
{
    [Fact]
    public void SettledRetirementKeepsOriginReceiptsAndCreditsButDeliberatelyRemovesOnlyItsBody()
    {
        var f = new F(); var tree = f.Registrations[0]; var stock = f.Registrations[2];
        f.Native.TreeYieldReceipt = 1;
        f.Consumer.Consume(1, [f.Delta(tree.EntityId, 15), f.Delta(stock.EntityId, 1)]);
        var before = f.Consumer.CaptureHistory(); var bindings = f.Registry.CaptureBindings();
        f.Native.Live.Remove(tree.EntityId); f.Native.Live.Remove(stock.EntityId);
        Assert.True(f.Consumer.RetireNativeOwner(tree.EntityId));
        Assert.True(f.Consumer.RetireNativeOwner(stock.EntityId));
        Assert.False(f.Consumer.RetireNativeOwner(tree.EntityId));
        var after = f.Consumer.CaptureHistory();
        Assert.Equal(before.Natural.Select(NaturalValues), after.Natural.Select(NaturalValues));
        Assert.Equal(before.StorageCredits.Select(value => (value.EntityId, value.ResourceId, value.FractionalBudget, value.FuelValue)),
            after.StorageCredits.Select(value => (value.EntityId, value.ResourceId, value.FractionalBudget, value.FuelValue)));
        Assert.Equal(2, after.Owners.Count(owner => owner.Retention == OwnedBodyRetention.RetiredNativeOwner));
        Assert.DoesNotContain(Key(tree), f.Damage.States.Keys);
        Assert.Equal(2, f.Damage.States.Count);
        Assert.Null(f.Registry.ResolveCell(0).Owner); Assert.Null(f.Registry.ResolveCell(4).Owner);
        var old = bindings.Entities.Single(owner => owner.EntityId == tree.EntityId);
        Assert.True(f.Registry.TryResolveOrigin(old.TargetId, out var retained)); Assert.Equal(tree.EntityId, retained);
        Assert.All(old.Slots, slot => Assert.True(f.Registry.IsSlotBound(old.TargetId, slot.SlotId)));
        Assert.Equal(bindings.NextTargetId, f.Registry.CaptureBindings().NextTargetId);
        f.Native.TreeCalls.Clear();
        Assert.Equal(1, f.Consumer.Consume(2, [f.Delta(tree.EntityId, 1)]).NotLiveOwners);
        Assert.Empty(f.Native.TreeCalls); // GPU material was not touched or synthesized by retirement.
    }

    [Theory]
    [InlineData(TimberbornOwnedBodyPresence.Live)]
    [InlineData(TimberbornOwnedBodyPresence.Uninitialized)]
    [InlineData(TimberbornOwnedBodyPresence.Deleted)]
    [InlineData(TimberbornOwnedBodyPresence.InvalidNativeReference)]
    [InlineData((TimberbornOwnedBodyPresence)99)]
    public void AnythingButExactAbsenceRejectsBeforePublicationWithoutPoison(TimberbornOwnedBodyPresence presence)
    {
        var f = new F(); var owner = f.Registrations[0];
        f.Native.PresenceOverrides[owner.EntityId] = presence;
        Assert.Throws<InvalidOperationException>(() => f.Consumer.RetireNativeOwner(owner.EntityId));
        Assert.True(f.Damage.States.ContainsKey(Key(owner)));
        Assert.Equal(owner.EntityId, f.Registry.ResolveCell(0).Owner!.Value.EntityId);
        Assert.False(f.Guard.IsIndeterminate);
    }

    [Fact]
    public void MissingBodyDoesNotSilentlyCreateRetirementAndUnknownOwnerCannotRetire()
    {
        var f = new F(); var owner = f.Registrations[0];
        f.Native.Live.Remove(owner.EntityId); f.Damage.RemoveTarget(Key(owner));
        Assert.Throws<InvalidOperationException>(() => f.Consumer.CaptureHistory());
        Assert.Throws<InvalidOperationException>(() => f.Consumer.RetireNativeOwner(owner.EntityId));
        Assert.Throws<ArgumentException>(() => f.Consumer.RetireNativeOwner(Guid.NewGuid()));
        Assert.False(f.Guard.IsIndeterminate);
    }

    [Fact]
    public void RetiredOwnerCannotBeResurrectedEvenAfterDirectBodyUpsert()
    {
        var f = new F(); var owner = f.Registrations[0];
        f.Native.Live.Remove(owner.EntityId); f.Consumer.RetireNativeOwner(owner.EntityId);
        f.Damage.UpsertTarget(new(4, 2, 1), new(Key(owner), "Pine", [new(0, 0, 0)], 10));
        f.Native.Live.Add(owner.EntityId);
        Assert.Throws<ArgumentException>(() => f.Consumer.Register(owner));
        Assert.Throws<InvalidOperationException>(() => f.Consumer.CaptureHistory());
        Assert.Throws<InvalidOperationException>(() => f.Consumer.Consume(2, [f.Delta(owner.EntityId, 1)]));
        Assert.Empty(f.Native.TreeCalls); Assert.False(f.Guard.IsIndeterminate);
    }

    [Fact]
    public void RetirementCannotNestInsideConsequenceDeliveryOrMutateDuringPresenceRead()
    {
        var f = new F(); var tree = f.Registrations[0]; var stock = f.Registrations[2];
        f.Native.Live.Remove(stock.EntityId);
        f.Native.AfterTree = () => Assert.Throws<InvalidOperationException>(() => f.Consumer.RetireNativeOwner(stock.EntityId));
        f.Consumer.Consume(1, [f.Delta(tree.EntityId, 15)]);
        f.Native.DuringIsLive = () => Assert.Throws<InvalidOperationException>(() => f.Consumer.Register(tree));
        Assert.True(f.Consumer.RetireNativeOwner(stock.EntityId));
        Assert.False(f.Guard.IsIndeterminate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PostPublicationFailureOrNativeReappearancePoisonsWithoutReplay(bool reappears)
    {
        var f = new F(); var owner = f.Registrations[0];
        var cause = new ApplicationException("registration observer failed after body removal");
        f.Native.Live.Remove(owner.EntityId);
        f.Native.DuringRegistration = () =>
        {
            Assert.Throws<InvalidOperationException>(f.Guard.ThrowIfSaveUnsafe);
            if (reappears) f.Native.Live.Add(owner.EntityId); else throw cause;
        };
        if (reappears) Assert.Throws<InvalidOperationException>(() => f.Consumer.RetireNativeOwner(owner.EntityId));
        else Assert.Same(cause, Assert.Throws<ApplicationException>(() => f.Consumer.RetireNativeOwner(owner.EntityId)));
        Assert.False(f.Damage.States.ContainsKey(Key(owner))); Assert.Null(f.Registry.ResolveCell(0).Owner);
        Assert.True(f.Guard.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(() => f.Consumer.RetireNativeOwner(owner.EntityId));
        Assert.Throws<InvalidOperationException>(() => f.Consumer.CaptureHistory());
    }

    private static object NaturalValues(OwnedNaturalProgress value) => (value.EntityId, value.AppliedYieldLoss,
        value.DryRequestSatisfied, value.DeathRequestSatisfied, value.LeftoverRequestSatisfied, value.DesiredPresentation);

    private static TimberbornBurnDamageTargetKey Key(TimberbornOwnedBodyRegistration owner) =>
        new(TimberbornBurnDamageIdentity.ForEntity(owner.EntityId, owner.Family));
}
