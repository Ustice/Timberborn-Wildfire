using Wildfire.Core;

namespace Wildfire.Timberborn.Ash;

public sealed class TimberbornAshFieldService
{
    public const int MaxStrength = 3;
    public const int FertileAshDecayDays = 15;
    public const int TaintedAshDecayDays = 30;
    public const string FertileAshGoodId = "FertileAsh";

    private readonly Dictionary<int, TimberbornAshFieldEntry> _entries = new();
    private readonly Dictionary<int, int> _lastDecayDayByCell = new();
    private readonly ITimberbornFireLogSink _logSink;
    private int _persistenceSaveCount;
    private int _persistenceLoadCount;

    public TimberbornAshFieldService(
        ITimberbornFireLogSink? logSink = null)
    {
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
        LastSummary = TimberbornAshFieldSummary.Empty;
    }

    public IReadOnlyDictionary<int, TimberbornAshFieldEntry> Entries => _entries;

    public TimberbornAshFieldSummary LastSummary { get; private set; }

    public bool TryGetEntry(int cellIndex, out TimberbornAshFieldEntry entry)
    {
        return _entries.TryGetValue(cellIndex, out entry);
    }

    public TimberbornAshGrowthBonusRequest[] CaptureGrowthRequests() => _entries.Values
        .Where(static entry => entry.GrantsGrowth)
        .Select(static entry => new TimberbornAshGrowthBonusRequest(
            entry.CellIndex, entry.GrowthMultiplier(), entry.Quality, entry.Strength))
        .ToArray();

    public void Clear()
    {
        _entries.Clear();
        _lastDecayDayByCell.Clear();
        LastSummary = TimberbornAshFieldSummary.Empty;
    }

    public TimberbornAshFieldSummary Advance(uint tick, int dayNumber = 0)
    {
        return ApplyDayDecay(tick, dayNumber);
    }

    public TimberbornAshFieldSummary ApplyDayDecay(
        uint tick,
        int dayNumber,
        Action<TimberbornAshFieldCollectionRemoval>? onDecayedCell = null)
    {
        TimberbornAshFieldCollectionRemoval[] removals = CalculateDayDecayRemovals(dayNumber)
            .ToArray();
        if (removals.Length == 0 && LastSummary.Tick == tick)
        {
            return LastSummary;
        }

        removals
            .ToList()
            .ForEach(removal =>
            {
                if (_entries.TryGetValue(removal.CellIndex, out TimberbornAshFieldEntry entry))
                {
                    _lastDecayDayByCell[removal.CellIndex] = NextDecayDay(entry, dayNumber, removal.StrengthRemoved);
                }

                onDecayedCell?.Invoke(removal);
            });
        return UpdateSummary(
            tick,
            sourceEventCount: 0,
            newAshCells: 0,
            decayedAshCells: removals.Length);
    }

    public TimberbornAshFieldCollectionRemoval CalculateCollectedFertileStrengthRemoval(
        int cellIndex,
        int strengthToRemove)
    {
        if (strengthToRemove <= 0 ||
            !_entries.TryGetValue(cellIndex, out TimberbornAshFieldEntry entry) ||
            entry.Quality != WildfireAshQuality.Fertile ||
            entry.IsActiveSource)
        {
            return new TimberbornAshFieldCollectionRemoval(cellIndex, 0, RemovedEntry: false);
        }

        int removedStrength = Math.Min(entry.Strength, strengthToRemove);
        return new TimberbornAshFieldCollectionRemoval(
            cellIndex,
            removedStrength,
            RemovedEntry: removedStrength >= entry.Strength);
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

    public TimberbornAshFieldSummary RestoreSnapshot(
        uint tick,
        TimberbornAshFieldSnapshot snapshot,
        int dayNumber = 0)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        _entries.Clear();
        snapshot.Entries
            .Where(static entry => entry.PersistenceVersion > 0 &&
                entry.PersistenceVersion <= TimberbornAshFieldEntry.CurrentPersistenceVersion)
            .Where(static entry => entry.CellIndex >= 0 && entry.Quality != WildfireAshQuality.None && entry.Strength > 0)
            .ToList()
            .ForEach(entry => _entries[entry.CellIndex] = entry with
            {
                Strength = Math.Clamp(entry.Strength, 1, MaxStrength),
                UpdatedTick = tick,
                UpdatedDayNumber = entry.UpdatedDayNumber == 0 ? dayNumber : entry.UpdatedDayNumber,
            });
        _lastDecayDayByCell.Clear();
        _entries.Values
            .ToList()
            .ForEach(entry => _lastDecayDayByCell[entry.CellIndex] = entry.UpdatedDayNumber);
        _persistenceLoadCount++;
        return UpdateSummary(tick, sourceEventCount: 0, newAshCells: 0, decayedAshCells: 0);
    }

