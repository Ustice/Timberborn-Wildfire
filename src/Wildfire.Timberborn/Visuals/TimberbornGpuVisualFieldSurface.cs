using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public interface ITimberbornGpuVisualFieldSurface
{
    TimberbornGpuVisualFieldSurfaceState State { get; }

    bool TryGetBinding(out TimberbornGpuVisualFieldSurfaceBinding binding);

    void Bind(TimberbornGpuVisualFieldSurfaceBinding binding);

    void MarkUpdated(uint tick);

    IReadOnlyList<TimberbornGpuVisualFieldSample> InspectCells(IReadOnlyList<int> cellIndices);

    TimberbornSmokeHeightTelemetry SnapshotSmokeHeight();

    void Unbind();
}

public interface ITimberbornGpuVisualFieldStateProvider
{
    TimberbornGpuVisualFieldSurfaceState VisualFieldSurfaceState { get; }
}

public static class TimberbornGpuVisualFieldChannels
{
    public const string Fire = "fire";
    public const string Smoke = "smoke";
    public const string Ash = "ash";
    public const string Visibility = "visibility";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Fire,
        Smoke,
        Ash,
        Visibility,
    };
}

public sealed record TimberbornGpuVisualFieldSurfaceBinding
{
    public TimberbornGpuVisualFieldSurfaceBinding(
        object visualFieldsBuffer,
        int width,
        int height,
        int depth,
        int cellCount,
        int strideBytes,
        IReadOnlyList<string> channels)
        : this(
            visualFieldsBuffer,
            transportFieldsBuffer: null,
            width,
            height,
            depth,
            cellCount,
            strideBytes,
            channels)
    {
    }

    public TimberbornGpuVisualFieldSurfaceBinding(
        object visualFieldsBuffer,
        object? transportFieldsBuffer,
        int width,
        int height,
        int depth,
        int cellCount,
        int strideBytes,
        IReadOnlyList<string> channels)
        : this(
            visualFieldsBuffer,
            transportFieldsBuffer,
            materialFieldsBuffer: null,
            width,
            height,
            depth,
            cellCount,
            strideBytes,
            channels)
    {
    }

    public TimberbornGpuVisualFieldSurfaceBinding(
        object visualFieldsBuffer,
        object? transportFieldsBuffer,
        object? materialFieldsBuffer,
        int width,
        int height,
        int depth,
        int cellCount,
        int strideBytes,
        IReadOnlyList<string> channels)
    {
        if (visualFieldsBuffer is null)
        {
            throw new ArgumentNullException(nameof(visualFieldsBuffer));
        }

        RequirePositive(width, nameof(width));
        RequirePositive(height, nameof(height));
        RequirePositive(depth, nameof(depth));
        RequirePositive(cellCount, nameof(cellCount));
        RequirePositive(strideBytes, nameof(strideBytes));

        if (channels is null)
        {
            throw new ArgumentNullException(nameof(channels));
        }

        if (channels.Count != 4 ||
            !channels.SequenceEqual(TimberbornGpuVisualFieldChannels.All, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Visual field channels must be fire, smoke, ash, and visibility in buffer order.",
                nameof(channels));
        }

        VisualFieldsBuffer = visualFieldsBuffer;
        TransportFieldsBuffer = transportFieldsBuffer;
        MaterialFieldsBuffer = materialFieldsBuffer;
        Width = width;
        Height = height;
        Depth = depth;
        CellCount = cellCount;
        StrideBytes = strideBytes;
        Channels = channels;
    }

    public object VisualFieldsBuffer { get; }

    public object? TransportFieldsBuffer { get; }

    public object? MaterialFieldsBuffer { get; }

    public object? AtmosphericFieldsBuffer => TransportFieldsBuffer;

    public object? CompanionFieldsBuffer => MaterialFieldsBuffer;

    public int Width { get; }

    public int Height { get; }

    public int Depth { get; }

    public int CellCount { get; }

    public int StrideBytes { get; }

    public IReadOnlyList<string> Channels { get; }

