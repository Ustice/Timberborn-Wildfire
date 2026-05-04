using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornWorldCellImporterTests
{
    [Fact]
    public void ImporterComposesProvidersAndBuildsCompanionFields()
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
                    [resourceAdapter.CreateTreeSource(1, 0, 0, companionTargetId: 77u)]),
                new TimberbornSafeUnavailableCellSourceProvider("crops", "safe_live_crop_enumeration_unavailable"),
            ]);

        TimberbornWorldCellImportResult result = importer.Import(grid);

        Assert.Equal(3, result.Summary.TotalSources);
        Assert.Equal(2, result.Summary.Count(WildfireMaterialClass.Terrain));
        Assert.Equal(1, result.Summary.Count(WildfireMaterialClass.Tree));
        Assert.Equal(1, result.Summary.ProviderSafeUnavailableCounts["crops"]);
        Assert.Contains("terrain_sources=2", result.Summary.StatusToken);
        Assert.Contains("tree_sources=1", result.Summary.StatusToken);
        Assert.Contains("safe_unavailable=1", result.Summary.StatusToken);

        WildfireCompanionField treeCompanion = result.CompanionFields[grid.ToIndex(1, 0, 0)];
        Assert.Equal(77u, treeCompanion.TargetId);
        Assert.Equal(WildfireMaterialClass.Tree, treeCompanion.State.MaterialClass);
        Assert.Equal(12, treeCompanion.State.BurnCapacity);
    }

    [Fact]
    public void MapperBuildsFailClosedUnknownCompanionFields()
    {
        FireGrid grid = new(2, 1, 1);
        TimberbornCellSource unknownSource = new(
            new TimberbornCellCoordinates(0, 0, 0),
            MaterialClass: WildfireMaterialClass.Unknown,
            CompanionTargetId: 123u);
        TimberbornFireCellMapper mapper = new();

        WildfireCompanionField[] companionFields = mapper.CreateCompanionFields(grid, [unknownSource]);

        Assert.Equal(123u, companionFields[0].TargetId);
        Assert.Equal(WildfireMaterialClass.Unknown, companionFields[0].State.MaterialClass);
        Assert.Equal(WildfireContaminationBehavior.FailClosed, companionFields[0].State.ContaminationBehavior);
        Assert.Equal(WildfireCompanionField.Empty, companionFields[1]);
    }
}
