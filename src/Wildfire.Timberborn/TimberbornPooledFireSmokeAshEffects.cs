using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public enum TimberbornPooledFireEffectKind
{
    Fire,
    Smoke,
    ToxicSmoke,
    Steam,
    Ash,
    ToxicAsh,
}

public sealed class TimberbornPooledFireEffectOptions
{
    public static readonly TimberbornPooledFireEffectOptions Default = new();

    public TimberbornPooledFireEffectOptions(
        int MaxActiveEffects = 512,
        int MaxUpdatedVisualRegionsPerDispatch = 512,
        float MinimumVisibleIntensity = 0.01f,
        uint SteamEffectLifetimeTicks = 2)
    {
        if (MaxActiveEffects <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxActiveEffects),
                MaxActiveEffects,
                "The pooled fire effect limit must be positive.");
        }

        if (MaxUpdatedVisualRegionsPerDispatch <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxUpdatedVisualRegionsPerDispatch),
                MaxUpdatedVisualRegionsPerDispatch,
                "The updated visual-region limit must be positive.");
        }

        if (MinimumVisibleIntensity < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumVisibleIntensity),
                MinimumVisibleIntensity,
                "The minimum visible intensity cannot be negative.");
        }

        if (SteamEffectLifetimeTicks == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SteamEffectLifetimeTicks),
                SteamEffectLifetimeTicks,
                "The steam effect lifetime must be positive.");
        }

        this.MaxActiveEffects = MaxActiveEffects;
        this.MaxUpdatedVisualRegionsPerDispatch = MaxUpdatedVisualRegionsPerDispatch;
        this.MinimumVisibleIntensity = MinimumVisibleIntensity;
        this.SteamEffectLifetimeTicks = SteamEffectLifetimeTicks;
    }

    public int MaxActiveEffects { get; }

    public int MaxUpdatedVisualRegionsPerDispatch { get; }

    public float MinimumVisibleIntensity { get; }

    public uint SteamEffectLifetimeTicks { get; }
}

public readonly record struct TimberbornPooledFireEffectState(
    int SlotId,
    int CellIndex,
    uint Tick,
    int X,
    int Y,
    int Z,
    TimberbornPooledFireEffectKind Kind,
    float Fire,
    float Smoke,
    float Steam,
    float Ash,
    float Visibility,
    float MoistureDrop,
    float Intensity,
    float SmokeContamination = 0f,
    float AshContamination = 0f,
    float WindDirectionX = 0f,
    float WindDirectionY = 0f,
    float WindStrength = 0f);

public readonly record struct TimberbornPooledFireEffectKey(
    int CellIndex,
    TimberbornPooledFireEffectKind Kind)
{
    public static TimberbornPooledFireEffectKey FromState(TimberbornPooledFireEffectState state)
    {
        return new TimberbornPooledFireEffectKey(state.CellIndex, state.Kind);
    }
}

public readonly record struct TimberbornPooledFireEffectLocalPosition(float X, float Y, float Z);

public readonly record struct TimberbornPooledFireEffectCounters(
    int ActivePooledEffectCount,
    int UpdatedVisualRegionCount,
    int LastNonZeroUpdatedVisualRegionCount,
    int MaxActivePooledEffectCount,
    int MaxUpdatedVisualRegionCount,
    int DroppedVisualRegionCount,
    int DisabledVisualRegionCount,
    int PresentationFailureCount,
    uint? LastUpdatedTick,
    uint? LastNonZeroUpdatedVisualRegionTick,
    bool VisibleEffectsEnabled,
    bool NativeEffectPrefabResolved,
    string? LastNativeEffectPrefabName);

public interface ITimberbornPooledFireEffectCounterProvider
{
    TimberbornPooledFireEffectCounters Counters { get; }
}

public interface ITimberbornPooledFireEffectPresenter
{
    TimberbornPooledFireEffectPresenterState State { get; }

    TimberbornPooledFireEffectPresentationResult UpdateEffect(TimberbornPooledFireEffectState state);

    void ReleaseEffect(int slotId);

    void Clear();
}

public readonly record struct TimberbornPooledFireEffectPresenterState(
    bool VisibleEffectsEnabled,
    bool NativeEffectPrefabResolved,
    string? LastNativeEffectPrefabName);

public enum TimberbornPooledFireEffectPresentationStatus
{
    Applied,
    Disabled,
    Failed,
}

public readonly record struct TimberbornPooledFireEffectPresentationResult(
    TimberbornPooledFireEffectPresentationStatus Status,
    string? NativeEffectPrefabName = null,
    string? Message = null)
{
    public static TimberbornPooledFireEffectPresentationResult Applied(string? nativeEffectPrefabName)
    {
        return new TimberbornPooledFireEffectPresentationResult(
            TimberbornPooledFireEffectPresentationStatus.Applied,
            nativeEffectPrefabName);
    }

    public static TimberbornPooledFireEffectPresentationResult Disabled(string message)
    {
        return new TimberbornPooledFireEffectPresentationResult(
            TimberbornPooledFireEffectPresentationStatus.Disabled,
            Message: message);
    }

    public static TimberbornPooledFireEffectPresentationResult Failed(string message)
    {
        return new TimberbornPooledFireEffectPresentationResult(
            TimberbornPooledFireEffectPresentationStatus.Failed,
            Message: message);
    }
}

