namespace Wildfire.Core.Tests;

public sealed class TimberbornReleaseIconBindingTests
{
    [Fact]
    public void FertilizeToolbarButtonsUseWildfireOwnedEmbeddedIcons()
    {
        string root = FindRepoRoot();
        string source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Wildfire.Timberborn",
            "Tools",
            "TimberbornFertilizeToolButtons.cs"));
        string project = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Wildfire.Timberborn",
            "Wildfire.Timberborn.csproj"));

        Assert.DoesNotContain("BurnToolImageName", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_toolButtonFactory.Create(_tool, \"DemolishResourcesTool\"", source, StringComparison.Ordinal);
        Assert.Contains("FertilizeCropsToolImageName = \"FieldsPlantingToolGroupIcon\"", source, StringComparison.Ordinal);
        Assert.Contains("FertilizeTreesToolImageName = \"ForestryPlantingToolGroupIcon\"", source, StringComparison.Ordinal);
        Assert.Contains("WildfireFertilizeCropsToolIcon.png", source, StringComparison.Ordinal);
        Assert.Contains("WildfireFertilizeTreesToolIcon.png", source, StringComparison.Ordinal);
        Assert.Contains("ApplyToolIcon(", source, StringComparison.Ordinal);
        Assert.Contains("Assets\\WildfireFertilizeCropsToolIcon.png", project, StringComparison.Ordinal);
        Assert.Contains("Assets\\WildfireFertilizeTreesToolIcon.png", project, StringComparison.Ordinal);
    }

    [Fact]
    public void FertilizeToolbarIconAssetsHaveRuntimeAndReferenceCopies()
    {
        string root = FindRepoRoot();
        AssertPngExists(root, "src", "Wildfire.Timberborn", "Assets", "WildfireFertilizeCropsToolIcon.png");
        AssertPngExists(root, "src", "Wildfire.Timberborn", "Assets", "WildfireFertilizeTreesToolIcon.png");
        AssertPngExists(root, "docs", "reference", "assets", "menu-icons", "WildfireFertilizeCropsToolIcon.png");
        AssertPngExists(root, "docs", "reference", "assets", "menu-icons", "WildfireFertilizeTreesToolIcon.png");

        string referenceNotes = File.ReadAllText(Path.Combine(
            root,
            "docs",
            "reference",
            "assets",
            "menu-icons",
            "wildfire-fertilize-toolbar-icons.md"));
        Assert.Contains("FieldsPlantingToolGroupIcon.png", referenceNotes, StringComparison.Ordinal);
        Assert.Contains("ForestryPlantingToolGroupIcon.png", referenceNotes, StringComparison.Ordinal);
        Assert.Contains("FertileAshIcon.png", referenceNotes, StringComparison.Ordinal);
    }

    private static void AssertPngExists(string root, params string[] pathParts)
    {
        string path = Path.Combine(new[] { root }.Concat(pathParts).ToArray());
        byte[] bytes = File.ReadAllBytes(path);
        byte[] pngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Assert.True(bytes.Length > pngSignature.Length, $"{path} should not be empty.");
        Assert.Equal(pngSignature, bytes.Take(pngSignature.Length).ToArray());
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
