using System.Runtime.CompilerServices;
using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.BlueprintSystem;
using Timberborn.BlockSystem;
using Timberborn.Carrying;
using Timberborn.EntitySystem;
using Timberborn.Gathering;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Navigation;
using Timberborn.SimpleOutputBuildings;
using Timberborn.TemplateSystem;
using Timberborn.WalkingSystem;
using Timberborn.WorkSystem;
using Timberborn.WorldPersistence;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Ash;

public static class TimberbornBoundedCellRange
{
    public static bool IsWithinRange(
        int sourceX,
        int sourceY,
        int sourceZ,
        int targetX,
        int targetY,
        int targetZ,
        FireGrid grid,
        int rangeCells)
    {
        if ((uint)sourceX >= (uint)grid.Width ||
            (uint)sourceY >= (uint)grid.Height ||
            (uint)sourceZ >= (uint)grid.Depth ||
            (uint)targetX >= (uint)grid.Width ||
            (uint)targetY >= (uint)grid.Height ||
            (uint)targetZ >= (uint)grid.Depth)
        {
            return false;
        }

        int range = Math.Max(0, rangeCells);
        int distanceSquared = DistanceSquared(sourceX, sourceY, sourceZ, targetX, targetY, targetZ);
        return distanceSquared <= range * range;
    }

    public static int DistanceSquared(
        int sourceX,
        int sourceY,
        int sourceZ,
        int targetX,
        int targetY,
        int targetZ)
    {
        int dx = sourceX - targetX;
        int dy = sourceY - targetY;
        int dz = sourceZ - targetZ;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }
}

public readonly record struct TimberbornTaintedAshSoilPoisoningCandidate(
    int CellIndex,
    int Strength);

public enum TimberbornTaintedAshSoilPoisoningOutcome
{
    NoCandidates,
    Applied,
    Unavailable,
}

public readonly record struct TimberbornTaintedAshSoilPoisoningSummary(
    int CandidateCellCount,
    int AppliedCellCount,
    TimberbornTaintedAshSoilPoisoningOutcome Outcome)
{
    public static readonly TimberbornTaintedAshSoilPoisoningSummary Empty = new(
        CandidateCellCount: 0,
        AppliedCellCount: 0,
        Outcome: TimberbornTaintedAshSoilPoisoningOutcome.NoCandidates);

    public string ToLogToken(uint tick)
    {
        return "wildfire_timberborn_tainted_ash_soil_poisoning " +
            $"tick={tick} " +
            $"outcome={Outcome.ToString().ToLowerInvariant()} " +
            $"candidate_cells={CandidateCellCount} " +
            $"applied_cells={AppliedCellCount}";
    }
}

public interface ITimberbornTaintedAshSoilPoisoningAdapter
{
    TimberbornTaintedAshSoilPoisoningSummary ApplyPoisoning(
        uint tick,
        IReadOnlyList<TimberbornTaintedAshSoilPoisoningCandidate> candidates);
}

public sealed class UnavailableTimberbornTaintedAshSoilPoisoningAdapter : ITimberbornTaintedAshSoilPoisoningAdapter
{
    public static readonly UnavailableTimberbornTaintedAshSoilPoisoningAdapter Instance = new();

    private UnavailableTimberbornTaintedAshSoilPoisoningAdapter()
    {
    }

    public TimberbornTaintedAshSoilPoisoningSummary ApplyPoisoning(
        uint tick,
        IReadOnlyList<TimberbornTaintedAshSoilPoisoningCandidate> candidates)
    {
        if (candidates is null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        return candidates.Count == 0
            ? TimberbornTaintedAshSoilPoisoningSummary.Empty
            : new TimberbornTaintedAshSoilPoisoningSummary(
                candidates.Count, 0, TimberbornTaintedAshSoilPoisoningOutcome.Unavailable);
    }
}

public sealed class TimberbornTaintedAshSoilPoisoningService
{
    private readonly ITimberbornTaintedAshSoilPoisoningAdapter _adapter;
    private readonly ITimberbornFireLogSink _logSink;
    private bool _reportedUnavailable;

    public TimberbornTaintedAshSoilPoisoningService(
        ITimberbornTaintedAshSoilPoisoningAdapter? adapter = null,
        ITimberbornFireLogSink? logSink = null)
    {
        _adapter = adapter ?? UnavailableTimberbornTaintedAshSoilPoisoningAdapter.Instance;
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
    }

    public TimberbornTaintedAshSoilPoisoningSummary LastSummary { get; private set; } =
        TimberbornTaintedAshSoilPoisoningSummary.Empty;

