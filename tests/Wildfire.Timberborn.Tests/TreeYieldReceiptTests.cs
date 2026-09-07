using Wildfire.Core;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

public sealed class TreeYieldReceiptTests
{
    [Fact]
    public void AppliedPartialReceiptAdvancesOnlyActualLossAndRequestsRemainingDeficit()
    {
        var f = new Fixture();
        int calls = 0;
        var requests = new List<int>();
        f.Api.Apply = consequence =>
        {
            if (consequence.Kind != TimberbornTreeBurnConsequenceKind.ReduceYield)
                return new(TimberbornTreeBurnConsequenceStatus.AlreadySatisfied);
            requests.Add(consequence.YieldLost);
            return new(TimberbornTreeBurnConsequenceStatus.Applied, calls++ == 0 ? 0 : 1);
        };
        var first = f.Tick(1, 15, 1);
        var second = f.Tick(2, 15, 1);
        var third = f.Tick(3, 15, 14);
        Assert.Equal(new[] { 1, 2, 1 }, requests);
        Assert.Equal(0, first.YieldLost); Assert.Equal(1, second.YieldLost); Assert.Equal(1, third.YieldLost);
    }

    [Fact]
    public void TerminalAndVisualSuccessCannotFabricateAnyYieldReceipt()
    {
        var f = new Fixture();
        f.Api.Apply = consequence => new(consequence.Kind == TimberbornTreeBurnConsequenceKind.ReduceYield
            ? TimberbornTreeBurnConsequenceStatus.Unavailable : TimberbornTreeBurnConsequenceStatus.Applied);
        var result = f.Tick(1, 15, 0);
        Assert.Equal(0, result.YieldLost);
        Assert.True(result.VisualStateUpdateCount > 0);
    }

    [Theory]
    [InlineData(-1, TimberbornTreeBurnConsequenceStatus.Applied)]
    [InlineData(2, TimberbornTreeBurnConsequenceStatus.Applied)]
    [InlineData(1, TimberbornTreeBurnConsequenceStatus.Unavailable)]
    [InlineData(0, (TimberbornTreeBurnConsequenceStatus)999)]
    public void InvalidReceiptFailsWithinSharedResourceGuard(int loss, TimberbornTreeBurnConsequenceStatus status)
    {
        var f = new Fixture();
        var guard = new NativeResourceTransaction();
        f.Api.Apply = _ => new(status, loss);
        Assert.Throws<InvalidOperationException>(() => guard.TransferInventory(() => f.Tick(1, 15, 1)));
        Assert.True(guard.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(guard.ThrowIfSaveUnsafe);
    }

    private sealed class Fixture
    {
        internal readonly Api Api = new();
        private readonly TimberbornBurnDamageService _damage;
        private readonly TimberbornTreeBurnConsequenceSink _sink;
        internal Fixture()
        {
            _damage = new(new TimberbornBurnDamageDescriptorCatalog([new("Tree.Pine", TimberbornBurnDamageTargetKind.Tree,
                TimberbornBurnMaterialKind.Organic, resourceYields: [new("Log", 10)])]));
            _damage.RegisterTargets(new(1, 1, 1), [new(new("tree"), "Tree.Pine", [new(0, 0, 0)])]);
            _sink = new(_damage, Api);
        }
        internal TimberbornTreeBurnConsequenceSummary Tick(uint tick, int before, int after)
        {
            var decision = TimberbornFireCellDeltaDecision.FromDelta(new(0, PackedCell.Pack(before, 10, 3, 0, 1, 1),
                PackedCell.Pack(after, 10, 3, 0, 1, 1)));
            _damage.ApplyDamage(tick, [decision]);
            return _sink.ApplyConsequences(tick, [decision]);
        }
    }
    private sealed class Api : ITimberbornTreeBurnConsequenceApi
    {
        internal Func<TimberbornTreeBurnConsequence, TimberbornTreeBurnConsequenceResult> Apply = _ => default;
        public TimberbornTreeBurnConsequenceResult ApplyConsequence(TimberbornTreeBurnConsequence consequence) => Apply(consequence);
    }
}
