namespace Wildfire.Core.Tests;

public sealed class TimberbornReleaseIconBindingTests
{
    [Fact]
    public void FertilizeToolbarButtonsRegisterWildfireOwnedEmbeddedIconResource()
    {
        string[] resourceNames = typeof(TimberbornFertilizeDesignationRules)
            .Assembly
            .GetManifestResourceNames();

        Assert.Contains(
            TimberbornFertilizeToolButtonResources.FertilizeToolIconResourceName,
            resourceNames);
        Assert.Equal(["Fields", "Forestry"], TimberbornFertilizeToolButtonResources.ToolGroupIds);
        Assert.Equal(
            ["FieldsPlantingToolGroupIcon", "ForestryPlantingToolGroupIcon"],
            TimberbornFertilizeToolButtonResources.FallbackImageNames);
    }

    [Fact]
    public void FertilizeToolbarIconAssetsHaveRuntimeAndReferenceCopies()
    {
        string root = FindRepoRoot();
        AssertPngExists(root, "src", "Wildfire.Timberborn", "Assets", "WildfireFertilizeToolIcon.png");
        AssertPngExists(root, "docs", "reference", "assets", "menu-icons", "WildfireFertilizeToolIcon.png");

        string referenceNotes = File.ReadAllText(Path.Combine(
            root,
            "docs",
            "reference",
            "assets",
            "menu-icons",
            "wildfire-fertilize-toolbar-icons.md"));
        Assert.Contains("WildfireFertilizeToolIcon.png", referenceNotes, StringComparison.Ordinal);
        Assert.Contains("FieldsPlantingToolGroupIcon.png", referenceNotes, StringComparison.Ordinal);
        Assert.Contains("ForestryPlantingToolGroupIcon.png", referenceNotes, StringComparison.Ordinal);
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