public sealed class TimberbornPooledFireSmokeAshEffectSink :
    ITimberbornFireVisualEffectDispatchSink,
    ITimberbornPooledFireEffectCounterProvider
{
    private readonly ITimberbornGpuVisualFieldSurface _visualFieldSurface;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly ITimberbornWindProvider _windProvider;
    private readonly ITimberbornPooledFireEffectPresenter _presenter;
    private readonly Queue<int> _freeSlotIds;
    private readonly Dictionary<TimberbornPooledFireEffectKey, TimberbornPooledFireEffectSlot> _slotsByKey = new();
    private readonly Dictionary<int, TimberbornPooledFireEffectSlot> _slotsById = new();
    private int _updatedVisualRegionsThisDispatch;
    private int _lastNonZeroUpdatedVisualRegionCount;
    private int _sampledVisualCellsThisDispatch;
    private int _droppedVisualRegionsThisDispatch;
    private int _disabledVisualRegionsThisDispatch;
    private int _presentationFailuresThisDispatch;
    private uint? _lastNonZeroUpdatedVisualRegionTick;
    private string? _lastNativeEffectPrefabName;
    private uint? _currentTick;

    public TimberbornPooledFireSmokeAshEffectSink(
        ITimberbornGpuVisualFieldSurface visualFieldSurface,
        ITimberbornFireLogSink logSink)
        : this(
            visualFieldSurface,
            logSink,
            NullTimberbornWindProvider.Instance)
    {
    }

    public TimberbornPooledFireSmokeAshEffectSink(
        ITimberbornGpuVisualFieldSurface visualFieldSurface,
        ITimberbornFireLogSink logSink,
        ITimberbornWindProvider windProvider)
        : this(
            visualFieldSurface,
            logSink,
            windProvider,
            TimberbornPooledFireEffectOptions.Default,
            new TimberbornUnityPooledFireEffectPresenter(logSink))
    {
    }

    public TimberbornPooledFireSmokeAshEffectSink(
        ITimberbornGpuVisualFieldSurface visualFieldSurface,
        ITimberbornFireLogSink logSink,
        TimberbornPooledFireEffectOptions options,
        ITimberbornPooledFireEffectPresenter presenter)
        : this(
            visualFieldSurface,
            logSink,
            NullTimberbornWindProvider.Instance,
            options,
            presenter)
    {
    }

    public TimberbornPooledFireSmokeAshEffectSink(
        ITimberbornGpuVisualFieldSurface visualFieldSurface,
        ITimberbornFireLogSink logSink,
        ITimberbornWindProvider windProvider,
        TimberbornPooledFireEffectOptions options,
        ITimberbornPooledFireEffectPresenter presenter)
    {
        _visualFieldSurface = visualFieldSurface ?? throw new ArgumentNullException(nameof(visualFieldSurface));
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        _windProvider = windProvider ?? throw new ArgumentNullException(nameof(windProvider));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _freeSlotIds = new Queue<int>(Enumerable.Range(0, Options.MaxActiveEffects));
    }

    public TimberbornPooledFireEffectOptions Options { get; }

    public TimberbornPooledFireEffectCounters Counters => new(
        ActivePooledEffectCount: _slotsByKey.Count,
        UpdatedVisualRegionCount: _updatedVisualRegionsThisDispatch,
        LastNonZeroUpdatedVisualRegionCount: _lastNonZeroUpdatedVisualRegionCount,
        MaxActivePooledEffectCount: Options.MaxActiveEffects,
        MaxUpdatedVisualRegionCount: Options.MaxUpdatedVisualRegionsPerDispatch,
        DroppedVisualRegionCount: _droppedVisualRegionsThisDispatch,
        DisabledVisualRegionCount: _disabledVisualRegionsThisDispatch,
        PresentationFailureCount: _presentationFailuresThisDispatch,
        LastUpdatedTick: _currentTick,
        LastNonZeroUpdatedVisualRegionTick: _lastNonZeroUpdatedVisualRegionTick,
        VisibleEffectsEnabled: _presenter.State.VisibleEffectsEnabled,
        NativeEffectPrefabResolved: _presenter.State.NativeEffectPrefabResolved,
        LastNativeEffectPrefabName: _lastNativeEffectPrefabName ?? _presenter.State.LastNativeEffectPrefabName);

    public IReadOnlyDictionary<int, TimberbornPooledFireEffectState> ActiveEffectsByCellIndex =>
        _slotsByKey.Values
            .GroupBy(static slot => slot.State.CellIndex)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .OrderByDescending(static slot => slot.State.Intensity)
                    .ThenBy(static slot => slot.SlotId)
                    .First()
                    .State);

    public void BeginVisualEffectDispatch(uint tick)
    {
        _currentTick = tick;
        _updatedVisualRegionsThisDispatch = 0;
        _sampledVisualCellsThisDispatch = 0;
        _droppedVisualRegionsThisDispatch = 0;
        _disabledVisualRegionsThisDispatch = 0;
        _presentationFailuresThisDispatch = 0;
    }

    public void UpdateVisualEffect(TimberbornFireVisualEffectEvent effectEvent)
    {
        try
        {
            UpdateVisualEffectCore(effectEvent);
        }
        catch (Exception exception)
        {
            _presentationFailuresThisDispatch++;
            ReleaseCell(effectEvent.CellIndex);
            _logSink.Warning(
                "wildfire_timberborn_pooled_fire_effects_failed " +
                $"stage=update tick={effectEvent.Tick} cell_index={effectEvent.CellIndex} " +
                $"message=\"{EscapeLogValue(exception.Message)}\"");
        }
    }

    public void CompleteVisualEffectDispatch(uint tick)
    {
        _currentTick = tick;
        RefreshActiveEffects(tick);
        if (_updatedVisualRegionsThisDispatch > 0)
        {
            _lastNonZeroUpdatedVisualRegionCount = _updatedVisualRegionsThisDispatch;
            _lastNonZeroUpdatedVisualRegionTick = tick;
        }

        _logSink.Info(
            "wildfire_timberborn_pooled_fire_effects_updated " +
            $"tick={tick} " +
            $"active_pooled_effects={_slotsByKey.Count} " +
            $"updated_visual_regions={_updatedVisualRegionsThisDispatch} " +
            $"last_nonzero_updated_visual_regions={_lastNonZeroUpdatedVisualRegionCount} " +
            $"last_nonzero_updated_visual_regions_tick={FormatNumber(_lastNonZeroUpdatedVisualRegionTick)} " +
            $"sampled_visual_cells={_sampledVisualCellsThisDispatch} " +
            $"dropped_visual_regions={_droppedVisualRegionsThisDispatch} " +
            $"disabled_visual_regions={_disabledVisualRegionsThisDispatch} " +
            $"presentation_failures={_presentationFailuresThisDispatch} " +
            $"max_pooled_effects={Options.MaxActiveEffects} " +
            $"max_updated_visual_regions={Options.MaxUpdatedVisualRegionsPerDispatch} " +
            $"visible_effects_enabled={_presenter.State.VisibleEffectsEnabled.ToString().ToLowerInvariant()} " +
            $"native_effect_prefab_resolved={_presenter.State.NativeEffectPrefabResolved.ToString().ToLowerInvariant()} " +
            $"native_effect_prefab={FormatLogToken(_lastNativeEffectPrefabName ?? _presenter.State.LastNativeEffectPrefabName)} " +
            $"visual_field_surface_bound={_visualFieldSurface.State.IsBound.ToString().ToLowerInvariant()}");
    }

    public void Clear()
    {
        _slotsByKey.Clear();
        _slotsById.Clear();
        _freeSlotIds.Clear();
        Enumerable.Range(0, Options.MaxActiveEffects)
            .ToList()
            .ForEach(_freeSlotIds.Enqueue);
        _updatedVisualRegionsThisDispatch = 0;
        _lastNonZeroUpdatedVisualRegionCount = 0;
        _sampledVisualCellsThisDispatch = 0;
        _droppedVisualRegionsThisDispatch = 0;
        _disabledVisualRegionsThisDispatch = 0;
        _presentationFailuresThisDispatch = 0;
        _currentTick = null;
        _lastNonZeroUpdatedVisualRegionTick = null;
        _lastNativeEffectPrefabName = null;
        try
        {
            _presenter.Clear();
        }
        catch (Exception exception)
        {
            _presentationFailuresThisDispatch++;
            _logSink.Warning(
                "wildfire_timberborn_pooled_fire_effects_failed " +
                $"stage=clear message=\"{EscapeLogValue(exception.Message)}\"");
        }
    }

    private void UpdateVisualEffectCore(TimberbornFireVisualEffectEvent effectEvent)
    {
        if (_currentTick != effectEvent.Tick)
        {
            BeginVisualEffectDispatch(effectEvent.Tick);
        }

        if (_updatedVisualRegionsThisDispatch >= Options.MaxUpdatedVisualRegionsPerDispatch)
        {
            _droppedVisualRegionsThisDispatch++;
            return;
        }

        if (!_visualFieldSurface.TryGetBinding(out TimberbornGpuVisualFieldSurfaceBinding binding))
        {
            _droppedVisualRegionsThisDispatch++;
            return;
        }

        TimberbornGpuVisualFieldSample sample = _visualFieldSurface
            .InspectCells(new[] { effectEvent.CellIndex })
            .Single();
        _sampledVisualCellsThisDispatch++;

        TimberbornPooledFireEffectState[] states = CreateEffectStates(binding, effectEvent, sample).ToArray();
        if (states.Length == 0)
        {
            ReleaseCell(effectEvent.CellIndex);
            _updatedVisualRegionsThisDispatch++;
            return;
        }

        states.ToList().ForEach(state => PresentEffectState(effectEvent, state));
    }

    private void PresentEffectState(
        TimberbornFireVisualEffectEvent effectEvent,
        TimberbornPooledFireEffectState state)
    {
        if (_updatedVisualRegionsThisDispatch >= Options.MaxUpdatedVisualRegionsPerDispatch)
        {
            _droppedVisualRegionsThisDispatch++;
            return;
        }

        TimberbornPooledFireEffectKey key = TimberbornPooledFireEffectKey.FromState(state);
        TimberbornPooledFireEffectSlot? slot = FindOrAllocateSlot(key, state.Intensity);
        if (slot is null)
        {
            _droppedVisualRegionsThisDispatch++;
            return;
        }

        TimberbornPooledFireEffectState slottedState = SmoothEffectState(slot.State, state) with
        {
            SlotId = slot.SlotId,
        };
        slot.State = slottedState;
        _slotsByKey[key] = slot;
        _slotsById[slot.SlotId] = slot;
        TimberbornPooledFireEffectPresentationResult presentationResult = _presenter.UpdateEffect(slottedState);
        if (presentationResult.NativeEffectPrefabName is not null)
        {
            _lastNativeEffectPrefabName = presentationResult.NativeEffectPrefabName;
        }

        if (presentationResult.Status == TimberbornPooledFireEffectPresentationStatus.Disabled)
        {
            _disabledVisualRegionsThisDispatch++;
            ReleaseKey(key);
            return;
        }

        if (presentationResult.Status == TimberbornPooledFireEffectPresentationStatus.Failed)
        {
            _presentationFailuresThisDispatch++;
            ReleaseKey(key);
            _logSink.Warning(
                "wildfire_timberborn_pooled_fire_effects_failed " +
                $"stage=presenter tick={effectEvent.Tick} cell_index={effectEvent.CellIndex} " +
                $"message=\"{EscapeLogValue(presentationResult.Message ?? "presentation failed")}\"");
            return;
        }

        _updatedVisualRegionsThisDispatch++;
    }

    private void RefreshActiveEffects(uint tick)
    {
        if (_slotsByKey.Count == 0)
        {
            return;
        }

        if (!_visualFieldSurface.TryGetBinding(out TimberbornGpuVisualFieldSurfaceBinding binding))
        {
            return;
        }

        TimberbornPooledFireEffectSlot[] slots = _slotsByKey.Values
            .OrderBy(static slot => slot.SlotId)
            .ToArray();

        slots
            .Where(slot => slot.State.Intensity <= 0f &&
                slot.State.Tick != tick &&
                tick >= slot.State.Tick &&
                tick - slot.State.Tick >= FinishedAnimationLifetimeTicks(slot.State.Kind))
            .ToList()
            .ForEach(slot => ReleaseKey(TimberbornPooledFireEffectKey.FromState(slot.State)));

        TimberbornPooledFireEffectSlot[] slotsToRefresh = slots
            .Where(slot => slot.State.Tick != tick &&
                _slotsById.ContainsKey(slot.SlotId))
            .ToArray();
        if (slotsToRefresh.Length == 0)
        {
            return;
        }

        int[] cellIndices = slotsToRefresh
            .Select(static slot => slot.State.CellIndex)
            .Distinct()
            .ToArray();
        IReadOnlyDictionary<int, TimberbornGpuVisualFieldSample> samplesByCellIndex = BatchCellIndices(cellIndices)
            .SelectMany(_visualFieldSurface.InspectCells)
            .ToDictionary(static sample => sample.CellIndex);
        _sampledVisualCellsThisDispatch += samplesByCellIndex.Count;

        slotsToRefresh.ToList().ForEach(slot =>
        {
            if (!_slotsById.ContainsKey(slot.SlotId))
            {
                return;
            }

            if (!samplesByCellIndex.TryGetValue(slot.State.CellIndex, out TimberbornGpuVisualFieldSample sample))
            {
                return;
            }

            TimberbornFireVisualEffectEvent refreshEvent = new(
                CellIndex: slot.State.CellIndex,
                Tick: tick,
                Kind: TimberbornFireVisualEffectKind.HeatChanged,
                Fuel: 0,
                Heat: 0,
                OldWater: 0,
                Water: 0,
                IsBurning: false);
            TimberbornPooledFireEffectKey key = TimberbornPooledFireEffectKey.FromState(slot.State);
            if (!TryCreateEffectState(
                binding,
                refreshEvent,
                sample,
                slot.State.Kind,
                out TimberbornPooledFireEffectState refreshedState))
            {
                if (LeavesParticlesToFinish(slot.State.Kind))
                {
                    StopEmittingSlot(slot);
                }
                else
                {
                    ReleaseKey(key);
                }

                return;
            }

            TimberbornPooledFireEffectState slottedState = SmoothEffectState(slot.State, refreshedState) with
            {
                SlotId = slot.SlotId,
            };
            slot.State = slottedState;
            TimberbornPooledFireEffectPresentationResult presentationResult = _presenter.UpdateEffect(slottedState);
            if (presentationResult.NativeEffectPrefabName is not null)
            {
                _lastNativeEffectPrefabName = presentationResult.NativeEffectPrefabName;
            }

            if (presentationResult.Status == TimberbornPooledFireEffectPresentationStatus.Disabled)
            {
                _disabledVisualRegionsThisDispatch++;
                ReleaseKey(key);
                return;
            }

            if (presentationResult.Status == TimberbornPooledFireEffectPresentationStatus.Failed)
            {
                _presentationFailuresThisDispatch++;
                ReleaseKey(key);
                _logSink.Warning(
                    "wildfire_timberborn_pooled_fire_effects_failed " +
                    $"stage=refresh tick={tick} cell_index={slot.State.CellIndex} " +
                    $"message=\"{EscapeLogValue(presentationResult.Message ?? "presentation failed")}\"");
            }
        });
    }

    private void StopEmittingSlot(TimberbornPooledFireEffectSlot slot)
    {
        TimberbornPooledFireEffectState stoppedState = StopEmittingState(slot.State);
        slot.State = stoppedState;
        TimberbornPooledFireEffectPresentationResult presentationResult = _presenter.UpdateEffect(stoppedState);
        if (presentationResult.NativeEffectPrefabName is not null)
        {
            _lastNativeEffectPrefabName = presentationResult.NativeEffectPrefabName;
        }
    }

    private static bool LeavesParticlesToFinish(TimberbornPooledFireEffectKind kind)
    {
        return kind is
            TimberbornPooledFireEffectKind.Fire or
            TimberbornPooledFireEffectKind.Steam or
            TimberbornPooledFireEffectKind.Smoke or
            TimberbornPooledFireEffectKind.ToxicSmoke;
    }

    private static uint FinishedAnimationLifetimeTicks(TimberbornPooledFireEffectKind kind)
    {
        return kind switch
        {
            TimberbornPooledFireEffectKind.Fire => 2u,
            TimberbornPooledFireEffectKind.Steam => 3u,
            TimberbornPooledFireEffectKind.Smoke => 4u,
            TimberbornPooledFireEffectKind.ToxicSmoke => 4u,
            _ => 1u,
        };
    }

    private static TimberbornPooledFireEffectState StopEmittingState(TimberbornPooledFireEffectState state)
    {
        return state.Kind switch
        {
            TimberbornPooledFireEffectKind.Fire => state with
            {
                Fire = 0f,
                Intensity = 0f,
            },
            TimberbornPooledFireEffectKind.Steam => state with
            {
                Steam = 0f,
                Intensity = 0f,
            },
            TimberbornPooledFireEffectKind.Smoke => state with
            {
                Smoke = 0f,
                Intensity = 0f,
            },
            TimberbornPooledFireEffectKind.ToxicSmoke => state with
            {
                Smoke = 0f,
                SmokeContamination = 0f,
                Intensity = 0f,
            },
            _ => state with
            {
                Intensity = 0f,
            },
        };
    }

    private TimberbornPooledFireEffectSlot? FindOrAllocateSlot(TimberbornPooledFireEffectKey key, float intensity)
    {
        if (_slotsByKey.TryGetValue(key, out TimberbornPooledFireEffectSlot? existingSlot))
        {
            return existingSlot;
        }

        if (_freeSlotIds.Count > 0)
        {
            return new TimberbornPooledFireEffectSlot(_freeSlotIds.Dequeue());
        }

        TimberbornPooledFireEffectSlot? replacement = _slotsById.Values
            .OrderBy(static slot => slot.State.Intensity)
            .ThenBy(static slot => slot.State.Tick)
            .ThenBy(static slot => slot.SlotId)
            .FirstOrDefault(slot => slot.State.Intensity < intensity);
        if (replacement is null)
        {
            return null;
        }

        _slotsByKey.Remove(TimberbornPooledFireEffectKey.FromState(replacement.State));
        return replacement;
    }

    private void ReleaseCell(int cellIndex)
    {
        _slotsByKey.Keys
            .Where(key => key.CellIndex == cellIndex)
            .ToArray()
            .ToList()
            .ForEach(ReleaseKey);
    }

    private void ReleaseKey(TimberbornPooledFireEffectKey key)
    {
        if (!_slotsByKey.TryGetValue(key, out TimberbornPooledFireEffectSlot? slot))
        {
            return;
        }

        _slotsByKey.Remove(key);
        _slotsById.Remove(slot.SlotId);
        _freeSlotIds.Enqueue(slot.SlotId);
        try
        {
            _presenter.ReleaseEffect(slot.SlotId);
        }
        catch (Exception exception)
        {
            _presentationFailuresThisDispatch++;
            _logSink.Warning(
                "wildfire_timberborn_pooled_fire_effects_failed " +
                $"stage=release cell_index={key.CellIndex} kind={key.Kind.ToString().ToLowerInvariant()} slot_id={slot.SlotId} " +
                $"message=\"{EscapeLogValue(exception.Message)}\"");
        }
    }

    private IEnumerable<TimberbornPooledFireEffectState> CreateEffectStates(
        TimberbornGpuVisualFieldSurfaceBinding binding,
        TimberbornFireVisualEffectEvent effectEvent,
        TimberbornGpuVisualFieldSample sample)
    {
        TimberbornPooledFireEffectKind[] lanes = new[]
        {
            TimberbornPooledFireEffectKind.Fire,
            TimberbornPooledFireEffectKind.Steam,
            TimberbornPooledFireEffectKind.Smoke,
            TimberbornPooledFireEffectKind.ToxicSmoke,
        };
        return lanes
            .Select(kind => TryCreateEffectState(
                binding,
                effectEvent,
                sample,
                kind,
                out TimberbornPooledFireEffectState state)
                    ? state
                    : (TimberbornPooledFireEffectState?)null)
            .Where(static state => state.HasValue)
            .Select(static state => state!.Value);
    }

    private bool TryCreateEffectState(
        TimberbornGpuVisualFieldSurfaceBinding binding,
        TimberbornFireVisualEffectEvent effectEvent,
        TimberbornGpuVisualFieldSample sample,
        TimberbornPooledFireEffectKind kind,
        out TimberbornPooledFireEffectState state)
    {
        float fire = Clamp01(sample.Fire);
        float smoke = Clamp01(sample.Smoke);
        float ash = Clamp01(sample.Ash);
        float smokeContamination = Clamp01(sample.SmokeContamination);
        float ashContamination = Clamp01(sample.AshContamination);
        float moistureDrop = Clamp01(Math.Max(0, effectEvent.OldWater - effectEvent.Water) / 3f);
        float steam = Clamp01(sample.Steam);
        float visibility = Clamp01(sample.Visibility);
        float fieldValue = FieldValue(kind, fire, smoke, steam, ash, smokeContamination, ashContamination);
        float intensity = fieldValue * visibility;
        FireSimWind wind = _windProvider.CurrentWind.Normalized();
        if (fieldValue < VisualLaneMinimum(kind) || intensity < Options.MinimumVisibleIntensity)
        {
            state = default;
            return false;
        }

        (int x, int y, int z) = FromIndex(binding, sample.CellIndex);
        state = new TimberbornPooledFireEffectState(
            SlotId: -1,
            CellIndex: sample.CellIndex,
            Tick: effectEvent.Tick,
            X: x,
            Y: y,
            Z: z,
            Kind: kind,
            Fire: fire,
            Smoke: smoke,
            Steam: steam,
            Ash: ash,
            Visibility: visibility,
            MoistureDrop: moistureDrop,
            Intensity: intensity,
            SmokeContamination: smokeContamination,
            AshContamination: ashContamination,
            WindDirectionX: wind.DirectionX,
            WindDirectionY: wind.DirectionY,
            WindStrength: wind.Strength);
        return true;
    }

    private static float FieldValue(
        TimberbornPooledFireEffectKind kind,
        float fire,
        float smoke,
        float steam,
        float ash,
        float smokeContamination,
        float ashContamination)
    {
        return kind switch
        {
            TimberbornPooledFireEffectKind.Fire => fire,
            TimberbornPooledFireEffectKind.Smoke => smoke,
            TimberbornPooledFireEffectKind.ToxicSmoke => smoke * smokeContamination,
            TimberbornPooledFireEffectKind.Steam => steam,
            TimberbornPooledFireEffectKind.Ash => ash,
            TimberbornPooledFireEffectKind.ToxicAsh => ash * ashContamination,
            _ => 0f,
        };
    }

    private static float VisualLaneMinimum(TimberbornPooledFireEffectKind kind)
    {
        return kind switch
        {
            TimberbornPooledFireEffectKind.Fire => 0.15f,
            TimberbornPooledFireEffectKind.Steam => 0.01f,
            TimberbornPooledFireEffectKind.Smoke => 0.15f,
            TimberbornPooledFireEffectKind.Ash => 0.12f,
            TimberbornPooledFireEffectKind.ToxicSmoke => 0.08f,
            TimberbornPooledFireEffectKind.ToxicAsh => 0.08f,
            _ => 0.01f,
        };
    }

    private static IEnumerable<int[]> BatchCellIndices(int[] cellIndices)
    {
        const int MaxCellsPerVisualFieldInspection = 256;
        int batchCount = (cellIndices.Length + MaxCellsPerVisualFieldInspection - 1) /
            MaxCellsPerVisualFieldInspection;
        return Enumerable.Range(0, batchCount)
            .Select(batchIndex => cellIndices
                .Skip(batchIndex * MaxCellsPerVisualFieldInspection)
                .Take(MaxCellsPerVisualFieldInspection)
                .ToArray());
    }

    private static TimberbornPooledFireEffectState SmoothEffectState(
        TimberbornPooledFireEffectState previous,
        TimberbornPooledFireEffectState next)
    {
        if (previous.SlotId < 0 ||
            previous.CellIndex != next.CellIndex ||
            previous.Kind != next.Kind ||
            previous.Tick == next.Tick)
        {
            return next;
        }

        float smoothing = next.Kind switch
        {
            TimberbornPooledFireEffectKind.Smoke => 0.32f,
            TimberbornPooledFireEffectKind.ToxicSmoke => 0.32f,
            _ => 1f,
        };
        return next with
        {
            Fire = SmoothLerp(previous.Fire, next.Fire, smoothing),
            Smoke = SmoothLerp(previous.Smoke, next.Smoke, smoothing),
            Steam = SmoothLerp(previous.Steam, next.Steam, smoothing),
            Ash = SmoothLerp(previous.Ash, next.Ash, smoothing),
            Visibility = SmoothLerp(previous.Visibility, next.Visibility, smoothing),
            MoistureDrop = SmoothLerp(previous.MoistureDrop, next.MoistureDrop, smoothing),
            Intensity = SmoothLerp(previous.Intensity, next.Intensity, smoothing),
            SmokeContamination = SmoothLerp(previous.SmokeContamination, next.SmokeContamination, smoothing),
            AshContamination = SmoothLerp(previous.AshContamination, next.AshContamination, smoothing),
            WindDirectionX = next.WindDirectionX,
            WindDirectionY = next.WindDirectionY,
            WindStrength = next.WindStrength,
        };
    }

    private static float SmoothLerp(float min, float max, float value)
    {
        return min + ((max - min) * Math.Clamp(value, 0f, 1f));
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

    private static float Clamp01(float value)
    {
        return Math.Clamp(value, 0f, 1f);
    }

    private static float Hash01(int cellIndex, int salt)
    {
        unchecked
        {
            uint value = (uint)cellIndex;
            value ^= (uint)salt * 0x9E3779B9u;
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0xFFFFu) / 65535f;
        }
    }

    private static string FormatNumber(uint? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }

    private static string FormatLogToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "placeholder"
            : value.Replace(' ', '_').Replace('"', '\'');
    }

    private static string EscapeLogValue(string value)
    {
        return value.Replace('\\', '/').Replace('"', '\'');
    }

    private sealed class TimberbornPooledFireEffectSlot
    {
        public TimberbornPooledFireEffectSlot(int slotId)
        {
            SlotId = slotId;
            State = default;
        }

        public int SlotId { get; }

        public TimberbornPooledFireEffectState State { get; set; }
    }
}

