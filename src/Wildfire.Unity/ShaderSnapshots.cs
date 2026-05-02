using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Wildfire.Unity;

public sealed record ShaderSnapshotFixture(
    int FormatVersion,
    string Scenario,
    uint Seed,
    ComputeGridDimensions Grid,
    ShaderSnapshotLayer SelectedLayer,
    ushort[] InitialCells)
{
    public const int CurrentFormatVersion = 1;
    public const string PackedCellValueType = "uint16";
    public const string PackedCellIndexOrder = "x + y * width + z * width * height";

    public ComputeBufferGrid CreateBufferGrid(IComputeBufferAllocator allocator)
    {
        return ComputeBufferGrid.FromCells(Grid.Width, Grid.Height, Grid.Depth, InitialCells, allocator);
    }
}

public sealed record ShaderSnapshotLayer(int Index, int Offset, int CellCount);

public sealed record ShaderSnapshotCapture(
    string Scenario,
    uint Seed,
    ComputeGridDimensions Grid,
    int TickCount,
    ushort[] FinalPackedCells,
    ShaderSnapshotTick[] Ticks,
    ShaderSnapshotVisual? Visual = null);

public sealed record ShaderSnapshotTick(
    int Tick,
    int DeltaCount,
    ShaderSnapshotDelta[] Deltas);

public readonly record struct ShaderSnapshotDelta(
    int CellIndex,
    ushort OldCell,
    ushort NewCell);

public sealed record ShaderSnapshotVisual(
    string? Checksum = null,
    string? ArtifactPath = null);

public interface IShaderSnapshotExecutor
{
    ShaderSnapshotCapture Capture(ShaderSnapshotFixture fixture, int tickCount);
}

public sealed class ShaderSnapshotHarness(IShaderSnapshotExecutor executor)
{
    public ShaderSnapshotCapture Capture(ShaderSnapshotFixture fixture, int tickCount)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tickCount);

        return executor.Capture(fixture, tickCount);
    }

    public ShaderSnapshotComparison Compare(
        ShaderSnapshotCapture expected,
        ShaderSnapshotCapture actual,
        int maxDifferences = 8)
    {
        return ShaderSnapshotComparison.Create(expected, actual, maxDifferences);
    }
}