    public TimberbornTaintedAshSoilPoisoningSummary Apply(
        uint tick,
        IReadOnlyDictionary<int, TimberbornAshFieldEntry> entries)
    {
        if (entries is null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        TimberbornTaintedAshSoilPoisoningCandidate[] candidates = entries.Values
            .Where(static entry => entry.Quality == WildfireAshQuality.Tainted && entry.Strength > 0)
            .Select(static entry => new TimberbornTaintedAshSoilPoisoningCandidate(entry.CellIndex, entry.Strength))
            .ToArray();

        LastSummary = candidates.Length == 0
            ? TimberbornTaintedAshSoilPoisoningSummary.Empty
            : _adapter.ApplyPoisoning(tick, candidates);
        if (LastSummary.Outcome == TimberbornTaintedAshSoilPoisoningOutcome.Unavailable)
        {
            if (!_reportedUnavailable)
            {
                _reportedUnavailable = true;
                _logSink.Info(LastSummary.ToLogToken(tick));
            }
        }
        else if (LastSummary.Outcome == TimberbornTaintedAshSoilPoisoningOutcome.Applied)
        {
            _logSink.Info(LastSummary.ToLogToken(tick));
        }

        return LastSummary;
    }

    public void Clear()
    {
        LastSummary = TimberbornTaintedAshSoilPoisoningSummary.Empty;
    }
}

public enum TimberbornAshWaterContactKind
{
    CleanWater,
    BadwaterOrContaminatedWater,
}

public readonly record struct TimberbornAshWaterContact(
    int CellIndex,
    int WaterLevel,
    TimberbornAshWaterContactKind Kind);

public static class TimberbornAshWaterContactClassifier
{
    public static IReadOnlyDictionary<int, TimberbornAshWaterContact> FromFireSimState(
        TimberbornFireSimPersistenceSnapshot? snapshot,
        IEnumerable<TimberbornImportedFieldTarget> importedTargets)
    {
        if (snapshot?.Cells is not { Count: > 0 } cells)
        {
            return new Dictionary<int, TimberbornAshWaterContact>();
        }

        if (importedTargets is null)
        {
            throw new ArgumentNullException(nameof(importedTargets));
        }

        HashSet<int> contaminatedWaterCells = importedTargets
            .Where(static target => target.MaterialClass == WildfireMaterialClass.Badwater)
            .Select(static target => target.CellIndex)
            .ToHashSet();
        return cells
            .Select(static (cell, index) => (CellIndex: index, Water: PackedCell.Water(cell)))
            .Where(static item => item.Water > 0)
            .ToDictionary(
                static item => item.CellIndex,
                item => new TimberbornAshWaterContact(
                    item.CellIndex,
                    item.Water,
                    contaminatedWaterCells.Contains(item.CellIndex)
                        ? TimberbornAshWaterContactKind.BadwaterOrContaminatedWater
                        : TimberbornAshWaterContactKind.CleanWater));
    }
}

public readonly record struct TimberbornAshWaterWashoutRemoval(
    int CellIndex,
    int StrengthRemoved,
    WildfireAshQuality Quality,
    TimberbornAshWaterContactKind WaterKind);

public readonly record struct TimberbornAshWaterWashoutSummary(
    int CandidateAshCellCount,
    int CleanAshWashedCellCount,
    int TaintedAshWashedCellCount,
    int WaterTaintAttemptCount,
    int WaterTaintSuccessCount,
    int NoOpCellCount)
{
    public static readonly TimberbornAshWaterWashoutSummary Empty = new(
        CandidateAshCellCount: 0,
        CleanAshWashedCellCount: 0,
        TaintedAshWashedCellCount: 0,
        WaterTaintAttemptCount: 0,
        WaterTaintSuccessCount: 0,
        NoOpCellCount: 0);

    public string ToLogToken(uint tick)
    {
        return "wildfire_timberborn_ash_water_washout_applied " +
            $"tick={tick} " +
            $"candidate_ash_cells={CandidateAshCellCount} " +
            $"clean_ash_washed={CleanAshWashedCellCount} " +
            $"tainted_ash_washed={TaintedAshWashedCellCount} " +
            $"water_taint_attempts={WaterTaintAttemptCount} " +
            $"water_taint_successes={WaterTaintSuccessCount} " +
            $"no_op_cells={NoOpCellCount}";
    }
}

public interface ITimberbornAshWaterTaintAdapter
{
    TimberbornAshWaterWashoutSummary ApplyWaterTaint(
        uint tick,
        IReadOnlyList<TimberbornAshWaterWashoutRemoval> taintedWashouts,
        TimberbornAshWaterWashoutSummary washoutSummary);
}

public sealed class UnavailableTimberbornAshWaterTaintAdapter : ITimberbornAshWaterTaintAdapter
{
    public static readonly UnavailableTimberbornAshWaterTaintAdapter Instance = new();

