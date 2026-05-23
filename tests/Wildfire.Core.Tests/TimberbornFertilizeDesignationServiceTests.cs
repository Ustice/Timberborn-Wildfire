namespace Wildfire.Core.Tests;

public sealed class TimberbornFertilizeDesignationServiceTests
{
    [Fact]
    public void FertileAshInventoryScanContainsNativeComponentCacheFailures()
    {
        string source = ReadTimberbornSource("TimberbornFertilizeDesignationService.cs");

        Assert.Contains(".Select(CreateInventoryTargetSafely)", source, StringComparison.Ordinal);
        Assert.Contains("catch (Exception exception)", source, StringComparison.Ordinal);
        Assert.Contains("wildfire_fertilize_inventory_scan_skipped", source, StringComparison.Ordinal);
        Assert.Contains("return null;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FertileAshInventoryScanIncludesGathererFlagOutputs()
    {
        string source = ReadTimberbornSource("TimberbornFertilizeDesignationService.cs");

        Assert.Contains("using Timberborn.SimpleOutputBuildings;", source, StringComparison.Ordinal);
        Assert.Contains("entity.TryGetComponent(out SimpleOutputInventory simpleOutputInventory)", source, StringComparison.Ordinal);
        Assert.Contains("inventory = simpleOutputInventory.Inventory;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FertileAshApplicationResolvesDesignationToSimulatorLandingCellBeforeConsumingInventory()
    {
        string source = ReadTimberbornSource("TimberbornFertilizeDesignationService.cs");

        Assert.Contains(
            "_fireRuntime.TryResolveFertileAshApplicationCell(cellIndex, out int applicationCellIndex)",
            source,
            StringComparison.Ordinal);
        Assert.Contains("_fireRuntime.IsCellTaintedAsh(applicationCellIndex)", source, StringComparison.Ordinal);
        Assert.Contains("grid.FromIndex(applicationCellIndex)", source, StringComparison.Ordinal);
        Assert.Contains(
            "_fireRuntime.ApplyPlayerFertileAshDesignation(applicationCellIndex, StrengthPerGood)",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FertileAshApplicationConsumesOnlyUnreservedInventory()
    {
        string source = ReadTimberbornSource("TimberbornFertilizeDesignationService.cs");

        Assert.Contains("private static bool HasUnreservedFertileAsh(Inventory inventory)", source, StringComparison.Ordinal);
        Assert.Contains("inventory.HasUnreservedStock(new GoodAmount(TimberbornAshFieldService.FertileAshGoodId, 1))", source, StringComparison.Ordinal);
        Assert.Contains("HasUnreservedFertileAsh(inv.Inventory)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FertileAshLandingCellResolutionUsesCurrentSimulatorTerrainSnapshot()
    {
        string source = ReadTimberbornSource("TimberbornFireRuntime.cs");

        Assert.Contains("CapturePersistentFireSimState()", source, StringComparison.Ordinal);
        Assert.Contains("PackedCell.Terrain(cells[candidate]) == 1", source, StringComparison.Ordinal);
        Assert.Contains("applicationCellIndex = landingCellIndex.Value;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FireSimKeepsWetCellAshWashoffSparseEnoughForFertilizerReadback()
    {
        string source = ReadUnitySource("FireSim.compute");

        Assert.Contains("Water(newCell) > 0u && ash > 0u && (Tick & 63u) == 0u", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Water(newCell) > 0u && ash > 0u)", source, StringComparison.Ordinal);
    }

    private static string ReadTimberbornSource(string fileName)
    {
        string root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(root, "src", "Wildfire.Timberborn", fileName));
    }

    private static string ReadUnitySource(string fileName)
    {
        string root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(root, "src", "Wildfire.Unity", fileName));
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Wildfire.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate Wildfire repo root.");
    }
}
