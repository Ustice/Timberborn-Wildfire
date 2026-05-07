using Timberborn.Navigation;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornBeaverHazardAvoidanceOptions
{
    public static readonly TimberbornBeaverHazardAvoidanceOptions Default = new();

    public TimberbornBeaverHazardAvoidanceOptions(
        int MaxRestrictedCells = 128,
        int MaxRestrictionChangesPerDispatch = 32,
        uint ReleaseAfterMissingTicks = 8,
        float FireThreshold = TimberbornBeaverFieldExposureTelemetry.BurnFireThreshold,
        float SmokeThreshold = TimberbornBeaverFieldExposureTelemetry.RespiratorySmokeThreshold)
    {
        if (MaxRestrictedCells <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxRestrictedCells),
                MaxRestrictedCells,
                "The maximum restricted cell count must be positive.");
        }

        if (MaxRestrictionChangesPerDispatch <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxRestrictionChangesPerDispatch),
                MaxRestrictionChangesPerDispatch,
                "The maximum restriction change count must be positive.");
        }

        if (ReleaseAfterMissingTicks == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ReleaseAfterMissingTicks),
                ReleaseAfterMissingTicks,
                "The missing-tick release window must be positive.");
        }

        if (FireThreshold < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(FireThreshold), FireThreshold, "Fire threshold cannot be negative.");
        }

        if (SmokeThreshold < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(SmokeThreshold), SmokeThreshold, "Smoke threshold cannot be negative.");
        }

        this.MaxRestrictedCells = MaxRestrictedCells;
        this.MaxRestrictionChangesPerDispatch = MaxRestrictionChangesPerDispatch;
        this.ReleaseAfterMissingTicks = ReleaseAfterMissingTicks;
        this.FireThreshold = FireThreshold;
        this.SmokeThreshold = SmokeThreshold;
    }

    public int MaxRestrictedCells { get; }

    public int MaxRestrictionChangesPerDispatch { get; }

    public uint ReleaseAfterMissingTicks { get; }

    public float FireThreshold { get; }

    public float SmokeThreshold { get; }
}

public readonly record struct TimberbornBeaverHazardAvoidanceCounters(
    bool AvoidanceEnabled,
    int ObservedHazardCellCount,
    int RestrictedCellCount,
    int AppliedRestrictionCount,
    int ReleasedRestrictionCount,
    int SkippedNoSafeApiCount,
    int FailedRestrictionCount,
    uint? LastUpdatedTick);

public interface ITimberbornBeaverHazardAvoidanceCounterProvider
{
    TimberbornBeaverHazardAvoidanceCounters Counters { get; }
}

public interface ITimberbornBeaverHazardBlocker
{
    bool IsAvailable { get; }

    bool TryRestrict(TimberbornBeaverHazardCell cell);

    bool TryRelease(int cellIndex);

    void Clear();
}