    public bool TryGetComputeBuffer(out ComputeBuffer computeBuffer)
    {
        if (VisualFieldsBuffer is ComputeBuffer buffer)
        {
            computeBuffer = buffer;
            return true;
        }

        computeBuffer = null!;
        return false;
    }

    public TimberbornGpuVisualFieldSurfaceBinding WithTransportFieldsBuffer(object? transportFieldsBuffer)
    {
        return new TimberbornGpuVisualFieldSurfaceBinding(
            VisualFieldsBuffer,
            transportFieldsBuffer,
            MaterialFieldsBuffer,
            Width,
            Height,
            Depth,
            CellCount,
            StrideBytes,
            Channels);
    }

    public TimberbornGpuVisualFieldSurfaceBinding WithMaterialFieldsBuffer(object? materialFieldsBuffer)
    {
        return new TimberbornGpuVisualFieldSurfaceBinding(
            VisualFieldsBuffer,
            TransportFieldsBuffer,
            materialFieldsBuffer,
            Width,
            Height,
            Depth,
            CellCount,
            StrideBytes,
            Channels);
    }

    private static void RequirePositive(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Visual field binding values must be positive.");
        }
    }
}

public sealed record TimberbornGpuVisualFieldSample(
    int CellIndex,
    uint? Tick,
    float Fire,
    float Smoke,
    float Ash,
    float Visibility,
    float Steam = 0f,
    float AtmosphericSmoke = 0f,
    float SmokeContamination = 0f,
    float AshContamination = 0f,
    bool Source = false);

public sealed record TimberbornGpuVisualFieldSurfaceState(
    bool IsBound,
    int? Width = null,
    int? Height = null,
    int? Depth = null,
    int? CellCount = null,
    int? StrideBytes = null,
    IReadOnlyList<string>? Channels = null,
    uint? LastUpdatedTick = null,
    TimberbornSmokeHeightTelemetry? SmokeHeightTelemetry = null)
{
    public static readonly TimberbornGpuVisualFieldSurfaceState Unbound = new(IsBound: false);
}

public sealed record TimberbornSmokeHeightTelemetry(
    uint? Tick,
    int SmokeCellCount,
    int GroundContactSmokeCellCount,
    int AbsoluteGroundSmokeCellCount,
    int NearBottomSmokeCellCount,
    int LowestSmokeZ,
    int HighestSmokeZ,
    int PeakSmoke,
    int SmokeCellCountAtLowestZ,
    int ContaminatedSmokeCellCount,
    int SourceSmokeCellCount = 0,
    int NonSourceSmokeCellCount = 0,
    int NonSourceGroundContactSmokeCellCount = 0,
    int MaxNonSourceSmokeDistanceFromSource = -1)
{
    public static readonly TimberbornSmokeHeightTelemetry Empty = new(
        Tick: null,
        SmokeCellCount: 0,
        GroundContactSmokeCellCount: 0,
        AbsoluteGroundSmokeCellCount: 0,
        NearBottomSmokeCellCount: 0,
        LowestSmokeZ: -1,
        HighestSmokeZ: -1,
        PeakSmoke: 0,
        SmokeCellCountAtLowestZ: 0,
        ContaminatedSmokeCellCount: 0,
        SourceSmokeCellCount: 0,
        NonSourceSmokeCellCount: 0,
        NonSourceGroundContactSmokeCellCount: 0,
        MaxNonSourceSmokeDistanceFromSource: -1);
}

public class TimberbornGpuVisualFieldSurface : ITimberbornGpuVisualFieldSurface
{
    public const int MaxInspectionCellCount = 256;

    private readonly ITimberbornFireLogSink _logSink;
    private readonly ITimberbornGpuVisualFieldDataReader _dataReader;
    private TimberbornGpuVisualFieldSurfaceBinding? _binding;

    public TimberbornGpuVisualFieldSurface(ITimberbornFireLogSink logSink)
        : this(logSink, NullTimberbornGpuVisualFieldDataReader.Instance)
    {
    }

