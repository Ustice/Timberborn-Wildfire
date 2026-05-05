using UnityEngine;

namespace Wildfire.Timberborn;

public sealed class TimberbornGpuFieldRendererOptions
{
    public static readonly TimberbornGpuFieldRendererOptions Default = new();

    public TimberbornGpuFieldRendererOptions(
        int RegionSize = 4,
        int MaxUpdatedRegionsPerDispatch = 512,
        float MinimumVisibleIntensity = 0.01f)
    {
        if (RegionSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RegionSize), RegionSize, "Region size must be positive.");
        }

        if (MaxUpdatedRegionsPerDispatch <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxUpdatedRegionsPerDispatch),
                MaxUpdatedRegionsPerDispatch,
                "Updated region limit must be positive.");
        }

        if (MinimumVisibleIntensity < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumVisibleIntensity),
                MinimumVisibleIntensity,
                "Minimum visible intensity cannot be negative.");
        }

        this.RegionSize = RegionSize;
        this.MaxUpdatedRegionsPerDispatch = MaxUpdatedRegionsPerDispatch;
        this.MinimumVisibleIntensity = MinimumVisibleIntensity;
    }

    public int RegionSize { get; }

    public int MaxUpdatedRegionsPerDispatch { get; }

    public float MinimumVisibleIntensity { get; }
}

public readonly record struct TimberbornGpuFieldRendererRegionState(
    int RegionId,
    uint Tick,
    int MinX,
    int MinY,
    int MinZ,
    int MaxX,
    int MaxY,
    int MaxZ,
    int SampleCount,
    float Fire,
    float Smoke,
    float Ash,
    float Steam,
    float Visibility,
    float HeatHaze,
    float Intensity);

public readonly record struct TimberbornGpuFieldRendererCounters(
    bool RendererEnabled,
    bool MaterialReady,
    bool VisualFieldSurfaceBound,
    int VisibleRegionCount,
    int UpdatedRegionCount,
    int LastNonZeroUpdatedRegionCount,
    int MaxUpdatedRegionCount,
    int DroppedRegionCount,
    int MaterialFailureCount,
    uint? LastUpdatedTick,
    uint? LastNonZeroUpdatedRegionTick);

public interface ITimberbornGpuFieldRendererCounterProvider
{
    TimberbornGpuFieldRendererCounters Counters { get; }
}

public interface ITimberbornGpuFieldRendererPresenter
{
    TimberbornGpuFieldRendererPresenterState State { get; }

    TimberbornGpuFieldRendererPresentationResult RenderRegions(
        IReadOnlyList<TimberbornGpuFieldRendererRegionState> regions);

    void Clear();
}

public readonly record struct TimberbornGpuFieldRendererPresenterState(
    bool RendererEnabled,
    bool MaterialReady);

public enum TimberbornGpuFieldRendererPresentationStatus
{
    Applied,
    Disabled,
    Failed,
}

public readonly record struct TimberbornGpuFieldRendererPresentationResult(
    TimberbornGpuFieldRendererPresentationStatus Status,
    string? Message = null)
{
    public static readonly TimberbornGpuFieldRendererPresentationResult Applied = new(
        TimberbornGpuFieldRendererPresentationStatus.Applied);

    public static TimberbornGpuFieldRendererPresentationResult Disabled(string message)
    {
        return new TimberbornGpuFieldRendererPresentationResult(
            TimberbornGpuFieldRendererPresentationStatus.Disabled,
            message);
    }

    public static TimberbornGpuFieldRendererPresentationResult Failed(string message)
    {
        return new TimberbornGpuFieldRendererPresentationResult(
            TimberbornGpuFieldRendererPresentationStatus.Failed,
            message);
    }
}

