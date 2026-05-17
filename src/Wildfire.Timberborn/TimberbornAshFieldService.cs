using Wildfire.Core;

namespace Wildfire.Timberborn;

public enum TimberbornAshSourceKind
{
    Unknown,
    Crop,
    Tree,
    Resource,
    Structure,
    Storage,
    Infrastructure,
}

public readonly record struct TimberbornAshSourceEvent(
    int CellIndex,
    uint Tick,
    TimberbornAshSourceKind SourceKind,
    TimberbornBurnMaterialKind MaterialKind,
    int Strength,
    bool IsSourceContaminated,
    bool IsAffectedCellContaminated,
    IReadOnlyList<string> AccountedResourceIds);

public readonly record struct TimberbornAshFieldEntry(
    int CellIndex,
    WildfireAshQuality Quality,
    int Strength,
    TimberbornAshSourceKind SourceKind,
    uint CreatedTick,
    uint UpdatedTick,
    int PersistenceVersion)
{
    public const int CurrentPersistenceVersion = 1;

    public bool GrantsGrowth => Quality == WildfireAshQuality.Fertile && Strength > 0;

    public float GrowthMultiplier()
    {
        return GrantsGrowth
            ? 1f + (0.10f * Math.Clamp(Strength, 0, TimberbornAshFieldService.MaxStrength) /
                TimberbornAshFieldService.MaxStrength)
            : 1f;
    }
}

public readonly record struct TimberbornAshGrowthBonusRequest(
    int CellIndex,
    float GrowthMultiplier,
    WildfireAshQuality Quality,
    int Strength);

public readonly record struct TimberbornAshGrowthApplicationResult(
    int CandidateGrowableCount,
    int AppliedGrowableCount,
    int SkippedUnsafeApiCount,
    int SkippedUnsupportedGrowableCount);

public interface ITimberbornAshGrowthAdapter
{
    TimberbornAshGrowthApplicationResult ApplyGrowthBonuses(
        uint tick,
        IReadOnlyList<TimberbornAshGrowthBonusRequest> requests);
}

public sealed class UnavailableTimberbornAshGrowthAdapter : ITimberbornAshGrowthAdapter
{
    public static readonly UnavailableTimberbornAshGrowthAdapter Instance = new();

    private UnavailableTimberbornAshGrowthAdapter()
    {
    }

    public TimberbornAshGrowthApplicationResult ApplyGrowthBonuses(
        uint tick,
        IReadOnlyList<TimberbornAshGrowthBonusRequest> requests)
    {
        if (requests is null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        return new TimberbornAshGrowthApplicationResult(
            CandidateGrowableCount: requests.Count,
            AppliedGrowableCount: 0,
            SkippedUnsafeApiCount: requests.Count,
            SkippedUnsupportedGrowableCount: 0);
    }
}

public sealed record TimberbornAshFieldSnapshot(
    int PersistenceVersion,
    IReadOnlyList<TimberbornAshFieldEntry> Entries);

public readonly record struct TimberbornAshFieldCollectionRemoval(
    int CellIndex,
    int StrengthRemoved,
    bool RemovedEntry);

public readonly record struct TimberbornAshFieldSummary(
    uint Tick,
    int SourceEventCount,
    int NewAshCellCount,
    int FertileAshCellCount,
    int SpentAshCellCount,
    int TaintedAshCellCount,
    int DecayedAshCellCount,
    int GrowthCandidateCellCount,
    int GrowthAppliedGrowableCount,
    int GrowthSkippedTaintedCellCount,
    int GrowthSkippedUnsafeApiCount,
    int GrowthSkippedUnsupportedGrowableCount,
    int PersistenceSaveCount,
    int PersistenceLoadCount)
{
    public static readonly TimberbornAshFieldSummary Empty = new(
        Tick: 0,
        SourceEventCount: 0,
        NewAshCellCount: 0,
        FertileAshCellCount: 0,
        SpentAshCellCount: 0,
        TaintedAshCellCount: 0,
        DecayedAshCellCount: 0,
        GrowthCandidateCellCount: 0,
        GrowthAppliedGrowableCount: 0,
        GrowthSkippedTaintedCellCount: 0,
        GrowthSkippedUnsafeApiCount: 0,
        GrowthSkippedUnsupportedGrowableCount: 0,
        PersistenceSaveCount: 0,
        PersistenceLoadCount: 0);

    public string ToLogToken()
    {
        return "wildfire_timberborn_ash_field_updated " +
            $"tick={Tick} " +
            $"ash_source_events={SourceEventCount} " +
            $"new_ash_cells={NewAshCellCount} " +
            $"fertile_ash_cells={FertileAshCellCount} " +
            $"spent_ash_cells={SpentAshCellCount} " +
            $"tainted_ash_cells={TaintedAshCellCount} " +
            $"decayed_ash_cells={DecayedAshCellCount} " +
            $"ash_growth_candidate_cells={GrowthCandidateCellCount} " +
            $"ash_growth_applied_growables={GrowthAppliedGrowableCount} " +
            $"ash_growth_skipped_tainted_cells={GrowthSkippedTaintedCellCount} " +
            $"ash_growth_skipped_unsafe_apis={GrowthSkippedUnsafeApiCount} " +
            $"ash_growth_skipped_unsupported_growables={GrowthSkippedUnsupportedGrowableCount} " +
            $"ash_persistence_saves={PersistenceSaveCount} " +
            $"ash_persistence_loads={PersistenceLoadCount}";
    }
}

public sealed class TimberbornAshFieldService
{
    public const int MaxStrength = 100;
    public const int DefaultDecayPerTick = 1;
    public const string FertileAshGoodId = "FertileAsh";

