using System.IO;
using System.Reflection;
using UnityEngine;

namespace Wildfire.Timberborn.Visuals;

public sealed class TimberbornGpuFieldRendererOptions
{
    public static readonly TimberbornGpuFieldRendererOptions Default = new();

    public TimberbornGpuFieldRendererOptions(
        int RegionSize = 1,
        int MaxUpdatedRegionsPerDispatch = 2048,
        float MinimumVisibleIntensity = 0.01f,
        bool AshOverlayEnabled = true,
        bool DebugOverlayEnabled = false,
        float DebugOverlayHeightOffset = 0.02f,
        bool IndirectFireRendererActive = false)
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
        this.AshOverlayEnabled = AshOverlayEnabled;
        this.DebugOverlayEnabled = DebugOverlayEnabled;
        this.DebugOverlayHeightOffset = DebugOverlayHeightOffset;
        this.IndirectFireRendererActive = IndirectFireRendererActive;
    }

    public int RegionSize { get; }

    public int MaxUpdatedRegionsPerDispatch { get; }

    public float MinimumVisibleIntensity { get; }

    public bool AshOverlayEnabled { get; }

    public bool DebugOverlayEnabled { get; }

    public float DebugOverlayHeightOffset { get; }

    // When true, fire/smoke/steam are rendered by TimberbornGpuIndirectFireRenderer; this
    // sink only needs to run for cells that carry ash (skipping the per-cell GetData stall
    // for every other visual-effect event).
    public bool IndirectFireRendererActive { get; }
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

public readonly record struct TimberbornGpuFieldRendererPresentation(
    object? MaterialFieldsBuffer = null,
    object? TransportFieldsBuffer = null,
    int GridWidth = 0,
    int GridHeight = 0,
    int GridDepth = 0)
{
    public object? CompanionFieldsBuffer => MaterialFieldsBuffer;

    public object? AtmosphericFieldsBuffer => TransportFieldsBuffer;
}

public interface ITimberbornGpuFieldRendererPresenter
{
    TimberbornGpuFieldRendererPresenterState State { get; }

    TimberbornGpuFieldRendererPresentationResult RenderRegions(
        IReadOnlyList<TimberbornGpuFieldRendererRegionState> regions);

    TimberbornGpuFieldRendererPresentationResult RenderPresentation(
        TimberbornGpuFieldRendererPresentation presentation);

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
    private int _materialFailuresThisDispatch;

    public TimberbornGpuFieldRendererSink(
        ITimberbornGpuVisualFieldSurface visualFieldSurface,
        ITimberbornFireLogSink logSink)
        : this(
            visualFieldSurface,
            logSink,
            TimberbornGpuFieldRendererOptions.Default,
            CreateDefaultPresenter(logSink, TimberbornGpuFieldRendererOptions.Default))
    {
    }

    public TimberbornGpuFieldRendererSink(
        ITimberbornGpuVisualFieldSurface visualFieldSurface,
        ITimberbornFireLogSink logSink,
        TimberbornGpuFieldRendererOptions options)
        : this(visualFieldSurface, logSink, options, CreateDefaultPresenter(logSink, options))
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
        VisibleRegionCount: 0,
        UpdatedRegionCount: 0,
        LastNonZeroUpdatedRegionCount: 0,
        MaxUpdatedRegionCount: 0,
        DroppedRegionCount: 0,
        InvisibleRegionCount: 0,
        MaterialFailureCount: _materialFailuresThisDispatch,
        LastUpdatedTick: null,
        LastNonZeroUpdatedRegionTick: null);

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
        _materialFailuresThisDispatch = 0;
    }

    public void UpdateVisualEffect(TimberbornFireVisualEffectEvent effectEvent)
    {
    }

    public void CompleteVisualEffectDispatch(uint tick)
    {
        bool hasRenderBinding = _visualFieldSurface.TryGetBinding(out TimberbornGpuVisualFieldSurfaceBinding renderBinding);
        TimberbornGpuFieldRendererPresentation presentation = new(
            MaterialFieldsBuffer: hasRenderBinding ? renderBinding.MaterialFieldsBuffer : null,
            TransportFieldsBuffer: hasRenderBinding ? renderBinding.TransportFieldsBuffer : null,
            GridWidth: hasRenderBinding ? renderBinding.Width : 0,
            GridHeight: hasRenderBinding ? renderBinding.Height : 0,
            GridDepth: hasRenderBinding ? renderBinding.Depth : 0);
        TimberbornGpuFieldRendererPresentationResult presentationResult = _presenter.RenderPresentation(presentation);
        if (presentationResult.Status == TimberbornGpuFieldRendererPresentationStatus.Failed)
        {
            _materialFailuresThisDispatch++;
            _logSink.Warning(
                "wildfire_timberborn_gpu_field_renderer_failed " +
                $"stage=presenter tick={tick} message=\"{EscapeLogValue(presentationResult.Message ?? "presentation failed")}\"");
        }

        _logSink.Info(
            "wildfire_timberborn_gpu_field_renderer_updated " +
            $"tick={tick} " +
            $"material_failures={_materialFailuresThisDispatch} " +
            $"renderer_enabled={_presenter.State.RendererEnabled.ToString().ToLowerInvariant()} " +
            $"material_ready={_presenter.State.MaterialReady.ToString().ToLowerInvariant()} " +
            $"visual_field_surface_bound={_visualFieldSurface.State.IsBound.ToString().ToLowerInvariant()}");
    }

    public void Clear()
    {
        _materialFailuresThisDispatch = 0;
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

    private static string EscapeLogValue(string value)
    {
        return value.Replace('\\', '/').Replace('"', '\'');
    }
}

