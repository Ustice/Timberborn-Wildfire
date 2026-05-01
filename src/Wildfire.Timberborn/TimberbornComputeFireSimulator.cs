using System.Runtime.InteropServices;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornComputeFireSimulatorFactory : ITimberbornFireSimulatorFactory, IDisposable
{
    private readonly ITimberbornFireLogSink _logSink;
    private readonly TimberbornComputeShaderLoader _shaderLoader;

    public TimberbornComputeFireSimulatorFactory()
    {
        _logSink = new UnityTimberbornFireLogSink();
        _shaderLoader = new TimberbornComputeShaderLoader(_logSink);
        _logSink.Info("wildfire_timberborn_gpu_factory_created backend=unity_compute");
    }

    public IGpuFireSimulator Create(FireGrid grid, ReadOnlySpan<ushort> initialCells)
    {
        if (!SystemInfo.supportsComputeShaders)
        {
            throw new InvalidOperationException("The current Timberborn graphics device does not support compute shaders.");
        }

        ComputeShader shader = _shaderLoader.Load();
        TimberbornComputeFireSimulator simulator = new(grid, initialCells, shader, _logSink);
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
    private const string PrivateBundleDirectoryName = "ComputeShaders";

    private readonly ITimberbornFireLogSink _logSink;
    private AssetBundle? _assetBundle;
    private ComputeShader? _shader;

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

        if (!File.Exists(bundlePath))
        {
            throw new FileNotFoundException("Wildfire compute shader AssetBundle was not deployed.", bundlePath);
        }

        _assetBundle = AssetBundle.LoadFromFile(bundlePath);
        if (_assetBundle == null)
        {
            throw new InvalidOperationException($"Unity could not load the Wildfire compute shader AssetBundle at '{bundlePath}'.");
        }

        string[] assetNames = _assetBundle.GetAllAssetNames();
        string? shaderAssetName = assetNames.FirstOrDefault(static name =>
            name.EndsWith("firesim.compute", StringComparison.OrdinalIgnoreCase));
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

    public void Dispose()
    {
        if (_assetBundle != null)
        {
            _assetBundle.Unload(unloadAllLoadedObjects: true);
            _assetBundle = null;
            _shader = null;
        }
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

public sealed class TimberbornComputeFireSimulator : IGpuFireSimulator, IDisposable
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
    private readonly List<FireSimChange> _queuedChanges = new List<FireSimChange>();
    private readonly List<IFireSimListener> _listeners = new List<IFireSimListener>();
    private readonly ComputeBuffer _currentCells;
    private readonly ComputeBuffer _nextCells;
    private readonly ComputeBuffer _externalChanges;
    private readonly ComputeBuffer _deltas;
    private readonly ComputeBuffer _visualFields;
    private readonly ComputeBuffer _deltaCounter;
    private readonly int _applyExternalChangesKernel;
    private readonly int _fullGridKernel;
    private ComputeBuffer _readCells;
    private ComputeBuffer _writeCells;
    private uint _tick;
    private bool _disposed;

    public TimberbornComputeFireSimulator(
        FireGrid grid,
        ReadOnlySpan<ushort> initialCells,
        ComputeShader shader,
        ITimberbornFireLogSink logSink)
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
            $"wildfire_timberborn_gpu_dispatch_start tick={dispatchTick} queued_changes={_queuedChanges.Count} valid_changes={changeBatch.ValidChanges.Length} ignored_changes={changeBatch.InvalidIndices.Count}");

        try
        {
            _deltas.SetCounterValue(0);

            if (changeBatch.ValidChanges.Length > 0)
            {
                _externalChanges.SetData(ToGpuChanges(changeBatch.ValidChanges), 0, 0, changeBatch.ValidChanges.Length);
                BindKernel(_applyExternalChangesKernel, dispatchTick, checked((uint)changeBatch.ValidChanges.Length));
                _logSink.Info(
                    $"wildfire_timberborn_gpu_dispatch_kernel kernel={ApplyExternalChangesKernelName} tick={dispatchTick} change_count={changeBatch.ValidChanges.Length}");
                _shader.Dispatch(_applyExternalChangesKernel, 1, 1, 1);
            }

            ConsumeQueuedChanges(changeBatch);
            _tick = dispatchTick;

            BindKernel(_fullGridKernel, dispatchTick, 0u);
            int groupsX = GetThreadGroups(Width, ThreadGroupSizeX);
            int groupsY = GetThreadGroups(Height, ThreadGroupSizeY);
            int groupsZ = GetThreadGroups(Depth, ThreadGroupSizeZ);
            _logSink.Info(
                $"wildfire_timberborn_gpu_dispatch_kernel kernel={FullGridKernelName} tick={dispatchTick} groups={groupsX}x{groupsY}x{groupsZ}");
            _shader.Dispatch(_fullGridKernel, groupsX, groupsY, groupsZ);

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
        _disposed = true;
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
        _shader.SetBuffer(kernel, "CurrentCells", _readCells);
        _shader.SetBuffer(kernel, "NextCells", _writeCells);
        _shader.SetBuffer(kernel, "ExternalChanges", _externalChanges);
        _shader.SetBuffer(kernel, "Deltas", _deltas);
        _shader.SetBuffer(kernel, "VisualFields", _visualFields);
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
        _listeners
            .ToArray()
            .ToList()
            .ForEach(listener => listener.OnFireSimDeltas(deltas));
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