public sealed class TimberbornUnityPooledFireEffectPresenter : ITimberbornPooledFireEffectPresenter
{
    private readonly Dictionary<int, TimberbornPooledFireEffectInstance> _instancesBySlotId = new();
    private readonly Dictionary<TimberbornPooledFireEffectKind, TimberbornNativeFireEffectPrefabResolution> _resolutionByKind = new();
    private readonly ITimberbornFireLogSink _logSink;
    private GameObject? _root;
    private string? _lastNativeEffectPrefabName;

    public TimberbornUnityPooledFireEffectPresenter(ITimberbornFireLogSink logSink)
    {
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
    }

    public TimberbornPooledFireEffectPresenterState State => new(
        VisibleEffectsEnabled: _lastNativeEffectPrefabName is not null,
        NativeEffectPrefabResolved: _lastNativeEffectPrefabName is not null,
        LastNativeEffectPrefabName: _lastNativeEffectPrefabName);

    public TimberbornPooledFireEffectPresentationResult UpdateEffect(TimberbornPooledFireEffectState state)
    {
        TimberbornNativeFireEffectPrefabResolution resolution = Resolve(state.Kind);
        if (!resolution.IsResolved || resolution.Prefab is null)
        {
            return TimberbornPooledFireEffectPresentationResult.Disabled(
                "native_effect_prefab_unavailable");
        }

        GameObject instance = GetOrCreateInstance(state, resolution.Prefab, out bool created);
        instance.name = $"Wildfire {state.Kind} Effect {state.SlotId}";
        instance.SetActive(true);
        if (state.Kind == TimberbornPooledFireEffectKind.Fire && !created)
        {
            ConfigureParticleEmission(instance, state);
        }
        else
        {
            TimberbornPooledFireEffectLocalPosition position = ToUnityLocalPosition(state);
            instance.transform.localPosition = new Vector3(position.X, position.Y, position.Z);
            instance.transform.localScale = Vector3.one;
            ConfigureParticleSystem(instance, state);
        }

        _lastNativeEffectPrefabName = resolution.PrefabName;
        return TimberbornPooledFireEffectPresentationResult.Applied(resolution.PrefabName);
    }