    public TimberbornGpuVisualFieldSurface(
        ITimberbornFireLogSink logSink,
        ITimberbornGpuVisualFieldDataReader dataReader)
    {
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        _dataReader = dataReader ?? throw new ArgumentNullException(nameof(dataReader));
        State = TimberbornGpuVisualFieldSurfaceState.Unbound;
    }

    public TimberbornGpuVisualFieldSurfaceState State { get; private set; }

    public bool TryGetBinding(out TimberbornGpuVisualFieldSurfaceBinding binding)
    {
        if (_binding is null)
        {
            binding = null!;
            return false;
        }

        binding = _binding;
        return true;
    }

    public void Bind(TimberbornGpuVisualFieldSurfaceBinding binding)
    {
        _binding = binding ?? throw new ArgumentNullException(nameof(binding));
        State = new TimberbornGpuVisualFieldSurfaceState(
            IsBound: true,
            Width: binding.Width,
            Height: binding.Height,
            Depth: binding.Depth,
            CellCount: binding.CellCount,
            StrideBytes: binding.StrideBytes,
            Channels: binding.Channels,
            LastUpdatedTick: null,
            SmokeHeightTelemetry: State.SmokeHeightTelemetry);
        _logSink.Info(
            "wildfire_timberborn_gpu_visual_field_surface_bound " +
            $"width={binding.Width} " +
            $"height={binding.Height} " +
            $"depth={binding.Depth} " +
            $"cell_count={binding.CellCount} " +
            $"stride_bytes={binding.StrideBytes} " +
            $"channels={string.Join(",", binding.Channels)}");
    }

    public void MarkUpdated(uint tick)
    {
        if (_binding is null)
        {
            return;
        }

        TimberbornSmokeHeightTelemetry smokeHeightTelemetry = _dataReader.ReadSmokeHeightTelemetry(_binding, tick);
        State = State with
        {
            LastUpdatedTick = tick,
            SmokeHeightTelemetry = smokeHeightTelemetry,
        };
        _logSink.Info(
            "wildfire_timberborn_gpu_visual_field_surface_updated " +
            $"tick={tick} " +
            $"cell_count={_binding.CellCount} " +
            $"channels={string.Join(",", _binding.Channels)}");
        _logSink.Info(
            "wildfire_timberborn_smoke_height_sampled " +
            $"tick={tick} " +
            $"smoke_cells={smokeHeightTelemetry.SmokeCellCount} " +
            $"ground_contact_smoke_cells={smokeHeightTelemetry.GroundContactSmokeCellCount} " +
            $"absolute_ground_smoke_cells={smokeHeightTelemetry.AbsoluteGroundSmokeCellCount} " +
            $"near_bottom_smoke_cells={smokeHeightTelemetry.NearBottomSmokeCellCount} " +
            $"lowest_smoke_z={smokeHeightTelemetry.LowestSmokeZ} " +
            $"highest_smoke_z={smokeHeightTelemetry.HighestSmokeZ} " +
            $"peak_smoke={smokeHeightTelemetry.PeakSmoke} " +
            $"smoke_cells_at_lowest_z={smokeHeightTelemetry.SmokeCellCountAtLowestZ} " +
            $"contaminated_smoke_cells={smokeHeightTelemetry.ContaminatedSmokeCellCount} " +
            $"source_smoke_cells={smokeHeightTelemetry.SourceSmokeCellCount} " +
            $"non_source_smoke_cells={smokeHeightTelemetry.NonSourceSmokeCellCount} " +
            $"non_source_ground_contact_smoke_cells={smokeHeightTelemetry.NonSourceGroundContactSmokeCellCount} " +
            $"max_non_source_smoke_distance_from_source={smokeHeightTelemetry.MaxNonSourceSmokeDistanceFromSource}");
    }

