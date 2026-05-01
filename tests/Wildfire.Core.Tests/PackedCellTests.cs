using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class PackedCellTests
{
    [Fact]
    public void PackRoundTripsFields()
    {
        ushort cell = PackedCell.Pack(15, 14, 3, 2, 1, 7);

        Assert.Equal(15, PackedCell.Fuel(cell));
        Assert.Equal(14, PackedCell.Heat(cell));
        Assert.Equal(3, PackedCell.Flammability(cell));
        Assert.Equal(2, PackedCell.Water(cell));
        Assert.Equal(1, PackedCell.Terrain(cell));
        Assert.Equal(7, PackedCell.HeatLoss(cell));
    }

    [Fact]
    public void SettersOnlyChangeTargetFields()
    {
        ushort cell = PackedCell.Pack(1, 2, 3, 1, 1, 4);

        cell = PackedCell.SetFuel(cell, 9);
        cell = PackedCell.SetHeat(cell, 10);
        cell = PackedCell.SetWater(cell, 2);

        Assert.Equal(9, PackedCell.Fuel(cell));
        Assert.Equal(10, PackedCell.Heat(cell));
        Assert.Equal(3, PackedCell.Flammability(cell));
        Assert.Equal(2, PackedCell.Water(cell));
        Assert.Equal(1, PackedCell.Terrain(cell));
        Assert.Equal(4, PackedCell.HeatLoss(cell));
    }

    [Theory]
    [InlineData(9, 3, 0, true)]
    [InlineData(9, 3, 1, false)]
    [InlineData(10, 3, 1, true)]
    [InlineData(15, 0, 3, true)]
    public void BurningStateUsesThreshold(int heat, int flammability, int water, bool expected)
    {
        ushort cell = PackedCell.Pack(5, heat, flammability, water, 1, 0);

        Assert.Equal(expected, PackedCell.IsBurning(cell));
    }
}