    public void ReleaseEffect(int slotId)
    {
        if (_instancesBySlotId.TryGetValue(slotId, out TimberbornPooledFireEffectInstance? instance))
        {
            ParticleSystem? particleSystem = instance.GameObject.GetComponent<ParticleSystem>();
            if (particleSystem is null)
            {
                instance.GameObject.SetActive(false);
                return;
            }

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);
            particleSystem.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    public void Clear()
    {
        _instancesBySlotId.Values
            .Select(static instance => instance.GameObject)
            .Where(static gameObject => gameObject != null)
            .ToList()
            .ForEach(UnityEngine.Object.Destroy);
        _instancesBySlotId.Clear();
        _resolutionByKind.Clear();
        if (_root is not null)
        {
            UnityEngine.Object.Destroy(_root);
            _root = null;
        }
        _lastNativeEffectPrefabName = null;
    }

    public static TimberbornPooledFireEffectLocalPosition ToUnityLocalPosition(TimberbornPooledFireEffectState state)
    {
        float jitterRadius = state.Kind switch
        {
            TimberbornPooledFireEffectKind.Fire => 0f,
            TimberbornPooledFireEffectKind.Steam => 0.42f,
            TimberbornPooledFireEffectKind.Smoke => 0.42f,
            TimberbornPooledFireEffectKind.ToxicSmoke => 0.42f,
            _ => 0.32f,
        };
        float jitterX = (Hash01(state.CellIndex, 11) - 0.5f) * jitterRadius * 2f;
        float jitterZ = (Hash01(state.CellIndex, 23) - 0.5f) * jitterRadius * 2f;
        float verticalLift = state.Kind switch
        {
            TimberbornPooledFireEffectKind.Steam => 0.45f,
            TimberbornPooledFireEffectKind.Smoke => 0.45f,
            TimberbornPooledFireEffectKind.ToxicSmoke => 0.48f,
            TimberbornPooledFireEffectKind.Ash => 0.2f,
            TimberbornPooledFireEffectKind.ToxicAsh => 0.24f,
            _ => 0.02f,
        };
        return new TimberbornPooledFireEffectLocalPosition(
            state.X + 0.5f + jitterX,
            state.Z + (state.Kind is TimberbornPooledFireEffectKind.Fire
                ? verticalLift
                : 0.5f + verticalLift),
            state.Y + 0.5f + jitterZ);
    }

    public static bool CanReuseInstance(
        TimberbornPooledFireEffectKind existingKind,
        string? existingPrefabName,
        TimberbornPooledFireEffectKind requestedKind,
        string? requestedPrefabName)
    {
        return existingKind == requestedKind &&
            string.Equals(existingPrefabName, requestedPrefabName, StringComparison.Ordinal);
    }

    private GameObject GetOrCreateInstance(
        TimberbornPooledFireEffectState state,
        GameObject nativePrefab,
        out bool created)
    {
        int slotId = state.SlotId;
        if (_instancesBySlotId.TryGetValue(slotId, out TimberbornPooledFireEffectInstance? existingInstance))
        {
            if (CanReuseInstance(
                existingInstance.Kind,
                existingInstance.PrefabName,
                state.Kind,
                nativePrefab.name))
            {
                created = false;
                return existingInstance.GameObject;
            }

            UnityEngine.Object.Destroy(existingInstance.GameObject);
            _instancesBySlotId.Remove(slotId);
        }

        GameObject createdInstance = UnityEngine.Object.Instantiate(nativePrefab);
        createdInstance.hideFlags = HideFlags.DontSave;
        createdInstance.SetActive(false);
        createdInstance.transform.SetParent(GetOrCreateRoot().transform, worldPositionStays: false);
        _instancesBySlotId[slotId] = new TimberbornPooledFireEffectInstance(
            createdInstance,
            state.Kind,
            nativePrefab.name);
        created = true;
        return createdInstance;
    }

    private static void ConfigureParticleEmission(GameObject instance, TimberbornPooledFireEffectState state)
    {
        ParticleSystem? particleSystem = instance.GetComponent<ParticleSystem>();
        if (particleSystem is null)
        {
            return;
        }

        float fieldValue = FieldValue(state);
        float visibleIntensity = Math.Clamp(fieldValue * state.Visibility, 0f, 1f);
        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(EmissionRate(state.Kind, visibleIntensity));
        if (!particleSystem.isPlaying)
        {
            particleSystem.Play(withChildren: false);
        }
    }

    private static void ConfigureParticleSystem(GameObject instance, TimberbornPooledFireEffectState state)
    {
        ParticleSystem? particleSystem = instance.GetComponent<ParticleSystem>();
        if (particleSystem is null)
        {
            return;
        }

        float fieldValue = FieldValue(state);
        float visibleIntensity = Math.Clamp(fieldValue * state.Visibility, 0f, 1f);
        ParticleSystem.MainModule main = particleSystem.main;
        main.startLifetime = StartLifetime(state.Kind, visibleIntensity);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
        main.startSize = new ParticleSystem.MinMaxCurve(
            StartSizeMin(state.Kind, visibleIntensity),
            StartSizeMax(state.Kind, visibleIntensity));
        main.startColor = new ParticleSystem.MinMaxGradient(
            TimberbornProceduralFireSmokeAshEffectPrefabCatalog.StartColor(state.Kind, visibleIntensity));

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(EmissionRate(state.Kind, visibleIntensity));

        ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = HorizontalDriftVelocity(state, salt: 43, axis: 0);
        velocity.y = new ParticleSystem.MinMaxCurve(
            UpwardVelocityMin(state.Kind, OverLifetimeIntensity(state.Kind, visibleIntensity)),
            UpwardVelocityMax(state.Kind, OverLifetimeIntensity(state.Kind, visibleIntensity)));
        velocity.z = HorizontalDriftVelocity(state, salt: 59, axis: 1);

        ParticleSystem.ForceOverLifetimeModule force = particleSystem.forceOverLifetime;
        force.enabled = state.Kind == TimberbornPooledFireEffectKind.Fire;
        force.space = ParticleSystemSimulationSpace.World;
        force.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        force.y = state.Kind == TimberbornPooledFireEffectKind.Fire
            ? new ParticleSystem.MinMaxCurve(0.3f, 0.6f)
            : new ParticleSystem.MinMaxCurve(0f, 0f);
        force.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.color =
            TimberbornProceduralFireSmokeAshEffectPrefabCatalog.LifetimeGradient(
                state.Kind,
                OverLifetimeIntensity(state.Kind, visibleIntensity));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        if (!particleSystem.isPlaying)
        {
            particleSystem.Play(withChildren: false);
        }
    }

    private static float FieldValue(TimberbornPooledFireEffectState state)
    {
        return state.Kind switch
        {
            TimberbornPooledFireEffectKind.Fire => state.Fire,
            TimberbornPooledFireEffectKind.Smoke => state.Smoke,
            TimberbornPooledFireEffectKind.ToxicSmoke => state.Smoke * state.SmokeContamination,
            TimberbornPooledFireEffectKind.Steam => state.Steam,
            TimberbornPooledFireEffectKind.ToxicAsh => state.Ash * state.AshContamination,
            _ => state.Ash,
        };
    }

    private static float OverLifetimeIntensity(TimberbornPooledFireEffectKind kind, float intensity)
    {
        return kind == TimberbornPooledFireEffectKind.Fire ? 1f : intensity;
    }

    private static float EmissionRate(TimberbornPooledFireEffectKind kind, float intensity)
    {
        if (intensity <= 0f)
        {
            return 0f;
        }

        return kind switch
        {
            TimberbornPooledFireEffectKind.Fire => Lerp(6f, 32f, intensity),
            TimberbornPooledFireEffectKind.Smoke => Lerp(1f, 4.5f, intensity),
            TimberbornPooledFireEffectKind.ToxicSmoke => Lerp(0.75f, 3.5f, intensity),
            TimberbornPooledFireEffectKind.Steam => Lerp(3f, 13.5f, intensity),
            TimberbornPooledFireEffectKind.ToxicAsh => Lerp(3f, 14f, intensity),
            _ => Lerp(4f, 18f, intensity),
        };
    }

    private static ParticleSystem.MinMaxCurve StartLifetime(TimberbornPooledFireEffectKind kind, float intensity)
    {
        return kind switch
        {
            TimberbornPooledFireEffectKind.Fire => new ParticleSystem.MinMaxCurve(
                Lerp(0.55f, 0.75f, intensity),
                Lerp(0.8f, 1.05f, intensity)),
            TimberbornPooledFireEffectKind.Smoke => new ParticleSystem.MinMaxCurve(4.8f),
            TimberbornPooledFireEffectKind.ToxicSmoke => new ParticleSystem.MinMaxCurve(5.4f),
            TimberbornPooledFireEffectKind.Steam => new ParticleSystem.MinMaxCurve(4.8f),
            TimberbornPooledFireEffectKind.ToxicAsh => new ParticleSystem.MinMaxCurve(2.1f),
            _ => new ParticleSystem.MinMaxCurve(1.8f),
        };
    }

    private static float StartSizeMin(TimberbornPooledFireEffectKind kind, float intensity)
    {
        return kind switch
        {
            TimberbornPooledFireEffectKind.Fire => Lerp(0.08f, 0.16f, intensity),
            TimberbornPooledFireEffectKind.Smoke => Lerp(0.45f, 0.85f, intensity),
            TimberbornPooledFireEffectKind.ToxicSmoke => Lerp(0.42f, 0.78f, intensity),
            TimberbornPooledFireEffectKind.Steam => Lerp(0.45f, 0.85f, intensity),
            TimberbornPooledFireEffectKind.ToxicAsh => Lerp(0.2f, 0.42f, intensity),
            _ => Lerp(0.22f, 0.46f, intensity),
        };
    }

    private static float StartSizeMax(TimberbornPooledFireEffectKind kind, float intensity)
    {
        return kind switch
        {
            TimberbornPooledFireEffectKind.Fire => Lerp(0.22f, 0.48f, intensity),
            TimberbornPooledFireEffectKind.Smoke => Lerp(0.9f, 1.55f, intensity),
            TimberbornPooledFireEffectKind.ToxicSmoke => Lerp(0.84f, 1.38f, intensity),
            TimberbornPooledFireEffectKind.Steam => Lerp(0.9f, 1.55f, intensity),
            TimberbornPooledFireEffectKind.ToxicAsh => Lerp(0.38f, 0.78f, intensity),
            _ => Lerp(0.42f, 0.82f, intensity),
        };
    }

    private static float UpwardVelocityMin(TimberbornPooledFireEffectKind kind, float intensity)
    {
        return kind switch
        {
            TimberbornPooledFireEffectKind.Fire => Lerp(1.85f, 2.85f, intensity),
            TimberbornPooledFireEffectKind.Smoke => Lerp(0.35f, 0.65f, intensity),
            TimberbornPooledFireEffectKind.ToxicSmoke => Lerp(0.3f, 0.58f, intensity),
            TimberbornPooledFireEffectKind.Steam => Lerp(0.35f, 0.65f, intensity),
            TimberbornPooledFireEffectKind.ToxicAsh => Lerp(0.06f, 0.18f, intensity),
            _ => Lerp(0.08f, 0.22f, intensity),
        };
    }

    private static float UpwardVelocityMax(TimberbornPooledFireEffectKind kind, float intensity)
    {
        return kind switch
        {
            TimberbornPooledFireEffectKind.Fire => Lerp(3.75f, 5.35f, intensity),
            TimberbornPooledFireEffectKind.Smoke => Lerp(0.85f, 1.35f, intensity),
            TimberbornPooledFireEffectKind.ToxicSmoke => Lerp(0.72f, 1.2f, intensity),
            TimberbornPooledFireEffectKind.Steam => Lerp(0.85f, 1.35f, intensity),
            TimberbornPooledFireEffectKind.ToxicAsh => Lerp(0.14f, 0.36f, intensity),
            _ => Lerp(0.18f, 0.44f, intensity),
        };
    }

    private static ParticleSystem.MinMaxCurve HorizontalDriftVelocity(
        TimberbornPooledFireEffectState state,
        int salt,
        int axis)
    {
        if (state.Kind == TimberbornPooledFireEffectKind.Fire)
        {
            float wind = (axis == 0 ? state.WindDirectionX : state.WindDirectionY) * state.WindStrength * 0.5f;
            float drift = wind + ((Hash01(state.CellIndex, salt) - 0.5f) * 0.5f);
            return new ParticleSystem.MinMaxCurve(drift - 0.5f, drift + 0.5f);
        }

        if (state.Kind is
            TimberbornPooledFireEffectKind.Smoke or
            TimberbornPooledFireEffectKind.ToxicSmoke or
            TimberbornPooledFireEffectKind.Steam)
        {
            float wind = (axis == 0 ? state.WindDirectionX : state.WindDirectionY) *
                Lerp(0.35f, 1.35f, state.WindStrength);
            float jitter = (Hash01(state.CellIndex, salt) - 0.5f) * 0.3f;
            return new ParticleSystem.MinMaxCurve(wind + jitter - 0.18f, wind + jitter + 0.18f);
        }

        return new ParticleSystem.MinMaxCurve(0f, 0f);
    }

    private static float Lerp(float min, float max, float value)
    {
        return min + ((max - min) * Math.Clamp(value, 0f, 1f));
    }

    private GameObject GetOrCreateRoot()
    {
        if (_root is not null)
        {
            return _root;
        }

        _root = new GameObject("Wildfire Pooled Fire Smoke Ash Effects")
        {
            hideFlags = HideFlags.DontSave,
        };
        return _root;
    }

    private TimberbornNativeFireEffectPrefabResolution Resolve(TimberbornPooledFireEffectKind kind)
    {
        if (_resolutionByKind.TryGetValue(kind, out TimberbornNativeFireEffectPrefabResolution resolution))
        {
            return resolution;
        }

        resolution = TimberbornProceduralFireSmokeAshEffectPrefabCatalog.Resolve(kind);
        _resolutionByKind[kind] = resolution;
        if (resolution.IsResolved)
        {
            _logSink.Info(
                "wildfire_timberborn_pooled_fire_effect_native_prefab_resolved " +
                $"kind={kind.ToString().ToLowerInvariant()} " +
                $"prefab={FormatLogToken(resolution.PrefabName)}");
        }
        else
        {
            _logSink.Warning(
                "wildfire_timberborn_pooled_fire_effect_native_prefab_unavailable " +
                $"kind={kind.ToString().ToLowerInvariant()} " +
                $"preferred={FormatLogToken(string.Join(",", resolution.PreferredNames))}");
        }

        return resolution;
    }

    private static string FormatLogToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "placeholder"
            : value.Replace(' ', '_').Replace('"', '\'');
    }