    public IReadOnlyList<TimberbornGpuVisualFieldSample> InspectCells(IReadOnlyList<int> cellIndices)
    {
        if (cellIndices is null)
        {
            throw new ArgumentNullException(nameof(cellIndices));
        }

        if (_binding is null)
        {
            throw new InvalidOperationException("Visual field surface must be bound before inspecting cells.");
        }

        if (cellIndices.Count > MaxInspectionCellCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cellIndices),
                cellIndices.Count,
                $"Visual field inspection is limited to {MaxInspectionCellCount} cells per request.");
        }

        int[] requestedCellIndices = cellIndices.ToArray();
        int[] invalidCellIndices = requestedCellIndices
            .Where(index => index < 0 || index >= _binding.CellCount)
            .Take(1)
            .ToArray();

        if (invalidCellIndices.Length > 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cellIndices),
                invalidCellIndices[0],
                $"Visual field cell index must be between 0 and {_binding.CellCount - 1}.");
        }

        return _dataReader.ReadSamples(_binding, requestedCellIndices, State.LastUpdatedTick);
    }

    public TimberbornSmokeHeightTelemetry SnapshotSmokeHeight()
    {
        return State.SmokeHeightTelemetry ?? TimberbornSmokeHeightTelemetry.Empty;
    }

    public void Unbind()
    {
        if (_binding is null)
        {
            return;
        }

        _logSink.Info("wildfire_timberborn_gpu_visual_field_surface_unbound");
        _binding = null;
        State = TimberbornGpuVisualFieldSurfaceState.Unbound;
    }
}

public sealed class TimberbornLiveGpuVisualFieldSurface : TimberbornGpuVisualFieldSurface
{
    public TimberbornLiveGpuVisualFieldSurface()
        : base(new UnityTimberbornFireLogSink(), new TimberbornComputeBufferVisualFieldDataReader())
    {
    }
}

public sealed class NullTimberbornGpuVisualFieldSurface : ITimberbornGpuVisualFieldSurface
{
    public static readonly NullTimberbornGpuVisualFieldSurface Instance = new();

    private NullTimberbornGpuVisualFieldSurface()
    {
    }

    public TimberbornGpuVisualFieldSurfaceState State => TimberbornGpuVisualFieldSurfaceState.Unbound;

    public bool TryGetBinding(out TimberbornGpuVisualFieldSurfaceBinding binding)
    {
        binding = null!;
        return false;
    }

    public void Bind(TimberbornGpuVisualFieldSurfaceBinding binding)
    {
    }

    public void MarkUpdated(uint tick)
    {
    }

    public IReadOnlyList<TimberbornGpuVisualFieldSample> InspectCells(IReadOnlyList<int> cellIndices)
    {
        return Array.Empty<TimberbornGpuVisualFieldSample>();
    }

    public TimberbornSmokeHeightTelemetry SnapshotSmokeHeight()
    {
        return TimberbornSmokeHeightTelemetry.Empty;
    }

    public void Unbind()
    {
    }
}

public sealed class TimberbornGpuVisualFieldSurfaceBindingLifecycle
{
    private readonly ITimberbornGpuVisualFieldSurface _surface;
    private TimberbornGpuVisualFieldSurfaceBinding _binding;
    private bool _isBound;

    public TimberbornGpuVisualFieldSurfaceBindingLifecycle(
        ITimberbornGpuVisualFieldSurface surface,
        object visualFieldsBuffer,
        object? atmosphericFieldsBuffer,
        object? companionFieldsBuffer,
        FireGrid grid,
        int strideBytes)
        : this(
            surface,
            visualFieldsBuffer,
            atmosphericFieldsBuffer,
            companionFieldsBuffer,
            grid.Width,
            grid.Height,
            grid.Depth,
            grid.CellCount,
            strideBytes)
    {
    }

    public TimberbornGpuVisualFieldSurfaceBindingLifecycle(
        ITimberbornGpuVisualFieldSurface surface,
        object visualFieldsBuffer,
        object? atmosphericFieldsBuffer,
        object? companionFieldsBuffer,
        int width,
        int height,
        int depth,
        int cellCount,
        int strideBytes)
    {
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        _binding = new TimberbornGpuVisualFieldSurfaceBinding(
            visualFieldsBuffer,
            atmosphericFieldsBuffer,
            companionFieldsBuffer,
            width,
            height,
            depth,
            cellCount,
            strideBytes,
            TimberbornGpuVisualFieldChannels.All);
    }

