using System.IO;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

// Renders fire, smoke, and steam directly from the GPU simulation buffers via
// Graphics.DrawProceduralIndirect — no CPU readback, no mesh rebuilds per frame.
//
// Flame:  5 tongue slots per cell, each a bent 4-faced pyramid (36 verts).  Additive blend.
// Smoke:  SmokePuffsPerCell staggered billboard quads per cell drawn with alpha blend.
// Steam:  SteamPuffsPerCell rising billboard quads per cell drawn with alpha blend.
//
// A smoothing compute pass runs each frame, lerping a _SmoothedFields buffer toward
// the live sim values so intensities ramp up/down rather than popping.
//
// Initialize() is called once after the simulator is created with a valid grid.
// OnUpdate() is called every game frame to run the smoothing pass and issue draw calls.
public sealed class TimberbornGpuIndirectFireRenderer : IDisposable
{
    public const string MacBundleName = "wildfire_effects_mac";
    public const string WindowsBundleName = "wildfire_effects_win";
    public const string PrivateBundleDirectoryName = "ComputeShaders";

    private const int MaxTonguesPerCell  = 5;
    private const int VertsPerTongue     = 36;  // 4 pyramid faces × 9 verts (bottom quad + top tri)
    private const int VertsPerCloud      = 6;
    private const int SmokePuffsPerCell  = 6;
    private const int SteamPuffsPerCell  = 3;

    // Asymmetric smoothing rates (exponential lerp per second).
    // Fast ramp-up for immediate ignition feedback; slow ramp-down for particle-lifetime feel.
    private const float FireUpSpeed   = 4.0f;
    private const float FireDownSpeed = 2.0f;
    private const float SmokeUpSpeed  = 3.0f;
    private const float SmokeDownSpeed = 1.0f;
    private const float SteamUpSpeed  = 1.45f;
    private const float SteamDownSpeed = 3.75f;

    private readonly TimberbornComputeFireSimulator _simulator;
    private readonly FireGrid _grid;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly ITimberbornWindProvider _windProvider;

    private ComputeBuffer? _cellPositionsBuffer;
    private ComputeBuffer? _smoothedFieldsBuffer;
    private ComputeBuffer? _flameArgsBuffer;
    private ComputeBuffer? _smokeArgsBuffer;
    private ComputeBuffer? _steamArgsBuffer;
    private Material?      _flameMaterial;
    private Material?      _smokeMaterial;
    private Material?      _steamMaterial;
    private ComputeShader? _smoothingShader;
    private int            _smoothingKernel;
    private AssetBundle?   _bundle;
    private Bounds         _gridBounds;
    private bool           _initialized;
    private bool           _disposed;

    public TimberbornGpuIndirectFireRenderer(
        TimberbornComputeFireSimulator simulator,
        FireGrid grid,
        ITimberbornFireLogSink logSink)
        : this(simulator, grid, logSink, NullTimberbornWindProvider.Instance)
    {
    }

    public TimberbornGpuIndirectFireRenderer(
        TimberbornComputeFireSimulator simulator,
        FireGrid grid,
        ITimberbornFireLogSink logSink,
        ITimberbornWindProvider windProvider)
    {
        _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
        _grid = grid;
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        _windProvider = windProvider ?? throw new ArgumentNullException(nameof(windProvider));
    }

    public bool IsInitialized => _initialized;

