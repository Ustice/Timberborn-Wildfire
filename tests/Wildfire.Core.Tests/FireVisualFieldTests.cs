using Wildfire.Core;
using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed class FireVisualFieldTests
{
    [Fact]
    public void VisualFieldUsesFloat4CellStride()
    {
        Assert.Equal(4, FireVisualField.ChannelCount);
        Assert.Equal(sizeof(float) * 4, FireVisualField.StrideBytes);
        Assert.Equal(FireVisualField.StrideBytes, ComputeBufferGrid.VisualFieldStrideBytes);
    }

    [Fact]
    public void VisualSampleDerivesFireSmokeAndVisibilityFromPackedBurningCell()
    {
        ushort cell = PackedCell.Pack(fuel: 8, heat: 12, flammability: 3, water: 0, terrain: 1, burningLevel: 1);

        FireVisualSample sample = FireVisualField.FromPackedCell(cell);

        Assert.Equal(0.89f, sample.Fire, precision: 4);
        Assert.Equal(0.312f, sample.Smoke, precision: 4);
        Assert.Equal(0f, sample.Ash);
        Assert.Equal(sample.Fire, sample.Visibility);
    }

    [Fact]
    public void VisualSampleUsesHeatForSmokeOnFueledCells()
    {
        ushort cell = PackedCell.Pack(fuel: 15, heat: 9, flammability: 3, water: 0, terrain: 1, burningLevel: 1);

        FireVisualSample sample = FireVisualField.FromPackedCell(cell);

        Assert.Equal(0.78f, sample.Fire, precision: 4);
        Assert.Equal(0.264f, sample.Smoke, precision: 4);
        Assert.Equal(0f, sample.Ash);
        Assert.True(sample.Smoke > 0f);
    }

    [Fact]
    public void VisualSampleUsesRuntimeParameters()
    {
        ushort cell = PackedCell.Pack(fuel: 15, heat: 9, flammability: 3, water: 0, terrain: 1, burningLevel: 1);
        FireSimParameters parameters = FireSimParameters.Default with
        {
            VisualSmokeHeatWeight = 0.1f,
            VisualFireHeatWeight = 0.25f,
        };

        FireVisualSample sample = FireVisualField.FromPackedCell(cell, parameters);

        Assert.Equal(0.6f, sample.Fire, precision: 4);
        Assert.Equal(0.18f, sample.Smoke, precision: 4);
        Assert.Equal(sample.Fire, sample.Visibility);
    }

    [Fact]
    public void VisualSampleShowsLightSmokeForLowHeatFuelBeforeIgnition()
    {
        ushort cell = PackedCell.Pack(fuel: 12, heat: 4, flammability: 2, water: 0, terrain: 1, burningLevel: 0);

        FireVisualSample sample = FireVisualField.FromPackedCell(cell);

        Assert.Equal(0f, sample.Fire);
        Assert.Equal(0.184f, sample.Smoke, precision: 4);
        Assert.Equal(0.1656f, sample.Visibility, precision: 4);
    }

    [Fact]
    public void VisualSampleDoesNotShowSmokeForHotWetTerrainWithoutFuel()
    {
        ushort cell = PackedCell.Pack(fuel: 0, heat: 4, flammability: 2, water: 2, terrain: 1, burningLevel: 0);

        FireVisualSample sample = FireVisualField.FromPackedCell(cell);

        Assert.Equal(0f, sample.Smoke);
    }

    [Fact]
    public void VisualSampleApproximatesAshOnlyFromTerrainLowFuelAndResidualHeat()
    {
        ushort spentTerrain = PackedCell.Pack(fuel: 0, heat: 6, flammability: 0, water: 0, terrain: 1, burningLevel: 0);
        ushort coldBareTerrain = PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 1, burningLevel: 0);
        ushort nonTerrain = PackedCell.Pack(fuel: 0, heat: 6, flammability: 0, water: 0, terrain: 0, burningLevel: 0);

        FireVisualSample spentSample = FireVisualField.FromPackedCell(spentTerrain);

        Assert.Equal(0.808f, spentSample.Ash, precision: 4);
        Assert.Equal(0.6464f, spentSample.Visibility, precision: 4);
        Assert.Equal(0f, FireVisualField.FromPackedCell(coldBareTerrain).Ash);
        Assert.Equal(0f, FireVisualField.FromPackedCell(nonTerrain).Ash);
    }
}