    private UnavailableTimberbornAshWaterTaintAdapter()
    {
    }

    public TimberbornAshWaterWashoutSummary ApplyWaterTaint(
        uint tick,
        IReadOnlyList<TimberbornAshWaterWashoutRemoval> taintedWashouts,
        TimberbornAshWaterWashoutSummary washoutSummary)
    {
        if (taintedWashouts is null)
        {
            throw new ArgumentNullException(nameof(taintedWashouts));
        }

        throw new InvalidOperationException("Ash water taint adapter is unavailable.");
    }
}

public sealed class TimberbornAshWaterWashoutService
{
    private readonly ITimberbornAshWaterTaintAdapter _waterTaintAdapter;
    private readonly ITimberbornFireLogSink _logSink;

    public TimberbornAshWaterWashoutService(
        ITimberbornAshWaterTaintAdapter? waterTaintAdapter = null,
        ITimberbornFireLogSink? logSink = null)
    {
        _waterTaintAdapter = waterTaintAdapter ?? UnavailableTimberbornAshWaterTaintAdapter.Instance;
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
    }

    public TimberbornAshWaterWashoutSummary LastSummary { get; private set; } =
        TimberbornAshWaterWashoutSummary.Empty;

    public TimberbornAshWaterWashoutSummary Apply(
        uint tick,
        IReadOnlyDictionary<int, TimberbornAshFieldEntry> entries,
        IReadOnlyDictionary<int, TimberbornAshWaterContact> waterContacts,
        Action<TimberbornAshWaterWashoutRemoval>? onWashedCell = null)
    {
        if (entries is null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        if (waterContacts is null)
        {
            throw new ArgumentNullException(nameof(waterContacts));
        }

        TimberbornAshWaterWashoutRemoval[] removals = entries.Values
            .Where(static entry => entry.Strength > 0)
            .Select(entry => CreateRemoval(entry, waterContacts))
            .Where(static removal => removal.HasValue)
            .Select(static removal => removal!.Value)
            .ToArray();

        removals
            .ToList()
            .ForEach(removal => onWashedCell?.Invoke(removal));

        int noOpCells = entries.Values
            .Count(entry => entry.Strength > 0 &&
                (entry.IsActiveSource || !waterContacts.ContainsKey(entry.CellIndex)));
        TimberbornAshWaterWashoutSummary washoutSummary = new(
            CandidateAshCellCount: entries.Values.Count(static entry => entry.Strength > 0),
            CleanAshWashedCellCount: removals.Count(static removal => removal.Quality != WildfireAshQuality.Tainted),
            TaintedAshWashedCellCount: removals.Count(static removal => removal.Quality == WildfireAshQuality.Tainted),
            WaterTaintAttemptCount: removals.Count(static removal => removal.Quality == WildfireAshQuality.Tainted),
            WaterTaintSuccessCount: 0,
            NoOpCellCount: noOpCells);
        TimberbornAshWaterWashoutRemoval[] taintedWashouts = removals
            .Where(static removal => removal.Quality == WildfireAshQuality.Tainted)
            .ToArray();

        LastSummary = taintedWashouts.Length == 0
            ? washoutSummary
            : _waterTaintAdapter.ApplyWaterTaint(tick, taintedWashouts, washoutSummary);
        if (LastSummary.CleanAshWashedCellCount > 0 ||
            LastSummary.TaintedAshWashedCellCount > 0 ||
            LastSummary.WaterTaintAttemptCount > 0)
        {
            _logSink.Info(LastSummary.ToLogToken(tick));
        }

        return LastSummary;
    }

    public void Clear()
    {
        LastSummary = TimberbornAshWaterWashoutSummary.Empty;
    }

    private static TimberbornAshWaterWashoutRemoval? CreateRemoval(
        TimberbornAshFieldEntry entry,
        IReadOnlyDictionary<int, TimberbornAshWaterContact> waterContacts)
    {
        if (entry.IsActiveSource ||
            !waterContacts.TryGetValue(entry.CellIndex, out TimberbornAshWaterContact contact) ||
            contact.WaterLevel <= 0 ||
            entry.Quality == WildfireAshQuality.None)
        {
            return null;
        }

        return new TimberbornAshWaterWashoutRemoval(
            entry.CellIndex,
            entry.Strength,
            entry.Quality,
            contact.Kind);
    }
}

public readonly record struct TimberbornFertileAshCollectionCandidate(
    int CellIndex,
    int Strength);

public readonly record struct TimberbornFertileAshCollectedCell(
    int CellIndex,
    int StrengthToRemove,
    int GoodAmount);