    private static float Hash01(int cellIndex, int salt)
    {
        unchecked
        {
            uint value = (uint)cellIndex;
            value ^= (uint)salt * 0x9E3779B9u;
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0xFFFFu) / 65535f;
        }
    }

    private sealed record TimberbornPooledFireEffectInstance(
        GameObject GameObject,
        TimberbornPooledFireEffectKind Kind,
        string? PrefabName);
}

public static class TimberbornProceduralFireSmokeAshEffectPrefabCatalog
{
    public static TimberbornNativeFireEffectPrefabResolution Resolve(TimberbornPooledFireEffectKind kind)
    {
        GameObject prefab = CreatePrefab(kind);
        return new TimberbornNativeFireEffectPrefabResolution(
            IsResolved: true,
            Prefab: prefab,
            PrefabName: prefab.name,
            PreferredNames: new[] { prefab.name });
    }

    private static GameObject CreatePrefab(TimberbornPooledFireEffectKind kind)
    {
        GameObject prefab = new($"WildfireProcedural{kind}Particles")
        {
            hideFlags = HideFlags.DontSave,
        };
        prefab.SetActive(false);

        ParticleSystem particleSystem = prefab.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 3f;
        main.startLifetime = kind switch
        {
            TimberbornPooledFireEffectKind.Fire => new ParticleSystem.MinMaxCurve(0.6f, 0.95f),
            TimberbornPooledFireEffectKind.Smoke => new ParticleSystem.MinMaxCurve(4.8f),
            TimberbornPooledFireEffectKind.ToxicSmoke => new ParticleSystem.MinMaxCurve(5.4f),
            TimberbornPooledFireEffectKind.Steam => new ParticleSystem.MinMaxCurve(4.8f),
            TimberbornPooledFireEffectKind.ToxicAsh => new ParticleSystem.MinMaxCurve(2.1f),
            _ => new ParticleSystem.MinMaxCurve(1.8f),
        };
        main.startSpeed = 0f;
        main.startSize = kind switch
        {
            TimberbornPooledFireEffectKind.Fire => new ParticleSystem.MinMaxCurve(0.08f, 0.38f),
            TimberbornPooledFireEffectKind.Smoke => new ParticleSystem.MinMaxCurve(0.85f, 1.75f),
            TimberbornPooledFireEffectKind.ToxicSmoke => new ParticleSystem.MinMaxCurve(0.75f, 1.55f),
            TimberbornPooledFireEffectKind.Steam => new ParticleSystem.MinMaxCurve(0.85f, 1.75f),
            TimberbornPooledFireEffectKind.ToxicAsh => new ParticleSystem.MinMaxCurve(0.26f, 0.72f),
            _ => new ParticleSystem.MinMaxCurve(0.28f, 0.62f),
        };
        main.startColor = new ParticleSystem.MinMaxGradient(StartColor(kind));
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = kind switch
        {
            TimberbornPooledFireEffectKind.Fire => 24f,
            TimberbornPooledFireEffectKind.Smoke => 6.5f,
            TimberbornPooledFireEffectKind.ToxicSmoke => 4f,
            TimberbornPooledFireEffectKind.Steam => 19.5f,
            TimberbornPooledFireEffectKind.ToxicAsh => 12f,
            _ => 8f,
        };

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        if (kind is TimberbornPooledFireEffectKind.Fire)
        {
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.92f, 0.04f, 0.92f);
        }
        else
        {
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = kind switch
            {
                TimberbornPooledFireEffectKind.Steam => 0.45f,
                TimberbornPooledFireEffectKind.Smoke => 0.45f,
                TimberbornPooledFireEffectKind.ToxicSmoke => 0.42f,
                TimberbornPooledFireEffectKind.ToxicAsh => 0.3f,
                _ => 0.28f,
            };
        }

        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.x = kind switch
        {
            TimberbornPooledFireEffectKind.Fire => new ParticleSystem.MinMaxCurve(-0.55f, 0.55f),
            TimberbornPooledFireEffectKind.Steam => new ParticleSystem.MinMaxCurve(0.24f, 0.66f),
            TimberbornPooledFireEffectKind.Smoke => new ParticleSystem.MinMaxCurve(0.24f, 0.66f),
            TimberbornPooledFireEffectKind.ToxicSmoke => new ParticleSystem.MinMaxCurve(0.2f, 0.58f),
            _ => new ParticleSystem.MinMaxCurve(0f, 0f),
        };
        velocityOverLifetime.y = kind switch
        {
            TimberbornPooledFireEffectKind.Fire => new ParticleSystem.MinMaxCurve(4.4f, 9.2f),
            TimberbornPooledFireEffectKind.Steam => new ParticleSystem.MinMaxCurve(0.55f, 1.1f),
            TimberbornPooledFireEffectKind.Smoke => new ParticleSystem.MinMaxCurve(0.55f, 1.1f),
            TimberbornPooledFireEffectKind.ToxicSmoke => new ParticleSystem.MinMaxCurve(0.45f, 0.95f),
            TimberbornPooledFireEffectKind.ToxicAsh => new ParticleSystem.MinMaxCurve(0.1f, 0.28f),
            _ => new ParticleSystem.MinMaxCurve(0.12f, 0.32f),
        };
        velocityOverLifetime.z = kind switch
        {
            TimberbornPooledFireEffectKind.Fire => new ParticleSystem.MinMaxCurve(-0.55f, 0.55f),
            TimberbornPooledFireEffectKind.Steam => new ParticleSystem.MinMaxCurve(0.06f, 0.36f),
            TimberbornPooledFireEffectKind.Smoke => new ParticleSystem.MinMaxCurve(0.06f, 0.36f),
            TimberbornPooledFireEffectKind.ToxicSmoke => new ParticleSystem.MinMaxCurve(0.04f, 0.32f),
            _ => new ParticleSystem.MinMaxCurve(0f, 0f),
        };

