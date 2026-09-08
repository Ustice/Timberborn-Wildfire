using Wildfire.Core;
using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Tests;

public sealed class InitialEnvironmentProjectionTests
{
    [Fact]
    public void AcceptedHugeFiniteMoistureSaturatesInitialWetness()
    {
        var result = TimberbornInitialEnvironmentProjection.Project(new(new(1, 1, 2), [0],
            [new(1, float.MaxValue, 0, true, false)], []));
        Assert.Equal((byte)3, result.InitialFields[1].Wetness);
    }

    [Fact]
    public void AcceptedHugeFiniteContaminationSaturatesInitialSoilBand()
    {
        var result = TimberbornInitialEnvironmentProjection.Project(new(new(1, 1, 2), [0],
            [new(1, 0, float.MaxValue, false, true)], []));
        Assert.Equal((byte)7, result.InitialFields[1].SoilContamination);
    }

    [Fact]
    public void StackedColumnsKeepSolidSoilAndActualLiquidDistinct()
    {
        var capture = new TimberbornInitialEnvironmentCapture(new(1, 1, 6), [0, 3],
            [new(1, 4, 0, true, false), new(4, 12, .6f, true, true)],
            [new(0, 0, 1, 3, .25f, 0, 100), new(0, 0, 4, 255, 1.5f, .8f, 100)]);
        var result = TimberbornInitialEnvironmentProjection.Project(capture);
        Assert.Same(capture, result.SourceCapture);
        Assert.Equal(new[] { FireSimBaselineDefinition.SolidTerrain, FireSimBaselineDefinition.OpenSoil,
            FireSimBaselineDefinition.Empty, FireSimBaselineDefinition.SolidTerrain,
            FireSimBaselineDefinition.OpenSoil, FireSimBaselineDefinition.Badwater },
            Enumerable.Range(0, 6).Select(result.MaterialBaseline.GetCell));
        Assert.Equal(new TimberbornInitialEnvironmentFields[] { new(0, 0), new(3, 0), new(0, 0),
            new(0, 0), new(3, 5), new(3, 0) }, result.InitialFields);
        Assert.Equal(new TimberbornInitialLiquidContact[] { new(1, 1, 3, .25, 0),
            new(4, 4, 255, 1, .8f), new(5, 4, 255, .5, .8f) }, result.LiquidContacts);
    }

    [Fact]
    public void SmallestPositiveDepthAtElevatedFloorIsStillLiquid()
    {
        var result = Project(new(1, 1, 6), new TimberbornWaterColumnSample(0, 0, 4, 255, float.Epsilon, float.Epsilon, 0));
        var contact = Assert.Single(result.LiquidContacts);
        Assert.Equal((double)float.Epsilon, contact.OverlapDepth);
        Assert.Equal(4, contact.CellIndex);
        Assert.Equal(FireSimBaselineDefinition.Badwater, result.MaterialBaseline.GetCell(4));
        Assert.Equal(new(3, 0), result.InitialFields[4]);
        Assert.Equal(FireSimBaselineDefinition.Empty, result.MaterialBaseline.GetCell(5));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(1.5f, 2, 1.5)]
    [InlineData(100, 2, 2)]
    public void DepthIsClippedToCeilingAndOverflowAddsNoLiquid(float depth, int cells, double total)
    {
        var result = Project(new(1, 1, 5), new TimberbornWaterColumnSample(0, 0, 1, 3, depth, 0, 999));
        Assert.Equal(cells, result.LiquidContacts.Count);
        Assert.Equal(total, result.LiquidContacts.Sum(contact => contact.OverlapDepth));
        Assert.Equal(FireSimBaselineDefinition.Empty, result.MaterialBaseline.GetCell(3));
        Assert.All(result.LiquidContacts, contact => Assert.Equal(FireSimBaselineDefinition.Water,
            result.MaterialBaseline.GetCell(contact.CellIndex)));
    }

