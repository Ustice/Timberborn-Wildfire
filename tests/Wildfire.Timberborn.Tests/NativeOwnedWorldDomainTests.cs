using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Wildfire.Core;
using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeOwnedWorldDomainTests
{
    [Fact]
    public void InstalledMapSizeInitializerProvidesActualTerrainAndBuildableDimensions()
    {
        string zip = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/StreamingAssets/Modding/Blueprints.zip");
        using var archive = ZipFile.OpenRead(zip);
        var entry = archive.Entries.Single(item => item.FullName.EndsWith("/MapSize.blueprint.json", StringComparison.Ordinal) || item.FullName == "MapSize.blueprint.json");
        using var spec = JsonDocument.Parse(entry.Open()); var values = spec.RootElement.GetProperty("MapSizeSpec");
        var f = new NativeOwnedWorldDomainFixture(values.GetProperty("MaxGameTerrainHeight").GetInt32(), values.GetProperty("MaxHeightAboveTerrain").GetInt32());
        Assert.Equal(new FireGrid(1, 1, 23), f.Grid("TerrainGrid"));
        Assert.Equal(new FireGrid(1, 1, 33), f.Grid("WorldGrid"));
        // Separate nondefault native spec establishes that no fixed ten-air-layer adjustment is encoded.
        var changed = new NativeOwnedWorldDomainFixture(5, 2);
        Assert.Equal(6, changed.Grid("TerrainGrid").Depth); Assert.Equal(8, changed.Grid("WorldGrid").Depth);
    }

    [Fact]
    public void SolidReadsStayInNativeTerrainWhileTopSoilAndLiquidUseFullWorld()
    {
        var f = new NativeOwnedWorldDomainFixture(); f.PutSoilAtTerrainTop();
        var grid = f.Grid("WorldGrid"); Assert.Equal(10, grid.Depth);
        var captured = f.Capture(grid);
        Assert.Equal(54, f.Terrain.SolidVoxels.Length); // Native padded 3*3*6 allocation, not world depth10.
        Assert.Equal(new[] { 0, 5 }, captured.SolidVoxelIndices);
        var top = captured.SoilSurfaces.Single(sample => sample.CellIndex == 6);
        Assert.Equal(12, top.Moisture); Assert.Equal(.6f, top.Contamination);
        var projected = TimberbornInitialEnvironmentProjection.Project(captured);
        Assert.Equal(FireSimBaselineDefinition.OpenSoil, projected.MaterialBaseline.GetCell(6));
        Assert.Equal(FireSimBaselineDefinition.Badwater, projected.MaterialBaseline.GetCell(9));
        Assert.Equal(3, projected.InitialFields[6].Wetness);
        Assert.All(projected.LiquidContacts, contact => Assert.InRange(contact.CellIndex, 0, 9));
    }

    [Fact]
    public void HollowNativeColumnsPreserveSeparateSoilSamplesAndRejectMalformedStorage()
    {
        var f = new NativeOwnedWorldDomainFixture();
        var captured = f.Capture(f.Grid("WorldGrid"));
        Assert.Equal(new[] { 0, 3 }, captured.SolidVoxelIndices);
        Assert.Equal(new[] { 1, 4 }, captured.SoilSurfaces.Select(sample => sample.CellIndex));
        Assert.Equal(new[] { 4f, 12f }, captured.SoilSurfaces.Select(sample => sample.Moisture));
        f.AlterColumnCount(3);
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => f.CaptureRaw(f.Grid("WorldGrid"))).InnerException);
        f.AlterColumnCount(2); f.TruncateColumns();
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => f.CaptureRaw(f.Grid("WorldGrid"))).InnerException);
    }

    [Theory]
    [InlineData(5, 7, 4)] // Above the terrain domain, even though still inside world air.
    [InlineData(5, 6, 0)] // Native world has no air cell at this ceiling.
    [InlineData(3, 3, 4)] // Empty or inverted terrain column.
    [InlineData(0, 4, 4)] // Overlaps the first native column.
    public void InvalidNativeCeilingsCannotPublishSoil(int floor, int ceiling, int air)
    {
        var f = new NativeOwnedWorldDomainFixture(5, air); f.AlterColumn(floor, ceiling);
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => f.CaptureRaw(f.Grid("WorldGrid"))).InnerException);
    }

    [Fact]
    public void TerrainOnlySavedGridRejectsAndNativeDomainChangeCannotPassReread()
    {
        var f = new NativeOwnedWorldDomainFixture();
        var mismatch = Assert.Throws<TargetInvocationException>(() => f.CaptureRaw(f.Grid("TerrainGrid")));
        Assert.IsType<NotSupportedException>(mismatch.InnerException);
        var captured = f.CaptureRaw(f.Grid("WorldGrid")); f.RequireUnchanged(captured);
        f.AlterTotal(1, 11);
        Assert.Throws<TargetInvocationException>(() => f.RequireUnchanged(captured));
        f.AlterTotal(2, 10);
        Assert.Throws<TargetInvocationException>(() => f.ReadDomain());
        f.AlterTotal(1, 5);
        Assert.Throws<TargetInvocationException>(() => f.ReadDomain());
    }

    [Fact]
    public void ActualNativeFootprintIncludesHighestBuildableLayerAndRejectsAboveWorld()
    {
        using var f = new NativeMaterialFootprintTests.NativeFootprintFixture();
        var upper = f.Project("Cw0", false, new(20, 20, 33), z: 32);
        Assert.All(upper, slot => Assert.Equal(32, new FireGrid(20, 20, 33).FromIndex(slot.CellIndex).Z));
        Assert.Throws<TargetInvocationException>(() => f.Project("Cw0", false, new(20, 20, 23), z: 32));
        Assert.Throws<TargetInvocationException>(() => f.Project("Cw0", false, new(20, 20, 33), z: 33));
    }
}