    private readonly Dictionary<int, TimberbornAshFieldEntry> _entries = new();
    private readonly ITimberbornAshGrowthAdapter _growthAdapter;
    private readonly ITimberbornFireLogSink _logSink;
    private int _persistenceSaveCount;
    private int _persistenceLoadCount;

    public TimberbornAshFieldService(
        ITimberbornAshGrowthAdapter? growthAdapter = null,
        ITimberbornFireLogSink? logSink = null)
    {
        _growthAdapter = growthAdapter ?? UnavailableTimberbornAshGrowthAdapter.Instance;
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
        LastSummary = TimberbornAshFieldSummary.Empty;
    }

    public IReadOnlyDictionary<int, TimberbornAshFieldEntry> Entries => _entries;

    public TimberbornAshFieldSummary LastSummary { get; private set; }

    public bool TryGetEntry(int cellIndex, out TimberbornAshFieldEntry entry)
    {
        return _entries.TryGetValue(cellIndex, out entry);
    }

    public void Clear()
    {
        _entries.Clear();
        LastSummary = TimberbornAshFieldSummary.Empty;
    }

    public TimberbornAshFieldSummary ApplySources(
        uint tick,
        IReadOnlyList<TimberbornAshSourceEvent> sourceEvents)
    {
        if (sourceEvents is null)
        {
            throw new ArgumentNullException(nameof(sourceEvents));
        }

        int newAshCells = 0;
        foreach (TimberbornAshSourceEvent sourceEvent in sourceEvents)
        {
            if (sourceEvent.CellIndex < 0 || sourceEvent.Strength <= 0)
            {
                continue;
            }

            WildfireAshQuality quality = ClassifyQuality(sourceEvent);
            if (quality == WildfireAshQuality.None)
            {
                continue;
            }

            int strength = Math.Clamp(sourceEvent.Strength, 1, MaxStrength);
            if (_entries.TryGetValue(sourceEvent.CellIndex, out TimberbornAshFieldEntry existing))
            {
                _entries[sourceEvent.CellIndex] = Merge(existing, sourceEvent, quality, strength, tick);
            }
            else
            {
                _entries[sourceEvent.CellIndex] = new TimberbornAshFieldEntry(
                    sourceEvent.CellIndex,
                    quality,
                    strength,
                    sourceEvent.SourceKind,
                    CreatedTick: tick,
                    UpdatedTick: tick,
                    TimberbornAshFieldEntry.CurrentPersistenceVersion);
                newAshCells++;
            }
        }

        return UpdateAndApplyGrowth(tick, sourceEvents.Count, newAshCells);
    }

    public TimberbornAshFieldSummary Advance(uint tick)
    {
        return UpdateAndApplyGrowth(tick, sourceEventCount: 0, newAshCells: 0);
    }

