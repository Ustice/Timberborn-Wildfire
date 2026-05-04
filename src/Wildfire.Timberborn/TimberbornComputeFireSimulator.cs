using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornComputeFireSimulatorFactory : ITimberbornFireSimulatorFactory, IDisposable
{
    private readonly ITimberbornFireLogSink _logSink;
    private readonly TimberbornComputeShaderLoader _shaderLoader;
    private readonly ITimberbornGpuVisualFieldSurface _visualFieldSurface;

    public TimberbornComputeFireSimulatorFactory(ITimberbornGpuVisualFieldSurface visualFieldSurface)
    {
        _logSink = new UnityTimberbornFireLogSink();
        _shaderLoader = new TimberbornComputeShaderLoader(_logSink);
        _visualFieldSurface = visualFieldSurface ?? throw new ArgumentNullException(nameof(visualFieldSurface));
    }

    public ITimberbornGpuVisualFieldSurface VisualFieldSurface => _visualFieldSurface;

    public IGpuFireSimulator Create(FireGrid grid, ReadOnlySpan<ushort> initialCells)
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            throw new InvalidOperationException("The current Timberborn graphics device does not support compute shaders.");
        }

        _logSink.Info("wildfire_timberborn_gpu_factory_created backend=unity_compute");
        ComputeShader shader = _shaderLoader.Load();
        TimberbornComputeFireSimulator simulator = new(grid, initialCells, shader, _logSink, _visualFieldSurface);
        _logSink.Info(
            $"wildfire_timberborn_gpu_simulator_created width={grid.Width} height={grid.Height} depth={grid.Depth} cell_count={grid.CellCount}");

        return simulator;
    }

    public void Dispose()
    {
        _shaderLoader.Dispose();
    }
}

public sealed class TimberbornComputeShaderLoader : IDisposable
{
    public const string MacBundleName = "wildfire_compute_mac";
    public const string WindowsBundleName = "wildfire_compute_win";
    public const string MacDiagnosticBundleName = "wildfire_diagnostic_mac";
    public const string WindowsDiagnosticBundleName = "wildfire_diagnostic_win";
    public const string PrivateBundleDirectoryName = "ComputeShaders";

    private readonly ITimberbornFireLogSink _logSink;
    private AssetBundle? _assetBundle;
    private ComputeShader? _shader;
    private bool _ownsAssetBundle;

    public TimberbornComputeShaderLoader(ITimberbornFireLogSink logSink)
    {
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
    }

    public ComputeShader Load()
    {
        if (_shader != null)
        {
            return _shader;
        }

        string bundleName = GetPlatformBundleName();
        string bundlePath = GetBundlePath(bundleName);
        _logSink.Info($"wildfire_timberborn_compute_asset_load_started bundle={bundleName} path=\"{bundlePath}\"");
        _logSink.Info(
            $"wildfire_timberborn_compute_asset_context unity_version=\"{Application.unityVersion}\" platform={Application.platform} supports_compute_shaders={SystemInfo.supportsComputeShaders}");
        ProbeDiagnosticTextBundle();

        if (!File.Exists(bundlePath))
        {
            throw new FileNotFoundException("Wildfire compute shader AssetBundle was not deployed.", bundlePath);
        }

        try
        {
            _assetBundle = TryFindLoadedComputeBundle(bundleName);
            if (_assetBundle != null)
            {
                _ownsAssetBundle = false;
                _logSink.Info(
                    $"wildfire_timberborn_compute_asset_reused bundle={bundleName} loaded_bundle=\"{_assetBundle.name}\"");
            }
            else
            {
                _assetBundle = AssetBundle.LoadFromFile(bundlePath);
                _ownsAssetBundle = _assetBundle != null;
            }

            if (_assetBundle == null)
            {
                throw new InvalidOperationException($"Unity could not load the Wildfire compute shader AssetBundle at '{bundlePath}'.");
            }

            string[] assetNames = _assetBundle.GetAllAssetNames();
            string? shaderAssetName = FindFireSimAssetName(assetNames);
            if (shaderAssetName is null)
            {
                throw new InvalidOperationException(
                    $"Wildfire compute shader AssetBundle '{bundlePath}' did not contain FireSim.compute. Assets: {string.Join(",", assetNames)}");
            }

            _shader = _assetBundle.LoadAsset<ComputeShader>(shaderAssetName);
            if (_shader == null)
            {
                throw new InvalidOperationException(
                    $"Wildfire compute shader asset '{shaderAssetName}' was present but did not load as a ComputeShader.");
            }

            _shader.FindKernel(TimberbornComputeFireSimulator.ApplyExternalChangesKernelName);
            _shader.FindKernel(TimberbornComputeFireSimulator.FullGridKernelName);
            _logSink.Info(
                $"wildfire_timberborn_compute_asset_loaded bundle={bundleName} asset={shaderAssetName}");
            return _shader;
        }
        catch
        {
            UnloadOwnedAssetBundle();
            throw;
        }
    }