    public void Initialize()
    {
        if (_disposed || _initialized)
        {
            return;
        }

        try
        {
            LoadBundle();
            BuildCellPositionsBuffer();
            BuildSmoothedFieldsBuffer();
            BuildIndirectArgsBuffers();
            BuildMaterials();
            LoadSmoothingShader();
            _gridBounds = ComputeGridBounds();
            _initialized = true;
            _logSink.Info(
                "wildfire_timberborn_gpu_indirect_renderer_initialized " +
                $"cell_count={_grid.CellCount} " +
                $"width={_grid.Width} height={_grid.Height} depth={_grid.Depth}");
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                "wildfire_timberborn_gpu_indirect_renderer_init_failed " +
                $"message=\"{EscapeLogValue(exception.Message)}\"");
            DisposeGpuResources();
        }
    }

    public void OnUpdate()
    {
        if (!_initialized)
        {
            return;
        }

        // The atmospheric buffer swaps each simulation tick; re-bind before the smoothing dispatch.
        ComputeBuffer atmosphericBuffer = _simulator.CurrentAtmosphericFieldsBuffer;
        _smoothingShader!.SetBuffer(_smoothingKernel, "_AtmosphericFields", atmosphericBuffer);
        _smoothingShader.SetFloat("_DeltaTime", Time.deltaTime);

        // Smooth visual intensities toward live sim values (ramp-up/ramp-down).
        int threadGroups = (_grid.CellCount + 63) / 64;
        _smoothingShader.Dispatch(_smoothingKernel, threadGroups, 1, 1);

        BindWind();

        // GPU-driven draw calls — no per-frame CPU work below this line.
        Graphics.DrawProceduralIndirect(
            _flameMaterial!, _gridBounds, MeshTopology.Triangles, _flameArgsBuffer!);
        Graphics.DrawProceduralIndirect(
            _smokeMaterial!, _gridBounds, MeshTopology.Triangles, _smokeArgsBuffer!);
        Graphics.DrawProceduralIndirect(
            _steamMaterial!, _gridBounds, MeshTopology.Triangles, _steamArgsBuffer!);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DisposeGpuResources();
        UnloadBundle();
        _initialized = false;
        _disposed = true;
    }

    private void LoadBundle()
    {
        string bundleName = Application.platform switch
        {
            RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer => MacBundleName,
            RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer => WindowsBundleName,
            _ => throw new PlatformNotSupportedException(
                $"Wildfire effects AssetBundle is not packaged for Unity platform {Application.platform}."),
        };

        string bundlePath = GetBundlePath(bundleName);
        _logSink.Info(
            $"wildfire_timberborn_effects_bundle_load_started bundle={bundleName} path=\"{bundlePath}\"");

        if (!File.Exists(bundlePath))
        {
            throw new FileNotFoundException(
                "Wildfire effects shader AssetBundle was not deployed.", bundlePath);
        }

        _bundle = AssetBundle.LoadFromFile(bundlePath);
        if (_bundle == null)
        {
            throw new InvalidOperationException(
                $"Unity could not load the Wildfire effects AssetBundle at '{bundlePath}'.");
        }

        _logSink.Info(
            $"wildfire_timberborn_effects_bundle_loaded bundle={bundleName}");
    }

    private void BuildCellPositionsBuffer()
    {
        int width  = _grid.Width;
        int height = _grid.Height;
        int depth  = _grid.Depth;
        int cellCount = _grid.CellCount;

        Vector3[] positions = new Vector3[cellCount];
        for (int z = 0; z < depth; z++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = x + y * width + z * width * height;
                    // Timberborn (x=east, y=north, z=level) → Unity world (x=east, y=up, z=north)
                    positions[idx] = new Vector3(x + 0.5f, z, y + 0.5f);
                }
            }
        }

        _cellPositionsBuffer = new ComputeBuffer(cellCount, sizeof(float) * 3, ComputeBufferType.Structured);
        _cellPositionsBuffer.SetData(positions);
        _cellPositionsBuffer.name = "wildfire.cell_world_positions";
    }

    private void BuildSmoothedFieldsBuffer()
    {
        // float4 per cell: (smoothedFire, smoothedSmoke, smoothedSmokeContam, smoothedSteam).
        // Starts at zero; the smoothing compute fills it each frame.
        _smoothedFieldsBuffer = new ComputeBuffer(_grid.CellCount, sizeof(float) * 4, ComputeBufferType.Structured);
        _smoothedFieldsBuffer.name = "wildfire.smoothed_fields";
    }

    private void BuildIndirectArgsBuffers()
    {
        int cellCount = _grid.CellCount;

        // DrawProceduralIndirect non-indexed args: [vertexCount, instanceCount, startVertex, startInstance]
        _flameArgsBuffer = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);
        _flameArgsBuffer.SetData(new uint[]
        {
            (uint)VertsPerTongue,
            (uint)(cellCount * MaxTonguesPerCell),
            0u,
            0u,
        });
        _flameArgsBuffer.name = "wildfire.flame_indirect_args";

        _smokeArgsBuffer = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);
        _smokeArgsBuffer.SetData(new uint[]
        {
            (uint)VertsPerCloud,
            (uint)(cellCount * SmokePuffsPerCell),
            0u,
            0u,
        });
        _smokeArgsBuffer.name = "wildfire.smoke_indirect_args";

        _steamArgsBuffer = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);
        _steamArgsBuffer.SetData(new uint[]
        {
            (uint)VertsPerCloud,
            (uint)(cellCount * SteamPuffsPerCell),
            0u,
            0u,
        });
        _steamArgsBuffer.name = "wildfire.steam_indirect_args";
    }

    private void BuildMaterials()
    {
        Shader flameShader = LoadShader("WildfireFlame");
        Shader cloudShader = LoadShader("WildfireCloud");

        _flameMaterial = new Material(flameShader) { name = "wildfire_flame" };
        _flameMaterial.SetBuffer("_SmoothedFields",     _smoothedFieldsBuffer!);
        _flameMaterial.SetBuffer("_CellWorldPositions", _cellPositionsBuffer!);

        _smokeMaterial = new Material(cloudShader) { name = "wildfire_smoke" };
        _smokeMaterial.SetBuffer("_SmoothedFields",     _smoothedFieldsBuffer!);
        _smokeMaterial.SetBuffer("_CellWorldPositions", _cellPositionsBuffer!);
        _smokeMaterial.SetColor("_BaseColor",   new Color(0.27f, 0.28f, 0.27f));
        _smokeMaterial.SetColor("_ContamColor", new Color(0.35f, 0.05f, 0.10f));  // burgundy
        _smokeMaterial.SetFloat("_Radius",        1.14f);
        _smokeMaterial.SetFloat("_HeightOffset",  3.24f);
        _smokeMaterial.SetFloat("_MaxOpacity",    0.62f);
        _smokeMaterial.SetFloat("_IsSteam",       0f);
        _smokeMaterial.SetFloat("_PuffsPerCell",  (float)SmokePuffsPerCell);

        _steamMaterial = new Material(cloudShader) { name = "wildfire_steam" };
        _steamMaterial.SetBuffer("_SmoothedFields",     _smoothedFieldsBuffer!);
        _steamMaterial.SetBuffer("_CellWorldPositions", _cellPositionsBuffer!);
        _steamMaterial.SetColor("_BaseColor",   new Color(0.92f, 0.94f, 0.96f));
        _steamMaterial.SetFloat("_Radius",        0.66f);
        _steamMaterial.SetFloat("_HeightOffset",  0.1f);  // steam starts near ground
        _steamMaterial.SetFloat("_MaxSteamHeight", 1.45f);
        _steamMaterial.SetFloat("_MaxOpacity",    0.36f);
        _steamMaterial.SetFloat("_IsSteam",       1f);
        _steamMaterial.SetFloat("_PuffsPerCell",  (float)SteamPuffsPerCell);
    }

    private void BindWind()
    {
        FireSimWind wind = _windProvider.CurrentWind.Normalized();
        Vector4 cloudWind = new(wind.DirectionX, wind.DirectionY, wind.Strength, 0f);
        _smokeMaterial!.SetVector("_Wind", cloudWind);
        _steamMaterial!.SetVector("_Wind", cloudWind);
    }

    private void LoadSmoothingShader()
    {
        _smoothingShader = LoadComputeShader("WildfireSmoothing");
        _smoothingKernel = _smoothingShader.FindKernel("SmoothFields");

        _smoothingShader.SetBuffer(_smoothingKernel, "_VisualFields",   _simulator.VisualFieldsBuffer);
        _smoothingShader.SetBuffer(_smoothingKernel, "_SmoothedFields", _smoothedFieldsBuffer!);
        _smoothingShader.SetInt(   "_CellCount",   _grid.CellCount);
        _smoothingShader.SetFloat( "_FireUpSpeed",   FireUpSpeed);
        _smoothingShader.SetFloat( "_FireDownSpeed", FireDownSpeed);
        _smoothingShader.SetFloat( "_SmokeUpSpeed",  SmokeUpSpeed);
        _smoothingShader.SetFloat( "_SmokeDownSpeed",SmokeDownSpeed);
        _smoothingShader.SetFloat( "_SteamUpSpeed",  SteamUpSpeed);
        _smoothingShader.SetFloat( "_SteamDownSpeed",SteamDownSpeed);
    }

    private Shader LoadShader(string assetName)
    {
        string path = FindAssetPath(assetName + ".shader");
        Shader? shader = _bundle!.LoadAsset<Shader>(path);
        if (shader == null)
        {
            throw new InvalidOperationException(
                $"Unity loaded '{path}' but it was not a valid Shader asset.");
        }

        _logSink.Info($"wildfire_timberborn_effects_shader_loaded name={assetName}");
        return shader;
    }

    private ComputeShader LoadComputeShader(string assetName)
    {
        string path = FindAssetPath(assetName + ".compute");
        ComputeShader? shader = _bundle!.LoadAsset<ComputeShader>(path);
        if (shader == null)
        {
            throw new InvalidOperationException(
                $"Unity loaded '{path}' but it was not a valid ComputeShader asset.");
        }

        _logSink.Info($"wildfire_timberborn_effects_compute_loaded name={assetName}");
        return shader;
    }

    private string FindAssetPath(string fileName)
    {
        string[] assetPaths = _bundle!.GetAllAssetNames();
        string? path = assetPaths.FirstOrDefault(p =>
            p.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (path is null)
        {
            throw new InvalidOperationException(
                $"Wildfire effects AssetBundle did not contain {fileName}. " +
                $"Assets: {string.Join(",", assetPaths)}");
        }

        return path;
    }

    private Bounds ComputeGridBounds()
    {
        float cx = _grid.Width  * 0.5f;
        float cz = _grid.Height * 0.5f;
        float cy = _grid.Depth  * 0.5f + 2f;
        Vector3 center = new(cx, cy, cz);
        Vector3 size   = new(_grid.Width + 2f, _grid.Depth + 6f, _grid.Height + 2f);
        return new Bounds(center, size);
    }

    private void DisposeGpuResources()
    {
        _cellPositionsBuffer?.Dispose();
        _smoothedFieldsBuffer?.Dispose();
        _flameArgsBuffer?.Dispose();
        _smokeArgsBuffer?.Dispose();
        _steamArgsBuffer?.Dispose();
        _cellPositionsBuffer  = null;
        _smoothedFieldsBuffer = null;
        _flameArgsBuffer      = null;
        _smokeArgsBuffer      = null;
        _steamArgsBuffer      = null;
        _smoothingShader      = null;

        DestroyMaterial(ref _flameMaterial);
        DestroyMaterial(ref _smokeMaterial);
        DestroyMaterial(ref _steamMaterial);
    }

    private static void DestroyMaterial(ref Material? material)
    {
        if (material != null)
        {
            UnityEngine.Object.Destroy(material);
            material = null;
        }
    }

    private void UnloadBundle()
    {
        _bundle?.Unload(unloadAllLoadedObjects: true);
        _bundle = null;
    }

    private static string GetBundlePath(string bundleName)
    {
        string? assemblyDir = TryGetDirectoryName(typeof(WildfireConfigurator).Assembly.Location);
        string? modDir = TryGetParentDirectory(assemblyDir);
        if (modDir != null)
        {
            string candidate = Path.Combine(modDir, PrivateBundleDirectoryName, bundleName);
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
                PrivateBundleDirectoryName, bundleName);
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
                PrivateBundleDirectoryName, bundleName);
        }

        throw new InvalidOperationException(
            $"Could not resolve a path for Wildfire effects AssetBundle '{bundleName}'.");
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
            string? dir = Path.GetDirectoryName(resolved);
            return string.IsNullOrWhiteSpace(dir) ? null : dir;
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
