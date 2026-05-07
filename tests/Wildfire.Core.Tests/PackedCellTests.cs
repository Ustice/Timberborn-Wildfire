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
        Assert.Equal(7, PackedCell.BurningLevel(cell));
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
        Assert.Equal(4, PackedCell.BurningLevel(cell));
    }

    [Fact]
    public void BurningLevelSetterOnlyChangesBurningLevel()
    {
        ushort cell = PackedCell.Pack(5, 10, 3, 1, 1, 0);
        ushort changed = PackedCell.SetBurningLevel(cell, 6);

        Assert.Equal(5, PackedCell.Fuel(changed));
        Assert.Equal(10, PackedCell.Heat(changed));
        Assert.Equal(3, PackedCell.Flammability(changed));
        Assert.Equal(1, PackedCell.Water(changed));
        Assert.Equal(1, PackedCell.Terrain(changed));
        Assert.Equal(6, PackedCell.BurningLevel(changed));
    }
}