    public void Dispose()
    {
        UnloadOwnedAssetBundle();
    }

    public static TimberbornComputeShaderBundleProbe ProbeDeployedBundles()
    {
        string computeBundleName = GetPlatformBundleName();
        string diagnosticBundleName = GetPlatformDiagnosticBundleName();
        string computeBundlePath = GetBundlePath(computeBundleName);
        string diagnosticBundlePath = GetBundlePath(diagnosticBundleName);
        TimberbornAssetBundleFileProbe computeProbe = ProbeAssetBundleFile(computeBundlePath);
        TimberbornAssetBundleFileProbe diagnosticProbe = ProbeAssetBundleFile(diagnosticBundlePath);

        return new TimberbornComputeShaderBundleProbe(
            computeBundleName,
            computeBundlePath,
            computeProbe.Exists,
            computeProbe.SizeBytes,
            computeProbe.Header,
            computeProbe.ReadError,
            diagnosticBundleName,
            diagnosticBundlePath,
            diagnosticProbe.Exists,
            diagnosticProbe.SizeBytes,
            diagnosticProbe.Header,
            diagnosticProbe.ReadError);
    }

    private static TimberbornAssetBundleFileProbe ProbeAssetBundleFile(string path)
    {
        if (!File.Exists(path))
        {
            return new TimberbornAssetBundleFileProbe(
                Exists: false,
                SizeBytes: null,
                Header: null,
                ReadError: null);
        }

        try
        {
            FileInfo fileInfo = new(path);
            byte[] headerBytes = new byte[16];
            using FileStream stream = File.OpenRead(path);
            int bytesRead = stream.Read(headerBytes, 0, headerBytes.Length);
            string header = Encoding.ASCII.GetString(headerBytes, 0, bytesRead).TrimEnd('\0');

            return new TimberbornAssetBundleFileProbe(
                Exists: true,
                SizeBytes: fileInfo.Length,
                Header: header,
                ReadError: null);
        }
        catch (Exception exception)
        {
            return new TimberbornAssetBundleFileProbe(
                Exists: true,
                SizeBytes: null,
                Header: null,
                ReadError: exception.Message);
        }
    }

    private AssetBundle? TryFindLoadedComputeBundle(string bundleName)
    {
        AssetBundle[] loadedBundles = AssetBundle.GetAllLoadedAssetBundles().ToArray();
        _logSink.Info($"wildfire_timberborn_compute_asset_loaded_bundle_scan count={loadedBundles.Length}");

        foreach (AssetBundle loadedBundle in loadedBundles)
        {
            string[] assetNames = loadedBundle.GetAllAssetNames();
            string? shaderAssetName = FindFireSimAssetName(assetNames);
            if (shaderAssetName != null)
            {
                _logSink.Info(
                    $"wildfire_timberborn_compute_asset_loaded_bundle_match bundle={bundleName} loaded_bundle=\"{loadedBundle.name}\" asset={shaderAssetName}");
                return loadedBundle;
            }

            if (string.Equals(loadedBundle.name, bundleName, StringComparison.OrdinalIgnoreCase))
            {
                _logSink.Info(
                    $"wildfire_timberborn_compute_asset_loaded_bundle_name_conflict bundle={bundleName} assets={string.Join(",", assetNames)}");
            }
        }

        return null;
    }