        ParticleSystem.ForceOverLifetimeModule forceOverLifetime = particleSystem.forceOverLifetime;
        forceOverLifetime.enabled = kind == TimberbornPooledFireEffectKind.Fire;
        forceOverLifetime.space = ParticleSystemSimulationSpace.World;
        forceOverLifetime.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        forceOverLifetime.y = kind == TimberbornPooledFireEffectKind.Fire
            ? new ParticleSystem.MinMaxCurve(0.3f, 0.6f)
            : new ParticleSystem.MinMaxCurve(0f, 0f);
        forceOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new();
        Color start = StartColor(kind);
        Color mid = MidColor(kind);
        Color end = EndColor(kind);
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(start, 0f),
                new GradientColorKey(mid, 0.45f),
                new GradientColorKey(end, 1f),
            },
            new[]
            {
                new GradientAlphaKey(start.a, 0f),
                new GradientAlphaKey(mid.a, 0.45f),
                new GradientAlphaKey(0f, 1f),
            });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = kind switch
        {
            TimberbornPooledFireEffectKind.Fire =>
                new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1.25f, 1f, 0.18f)),
            TimberbornPooledFireEffectKind.Smoke =>
                new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.85f)),
            TimberbornPooledFireEffectKind.ToxicSmoke =>
                new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.65f)),
            TimberbornPooledFireEffectKind.Steam =>
                new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.85f)),
            TimberbornPooledFireEffectKind.ToxicAsh =>
                new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.58f, 1f, 1.35f)),
            _ => new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.45f)),
        };

        ParticleSystemRenderer renderer = prefab.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        Shader? shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
        if (shader is not null)
        {
            renderer.sharedMaterial = CreateParticleMaterial(shader, kind);
        }

        return prefab;
    }

    private static Material CreateParticleMaterial(Shader shader, TimberbornPooledFireEffectKind kind)
    {
        Material material = new(shader)
        {
            hideFlags = HideFlags.DontSave,
        };
        material.mainTexture = CreateCircularParticleTexture(kind);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 500;
        if (material.HasProperty("_ZWrite"))
        {
            material.SetInt("_ZWrite", 0);
        }

        return material;
    }

    private static Texture2D CreateCircularParticleTexture(TimberbornPooledFireEffectKind kind)
    {
        const int size = 32;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            name = $"Wildfire Circular {kind} Particle Texture",
            hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        float edgeSoftness = kind == TimberbornPooledFireEffectKind.Fire ? 0.18f : 0.32f;
        Color[] pixels = Enumerable.Range(0, size * size)
            .Select(index =>
            {
                int x = index % size;
                int y = index / size;
                float normalizedX = ((x + 0.5f) / size * 2f) - 1f;
                float normalizedY = ((y + 0.5f) / size * 2f) - 1f;
                float distance = MathF.Sqrt((normalizedX * normalizedX) + (normalizedY * normalizedY));
                float alpha = Math.Clamp((1f - distance) / edgeSoftness, 0f, 1f);
                return new Color(1f, 1f, 1f, alpha);
            })
            .ToArray();
        texture.SetPixels(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        return texture;
    }

    public static Color StartColor(TimberbornPooledFireEffectKind kind)
    {
        return StartColor(kind, 1f);
    }

    public static Color StartColor(TimberbornPooledFireEffectKind kind, float intensity)
    {
        return kind switch
        {
            TimberbornPooledFireEffectKind.Fire => new Color(1f, 0.9f, 0.35f, Lerp(0.24f, 0.58f, intensity)),
            TimberbornPooledFireEffectKind.Smoke => new Color(0.88f, 0.88f, 0.82f, Lerp(0.5f, 0.78f, intensity)),
            TimberbornPooledFireEffectKind.ToxicSmoke => new Color(0.42f, 0.03f, 0.1f, Lerp(0.36f, 0.66f, intensity)),
            TimberbornPooledFireEffectKind.Steam => new Color(0.96f, 0.96f, 0.92f, Lerp(0.34f, 0.62f, intensity)),
            TimberbornPooledFireEffectKind.ToxicAsh => new Color(0.52f, 0.58f, 0.38f, Lerp(0.34f, 0.64f, intensity)),
            _ => new Color(0.46f, 0.46f, 0.42f, Lerp(0.4f, 0.7f, intensity)),
        };
    }

    public static Color MidColor(TimberbornPooledFireEffectKind kind)
    {
        return MidColor(kind, 1f);
    }

    public static Color MidColor(TimberbornPooledFireEffectKind kind, float intensity)
    {
        return kind switch
        {
            TimberbornPooledFireEffectKind.Fire => new Color(1f, 0.32f, 0.04f, Lerp(0.62f, 0.94f, intensity)),
            TimberbornPooledFireEffectKind.Smoke => new Color(0.74f, 0.74f, 0.68f, Lerp(0.4f, 0.64f, intensity)),
            TimberbornPooledFireEffectKind.ToxicSmoke => new Color(0.26f, 0.01f, 0.07f, Lerp(0.28f, 0.52f, intensity)),
            TimberbornPooledFireEffectKind.Steam => new Color(0.86f, 0.86f, 0.82f, Lerp(0.24f, 0.5f, intensity)),
            TimberbornPooledFireEffectKind.ToxicAsh => new Color(0.38f, 0.5f, 0.32f, Lerp(0.24f, 0.48f, intensity)),
            _ => new Color(0.42f, 0.42f, 0.39f, Lerp(0.28f, 0.52f, intensity)),
        };
    }

    private static Color EndColor(TimberbornPooledFireEffectKind kind)
    {
        return kind switch
        {
            TimberbornPooledFireEffectKind.Fire => new Color(0.45f, 0.03f, 0.01f, 0f),
            TimberbornPooledFireEffectKind.Smoke => new Color(0.62f, 0.62f, 0.58f, 0f),
            TimberbornPooledFireEffectKind.ToxicSmoke => new Color(0.12f, 0.0f, 0.04f, 0f),
            TimberbornPooledFireEffectKind.Steam => new Color(0.82f, 0.82f, 0.78f, 0f),
            TimberbornPooledFireEffectKind.ToxicAsh => new Color(0.28f, 0.38f, 0.24f, 0f),
            _ => new Color(0.32f, 0.32f, 0.3f, 0f),
        };
    }

    public static Gradient LifetimeGradient(TimberbornPooledFireEffectKind kind, float intensity)
    {
        Gradient gradient = new();
        Color start = StartColor(kind, intensity);
        Color mid = MidColor(kind, intensity);
        Color end = EndColor(kind);
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(start, 0f),
                new GradientColorKey(mid, 0.45f),
                new GradientColorKey(end, 1f),
            },
            new[]
            {
                new GradientAlphaKey(start.a, 0f),
                new GradientAlphaKey(mid.a, 0.45f),
                new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }

    private static float Lerp(float min, float max, float value)
    {
        return min + ((max - min) * Math.Clamp(value, 0f, 1f));
    }
}