public static class ShaderSnapshotFixtureLoader
{
    public static ShaderSnapshotFixture LoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Fixture path must not be empty.", nameof(path));
        }

        return Load(File.ReadAllText(path, Encoding.UTF8), path);
    }

    public static ShaderSnapshotFixture Load(string json, string sourceName = "<fixture>")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        int formatVersion = GetRequiredProperty(root, "formatVersion", sourceName).GetInt32();
        if (formatVersion != ShaderSnapshotFixture.CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"{sourceName}: expected fixture formatVersion {ShaderSnapshotFixture.CurrentFormatVersion}, got {formatVersion}.");
        }

        string scenario = GetRequiredString(root, "scenario", sourceName);
        uint seed = GetRequiredUInt32(root, "seed", sourceName);
        JsonElement grid = GetRequiredProperty(root, "grid", sourceName);
        ComputeGridDimensions dimensions = new(
            GetRequiredInt32(grid, "width", sourceName),
            GetRequiredInt32(grid, "height", sourceName),
            GetRequiredInt32(grid, "depth", sourceName));
        JsonElement selectedLayer = GetRequiredProperty(root, "selectedLayer", sourceName);
        ShaderSnapshotLayer layer = new(
            GetRequiredInt32(selectedLayer, "index", sourceName),
            GetRequiredInt32(selectedLayer, "offset", sourceName),
            GetRequiredInt32(selectedLayer, "cellCount", sourceName));
        ValidateLayer(layer, dimensions, sourceName);

        JsonElement packedCells = GetRequiredProperty(root, "packedCellValues", sourceName);
        string valueType = GetRequiredString(packedCells, "valueType", sourceName);
        if (!string.Equals(valueType, ShaderSnapshotFixture.PackedCellValueType, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{sourceName}: expected packedCellValues.valueType uint16, got {valueType}.");
        }

        string indexOrder = GetRequiredString(packedCells, "indexOrder", sourceName);
        if (!string.Equals(indexOrder, ShaderSnapshotFixture.PackedCellIndexOrder, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{sourceName}: unexpected packed cell index order '{indexOrder}'.");
        }

        ushort[] cells = GetRequiredProperty(packedCells, "values", sourceName)
            .EnumerateArray()
            .Select(value => ReadPackedCell(value, sourceName))
            .ToArray();
        ComputeGridValidation.RequireCellCount(dimensions, cells.Length, "packedCellValues.values");

        return new ShaderSnapshotFixture(formatVersion, scenario, seed, dimensions, layer, cells);
    }

    private static ushort ReadPackedCell(JsonElement value, string sourceName)
    {
        uint packedCell = value.GetUInt32();
        if (packedCell > ushort.MaxValue)
        {
            throw new InvalidDataException($"{sourceName}: packed cell value {packedCell} does not fit uint16.");
        }

        return checked((ushort)packedCell);
    }

    private static void ValidateLayer(ShaderSnapshotLayer layer, ComputeGridDimensions dimensions, string sourceName)
    {
        if (layer.Index < 0 || layer.Index >= dimensions.Depth)
        {
            throw new InvalidDataException($"{sourceName}: selectedLayer.index {layer.Index} is outside depth {dimensions.Depth}.");
        }

        int expectedCellCount = dimensions.Width * dimensions.Height;
        int expectedOffset = layer.Index * expectedCellCount;
        if (layer.CellCount != expectedCellCount || layer.Offset != expectedOffset)
        {
            throw new InvalidDataException(
                $"{sourceName}: selectedLayer expected offset {expectedOffset} and cellCount {expectedCellCount}, " +
                $"got offset {layer.Offset} and cellCount {layer.CellCount}.");
        }
    }

    private static JsonElement GetRequiredProperty(JsonElement element, string propertyName, string sourceName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            throw new InvalidDataException($"{sourceName}: missing required property '{propertyName}'.");
        }

        return property;
    }

    private static string GetRequiredString(JsonElement element, string propertyName, string sourceName)
    {
        string? value = GetRequiredProperty(element, propertyName, sourceName).GetString();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"{sourceName}: property '{propertyName}' must not be empty.")
            : value;
    }

    private static int GetRequiredInt32(JsonElement element, string propertyName, string sourceName)
    {
        return GetRequiredProperty(element, propertyName, sourceName).GetInt32();
    }

    private static uint GetRequiredUInt32(JsonElement element, string propertyName, string sourceName)
    {
        return GetRequiredProperty(element, propertyName, sourceName).GetUInt32();
    }
}