    public TimberbornAshFieldSummary SyncFromTransportFields(
        uint tick,
        IReadOnlyList<uint> transportFields,
        int dayNumber = 0)
    {
        if (transportFields is null)
        {
            throw new ArgumentNullException(nameof(transportFields));
        }

        Dictionary<int, TimberbornAshFieldEntry> existingEntries = _entries.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value);
        _entries.Clear();
        int newAshCells = 0;
        transportFields
            .Select(static (packed, index) => (State: WildfireTransportFieldState.Unpack(packed), CellIndex: index))
            .Where(static item => item.State.Ash > 0)
            .ToList()
            .ForEach(item =>
            {
                WildfireAshQuality quality = item.State.AshContamination > 0
                    ? WildfireAshQuality.Tainted
                    : WildfireAshQuality.Fertile;
                bool hadExisting = existingEntries.TryGetValue(item.CellIndex, out TimberbornAshFieldEntry existing);
                if (!hadExisting)
                {
                    newAshCells++;
                }

                int updatedDayNumber = _lastDecayDayByCell.TryGetValue(item.CellIndex, out int lastDecayDay)
                    ? lastDecayDay
                    : hadExisting
                        ? existing.UpdatedDayNumber
                        : dayNumber;
                _entries[item.CellIndex] = new TimberbornAshFieldEntry(
                    item.CellIndex,
                    quality,
                    item.State.Ash * TimberbornFertileAshCollectionService.StrengthPerGood,
                    TimberbornAshSourceKind.Unknown,
                    CreatedTick: hadExisting ? existing.CreatedTick : tick,
                    UpdatedTick: tick,
                    TimberbornAshFieldEntry.CurrentPersistenceVersion,
                    CreatedDayNumber: hadExisting ? existing.CreatedDayNumber : dayNumber,
                    UpdatedDayNumber: updatedDayNumber,
                    IsActiveSource: item.State.Source);
            });
        _lastDecayDayByCell.Keys
            .Where(cellIndex => !_entries.ContainsKey(cellIndex))
            .ToArray()
            .ToList()
            .ForEach(cellIndex => _lastDecayDayByCell.Remove(cellIndex));

