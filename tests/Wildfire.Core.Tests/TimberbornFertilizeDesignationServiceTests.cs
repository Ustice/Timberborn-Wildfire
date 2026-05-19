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

    private static string ReadTimberbornSource(string fileName)
    {
        string root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(root, "src", "Wildfire.Timberborn", fileName));
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
