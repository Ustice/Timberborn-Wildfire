using Timberborn.MapStateSystem;
using Timberborn.TerrainSystem;
using UnityEngine;
using System.Reflection;

namespace Wildfire.Timberborn;

public sealed class TimberbornGpuFieldRendererOptions
{
    public static readonly TimberbornGpuFieldRendererOptions Default = new();

    public TimberbornGpuFieldRendererOptions(
        int RegionSize = 1,
        int MaxUpdatedRegionsPerDispatch = 2048,
        float MinimumVisibleIntensity = 0.01f,
        int AshBlendCellRadius = 2,
        bool AshOverlayEnabled = true,
        bool DebugOverlayEnabled = false,
        float DebugOverlayHeightOffset = 0.02f)
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

        if (AshBlendCellRadius < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(AshBlendCellRadius),
                AshBlendCellRadius,
                "Ash blend radius cannot be negative.");
        }

        if (DebugOverlayHeightOffset < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DebugOverlayHeightOffset),
                DebugOverlayHeightOffset,
                "Debug overlay height offset cannot be negative.");
        }

        this.RegionSize = RegionSize;
        this.MaxUpdatedRegionsPerDispatch = MaxUpdatedRegionsPerDispatch;
        this.MinimumVisibleIntensity = MinimumVisibleIntensity;
        this.AshBlendCellRadius = AshBlendCellRadius;
        this.AshOverlayEnabled = AshOverlayEnabled;
        this.DebugOverlayEnabled = DebugOverlayEnabled;
        this.DebugOverlayHeightOffset = DebugOverlayHeightOffset;
    }

    public int RegionSize { get; }

    public int MaxUpdatedRegionsPerDispatch { get; }

    public float MinimumVisibleIntensity { get; }

    public int AshBlendCellRadius { get; }

    public bool AshOverlayEnabled { get; }

    public bool DebugOverlayEnabled { get; }

    public float DebugOverlayHeightOffset { get; }
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
    int InvisibleRegionCount,
    int MaterialFailureCount,
    uint? LastUpdatedTick,
    uint? LastNonZeroUpdatedRegionTick);

public interface ITimberbornGpuFieldRendererCounterProvider
{
    TimberbornGpuFieldRendererCounters Counters { get; }
}

