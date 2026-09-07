using Wildfire.Core;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedCropDeltaConsumerTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000022");
    private static readonly FireGrid Grid = new(3, 1, 1);

    [Fact]
    public void OldAndReplacementRowsShareCellButBodyAndEffectsKeepTheirOriginalOwners()
    {
        var f = new Fixture();
        var result = f.Consumer.Consume(1, [Delta(f.Token(A), 0, 15, 13), Delta(f.Token(A), 1, 15, 12), Delta(f.Token(B), 0, 15, 11)]);
        Assert.Equal(5, f.Damage.States[Key(A)].DamageTaken);
        Assert.Equal(4, f.Damage.States[Key(B)].DamageTaken);
        Assert.Equal(0, result.Damage.DuplicateCellSuppressedCount);
        Assert.Equal(new[] { A, B }, f.Api.Calls.Select(call => call.EntityId).Distinct());
        Assert.Equal(0, result.Crops.YieldLost); // Native partial-yield effect remains unavailable.
    }

    [Fact]
    public void HiddenLiveOriginUsesRetainedGuidEvenWhenReplacementOwnsCurrentCell()
    {
        var f = new Fixture();
        f.Registry.Reconcile([], [A]);
        f.Damage.SuspendTarget(Key(A));
        Assert.Equal(B, f.Registry.ResolveCell(0).Owner!.Value.EntityId);
        f.Consumer.Consume(2, [Delta(f.Token(A), 0, 15, 13)]);
        Assert.Equal(2, f.Damage.States[Key(A)].DamageTaken);
        Assert.Equal(0, f.Damage.States[Key(B)].DamageTaken);
        Assert.All(f.Api.Calls, call => Assert.Equal(A, call.EntityId));
    }

    [Fact]
    public void DeletedCropDoesNotSelectReplacementOrPoisonOrdinaryMissingState()
    {
        var f = new Fixture();
        f.Api.Live.Remove(A);
        f.Damage.RemoveTarget(Key(A));
        var result = f.Consumer.Consume(3, [Delta(f.Token(A), 0, 15, 0)]);
        Assert.Equal(1, result.NotLiveCount);
        Assert.Equal(0, result.Damage.TotalDamageApplied);
        Assert.Empty(f.Api.Calls);
        Assert.False(f.Guard.IsIndeterminate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UnknownOrUnmigratedTrailingRowRejectsWholeBatchBeforeDamage(bool unknown)
    {
        var f = new Fixture(registerB: false);
        uint trailing = unknown ? 999u : f.Token(B);
        Action consume = () => f.Consumer.Consume(4, [Delta(f.Token(A), 0, 15, 0), Delta(trailing, 0, 15, 0)]);
        if (unknown) Assert.Throws<InvalidOperationException>(consume);
        else Assert.Throws<NotSupportedException>(consume);
        Assert.All(f.Damage.States.Values, state => Assert.Equal(0, state.DamageTaken));
        Assert.Empty(f.Api.Calls);
        Assert.False(f.Guard.IsIndeterminate); // Whole-batch preflight precedes the mutation guard.
    }

    [Fact]
    public void ZeroOwnerDoesNotDamageCellOccupant()
    {
        var f = new Fixture();
        var result = f.Consumer.Consume(4, [Delta(0, 0, 15, 0)]);
        Assert.Equal(1, result.UnownedCount);
        Assert.Empty(f.Api.Calls);
        Assert.Equal(0, result.Damage.TotalDamageApplied);
    }

    [Fact]
    public void CanonicalCropRegistrationIgnoresLegacyQaAliasAndCannotApplyBodyTwice()
    {
        var f = new Fixture();
        var alias = new TimberbornBurnDamageTargetKey(TimberbornBurnDamageIdentity.ForEntity(A, NativeBurnTargetFamily.SelectedCrop));
        f.Damage.UpsertTarget(Grid, new(alias, "Crop.Carrot", [new(0, 0, 0)], 100));
        f.Consumer.RegisterCrop(A); // Idempotent canonical binding, never a second alias damage target.
        var result = f.Consumer.Consume(5, [Delta(f.Token(A), 0, 15, 13), Delta(f.Token(A), 1, 15, 12)]);
        Assert.Equal(5, f.Damage.States[Key(A)].DamageTaken);
        Assert.Equal(0, f.Damage.States[alias].DamageTaken);
        Assert.Equal(1, result.Damage.DamageAppliedTargetCount);
        Assert.All(f.Api.Calls, call => Assert.Equal(Key(A), call.TargetKey));
    }

    [Fact]
    public void PartialYieldLedgerAdvancesOnlyActualReceiptAmount()
    {
        var f = new Fixture();
        var requests = new List<int>();
        f.Api.Apply = call =>
        {
            if (call.Kind != TimberbornCropBurnConsequenceKind.ReduceYield) return new(TimberbornCropBurnConsequenceStatus.AlreadySatisfied);
            requests.Add(call.YieldLost);
            return new(TimberbornCropBurnConsequenceStatus.Applied, YieldLost: requests.Count == 1 ? 1 : 0);
        };
        f.Consumer.Consume(6, [Delta(f.Token(A), 0, 15, 9)]); // Request two yields at Carrot fuel three.
        f.Consumer.Consume(7, [Delta(f.Token(A), 0, 9, 8)]);
        Assert.Equal(new[] { 2, 1 }, requests); // A partial receipt doesn't advance to the full desired loss.
    }

    [Fact]
    public void FullBurnReportsActualYieldAndDeletionSeparatelyFromKillOrVisual()
    {
        var f = new Fixture(amount: 2);
        f.Api.Apply = _ => new(TimberbornCropBurnConsequenceStatus.Applied, YieldLost: 1, Deleted: true, DestroyedGoodCount: 2);
        var result = f.Consumer.Consume(8, [Delta(f.Token(A), 0, 15, 0)]);
        Assert.Equal(1, result.Crops.YieldLost);
        Assert.Equal(1, result.Crops.DeletedCropCount);
        Assert.Equal(2, result.Crops.DestroyedGoodCount);
        Assert.Equal(0, result.Crops.KilledCropCount);
        Assert.Equal(0, result.Crops.VisualStateUpdateCount);
        f.Api.Calls.Clear();
        f.Consumer.Consume(9, [Delta(f.Token(A), 0, 1, 0)]);
        Assert.Empty(f.Api.Calls);
    }

    [Fact]
    public void VisualOnlyTerminalCompletionDoesNotInventYieldLoss()
    {
        var f = new Fixture(amount: 1);
        f.Api.Apply = _ => new(TimberbornCropBurnConsequenceStatus.Applied, VisualStateUpdated: true);
        var result = f.Consumer.Consume(10, [Delta(f.Token(A), 0, 3, 0)]);
        Assert.Equal(0, result.Crops.YieldLost);
        Assert.Equal(1, result.Crops.VisualStateUpdateCount);
    }

    [Fact]
    public void UnavailableWholeBurnDoesNotLatchTerminalCompletion()
    {
        var f = new Fixture(amount: 1);
        f.Api.Apply = _ => new(TimberbornCropBurnConsequenceStatus.Unavailable);
        var first = f.Consumer.Consume(11, [Delta(f.Token(A), 0, 3, 0)]);
        f.Api.Apply = _ => new(TimberbornCropBurnConsequenceStatus.Applied, Deleted: true);
        var second = f.Consumer.Consume(12, [Delta(f.Token(A), 0, 1, 0)]);
        Assert.Equal(1, first.Crops.UnavailableConsequenceCount);
        Assert.Equal(1, second.Crops.DeletedCropCount);
        Assert.Equal(2, f.Api.Calls.Count);
    }

    [Fact]
    public void ActualCallbackFailurePoisonsSharedGuardAndPreventsReplay()
    {
        var f = new Fixture();
        var cause = new ApplicationException("native mutation callback");
        f.Api.Apply = _ => throw cause;
        Assert.Same(cause, Assert.Throws<ApplicationException>(() => f.Consumer.Consume(13, [Delta(f.Token(A), 0, 15, 13)])));
        Assert.True(f.Guard.IsIndeterminate);
        Assert.Equal(2, f.Damage.States[Key(A)].DamageTaken);
        Assert.Throws<InvalidOperationException>(() => f.Consumer.Consume(13, [Delta(f.Token(A), 0, 15, 13)]));
        Assert.Single(f.Api.Calls);
    }

    [Theory]
    [InlineData(-1, 0, TimberbornCropBurnConsequenceStatus.Applied)]
    [InlineData(0, -1, TimberbornCropBurnConsequenceStatus.Applied)]
    [InlineData(1, 0, TimberbornCropBurnConsequenceStatus.Unavailable)]
    [InlineData(0, 1, TimberbornCropBurnConsequenceStatus.AlreadySatisfied)]
    public void ContradictoryReceiptCannotAdvanceLedgerOrEscapeSharedGuard(int yield, int goods, TimberbornCropBurnConsequenceStatus status)
    {
        var f = new Fixture(amount: 1);
        f.Api.Apply = _ => new(status, YieldLost: yield, DestroyedGoodCount: goods);
        Assert.Throws<InvalidOperationException>(() => f.Consumer.Consume(14, [Delta(f.Token(A), 0, 3, 0)]));
        Assert.True(f.Guard.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(f.Guard.ThrowIfSaveUnsafe);
        Assert.Single(f.Api.Calls);
    }

    private static TimberbornBurnDamageTargetKey Key(Guid id) => new(TimberbornBurnDamageIdentity.ForEntity(id, NativeBurnTargetFamily.Crop));
    private static CellDelta Delta(uint id, int cell, int oldFuel, int newFuel) => new(cell,
        PackedCell.Pack(oldFuel, 10, 3, 0, 0, 1), PackedCell.Pack(newFuel, 10, 3, 0, 0, 1), id);
    private sealed class Fixture
    {
        internal readonly TimberbornNativeMaterialRegistry Registry = new(Grid, []);
        internal readonly TimberbornBurnDamageService Damage;
        internal readonly NativeResourceTransaction Guard = new();
        internal readonly Api Api = new();
        internal readonly TimberbornOwnedCropDeltaConsumer Consumer;
        internal Fixture(bool registerB = true, int amount = 10)
        {
            Damage = new(new TimberbornBurnDamageDescriptorCatalog([new("Crop.Carrot", TimberbornBurnDamageTargetKind.Crop,
                TimberbornBurnMaterialKind.Organic, resourceYields: [new("Carrot", amount)])]));
            Registry.Reconcile(new[] { A, B }.Select(id => new TimberbornMaterialProjection(id,
                [new(new(0, 0, 0), 0)], [TimberbornMaterialPart.Crop("Carrot")])), []);
            Damage.RegisterTargets(Grid, [new(Key(A), "Crop.Carrot", [new(0, 0, 0), new(1, 0, 0)], 20),
                new(Key(B), "Crop.Carrot", [new(0, 0, 0)], 10)]);
            Consumer = new(Registry, Damage, Api, Guard, registerB ? [A, B] : [A]);
        }
        internal uint Token(Guid id) => Registry.CaptureBindings().Entities.Single(item => item.EntityId == id).TargetId;
    }
    private sealed class Api : ITimberbornLiveCropBurnConsequenceApi
    {
        internal HashSet<Guid> Live = [A, B];
        internal List<TimberbornCropBurnConsequence> Calls = [];
        internal Func<TimberbornCropBurnConsequence, TimberbornCropBurnConsequenceResult> Apply = call =>
            new(call.Kind == TimberbornCropBurnConsequenceKind.ReduceYield ? TimberbornCropBurnConsequenceStatus.Unavailable : TimberbornCropBurnConsequenceStatus.Applied);
        public bool IsLive(Guid id) => Live.Contains(id);
        public TimberbornCropBurnConsequenceResult ApplyConsequence(TimberbornCropBurnConsequence call)
        { Calls.Add(call); return IsLive(call.EntityId) ? Apply(call) : new(TimberbornCropBurnConsequenceStatus.NotLive); }
    }
}
