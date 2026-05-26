using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class TimberbornWorldCellImporterTests
{
    [Theory]
    [InlineData("Path")]
    [InlineData("PowerShaft.IronTeeth")]
    [InlineData("SolidPowerShaft.Folktails")]
    [InlineData("WaterWheel.Folktails")]
    [InlineData("GeothermalEngine.Folktails")]
    [InlineData("WaterPump.Folktails")]
    [InlineData("LargeWaterPump.Folktails")]
    [InlineData("Levee.Folktails")]
    [InlineData("Dam.Folktails")]
    [InlineData("Floodgate.Folktails")]
    [InlineData("Sluice.IronTeeth")]
    public void InfrastructureClassifierIncludesPowerAndWaterRuntimeTargets(string name)
    {
        Assert.True(TimberbornInfrastructureNameClassifier.IsAnyInfrastructureName(name));
    }

    [Fact]
    public void ImporterComposesProvidersAndBuildsMaterialFields()
    {
        FireGrid grid = new(4, 2, 1);
        TimberbornTerrainAdapter terrainAdapter = new();
        TimberbornResourceAdapter resourceAdapter = new();
        TimberbornWorldCellImporter importer = new(
            [
                new TimberbornStaticCellSourceProvider(
                    "terrain",
                    [
                        terrainAdapter.CreateSource(0, 0, 0, isSolid: true),
                        terrainAdapter.CreateSource(1, 0, 0, isSolid: true),
                    ]),
                new TimberbornStaticCellSourceProvider(
                    "trees",
                    [resourceAdapter.CreateTreeSource(1, 0, 0, materialTargetId: 77u)]),
                new TimberbornStaticCellSourceProvider(
                    "vegetation",
                    [resourceAdapter.CreateVegetationSource(2, 0, 0)]),
                new TimberbornSafeUnavailableCellSourceProvider("crops", "safe_live_crop_enumeration_unavailable"),
            ]);

        TimberbornWorldCellImportResult result = importer.Import(grid);

        Assert.Equal(4, result.Summary.TotalSources);
        Assert.Equal(2, result.Summary.Count(WildfireMaterialClass.Terrain));
        Assert.Equal(1, result.Summary.Count(WildfireMaterialClass.Vegetation));
        Assert.Equal(1, result.Summary.Count(WildfireMaterialClass.Tree));
        Assert.Equal(5, result.Summary.ResolvedCount(WildfireMaterialClass.Empty));
        Assert.Equal(1, result.Summary.ResolvedCount(WildfireMaterialClass.Terrain));
        Assert.Equal(1, result.Summary.ResolvedCount(WildfireMaterialClass.Vegetation));
        Assert.Equal(1, result.Summary.ResolvedCount(WildfireMaterialClass.Tree));
        Assert.Equal(1, result.Summary.ProviderSafeUnavailableCounts["crops"]);
        Assert.Contains("terrain_sources=2", result.Summary.StatusToken);
        Assert.Contains("vegetation_sources=1", result.Summary.StatusToken);
        Assert.Contains("tree_sources=1", result.Summary.StatusToken);
        Assert.Contains("resolved_empty_cells=5", result.Summary.StatusToken);
        Assert.Contains("resolved_vegetation_cells=1", result.Summary.StatusToken);
        Assert.Contains("resolved_tree_cells=1", result.Summary.StatusToken);

        WildfireMaterialField treeMaterial = result.MaterialFields[grid.ToIndex(1, 0, 0)];
        Assert.Equal(77u, treeMaterial.TargetId);
        Assert.Equal(WildfireMaterialClass.Tree, treeMaterial.State.MaterialClass);
        Assert.Equal(12, treeMaterial.State.BurnCapacity);
    }

    [Fact]
    public void MapperBuildsFailClosedUnknownMaterialFields()
    {
        FireGrid grid = new(2, 1, 1);
        TimberbornCellSource unknownSource = new(
            new TimberbornCellCoordinates(0, 0, 0),
            MaterialClass: WildfireMaterialClass.Unknown,
            CompanionTargetId: 123u);
        TimberbornFireCellMapper mapper = new();

        WildfireMaterialField[] materialFields = mapper.CreateMaterialFields(grid, [unknownSource]);

        Assert.Equal(123u, materialFields[0].TargetId);
        Assert.Equal(WildfireMaterialClass.Unknown, materialFields[0].State.MaterialClass);
        Assert.Equal(WildfireContaminationBehavior.FailClosed, materialFields[0].State.ContaminationBehavior);
        Assert.Equal(WildfireMaterialField.Empty, materialFields[1]);
    }
}