public sealed class TimberbornGpuFieldRendererSink :
    ITimberbornFireVisualEffectDispatchSink,
    ITimberbornGpuFieldRendererCounterProvider
{
    private readonly ITimberbornGpuVisualFieldSurface _visualFieldSurface;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly ITimberbornGpuFieldRendererPresenter _presenter;
    private readonly Dictionary<int, TimberbornGpuFieldRendererRegionAccumulator> _regionsThisDispatch = new();
    private readonly Dictionary<int, TimberbornGpuFieldRendererRegionState> _visibleRegions = new();
    private int _droppedRegionsThisDispatch;
    private int _materialFailuresThisDispatch;
    private int _lastNonZeroUpdatedRegionCount;
    private uint? _lastNonZeroUpdatedRegionTick;
    private uint? _currentTick;

    public TimberbornGpuFieldRendererSink(
        ITimberbornGpuVisualFieldSurface visualFieldSurface,
        ITimberbornFireLogSink logSink)
        : this(
            visualFieldSurface,
            logSink,
            TimberbornGpuFieldRendererOptions.Default,
            new TimberbornUnityGpuFieldRendererPresenter(logSink))
    {
    }

    public TimberbornGpuFieldRendererSink(
        ITimberbornGpuVisualFieldSurface visualFieldSurface,
        ITimberbornFireLogSink logSink,
        TimberbornGpuFieldRendererOptions options,
        ITimberbornGpuFieldRendererPresenter presenter)
    {
        _visualFieldSurface = visualFieldSurface ?? throw new ArgumentNullException(nameof(visualFieldSurface));
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
    }

    public TimberbornGpuFieldRendererOptions Options { get; }

    public TimberbornGpuFieldRendererCounters Counters => new(
        RendererEnabled: _presenter.State.RendererEnabled,
        MaterialReady: _presenter.State.MaterialReady,
        VisualFieldSurfaceBound: _visualFieldSurface.State.IsBound,
        VisibleRegionCount: _visibleRegions.Count,
        UpdatedRegionCount: _regionsThisDispatch.Count,
        LastNonZeroUpdatedRegionCount: _lastNonZeroUpdatedRegionCount,
        MaxUpdatedRegionCount: Options.MaxUpdatedRegionsPerDispatch,
        DroppedRegionCount: _droppedRegionsThisDispatch,
        MaterialFailureCount: _materialFailuresThisDispatch,
        LastUpdatedTick: _currentTick,
        LastNonZeroUpdatedRegionTick: _lastNonZeroUpdatedRegionTick);

    public IReadOnlyDictionary<int, TimberbornGpuFieldRendererRegionState> VisibleRegions => _visibleRegions;

    public void BeginVisualEffectDispatch(uint tick)
    {
        _currentTick = tick;
        _regionsThisDispatch.Clear();
        _droppedRegionsThisDispatch = 0;
        _materialFailuresThisDispatch = 0;
    }

    public void UpdateVisualEffect(TimberbornFireVisualEffectEvent effectEvent)
    {
        try
        {
            UpdateVisualEffectCore(effectEvent);
        }
        catch (Exception exception)
        {
            _materialFailuresThisDispatch++;
            _logSink.Warning(
                "wildfire_timberborn_gpu_field_renderer_failed " +
                $"stage=update tick={effectEvent.Tick} cell_index={effectEvent.CellIndex} " +
                $"message=\"{EscapeLogValue(exception.Message)}\"");
        }
    }

    public void CompleteVisualEffectDispatch(uint tick)
    {
        _currentTick = tick;
        TimberbornGpuFieldRendererRegionState[] updatedRegions = _regionsThisDispatch.Values
            .Select(static accumulator => accumulator.ToState())
            .Where(region => region.Intensity >= Options.MinimumVisibleIntensity)
            .OrderByDescending(static region => region.Intensity)
            .ThenBy(static region => region.RegionId)
            .Take(Options.MaxUpdatedRegionsPerDispatch)
            .ToArray();
        int droppedByLimit = Math.Max(0, _regionsThisDispatch.Count - updatedRegions.Length);
        _droppedRegionsThisDispatch += droppedByLimit;

        Array.ForEach(updatedRegions, region => _visibleRegions[region.RegionId] = region);
        int[] invisibleRegionIds = _regionsThisDispatch.Values
            .Select(static accumulator => accumulator.ToState())
            .Where(region => region.Intensity < Options.MinimumVisibleIntensity)
            .Select(static region => region.RegionId)
            .ToArray();
        Array.ForEach(invisibleRegionIds, regionId => _visibleRegions.Remove(regionId));

        TimberbornGpuFieldRendererPresentationResult presentationResult = _presenter.RenderRegions(updatedRegions);
        if (presentationResult.Status == TimberbornGpuFieldRendererPresentationStatus.Failed)
        {
            _materialFailuresThisDispatch++;
            _logSink.Warning(
                "wildfire_timberborn_gpu_field_renderer_failed " +
                $"stage=presenter tick={tick} message=\"{EscapeLogValue(presentationResult.Message ?? "presentation failed")}\"");
        }

        if (updatedRegions.Length > 0)
        {
            _lastNonZeroUpdatedRegionCount = updatedRegions.Length;
            _lastNonZeroUpdatedRegionTick = tick;
        }

        _logSink.Info(
            "wildfire_timberborn_gpu_field_renderer_updated " +
            $"tick={tick} " +
            $"visible_regions={_visibleRegions.Count} " +
            $"updated_regions={updatedRegions.Length} " +
            $"last_nonzero_updated_regions={_lastNonZeroUpdatedRegionCount} " +
            $"last_nonzero_updated_regions_tick={FormatNumber(_lastNonZeroUpdatedRegionTick)} " +
            $"dropped_regions={_droppedRegionsThisDispatch} " +
            $"material_failures={_materialFailuresThisDispatch} " +
            $"max_updated_regions={Options.MaxUpdatedRegionsPerDispatch} " +
            $"region_size={Options.RegionSize} " +
            $"renderer_enabled={_presenter.State.RendererEnabled.ToString().ToLowerInvariant()} " +
            $"material_ready={_presenter.State.MaterialReady.ToString().ToLowerInvariant()} " +
            $"visual_field_surface_bound={_visualFieldSurface.State.IsBound.ToString().ToLowerInvariant()}");
    }

    public void Clear()
    {
        _regionsThisDispatch.Clear();
        _visibleRegions.Clear();
        _droppedRegionsThisDispatch = 0;
        _materialFailuresThisDispatch = 0;
        _lastNonZeroUpdatedRegionCount = 0;
        _lastNonZeroUpdatedRegionTick = null;
        _currentTick = null;
        try
        {
            _presenter.Clear();
        }
        catch (Exception exception)
        {
            _materialFailuresThisDispatch++;
            _logSink.Warning(
                "wildfire_timberborn_gpu_field_renderer_failed " +
                $"stage=clear message=\"{EscapeLogValue(exception.Message)}\"");
        }
    }

    private void UpdateVisualEffectCore(TimberbornFireVisualEffectEvent effectEvent)
    {
        if (_currentTick != effectEvent.Tick)
        {
            BeginVisualEffectDispatch(effectEvent.Tick);
        }

        if (!_visualFieldSurface.TryGetBinding(out TimberbornGpuVisualFieldSurfaceBinding binding))
        {
            _droppedRegionsThisDispatch++;
            return;
        }

        TimberbornGpuVisualFieldSample sample = _visualFieldSurface
            .InspectCells(new[] { effectEvent.CellIndex })
            .Single();
        (int x, int y, int z) = FromIndex(binding, sample.CellIndex);
        int regionId = ToRegionId(binding, x, y, z, Options.RegionSize);
        if (!_regionsThisDispatch.TryGetValue(regionId, out TimberbornGpuFieldRendererRegionAccumulator? accumulator))
        {
            if (_regionsThisDispatch.Count >= Options.MaxUpdatedRegionsPerDispatch)
            {
                _droppedRegionsThisDispatch++;
                return;
            }

            accumulator = new TimberbornGpuFieldRendererRegionAccumulator(regionId, effectEvent.Tick, x, y, z);
            _regionsThisDispatch[regionId] = accumulator;
        }

        accumulator.Add(x, y, z, sample);
    }

    private static (int X, int Y, int Z) FromIndex(TimberbornGpuVisualFieldSurfaceBinding binding, int cellIndex)
    {
        int layerSize = binding.Width * binding.Height;
        int z = cellIndex / layerSize;
        int remainder = cellIndex % layerSize;
        int y = remainder / binding.Width;
        int x = remainder % binding.Width;
        return (x, y, z);
    }

    private static int ToRegionId(TimberbornGpuVisualFieldSurfaceBinding binding, int x, int y, int z, int regionSize)
    {
        int regionsX = checked((binding.Width + regionSize - 1) / regionSize);
        int regionsY = checked((binding.Height + regionSize - 1) / regionSize);
        int regionX = x / regionSize;
        int regionY = y / regionSize;
        int regionZ = z;
        return checked(regionZ * regionsX * regionsY + regionY * regionsX + regionX);
    }

    private static string FormatNumber(uint? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }

    private static string EscapeLogValue(string value)
    {
        return value.Replace('\\', '/').Replace('"', '\'');
    }

    private sealed class TimberbornGpuFieldRendererRegionAccumulator
    {
        private float _fire;
        private float _smoke;
        private float _ash;
        private float _visibility;
        private float _heatHaze;
        private float _steam;

        public TimberbornGpuFieldRendererRegionAccumulator(int regionId, uint tick, int x, int y, int z)
        {
            RegionId = regionId;
            Tick = tick;
            MinX = MaxX = x;
            MinY = MaxY = y;
            MinZ = MaxZ = z;
        }

        public int RegionId { get; }

        public uint Tick { get; }

        public int MinX { get; private set; }

        public int MinY { get; private set; }

        public int MinZ { get; private set; }

        public int MaxX { get; private set; }

        public int MaxY { get; private set; }

        public int MaxZ { get; private set; }

        public int SampleCount { get; private set; }

        public void Add(int x, int y, int z, TimberbornGpuVisualFieldSample sample)
        {
            MinX = Math.Min(MinX, x);
            MinY = Math.Min(MinY, y);
            MinZ = Math.Min(MinZ, z);
            MaxX = Math.Max(MaxX, x);
            MaxY = Math.Max(MaxY, y);
            MaxZ = Math.Max(MaxZ, z);
            SampleCount++;
            _fire = Math.Max(_fire, Clamp01(sample.Fire));
            _smoke = Math.Max(_smoke, Clamp01(sample.Smoke));
            _ash = Math.Max(_ash, Clamp01(sample.Ash));
            _visibility = Math.Max(_visibility, Clamp01(sample.Visibility));
            _heatHaze = Math.Max(_heatHaze, Clamp01(sample.Fire * sample.Visibility));
            _steam = Math.Max(_steam, Clamp01(sample.Smoke * (1f - sample.Fire)));
        }

        public TimberbornGpuFieldRendererRegionState ToState()
        {
            float intensity = Math.Max(_fire, Math.Max(_smoke, Math.Max(_ash, Math.Max(_steam, _heatHaze)))) *
                Math.Max(_visibility, 0.001f);
            return new TimberbornGpuFieldRendererRegionState(
                RegionId,
                Tick,
                MinX,
                MinY,
                MinZ,
                MaxX,
                MaxY,
                MaxZ,
                SampleCount,
                _fire,
                _smoke,
                _ash,
                _steam,
                _visibility,
                _heatHaze,
                intensity);
        }

        private static float Clamp01(float value)
        {
            return Math.Clamp(value, 0f, 1f);
        }
    }
}