    [Fact]
    public void GridCeilingClipsContactAndAboveGridColumnsContributeNothing()
    {
        var result = Project(new(2, 1, 3), new(0, 0, 2, 255, 100, 0, 0), new(1, 0, 3, 255, 100, 1, 0));
        Assert.Equal(new TimberbornInitialLiquidContact(4, 2, 255, 1, 0), Assert.Single(result.LiquidContacts));
        Assert.Equal(FireSimBaselineDefinition.Empty, result.MaterialBaseline.GetCell(5));
    }

    [Fact]
    public void OnlyPositiveLiquidOverlapContradictsSolidGeometry()
    {
        var grid = new FireGrid(1, 1, 3);
        var dry = new TimberbornInitialEnvironmentCapture(grid, [1], [], [new(0, 0, 0, 3, 0, 1, 10)]);
        var result = TimberbornInitialEnvironmentProjection.Project(dry);
        Assert.Empty(result.LiquidContacts);
        Assert.Equal(FireSimBaselineDefinition.SolidTerrain, result.MaterialBaseline.GetCell(1));
        var wet = new TimberbornInitialEnvironmentCapture(grid, [1], [], [new(0, 0, 0, 3, 1.5f, 1, 10)]);
        Assert.Throws<ArgumentException>(() => TimberbornInitialEnvironmentProjection.Project(wet));
        Assert.Equal(1.5f, wet.WaterColumns[0].Depth);
        Assert.Equal(new[] { 1 }, wet.SolidVoxelIndices);
    }

    [Fact]
    public void DrySoilUsesExistingQuantizersWithoutInventingLiquidOrMaterialFuel()
    {
        var result = TimberbornInitialEnvironmentProjection.Project(new(new(4, 1, 2), [0, 1, 2, 3],
            [new(4, 3.99f, 0, true, false), new(5, 4, float.Epsilon, true, true),
             new(6, 8, .9f, true, true), new(7, 12, 1, true, true)], []));
        Assert.Equal(new TimberbornInitialEnvironmentFields[] { new(0, 0), new(1, 1), new(2, 7), new(3, 7) },
            result.InitialFields.Skip(4));
        Assert.Empty(result.LiquidContacts);
        for (int cell = 4; cell < 8; cell++)
            Assert.Equal(FireSimBaselineDefinition.OpenSoil, result.MaterialBaseline.GetCell(cell));
    }

    [Fact]
    public void InitialOverlayPreservesResolvedOwnerMaterialAndRawUnrelatedBits()
    {
        var grid = new FireGrid(2, 1, 2);
        var result = TimberbornInitialEnvironmentProjection.Project(new(grid, [0],
            [new(2, 8, .6f, true, true)], [new(0, 0, 1, 255, .5f, 1, 0)]));
        ushort[] cells = [0xffff, 0x7aaa, 0xbeef, 0x9123];
        uint[] companion = [0xffffffff, 0xabcdef01, 0x917fedcb, 0xaabbccdd];
        var originalCells = cells.ToArray();
        var originalCompanion = companion.ToArray();
        var overlay = result.OverlayInitialFields(grid, cells, companion);
        for (int cell = 0; cell < cells.Length; cell++)
        {
            Assert.Equal(cells[cell] & ~(3 << 10), overlay.Cells[cell] & ~(3 << 10));
            Assert.Equal(companion[cell] & ~(7u << 25), overlay.CompanionFields[cell] & ~(7u << 25));
        }
        Assert.Equal(3, PackedCell.Water(overlay.Cells[2]));
        Assert.Equal(5u, (overlay.CompanionFields[2] >> 25) & 7);
        Assert.Equal(originalCells, cells);
        Assert.Equal(originalCompanion, companion);
        Assert.Throws<ArgumentException>(() => result.OverlayInitialFields(new(1, 2, 2), cells, companion));
        Assert.Throws<ArgumentException>(() => result.OverlayInitialFields(grid, cells[..3], companion));
        Assert.Throws<ArgumentException>(() => result.OverlayInitialFields(grid, cells, companion[..3]));
    }

    private static TimberbornInitialEnvironmentProjection Project(FireGrid grid, params TimberbornWaterColumnSample[] columns) =>
        TimberbornInitialEnvironmentProjection.Project(new(grid, [], [], columns));
}