    public TimberbornGpuVisualFieldSurfaceBindingLifecycle(
        ITimberbornGpuVisualFieldSurface surface,
        object visualFieldsBuffer,
        FireGrid grid,
        int strideBytes)
        : this(surface, visualFieldsBuffer, atmosphericFieldsBuffer: null, companionFieldsBuffer: null, grid, strideBytes)
    {
    }

    public TimberbornGpuVisualFieldSurfaceBindingLifecycle(
        ITimberbornGpuVisualFieldSurface surface,
        object visualFieldsBuffer,
        int width,
        int height,
        int depth,
        int cellCount,
        int strideBytes)
        : this(
            surface,
            visualFieldsBuffer,
            atmosphericFieldsBuffer: null,
            companionFieldsBuffer: null,
            width,
            height,
            depth,
            cellCount,
            strideBytes)
    {
    }

    public void Bind()
    {
        _surface.Bind(_binding);
        _isBound = true;
    }

    public void MarkUpdated(uint tick)
    {
        if (_isBound)
        {
            _surface.MarkUpdated(tick);
        }
    }

    public void UpdateTransportFieldsBuffer(object? transportFieldsBuffer)
    {
        if (_isBound)
        {
            _binding = _binding.WithTransportFieldsBuffer(transportFieldsBuffer);
            _surface.Bind(_binding);
        }
    }

    public void UpdateMaterialFieldsBuffer(object? materialFieldsBuffer)
    {
        if (_isBound)
        {
            _binding = _binding.WithMaterialFieldsBuffer(materialFieldsBuffer);
            _surface.Bind(_binding);
        }
    }

    public void UpdateAtmosphericFieldsBuffer(object? atmosphericFieldsBuffer)
    {
        UpdateTransportFieldsBuffer(atmosphericFieldsBuffer);
    }

    public void UpdateCompanionFieldsBuffer(object? companionFieldsBuffer)
    {
        UpdateMaterialFieldsBuffer(companionFieldsBuffer);
    }

    public void Unbind()
    {
        if (!_isBound)
        {
            return;
        }

        _surface.Unbind();
        _isBound = false;
    }
}

public interface ITimberbornGpuVisualFieldDataReader
{
    IReadOnlyList<TimberbornGpuVisualFieldSample> ReadSamples(
        TimberbornGpuVisualFieldSurfaceBinding binding,
        IReadOnlyList<int> cellIndices,
        uint? tick);

    TimberbornSmokeHeightTelemetry ReadSmokeHeightTelemetry(
        TimberbornGpuVisualFieldSurfaceBinding binding,
        uint? tick);
}

public sealed class TimberbornComputeBufferVisualFieldDataReader : ITimberbornGpuVisualFieldDataReader
{
    public IReadOnlyList<TimberbornGpuVisualFieldSample> ReadSamples(
        TimberbornGpuVisualFieldSurfaceBinding binding,
        IReadOnlyList<int> cellIndices,
        uint? tick)
    {
        if (binding.VisualFieldsBuffer is not ComputeBuffer computeBuffer)
        {
            throw new InvalidOperationException(
                "The Timberborn GPU visual-field surface is not backed by a Unity ComputeBuffer.");
        }

        ComputeBuffer? atmosphericBuffer = binding.TransportFieldsBuffer as ComputeBuffer;

        return cellIndices
            .Select(cellIndex => ReadSample(computeBuffer, atmosphericBuffer, cellIndex, tick))
            .ToArray();
    }

