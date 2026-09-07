using Wildfire.Core;

namespace Wildfire.Timberborn.Tests;

public sealed class BurnDamageBatchingTests
{
    private static readonly FireGrid Grid = new(1, 1, 3);
    private static readonly TimberbornBurnDamageTargetKey Key = new("pine-batching");

    [Fact]
    public void DistinctSlotExhaustionHasSameDamageTogetherOrAcrossTicks()
    {
        var together = Create();
        var separate = Create();
        var losses = Enumerable.Range(0, 3).Select(cell => Loss(cell, 8)).ToArray();
        together.ApplyDamage(3, losses);
        for (int i = 0; i < losses.Length; i++) separate.ApplyDamage((uint)i + 1, [losses[i]]);

        Assert.Equal(24, separate.States[Key].DamageTaken);
        Assert.Equal(separate.States[Key].DamageTaken, together.States[Key].DamageTaken);
        Assert.Equal(1, together.LastApplySummary.PersistenceWriteCount);
        Assert.Equal(0, together.LastApplySummary.DuplicateCellSuppressedCount);
    }

    [Fact]
    public void RepeatedCellReportsDoNotSpendAnotherCellsDamage()
    {
        var service = Create();
        var result = service.ApplyDamage(1, [Loss(0, 3), Loss(0, 3), Loss(1, 5), Loss(1, 5)]);
        Assert.Equal(8, service.States[Key].DamageTaken);
        Assert.Equal(2, result.DuplicateCellSuppressedCount);
        Assert.Equal(1, result.DamageAppliedTargetCount);
    }

    [Fact]
    public void SummedCellsStillClampAtBodyCapacityAndDoNotApplyAgainAfterExhaustion()
    {
        var service = Create();
        var result = service.ApplyDamage(1, [Loss(0, 15), Loss(1, 15), Loss(2, 15)]);
        Assert.Equal(24, result.TotalDamageApplied);
        Assert.Equal(0, service.ApplyDamage(2, [Loss(0, 1)]).TotalDamageApplied);
    }

    private static TimberbornBurnDamageService Create()
    {
        var descriptor = new TimberbornBurnDamageDescriptor("Pine", TimberbornBurnDamageTargetKind.Tree,
            TimberbornBurnMaterialKind.Wood, resourceYields: [new("Log", 2)]);
        var service = new TimberbornBurnDamageService(new([descriptor]));
        service.RegisterTargets(Grid, [new(Key, "Pine", Enumerable.Range(0, 3)
            .Select(z => new TimberbornCellCoordinates(0, 0, z)).ToArray())]);
        return service;
    }

    private static TimberbornFireCellDeltaDecision Loss(int cell, int amount) =>
        TimberbornFireCellDeltaDecision.FromDelta(new CellDelta(cell,
            PackedCell.Pack(amount, 10, 3, 0, 1, 1), PackedCell.Pack(0, 10, 3, 0, 1, 1)));
}