public sealed record TimberbornNativeFireEffectPrefabResolution(
    bool IsResolved,
    GameObject? Prefab,
    string? PrefabName,
    IReadOnlyList<string> PreferredNames);

public static class TimberbornNativeFireEffectPrefabCatalog
{
    private static IReadOnlyList<GameObject>? _prefabs;

    public static TimberbornNativeFireEffectPrefabResolution Resolve(TimberbornPooledFireEffectKind kind)
    {
        return ResolveFromPrefabs(kind, LoadPrefabs());
    }

    public static TimberbornNativeFireEffectPrefabResolution Probe(TimberbornPooledFireEffectKind kind)
    {
        return ResolveFromPrefabs(kind, Resources.LoadAll<GameObject>(string.Empty));
    }

    private static TimberbornNativeFireEffectPrefabResolution ResolveFromPrefabs(
        TimberbornPooledFireEffectKind kind,
        IEnumerable<GameObject> prefabs)
    {
        string[] preferredNames = PreferredNames(kind);
        GameObject[] loadedPrefabs = prefabs.ToArray();
        GameObject[] exactMatches = loadedPrefabs
            .Where(prefab => preferredNames.Any(name => string.Equals(prefab.name, name, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (exactMatches.Length > 0)
        {
            return new TimberbornNativeFireEffectPrefabResolution(
                IsResolved: true,
                Prefab: exactMatches[0],
                PrefabName: exactMatches[0].name,
                PreferredNames: preferredNames);
        }

        string[] broadNames = BroadNames(kind);
        GameObject? broadMatch = loadedPrefabs
            .FirstOrDefault(prefab => broadNames.Any(name =>
                prefab.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0));
        if (broadMatch is not null)
        {
            return new TimberbornNativeFireEffectPrefabResolution(
                IsResolved: true,
                Prefab: broadMatch,
                PrefabName: broadMatch.name,
                PreferredNames: preferredNames);
        }

        return new TimberbornNativeFireEffectPrefabResolution(
            IsResolved: false,
            Prefab: null,
            PrefabName: null,
            PreferredNames: preferredNames);
    }

    private static IReadOnlyList<GameObject> LoadPrefabs()
    {
        _prefabs ??= Resources.LoadAll<GameObject>(string.Empty);
        return _prefabs;
    }

    private static string[] PreferredNames(TimberbornPooledFireEffectKind kind)
    {
        return kind switch
        {
            TimberbornPooledFireEffectKind.Fire => new[] { "CampfireFire", "Sparks_Trail" },
            TimberbornPooledFireEffectKind.Smoke => new[] { "SmelterSmoke", "SteamEngineSmoke" },
            TimberbornPooledFireEffectKind.ToxicSmoke => new[] { "SmelterSmoke", "SteamEngineSmoke" },
            TimberbornPooledFireEffectKind.Ash => new[] { "SmelterSmoke", "SteamEngineSmoke" },
            TimberbornPooledFireEffectKind.ToxicAsh => new[] { "SmelterSmoke", "SteamEngineSmoke" },
            _ => Array.Empty<string>(),
        };
    }

    private static string[] BroadNames(TimberbornPooledFireEffectKind kind)
    {
        return kind switch
        {
            TimberbornPooledFireEffectKind.Fire => new[] { "fire", "spark" },
            TimberbornPooledFireEffectKind.Smoke => new[] { "smoke" },
            TimberbornPooledFireEffectKind.ToxicSmoke => new[] { "smoke" },
            TimberbornPooledFireEffectKind.Ash => new[] { "ash", "smoke" },
            TimberbornPooledFireEffectKind.ToxicAsh => new[] { "ash", "smoke" },
            _ => Array.Empty<string>(),
        };
    }
}

public sealed class NullTimberbornPooledFireEffectPresenter : ITimberbornPooledFireEffectPresenter
{
    public static readonly NullTimberbornPooledFireEffectPresenter Instance = new();

    private NullTimberbornPooledFireEffectPresenter()
    {
    }

    public TimberbornPooledFireEffectPresenterState State => new(
        VisibleEffectsEnabled: false,
        NativeEffectPrefabResolved: false,
        LastNativeEffectPrefabName: null);

    public TimberbornPooledFireEffectPresentationResult UpdateEffect(TimberbornPooledFireEffectState state)
    {
        return TimberbornPooledFireEffectPresentationResult.Disabled("null_presenter");
    }

    public void ReleaseEffect(int slotId)
    {
    }

    public void Clear()
    {
    }
}