    public TimberbornSmokeHeightTelemetry ReadSmokeHeightTelemetry(
        TimberbornGpuVisualFieldSurfaceBinding binding,
        uint? tick)
    {
        if (binding.TransportFieldsBuffer is not ComputeBuffer atmosphericBuffer)
        {
            return TimberbornSmokeHeightTelemetry.Empty with { Tick = tick };
        }

        uint[] atmosphericFields = new uint[binding.CellCount];
        atmosphericBuffer.GetData(atmosphericFields);
        uint[]? materialFields = ReadMaterialFields(binding);
        return SummarizeSmokeHeight(binding, atmosphericFields, materialFields, tick);
    }

    private static TimberbornGpuVisualFieldSample ReadSample(
        ComputeBuffer computeBuffer,
        ComputeBuffer? atmosphericBuffer,
        int cellIndex,
        uint? tick)
    {
        Vector4[] sample = new Vector4[1];
        computeBuffer.GetData(sample, 0, cellIndex, 1);
        WildfireTransportFieldState atmospheric = ReadAtmospheric(atmosphericBuffer, cellIndex);

        return new TimberbornGpuVisualFieldSample(
            cellIndex,
            tick,
            Fire: sample[0].x,
            Smoke: sample[0].y,
            Ash: sample[0].z,
            Visibility: sample[0].w,
            Steam: atmospheric.Steam / 7f,
            AtmosphericSmoke: atmospheric.Smoke / 7f,
            SmokeContamination: atmospheric.SmokeContamination / 7f,
            AshContamination: atmospheric.AshContamination / 7f,
            Source: atmospheric.Source);
    }

    private static WildfireTransportFieldState ReadAtmospheric(ComputeBuffer? atmosphericBuffer, int cellIndex)
    {
        if (atmosphericBuffer is null)
        {
            return WildfireTransportFieldState.Empty;
        }

        uint[] sample = new uint[1];
        atmosphericBuffer.GetData(sample, 0, cellIndex, 1);
        return WildfireTransportFieldState.Unpack(sample[0]);
    }

    private static uint[]? ReadMaterialFields(TimberbornGpuVisualFieldSurfaceBinding binding)
    {
        if (binding.MaterialFieldsBuffer is not ComputeBuffer materialBuffer)
        {
            return null;
        }

        uint[] materialFields = new uint[binding.CellCount];
        materialBuffer.GetData(materialFields);
        return materialFields;
    }

    private static TimberbornSmokeHeightTelemetry SummarizeSmokeHeight(
        TimberbornGpuVisualFieldSurfaceBinding binding,
        IReadOnlyList<uint> atmosphericFields,
        IReadOnlyList<uint>? materialFields,
        uint? tick)
    {
        int layerSize = binding.Width * binding.Height;
        int smokeCells = 0;
        int groundContactSmokeCells = 0;
        int absoluteGroundSmokeCells = 0;
        int nearBottomSmokeCells = 0;
        int lowestSmokeZ = int.MaxValue;
        int highestSmokeZ = -1;
        int peakSmoke = 0;
        int smokeCellsAtLowestZ = 0;
        int contaminatedSmokeCells = 0;
        List<int> sourceSmokeIndices = new();
        List<int> nonSourceSmokeIndices = new();
        int nonSourceGroundContactSmokeCells = 0;

        Enumerable.Range(0, atmosphericFields.Count)
            .Select(index => (Index: index, Atmospheric: WildfireTransportFieldState.Unpack(atmosphericFields[index])))
            .Where(static sample => sample.Atmospheric.Smoke > 0)
            .ToList()
            .ForEach(sample =>
            {
                int z = sample.Index / layerSize;
                int smoke = sample.Atmospheric.Smoke;
                smokeCells++;
                absoluteGroundSmokeCells += z == 0 ? 1 : 0;
                nearBottomSmokeCells += z <= 2 ? 1 : 0;
                contaminatedSmokeCells += sample.Atmospheric.SmokeContamination > 0 ? 1 : 0;
                groundContactSmokeCells += IsGroundContactSmoke(sample.Index, z, layerSize, materialFields) ? 1 : 0;
                if (sample.Atmospheric.Source)
                {
                    sourceSmokeIndices.Add(sample.Index);
                }
                else
                {
                    nonSourceSmokeIndices.Add(sample.Index);
                    nonSourceGroundContactSmokeCells += IsGroundContactSmoke(sample.Index, z, layerSize, materialFields) ? 1 : 0;
                }

                peakSmoke = Math.Max(peakSmoke, smoke);
                highestSmokeZ = Math.Max(highestSmokeZ, z);
                if (z < lowestSmokeZ)
                {
                    lowestSmokeZ = z;
                    smokeCellsAtLowestZ = 1;
                }
                else if (z == lowestSmokeZ)
                {
                    smokeCellsAtLowestZ++;
                }
            });

        return new TimberbornSmokeHeightTelemetry(
            tick,
            smokeCells,
            groundContactSmokeCells,
            absoluteGroundSmokeCells,
            nearBottomSmokeCells,
            lowestSmokeZ == int.MaxValue ? -1 : lowestSmokeZ,
            highestSmokeZ,
            peakSmoke,
            smokeCellsAtLowestZ,
            contaminatedSmokeCells,
            sourceSmokeIndices.Count,
            nonSourceSmokeIndices.Count,
            nonSourceGroundContactSmokeCells,
            MaxSmokeDistance(nonSourceSmokeIndices, sourceSmokeIndices, binding.Width, binding.Height));
    }