public static class ShaderSnapshotJson
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    public static string Serialize(ShaderSnapshotCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);

        return JsonSerializer.Serialize(ToDocument(capture), JsonOptions) + Environment.NewLine;
    }

    public static void WriteFile(string path, ShaderSnapshotCapture capture)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Snapshot path must not be empty.", nameof(path));
        }

        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, Serialize(capture), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static ShaderSnapshotCapture LoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Snapshot path must not be empty.", nameof(path));
        }

        return Load(File.ReadAllText(path, Encoding.UTF8), path);
    }

    public static ShaderSnapshotCapture Load(string json, string sourceName = "<snapshot>")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        int formatVersion = GetRequiredProperty(root, "formatVersion", sourceName).GetInt32();
        if (formatVersion != 1)
        {
            throw new InvalidDataException($"{sourceName}: expected snapshot formatVersion 1, got {formatVersion}.");
        }

        string scenario = GetRequiredString(root, "scenario", sourceName);
        uint seed = GetRequiredProperty(root, "seed", sourceName).GetUInt32();
        JsonElement grid = GetRequiredProperty(root, "grid", sourceName);
        ComputeGridDimensions dimensions = new(
            GetRequiredProperty(grid, "width", sourceName).GetInt32(),
            GetRequiredProperty(grid, "height", sourceName).GetInt32(),
            GetRequiredProperty(grid, "depth", sourceName).GetInt32());
        int tickCount = GetRequiredProperty(root, "tickCount", sourceName).GetInt32();
        ushort[] finalPackedCells = GetRequiredProperty(root, "finalPackedCells", sourceName)
            .EnumerateArray()
            .Select(value => ReadUInt16(value, sourceName, "finalPackedCells"))
            .ToArray();
        ComputeGridValidation.RequireCellCount(dimensions, finalPackedCells.Length, "finalPackedCells");
        ShaderSnapshotTick[] ticks = GetRequiredProperty(root, "perTickDeltas", sourceName)
            .EnumerateArray()
            .Select(tick => ReadTick(tick, sourceName))
            .ToArray();
        ShaderSnapshotVisual? visual = root.TryGetProperty("visual", out JsonElement visualElement) && visualElement.ValueKind != JsonValueKind.Null
            ? new ShaderSnapshotVisual(
                Checksum: visualElement.TryGetProperty("checksum", out JsonElement checksum) ? checksum.GetString() : null,
                ArtifactPath: visualElement.TryGetProperty("artifactPath", out JsonElement artifactPath) ? artifactPath.GetString() : null)
            : null;

        return new ShaderSnapshotCapture(scenario, seed, dimensions, tickCount, finalPackedCells, ticks, visual);
    }

    public static string SerializeFixture(ShaderSnapshotFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        ShaderSnapshotFixtureDocument document = new(
            FormatVersion: fixture.FormatVersion,
            Scenario: fixture.Scenario,
            Seed: fixture.Seed,
            Grid: new ShaderSnapshotGrid(fixture.Grid.Width, fixture.Grid.Height, fixture.Grid.Depth),
            SelectedLayer: fixture.SelectedLayer,
            PackedCellValues: new ShaderSnapshotPackedCellValues(
                ShaderSnapshotFixture.PackedCellValueType,
                ShaderSnapshotFixture.PackedCellIndexOrder,
                fixture.InitialCells));

        return JsonSerializer.Serialize(document, JsonOptions) + Environment.NewLine;
    }

    public static void WriteFixtureFile(string path, ShaderSnapshotFixture fixture)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Fixture path must not be empty.", nameof(path));
        }

        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, SerializeFixture(fixture), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static ShaderSnapshotDocument ToDocument(ShaderSnapshotCapture capture)
    {
        return new ShaderSnapshotDocument(
            FormatVersion: 1,
            Scenario: capture.Scenario,
            Seed: capture.Seed,
            Grid: new ShaderSnapshotGrid(capture.Grid.Width, capture.Grid.Height, capture.Grid.Depth),
            TickCount: capture.TickCount,
            FinalPackedCells: capture.FinalPackedCells,
            PerTickDeltaCounts: capture.Ticks.Select(static tick => tick.DeltaCount).ToArray(),
            PerTickDeltas: capture.Ticks,
            Visual: capture.Visual);
    }

    private static ShaderSnapshotTick ReadTick(JsonElement tick, string sourceName)
    {
        int tickNumber = GetRequiredProperty(tick, "tick", sourceName).GetInt32();
        int deltaCount = GetRequiredProperty(tick, "deltaCount", sourceName).GetInt32();
        ShaderSnapshotDelta[] deltas = GetRequiredProperty(tick, "deltas", sourceName)
            .EnumerateArray()
            .Select(delta => ReadDelta(delta, sourceName))
            .ToArray();

        if (deltaCount != deltas.Length)
        {
            throw new InvalidDataException($"{sourceName}: tick {tickNumber} deltaCount {deltaCount} does not match {deltas.Length} deltas.");
        }

        return new ShaderSnapshotTick(tickNumber, deltaCount, deltas);
    }

    private static ShaderSnapshotDelta ReadDelta(JsonElement delta, string sourceName)
    {
        return new ShaderSnapshotDelta(
            GetRequiredProperty(delta, "cellIndex", sourceName).GetInt32(),
            ReadUInt16(GetRequiredProperty(delta, "oldCell", sourceName), sourceName, "oldCell"),
            ReadUInt16(GetRequiredProperty(delta, "newCell", sourceName), sourceName, "newCell"));
    }

    private static ushort ReadUInt16(JsonElement value, string sourceName, string propertyName)
    {
        uint packedCell = value.GetUInt32();
        if (packedCell > ushort.MaxValue)
        {
            throw new InvalidDataException($"{sourceName}: {propertyName} value {packedCell} does not fit uint16.");
        }

        return checked((ushort)packedCell);
    }

    private static JsonElement GetRequiredProperty(JsonElement element, string propertyName, string sourceName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            throw new InvalidDataException($"{sourceName}: missing required property '{propertyName}'.");
        }

        return property;
    }

    private static string GetRequiredString(JsonElement element, string propertyName, string sourceName)
    {
        string? value = GetRequiredProperty(element, propertyName, sourceName).GetString();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"{sourceName}: property '{propertyName}' must not be empty.")
            : value;
    }

    private sealed record ShaderSnapshotDocument(
        int FormatVersion,
        string Scenario,
        uint Seed,
        ShaderSnapshotGrid Grid,
        int TickCount,
        ushort[] FinalPackedCells,
        int[] PerTickDeltaCounts,
        ShaderSnapshotTick[] PerTickDeltas,
        ShaderSnapshotVisual? Visual);

    private sealed record ShaderSnapshotGrid(int Width, int Height, int Depth);

    private sealed record ShaderSnapshotFixtureDocument(
        int FormatVersion,
        string Scenario,
        uint Seed,
        ShaderSnapshotGrid Grid,
        ShaderSnapshotLayer SelectedLayer,
        ShaderSnapshotPackedCellValues PackedCellValues);

    private sealed record ShaderSnapshotPackedCellValues(string ValueType, string IndexOrder, ushort[] Values);
}