        return UpdateSummary(tick, sourceEventCount: 0, newAshCells, decayedAshCells: 0);
    }

    public TimberbornAshFieldSummary SyncFromAtmosphericFields(
        uint tick,
        IReadOnlyList<uint> atmosphericFields,
        int dayNumber = 0)
    {
        return SyncFromTransportFields(tick, atmosphericFields, dayNumber);
    }

    private TimberbornAshFieldSummary UpdateSummary(
        uint tick,
        int sourceEventCount,
        int newAshCells,
        int decayedAshCells)
    {


        LastSummary = new TimberbornAshFieldSummary(
            Tick: tick,
            SourceEventCount: sourceEventCount,
            ContaminatedBurnSourceCellCount: 0,
            ContaminatedAffectedCellCount: 0,
            NewAshCellCount: newAshCells,
            FertileAshCellCount: _entries.Values.Count(static entry => entry.Quality == WildfireAshQuality.Fertile),
            SpentAshCellCount: _entries.Values.Count(static entry => entry.Quality == WildfireAshQuality.Spent),
            TaintedAshCellCount: _entries.Values.Count(static entry => entry.Quality == WildfireAshQuality.Tainted),
            DecayedAshCellCount: decayedAshCells,
            GrowthCandidateCellCount: _entries.Values.Count(static entry => entry.GrantsGrowth),
            GrowthAppliedGrowableCount: 0,
            GrowthSkippedTaintedCellCount: _entries.Values.Count(static entry => entry.Quality == WildfireAshQuality.Tainted),
            GrowthFailedConsequenceCount: 0,
            GrowthUnsupportedGrowableCount: 0,
            PersistenceSaveCount: _persistenceSaveCount,
            PersistenceLoadCount: _persistenceLoadCount);

        if (LastSummary.SourceEventCount > 0 ||
            LastSummary.FertileAshCellCount > 0 ||
            LastSummary.SpentAshCellCount > 0 ||
            LastSummary.TaintedAshCellCount > 0 ||
            LastSummary.DecayedAshCellCount > 0 ||
            LastSummary.GrowthFailedConsequenceCount > 0 ||
            LastSummary.GrowthAppliedGrowableCount > 0)
        {
            _logSink.Info(LastSummary.ToLogToken());
        }

        return LastSummary;
    }

    private IEnumerable<TimberbornAshFieldCollectionRemoval> CalculateDayDecayRemovals(int dayNumber)
    {
        return _entries.Values
            .Select(entry => DecayEntryByDay(entry, dayNumber))
            .Where(static removal => removal.HasValue)
            .Select(static removal => removal!.Value)
            .ToList();
    }

    private static TimberbornAshFieldCollectionRemoval? DecayEntryByDay(
        TimberbornAshFieldEntry entry,
        int dayNumber)
    {
        if (entry.Quality == WildfireAshQuality.None || entry.Strength <= 0)
        {
            return null;
        }

        int intervalDays = entry.Quality == WildfireAshQuality.Tainted
            ? TaintedAshDecayDays
            : FertileAshDecayDays;
        int elapsedDays = Math.Max(0, dayNumber - entry.UpdatedDayNumber);
        int strengthToRemove = Math.Min(entry.Strength, elapsedDays / intervalDays);
        return strengthToRemove <= 0
            ? null
            : new TimberbornAshFieldCollectionRemoval(
                entry.CellIndex,
                strengthToRemove,
                RemovedEntry: strengthToRemove >= entry.Strength);
    }

    private static int NextDecayDay(TimberbornAshFieldEntry entry, int dayNumber, int strengthRemoved)
    {
        int intervalDays = entry.Quality == WildfireAshQuality.Tainted
            ? TaintedAshDecayDays
            : FertileAshDecayDays;
        return Math.Min(dayNumber, entry.UpdatedDayNumber + (strengthRemoved * intervalDays));
    }

    internal static WildfireAshQuality ClassifyQuality(TimberbornAshSourceEvent sourceEvent)
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
    private readonly TimberbornResourceFuelCatalog _resourceFuelCatalog;
    private readonly Func<int, bool> _affectedCellContaminationProvider;

    public TimberbornAshFieldSink(
        ITimberbornBurnDamageTargetStateProvider targetStateProvider,
        TimberbornAshFieldService ashFieldService,
        TimberbornResourceFuelCatalog? resourceFuelCatalog = null,
        Func<int, bool>? affectedCellContaminationProvider = null)
    {
        _targetStateProvider = targetStateProvider ?? throw new ArgumentNullException(nameof(targetStateProvider));
        _ = ashFieldService ?? throw new ArgumentNullException(nameof(ashFieldService));
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

        return SummarizeSourceEvents(tick, sourceEvents);
    }

    private static TimberbornAshFieldSummary SummarizeSourceEvents(
        uint tick,
        IReadOnlyList<TimberbornAshSourceEvent> sourceEvents)
    {
        TimberbornAshSourceEvent[] ashEvents = sourceEvents
            .Where(static sourceEvent => sourceEvent.CellIndex >= 0 && sourceEvent.Strength > 0)
            .ToArray();
        int fertileCells = ashEvents
            .Count(static sourceEvent => TimberbornAshFieldService.ClassifyQuality(sourceEvent) == WildfireAshQuality.Fertile);
        int spentCells = ashEvents
            .Count(static sourceEvent => TimberbornAshFieldService.ClassifyQuality(sourceEvent) == WildfireAshQuality.Spent);
        int taintedCells = ashEvents
            .Count(static sourceEvent => TimberbornAshFieldService.ClassifyQuality(sourceEvent) == WildfireAshQuality.Tainted);

        return TimberbornAshFieldSummary.Empty with
        {
            Tick = tick,
            SourceEventCount = ashEvents.Length,
            ContaminatedBurnSourceCellCount = ashEvents.Count(static sourceEvent => sourceEvent.IsSourceContaminated),
            ContaminatedAffectedCellCount = ashEvents.Count(static sourceEvent => sourceEvent.IsAffectedCellContaminated),
            FertileAshCellCount = fertileCells,
            SpentAshCellCount = spentCells,
            TaintedAshCellCount = taintedCells,
            GrowthCandidateCellCount = fertileCells,
            GrowthSkippedTaintedCellCount = taintedCells,
        };
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
