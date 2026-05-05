using UnityEngine;

namespace Wildfire.Timberborn;

public enum TimberbornPooledFireEffectKind
{
    Fire,
    Smoke,
    Ash,
}

public sealed class TimberbornPooledFireEffectOptions
{
    public static readonly TimberbornPooledFireEffectOptions Default = new();

    public TimberbornPooledFireEffectOptions(
        int MaxActiveEffects = 256,
        int MaxUpdatedVisualRegionsPerDispatch = 512,
        float MinimumVisibleIntensity = 0.01f)
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

        this.MaxActiveEffects = MaxActiveEffects;
        this.MaxUpdatedVisualRegionsPerDispatch = MaxUpdatedVisualRegionsPerDispatch;
        this.MinimumVisibleIntensity = MinimumVisibleIntensity;
    }

    public int MaxActiveEffects { get; }

    public int MaxUpdatedVisualRegionsPerDispatch { get; }

    public float MinimumVisibleIntensity { get; }
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
    float Ash,
    float Visibility,
    float Intensity);

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
    private readonly ITimberbornPooledFireEffectPresenter _presenter;
    private readonly Queue<int> _freeSlotIds;
    private readonly Dictionary<int, TimberbornPooledFireEffectSlot> _slotsByCellIndex = new();
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
            TimberbornPooledFireEffectOptions.Default,
            new TimberbornUnityPooledFireEffectPresenter(logSink))
    {
    }

    public TimberbornPooledFireSmokeAshEffectSink(
        ITimberbornGpuVisualFieldSurface visualFieldSurface,
        ITimberbornFireLogSink logSink,
        TimberbornPooledFireEffectOptions options,
        ITimberbornPooledFireEffectPresenter presenter)
    {
        _visualFieldSurface = visualFieldSurface ?? throw new ArgumentNullException(nameof(visualFieldSurface));
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _freeSlotIds = new Queue<int>(Enumerable.Range(0, Options.MaxActiveEffects));
    }

    public TimberbornPooledFireEffectOptions Options { get; }

    public TimberbornPooledFireEffectCounters Counters => new(
        ActivePooledEffectCount: _slotsByCellIndex.Count,
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
        _slotsByCellIndex.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.State);

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
        if (_updatedVisualRegionsThisDispatch > 0)
        {
            _lastNonZeroUpdatedVisualRegionCount = _updatedVisualRegionsThisDispatch;
            _lastNonZeroUpdatedVisualRegionTick = tick;
        }

        _logSink.Info(
            "wildfire_timberborn_pooled_fire_effects_updated " +
            $"tick={tick} " +
            $"active_pooled_effects={_slotsByCellIndex.Count} " +
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
        _slotsByCellIndex.Clear();
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

        if (!TryCreateEffectState(binding, effectEvent, sample, out TimberbornPooledFireEffectState state))
        {
            ReleaseCell(effectEvent.CellIndex);
            _updatedVisualRegionsThisDispatch++;
            return;
        }

        TimberbornPooledFireEffectSlot? slot = FindOrAllocateSlot(effectEvent.CellIndex, state.Intensity);
        if (slot is null)
        {
            _droppedVisualRegionsThisDispatch++;
            return;
        }

        TimberbornPooledFireEffectState slottedState = state with { SlotId = slot.SlotId };
        slot.State = slottedState;
        _slotsByCellIndex[effectEvent.CellIndex] = slot;
        _slotsById[slot.SlotId] = slot;
        TimberbornPooledFireEffectPresentationResult presentationResult = _presenter.UpdateEffect(slottedState);
        if (presentationResult.NativeEffectPrefabName is not null)
        {
            _lastNativeEffectPrefabName = presentationResult.NativeEffectPrefabName;
        }

        if (presentationResult.Status == TimberbornPooledFireEffectPresentationStatus.Disabled)
        {
            _disabledVisualRegionsThisDispatch++;
            ReleaseCell(effectEvent.CellIndex);
            return;
        }

        if (presentationResult.Status == TimberbornPooledFireEffectPresentationStatus.Failed)
        {
            _presentationFailuresThisDispatch++;
            ReleaseCell(effectEvent.CellIndex);
            _logSink.Warning(
                "wildfire_timberborn_pooled_fire_effects_failed " +
                $"stage=presenter tick={effectEvent.Tick} cell_index={effectEvent.CellIndex} " +
                $"message=\"{EscapeLogValue(presentationResult.Message ?? "presentation failed")}\"");
            return;
        }

        _updatedVisualRegionsThisDispatch++;
    }

    private TimberbornPooledFireEffectSlot? FindOrAllocateSlot(int cellIndex, float intensity)
    {
        if (_slotsByCellIndex.TryGetValue(cellIndex, out TimberbornPooledFireEffectSlot? existingSlot))
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

        _slotsByCellIndex.Remove(replacement.State.CellIndex);
        return replacement;
    }

    private void ReleaseCell(int cellIndex)
    {
        if (!_slotsByCellIndex.TryGetValue(cellIndex, out TimberbornPooledFireEffectSlot? slot))
        {
            return;
        }

        _slotsByCellIndex.Remove(cellIndex);
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
                $"stage=release cell_index={cellIndex} slot_id={slot.SlotId} " +
                $"message=\"{EscapeLogValue(exception.Message)}\"");
        }
    }

    private bool TryCreateEffectState(
        TimberbornGpuVisualFieldSurfaceBinding binding,
        TimberbornFireVisualEffectEvent effectEvent,
        TimberbornGpuVisualFieldSample sample,
        out TimberbornPooledFireEffectState state)
    {
        float fire = Clamp01(sample.Fire);
        float smoke = Clamp01(sample.Smoke);
        float ash = Clamp01(sample.Ash);
        float visibility = Clamp01(sample.Visibility);
        float intensity = Math.Max(fire, Math.Max(smoke, ash)) * visibility;
        if (intensity < Options.MinimumVisibleIntensity)
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
            Kind: SelectKind(sample.CellIndex, fire, smoke, ash),
            Fire: fire,
            Smoke: smoke,
            Ash: ash,
            Visibility: visibility,
            Intensity: intensity);
        return true;
    }

    private static TimberbornPooledFireEffectKind SelectKind(int cellIndex, float fire, float smoke, float ash)
    {
        if (smoke > 0.35f && fire > 0.2f && smoke >= fire * 0.6f && Hash01(cellIndex, 29) < 0.34f)
        {
            return TimberbornPooledFireEffectKind.Smoke;
        }

        if (fire >= smoke && fire >= ash)
        {
            return TimberbornPooledFireEffectKind.Fire;
        }

        return smoke >= ash
            ? TimberbornPooledFireEffectKind.Smoke
            : TimberbornPooledFireEffectKind.Ash;
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

        GameObject instance = GetOrCreateInstance(state, resolution.Prefab);
        instance.name = $"Wildfire {state.Kind} Effect {state.SlotId}";
        instance.SetActive(true);
        TimberbornPooledFireEffectLocalPosition position = ToUnityLocalPosition(state);
        instance.transform.localPosition = new Vector3(position.X, position.Y, position.Z);
        instance.transform.localScale = Vector3.one;
        _lastNativeEffectPrefabName = resolution.PrefabName;
        return TimberbornPooledFireEffectPresentationResult.Applied(resolution.PrefabName);
    }

    public void ReleaseEffect(int slotId)
    {
        if (_instancesBySlotId.TryGetValue(slotId, out TimberbornPooledFireEffectInstance? instance))
        {
            instance.GameObject.SetActive(false);
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
        float jitterRadius = state.Kind == TimberbornPooledFireEffectKind.Smoke ? 0.42f : 0.32f;
        float jitterX = (Hash01(state.CellIndex, 11) - 0.5f) * jitterRadius * 2f;
        float jitterZ = (Hash01(state.CellIndex, 23) - 0.5f) * jitterRadius * 2f;
        float verticalLift = state.Kind switch
        {
            TimberbornPooledFireEffectKind.Smoke => 0.45f,
            TimberbornPooledFireEffectKind.Ash => 0.2f,
            _ => 0.05f,
        };
        return new TimberbornPooledFireEffectLocalPosition(
            state.X + 0.5f + jitterX,
            state.Z + 0.5f + verticalLift,
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

    private GameObject GetOrCreateInstance(TimberbornPooledFireEffectState state, GameObject nativePrefab)
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
                return existingInstance.GameObject;
            }

            UnityEngine.Object.Destroy(existingInstance.GameObject);
            _instancesBySlotId.Remove(slotId);
        }

        GameObject created = UnityEngine.Object.Instantiate(nativePrefab);
        created.hideFlags = HideFlags.DontSave;
        created.SetActive(false);
        created.transform.SetParent(GetOrCreateRoot().transform, worldPositionStays: false);
        _instancesBySlotId[slotId] = new TimberbornPooledFireEffectInstance(
            created,
            state.Kind,
            nativePrefab.name);
        return created;
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

        resolution = kind == TimberbornPooledFireEffectKind.Fire
            ? TimberbornNativeFireEffectPrefabCatalog.Resolve(kind)
            : TimberbornProceduralSmokeEffectPrefabCatalog.Resolve(kind);
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

public static class TimberbornProceduralSmokeEffectPrefabCatalog
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
        main.startLifetime = kind == TimberbornPooledFireEffectKind.Smoke ? 2.4f : 1.8f;
        main.startSpeed = kind == TimberbornPooledFireEffectKind.Smoke ? 0.55f : 0.25f;
        main.startSize = kind == TimberbornPooledFireEffectKind.Smoke ? 0.95f : 0.55f;
        main.startColor = kind == TimberbornPooledFireEffectKind.Smoke
            ? new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.78f, 0.72f, 0.62f))
            : new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.42f, 0.39f, 0.46f));
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = kind == TimberbornPooledFireEffectKind.Smoke ? 16f : 8f;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = kind == TimberbornPooledFireEffectKind.Smoke ? 0.45f : 0.28f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new();
        Color start = kind == TimberbornPooledFireEffectKind.Smoke
            ? new Color(0.9f, 0.9f, 0.84f, 0.62f)
            : new Color(0.46f, 0.46f, 0.42f, 0.42f);
        Color end = kind == TimberbornPooledFireEffectKind.Smoke
            ? new Color(0.62f, 0.62f, 0.58f, 0f)
            : new Color(0.32f, 0.32f, 0.3f, 0f);
        gradient.SetKeys(
            new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
            new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.45f));

        ParticleSystemRenderer renderer = prefab.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        Shader? shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
        if (shader is not null)
        {
            renderer.sharedMaterial = new Material(shader)
            {
                hideFlags = HideFlags.DontSave,
            };
        }

        return prefab;
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
            TimberbornPooledFireEffectKind.Ash => new[] { "SmelterSmoke", "SteamEngineSmoke" },
            _ => Array.Empty<string>(),
        };
    }

    private static string[] BroadNames(TimberbornPooledFireEffectKind kind)
    {
        return kind switch
        {
            TimberbornPooledFireEffectKind.Fire => new[] { "fire", "spark" },
            TimberbornPooledFireEffectKind.Smoke => new[] { "smoke" },
            TimberbornPooledFireEffectKind.Ash => new[] { "ash", "smoke" },
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
