using Wildfire.Timberborn.Ash;

namespace Wildfire.Timberborn.Tests;

public sealed class AshHarvestCycleTests
{
    [Fact]
    public void StoppedWalkWithoutArrivalCannotAuthorizeCollection()
    {
        var cycle = new AshHarvestCycle(); cycle.Begin();
        Assert.Throws<InvalidOperationException>(() => cycle.Arrived(false));
        Assert.Equal(AshHarvestPhase.Approaching, cycle.Phase);
    }
    [Fact]
    public void ReadySnapshotIsEmptyAndHasNoAdmittedReceiptToReplay()
    {
        var cycle = Ready();
        var restored = new AshHarvestCycle(); restored.Restore((int)cycle.Phase, cycle.Hours);
        restored.ValidateCargo(false, false, 0);
        Assert.Throws<InvalidOperationException>(() => restored.ValidateCargo(true, true, 1));
        Assert.Equal(AshHarvestPhase.ReadyToCollect, restored.Phase);
    }
    [Theory]
    [InlineData(0, AshHarvestPhase.Idle)]
    [InlineData(1, AshHarvestPhase.Returning)]
    public void ExactReceiptControlsWhetherNativeCargoIsExpected(byte receipt, AshHarvestPhase expected)
    {
        var cycle = Ready(); cycle.Received(receipt);
        Assert.Equal(expected, cycle.Phase);
        cycle.ValidateCargo(receipt == 1, receipt == 1, receipt);
        if (receipt == 1)
        {
            cycle.Hold();
            var restored = new AshHarvestCycle(); restored.Restore((int)cycle.Phase, cycle.Hours);
            Assert.Throws<InvalidOperationException>(() => restored.ValidateCargo(false, false, 0));
            Assert.Throws<InvalidOperationException>(() => restored.ValidateCargo(true, true, 2));
            Assert.Throws<InvalidOperationException>(() => restored.ValidateCargo(true, false, 1));
        }
    }
    [Fact]
    public void MultipleOrUnexpectedReceiptCannotMintAnExtraUnit()
    {
        var cycle = Ready();
        Assert.Throws<InvalidOperationException>(() => cycle.Received(2));
        cycle.Received(1);
        Assert.Throws<InvalidOperationException>(() => cycle.Received(1));
    }
    [Theory]
    [InlineData(6, 0)]
    [InlineData(-1, 0)]
    [InlineData(3, -1)]
    public void UnknownOrTransientReceiptPhaseIsNotResumable(int phase, float hours)
    {
        Assert.Throws<InvalidOperationException>(() => new AshHarvestCycle().Restore(phase, hours));
    }
    private static AshHarvestCycle Ready()
    {
        var cycle = new AshHarvestCycle(); cycle.Begin(); cycle.Arrived(true); cycle.Advance(.1f); return cycle;
    }
}