    private static int MaxSmokeDistance(
        IReadOnlyList<int> nonSourceSmokeIndices,
        IReadOnlyList<int> sourceSmokeIndices,
        int width,
        int height)
    {
        if (nonSourceSmokeIndices.Count == 0 || sourceSmokeIndices.Count == 0)
        {
            return -1;
        }

        int layerSize = width * height;
        (int X, int Y, int Z)[] sources = sourceSmokeIndices
            .Select(index => ToCoordinate(index, width, layerSize))
            .ToArray();
        return nonSourceSmokeIndices
            .Select(index =>
            {
                (int x, int y, int z) = ToCoordinate(index, width, layerSize);
                return sources
                    .Select(source => Math.Abs(x - source.X) + Math.Abs(y - source.Y) + Math.Abs(z - source.Z))
                    .Min();
            })
            .Max();
    }

    private static (int X, int Y, int Z) ToCoordinate(int index, int width, int layerSize)
    {
        int z = index / layerSize;
        int layerIndex = index - (z * layerSize);
        return (layerIndex % width, layerIndex / width, z);
    }

    private static bool IsGroundContactSmoke(int index, int z, int layerSize, IReadOnlyList<uint>? materialFields)
    {
        if (materialFields is null)
        {
            return z == 0;
        }

        return IsNonEmptyMaterial(materialFields[index]) ||
            z == 0 ||
            IsNonEmptyMaterial(materialFields[index - layerSize]);
    }

    private static bool IsNonEmptyMaterial(uint packedMaterial)
    {
        return WildfireMaterialFieldState.Unpack(packedMaterial).MaterialClass != WildfireMaterialClass.Empty;
    }
}

public sealed class NullTimberbornGpuVisualFieldDataReader : ITimberbornGpuVisualFieldDataReader
{
    public static readonly NullTimberbornGpuVisualFieldDataReader Instance = new();

    private NullTimberbornGpuVisualFieldDataReader()
    {
    }

    public IReadOnlyList<TimberbornGpuVisualFieldSample> ReadSamples(
        TimberbornGpuVisualFieldSurfaceBinding binding,
        IReadOnlyList<int> cellIndices,
        uint? tick)
    {
        return cellIndices
            .Select(cellIndex => new TimberbornGpuVisualFieldSample(
                cellIndex,
                tick,
                Fire: 0f,
                Smoke: 0f,
                SmokeContamination: 0f,
                Ash: 0f,
                AshContamination: 0f,
                Source: false,
                Visibility: 0f))
            .ToArray();
    }

    public TimberbornSmokeHeightTelemetry ReadSmokeHeightTelemetry(
        TimberbornGpuVisualFieldSurfaceBinding binding,
        uint? tick)
    {
        return TimberbornSmokeHeightTelemetry.Empty with { Tick = tick };
    }
}
