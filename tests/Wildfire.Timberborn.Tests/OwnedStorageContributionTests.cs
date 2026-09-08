using Wildfire.Core;

namespace Wildfire.Timberborn.Tests;

public sealed partial class OwnedStorageDeltaConsumerTests
{
    [Fact]
    public void TwoFractionalCellsEqualOrderedSeparateTicksWithFrozenStock()
    {
        var together=new Fixture(); var apart=new Fixture();
        var result=together.Consumer.Consume(1,[together.Delta(A,1,1),together.Delta(A,1)]);
        int removed=apart.Consumer.Consume(1,[apart.Delta(A,1)]).DestroyedItems+
            apart.Consumer.Consume(2,[apart.Delta(A,1,1)]).DestroyedItems;
        Assert.Equal(1,result.DestroyedItems); Assert.Equal(removed,result.DestroyedItems);
        Assert.Equal(apart.Inventory.Stock[A]["Log"],together.Inventory.Stock[A]["Log"]);
        Assert.False(together.Consumer.HasTransientFuelCredit); Assert.False(apart.Consumer.HasTransientFuelCredit);
    }

    [Fact]
    public void CompletionContributionOwnsBlastLocationAndCannotDoubleSpendCredit()
    {
        var f=new Fixture(); f.Inventory.Stock[A]=new() { ["Dynamite"]=1,["Log"]=1 };
        var consumer=CostThreeExplosives(f);
        // Deliberately reversed source cells: deterministic spatial processing completes at cell1.
        var result=consumer.Consume(1,[f.Delta(A,2,1),f.Delta(A,1)]);
        Assert.Equal(1,result.DestroyedItems); Assert.Equal(new[]{1},f.Hazards.Cells);
        Assert.Equal(0,f.Inventory.Stock[A]["Dynamite"]); Assert.Equal(1,f.Inventory.Stock[A]["Log"]);
        Assert.False(consumer.HasTransientFuelCredit);
    }

    [Fact]
    public void ReplayDoesNotEarnCreditButARealRepeatedLossAfterGainDoes()
    {
        var replay=new Fixture(); var cycle=new Fixture();
        var first=replay.Delta(A,1);
        Assert.Equal(0,replay.Consumer.Consume(1,[first,first]).DestroyedItems);
        var gain=first with { OldCell=first.NewCell,NewCell=first.OldCell };
        Assert.Equal(1,cycle.Consumer.Consume(1,[first,gain,first]).DestroyedItems);
        Assert.Equal(10,replay.Inventory.Stock[A]["Log"]); Assert.Equal(9,cycle.Inventory.Stock[A]["Log"]);
    }

    [Fact]
    public void ColdLossSpendsBudgetAndHeatOnlyGainRowsPreserveContinuityWithoutSpending()
    {
        var f=new Fixture();
        var cold=f.Delta(A,1) with { OldCell=PackedCell.Pack(15,10,0,0,0,0),NewCell=PackedCell.Pack(14,10,0,0,0,0) };
        var hot=cold with { OldCell=cold.NewCell,NewCell=PackedCell.Pack(14,10,3,0,0,1) };
        var gain=hot with { OldCell=hot.NewCell,NewCell=PackedCell.Pack(15,10,3,0,0,1) };
        var loss=gain with { OldCell=gain.NewCell,NewCell=hot.NewCell };
        var result=f.Consumer.Consume(1,[cold,hot,gain,loss]);
        Assert.Equal(2,result.Damage.TotalDamageApplied); Assert.Equal(1,result.DestroyedItems);
        Assert.False(f.Consumer.HasTransientFuelCredit);
    }

    [Fact]
    public void EachContributionRechecksOwnerAfterEarlierHazardAndNeverConsumesReplacement()
    {
        var f=new Fixture(); f.Inventory.Stock[A]=new() { ["Dynamite"]=10 };
        f.Hazards.Callback=()=>f.Inventory.Live.Remove(A);
        var result=f.Consumer.Consume(1,[f.Delta(A,1),f.Delta(A,1,1)]);
        Assert.Equal(1,result.DestroyedItems); Assert.Equal(1,result.NotLiveCount);
        Assert.Equal(new[]{0},f.Hazards.Cells); Assert.Equal(9,f.Inventory.Stock[A]["Dynamite"]);
        Assert.DoesNotContain(B,f.Inventory.Calls); Assert.False(f.Resources.IsIndeterminate);
    }

    [Fact]
    public void IndependentCompletedContributionsEmitSeparateBlastsAtTheirOwnCells()
    {
        var f=new Fixture(); f.Inventory.Stock[A]=new() { ["Dynamite"]=10 };
        var result=f.Consumer.Consume(1,[f.Delta(A,1,1),f.Delta(A,1)]);
        Assert.Equal(2,result.DestroyedItems); Assert.Equal(2,result.ExplosiveBlasts);
        Assert.Equal(new[]{0,1},f.Hazards.Cells);
    }

    [Fact]
    public void LaterCallbackFailureKeepsPriorRemovalAndPoisonsSharedSaveGuard()
    {
        var f=new Fixture(); f.Inventory.Stock[A]=new() { ["Dynamite"]=10 };
        var cause=new ApplicationException("second hazard callback after consumption");
        f.Hazards.Callback=()=> { if(f.Hazards.Cells.Count==2) throw cause; };
        Assert.Same(cause,Assert.Throws<ApplicationException>(()=>f.Consumer.Consume(1,[f.Delta(A,1),f.Delta(A,1,1)])));
        Assert.Equal(8,f.Inventory.Stock[A]["Dynamite"]); Assert.True(f.Resources.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(f.Resources.ThrowIfSaveUnsafe);
    }

    private static TimberbornOwnedStorageDeltaConsumer CostThreeExplosives(Fixture f)=>new(f.Registry,f.Damage,
        f.Inventory,f.Hazards,f.Resources,[Registration(A),Registration(B)],new TimberbornResourceFuelCatalog([
            new("Log",2,3,false,false,true),new("Dynamite",3,3,true,false,true)]));
}
