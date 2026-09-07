using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wildfire.Core;

namespace Wildfire.Unity;

public sealed record ShaderSnapshotFixture(
    int FormatVersion,
    string Scenario,
    uint Seed,
    ComputeGridDimensions Grid,
    ShaderSnapshotLayer SelectedLayer,
    ushort[] InitialCells,
    uint[]? InitialAtmosphericFields = null,
    uint[]? CompanionFields = null,
    FireSimWind? Wind = null,
    ShaderSnapshotExternalChanges[]? ExternalChanges = null,
    FireSimParameters? Parameters = null,
    uint[]? InitialTargetIds = null,
    uint[]? InitialSlotIds = null,
    ShaderSnapshotMaterialHandoff[]? MaterialHandoffs = null)
{
    public const int CurrentFormatVersion = 1;
    public const string PackedCellValueType = "uint16";
    public const string PackedCellIndexOrder = "x + y * width + z * width * height";

    public uint[] EffectiveInitialAtmosphericFields => InitialAtmosphericFields ?? [];

    public uint[] EffectiveMaterialFields => CompanionFields ?? [];

    public FireSimWind EffectiveWind => Wind ?? FireSimWind.None;

    public FireSimParameters EffectiveParameters => Parameters ?? FireSimParameters.Default;

    public ComputeBufferGrid CreateBufferGrid(IComputeBufferAllocator allocator)
    {
        ShaderSnapshotMaterialHandoff.Validate(this);
        var materials = Enumerable.Range(0, Grid.CellCount)
            .Select(index => new WildfireMaterialField(
                InitialTargetIds?[index] ?? 0,
                WildfireMaterialFieldState.Unpack(CompanionFields?[index] ?? 0))).ToArray();
        ComputeBufferGrid grid = new(Grid, InitialCells, materials, allocator, InitialSlotIds ?? []);

        if (InitialAtmosphericFields is { Length: > 0 } atmosphericFields)
        {
            grid.CurrentTransportFields.Upload(atmosphericFields);
        }

        return grid;
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
    uint[]? FinalAtmosphericFields = null,
    uint[]? FinalCompanionFields = null,
    ShaderSnapshotVisual? Visual = null,
    uint[]? FinalTargetIds = null,
    uint[]? FinalSlotIds = null);

public sealed record ShaderSnapshotTick(
    int Tick,
    int DeltaCount,
    ShaderSnapshotDelta[] Deltas,
    uint[]? AppliedChangeWords = null,
    uint[]? MaterialHeader = null,
    uint[]? MaterialReceipts = null);

public readonly record struct ShaderSnapshotDelta(
    int CellIndex,
    ushort OldCell,
    ushort NewCell,
    uint TargetId = 0,
    uint SlotId = 0);

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
        uint[]? atmosphericFields = ReadOptionalUInt32CellArray(
            root,
            "initialAtmosphericFields",
            dimensions,
            sourceName);
        uint[]? materialFields = ReadOptionalUInt32CellArray(
            root,
            "companionFields",
            dimensions,
            sourceName);
        FireSimWind? wind = ReadOptionalWind(root);

        return new ShaderSnapshotFixture(
            formatVersion,
            scenario,
            seed,
            dimensions,
            layer,
            cells,
            atmosphericFields,
            materialFields,
            wind,
            ShaderSnapshotExternalChanges.Read(root, dimensions.CellCount),
            ShaderSnapshotParameters.Read(root),
            ReadOptionalUInt32CellArray(root, "initialTargetIds", dimensions, sourceName),
            ReadOptionalUInt32CellArray(root, "initialSlotIds", dimensions, sourceName),
            ShaderSnapshotMaterialHandoff.Read(root));
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

    private static uint[]? ReadOptionalUInt32CellArray(
        JsonElement root,
        string propertyName,
        ComputeGridDimensions dimensions,
        string sourceName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement arrayElement) || arrayElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        uint[] values = arrayElement
            .EnumerateArray()
            .Select(static value => value.GetUInt32())
            .ToArray();
        ComputeGridValidation.RequireCellCount(dimensions, values.Length, propertyName);
        return values;
    }

    private static FireSimWind? ReadOptionalWind(JsonElement root)
    {
        if (!root.TryGetProperty("wind", out JsonElement windElement) || windElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        float directionX = windElement.TryGetProperty("directionX", out JsonElement directionXElement)
            ? directionXElement.GetSingle()
            : 0f;
        float directionY = windElement.TryGetProperty("directionY", out JsonElement directionYElement)
            ? directionYElement.GetSingle()
            : 0f;
        float strength = windElement.TryGetProperty("strength", out JsonElement strengthElement)
            ? strengthElement.GetSingle()
            : 0f;

        return new FireSimWind(directionX, directionY, strength);
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
        uint[]? finalAtmosphericFields = ReadOptionalUInt32CellArray(
            root,
            "finalAtmosphericFields",
            dimensions,
            sourceName);
        uint[]? finalMaterialFields = ReadOptionalUInt32CellArray(
            root,
            "finalCompanionFields",
            dimensions,
            sourceName);

        return new ShaderSnapshotCapture(
            scenario,
            seed,
            dimensions,
            tickCount,
            finalPackedCells,
            ticks,
            finalAtmosphericFields,
            finalMaterialFields,
            visual,
            ReadOptionalUInt32CellArray(root, "finalTargetIds", dimensions, sourceName),
            ReadOptionalUInt32CellArray(root, "finalSlotIds", dimensions, sourceName));
    }

    public static string SerializeFixture(ShaderSnapshotFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ShaderSnapshotExternalChanges.Validate(fixture.ExternalChanges, fixture.Grid.CellCount);
        ShaderSnapshotMaterialHandoff.Validate(fixture);

        ShaderSnapshotFixtureDocument document = new(
            FormatVersion: fixture.FormatVersion,
            Scenario: fixture.Scenario,
            Seed: fixture.Seed,
            Grid: new ShaderSnapshotGrid(fixture.Grid.Width, fixture.Grid.Height, fixture.Grid.Depth),
            SelectedLayer: fixture.SelectedLayer,
            PackedCellValues: new ShaderSnapshotPackedCellValues(
                ShaderSnapshotFixture.PackedCellValueType,
                ShaderSnapshotFixture.PackedCellIndexOrder,
                fixture.InitialCells),
            InitialAtmosphericFields: fixture.InitialAtmosphericFields,
            MaterialFields: fixture.CompanionFields,
            Wind: fixture.Wind is { } wind
                ? new ShaderSnapshotWind(wind.DirectionX, wind.DirectionY, wind.Strength)
                : null,
            ExternalChanges: fixture.ExternalChanges,
            Parameters: fixture.EffectiveParameters,
            InitialTargetIds: fixture.InitialTargetIds,
            InitialSlotIds: fixture.InitialSlotIds,
            MaterialHandoffs: fixture.MaterialHandoffs);

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
            FinalAtmosphericFields: capture.FinalAtmosphericFields,
            FinalCompanionFields: capture.FinalCompanionFields,
            PerTickDeltaCounts: capture.Ticks.Select(static tick => tick.DeltaCount).ToArray(),
            PerTickDeltas: capture.Ticks,
            Visual: capture.Visual,
            FinalTargetIds: capture.FinalTargetIds,
            FinalSlotIds: capture.FinalSlotIds);
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

        uint[]? appliedWords = tick.TryGetProperty("appliedChangeWords", out var words) && words.ValueKind != JsonValueKind.Null
            ? words.EnumerateArray().Select(word => word.GetUInt32()).ToArray() : null;
        if (appliedWords is not null && appliedWords.Length % FireSimGpuProtocol.UInt32WordsPerChange != 0)
            throw new InvalidDataException($"{sourceName}: appliedChangeWords must contain complete commands.");
        return new ShaderSnapshotTick(tickNumber, deltaCount, deltas, appliedWords,
            ShaderSnapshotMaterialHandoff.ReadWords(tick, "materialHeader"),
            ShaderSnapshotMaterialHandoff.ReadWords(tick, "materialReceipts"));
    }

    private static ShaderSnapshotDelta ReadDelta(JsonElement delta, string sourceName)
    {
        return new ShaderSnapshotDelta(
            GetRequiredProperty(delta, "cellIndex", sourceName).GetInt32(),
            ReadUInt16(GetRequiredProperty(delta, "oldCell", sourceName), sourceName, "oldCell"),
            ReadUInt16(GetRequiredProperty(delta, "newCell", sourceName), sourceName, "newCell"),
            delta.TryGetProperty("targetId", out var targetId) ? targetId.GetUInt32() : 0,
            delta.TryGetProperty("slotId", out var slotId) ? slotId.GetUInt32() : 0);
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

    private static uint[]? ReadOptionalUInt32CellArray(
        JsonElement root,
        string propertyName,
        ComputeGridDimensions dimensions,
        string sourceName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement arrayElement) || arrayElement.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        uint[] values = arrayElement
            .EnumerateArray()
            .Select(static value => value.GetUInt32())
            .ToArray();
        ComputeGridValidation.RequireCellCount(dimensions, values.Length, propertyName);
        return values;
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
        uint[]? FinalAtmosphericFields,
        [property: JsonPropertyName("finalCompanionFields")]
        uint[]? FinalCompanionFields,
        int[] PerTickDeltaCounts,
        ShaderSnapshotTick[] PerTickDeltas,
        ShaderSnapshotVisual? Visual,
        uint[]? FinalTargetIds,
        uint[]? FinalSlotIds);

    private sealed record ShaderSnapshotGrid(int Width, int Height, int Depth);

    private sealed record ShaderSnapshotFixtureDocument(
        int FormatVersion,
        string Scenario,
        uint Seed,
        ShaderSnapshotGrid Grid,
        ShaderSnapshotLayer SelectedLayer,
        ShaderSnapshotPackedCellValues PackedCellValues,
        uint[]? InitialAtmosphericFields,
        [property: JsonPropertyName("companionFields")]
        uint[]? MaterialFields,
        ShaderSnapshotWind? Wind,
        ShaderSnapshotExternalChanges[]? ExternalChanges,
        FireSimParameters Parameters,
        uint[]? InitialTargetIds,
        uint[]? InitialSlotIds,
        ShaderSnapshotMaterialHandoff[]? MaterialHandoffs);

    private sealed record ShaderSnapshotPackedCellValues(string ValueType, string IndexOrder, ushort[] Values);

    private sealed record ShaderSnapshotWind(float DirectionX, float DirectionY, float Strength);
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
