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
        ushort cell = PackedCell.Pack(fuel: 8, heat: 12, flammability: 3, water: 0, terrain: 1, heatLoss: 1);

        FireVisualSample sample = FireVisualField.FromPackedCell(cell);

        Assert.Equal(0.87f, sample.Fire, precision: 4);
        Assert.Equal(0.7467f, sample.Smoke, precision: 4);
        Assert.Equal(0f, sample.Ash);
        Assert.Equal(sample.Fire, sample.Visibility);
    }

    [Fact]
    public void VisualSampleApproximatesAshOnlyFromTerrainLowFuelAndResidualHeat()
    {
        ushort spentTerrain = PackedCell.Pack(fuel: 0, heat: 6, flammability: 0, water: 0, terrain: 1, heatLoss: 2);
        ushort coldBareTerrain = PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 1, heatLoss: 2);
        ushort nonTerrain = PackedCell.Pack(fuel: 0, heat: 6, flammability: 0, water: 0, terrain: 0, heatLoss: 2);

        Assert.Equal(0.4f, FireVisualField.FromPackedCell(spentTerrain).Ash, precision: 4);
        Assert.Equal(0f, FireVisualField.FromPackedCell(coldBareTerrain).Ash);
        Assert.Equal(0f, FireVisualField.FromPackedCell(nonTerrain).Ash);
    }

    [Fact]
    public void ShaderSourceWritesVisualFieldFromPackedSimulationOutput()
    {
        string shader = ReadFireSimShader();

        Assert.Contains("RWStructuredBuffer<float4> VisualFields;", shader);
        Assert.Contains("float4 BuildVisualSample(uint cell)", shader);
        Assert.Contains("WriteVisualField(index, newCell);", shader);
        Assert.DoesNotContain("Flame", shader);
    }

    private static string ReadFireSimShader()
    {
        string path = SelfAndParents(new DirectoryInfo(AppContext.BaseDirectory))
            .Select(directory => Path.Combine(directory.FullName, "src", "Wildfire.Unity", "FireSim.compute"))
            .First(File.Exists);

        return File.ReadAllText(path);
    }

    private static IEnumerable<DirectoryInfo> SelfAndParents(DirectoryInfo directory)
    {
        return directory.Parent is null
            ? [directory]
            : new[] { directory }.Concat(SelfAndParents(directory.Parent));
    }
}
