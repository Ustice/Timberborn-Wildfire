using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornFireCellMapperTests
{
    [Fact]
    public void CreateInitialCellsMapsSourcesByGridIndex()
    {
        FireGrid grid = new(3, 2, 1);
        TimberbornFireCellMapper mapper = new();
        TimberbornTerrainAdapter terrainAdapter = new();
        TimberbornResourceAdapter resourceAdapter = new();
        TimberbornWaterAdapter waterAdapter = new();

        ushort[] cells = mapper.CreateInitialCells(
            grid,
            [
                terrainAdapter.CreateSource(0, 0, 0, isSolid: true),
                resourceAdapter.CreateSource(1, 0, 0, fuel: 9, flammability: 3, heatLoss: 2, TimberbornResourceKind.Vegetation),
                waterAdapter.CreateSource(1, 0, 0, water: 2),
            ]);

        Assert.Equal(PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 1, heatLoss: 6), cells[0]);
        Assert.Equal(PackedCell.Pack(fuel: 9, heat: 0, flammability: 3, water: 2, terrain: 1, heatLoss: 2), cells[1]);
        Assert.Equal(TimberbornFireCellMapper.EmptyCell, cells[2]);
    }

    [Fact]
    public void CreateInitialCellsUsesDeterministicMaterialBands()
    {
        FireGrid grid = new(5, 1, 1);
        TimberbornFireCellMapper mapper = new();
        TimberbornTerrainAdapter terrainAdapter = new();
        TimberbornResourceAdapter resourceAdapter = new();
        TimberbornBuildingAdapter buildingAdapter = new();

        ushort[] cells = mapper.CreateInitialCells(
            grid,
            [
                terrainAdapter.CreateSource(0, 0, 0, isSolid: true),
                resourceAdapter.CreateStockpileResourceSource(1, 0, 0),
                resourceAdapter.CreateVegetationSource(2, 0, 0),
                buildingAdapter.CreateWoodLikeSource(3, 0, 0),
                buildingAdapter.CreateNonBurnableSource(4, 0, 0),
            ]);

        Assert.Equal(PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 1, heatLoss: 6), cells[0]);
        Assert.Equal(PackedCell.Pack(fuel: 8, heat: 0, flammability: 2, water: 0, terrain: 1, heatLoss: 3), cells[1]);
        Assert.Equal(PackedCell.Pack(fuel: 10, heat: 0, flammability: 3, water: 0, terrain: 1, heatLoss: 1), cells[2]);
        Assert.Equal(PackedCell.Pack(fuel: 15, heat: 0, flammability: 1, water: 0, terrain: 1, heatLoss: 3), cells[3]);
        Assert.Equal(PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 1, heatLoss: 7), cells[4]);
    }

    [Fact]
    public void CreateMappedCellsIsDeterministicAcrossSourceOrder()
    {
        FireGrid grid = new(2, 2, 1);
        TimberbornFireCellMapper mapper = new();
        TimberbornTerrainAdapter terrainAdapter = new();
        TimberbornBuildingAdapter buildingAdapter = new();
        TimberbornResourceAdapter resourceAdapter = new();
        TimberbornWaterAdapter waterAdapter = new();
        TimberbornCellSource[] orderedSources =
        [
            terrainAdapter.CreateSource(0, 0, 0, isSolid: true, wetness: 1),
            resourceAdapter.CreateSource(0, 0, 0, fuel: 4, flammability: 2, heatLoss: 5, TimberbornResourceKind.StockpileResource),
            buildingAdapter.CreateSource(0, 0, 0, fuel: 12, flammability: 1, heatLoss: 4),
            waterAdapter.CreateSource(0, 0, 0, water: 3),
            resourceAdapter.CreateSource(1, 1, 0, fuel: 8, flammability: 3, heatLoss: 2, TimberbornResourceKind.Vegetation),
        ];
        TimberbornCellSource[] reversedSources = orderedSources.Reverse().ToArray();

        IReadOnlyList<TimberbornMappedCell> ordered = mapper.CreateMappedCells(grid, orderedSources);
        IReadOnlyList<TimberbornMappedCell> reversed = mapper.CreateMappedCells(grid, reversedSources);

        Assert.Equal(ordered, reversed);
        Assert.Equal(
            new TimberbornMappedCell(
                0,
                PackedCell.Pack(fuel: 12, heat: 0, flammability: 1, water: 3, terrain: 1, heatLoss: 4)),
            ordered[0]);
        Assert.Equal(
            new TimberbornMappedCell(
                3,
                PackedCell.Pack(fuel: 8, heat: 0, flammability: 3, water: 0, terrain: 1, heatLoss: 2)),
            ordered[1]);
    }

    [Theory]
    [InlineData("building")]
    [InlineData("resource")]
    public void CreateMappedCellsOverlaysTerrainWetnessOnSelectedMaterial(string selectedMaterial)
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornFireCellMapper mapper = new();
        TimberbornTerrainAdapter terrainAdapter = new();
        TimberbornBuildingAdapter buildingAdapter = new();
        TimberbornResourceAdapter resourceAdapter = new();
        TimberbornCellSource materialSource = selectedMaterial == "building"
            ? buildingAdapter.CreateSource(0, 0, 0, fuel: 11, flammability: 2, heatLoss: 5)
            : resourceAdapter.CreateSource(0, 0, 0, fuel: 7, flammability: 3, heatLoss: 2, TimberbornResourceKind.Vegetation);
        ushort expectedCell = selectedMaterial == "building"
            ? PackedCell.Pack(fuel: 11, heat: 0, flammability: 2, water: 2, terrain: 1, heatLoss: 5)
            : PackedCell.Pack(fuel: 7, heat: 0, flammability: 3, water: 2, terrain: 1, heatLoss: 2);

        IReadOnlyList<TimberbornMappedCell> cells = mapper.CreateMappedCells(
            grid,
            [
                terrainAdapter.CreateSource(0, 0, 0, isSolid: true, wetness: 2),
                materialSource,
            ]);

        TimberbornMappedCell cell = Assert.Single(cells);
        Assert.Equal(new TimberbornMappedCell(0, expectedCell), cell);
    }

    [Fact]
    public void CreateMappedCellsPrioritizesNonBurnableBuildingWithoutFuel()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornFireCellMapper mapper = new();
        TimberbornTerrainAdapter terrainAdapter = new();
        TimberbornBuildingAdapter buildingAdapter = new();
        TimberbornResourceAdapter resourceAdapter = new();
        TimberbornWaterAdapter waterAdapter = new();

        TimberbornMappedCell cell = Assert.Single(mapper.CreateMappedCells(
            grid,
            [
                terrainAdapter.CreateSource(0, 0, 0, isSolid: true, wetness: 1),
                resourceAdapter.CreateVegetationSource(0, 0, 0),
                buildingAdapter.CreateNonBurnableSource(0, 0, 0),
                waterAdapter.CreateSource(0, 0, 0, water: 2),
            ]));

        Assert.Equal(
            new TimberbornMappedCell(
                0,
                PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 2, terrain: 1, heatLoss: 7)),
            cell);
    }

    [Fact]
    public void CreateMappedCellsClampsWetCellsWithoutOverwritingMaterial()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornFireCellMapper mapper = new();
        TimberbornTerrainAdapter terrainAdapter = new();
        TimberbornBuildingAdapter buildingAdapter = new();
        TimberbornWaterAdapter waterAdapter = new();

        TimberbornMappedCell cell = Assert.Single(mapper.CreateMappedCells(
            grid,
            [
                terrainAdapter.CreateSource(0, 0, 0, isSolid: true, wetness: 9),
                buildingAdapter.CreateWoodLikeSource(0, 0, 0),
                waterAdapter.CreateSource(0, 0, 0, water: 1),
            ]));

        Assert.Equal(
            new TimberbornMappedCell(
                0,
                PackedCell.Pack(fuel: 15, heat: 0, flammability: 1, water: 3, terrain: 1, heatLoss: 3)),
            cell);
    }

    [Fact]
    public void CreateMappedCellsMapsMultiCellVerticalFootprints()
    {
        FireGrid grid = new(3, 3, 3);
        TimberbornFireCellMapper mapper = new();
        TimberbornBuildingAdapter buildingAdapter = new();

        IReadOnlyList<TimberbornMappedCell> cells = mapper.CreateMappedCells(
            grid,
            buildingAdapter.CreateWoodLikeFootprintSources(new TimberbornCellFootprint(
                X: 0,
                Y: 1,
                Z: 1,
                Width: 2,
                Height: 1,
                Depth: 2)));

        Assert.Equal([12, 13, 21, 22], cells.Select(static cell => cell.CellIndex).ToArray());
        Assert.All(cells, static cell => Assert.Equal(
            PackedCell.Pack(fuel: 15, heat: 0, flammability: 1, water: 0, terrain: 1, heatLoss: 3),
            cell.PackedCell));
    }

    [Fact]
    public void CreateSetCellChangesEmitsSortedSetCellUpdates()
    {
        FireGrid grid = new(3, 1, 1);
        TimberbornFireCellMapper mapper = new();
        TimberbornBuildingAdapter buildingAdapter = new();
        TimberbornWaterAdapter waterAdapter = new();

        IReadOnlyList<FireSimChange> changes = mapper.CreateSetCellChanges(
            grid,
            [
                waterAdapter.CreateSource(2, 0, 0, water: 2),
                buildingAdapter.CreateSource(1, 0, 0, fuel: 20, flammability: 9, heatLoss: 9),
            ]);

        Assert.Equal([1, 2], changes.Select(static change => change.CellIndex).ToArray());
        Assert.Equal(PackedCell.Pack(fuel: 15, heat: 0, flammability: 3, water: 0, terrain: 1, heatLoss: 7), changes[0].SetCell);
        Assert.Equal(PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 2, terrain: 0, heatLoss: 7), changes[1].SetCell);
        Assert.All(changes, static change =>
        {
            Assert.Null(change.AddFuel);
            Assert.Null(change.AddHeat);
            Assert.Null(change.SetWater);
            Assert.NotNull(change.SetCell);
        });
    }

    [Fact]
    public void CreateMappedCellsRejectsOutOfBoundsSources()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornFireCellMapper mapper = new();
        TimberbornTerrainAdapter terrainAdapter = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => mapper.CreateMappedCells(
            grid,
            [terrainAdapter.CreateSource(1, 0, 0, isSolid: true)]));
    }
}
