using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class TimberbornBurnDamageRegistrationTests
{
    private static readonly FireGrid Grid = new(4, 1, 1);
    private static readonly TimberbornBurnDamageTargetKey First = new("structure:11111111-1111-1111-1111-111111111111");
    private static readonly TimberbornBurnDamageTargetKey Second = new("structure:22222222-2222-2222-2222-222222222222");
    private static readonly TimberbornBurnDamageTargetKey Replacement = new("structure:33333333-3333-3333-3333-333333333333");

    [Fact]
    public void UpdatingFootprintPreservesDamageAndUnrelatedStateWithoutReplayingOldEvents()
    {
        TimberbornBurnDamageService service = CreateService();
        service.RegisterTargets(Grid, [Target(First, [0, 1]), Target(Second, [3])]);
        service.ApplyDamage(12, [Hit(0), Hit(1), Hit(3)]);
        TimberbornBurnDamageTargetState unrelated = service.States[Second];
        TimberbornBurnDamageApplySummary previousSummary = service.LastApplySummary;

        service.UpsertTarget(Grid, Target(First, [1, 2]));

        Assert.False(service.TryGetStateForCell(0, out _));
        Assert.Equal(First, service.TargetKeyByCellIndex[2]);
        Assert.Equal(4, service.States[First].DamageTaken);
        Assert.Equal(12u, service.States[First].LastDamagedTick);
        Assert.Same(unrelated, service.States[Second]);
        Assert.False(service.TryGetAppliedEvent(First, out _));
        Assert.True(service.TryGetAppliedEvent(Second, out _));
        Assert.Equal(previousSummary, service.LastApplySummary);
        service.ApplyDamage(13, [Hit(1), Hit(2)]);
        Assert.Equal(8, service.States[First].DamageTaken); // Both distinct cells burn; the entity is updated once.
    }

    [Fact]
    public void SuspensionRevealsUnderlyingOwnerAndResumptionKeepsDamage()
    {
        TimberbornBurnDamageService service = CreateService();
        service.RegisterTargets(Grid, [Target(First, [0, 1], 20), Target(Second, [0], 10)]);
        service.ApplyDamage(7, [Hit(0)]);

        Assert.True(service.SuspendTarget(First));
        Assert.False(service.SuspendTarget(First));
        Assert.Equal(Second, service.TargetKeyByCellIndex[0]);
        Assert.False(service.TryGetStateForCell(1, out _));
        Assert.Equal(2, service.States[First].DamageTaken);
        Assert.Equal(2, service.CaptureState().Count);
        Assert.False(service.TryGetAppliedEvent(First, out _));
        service.ApplyDamage(8, [Hit(0), Hit(1)]);
        Assert.Equal(2, service.States[First].DamageTaken);
        Assert.Equal(2, service.States[Second].DamageTaken);

        service.UpsertTarget(Grid, Target(First, [0, 1], 20));
        Assert.Equal(First, service.TargetKeyByCellIndex[0]);
        Assert.Equal(2, service.States[First].DamageTaken);
        Assert.Equal(7u, service.States[First].LastDamagedTick);
    }

    [Fact]
    public void DeletingOwnerRestoresOverlapAndNewIdentityStartsFresh()
    {
        TimberbornBurnDamageService service = CreateService();
        service.RegisterTargets(Grid, [Target(First, [0], 20), Target(Second, [0], 10)]);
        service.ApplyDamage(7, [Hit(0)]);

        Assert.True(service.RemoveTarget(First));
        Assert.False(service.RemoveTarget(First));
        Assert.Equal(Second, service.TargetKeyByCellIndex[0]);
        Assert.False(service.TryGetState(First, out _));
        Assert.False(service.TryGetAppliedEvent(First, out _));
        Assert.DoesNotContain(service.CaptureState(), state => state.TargetKey == First);
        service.UpsertTarget(Grid, Target(Replacement, [0], 30));
        Assert.Equal(Replacement, service.TargetKeyByCellIndex[0]);
        Assert.Equal(0, service.States[Replacement].DamageTaken);
        Assert.Equal(0u, service.States[Replacement].LastDamagedTick);
    }

    [Fact]
    public void PriorityRefreshUsesInitialDeterministicTieRule()
    {
        TimberbornBurnDamageService service = CreateService();
        service.UpsertTarget(Grid, Target(Second, [0], 10));
        service.UpsertTarget(Grid, Target(First, [0], 10));
        Assert.Equal(First, service.TargetKeyByCellIndex[0]);
        service.UpsertTarget(Grid, Target(Second, [0], 20));
        Assert.Equal(Second, service.TargetKeyByCellIndex[0]);
        service.RemoveTarget(Second);
        Assert.Equal(First, service.TargetKeyByCellIndex[0]);
    }

    [Theory]
    [InlineData("spec")]
    [InlineData("capacity")]
    [InlineData("accounting")]
    public void ChangedProfileRejectsBeforeStateOwnershipOrEventMutation(string change)
    {
        TimberbornBurnDamageService service = CreateService();
        service.UpsertTarget(Grid, Target(First, [0]));
        service.ApplyDamage(5, [Hit(0)]);
        TimberbornBurnDamageTargetState previous = service.States[First];
        TimberbornBurnDamageAppliedEvent previousEvent = service.LastAppliedEventsByTargetKey[First];
        TimberbornBurnDamageDescriptor descriptor = Profile(
            spec: change == "spec" ? "Other" : "House",
            logs: change == "capacity" ? 3 : 2,
            water: change == "accounting" ? 8 : 7);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            service.UpsertTarget(Grid, Target(First, [2], descriptor: descriptor)));

        Assert.Contains("material/accounting profile changed", error.Message);
        Assert.Same(previous, service.States[First]);
        Assert.Same(previousEvent, service.LastAppliedEventsByTargetKey[First]);
        Assert.Equal(First, service.TargetKeyByCellIndex[0]);
        Assert.False(service.TryGetStateForCell(2, out _));
    }

    [Fact]
    public void FullyDamagedTargetStaysExhaustedAfterOwnershipRefresh()
    {
        TimberbornBurnDamageService service = CreateService();
        service.UpsertTarget(Grid, Target(First, [0]));
        for (uint tick = 1; tick <= 12; tick++) service.ApplyDamage(tick, [Hit(0)]);
        Assert.True(service.States[First].IsFullyDamaged);

        service.SuspendTarget(First);
        service.UpsertTarget(Grid, Target(First, [1]));
        Assert.Equal(0, service.ApplyDamage(13, [Hit(1)]).TotalDamageApplied);
        Assert.Equal(24, service.States[First].DamageTaken);
        Assert.Equal(12u, service.States[First].LastDamagedTick);
    }

    [Fact]
    public void OutOfGridAndDifferentGridRefreshLeaveOldRegistrationIntact()
    {
        TimberbornBurnDamageService service = CreateService();
        service.UpsertTarget(Grid, Target(First, [0]));
        service.ApplyDamage(1, [Hit(0)]);
        TimberbornBurnDamageTargetState original = service.States[First];

        Assert.Throws<ArgumentOutOfRangeException>(() => service.UpsertTarget(Grid, Target(First, [1, 4])));
        Assert.Throws<ArgumentException>(() => service.UpsertTarget(new FireGrid(5, 1, 1), Target(Second, [3])));

        Assert.Same(original, service.States[First]);
        Assert.Single(service.TargetKeyByCellIndex);
        Assert.True(service.TryGetAppliedEvent(First, out _));
    }

    [Fact]
    public void InvalidInitialResetCannotPartiallyReplaceExistingRegistryOrDescriptors()
    {
        TimberbornBurnDamageService service = CreateService();
        service.UpsertTarget(Grid, Target(First, [0]));
        service.ApplyDamage(1, [Hit(0)]);
        TimberbornBurnDamageTargetState original = service.States[First];

        Assert.Throws<ArgumentException>(() => service.RegisterTargets(Grid,
            [Target(Second, [1]), Target(Second, [2])], [Profile(logs: 3)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.RegisterTargets(Grid,
            [Target(Second, [1]), Target(Replacement, [4])], [Profile(logs: 3)]));

        Assert.Same(original, service.States[First]);
        Assert.Single(service.TargetKeyByCellIndex);
        service.UpsertTarget(Grid, Target(First, [0])); // Failed reset did not alter the dynamic profile catalog.
        Assert.Equal(2, service.States[First].DamageTaken);
    }

    [Fact]
    public void InitialResetRemainsExplicitFreshStateAndClearsHistoricalEvents()
    {
        TimberbornBurnDamageService service = CreateService();
        service.UpsertTarget(Grid, Target(First, [0, 0]));
        Assert.Equal(1, service.LastRegistrationSummary.DuplicateOwnedCellCount);
        service.ApplyDamage(1, [Hit(0)]);

        service.RegisterTargets(Grid, [Target(Second, [1])]);

        Assert.False(service.TryGetState(First, out _));
        Assert.Empty(service.LastAppliedEventsByTargetKey);
        Assert.Equal(TimberbornBurnDamageApplySummary.Empty, service.LastApplySummary);
        Assert.Equal(0, service.States[Second].DamageTaken);
        Assert.Equal(0, service.LastRegistrationSummary.DuplicateOwnedCellCount);
    }

    private static TimberbornBurnDamageService CreateService() => new(new TimberbornBurnDamageDescriptorCatalog([Profile()]));

    private static TimberbornBurnDamageDescriptor Profile(string spec = "House", int logs = 2, int water = 7) => new(
        spec, TimberbornBurnDamageTargetKind.Structure, TimberbornBurnMaterialKind.Constructed,
        constructionResources: [new("Log", logs), new("Water", water)]);

    private static TimberbornBurnDamageTargetRegistration Target(
        TimberbornBurnDamageTargetKey key, int[] cells, int priority = 0, TimberbornBurnDamageDescriptor? descriptor = null) => new(
        key, descriptor?.SpecId ?? "House", cells.Select(static x => new TimberbornCellCoordinates(x, 0, 0)).ToArray(),
        priority, descriptor);

    private static TimberbornFireCellDeltaDecision Hit(int cell) => TimberbornFireCellDeltaDecision.FromDelta(new CellDelta(
        cell, PackedCell.Pack(5, 10, 3, 0, 1, 1), PackedCell.Pack(3, 10, 3, 0, 1, 1)));
}