    public TimberbornAshFieldCollectionRemoval RemoveCollectedFertileStrength(
        int cellIndex,
        int strengthToRemove)
    {
        if (strengthToRemove <= 0 ||
            !_entries.TryGetValue(cellIndex, out TimberbornAshFieldEntry entry) ||
            entry.Quality != WildfireAshQuality.Fertile)
        {
            return new TimberbornAshFieldCollectionRemoval(cellIndex, 0, RemovedEntry: false);
        }

        int removedStrength = Math.Min(entry.Strength, strengthToRemove);
        int remainingStrength = entry.Strength - removedStrength;
        if (remainingStrength <= 0)
        {
            _entries.Remove(cellIndex);
            return new TimberbornAshFieldCollectionRemoval(cellIndex, removedStrength, RemovedEntry: true);
        }

        _entries[cellIndex] = entry with { Strength = remainingStrength };
        return new TimberbornAshFieldCollectionRemoval(cellIndex, removedStrength, RemovedEntry: false);
    }

    public TimberbornAshFieldSnapshot SaveSnapshot()
    {
        _persistenceSaveCount++;
        return new TimberbornAshFieldSnapshot(
            TimberbornAshFieldEntry.CurrentPersistenceVersion,
            _entries.Values
                .OrderBy(static entry => entry.CellIndex)
                .ToArray());
    }

    public TimberbornAshFieldSummary RestoreSnapshot(uint tick, TimberbornAshFieldSnapshot snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        _entries.Clear();
        snapshot.Entries
            .Where(static entry => entry.PersistenceVersion == TimberbornAshFieldEntry.CurrentPersistenceVersion)
            .Where(static entry => entry.CellIndex >= 0 && entry.Quality != WildfireAshQuality.None && entry.Strength > 0)
            .ToList()
            .ForEach(entry => _entries[entry.CellIndex] = entry with
            {
                Strength = Math.Clamp(entry.Strength, 1, MaxStrength),
                UpdatedTick = tick,
            });
        _persistenceLoadCount++;
        return UpdateAndApplyGrowth(tick, sourceEventCount: 0, newAshCells: 0);
    }

    private TimberbornAshFieldSummary UpdateAndApplyGrowth(uint tick, int sourceEventCount, int newAshCells)
    {
        int decayedAshCells = DecayEntries(tick);
        TimberbornAshGrowthBonusRequest[] growthRequests = _entries.Values
            .Where(static entry => entry.GrantsGrowth)
            .Select(static entry => new TimberbornAshGrowthBonusRequest(
                entry.CellIndex,
                entry.GrowthMultiplier(),
                entry.Quality,
                entry.Strength))
            .ToArray();
        TimberbornAshGrowthApplicationResult growthResult =
            _growthAdapter.ApplyGrowthBonuses(tick, growthRequests);

        LastSummary = new TimberbornAshFieldSummary(
            Tick: tick,
            SourceEventCount: sourceEventCount,
            NewAshCellCount: newAshCells,
            FertileAshCellCount: _entries.Values.Count(static entry => entry.Quality == WildfireAshQuality.Fertile),
            SpentAshCellCount: _entries.Values.Count(static entry => entry.Quality == WildfireAshQuality.Spent),
            TaintedAshCellCount: _entries.Values.Count(static entry => entry.Quality == WildfireAshQuality.Tainted),
            DecayedAshCellCount: decayedAshCells,
            GrowthCandidateCellCount: growthRequests.Length,
            GrowthAppliedGrowableCount: growthResult.AppliedGrowableCount,
            GrowthSkippedTaintedCellCount: _entries.Values.Count(static entry => entry.Quality == WildfireAshQuality.Tainted),
            GrowthSkippedUnsafeApiCount: growthResult.SkippedUnsafeApiCount,
            GrowthSkippedUnsupportedGrowableCount: growthResult.SkippedUnsupportedGrowableCount,
            PersistenceSaveCount: _persistenceSaveCount,
            PersistenceLoadCount: _persistenceLoadCount);

        if (LastSummary.SourceEventCount > 0 ||
            LastSummary.FertileAshCellCount > 0 ||
            LastSummary.SpentAshCellCount > 0 ||
            LastSummary.TaintedAshCellCount > 0 ||
            LastSummary.DecayedAshCellCount > 0 ||
            LastSummary.GrowthSkippedUnsafeApiCount > 0 ||
            LastSummary.GrowthAppliedGrowableCount > 0)
        {
            _logSink.Info(LastSummary.ToLogToken());
        }

        return LastSummary;
    }