public sealed class TimberbornBeaverHazardAvoidanceSink :
    ITimberbornFireVisualEffectDispatchSink,
    ITimberbornBeaverHazardAvoidanceCounterProvider
{
    private readonly ITimberbornGpuVisualFieldSurface _visualFieldSurface;
    private readonly ITimberbornBeaverHazardBlocker _hazardBlocker;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly Dictionary<int, TimberbornBeaverHazardCell> _observedHazardCells = new();
    private readonly HashSet<int> _restrictedCellIndices = new();
    private uint? _currentTick;
    private int _appliedRestrictionCount;
    private int _releasedRestrictionCount;
    private int _skippedNoSafeApiCount;
    private int _failedRestrictionCount;

    public TimberbornBeaverHazardAvoidanceSink(
        ITimberbornGpuVisualFieldSurface visualFieldSurface,
        ITimberbornBeaverHazardBlocker hazardBlocker,
        ITimberbornFireLogSink logSink,
        TimberbornBeaverHazardAvoidanceOptions? options = null)
    {
        _visualFieldSurface = visualFieldSurface ?? throw new ArgumentNullException(nameof(visualFieldSurface));
        _hazardBlocker = hazardBlocker ?? throw new ArgumentNullException(nameof(hazardBlocker));
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        Options = options ?? TimberbornBeaverHazardAvoidanceOptions.Default;
    }

    public TimberbornBeaverHazardAvoidanceOptions Options { get; }

    public TimberbornBeaverHazardAvoidanceCounters Counters => new(
        AvoidanceEnabled: _hazardBlocker.IsAvailable,
        ObservedHazardCellCount: _observedHazardCells.Count,
        RestrictedCellCount: _restrictedCellIndices.Count,
        AppliedRestrictionCount: _appliedRestrictionCount,
        ReleasedRestrictionCount: _releasedRestrictionCount,
        SkippedNoSafeApiCount: _skippedNoSafeApiCount,
        FailedRestrictionCount: _failedRestrictionCount,
        LastUpdatedTick: _currentTick);

    public void BeginVisualEffectDispatch(uint tick)
    {
        _currentTick = tick;
    }

    public void UpdateVisualEffect(TimberbornFireVisualEffectEvent effectEvent)
    {
        try
        {
            if (!_visualFieldSurface.TryGetBinding(out TimberbornGpuVisualFieldSurfaceBinding binding))
            {
                return;
            }

            IReadOnlyList<TimberbornGpuVisualFieldSample> samples =
                _visualFieldSurface.InspectCells(new[] { effectEvent.CellIndex });
            TimberbornGpuVisualFieldSample sample = samples
                .FirstOrDefault(sample => sample.CellIndex == effectEvent.CellIndex);
            if (sample.CellIndex != effectEvent.CellIndex)
            {
                _observedHazardCells.Remove(effectEvent.CellIndex);
                return;
            }

            if (!IsHazard(sample))
            {
                _observedHazardCells.Remove(effectEvent.CellIndex);
                return;
            }

            (int x, int y, int z) = FromIndex(binding, effectEvent.CellIndex);
            _observedHazardCells[effectEvent.CellIndex] = new TimberbornBeaverHazardCell(
                effectEvent.CellIndex,
                x,
                y,
                z,
                sample.Fire,
                sample.Smoke,
                effectEvent.Tick);
        }
        catch (Exception exception)
        {
            _observedHazardCells.Remove(effectEvent.CellIndex);
            _failedRestrictionCount++;
            _logSink.Warning(
                "wildfire_timberborn_beaver_hazard_avoidance_failed " +
                $"stage=inspect cell_index={effectEvent.CellIndex} message=\"{EscapeLogValue(exception.Message)}\"");
        }
    }

    public void CompleteVisualEffectDispatch(uint tick)
    {
        _currentTick = tick;
        RemoveStaleHazards(tick);
        if (!_hazardBlocker.IsAvailable)
        {
            _skippedNoSafeApiCount += _observedHazardCells.Count;
            LogState(tick);
            return;
        }

        HashSet<int> desiredRestrictedCells = _observedHazardCells.Values
            .OrderByDescending(static cell => Math.Max(cell.Fire, cell.Smoke))
            .ThenBy(static cell => cell.CellIndex)
            .Take(Options.MaxRestrictedCells)
            .Select(static cell => cell.CellIndex)
            .ToHashSet();
        int appliedBeforeDispatch = _appliedRestrictionCount;
        _observedHazardCells.Values
            .Where(cell => desiredRestrictedCells.Contains(cell.CellIndex))
            .Where(cell => !_restrictedCellIndices.Contains(cell.CellIndex))
            .Take(Options.MaxRestrictionChangesPerDispatch)
            .ToList()
            .ForEach(RestrictCell);
        int appliedThisDispatch = _appliedRestrictionCount - appliedBeforeDispatch;
        int remainingChangeBudget = Math.Max(Options.MaxRestrictionChangesPerDispatch - appliedThisDispatch, 0);
        _restrictedCellIndices
            .Where(cellIndex => !desiredRestrictedCells.Contains(cellIndex))
            .ToArray()
            .Take(remainingChangeBudget)
            .ToList()
            .ForEach(ReleaseCell);
        LogState(tick);
    }

    public void Clear()
    {
        _observedHazardCells.Clear();
        _restrictedCellIndices.Clear();
        _currentTick = null;
        _appliedRestrictionCount = 0;
        _releasedRestrictionCount = 0;
        _skippedNoSafeApiCount = 0;
        _failedRestrictionCount = 0;
        _hazardBlocker.Clear();
    }

    private void RemoveStaleHazards(uint tick)
    {
        _observedHazardCells.Values
            .Where(cell => tick > cell.LastSeenTick && tick - cell.LastSeenTick >= Options.ReleaseAfterMissingTicks)
            .Select(static cell => cell.CellIndex)
            .ToArray()
            .ToList()
            .ForEach(cellIndex => _observedHazardCells.Remove(cellIndex));
    }

    private void RestrictCell(TimberbornBeaverHazardCell cell)
    {
        if (_hazardBlocker.TryRestrict(cell))
        {
            _restrictedCellIndices.Add(cell.CellIndex);
            _appliedRestrictionCount++;
            return;
        }

        _failedRestrictionCount++;
    }

    private void ReleaseCell(int cellIndex)
    {
        if (_hazardBlocker.TryRelease(cellIndex))
        {
            _restrictedCellIndices.Remove(cellIndex);
            _releasedRestrictionCount++;
            return;
        }

        _failedRestrictionCount++;
    }

    private void LogState(uint tick)
    {
        _logSink.Info(
            "wildfire_timberborn_beaver_hazard_avoidance_updated " +
            $"tick={tick} " +
            $"enabled={_hazardBlocker.IsAvailable.ToString().ToLowerInvariant()} " +
            $"observed_hazard_cells={_observedHazardCells.Count} " +
            $"restricted_cells={_restrictedCellIndices.Count} " +
            $"applied_restrictions={_appliedRestrictionCount} " +
            $"released_restrictions={_releasedRestrictionCount} " +
            $"skipped_no_safe_api={_skippedNoSafeApiCount} " +
            $"failed_restrictions={_failedRestrictionCount}");
    }

    private bool IsHazard(TimberbornGpuVisualFieldSample sample)
    {
        return sample.Fire >= Options.FireThreshold || sample.Smoke >= Options.SmokeThreshold;
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

    private static string EscapeLogValue(string value)
    {
        return value.Replace('\\', '/').Replace('"', '\'');
    }
}

public sealed class TimberbornNavMeshBeaverHazardBlocker : ITimberbornBeaverHazardBlocker
{
    private readonly INavMeshObjectFactory? _navMeshObjectFactory;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly Dictionary<int, NavMeshObject> _navMeshObjectsByCellIndex = new();

    public TimberbornNavMeshBeaverHazardBlocker(
        INavMeshObjectFactory? navMeshObjectFactory,
        ITimberbornFireLogSink logSink)
    {
        _navMeshObjectFactory = navMeshObjectFactory;
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
    }

    public bool IsAvailable => _navMeshObjectFactory is not null;

    public bool TryRestrict(TimberbornBeaverHazardCell cell)
    {
        if (_navMeshObjectFactory is null)
        {
            return false;
        }

        if (_navMeshObjectsByCellIndex.ContainsKey(cell.CellIndex))
        {
            return true;
        }

        try
        {
            NavMeshObject navMeshObject = _navMeshObjectFactory.Create();
            navMeshObject.AddRestrictedCoordinates(new Vector3Int(cell.X, cell.Y, cell.Z));
            navMeshObject.EnqueueAddToRegularNavMesh();
            _navMeshObjectsByCellIndex[cell.CellIndex] = navMeshObject;
            return true;
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                "wildfire_timberborn_beaver_hazard_avoidance_failed " +
                $"stage=restrict cell_index={cell.CellIndex} message=\"{EscapeLogValue(exception.Message)}\"");
            return false;
        }
    }

    public bool TryRelease(int cellIndex)
    {
        if (!_navMeshObjectsByCellIndex.TryGetValue(cellIndex, out NavMeshObject? navMeshObject))
        {
            return true;
        }

        try
        {
            navMeshObject.EnqueueRemoveFromRegularNavMesh();
            _navMeshObjectsByCellIndex.Remove(cellIndex);
            return true;
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                "wildfire_timberborn_beaver_hazard_avoidance_failed " +
                $"stage=release cell_index={cellIndex} message=\"{EscapeLogValue(exception.Message)}\"");
            return false;
        }
    }

    public void Clear()
    {
        _navMeshObjectsByCellIndex.Keys
            .ToArray()
            .ToList()
            .ForEach(cellIndex => TryRelease(cellIndex));
        _navMeshObjectsByCellIndex.Clear();
    }

    private static string EscapeLogValue(string value)
    {
        return value.Replace('\\', '/').Replace('"', '\'');
    }
}

public sealed class NullTimberbornBeaverHazardBlocker : ITimberbornBeaverHazardBlocker
{
    public static readonly NullTimberbornBeaverHazardBlocker Instance = new();

    private NullTimberbornBeaverHazardBlocker()
    {
    }

    public bool IsAvailable => false;

    public bool TryRestrict(TimberbornBeaverHazardCell cell)
    {
        return false;
    }

    public bool TryRelease(int cellIndex)
    {
        return false;
    }

    public void Clear()
    {
    }
}

public readonly record struct TimberbornBeaverHazardCell(
    int CellIndex,
    int X,
    int Y,
    int Z,
    float Fire,
    float Smoke,
    uint LastSeenTick);
