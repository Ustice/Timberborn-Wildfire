using Wildfire.Cli;
using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class CliScenarioTests
{
    [Fact]
    public void CatalogIncludesRequiredTuningScenarios()
    {
        string[] expected =
        [
            "single-ignition",
            "line-of-fuel",
            "water-barrier",
            "vertical-fuel-column",
            "sparse-forest",
            "building-cluster",
            "mixed-terrain",
        ];

        Assert.Subset(ScenarioCatalog.Names.ToHashSet(StringComparer.OrdinalIgnoreCase), expected.ToHashSet());
    }

    [Fact]
    public void OptionsOverrideSeedDimensionsAndScenario()
    {
        CliOptions options = CliOptions.Parse(
        [
            "--scenario=water-barrier",
            "--seed=42",
            "--width=11",
            "--height=7",
            "--depth=2",
            "--layer=1",
        ]);

        Scenario scenario = ScenarioCatalog.Build(options);

        Assert.Equal("water-barrier", scenario.Name);
        Assert.Equal<uint>(42, scenario.Seed);
        Assert.Equal(11, scenario.Grid.Width);
        Assert.Equal(7, scenario.Grid.Height);
        Assert.Equal(2, scenario.Grid.Depth);
    }

    [Theory]
    [InlineData("single-ignition")]
    [InlineData("line-of-fuel")]
    [InlineData("water-barrier")]
    [InlineData("vertical-fuel-column")]
    [InlineData("sparse-forest")]
    [InlineData("building-cluster")]
    [InlineData("mixed-terrain")]
    public void ScenariosProducePackedCellsWithAnIgnition(string name)
    {
        Scenario scenario = ScenarioCatalog.Build(CliOptions.Parse(["--scenario=" + name, "--seed=99"]));

        Assert.Equal(scenario.Grid.CellCount, scenario.Cells.Length);
        Assert.Contains(scenario.Cells, PackedCell.IsBurning);
    }

    [Fact]
    public void SparseScenarioUsesSeedForLayout()
    {
        ushort[] first = ScenarioCatalog.Build(CliOptions.Parse(["--scenario=sparse-forest", "--seed=1"])).Cells;
        ushort[] second = ScenarioCatalog.Build(CliOptions.Parse(["--scenario=sparse-forest", "--seed=2"])).Cells;

        Assert.NotEqual(first, second);
    }
}