    private int DecayEntries(uint tick)
    {
        int removed = 0;
        _entries.Values
            .Where(entry => tick > entry.UpdatedTick)
            .Select(entry => DecayEntry(entry, tick))
            .ToList()
            .ForEach(entry =>
            {
                if (entry.Strength <= 0)
                {
                    _entries.Remove(entry.CellIndex);
                    removed++;
                }
                else
                {
                    _entries[entry.CellIndex] = entry;
                }
            });

        return removed;
    }

    private static TimberbornAshFieldEntry DecayEntry(TimberbornAshFieldEntry entry, uint tick)
    {
        uint elapsed = tick - entry.UpdatedTick;
        int decayedStrength = entry.Strength - (int)Math.Min(int.MaxValue, elapsed * DefaultDecayPerTick);
        return entry with
        {
            Strength = decayedStrength,
            UpdatedTick = tick,
        };
    }

    private static TimberbornAshFieldEntry Merge(
        TimberbornAshFieldEntry existing,
        TimberbornAshSourceEvent sourceEvent,
        WildfireAshQuality quality,
        int strength,
        uint tick)
    {
        WildfireAshQuality mergedQuality = MergeQuality(existing.Quality, quality);
        return existing with
        {
            Quality = mergedQuality,
            Strength = Math.Clamp(Math.Max(existing.Strength, strength), 1, MaxStrength),
            SourceKind = sourceEvent.SourceKind,
            UpdatedTick = tick,
        };
    }

    private static WildfireAshQuality MergeQuality(WildfireAshQuality existing, WildfireAshQuality incoming)
    {
        if (existing == WildfireAshQuality.Tainted || incoming == WildfireAshQuality.Tainted)
        {
            return WildfireAshQuality.Tainted;
        }

        if (existing == WildfireAshQuality.Fertile || incoming == WildfireAshQuality.Fertile)
        {
            return WildfireAshQuality.Fertile;
        }

        return existing == WildfireAshQuality.Spent || incoming == WildfireAshQuality.Spent
            ? WildfireAshQuality.Spent
            : WildfireAshQuality.None;
    }

    private static WildfireAshQuality ClassifyQuality(TimberbornAshSourceEvent sourceEvent)
    {
        if (sourceEvent.IsSourceContaminated || sourceEvent.IsAffectedCellContaminated)
        {
            return WildfireAshQuality.Tainted;
        }

        if (sourceEvent.MaterialKind is TimberbornBurnMaterialKind.Organic or TimberbornBurnMaterialKind.Wood)
        {
            return WildfireAshQuality.Fertile;
        }

        if (sourceEvent.AccountedResourceIds.Any(IsCleanOrganicResource))
        {
            return WildfireAshQuality.Fertile;
        }

        return sourceEvent.MaterialKind is TimberbornBurnMaterialKind.Constructed or
            TimberbornBurnMaterialKind.StoredGood
                ? WildfireAshQuality.Spent
                : WildfireAshQuality.None;
    }

    private static bool IsCleanOrganicResource(string resourceId)
    {
        return resourceId is "Berries" or
            "Blueberry" or
            "Carrot" or
            "Cassava" or
            "CattailRoot" or
            "Chestnut" or
            "CoffeeBean" or
            "Corn" or
            "Dandelion" or
            "Eggplant" or
            "Kohlrabi" or
            "Log" or
            "MangroveFruit" or
            "MapleSyrup" or
            "Paper" or
            "PineResin" or
            "Plank" or
            "Potato" or
            "Soybean" or
            "Spadderdock" or
            "SunflowerSeeds" or
            "Wheat";
    }
}

public interface ITimberbornAshFieldSink
{
    TimberbornAshFieldSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions);
}

public sealed class NullTimberbornAshFieldSink : ITimberbornAshFieldSink
{
    public static readonly NullTimberbornAshFieldSink Instance = new();

    private NullTimberbornAshFieldSink()
    {
    }

    public TimberbornAshFieldSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        return TimberbornAshFieldSummary.Empty with { Tick = tick };
    }
}