public sealed record ShaderSnapshotComparison(bool Matches, string[] Differences)
{
    public static ShaderSnapshotComparison Create(
        ShaderSnapshotCapture expected,
        ShaderSnapshotCapture actual,
        int maxDifferences = 8)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDifferences);

        List<string> differences = [];
        AddHeaderDifferences(expected, actual, differences, maxDifferences);
        AddFinalCellDifferences(expected, actual, differences, maxDifferences);
        AddTickDifferences(expected, actual, differences, maxDifferences);
        AddVisualDifferences(expected, actual, differences, maxDifferences);

        return new ShaderSnapshotComparison(differences.Count == 0, differences.ToArray());
    }

    private static void AddHeaderDifferences(
        ShaderSnapshotCapture expected,
        ShaderSnapshotCapture actual,
        List<string> differences,
        int maxDifferences)
    {
        AddIfDifferent(differences, "scenario", expected.Scenario, actual.Scenario, maxDifferences);
        AddIfDifferent(differences, "seed", expected.Seed, actual.Seed, maxDifferences);
        AddIfDifferent(differences, "tickCount", expected.TickCount, actual.TickCount, maxDifferences);
        AddIfDifferent(differences, "grid.width", expected.Grid.Width, actual.Grid.Width, maxDifferences);
        AddIfDifferent(differences, "grid.height", expected.Grid.Height, actual.Grid.Height, maxDifferences);
        AddIfDifferent(differences, "grid.depth", expected.Grid.Depth, actual.Grid.Depth, maxDifferences);
    }

    private static void AddFinalCellDifferences(
        ShaderSnapshotCapture expected,
        ShaderSnapshotCapture actual,
        List<string> differences,
        int maxDifferences)
    {
        AddIfDifferent(differences, "finalPackedCells.length", expected.FinalPackedCells.Length, actual.FinalPackedCells.Length, maxDifferences);

        int compareLength = Math.Min(expected.FinalPackedCells.Length, actual.FinalPackedCells.Length);
        Enumerable.Range(0, compareLength)
            .Where(index => expected.FinalPackedCells[index] != actual.FinalPackedCells[index])
            .Take(Math.Max(0, maxDifferences - differences.Count))
            .Select(index => $"finalPackedCells[{index}] expected 0x{expected.FinalPackedCells[index]:X4}, got 0x{actual.FinalPackedCells[index]:X4}.")
            .ToList()
            .ForEach(differences.Add);
    }

    private static void AddTickDifferences(
        ShaderSnapshotCapture expected,
        ShaderSnapshotCapture actual,
        List<string> differences,
        int maxDifferences)
    {
        AddIfDifferent(differences, "ticks.length", expected.Ticks.Length, actual.Ticks.Length, maxDifferences);

        int compareLength = Math.Min(expected.Ticks.Length, actual.Ticks.Length);
        Enumerable.Range(0, compareLength)
            .TakeWhile(_ => differences.Count < maxDifferences)
            .ToList()
            .ForEach(index => AddSingleTickDifferences(expected.Ticks[index], actual.Ticks[index], differences, maxDifferences));
    }

    private static void AddSingleTickDifferences(
        ShaderSnapshotTick expected,
        ShaderSnapshotTick actual,
        List<string> differences,
        int maxDifferences)
    {
        AddIfDifferent(differences, $"ticks[{expected.Tick}].tick", expected.Tick, actual.Tick, maxDifferences);
        AddIfDifferent(differences, $"ticks[{expected.Tick}].deltaCount", expected.DeltaCount, actual.DeltaCount, maxDifferences);
        AddIfDifferent(differences, $"ticks[{expected.Tick}].deltas.length", expected.Deltas.Length, actual.Deltas.Length, maxDifferences);

        ShaderSnapshotDelta[] expectedDeltas = SortDeltas(expected.Deltas);
        ShaderSnapshotDelta[] actualDeltas = SortDeltas(actual.Deltas);
        int compareLength = Math.Min(expected.Deltas.Length, actual.Deltas.Length);
        Enumerable.Range(0, compareLength)
            .Where(index => !expectedDeltas[index].Equals(actualDeltas[index]))
            .Take(Math.Max(0, maxDifferences - differences.Count))
            .Select(index => FormatDeltaDifference(expected.Tick, index, expectedDeltas[index], actualDeltas[index]))
            .ToList()
            .ForEach(differences.Add);
    }

    private static ShaderSnapshotDelta[] SortDeltas(ShaderSnapshotDelta[] deltas)
    {
        return deltas
            .OrderBy(static delta => delta.CellIndex)
            .ThenBy(static delta => delta.OldCell)
            .ThenBy(static delta => delta.NewCell)
            .ToArray();
    }

    private static void AddVisualDifferences(
        ShaderSnapshotCapture expected,
        ShaderSnapshotCapture actual,
        List<string> differences,
        int maxDifferences)
    {
        AddIfDifferent(differences, "visual.checksum", expected.Visual?.Checksum, actual.Visual?.Checksum, maxDifferences);
        AddIfDifferent(differences, "visual.artifactPath", expected.Visual?.ArtifactPath, actual.Visual?.ArtifactPath, maxDifferences);
    }

    private static void AddIfDifferent<T>(List<string> differences, string fieldName, T expected, T actual, int maxDifferences)
    {
        if (differences.Count >= maxDifferences || EqualityComparer<T>.Default.Equals(expected, actual))
        {
            return;
        }

        differences.Add($"{fieldName} expected {expected}, got {actual}.");
    }

    private static string FormatDeltaDifference(
        int tick,
        int deltaIndex,
        ShaderSnapshotDelta expected,
        ShaderSnapshotDelta actual)
    {
        return $"ticks[{tick}].deltas[{deltaIndex}] expected cell {expected.CellIndex} " +
            $"0x{expected.OldCell:X4}->0x{expected.NewCell:X4}, got cell {actual.CellIndex} " +
            $"0x{actual.OldCell:X4}->0x{actual.NewCell:X4}.";
    }
}

public sealed class ShaderSnapshotExecutionBlockedException(ShaderSnapshotExecutionBlocker blocker)
    : InvalidOperationException(blocker.Reason)
{
    public ShaderSnapshotExecutionBlocker Blocker { get; } = blocker;
}

public sealed record ShaderSnapshotExecutionBlocker(string Reason, string Enablement)
{
    public static ShaderSnapshotExecutionBlocker CurrentRepository { get; } = new(
        "Shader snapshot execution requires a local Unity Editor installation with compute-shader capable graphics access.",
        "Use UnityBatchmodeShaderSnapshotExecutor with src/Wildfire.Unity/UnityBatchmodeProject, set WILDFIRE_UNITY_EXECUTABLE if Unity is not at the default path, and enable the opt-in execution test with WILDFIRE_RUN_UNITY_SHADER_HARNESS=1.");
}

public sealed class BlockedShaderSnapshotExecutor(ShaderSnapshotExecutionBlocker blocker) : IShaderSnapshotExecutor
{
    public ShaderSnapshotCapture Capture(ShaderSnapshotFixture fixture, int tickCount)
    {
        throw new ShaderSnapshotExecutionBlockedException(blocker);
    }
}
