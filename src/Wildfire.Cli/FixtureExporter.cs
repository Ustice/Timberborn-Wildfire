using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Wildfire.Cli;

public static class FixtureExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public static string Export(Scenario scenario, int selectedLayer)
    {
        int layer = Math.Clamp(selectedLayer, 0, scenario.Grid.Depth - 1);
        int layerCellCount = scenario.Grid.Width * scenario.Grid.Height;
        int layerOffset = layer * layerCellCount;
        WildfireFixture fixture = new(
            FormatVersion: 1,
            Scenario: scenario.Name,
            Seed: scenario.Seed,
            Grid: new FixtureGrid(scenario.Grid.Width, scenario.Grid.Height, scenario.Grid.Depth),
            SelectedLayer: new FixtureLayer(layer, layerOffset, layerCellCount),
            PackedCellValues: new PackedCellValues(
                ValueType: "uint16",
                IndexOrder: "x + y * width + z * width * height",
                Values: scenario.Cells));

        return JsonSerializer.Serialize(fixture, JsonOptions) + Environment.NewLine;
    }

    public static void Write(string path, Scenario scenario, int selectedLayer)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Fixture export path must not be empty.", nameof(path));
        }

        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, Export(scenario, selectedLayer), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}

public sealed record WildfireFixture(
    int FormatVersion,
    string Scenario,
    uint Seed,
    FixtureGrid Grid,
    FixtureLayer SelectedLayer,
    PackedCellValues PackedCellValues);

public sealed record FixtureGrid(int Width, int Height, int Depth);

public sealed record FixtureLayer(int Index, int Offset, int CellCount);

public sealed record PackedCellValues(string ValueType, string IndexOrder, ushort[] Values);