public sealed class TimberbornAshFieldSink : ITimberbornAshFieldSink
{
    private readonly ITimberbornBurnDamageTargetStateProvider _targetStateProvider;
    private readonly TimberbornAshFieldService _ashFieldService;
    private readonly TimberbornResourceFuelCatalog _resourceFuelCatalog;
    private readonly Func<int, bool> _affectedCellContaminationProvider;

    public TimberbornAshFieldSink(
        ITimberbornBurnDamageTargetStateProvider targetStateProvider,
        TimberbornAshFieldService ashFieldService,
        TimberbornResourceFuelCatalog? resourceFuelCatalog = null,
        Func<int, bool>? affectedCellContaminationProvider = null)
    {
        _targetStateProvider = targetStateProvider ?? throw new ArgumentNullException(nameof(targetStateProvider));
        _ashFieldService = ashFieldService ?? throw new ArgumentNullException(nameof(ashFieldService));
        _resourceFuelCatalog = resourceFuelCatalog ?? TimberbornResourceFuelCatalog.Default;
        _affectedCellContaminationProvider = affectedCellContaminationProvider ?? (_ => false);
    }

    public TimberbornAshFieldSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        if (decisions is null)
        {
            throw new ArgumentNullException(nameof(decisions));
        }

        TimberbornAshSourceEvent[] sourceEvents = decisions
            .Where(static decision => decision.OldFuel > decision.NewFuel)
            .Select(decision => CreateSourceEvent(tick, decision))
            .Where(static sourceEvent => sourceEvent.HasValue)
            .Select(static sourceEvent => sourceEvent!.Value)
            .ToArray();

        return _ashFieldService.ApplySources(tick, sourceEvents);
    }

    private TimberbornAshSourceEvent? CreateSourceEvent(uint tick, TimberbornFireCellDeltaDecision decision)
    {
        if (!_targetStateProvider.TryGetStateForCell(decision.CellIndex, out TimberbornBurnDamageTargetState state))
        {
            return null;
        }

        int damageApplied = _targetStateProvider.TryGetAppliedEvent(state.TargetKey, out TimberbornBurnDamageAppliedEvent appliedEvent)
            ? appliedEvent.DamageApplied
            : Math.Max(1, decision.OldFuel - decision.NewFuel);
        int strength = state.DamageCapacity > 0
            ? (int)Math.Ceiling((double)damageApplied / state.DamageCapacity * TimberbornAshFieldService.MaxStrength)
            : Math.Max(1, Math.Min(TimberbornAshFieldService.MaxStrength, damageApplied * 10));

        return new TimberbornAshSourceEvent(
            decision.CellIndex,
            tick,
            ToAshSourceKind(state.TargetKind),
            state.MaterialKind,
            strength,
            IsSourceContaminated: IsSourceContaminated(state),
            IsAffectedCellContaminated: _affectedCellContaminationProvider(decision.CellIndex),
            state.AccountedResourceIds);
    }

    private bool IsSourceContaminated(TimberbornBurnDamageTargetState state)
    {
        if (state.AccountedResourceIds.Any(resourceId => _resourceFuelCatalog.Lookup(resourceId).Contaminated))
        {
            return true;
        }

        return state.SpecId.Contains("Badwater", StringComparison.OrdinalIgnoreCase) ||
            state.SpecId.Contains("Contaminated", StringComparison.OrdinalIgnoreCase);
    }

    private static TimberbornAshSourceKind ToAshSourceKind(TimberbornBurnDamageTargetKind targetKind)
    {
        return targetKind switch
        {
            TimberbornBurnDamageTargetKind.Crop => TimberbornAshSourceKind.Crop,
            TimberbornBurnDamageTargetKind.Tree => TimberbornAshSourceKind.Tree,
            TimberbornBurnDamageTargetKind.Resource => TimberbornAshSourceKind.Resource,
            TimberbornBurnDamageTargetKind.Structure => TimberbornAshSourceKind.Structure,
            TimberbornBurnDamageTargetKind.Storage => TimberbornAshSourceKind.Storage,
            TimberbornBurnDamageTargetKind.Infrastructure => TimberbornAshSourceKind.Infrastructure,
            _ => TimberbornAshSourceKind.Unknown,
        };
    }
}