    private void ProbeDiagnosticTextBundle()
    {
        string bundleName = GetPlatformDiagnosticBundleName();
        string bundlePath = GetBundlePath(bundleName);
        _logSink.Info($"wildfire_timberborn_diagnostic_asset_load_started bundle={bundleName} path=\"{bundlePath}\"");

        if (!File.Exists(bundlePath))
        {
            _logSink.Warning($"wildfire_timberborn_diagnostic_asset_missing bundle={bundleName} path=\"{bundlePath}\"");
            return;
        }

        AssetBundle? diagnosticBundle = null;
        try
        {
            diagnosticBundle = AssetBundle.LoadFromFile(bundlePath);
            if (diagnosticBundle == null)
            {
                _logSink.Warning($"wildfire_timberborn_diagnostic_asset_load_failed bundle={bundleName} reason=null_bundle");
                return;
            }

            string[] assetNames = diagnosticBundle.GetAllAssetNames();
            string? diagnosticAssetName = FindDiagnosticAssetName(assetNames);
            if (diagnosticAssetName is null)
            {
                _logSink.Warning(
                    $"wildfire_timberborn_diagnostic_asset_missing_text bundle={bundleName} assets={string.Join(",", assetNames)}");
                return;
            }

            TextAsset? diagnosticAsset = diagnosticBundle.LoadAsset<TextAsset>(diagnosticAssetName);
            if (diagnosticAsset == null)
            {
                _logSink.Warning(
                    $"wildfire_timberborn_diagnostic_asset_load_failed bundle={bundleName} asset={diagnosticAssetName} reason=null_text_asset");
                return;
            }

            _logSink.Info(
                $"wildfire_timberborn_diagnostic_asset_loaded bundle={bundleName} asset={diagnosticAssetName} text_length={diagnosticAsset.text.Length}");
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                $"wildfire_timberborn_diagnostic_asset_load_failed bundle={bundleName} message=\"{EscapeLogValue(exception.Message)}\"");
        }
        finally
        {
            diagnosticBundle?.Unload(unloadAllLoadedObjects: true);
        }
    }

    private void UnloadOwnedAssetBundle()
    {
        if (_assetBundle != null && _ownsAssetBundle)
        {
            _assetBundle.Unload(unloadAllLoadedObjects: true);
        }

        _assetBundle = null;
        _shader = null;
        _ownsAssetBundle = false;
    }

    private static string? FindFireSimAssetName(IEnumerable<string> assetNames)
    {
        return assetNames.FirstOrDefault(static name =>
            name.EndsWith("firesim.compute", StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindDiagnosticAssetName(IEnumerable<string> assetNames)
    {
        return assetNames.FirstOrDefault(static name =>
            name.EndsWith("diagnostic.txt", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetPlatformBundleName()
    {
        return Application.platform switch
        {
            RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer => MacBundleName,
            RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer => WindowsBundleName,
            _ => throw new PlatformNotSupportedException(
                $"Wildfire compute AssetBundle is not packaged for Unity platform {Application.platform}."),
        };
    }

    private static string GetPlatformDiagnosticBundleName()
    {
        return Application.platform switch
        {
            RuntimePlatform.OSXEditor or RuntimePlatform.OSXPlayer => MacDiagnosticBundleName,
            RuntimePlatform.WindowsEditor or RuntimePlatform.WindowsPlayer => WindowsDiagnosticBundleName,
            _ => throw new PlatformNotSupportedException(
                $"Wildfire diagnostic AssetBundle is not packaged for Unity platform {Application.platform}."),
        };
    }

    private static string EscapeLogValue(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string GetBundlePath(string bundleName)
    {
        string[] candidatePaths = GetCandidateModDirectories()
            .Select(directory => Path.Combine(directory, PrivateBundleDirectoryName, bundleName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string? existingPath = candidatePaths.FirstOrDefault(File.Exists);
        if (existingPath != null)
        {
            return existingPath;
        }

        return candidatePaths.FirstOrDefault() ??
            throw new InvalidOperationException("Could not resolve a Wildfire compute shader AssetBundle path.");
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

public sealed record TimberbornComputeShaderBundleProbe(
    string ComputeBundleName,
    string ComputeBundlePath,
    bool ComputeBundleExists,
    long? ComputeBundleSizeBytes,
    string? ComputeBundleHeader,
    string? ComputeBundleReadError,
    string DiagnosticBundleName,
    string DiagnosticBundlePath,
    bool DiagnosticBundleExists,
    long? DiagnosticBundleSizeBytes,
    string? DiagnosticBundleHeader,
    string? DiagnosticBundleReadError);

public sealed record TimberbornAssetBundleFileProbe(
    bool Exists,
    long? SizeBytes,
    string? Header,
    string? ReadError);

public sealed class TimberbornComputeFireSimulator : IGpuFireSimulator, ITimberbornGpuVisualFieldStateProvider, IDisposable
{
    public const string ApplyExternalChangesKernelName = "ApplyExternalChanges";
    public const string FullGridKernelName = "SimulateFullGrid";
    public const int ThreadGroupSizeX = 8;
    public const int ThreadGroupSizeY = 8;
    public const int ThreadGroupSizeZ = 4;

    private const int PackedCellStrideBytes = sizeof(uint);
    private const int ChangeStrideBytes = sizeof(uint) * 4;
    private const int DeltaStrideBytes = sizeof(uint) * 4;
    private const int VisualFieldStrideBytes = sizeof(float) * 4;

    private readonly ComputeShader _shader;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly ITimberbornGpuVisualFieldSurface _visualFieldSurface;
    private readonly List<FireSimChange> _queuedChanges = new List<FireSimChange>();
    private readonly List<IFireSimListener> _listeners = new List<IFireSimListener>();
    private readonly ComputeBuffer _currentCells;
    private readonly ComputeBuffer _nextCells;
    private readonly ComputeBuffer _externalChanges;
    private readonly ComputeBuffer _deltas;
    private readonly ComputeBuffer _visualFields;
    private readonly ComputeBuffer _deltaCounter;
    private readonly FireSimParameters _parameters;
    private readonly int _applyExternalChangesKernel;
    private readonly int _fullGridKernel;
    private TimberbornGpuVisualFieldSurfaceBindingLifecycle? _visualFieldBindingLifecycle;
    private ComputeBuffer _readCells;
    private ComputeBuffer _writeCells;
    private uint _tick;
    private bool _disposed;

    public TimberbornComputeFireSimulator(
        FireGrid grid,
        ReadOnlySpan<ushort> initialCells,
        ComputeShader shader,
        ITimberbornFireLogSink logSink)
        : this(grid, initialCells, shader, logSink, NullTimberbornGpuVisualFieldSurface.Instance)
    {
    }

    public TimberbornComputeFireSimulator(
        FireGrid grid,
        ReadOnlySpan<ushort> initialCells,
        ComputeShader shader,
        ITimberbornFireLogSink logSink,
        ITimberbornGpuVisualFieldSurface visualFieldSurface)
        : this(grid, initialCells, shader, logSink, visualFieldSurface, FireSimParameters.Default)
    {
    }

    public TimberbornComputeFireSimulator(
        FireGrid grid,
        ReadOnlySpan<ushort> initialCells,
        ComputeShader shader,
        ITimberbornFireLogSink logSink,
        ITimberbornGpuVisualFieldSurface visualFieldSurface,
        FireSimParameters parameters)
    {
        if (grid.CellCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(grid), grid, "Fire grid dimensions must produce at least one cell.");
        }

        if (initialCells.Length != grid.CellCount)
        {
            throw new ArgumentException(
                $"Initial cell count {initialCells.Length} must match grid cell count {grid.CellCount}.",
                nameof(initialCells));
        }

        _shader = shader ?? throw new ArgumentNullException(nameof(shader));
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        _visualFieldSurface = visualFieldSurface ?? throw new ArgumentNullException(nameof(visualFieldSurface));
        _parameters = parameters;
        Grid = grid;
        _applyExternalChangesKernel = _shader.FindKernel(ApplyExternalChangesKernelName);
        _fullGridKernel = _shader.FindKernel(FullGridKernelName);

        try
        {
            _currentCells = CreateBuffer(grid.CellCount, PackedCellStrideBytes, ComputeBufferType.Structured);
            _nextCells = CreateBuffer(grid.CellCount, PackedCellStrideBytes, ComputeBufferType.Structured);
            _externalChanges = CreateBuffer(grid.CellCount, ChangeStrideBytes, ComputeBufferType.Structured);
            _deltas = CreateBuffer(grid.CellCount, DeltaStrideBytes, ComputeBufferType.Append);
            _visualFields = CreateBuffer(grid.CellCount, VisualFieldStrideBytes, ComputeBufferType.Structured);
            _deltaCounter = CreateBuffer(1, sizeof(uint), ComputeBufferType.Raw);

            uint[] packedCells = initialCells.ToArray().Select(static cell => (uint)cell).ToArray();
            _currentCells.SetData(packedCells);
            _nextCells.SetData(packedCells);
            _readCells = _currentCells;
            _writeCells = _nextCells;
            _visualFieldBindingLifecycle = new TimberbornGpuVisualFieldSurfaceBindingLifecycle(
                _visualFieldSurface,
                _visualFields,
                grid,
                VisualFieldStrideBytes);
            _visualFieldBindingLifecycle.Bind();
            _logSink.Info(
                $"wildfire_timberborn_gpu_simulator_initialized width={grid.Width} height={grid.Height} depth={grid.Depth} cell_count={grid.CellCount}");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public FireGrid Grid { get; }

    public int Width => Grid.Width;

    public int Height => Grid.Height;

    public int Depth => Grid.Depth;

    public TimberbornGpuVisualFieldSurfaceState VisualFieldSurfaceState => _visualFieldSurface.State;

    public void RegisterChange(FireSimChange change)
    {
        _queuedChanges.Add(change);
    }

    public GpuFireStepResult Tick()
    {
        ThrowIfDisposed();

        uint dispatchTick = _tick + 1;
        QueuedChangeBatch changeBatch = CreateQueuedChangeBatch(Grid.CellCount);
        _logSink.Info(
            $"wildfire_timberborn_gpu_queued_changes tick={dispatchTick} queued_changes={_queuedChanges.Count} upload_capacity={Grid.CellCount} valid_changes={changeBatch.ValidChanges.Length} ignored_changes={changeBatch.InvalidIndices.Count}");

        try
        {
            _deltas.SetCounterValue(0);

            if (changeBatch.ValidChanges.Length > 0)
            {
                _externalChanges.SetData(ToGpuChanges(changeBatch.ValidChanges), 0, 0, changeBatch.ValidChanges.Length);
                BindKernel(_applyExternalChangesKernel, dispatchTick, checked((uint)changeBatch.ValidChanges.Length));
                DispatchKernel(
                    _applyExternalChangesKernel,
                    ApplyExternalChangesKernelName,
                    dispatchTick,
                    changeBatch.ValidChanges.Length,
                    1,
                    1,
                    1);
            }

            ConsumeQueuedChanges(changeBatch);
            _tick = dispatchTick;

            BindKernel(_fullGridKernel, dispatchTick, 0u);
            int groupsX = GetThreadGroups(Width, ThreadGroupSizeX);
            int groupsY = GetThreadGroups(Height, ThreadGroupSizeY);
            int groupsZ = GetThreadGroups(Depth, ThreadGroupSizeZ);
            DispatchKernel(_fullGridKernel, FullGridKernelName, dispatchTick, 0, groupsX, groupsY, groupsZ);
            _visualFieldBindingLifecycle?.MarkUpdated(dispatchTick);

            CellDelta[] deltas = ReadDeltas();
            SwapCellBuffers();
            NotifyListeners(deltas);
            _logSink.Info(
                $"wildfire_timberborn_gpu_readback_completed tick={dispatchTick} delta_count={deltas.Length}");
            return new GpuFireStepResult(deltas, dispatchTick);
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                $"wildfire_timberborn_gpu_dispatch_failed tick={dispatchTick} message=\"{exception.Message}\"");
            throw;
        }
    }

    public IDisposable Subscribe(IFireSimListener listener)
    {
        if (listener is null)
        {
            throw new ArgumentNullException(nameof(listener));
        }

        _listeners.Add(listener);
        return new ListenerSubscription(_listeners, listener);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _visualFieldBindingLifecycle?.Unbind();
        new[]
        {
            _currentCells,
            _nextCells,
            _externalChanges,
            _deltas,
            _visualFields,
            _deltaCounter,
        }
            .Where(static buffer => buffer != null)
            .ToList()
            .ForEach(static buffer => buffer.Release());
        _logSink.Info(
            $"wildfire_timberborn_gpu_simulator_disposed tick={_tick} queued_changes={_queuedChanges.Count} listener_count={_listeners.Count}");
        _disposed = true;
    }

    private void DispatchKernel(int kernel, string kernelName, uint tick, int changeCount, int groupsX, int groupsY, int groupsZ)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        _logSink.Info(
            $"wildfire_timberborn_gpu_dispatch_kernel_started kernel={kernelName} tick={tick} groups={groupsX}x{groupsY}x{groupsZ} change_count={changeCount}");
        _shader.Dispatch(kernel, groupsX, groupsY, groupsZ);
        stopwatch.Stop();
        _logSink.Info(
            $"wildfire_timberborn_gpu_dispatch_kernel_completed kernel={kernelName} tick={tick} elapsed_ms={stopwatch.Elapsed.TotalMilliseconds:F3}");
    }

    private void BindKernel(int kernel, uint tick, uint changeCount)
    {
        _shader.SetInt("Width", Width);
        _shader.SetInt("Height", Height);
        _shader.SetInt("Depth", Depth);
        _shader.SetInt("CellCount", Grid.CellCount);
        _shader.SetInt("Tick", unchecked((int)tick));
        _shader.SetInt("Seed", 0);
        _shader.SetInt("ChangeCount", unchecked((int)changeCount));
        BindParameters();
        _shader.SetBuffer(kernel, "CurrentCells", _readCells);
        _shader.SetBuffer(kernel, "NextCells", _writeCells);
        _shader.SetBuffer(kernel, "ExternalChanges", _externalChanges);
        _shader.SetBuffer(kernel, "Deltas", _deltas);
        _shader.SetBuffer(kernel, "VisualFields", _visualFields);
    }

    private void BindParameters()
    {
        _shader.SetFloat("VisualFireBaseIntensity", _parameters.VisualFireBaseIntensity);
        _shader.SetFloat("VisualFireHeatWeight", _parameters.VisualFireHeatWeight);
        _shader.SetFloat("VisualSmokeBaseIntensity", _parameters.VisualSmokeBaseIntensity);
        _shader.SetFloat("VisualSmokeFuelWeight", _parameters.VisualSmokeFuelWeight);
        _shader.SetFloat("VisualSmokeHeatWeight", _parameters.VisualSmokeHeatWeight);
        _shader.SetFloat("VisualAshBaseIntensity", _parameters.VisualAshBaseIntensity);
        _shader.SetFloat("VisualAshFuelWeight", _parameters.VisualAshFuelWeight);
        _shader.SetFloat("VisualAshHeatWeight", _parameters.VisualAshHeatWeight);
        _shader.SetFloat("VisualVisibilityHeatWeight", _parameters.VisualVisibilityHeatWeight);
        _shader.SetFloat("VisualVisibilitySmokeWeight", _parameters.VisualVisibilitySmokeWeight);
        _shader.SetFloat("VisualVisibilityAshWeight", _parameters.VisualVisibilityAshWeight);
        _shader.SetInt("FireIgnitionBaseHeat", unchecked((int)_parameters.FireIgnitionBaseHeat));
        _shader.SetInt("FireWaterIgnitionPenalty", unchecked((int)_parameters.FireWaterIgnitionPenalty));
        _shader.SetInt("FireRetainedHeatWeight", unchecked((int)_parameters.FireRetainedHeatWeight));
        _shader.SetInt("FireSpreadHeatWeight", unchecked((int)_parameters.FireSpreadHeatWeight));
        _shader.SetInt("FireBurningNeighborHeatBonus", unchecked((int)_parameters.FireBurningNeighborHeatBonus));
        _shader.SetInt("FireBurningNeighborDirectHeat", unchecked((int)_parameters.FireBurningNeighborDirectHeat));
        _shader.SetInt("FireWaterSuppressionHeat", unchecked((int)_parameters.FireWaterSuppressionHeat));
        _shader.SetInt("FireWaterEvaporationHeat", unchecked((int)_parameters.FireWaterEvaporationHeat));
        _shader.SetInt("FireFlammabilityBurnPressure", unchecked((int)_parameters.FireFlammabilityBurnPressure));
        _shader.SetInt("FireWaterBurnPressurePenalty", unchecked((int)_parameters.FireWaterBurnPressurePenalty));
        _shader.SetInt("FireBurnHeatBase", unchecked((int)_parameters.FireBurnHeatBase));
        _shader.SetInt("FireCoolingBase", unchecked((int)_parameters.FireCoolingBase));
        _shader.SetInt("FireHeatLossCoolingDivisor", unchecked((int)_parameters.FireHeatLossCoolingDivisor));
        _shader.SetInt("FireFuelBurnDownPressureNumerator", unchecked((int)_parameters.FireFuelBurnDownPressureNumerator));
        _shader.SetInt("FireFuelBurnDownPressureDenominator", unchecked((int)_parameters.FireFuelBurnDownPressureDenominator));
        _shader.SetInt("FireFuelBurnDownRollSeed", unchecked((int)_parameters.FireFuelBurnDownRollSeed));
    }

    private CellDelta[] ReadDeltas()
    {
        uint[] counter = new uint[1];
        ComputeBuffer.CopyCount(_deltas, _deltaCounter, 0);
        _deltaCounter.GetData(counter);
        uint rawDeltaCount = counter[0];

        if (rawDeltaCount > Grid.CellCount)
        {
            throw new InvalidOperationException(
                $"GPU delta counter returned {rawDeltaCount}, but buffer capacity is {Grid.CellCount}.");
        }

        int deltaCount = checked((int)rawDeltaCount);
        _logSink.Info($"wildfire_timberborn_gpu_readback_counter tick={_tick} delta_count={deltaCount}");
        if (deltaCount == 0)
        {
            return Array.Empty<CellDelta>();
        }

        GpuCellDelta[] rawDeltas = new GpuCellDelta[deltaCount];
        _deltas.GetData(rawDeltas, 0, 0, deltaCount);
        return rawDeltas
            .Select(static delta => new CellDelta(
                checked((int)delta.Index),
                checked((ushort)(delta.OldCell & 0xFFFFu)),
                checked((ushort)(delta.NewCell & 0xFFFFu))))
            .ToArray();
    }

    private QueuedChangeBatch CreateQueuedChangeBatch(int uploadCapacity)
    {
        IndexedChange[] indexedChanges = _queuedChanges
            .Select(static (change, index) => new IndexedChange(index, change))
            .ToArray();
        IndexedChange[] validChanges = indexedChanges
            .Where(change => IsValidCellIndex(change.Change.CellIndex))
            .Take(uploadCapacity)
            .ToArray();
        int[] invalidIndices = indexedChanges
            .Where(change => !IsValidCellIndex(change.Change.CellIndex))
            .Select(static change => change.Index)
            .ToArray();

        return new QueuedChangeBatch(
            validChanges.Select(static change => change.Index).ToArray(),
            validChanges.Select(static change => change.Change).ToArray(),
            invalidIndices);
    }

    private static GpuFireSimChange[] ToGpuChanges(FireSimChange[] changes)
    {
        return changes.Select(ToGpuChange).ToArray();
    }

    private static GpuFireSimChange ToGpuChange(FireSimChange change)
    {
        return new GpuFireSimChange
        {
            CellIndex = checked((uint)change.CellIndex),
            SetMask = GetSetMask(change),
            AddFields = GetAddFields(change),
            SetValues = GetSetValues(change),
        };
    }

    private static uint GetSetMask(FireSimChange change)
    {
        uint mask = 0u;
        mask |= change.SetCell.HasValue ? 1u << 0 : 0u;
        mask |= change.SetWater.HasValue ? 1u << 1 : 0u;
        mask |= change.SetFuel.HasValue ? 1u << 2 : 0u;
        mask |= change.SetHeat.HasValue ? 1u << 3 : 0u;
        mask |= change.SetFlammability.HasValue ? 1u << 4 : 0u;
        mask |= change.SetHeatLoss.HasValue ? 1u << 5 : 0u;
        mask |= change.SetTerrain.HasValue ? 1u << 6 : 0u;
        return mask;
    }

    private static uint GetAddFields(FireSimChange change)
    {
        return Clamp(change.AddHeat, 15u) |
            (Clamp(change.AddFuel, 15u) << 4);
    }

    private static uint GetSetValues(FireSimChange change)
    {
        return ((uint)(change.SetCell ?? 0) & 0xFFFFu) |
            (Clamp(change.SetWater, 3u) << 16) |
            (Clamp(change.SetFuel, 15u) << 18) |
            (Clamp(change.SetHeat, 15u) << 22) |
            (Clamp(change.SetFlammability, 3u) << 26) |
            (Clamp(change.SetHeatLoss, 7u) << 28) |
            (Clamp(change.SetTerrain, 1u) << 31);
    }

    private static uint Clamp(byte? value, uint max)
    {
        return Math.Min((uint)(value ?? 0), max);
    }

    private bool IsValidCellIndex(int cellIndex)
    {
        return cellIndex >= 0 && cellIndex < Grid.CellCount;
    }

    private void ConsumeQueuedChanges(QueuedChangeBatch changeBatch)
    {
        changeBatch.ValidIndices
            .Concat(changeBatch.InvalidIndices)
            .OrderByDescending(static index => index)
            .ToList()
            .ForEach(index => _queuedChanges.RemoveAt(index));
    }

    private void NotifyListeners(CellDelta[] deltas)
    {
        IFireSimListener[] listeners = _listeners.ToArray();
        listeners
            .ToList()
            .ForEach(listener => listener.OnFireSimDeltas(deltas));
        _logSink.Info(
            $"wildfire_timberborn_gpu_listeners_notified tick={_tick} listener_count={listeners.Length} delta_count={deltas.Length}");
    }

    private void SwapCellBuffers()
    {
        (_readCells, _writeCells) = (_writeCells, _readCells);
    }

    private static int GetThreadGroups(int dimension, int threadGroupSize)
    {
        return (dimension + threadGroupSize - 1) / threadGroupSize;
    }

    private static ComputeBuffer CreateBuffer(int count, int strideBytes, ComputeBufferType type)
    {
        return new ComputeBuffer(count, strideBytes, type);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TimberbornComputeFireSimulator));
        }
    }

    private sealed record QueuedChangeBatch(
        IReadOnlyList<int> ValidIndices,
        FireSimChange[] ValidChanges,
        IReadOnlyList<int> InvalidIndices);

    private readonly record struct IndexedChange(int Index, FireSimChange Change);

    [StructLayout(LayoutKind.Sequential)]
    private struct GpuFireSimChange
    {
        public uint CellIndex;
        public uint SetMask;
        public uint AddFields;
        public uint SetValues;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GpuCellDelta
    {
        public uint Index;
        public uint OldCell;
        public uint NewCell;
        public uint Reserved;
    }

    private sealed class ListenerSubscription : IDisposable
    {
        private readonly List<IFireSimListener> _listeners;
        private readonly IFireSimListener _listener;
        private bool _disposed;

        public ListenerSubscription(List<IFireSimListener> listeners, IFireSimListener listener)
        {
            _listeners = listeners;
            _listener = listener;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _listeners.Remove(_listener);
            _disposed = true;
        }
    }
}
