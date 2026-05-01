using System.Text.Json.Nodes;
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
        Assert.Equal(1, options.Layer);
    }

    [Fact]
    public void OptionsCaptureFixtureExportPath()
    {
        CliOptions options = CliOptions.Parse(["--export-fixture=fixtures/mixed.json"]);

        Assert.Equal("fixtures/mixed.json", options.ExportFixturePath);
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

    [Fact]
    public void FixtureExportIncludesScenarioGridLayerAndPackedCells()
    {
        Scenario scenario = ScenarioCatalog.Build(CliOptions.Parse(
        [
            "--scenario=vertical-fuel-column",
            "--seed=17",
            "--width=5",
            "--height=4",
            "--depth=3",
        ]));

        string json = FixtureExporter.Export(scenario, selectedLayer: 2);
        JsonNode fixture = JsonNode.Parse(json) ?? throw new InvalidOperationException("Fixture JSON did not parse.");

        Assert.Equal(1, (int?)fixture["formatVersion"]);
        Assert.Equal("vertical-fuel-column", (string?)fixture["scenario"]);
        Assert.Equal(17u, (uint?)fixture["seed"]);
        Assert.Equal(5, (int?)fixture["grid"]?["width"]);
        Assert.Equal(4, (int?)fixture["grid"]?["height"]);
        Assert.Equal(3, (int?)fixture["grid"]?["depth"]);
        Assert.Equal(2, (int?)fixture["selectedLayer"]?["index"]);
        Assert.Equal(40, (int?)fixture["selectedLayer"]?["offset"]);
        Assert.Equal(20, (int?)fixture["selectedLayer"]?["cellCount"]);
        Assert.Equal("uint16", (string?)fixture["packedCellValues"]?["valueType"]);
        Assert.Equal("x + y * width + z * width * height", (string?)fixture["packedCellValues"]?["indexOrder"]);
        Assert.Equal(scenario.Grid.CellCount, fixture["packedCellValues"]?["values"]?.AsArray().Count);
    }

    [Fact]
    public void FixtureExportIsDeterministicForSameInputs()
    {
        CliOptions options = CliOptions.Parse(
        [
            "--scenario=sparse-forest",
            "--seed=42",
            "--width=9",
            "--height=7",
            "--depth=2",
            "--layer=1",
        ]);

        string first = FixtureExporter.Export(ScenarioCatalog.Build(options), options.Layer);
        string second = FixtureExporter.Export(ScenarioCatalog.Build(options), options.Layer);

        Assert.Equal(first, second);
    }
}