public sealed class TimberbornUnityGpuFieldRendererPresenter : ITimberbornGpuFieldRendererPresenter
{
    private readonly ITimberbornFireLogSink _logSink;
    private GameObject? _root;
    private Mesh? _mesh;
    private MeshRenderer? _renderer;
    private Material? _material;
    private int _materialFailureCount;

    public TimberbornUnityGpuFieldRendererPresenter(ITimberbornFireLogSink logSink)
    {
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
    }

    public TimberbornGpuFieldRendererPresenterState State => new(
        RendererEnabled: true,
        MaterialReady: _material is not null && _materialFailureCount == 0);

    public TimberbornGpuFieldRendererPresentationResult RenderRegions(
        IReadOnlyList<TimberbornGpuFieldRendererRegionState> regions)
    {
        try
        {
            EnsureObjects();
            if (_mesh is null)
            {
                return TimberbornGpuFieldRendererPresentationResult.Disabled("mesh_unavailable");
            }

            BuildMesh(_mesh, regions);
            if (_root is not null)
            {
                _root.SetActive(regions.Count > 0);
            }

            return TimberbornGpuFieldRendererPresentationResult.Applied;
        }
        catch (Exception exception)
        {
            _materialFailureCount++;
            return TimberbornGpuFieldRendererPresentationResult.Failed(exception.Message);
        }
    }

