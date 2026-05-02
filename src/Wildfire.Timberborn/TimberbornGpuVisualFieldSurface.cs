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
        Width = width;
        Height = height;
        Depth = depth;
        CellCount = cellCount;
        StrideBytes = strideBytes;
        Channels = channels;
    }

    public object VisualFieldsBuffer { get; }

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
    float Visibility);

public sealed record TimberbornGpuVisualFieldSurfaceState(
    bool IsBound,
    int? Width = null,
    int? Height = null,
    int? Depth = null,
    int? CellCount = null,
    int? StrideBytes = null,
    IReadOnlyList<string>? Channels = null,
    uint? LastUpdatedTick = null)
{
    public static readonly TimberbornGpuVisualFieldSurfaceState Unbound = new(IsBound: false);
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
            LastUpdatedTick: null);
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

        State = State with { LastUpdatedTick = tick };
        _logSink.Info(
            "wildfire_timberborn_gpu_visual_field_surface_updated " +
            $"tick={tick} " +
            $"cell_count={_binding.CellCount} " +
            $"channels={string.Join(",", _binding.Channels)}");
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

    public void Unbind()
    {
    }
}

public sealed class TimberbornGpuVisualFieldSurfaceBindingLifecycle
{
    private readonly ITimberbornGpuVisualFieldSurface _surface;
    private readonly TimberbornGpuVisualFieldSurfaceBinding _binding;
    private bool _isBound;

    public TimberbornGpuVisualFieldSurfaceBindingLifecycle(
        ITimberbornGpuVisualFieldSurface surface,
        object visualFieldsBuffer,
        FireGrid grid,
        int strideBytes)
        : this(
            surface,
            visualFieldsBuffer,
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
        int width,
        int height,
        int depth,
        int cellCount,
        int strideBytes)
    {
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        _binding = new TimberbornGpuVisualFieldSurfaceBinding(
            visualFieldsBuffer,
            width,
            height,
            depth,
            cellCount,
            strideBytes,
            TimberbornGpuVisualFieldChannels.All);
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

        return cellIndices
            .Select(cellIndex => ReadSample(computeBuffer, cellIndex, tick))
            .ToArray();
    }

    private static TimberbornGpuVisualFieldSample ReadSample(ComputeBuffer computeBuffer, int cellIndex, uint? tick)
    {
        Vector4[] sample = new Vector4[1];
        computeBuffer.GetData(sample, 0, cellIndex, 1);

        return new TimberbornGpuVisualFieldSample(
            cellIndex,
            tick,
            sample[0].x,
            sample[0].y,
            sample[0].z,
            sample[0].w);
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
                Ash: 0f,
                Visibility: 0f))
            .ToArray();
    }
}