public sealed class TimberbornUnityGpuFieldRendererPresenter : ITimberbornGpuFieldRendererPresenter
{
    private const int AshOverlayTextureSize = 2048;
    private const float AshOverlayTextureWorldSizeCells = 16f;
    private const string AshOverlayTextureResourceName = "Wildfire.Timberborn.Assets.WildfireAshGround2048.png";
    private const string AshOverlayMaskTextureResourceName = "Wildfire.Timberborn.Assets.WildfireAshMask2048Levels.png";
    private const string AshOverlayShaderBundleMac = "wildfire_visual_mac";
    private const string AshOverlayShaderBundleWindows = "wildfire_visual_win";
    private const string AshOverlayShaderAssetSuffix = "ashoverlay.shader";
    private const float AshOverlayMaxOpacity = 0.9f;
    private const float AshOverlayLevel1Coverage = 0.30f;
    private const float AshOverlayLevel2Coverage = 0.65f;
    private const float AshOverlayLevel3Coverage = 0.92f;
    private const float AshOverlayCoverageSoftness = 0.065f;
    private const int AshOverlayRenderQueueOffset = -10;
    private static readonly int MainTexturePropertyId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseMapPropertyId = Shader.PropertyToID("_BaseMap");
    private static readonly int AshTexturePropertyId = Shader.PropertyToID("_AshTex");
    private static readonly int MaskTexturePropertyId = Shader.PropertyToID("_MaskTex");
    private static readonly int CompanionFieldsPropertyId = Shader.PropertyToID("_CompanionFields");
    private static readonly int AtmosphericFieldsPropertyId = Shader.PropertyToID("_AtmosphericFields");
    private static readonly int UseCompanionAshPropertyId = Shader.PropertyToID("_UseCompanionAsh");
    private static readonly int UseAtmosphericAshPropertyId = Shader.PropertyToID("_UseAtmosphericAsh");
    private static readonly int GridWidthPropertyId = Shader.PropertyToID("_GridWidth");
    private static readonly int GridHeightPropertyId = Shader.PropertyToID("_GridHeight");
    private static readonly int GridDepthPropertyId = Shader.PropertyToID("_GridDepth");
    private static readonly int MaxOpacityPropertyId = Shader.PropertyToID("_MaxOpacity");
    private static readonly int Level1CoveragePropertyId = Shader.PropertyToID("_Level1Coverage");
    private static readonly int Level2CoveragePropertyId = Shader.PropertyToID("_Level2Coverage");
    private static readonly int Level3CoveragePropertyId = Shader.PropertyToID("_Level3Coverage");
    private static readonly int CoverageSoftnessPropertyId = Shader.PropertyToID("_CoverageSoftness");
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
        return RenderPresentation(new TimberbornGpuFieldRendererPresentation());
    }

    public TimberbornGpuFieldRendererPresentationResult RenderPresentation(
        TimberbornGpuFieldRendererPresentation presentation)
    {
        try
        {
            EnsureObjects();
            if (_mesh is null)
            {
                return TimberbornGpuFieldRendererPresentationResult.Disabled("mesh_unavailable");
            }

            BindAshPresentation(presentation);
            BuildMesh(_mesh, presentation, _heightOffset, _debugOverlayEnabled);
            if (_root is not null)
            {
                bool hasAshPresentation = !_debugOverlayEnabled &&
                    presentation.MaterialFieldsBuffer is not null &&
                    presentation.TransportFieldsBuffer is not null &&
                    presentation.GridWidth > 0 &&
                    presentation.GridHeight > 0 &&
                    presentation.GridDepth > 0;
                _root.SetActive(hasAshPresentation);
            }

            return TimberbornGpuFieldRendererPresentationResult.Applied;
        }
        catch (Exception exception)
        {
            _materialFailureCount++;
            return TimberbornGpuFieldRendererPresentationResult.Failed(exception.Message);
        }
    }

    private void BindAshPresentation(TimberbornGpuFieldRendererPresentation presentation)
    {
        if (_material is null)
        {
            return;
        }

        bool useCompanionAsh = !_debugOverlayEnabled &&
            presentation.MaterialFieldsBuffer is not null &&
            presentation.GridWidth > 0 &&
            presentation.GridHeight > 0 &&
            presentation.GridDepth > 0;
        bool useAtmosphericAsh = !_debugOverlayEnabled &&
            presentation.TransportFieldsBuffer is not null &&
            presentation.GridWidth > 0 &&
            presentation.GridHeight > 0 &&
            presentation.GridDepth > 0;
        _material.SetFloat(UseCompanionAshPropertyId, useCompanionAsh ? 1f : 0f);
        _material.SetFloat(UseAtmosphericAshPropertyId, useAtmosphericAsh ? 1f : 0f);
        _material.SetInt(GridWidthPropertyId, Math.Max(0, presentation.GridWidth));
        _material.SetInt(GridHeightPropertyId, Math.Max(0, presentation.GridHeight));
        _material.SetInt(GridDepthPropertyId, Math.Max(0, presentation.GridDepth));

        if (!useCompanionAsh)
        {
            return;
        }

        switch (presentation.MaterialFieldsBuffer)
        {
            case ComputeBuffer computeBuffer:
                _material.SetBuffer(CompanionFieldsPropertyId, computeBuffer);
                break;
            default:
                _material.SetFloat(UseCompanionAshPropertyId, 0f);
                break;
        }

        switch (presentation.TransportFieldsBuffer)
        {
            case ComputeBuffer computeBuffer:
                _material.SetBuffer(AtmosphericFieldsPropertyId, computeBuffer);
                break;
            default:
                _material.SetFloat(UseAtmosphericAshPropertyId, 0f);
                break;
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

        if (_ashOverlayTexture is not null)
        {
            UnityEngine.Object.Destroy(_ashOverlayTexture);
            _ashOverlayTexture = null;
        }

        if (_ashOverlayMaskTexture is not null)
        {
            UnityEngine.Object.Destroy(_ashOverlayMaskTexture);
            _ashOverlayMaskTexture = null;
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
            throw new InvalidOperationException("The ash overlay shader was not available for the GPU field renderer.");
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
                "Wildfire_AshMask2048Levels",
                TextureWrapMode.Repeat);
        _material.SetTexture(MainTexturePropertyId, _ashOverlayTexture);
        _material.SetTexture(BaseMapPropertyId, _ashOverlayTexture);
        _material.SetTexture(AshTexturePropertyId, _ashOverlayTexture);
        _material.SetTexture(MaskTexturePropertyId, _ashOverlayMaskTexture);
        _material.SetFloat(MaxOpacityPropertyId, AshOverlayMaxOpacity);
        _material.SetFloat(Level1CoveragePropertyId, AshOverlayLevel1Coverage);
        _material.SetFloat(Level2CoveragePropertyId, AshOverlayLevel2Coverage);
        _material.SetFloat(Level3CoveragePropertyId, AshOverlayLevel3Coverage);
        _material.SetFloat(CoverageSoftnessPropertyId, AshOverlayCoverageSoftness);
        _material.SetColor(ColorPropertyId, Color.white);
        _material.SetColor(BaseColorPropertyId, Color.white);
        _material.renderQueue = _debugOverlayEnabled
            ? (int)UnityEngine.Rendering.RenderQueue.Transparent + 500
            : (int)UnityEngine.Rendering.RenderQueue.Transparent + AshOverlayRenderQueueOffset;
        _renderer.sharedMaterial = _material;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.sortingOrder = -100;
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

    private static void BuildMesh(
        Mesh mesh,
        TimberbornGpuFieldRendererPresentation presentation,
        float heightOffset,
        bool debugOverlayEnabled)
    {
        AshFieldCellQuad[] ashFieldQuads = SelectAshFieldQuads(presentation).ToArray();
        Vector3[] vertices = ashFieldQuads
            .SelectMany(quad => quad.ToVertices(heightOffset))
            .ToArray();
        Color[] colors = Enumerable.Repeat(new Color(1f, 1f, 1f, 0f), vertices.Length).ToArray();
        Vector2[] uvs = ashFieldQuads
            .SelectMany(quad => quad.ToPatternUvs())
            .ToArray();
        Vector2[] uv2s = ashFieldQuads
            .SelectMany(static quad => quad.ToCellIndexUvs())
            .ToArray();
        int[] triangles = ashFieldQuads
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
        mesh.uv2 = uv2s;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
    }

    private static IEnumerable<AshFieldCellQuad> SelectAshFieldQuads(TimberbornGpuFieldRendererPresentation presentation)
    {
        if (presentation.MaterialFieldsBuffer is null ||
            presentation.TransportFieldsBuffer is null ||
            presentation.GridWidth <= 0 ||
            presentation.GridHeight <= 0 ||
            presentation.GridDepth <= 0)
        {
            yield break;
        }

        for (int z = 0; z < presentation.GridDepth; z++)
        {
            yield return new AshFieldCellQuad(
                checked(z * presentation.GridWidth * presentation.GridHeight),
                0,
                0,
                presentation.GridWidth - 1,
                presentation.GridHeight - 1,
                z);
        }
    }

    private readonly record struct AshFieldCellQuad(int CellIndex, int MinX, int MinY, int MaxX, int MaxY, int Z)
    {
        public IEnumerable<Vector3> ToVertices(float heightOffset)
        {
            float y = Z + heightOffset;
            return new[]
            {
                new Vector3(MinX, y, MinY),
                new Vector3(MaxX + 1f, y, MinY),
                new Vector3(MinX, y, MaxY + 1f),
                new Vector3(MaxX + 1f, y, MaxY + 1f),
            };
        }

        public IEnumerable<Vector2> ToPatternUvs()
        {
            return new[]
            {
                new Vector2(MinX / AshOverlayTextureWorldSizeCells, MinY / AshOverlayTextureWorldSizeCells),
                new Vector2((MaxX + 1f) / AshOverlayTextureWorldSizeCells, MinY / AshOverlayTextureWorldSizeCells),
                new Vector2(MinX / AshOverlayTextureWorldSizeCells, (MaxY + 1f) / AshOverlayTextureWorldSizeCells),
                new Vector2((MaxX + 1f) / AshOverlayTextureWorldSizeCells, (MaxY + 1f) / AshOverlayTextureWorldSizeCells),
            };
        }

        public IEnumerable<Vector2> ToCellIndexUvs()
        {
            // uv2.x = 0 tells the shader to use the companion buffer; uv2.y = Z layer
            return Enumerable.Repeat(new Vector2(0f, Z), 4);
        }
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
        Shader? loaded = Shader.Find("Wildfire/AshOverlay");
        if (loaded is not null)
        {
            return loaded;
        }

        string bundleName = Application.platform switch
        {
            RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer => AshOverlayShaderBundleMac,
            RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer => AshOverlayShaderBundleWindows,
            _ => throw new PlatformNotSupportedException(
                $"Wildfire ash overlay AssetBundle is not packaged for Unity platform {Application.platform}."),
        };
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
        string? shaderAssetName = assetNames.FirstOrDefault(name =>
            name.EndsWith(AshOverlayShaderAssetSuffix, StringComparison.OrdinalIgnoreCase));
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

    private static string GetAssetBundlePath(string bundleName)
    {
        string? assemblyDir = TryGetDirectoryName(typeof(WildfireConfigurator).Assembly.Location);
        string? modDir = TryGetParentDirectory(assemblyDir);
        if (modDir is not null)
        {
            string candidate = Path.Combine(modDir, TimberbornGpuIndirectFireRenderer.PrivateBundleDirectoryName, bundleName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string home = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(home))
        {
            string candidate = Path.Combine(
                home, "Documents", "Timberborn", "Mods", "Wildfire",
                TimberbornGpuIndirectFireRenderer.PrivateBundleDirectoryName, bundleName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(docs))
        {
            return Path.Combine(
                docs, "Timberborn", "Mods", "Wildfire",
                TimberbornGpuIndirectFireRenderer.PrivateBundleDirectoryName, bundleName);
        }

        throw new InvalidOperationException(
            $"Could not resolve a path for Wildfire ash overlay AssetBundle '{bundleName}'.");
    }

    private static string? TryGetDirectoryName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            string resolved = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
            return Path.GetDirectoryName(resolved);
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

    private static string EscapeLogValue(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
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

    public TimberbornGpuFieldRendererPresentationResult RenderPresentation(
        TimberbornGpuFieldRendererPresentation presentation)
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