    public void Clear()
    {
        if (_root is not null)
        {
            UnityEngine.Object.Destroy(_root);
            _root = null;
        }

        if (_mesh is not null)
        {
            UnityEngine.Object.Destroy(_mesh);
            _mesh = null;
        }

        if (_material is not null)
        {
            UnityEngine.Object.Destroy(_material);
            _material = null;
        }

        _renderer = null;
        _materialFailureCount = 0;
    }

    private void EnsureObjects()
    {
        if (_root is not null && _mesh is not null && _renderer is not null && _material is not null)
        {
            return;
        }

        _root = new GameObject("Wildfire GPU Field Renderer")
        {
            hideFlags = HideFlags.DontSave,
        };
        MeshFilter meshFilter = _root.AddComponent<MeshFilter>();
        _renderer = _root.AddComponent<MeshRenderer>();
        _mesh = new Mesh
        {
            name = "Wildfire GPU Field Renderer Mesh",
            hideFlags = HideFlags.DontSave,
        };
        meshFilter.sharedMesh = _mesh;
        Shader? shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
        if (shader is null)
        {
            _logSink.Warning("wildfire_timberborn_gpu_field_renderer_material_unavailable shader=Sprites/Default");
            throw new InvalidOperationException("No transparent shader was available for the GPU field renderer.");
        }

        _material = new Material(shader)
        {
            name = "Wildfire GPU Field Renderer Material",
            hideFlags = HideFlags.DontSave,
        };
        _renderer.sharedMaterial = _material;
    }