public interface ITimberbornAshOverlaySurfaceProvider
{
    bool TryProjectToSurfaceZ(int x, int y, int sourceZ, out int surfaceZ);
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
    private readonly ITimberbornAshOverlaySurfaceProvider _ashOverlaySurfaceProvider;
    private readonly Dictionary<int, TimberbornGpuFieldRendererRegionAccumulator> _regionsThisDispatch = new();
    private readonly Dictionary<int, TimberbornGpuFieldRendererRegionState> _visibleRegions = new();
    private int _updatedRegionsThisDispatch;
    private int _droppedRegionsThisDispatch;
    private int _invisibleRegionsThisDispatch;
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
            CreateDefaultPresenter(logSink, TimberbornGpuFieldRendererOptions.Default),
            NullTimberbornAshOverlaySurfaceProvider.Instance)
    {
    }

    public TimberbornGpuFieldRendererSink(
        ITimberbornGpuVisualFieldSurface visualFieldSurface,
        ITimberbornFireLogSink logSink,
        TimberbornGpuFieldRendererOptions options,
        ITimberbornGpuFieldRendererPresenter presenter)
        : this(
            visualFieldSurface,
            logSink,
            options,
            presenter,
            NullTimberbornAshOverlaySurfaceProvider.Instance)
    {
    }

    public TimberbornGpuFieldRendererSink(
        ITimberbornGpuVisualFieldSurface visualFieldSurface,
        ITimberbornFireLogSink logSink,
        TimberbornGpuFieldRendererOptions options,
        ITimberbornGpuFieldRendererPresenter presenter,
        ITimberbornAshOverlaySurfaceProvider ashOverlaySurfaceProvider)
    {
        _visualFieldSurface = visualFieldSurface ?? throw new ArgumentNullException(nameof(visualFieldSurface));
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _ashOverlaySurfaceProvider = ashOverlaySurfaceProvider ??
            throw new ArgumentNullException(nameof(ashOverlaySurfaceProvider));
    }

    public TimberbornGpuFieldRendererOptions Options { get; }

    public TimberbornGpuFieldRendererCounters Counters => new(
        RendererEnabled: _presenter.State.RendererEnabled,
        MaterialReady: _presenter.State.MaterialReady,
        VisualFieldSurfaceBound: _visualFieldSurface.State.IsBound,
        VisibleRegionCount: _visibleRegions.Count,
        UpdatedRegionCount: _updatedRegionsThisDispatch,
        LastNonZeroUpdatedRegionCount: _lastNonZeroUpdatedRegionCount,
        MaxUpdatedRegionCount: Options.MaxUpdatedRegionsPerDispatch,
        DroppedRegionCount: _droppedRegionsThisDispatch,
        InvisibleRegionCount: _invisibleRegionsThisDispatch,
        MaterialFailureCount: _materialFailuresThisDispatch,
        LastUpdatedTick: _currentTick,
        LastNonZeroUpdatedRegionTick: _lastNonZeroUpdatedRegionTick);

    public IReadOnlyDictionary<int, TimberbornGpuFieldRendererRegionState> VisibleRegions => _visibleRegions;

    private static ITimberbornGpuFieldRendererPresenter CreateDefaultPresenter(
        ITimberbornFireLogSink logSink,
        TimberbornGpuFieldRendererOptions options)
    {
        return options.DebugOverlayEnabled
            ? new TimberbornUnityGpuFieldRendererPresenter(logSink, options)
            : options.AshOverlayEnabled
                ? new TimberbornUnityGpuFieldRendererPresenter(logSink, options)
                : NullTimberbornGpuFieldRendererPresenter.Instance;
    }

    public void BeginVisualEffectDispatch(uint tick)
    {
        _currentTick = tick;
        _regionsThisDispatch.Clear();
        _updatedRegionsThisDispatch = 0;
        _droppedRegionsThisDispatch = 0;
        _invisibleRegionsThisDispatch = 0;
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
        TimberbornGpuFieldRendererRegionState[] candidateRegions = _regionsThisDispatch.Values
            .Select(static accumulator => accumulator.ToState())
            .ToArray();
        TimberbornGpuFieldRendererRegionState[] visibleCandidateRegions = candidateRegions
            .Where(region => region.Intensity >= Options.MinimumVisibleIntensity)
            .OrderByDescending(static region => region.Intensity)
            .ThenBy(static region => region.RegionId)
            .ToArray();
        TimberbornGpuFieldRendererRegionState[] updatedRegions = visibleCandidateRegions
            .Take(Options.MaxUpdatedRegionsPerDispatch)
            .ToArray();
        _updatedRegionsThisDispatch = updatedRegions.Length;
        int droppedByLimit = Math.Max(0, visibleCandidateRegions.Length - updatedRegions.Length);
        _droppedRegionsThisDispatch += droppedByLimit;
        _invisibleRegionsThisDispatch = candidateRegions.Length - visibleCandidateRegions.Length;

        Array.ForEach(updatedRegions, region => _visibleRegions[region.RegionId] = AccumulateRegion(region));
        if (Options.DebugOverlayEnabled)
        {
            int[] invisibleRegionIds = candidateRegions
                .Where(region => region.Intensity < Options.MinimumVisibleIntensity)
                .Select(static region => region.RegionId)
                .ToArray();
            Array.ForEach(invisibleRegionIds, regionId => _visibleRegions.Remove(regionId));
        }

        TimberbornGpuFieldRendererRegionState[] renderedRegions = _visibleRegions.Values
            .OrderByDescending(static region => region.Intensity)
            .ThenBy(static region => region.RegionId)
            .Take(Options.MaxUpdatedRegionsPerDispatch)
            .ToArray();
        TimberbornGpuFieldRendererPresentationResult presentationResult = _presenter.RenderRegions(renderedRegions);
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
            $"invisible_regions={_invisibleRegionsThisDispatch} " +
            $"material_failures={_materialFailuresThisDispatch} " +
            $"max_updated_regions={Options.MaxUpdatedRegionsPerDispatch} " +
            $"region_size={Options.RegionSize} " +
            $"renderer_enabled={_presenter.State.RendererEnabled.ToString().ToLowerInvariant()} " +
            $"material_ready={_presenter.State.MaterialReady.ToString().ToLowerInvariant()} " +
            $"visual_field_surface_bound={_visualFieldSurface.State.IsBound.ToString().ToLowerInvariant()}");
    }

    private TimberbornGpuFieldRendererRegionState AccumulateRegion(TimberbornGpuFieldRendererRegionState region)
    {
        if (Options.DebugOverlayEnabled || !_visibleRegions.TryGetValue(region.RegionId, out TimberbornGpuFieldRendererRegionState previous))
        {
            return region;
        }

        float accumulatedAsh = Math.Clamp(previous.Ash + region.Ash, 0f, 1f);
        return region with
        {
            Ash = accumulatedAsh,
            Visibility = Math.Max(previous.Visibility, region.Visibility),
            Intensity = accumulatedAsh * Math.Max(Math.Max(previous.Visibility, region.Visibility), 0.001f),
        };
    }

    public void Clear()
    {
        _regionsThisDispatch.Clear();
        _visibleRegions.Clear();
        _updatedRegionsThisDispatch = 0;
        _droppedRegionsThisDispatch = 0;
        _invisibleRegionsThisDispatch = 0;
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
        if (!Options.DebugOverlayEnabled && sample.Ash <= 0f)
        {
            return;
        }

        IEnumerable<(int X, int Y, int Z, float Weight)> affectedCells = Options.DebugOverlayEnabled || !Options.AshOverlayEnabled
            ? Enumerable.Repeat((X: x, Y: y, Z: z, Weight: 1f), 1)
            : GetAshBlendCells(binding, x, y, z, Options.AshBlendCellRadius)
                .Select(cell => _ashOverlaySurfaceProvider.TryProjectToSurfaceZ(
                    cell.X,
                    cell.Y,
                    z,
                    out int surfaceZ)
                        ? (cell.X, cell.Y, Z: surfaceZ, cell.Weight)
                        : ((int X, int Y, int Z, float Weight)?)null)
                .Where(static cell => cell.HasValue)
                .Select(static cell => cell!.Value);

        affectedCells
            .Select(cell => (
                cell.X,
                cell.Y,
                cell.Z,
                Sample: Options.DebugOverlayEnabled
                    ? sample
                    : WeightAshSample(sample, cell.Weight)))
            .Where(item => Options.DebugOverlayEnabled
                ? item.Sample.Ash > 0f || item.Sample.Fire > 0f || item.Sample.Smoke > 0f || item.Sample.Steam > 0f
                : item.Sample.Ash > 0f)
            .ToList()
            .ForEach(item => AddSampleToRegion(binding, effectEvent.Tick, item.X, item.Y, item.Z, item.Sample));
    }

    private void AddSampleToRegion(
        TimberbornGpuVisualFieldSurfaceBinding binding,
        uint tick,
        int x,
        int y,
        int z,
        TimberbornGpuVisualFieldSample sample)
    {
        int regionId = ToRegionId(binding, x, y, z, Options.RegionSize, Options.DebugOverlayEnabled);
        if (!_regionsThisDispatch.TryGetValue(regionId, out TimberbornGpuFieldRendererRegionAccumulator? accumulator))
        {
            if (_regionsThisDispatch.Count >= Options.MaxUpdatedRegionsPerDispatch)
            {
                _droppedRegionsThisDispatch++;
                return;
            }

            accumulator = new TimberbornGpuFieldRendererRegionAccumulator(
                regionId,
                tick,
                x,
                y,
                z,
                Options.DebugOverlayEnabled);
            _regionsThisDispatch[regionId] = accumulator;
        }

        accumulator.Add(x, y, z, sample);
    }

    private static IEnumerable<(int X, int Y, int Z, float Weight)> GetAshBlendCells(
        TimberbornGpuVisualFieldSurfaceBinding binding,
        int x,
        int y,
        int z,
        int radius)
    {
        if (radius == 1)
        {
            return GetOneTileAshBleedCells(binding, x, y, z);
        }

        int minX = Math.Max(0, x - radius);
        int maxX = Math.Min(binding.Width - 1, x + radius);
        int minY = Math.Max(0, y - radius);
        int maxY = Math.Min(binding.Height - 1, y + radius);

        return Enumerable.Range(minX, maxX - minX + 1)
            .SelectMany(blendX => Enumerable.Range(minY, maxY - minY + 1)
                .Select(blendY =>
                {
                    int distance = Math.Max(Math.Abs(blendX - x), Math.Abs(blendY - y));
                    float falloff = radius == 0
                        ? 1f
                        : SmoothStep01(Math.Clamp(1f - (distance / (radius + 1f)), 0f, 1f));
                    return (X: blendX, Y: blendY, Z: z, Weight: falloff);
                }))
            .Where(static cell => cell.Weight > 0.025f);
    }

    private static IEnumerable<(int X, int Y, int Z, float Weight)> GetOneTileAshBleedCells(
        TimberbornGpuVisualFieldSurfaceBinding binding,
        int x,
        int y,
        int z)
    {
        int minX = Math.Max(0, x - 1);
        int maxX = Math.Min(binding.Width - 1, x + 1);
        int minY = Math.Max(0, y - 1);
        int maxY = Math.Min(binding.Height - 1, y + 1);

        return Enumerable.Range(minX, maxX - minX + 1)
            .SelectMany(blendX => Enumerable.Range(minY, maxY - minY + 1)
                .Select(blendY =>
                {
                    int distance = Math.Max(Math.Abs(blendX - x), Math.Abs(blendY - y));
                    float weight = distance == 0
                        ? 1f
                        : Math.Abs(blendX - x) + Math.Abs(blendY - y) == 1
                            ? 0.45f
                            : 0.25f;
                    return (X: blendX, Y: blendY, Z: z, Weight: weight);
                }));
    }

    private static TimberbornGpuVisualFieldSample WeightAshSample(
        TimberbornGpuVisualFieldSample sample,
        float weight)
    {
        float clampedWeight = Math.Clamp(weight, 0f, 1f);
        return sample with
        {
            Fire = sample.Fire * clampedWeight * 0.2f,
            Smoke = sample.Smoke * clampedWeight * 0.35f,
            Ash = sample.Ash * clampedWeight,
            Steam = sample.Steam * clampedWeight * 0.35f,
            Visibility = Math.Max(sample.Visibility * clampedWeight, sample.Ash * clampedWeight),
        };
    }

    private static float SmoothStep01(float value)
    {
        float clamped = Math.Clamp(value, 0f, 1f);
        return clamped * clamped * (3f - (2f * clamped));
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

    private static int ToCellIndex(TimberbornGpuVisualFieldSurfaceBinding binding, int x, int y, int z)
    {
        return checked((z * binding.Width * binding.Height) + (y * binding.Width) + x);
    }

    private static int ToRegionId(
        TimberbornGpuVisualFieldSurfaceBinding binding,
        int x,
        int y,
        int z,
        int regionSize,
        bool debugOverlayEnabled)
    {
        int regionsX = checked((binding.Width + regionSize - 1) / regionSize);
        int regionsY = checked((binding.Height + regionSize - 1) / regionSize);
        int regionX = x / regionSize;
        int regionY = y / regionSize;
        int regionZ = debugOverlayEnabled ? z : 0;
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
        private readonly bool _debugOverlayEnabled;

        public TimberbornGpuFieldRendererRegionAccumulator(
            int regionId,
            uint tick,
            int x,
            int y,
            int z,
            bool debugOverlayEnabled)
        {
            RegionId = regionId;
            Tick = tick;
            MinX = MaxX = x;
            MinY = MaxY = y;
            MinZ = MaxZ = z;
            _debugOverlayEnabled = debugOverlayEnabled;
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
            _steam = Math.Max(_steam, Clamp01(sample.Steam));
        }

        public TimberbornGpuFieldRendererRegionState ToState()
        {
            float dominantField = _debugOverlayEnabled
                ? Math.Max(_fire, Math.Max(_smoke, Math.Max(_ash, Math.Max(_steam, _heatHaze))))
                : _ash;
            float intensity = dominantField * Math.Max(_visibility, 0.001f);
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
    private const int AshOverlayTextureSize = 4096;
    private const float AshOverlayTextureWorldSizeCells = 32f;
    private const string AshOverlayTextureResourceName = "Wildfire.Timberborn.Assets.WildfireAshGround2048.png";
    private const string AshOverlayMaskTextureResourceName = "Wildfire.Timberborn.Assets.WildfireAshMask4096Levels.png";
    private const string AshOverlayShaderBundleMac = "wildfire_visual_mac";
    private const string AshOverlayShaderBundleWindows = "wildfire_visual_win";
    private const string AshOverlayShaderAssetSuffix = "ashoverlay.shader";
    private const float AshOverlayMaxOpacity = 0.9f;
    private const float AshOverlaySigmoidSharpness = 14f;
    private const float AshOverlayThresholdLow = 0.08f;
    private const float AshOverlayThresholdHigh = 0.92f;
    private const int AshOverlayRenderQueueOffset = -100;
    private static readonly int MainTexturePropertyId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseMapPropertyId = Shader.PropertyToID("_BaseMap");
    private static readonly int AshTexturePropertyId = Shader.PropertyToID("_AshTex");
    private static readonly int MaskTexturePropertyId = Shader.PropertyToID("_MaskTex");
    private static readonly int MaxOpacityPropertyId = Shader.PropertyToID("_MaxOpacity");
    private static readonly int SigmoidSharpnessPropertyId = Shader.PropertyToID("_SigmoidSharpness");
    private static readonly int ThresholdLowPropertyId = Shader.PropertyToID("_ThresholdLow");
    private static readonly int ThresholdHighPropertyId = Shader.PropertyToID("_ThresholdHigh");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

    private readonly ITimberbornFireLogSink _logSink;
    private readonly float _heightOffset;
    private readonly bool _debugOverlayEnabled;
    private AssetBundle? _shaderBundle;
    private GameObject? _root;
    private Mesh? _mesh;
    private MeshRenderer? _renderer;
    private Material? _material;
    private Texture2D? _ashOverlayTexture;
    private Texture2D? _ashOverlayMaskTexture;
    private int _materialFailureCount;

    public TimberbornUnityGpuFieldRendererPresenter(ITimberbornFireLogSink logSink)
        : this(logSink, TimberbornGpuFieldRendererOptions.Default)
    {
    }

    public TimberbornUnityGpuFieldRendererPresenter(
        ITimberbornFireLogSink logSink,
        TimberbornGpuFieldRendererOptions options)
    {
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        TimberbornGpuFieldRendererOptions resolvedOptions =
            options ?? throw new ArgumentNullException(nameof(options));
        _heightOffset = resolvedOptions.DebugOverlayHeightOffset;
        _debugOverlayEnabled = resolvedOptions.DebugOverlayEnabled;
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

            BuildMesh(_mesh, regions, _heightOffset, _debugOverlayEnabled);
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

        if (_ashOverlayMaskTexture is not null)
        {
            if (!ReferenceEquals(_ashOverlayMaskTexture, _ashOverlayTexture))
            {
                UnityEngine.Object.Destroy(_ashOverlayMaskTexture);
            }

            _ashOverlayMaskTexture = null;
        }

        if (_ashOverlayTexture is not null)
        {
            UnityEngine.Object.Destroy(_ashOverlayTexture);
            _ashOverlayTexture = null;
        }

        if (_shaderBundle is not null)
        {
            _shaderBundle.Unload(unloadAllLoadedObjects: true);
            _shaderBundle = null;
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
        Shader? shader = _debugOverlayEnabled
            ? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent")
            : LoadAshOverlayShader();
        if (shader is null)
        {
            _logSink.Warning("wildfire_timberborn_gpu_field_renderer_material_unavailable shader=Wildfire/AshOverlay");
            throw new InvalidOperationException("No transparent shader was available for the GPU field renderer.");
        }

        _material = new Material(shader)
        {
            name = "Wildfire GPU Field Renderer Material",
            hideFlags = HideFlags.DontSave,
        };
        ConfigureTransparentMaterial(_material);
        _ashOverlayTexture = CreateTextureFromResource(
            AshOverlayTextureResourceName,
            "Wildfire_AshGround2048",
            TextureWrapMode.Repeat);
        _ashOverlayMaskTexture = _debugOverlayEnabled
            ? _ashOverlayTexture
            : CreateTextureFromResource(
                AshOverlayMaskTextureResourceName,
                "Wildfire_AshMask4096Levels",
                TextureWrapMode.Repeat);
        _material.SetTexture(MainTexturePropertyId, _ashOverlayTexture);
        _material.SetTexture(BaseMapPropertyId, _ashOverlayTexture);
        _material.SetTexture(AshTexturePropertyId, _ashOverlayTexture);
        _material.SetTexture(MaskTexturePropertyId, _ashOverlayMaskTexture);
        _material.SetFloat(MaxOpacityPropertyId, AshOverlayMaxOpacity);
        _material.SetFloat(SigmoidSharpnessPropertyId, AshOverlaySigmoidSharpness);
        _material.SetFloat(ThresholdLowPropertyId, AshOverlayThresholdLow);
        _material.SetFloat(ThresholdHighPropertyId, AshOverlayThresholdHigh);
        _material.SetColor(ColorPropertyId, Color.white);
        _material.SetColor(BaseColorPropertyId, Color.white);
        _material.renderQueue = _debugOverlayEnabled
            ? (int)UnityEngine.Rendering.RenderQueue.Transparent + 500
            : (int)UnityEngine.Rendering.RenderQueue.Transparent + AshOverlayRenderQueueOffset;
        _renderer.sharedMaterial = _material;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.sortingOrder = 2;
    }

    private static void BuildMesh(
        Mesh mesh,
        IReadOnlyList<TimberbornGpuFieldRendererRegionState> regions,
        float heightOffset,
        bool debugOverlayEnabled)
    {
        Vector3[] vertices = regions
            .SelectMany(region => ToVertices(region, heightOffset))
            .ToArray();
        Color[] colors = regions
            .SelectMany(region => ToColors(region, debugOverlayEnabled))
            .ToArray();
        Vector2[] uvs = regions
            .SelectMany(region => ToUvs(region, debugOverlayEnabled))
            .ToArray();
        int[] triangles = regions
            .SelectMany((_, index) =>
            {
                int offset = index * 4;
                return new[] { offset, offset + 2, offset + 1, offset + 2, offset + 3, offset + 1 };
            })
            .ToArray();

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private static IEnumerable<Vector3> ToVertices(
        TimberbornGpuFieldRendererRegionState region,
        float heightOffset)
    {
        AshOverlayQuadBounds bounds = ToAshOverlayQuadBounds(region);
        float y = region.MaxZ + heightOffset;

        return new[]
        {
            new Vector3(bounds.MinX, y, bounds.MinZ),
            new Vector3(bounds.MaxX, y, bounds.MinZ),
            new Vector3(bounds.MinX, y, bounds.MaxZ),
            new Vector3(bounds.MaxX, y, bounds.MaxZ),
        };
    }

    private static IEnumerable<Color> ToColors(TimberbornGpuFieldRendererRegionState region, bool debugOverlayEnabled)
    {
        Color center = debugOverlayEnabled
            ? ToDebugColor(region)
            : ToAshVertexColor(region);
        return Enumerable.Repeat(center, 4);
    }

    private static IEnumerable<Vector2> ToUvs(
        TimberbornGpuFieldRendererRegionState region,
        bool debugOverlayEnabled)
    {
        if (debugOverlayEnabled)
        {
            return new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };
        }

        AshOverlayQuadBounds bounds = ToAshOverlayQuadBounds(region);
        return new[]
        {
            new Vector2(bounds.MinX / AshOverlayTextureWorldSizeCells, bounds.MinZ / AshOverlayTextureWorldSizeCells),
            new Vector2(bounds.MaxX / AshOverlayTextureWorldSizeCells, bounds.MinZ / AshOverlayTextureWorldSizeCells),
            new Vector2(bounds.MinX / AshOverlayTextureWorldSizeCells, bounds.MaxZ / AshOverlayTextureWorldSizeCells),
            new Vector2(bounds.MaxX / AshOverlayTextureWorldSizeCells, bounds.MaxZ / AshOverlayTextureWorldSizeCells),
        };
    }

    private static AshOverlayQuadBounds ToAshOverlayQuadBounds(TimberbornGpuFieldRendererRegionState region)
    {
        float minX = region.MinX;
        float maxX = region.MaxX + 1f;
        float minZ = region.MinY;
        float maxZ = region.MaxY + 1f;
        return new AshOverlayQuadBounds(minX, maxX, minZ, maxZ);
    }

    private static Color ToAshVertexColor(TimberbornGpuFieldRendererRegionState region)
    {
        return new Color(1f, 1f, 1f, Math.Clamp(region.Ash, 0f, 1f));
    }

    private static Color ToDebugColor(TimberbornGpuFieldRendererRegionState region)
    {
        float red = Math.Clamp(region.Fire + region.HeatHaze * 0.4f + region.Ash * 0.2f, 0f, 1f);
        float green = Math.Clamp(region.Fire * 0.45f + region.Steam * 0.5f + region.Smoke * 0.25f, 0f, 1f);
        float blue = Math.Clamp(region.Steam * 0.75f + region.Smoke * 0.35f + region.Ash * 0.25f, 0f, 1f);
        float alpha = Math.Clamp(region.Intensity * 0.7f, 0f, 0.85f);
        return new Color(red, green, blue, alpha);
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }
    }

    private Shader? LoadAshOverlayShader()
    {
        Shader? loadedShader = Shader.Find("Wildfire/AshOverlay");
        if (loadedShader is not null)
        {
            return loadedShader;
        }

        string bundleName = GetPlatformAshOverlayShaderBundleName();
        string bundlePath = GetAssetBundlePath(bundleName);
        if (!File.Exists(bundlePath))
        {
            _logSink.Warning(
                $"wildfire_timberborn_ash_overlay_shader_missing bundle={bundleName} path=\"{EscapeLogValue(bundlePath)}\"");
            return null;
        }

        _shaderBundle = AssetBundle.LoadFromFile(bundlePath);
        if (_shaderBundle is null)
        {
            _logSink.Warning(
                $"wildfire_timberborn_ash_overlay_shader_load_failed bundle={bundleName} reason=null_bundle");
            return null;
        }

        string[] assetNames = _shaderBundle.GetAllAssetNames();
        string? shaderAssetName = assetNames.FirstOrDefault(static assetName =>
            assetName.EndsWith(AshOverlayShaderAssetSuffix, StringComparison.OrdinalIgnoreCase));
        if (shaderAssetName is null)
        {
            _logSink.Warning(
                $"wildfire_timberborn_ash_overlay_shader_missing_asset bundle={bundleName} assets={string.Join(",", assetNames)}");
            return null;
        }

        Shader? shader = _shaderBundle.LoadAsset<Shader>(shaderAssetName);
        if (shader is null)
        {
            _logSink.Warning(
                $"wildfire_timberborn_ash_overlay_shader_load_failed bundle={bundleName} asset={shaderAssetName} reason=null_shader");
            return null;
        }

        _logSink.Info(
            $"wildfire_timberborn_ash_overlay_shader_loaded bundle={bundleName} asset={shaderAssetName}");
        return shader;
    }

    private static Texture2D CreateTextureFromResource(
        string resourceName,
        string textureName,
        TextureWrapMode wrapMode)
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resourceName) ??
            throw new InvalidOperationException(
                $"Embedded ash overlay texture resource was not found: {resourceName}");
        using MemoryStream memoryStream = new();
        stream.CopyTo(memoryStream);
        Texture2D texture = new(
            AshOverlayTextureSize,
            AshOverlayTextureSize,
            TextureFormat.RGBA32,
            mipChain: true)
        {
            name = textureName,
            filterMode = FilterMode.Trilinear,
            wrapMode = wrapMode,
            hideFlags = HideFlags.DontSave,
        };
        if (!texture.LoadImage(memoryStream.ToArray(), markNonReadable: false))
        {
            UnityEngine.Object.Destroy(texture);
            throw new InvalidOperationException("Unity could not decode the embedded Wildfire ash overlay texture.");
        }

        texture.filterMode = FilterMode.Trilinear;
        texture.wrapMode = wrapMode;
        return texture;
    }

    private readonly record struct AshOverlayQuadBounds(float MinX, float MaxX, float MinZ, float MaxZ);

    private static string GetPlatformAshOverlayShaderBundleName()
    {
        return Application.platform switch
        {
            RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer => AshOverlayShaderBundleMac,
            RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer => AshOverlayShaderBundleWindows,
            _ => throw new PlatformNotSupportedException(
                $"Wildfire ash overlay AssetBundle is not packaged for Unity platform {Application.platform}."),
        };
    }

    private static string EscapeLogValue(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string GetAssetBundlePath(string bundleName)
    {
        string[] candidatePaths = GetCandidateModDirectories()
            .Select(directory => Path.Combine(directory, TimberbornComputeShaderLoader.PrivateBundleDirectoryName, bundleName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return candidatePaths.FirstOrDefault(File.Exists) ??
            candidatePaths.FirstOrDefault() ??
            throw new InvalidOperationException("Could not resolve a Wildfire ash overlay AssetBundle path.");
    }

    private static IEnumerable<string> GetCandidateModDirectories()
    {
        string? assemblyDirectory = TryGetDirectoryName(typeof(WildfireConfigurator).Assembly.Location);
        string? assemblyModDirectory = TryGetParentDirectory(assemblyDirectory);
        if (assemblyModDirectory != null)
        {
            yield return assemblyModDirectory;
        }

        string? homeDirectory = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrWhiteSpace(homeDirectory))
        {
            yield return Path.Combine(homeDirectory, "Documents", "Timberborn", "Mods", "Wildfire");
        }

        string documentsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(documentsDirectory))
        {
            yield return Path.Combine(documentsDirectory, "Timberborn", "Mods", "Wildfire");
        }
    }

    private static string? TryGetDirectoryName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            string resolvedPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(resolvedPath);
            return string.IsNullOrWhiteSpace(directory) ? null : directory;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetParentDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Directory.GetParent(path)?.FullName;
        }
        catch
        {
            return null;
        }
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

public sealed class NullTimberbornAshOverlaySurfaceProvider : ITimberbornAshOverlaySurfaceProvider
{
    public static readonly NullTimberbornAshOverlaySurfaceProvider Instance = new();

    private NullTimberbornAshOverlaySurfaceProvider()
    {
    }

    public bool TryProjectToSurfaceZ(int x, int y, int sourceZ, out int surfaceZ)
    {
        surfaceZ = sourceZ;
        return true;
    }
}

public sealed class TimberbornTerrainAshOverlaySurfaceProvider : ITimberbornAshOverlaySurfaceProvider
{
    private readonly MapSize _mapSize;
    private readonly ITerrainService _terrainService;

    public TimberbornTerrainAshOverlaySurfaceProvider(MapSize mapSize, ITerrainService terrainService)
    {
        _mapSize = mapSize ?? throw new ArgumentNullException(nameof(mapSize));
        _terrainService = terrainService ?? throw new ArgumentNullException(nameof(terrainService));
    }

    public bool TryProjectToSurfaceZ(int x, int y, int sourceZ, out int surfaceZ)
    {
        surfaceZ = sourceZ;
        Vector2Int cell = new(x, y);
        Vector3Int terrainSize = _mapSize.TerrainSize;
        if (x < 0 || y < 0 || x >= terrainSize.x || y >= terrainSize.y)
        {
            return false;
        }

        int[] surfaceHeights = _terrainService.GetAllHeightsInCell(cell)
            .Where(coordinates => coordinates.z >= 0 && coordinates.z < terrainSize.z)
            .Select(static coordinates => coordinates.z)
            .OrderByDescending(static z => z)
            .ToArray();
        if (surfaceHeights.Length == 0)
        {
            return false;
        }

        surfaceZ = surfaceHeights
            .Where(height => height <= sourceZ)
            .DefaultIfEmpty(surfaceHeights[0])
            .First();
        return true;
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
