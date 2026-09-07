using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using F = Wildfire.Timberborn.Tests.OwnedConsequenceBatchTests.Fixture;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedTransitionNormalizationTests
{
    [Fact]
    public void FullPackedChainKeepsHeatOnlyRowsAndSumsEveryLoss()
    {
        var f = new F(); var id = f.Registrations[0].EntityId;
        var first = Row(f, id, 9, 6);
        var heat = first with { OldCell = first.NewCell, NewCell = Cell(6, 2) };
        var last = heat with { OldCell = heat.NewCell, NewCell = Cell(4, 3) };
        var result = f.Consumer.Consume(1, [first, heat, last]);
        Assert.Equal(5, result.Damage.TotalDamageApplied);
        Assert.Equal(1, result.Damage.PersistenceWriteCount);
        Assert.Equal(0, result.Damage.DuplicateCellSuppressedCount);
    }

    [Fact]
    public void ChainContinuityPrecedesReplayMembershipAfterRealGain()
    {
        var f = new F(); var id = f.Registrations[0].EntityId;
        var loss = Row(f, id, 9, 6);
        var gain = loss with { OldCell = loss.NewCell, NewCell = loss.OldCell };
        var result = f.Consumer.Consume(1, [loss, gain, loss]);
        Assert.Equal(6, result.Damage.TotalDamageApplied);
        Assert.Equal(0, result.Damage.DuplicateCellSuppressedCount);
    }

    [Fact]
    public void ProvenReplayDoesNotAdvanceChainOrEarnBodyDamage()
    {
        var f = new F(); var id = f.Registrations[0].EntityId;
        var first = Row(f, id, 9, 6);
        var result = f.Consumer.Consume(1, [first, first, Row(f, id, 6, 4)]);
        Assert.Equal(5, result.Damage.TotalDamageApplied);
        Assert.Equal(1, result.Damage.DuplicateCellSuppressedCount);
        Assert.Equal(2, result.Damage.ResolvedTargetCellCount);
    }

    [Fact]
    public void SameOwnerSlotReplacementAndRelocationStartSeparatePhysicalChains()
    {
        var f = new F(); var id = f.Registrations[0].EntityId;
        var old = Row(f, id, 9, 6);
        var replacement = Row(f, id, 5, 3) with { SlotId = 2 };
        var relocated = Row(f, id, 3, 2) with { CellIndex = 4, OldCell = Cell(3, 1), NewCell = Cell(2, 2) };
        var result = f.Consumer.Consume(1, [old, replacement, relocated]);
        Assert.Equal(6, result.Damage.TotalDamageApplied);
        Assert.Equal(0, result.Damage.DuplicateCellSuppressedCount);
    }

    [Theory]
    [InlineData(0)] // Missing slot is explicit unsupported legacy provenance.
    [InlineData(99)] // Unknown slot is never inferred from a current occupant.
    [InlineData(-1)] // Unexplained full packed discontinuity.
    [InlineData(-2)] // Partial target/slot identity.
    [InlineData(-3)] // Invalid physical source cannot locate hazards.
    public void TrailingInvalidInputRejectsEntireMixedBatchBeforeAnyEffects(int kind)
    {
        var f = new F(); var tree = f.Registrations[0].EntityId; var stock = f.Registrations[2].EntityId;
        var first = Row(f, stock, 9, 6);
        var bad = kind switch
        {
            -1 => Row(f, stock, 5, 2),
            -2 => first with { TargetId = 0 },
            -3 => first with { CellIndex = 8 },
            _ => first with { SlotId = (uint)kind },
        };
        Assert.ThrowsAny<Exception>(() => f.Consumer.Consume(1, [Row(f, tree, 9, 6), first, bad]));
        Assert.All(f.Damage.States.Values, state => Assert.Equal(0, state.DamageTaken));
        Assert.Empty(f.Native.TreeCalls); Assert.Empty(f.Native.InventoryCalls);
        Assert.False(f.Guard.IsIndeterminate);
    }

    [Fact]
    public void ThreeSlotsHaveSameFrozenBodyDamageTogetherAndApartThenClamp()
    {
        var together = new F(); var apart = new F(); var id = together.Registrations[0].EntityId;
        foreach (var f in new[] { together, apart })
            f.Registry.Reconcile([new(id, [new(new(0,0,0),0), new(new(1,0,0),4), new(new(2,0,0),7)],
                [TimberbornMaterialPart.Tree("Pine")])], []);
        var rows = new[] { Row(together,id,9,1), Row(together,id,9,1) with { CellIndex=4, SlotId=2 },
            Row(together,id,9,1) with { CellIndex=7, SlotId=3 } };
        var result = together.Consumer.Consume(1,rows);
        int separate = 0;
        for (int i=0;i<rows.Length;i++) separate += apart.Consumer.Consume((uint)i+1,[rows[i]]).Damage.TotalDamageApplied;
        Assert.Equal(24,result.Damage.TotalDamageApplied); Assert.Equal(separate,result.Damage.TotalDamageApplied);
        Assert.Equal(1,result.Damage.PersistenceWriteCount);
        Assert.Equal(0,result.Damage.DuplicateCellSuppressedCount);
        // Pine's initial body has finite capacity; repeated valid future loss never overdraws it.
        for(uint tick=2;tick<=7;tick++) together.Consumer.Consume(tick,rows);
        var state=together.Damage.States.Values.Single(s=>s.TargetKey.StableId.Contains(id.ToString("D")));
        Assert.Equal(state.DamageCapacity,state.DamageTaken);
    }

    [Fact]
    public void RemovedAndRestoredBindingRetainsExactSlotsButRejectsNewPairs()
    {
        var f=new F(); var id=f.Registrations[0].EntityId; var row=Row(f,id,9,6);
        f.Registry.Reconcile([], [id]);
        var copy=new TimberbornNativeMaterialRegistry(new(4,2,1),[]); copy.RestoreBindings(f.Registry.CaptureBindings());
        Assert.True(copy.IsSlotBound(row.TargetId,1)); Assert.True(copy.IsSlotBound(row.TargetId,2));
        Assert.False(copy.IsSlotBound(row.TargetId,3)); Assert.False(copy.IsSlotBound(0,1));
        Assert.ThrowsAny<ArgumentException>(()=>f.Registry.Reconcile([new(id,[new(new(3,0,0),100)],
            [TimberbornMaterialPart.Tree("Pine")])],[]));
        Assert.False(f.Registry.IsSlotBound(row.TargetId,3));
        Assert.True(f.Registry.IsSlotBound(row.TargetId,1));
    }

    [Fact]
    public void EventUsesHighestIndividualLossThenHeatThenLowestCell()
    {
        var f=new F(); var id=f.Registrations[0].EntityId;
        var first=Row(f,id,9,6);
        var second=first with { CellIndex=4,SlotId=2,OldCell=Cell(9,2),NewCell=Cell(6,2) };
        var third=Row(f,id,6,4);
        f.Consumer.Consume(1,[second,first,third]);
        var receipt=Assert.Single(f.Damage.LastAppliedEventsByTargetKey.Values);
        Assert.Equal(8,receipt.DamageApplied); Assert.Equal(0,receipt.SourceCellIndex);
    }

    [Fact]
    public void NativeDecisionPreservesFullWidthOriginSlot()
    {
        var row=new CellDelta(0,Cell(9),Cell(6),uint.MaxValue-1,uint.MaxValue);
        var decision=TimberbornFireCellDeltaDecision.FromDelta(row);
        Assert.Equal(row.SlotId,decision.SlotId); Assert.Equal(row.TargetId,decision.TargetId);
    }

    private static ushort Cell(int fuel,int heat=3)=>PackedCell.Pack(fuel,10,heat,0,0,1);
    private static CellDelta Row(F f,Guid id,int oldFuel,int newFuel)=>f.Delta(id,0) with
        { OldCell=Cell(oldFuel),NewCell=Cell(newFuel) };
}