    private static void BuildMesh(Mesh mesh, IReadOnlyList<TimberbornGpuFieldRendererRegionState> regions)
    {
        Vector3[] vertices = regions
            .SelectMany(ToVertices)
            .ToArray();
        Color[] colors = regions
            .SelectMany(region => Enumerable.Repeat(ToColor(region), 4))
            .ToArray();
        int[] triangles = regions
            .SelectMany((_, index) =>
            {
                int offset = index * 4;
                return new[] { offset, offset + 1, offset + 2, offset, offset + 2, offset + 3 };
            })
            .ToArray();

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private static IEnumerable<Vector3> ToVertices(TimberbornGpuFieldRendererRegionState region)
    {
        float minX = region.MinX;
        float maxX = region.MaxX + 1f;
        float minY = region.MinY;
        float maxY = region.MaxY + 1f;
        float z = region.MaxZ + 1.02f;
        return new[]
        {
            new Vector3(minX, minY, z),
            new Vector3(maxX, minY, z),
            new Vector3(maxX, maxY, z),
            new Vector3(minX, maxY, z),
        };
    }

    private static Color ToColor(TimberbornGpuFieldRendererRegionState region)
    {
        float red = Math.Clamp(region.Fire + region.HeatHaze * 0.4f + region.Ash * 0.2f, 0f, 1f);
        float green = Math.Clamp(region.Fire * 0.45f + region.Steam * 0.5f + region.Smoke * 0.25f, 0f, 1f);
        float blue = Math.Clamp(region.Steam * 0.75f + region.Smoke * 0.35f + region.Ash * 0.25f, 0f, 1f);
        float alpha = Math.Clamp(region.Intensity * 0.7f, 0f, 0.85f);
        return new Color(red, green, blue, alpha);
    }
}

public sealed class NullTimberbornGpuFieldRendererPresenter : ITimberbornGpuFieldRendererPresenter
{
    public static readonly NullTimberbornGpuFieldRendererPresenter Instance = new();

    private NullTimberbornGpuFieldRendererPresenter()
    {
    }

    public TimberbornGpuFieldRendererPresenterState State => new(
        RendererEnabled: false,
        MaterialReady: false);

    public TimberbornGpuFieldRendererPresentationResult RenderRegions(
        IReadOnlyList<TimberbornGpuFieldRendererRegionState> regions)
    {
        return TimberbornGpuFieldRendererPresentationResult.Disabled("null_presenter");
    }

    public void Clear()
    {
    }
}

public sealed class TimberbornCompositeFireVisualEffectSink : ITimberbornFireVisualEffectDispatchSink
{
    private readonly IReadOnlyList<ITimberbornFireVisualEffectSink> _sinks;

    public TimberbornCompositeFireVisualEffectSink(params ITimberbornFireVisualEffectSink[] sinks)
    {
        _sinks = (sinks ?? throw new ArgumentNullException(nameof(sinks)))
            .Where(static sink => sink is not null)
            .ToArray();
    }

    public void BeginVisualEffectDispatch(uint tick)
    {
        _sinks
            .OfType<ITimberbornFireVisualEffectDispatchSink>()
            .ToList()
            .ForEach(sink => sink.BeginVisualEffectDispatch(tick));
    }

    public void UpdateVisualEffect(TimberbornFireVisualEffectEvent effectEvent)
    {
        _sinks
            .ToList()
            .ForEach(sink => sink.UpdateVisualEffect(effectEvent));
    }

    public void CompleteVisualEffectDispatch(uint tick)
    {
        _sinks
            .OfType<ITimberbornFireVisualEffectDispatchSink>()
            .ToList()
            .ForEach(sink => sink.CompleteVisualEffectDispatch(tick));
    }
}
