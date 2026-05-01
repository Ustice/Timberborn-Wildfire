using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class CpuFireSimulatorTests
{
    [Fact]
    public void RegisteredChangeAppliesOnNextTick()
    {
        ushort[] cells = [PackedCell.Pack(5, 0, 2, 0, 1, 0)];
        CpuFireSimulator simulator = new(1, 1, 1, seed: 123, cells);

        Assert.Equal(0, PackedCell.Heat(simulator.Cells[0]));

        simulator.RegisterChange(new FireSimChange(CellIndex: 0, AddHeat: 8));
        FireStepResult result = simulator.Tick();

        Assert.NotEmpty(result.Deltas);
        Assert.NotEqual(0, PackedCell.Heat(simulator.Cells[0]));
    }

    [Fact]
    public void HashIsDeterministic()
    {
        uint first = FireRandom.Hash(12, 34, 56);
        uint second = FireRandom.Hash(12, 34, 56);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ActiveFrontierKeepsHotCellsMovingAcrossTicks()
    {
        ushort[] cells = [PackedCell.Pack(5, 10, 0, 0, 1, 0)];
        CpuFireSimulator simulator = new(1, 1, 1, seed: 1, cells);

        FireStepResult first = simulator.Tick();
        FireStepResult second = simulator.Tick();

        Assert.NotEmpty(first.Deltas);
        Assert.NotEmpty(second.Deltas);
        Assert.True(PackedCell.Heat(simulator.Cells[0]) < 10);
    }
}
