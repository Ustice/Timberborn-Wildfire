using System.Reflection;
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
using Timberborn.MapIndexSystem;
using Timberborn.Navigation;
using Timberborn.SimpleOutputBuildings;
using Timberborn.TemplateSystem;
using Timberborn.WalkingSystem;
using Timberborn.WorkSystem;
using Timberborn.WorldPersistence;
using Timberborn.SoilContaminationSystem;
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

public readonly record struct TimberbornTaintedAshSoilPoisoningSummary(
    int CandidateCellCount,
    int AppliedCellCount)
{
    public static readonly TimberbornTaintedAshSoilPoisoningSummary Empty = new(
        CandidateCellCount: 0,
        AppliedCellCount: 0);

    public string ToLogToken(uint tick)
    {
        return "wildfire_timberborn_tainted_ash_soil_poisoning_applied " +
            $"tick={tick} " +
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

public interface ITimberbornSoilContaminationPoisoningApi
{
    bool IsAvailable { get; }

    float Contamination(int mapCellIndex);

    void UpdateContamination(int x, int y, int z, float contamination);
}

internal sealed class TimberbornSoilContaminationPoisoningApi : ITimberbornSoilContaminationPoisoningApi
{
    private readonly ISoilContaminationService _soilContaminationService;
    private readonly MethodInfo? _updateMethod;

    public TimberbornSoilContaminationPoisoningApi(ISoilContaminationService soilContaminationService)
    {
        _soilContaminationService = soilContaminationService ??
            throw new ArgumentNullException(nameof(soilContaminationService));
        _updateMethod = _soilContaminationService
            .GetType()
            .GetMethod(
                "UpdateContamination",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                new[] { typeof(Vector3Int), typeof(float) },
                modifiers: null);
    }

    public bool IsAvailable => _updateMethod is not null;

    public float Contamination(int mapCellIndex)
    {
        return _soilContaminationService.Contamination(mapCellIndex);
    }

    public void UpdateContamination(int x, int y, int z, float contamination)
    {
        if (_updateMethod is null)
        {
            throw new InvalidOperationException("The soil contamination update API is unavailable.");
        }

        _updateMethod.Invoke(_soilContaminationService, new object[] { new Vector3Int(x, y, z), contamination });
    }
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

        throw new InvalidOperationException("Tainted ash soil poisoning adapter is unavailable.");
    }
}

public sealed class TimberbornTaintedAshSoilPoisoningService
{
    private readonly ITimberbornTaintedAshSoilPoisoningAdapter _adapter;
    private readonly ITimberbornFireLogSink _logSink;

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
        if (LastSummary.CandidateCellCount > 0 ||
            LastSummary.AppliedCellCount > 0)
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

public sealed class TimberbornSoilContaminationAshPoisoningAdapter : ITimberbornTaintedAshSoilPoisoningAdapter
{
    private const float MaxMapContamination = 0.9f;

    private readonly ITimberbornSoilContaminationPoisoningApi _poisoningApi;
    private readonly Func<FireGrid?> _gridProvider;
    private readonly Func<int, int, int> _cellToIndex;
    private readonly ITimberbornFireLogSink _logSink;

    internal TimberbornSoilContaminationAshPoisoningAdapter(
        ISoilContaminationService soilContaminationService,
        Func<FireGrid?> gridProvider,
        MapIndexService mapIndexService,
        ITimberbornFireLogSink? logSink = null)
        : this(
            new TimberbornSoilContaminationPoisoningApi(soilContaminationService),
            gridProvider,
            (x, y) => (mapIndexService ?? throw new ArgumentNullException(nameof(mapIndexService)))
                .CellToIndex(new Vector2Int(x, y)),
            logSink)
    {
    }

    public TimberbornSoilContaminationAshPoisoningAdapter(
        ITimberbornSoilContaminationPoisoningApi poisoningApi,
        Func<FireGrid?> gridProvider,
        Func<int, int, int> cellToIndex,
        ITimberbornFireLogSink? logSink = null)
    {
        _poisoningApi = poisoningApi ?? throw new ArgumentNullException(nameof(poisoningApi));
        _gridProvider = gridProvider ?? throw new ArgumentNullException(nameof(gridProvider));
        _cellToIndex = cellToIndex ?? throw new ArgumentNullException(nameof(cellToIndex));
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
    }

    public TimberbornTaintedAshSoilPoisoningSummary ApplyPoisoning(
        uint tick,
        IReadOnlyList<TimberbornTaintedAshSoilPoisoningCandidate> candidates)
    {
        if (candidates is null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        FireGrid? grid = _gridProvider();
        if (!grid.HasValue)
        {
            throw new InvalidOperationException("Tainted ash soil poisoning grid is unavailable.");
        }

        if (!_poisoningApi.IsAvailable)
        {
            throw new InvalidOperationException("Tainted ash soil poisoning API is unavailable.");
        }

        int applied = 0;
        candidates.ToList().ForEach(candidate =>
        {
            try
            {
                (int x, int y, int z) = grid.Value.FromIndex(candidate.CellIndex);
                int mapCellIndex = _cellToIndex(x, y);
                float current = _poisoningApi.Contamination(mapCellIndex);
                float target = Math.Max(current, MaxMapContamination);
                _poisoningApi.UpdateContamination(x, y, z, target);
                applied++;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Tainted ash soil poisoning failed for cell {candidate.CellIndex}.",
                    exception);
            }
        });

        return new TimberbornTaintedAshSoilPoisoningSummary(
            CandidateCellCount: candidates.Count,
            AppliedCellCount: applied);
    }
}

public readonly record struct TimberbornFertileAshCollectionCandidate(
    int CellIndex,
    int Strength);

public readonly record struct TimberbornFertileAshCollectedCell(
    int CellIndex,
    int StrengthToRemove,
    int GoodAmount);

public readonly record struct TimberbornFertileAshFieldHarvestTarget(
    int CellIndex,
    int StrengthToRemove,
    GoodAmount GoodAmount,
    Vector3Int CellCoordinates,
    Vector3 WalkPosition,
    TimberbornFertileAshFieldHarvestSource Source);

public enum TimberbornFertileAshFieldHarvestSource
{
    SimulatorAshField,
}

public readonly record struct TimberbornFertileAshCollectionAdapterResult(
    int GathererPostCount,
    int CandidateCellCount,
    int ReachableCellCount,
    int CollectedGoodCount,
    IReadOnlyList<TimberbornFertileAshCollectedCell> CollectedCells);

public readonly record struct TimberbornFertileAshCollectionSummary(
    int GathererPostCount,
    int CandidateCellCount,
    int ReachableCellCount,
    int CollectedGoodCount,
    int DepletedAshCellCount,
    int SkippedTaintedOrSpentCellCount)
{
    public static readonly TimberbornFertileAshCollectionSummary Empty = new(
        GathererPostCount: 0,
        CandidateCellCount: 0,
        ReachableCellCount: 0,
        CollectedGoodCount: 0,
        DepletedAshCellCount: 0,
        SkippedTaintedOrSpentCellCount: 0);

    public string ToLogToken(uint tick)
    {
        return "wildfire_timberborn_fertile_ash_collection_applied " +
            $"tick={tick} " +
            $"gatherer_posts={GathererPostCount} " +
            $"candidate_cells={CandidateCellCount} " +
            $"reachable_cells={ReachableCellCount} " +
            $"collected_goods={CollectedGoodCount} " +
            $"depleted_ash_cells={DepletedAshCellCount} " +
            $"skipped_tainted_or_spent_cells={SkippedTaintedOrSpentCellCount}";
    }
}

public interface ITimberbornFertileAshCollectionAdapter
{
    TimberbornFertileAshCollectionAdapterResult Collect(
        uint tick,
        IReadOnlyList<TimberbornFertileAshCollectionCandidate> candidates);
}

public sealed class UnavailableTimberbornFertileAshCollectionAdapter : ITimberbornFertileAshCollectionAdapter
{
    public static readonly UnavailableTimberbornFertileAshCollectionAdapter Instance = new();

    private UnavailableTimberbornFertileAshCollectionAdapter()
    {
    }

    public TimberbornFertileAshCollectionAdapterResult Collect(
        uint tick,
        IReadOnlyList<TimberbornFertileAshCollectionCandidate> candidates)
    {
        if (candidates is null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        throw new InvalidOperationException("Fertile ash collection adapter is unavailable.");
    }
}

public sealed class TimberbornFertileAshCollectionService
{
    public const int StrengthPerGood = 1;

    private readonly ITimberbornFertileAshCollectionAdapter _adapter;
    private readonly ITimberbornFireLogSink _logSink;

    public TimberbornFertileAshCollectionService(
        ITimberbornFertileAshCollectionAdapter? adapter = null,
        ITimberbornFireLogSink? logSink = null)
    {
        _adapter = adapter ?? UnavailableTimberbornFertileAshCollectionAdapter.Instance;
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
    }

    public TimberbornFertileAshCollectionSummary LastSummary { get; private set; } =
        TimberbornFertileAshCollectionSummary.Empty;

    public TimberbornFertileAshCollectionSummary Apply(
        uint tick,
        TimberbornAshFieldService ashFieldService,
        Action<TimberbornFertileAshCollectedCell>? onCollectedCell = null)
    {
        if (ashFieldService is null)
        {
            throw new ArgumentNullException(nameof(ashFieldService));
        }

        int skippedTaintedOrSpent = ashFieldService.Entries.Values
            .Count(static entry => entry.Quality is WildfireAshQuality.Spent or WildfireAshQuality.Tainted);
        TimberbornFertileAshCollectionCandidate[] candidates = ashFieldService.Entries.Values
            .Where(static entry => entry.Quality == WildfireAshQuality.Fertile &&
                entry.Strength > 0 &&
                !entry.IsActiveSource)
            .Select(static entry => new TimberbornFertileAshCollectionCandidate(entry.CellIndex, entry.Strength))
            .ToArray();
        TimberbornFertileAshCollectionAdapterResult adapterResult = candidates.Length == 0
            ? new TimberbornFertileAshCollectionAdapterResult(
                GathererPostCount: 0,
                CandidateCellCount: 0,
                ReachableCellCount: 0,
                CollectedGoodCount: 0,
                CollectedCells: Array.Empty<TimberbornFertileAshCollectedCell>())
            : _adapter.Collect(tick, candidates);
        TimberbornAshFieldCollectionRemoval[] removals = adapterResult.CollectedCells
            .Select(cell =>
            {
                onCollectedCell?.Invoke(cell);
                return ashFieldService.CalculateCollectedFertileStrengthRemoval(cell.CellIndex, cell.StrengthToRemove);
            })
            .ToArray();
        LastSummary = new TimberbornFertileAshCollectionSummary(
            GathererPostCount: adapterResult.GathererPostCount,
            CandidateCellCount: adapterResult.CandidateCellCount,
            ReachableCellCount: adapterResult.ReachableCellCount,
            CollectedGoodCount: adapterResult.CollectedGoodCount,
            DepletedAshCellCount: removals.Count(static removal => removal.RemovedEntry),
            SkippedTaintedOrSpentCellCount: skippedTaintedOrSpent);

        if (LastSummary.GathererPostCount > 0 ||
            LastSummary.CandidateCellCount > 0 ||
            LastSummary.CollectedGoodCount > 0)
        {
            _logSink.Info(LastSummary.ToLogToken(tick));
        }

        return LastSummary;
    }

    public void RecordWorkerHarvest(
        uint tick,
        TimberbornAshFieldCollectionRemoval removal,
        int goodAmount)
    {
        if (goodAmount <= 0 || removal.StrengthRemoved <= 0)
        {
            return;
        }

        LastSummary = LastSummary with
        {
            CollectedGoodCount = LastSummary.CollectedGoodCount + goodAmount,
            DepletedAshCellCount = LastSummary.DepletedAshCellCount + (removal.RemovedEntry ? 1 : 0),
        };
        _logSink.Info(LastSummary.ToLogToken(tick));
    }

    public void Clear()
    {
        LastSummary = TimberbornFertileAshCollectionSummary.Empty;
    }
}

public sealed class TimberbornGathererPostFertileAshCollectionAdapter : ITimberbornFertileAshCollectionAdapter
{
    public const int MaxCollectionRangeCells = 16;
    public const int MaxGoodsPerPostPerTick = 1;

    private readonly EntityRegistry _entityRegistry;
    private readonly Func<FireGrid?> _gridProvider;
    private readonly ITimberbornFireLogSink _logSink;

    public TimberbornGathererPostFertileAshCollectionAdapter(
        EntityRegistry entityRegistry,
        Func<FireGrid?> gridProvider,
        ITimberbornFireLogSink? logSink = null)
    {
        _entityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
        _gridProvider = gridProvider ?? throw new ArgumentNullException(nameof(gridProvider));
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
    }

    public TimberbornFertileAshCollectionAdapterResult Collect(
        uint tick,
        IReadOnlyList<TimberbornFertileAshCollectionCandidate> candidates)
    {
        if (candidates is null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        FireGrid? grid = _gridProvider();
        if (!grid.HasValue)
        {
            throw new InvalidOperationException("Fertile ash collection grid is unavailable.");
        }

        GathererPostTarget[] gatherers = TimberbornEntityComponentCells
            .ComponentBlockObjects<GathererFlag>(_entityRegistry)
            .Select(CreateGathererTarget)
            .Where(static target => target.HasValue)
            .Select(static target => target!.Value)
            .ToArray();
        if (gatherers.Length == 0)
        {
            return new TimberbornFertileAshCollectionAdapterResult(
                GathererPostCount: 0,
                CandidateCellCount: candidates.Count,
                ReachableCellCount: 0,
                CollectedGoodCount: 0,
                CollectedCells: Array.Empty<TimberbornFertileAshCollectedCell>());
        }

        int reachableCells = 0;
        gatherers.ToList().ForEach(gatherer =>
        {
            TimberbornFertileAshCollectionCandidate? candidate = candidates
                .Select(candidate => new
                {
                    Candidate = candidate,
                    Coordinates = grid.Value.FromIndex(candidate.CellIndex),
                })
                .Where(item => DistanceSquared(gatherer.Center, item.Coordinates) <=
                    MaxCollectionRangeCells * MaxCollectionRangeCells)
                .OrderBy(item => DistanceSquared(gatherer.Center, item.Coordinates))
                .ThenBy(item => item.Candidate.CellIndex)
                .Select(item => (TimberbornFertileAshCollectionCandidate?)item.Candidate)
                .FirstOrDefault();

            if (!candidate.HasValue)
            {
                return;
            }

            int goodsToCollect = Math.Min(
                MaxGoodsPerPostPerTick,
                candidate.Value.Strength / TimberbornFertileAshCollectionService.StrengthPerGood);
            if (goodsToCollect <= 0)
            {
                return;
            }

            reachableCells++;
        });

        return new TimberbornFertileAshCollectionAdapterResult(
            GathererPostCount: gatherers.Length,
            CandidateCellCount: candidates.Count,
            ReachableCellCount: reachableCells,
            CollectedGoodCount: 0,
            CollectedCells: Array.Empty<TimberbornFertileAshCollectedCell>());
    }

    private static GathererPostTarget? CreateGathererTarget(
        TimberbornEntityComponentCells.TimberbornEntityComponentBlockObject<GathererFlag> item)
    {
        List<Inventory> inventoryList = new();
        item.Component.GetComponents<Inventory>(inventoryList);
        Inventory? inventory = inventoryList.Count > 0 ? inventoryList[0] : null;
        if (inventory is null)
        {
            return null;
        }

        Vector3Int[] occupiedCoordinates = item.BlockObject.PositionedBlocks.GetOccupiedCoordinates().ToArray();
        if (occupiedCoordinates.Length == 0)
        {
            return null;
        }

        Vector3Int center = occupiedCoordinates
            .OrderBy(static coordinates => coordinates.x)
            .ThenBy(static coordinates => coordinates.y)
            .ThenBy(static coordinates => coordinates.z)
            .Skip(occupiedCoordinates.Length / 2)
            .First();
        return new GathererPostTarget(
            RuntimeHelpers.GetHashCode(item.Component),
            center,
            inventory);
    }

    private static int DistanceSquared(Vector3Int first, (int X, int Y, int Z) second)
    {
        int dx = first.x - second.X;
        int dy = first.y - second.Y;
        int dz = first.z - second.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    private readonly record struct GathererPostTarget(int StableId, Vector3Int Center, Inventory Inventory);
}

public sealed class TimberbornFertileAshFieldWorkplaceBehavior :
    WorkplaceBehavior,
    IAwakableComponent,
    IUpdatableComponent
{
    internal const int GathererFlagOutputLimit = 20;

    private readonly TimberbornFireRuntime _runtime;
    private readonly ITimberbornFireLogSink _logSink;

    private GatherablePrioritizer? _gatherablePrioritizer;
    private GathererFlag? _gathererFlag;
    private Inventory? _inventory;
    private Workplace? _workplace;

    public TimberbornFertileAshFieldWorkplaceBehavior(TimberbornFireRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _logSink = new UnityTimberbornFireLogSink();
    }

    public void Awake()
    {
        _gatherablePrioritizer = GetComponent<GatherablePrioritizer>();
        _gathererFlag = GetComponent<GathererFlag>();
        _inventory = GetComponent<SimpleOutputInventory>().Inventory;
        _workplace = GetComponent<Workplace>();
        PromoteBeforeNativeGathering();
    }

    public void Update()
    {
        PromoteBeforeNativeGathering();
    }

    public override Decision Decide(BehaviorAgent agent)
    {
        if (agent is null)
        {
            throw new ArgumentNullException(nameof(agent));
        }

        if (!AllowsFertileAshPriority())
        {
            return Decision.ReleaseNow();
        }

        GoodCarrier carrier = agent.GetComponent<GoodCarrier>();
        if (carrier.IsCarrying || _inventory is null || !_inventory.Enabled)
        {
            return Decision.ReleaseNextTick();
        }

        if (_gathererFlag is null || !TryCenterOf(_gathererFlag, out Vector3Int center))
        {
            return Decision.ReleaseNextTick();
        }

        Accessible? depositAccessible = TryGetDepositAccessible(_gathererFlag);
        if (depositAccessible is null)
        {
            return Decision.ReleaseNextTick();
        }

        if (!_runtime.TryFindFertileAshFieldHarvestTarget(
                center,
                Math.Max(1, carrier.LiftingCapacity),
                out TimberbornFertileAshFieldHarvestTarget target))
        {
            return Decision.ReleaseNextTick();
        }

        if (!HasGathererFlagOutputRoom(_inventory, target.GoodAmount.Amount))
        {
            _logSink.Info(
                "wildfire_timberborn_fertile_ash_field_harvest_skipped " +
                "reason=no_gatherer_output_capacity " +
                $"cell_index={target.CellIndex} " +
                $"good_id={target.GoodAmount.GoodId} " +
                $"amount={target.GoodAmount.Amount} " +
                $"capacity={_inventory.Capacity} " +
                $"stock={TotalStock(_inventory)} " +
                $"reserved_capacity={TotalReservedCapacity(_inventory)}");
            return Decision.ReleaseNextTick();
        }

        TimberbornFertileAshFieldHarvestExecutor executor = new(
            _runtime,
            agent.GetComponent<WalkToPositionExecutor>(),
            agent.GetComponent<WalkToAccessibleExecutor>(),
            carrier,
            _inventory,
            target,
            depositAccessible,
            TryGetAnimatorController(agent),
            _logSink);
        executor.Launch();
        return Decision.ReleaseWhenFinished(executor);
    }

    private void PromoteBeforeNativeGathering()
    {
        if (_workplace is null)
        {
            throw new InvalidOperationException("Fertile ash workplace is unavailable.");
        }

        FieldInfo? field = typeof(Workplace).GetField(
            "_workplaceBehaviors",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(_workplace) is not List<WorkplaceBehavior> workplaceBehaviors)
        {
            throw new InvalidOperationException("Workplace behavior list is unavailable.");
        }

        int selfIndex = workplaceBehaviors.IndexOf(this);
        if (selfIndex <= 0)
        {
            return;
        }

        workplaceBehaviors.RemoveAt(selfIndex);
        workplaceBehaviors.Insert(0, this);
    }

    private bool AllowsFertileAshPriority()
    {
        GatherableSpec? prioritizedGatherable = _gatherablePrioritizer?.PrioritizedGatherable;
        if (prioritizedGatherable is null)
        {
            return false;
        }

        return string.Equals(
            prioritizedGatherable.GetSpec<TemplateSpec>().TemplateName,
            TimberbornFireRuntime.FertileAshFieldGatherableTemplateName,
            StringComparison.Ordinal);
    }

    private static bool TryCenterOf(GathererFlag gathererFlag, out Vector3Int center)
    {
        center = Vector3Int.zero;
        if (!gathererFlag.TryGetComponent(out BlockObject blockObject) ||
            blockObject.PositionedBlocks is null)
        {
            return false;
        }

        Vector3Int[] occupiedCoordinates = blockObject.PositionedBlocks.GetOccupiedCoordinates().ToArray();
        if (occupiedCoordinates.Length == 0)
        {
            return false;
        }

        center = occupiedCoordinates
            .OrderBy(static coordinates => coordinates.x)
            .ThenBy(static coordinates => coordinates.y)
            .ThenBy(static coordinates => coordinates.z)
            .Skip(occupiedCoordinates.Length / 2)
            .First();
        return true;
    }

    private static Accessible? TryGetDepositAccessible(GathererFlag gathererFlag)
    {
        List<Accessible> accessibles = new();
        gathererFlag.GetComponents(accessibles);
        return accessibles
            .Where(static accessible => accessible.ValidAccessible)
            .OrderByDescending(static accessible => accessible.HasSingleAccess)
            .FirstOrDefault();
    }

    private static object? TryGetAnimatorController(BehaviorAgent agent)
    {
        List<MonoBehaviour> components = new();
        agent.GetComponents(components);
        return components.FirstOrDefault(static component => component
                .GetType()
                .GetInterfaces()
                .Any(static implementedInterface =>
                    string.Equals(
                        implementedInterface.FullName,
                        "Timberborn.TimbermeshAnimations.IAnimatorController",
                        StringComparison.Ordinal)));
    }

    private static int TotalStock(Inventory inventory)
    {
        return inventory.Stock.Sum(static good => good.Amount);
    }

    private static int TotalReservedCapacity(Inventory inventory)
    {
        return inventory.ReservedCapacity().Sum(static good => good.Amount);
    }

    internal static bool HasGathererFlagOutputRoom(Inventory inventory, int amount)
    {
        return TotalStock(inventory) + Math.Max(1, amount) <= GathererFlagOutputLimit;
    }
}

internal sealed class TimberbornFertileAshFieldHarvestExecutor : IExecutor
{
    private const float HarvestTimeHours = 0.1f;

    private readonly TimberbornFireRuntime _runtime;
    private readonly WalkToPositionExecutor _walkToPositionExecutor;
    private readonly WalkToAccessibleExecutor _walkToAccessibleExecutor;
    private readonly GoodCarrier _goodCarrier;
    private readonly Inventory _inventory;
    private readonly TimberbornFertileAshFieldHarvestTarget _target;
    private readonly Accessible _depositAccessible;
    private readonly object? _animatorController;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly float _maxWalkHours;
    private bool _walking = true;
    private bool _returningToFlag;
    private bool _carryingHarvest;
    private TimberbornAshFieldCollectionRemoval? _removal;
    private float _walkedHours;
    private float _harvestedHours;
    private bool _completed;
    private bool _launchFailed;

    public TimberbornFertileAshFieldHarvestExecutor(
        TimberbornFireRuntime runtime,
        WalkToPositionExecutor walkToPositionExecutor,
        WalkToAccessibleExecutor walkToAccessibleExecutor,
        GoodCarrier goodCarrier,
        Inventory inventory,
        TimberbornFertileAshFieldHarvestTarget target,
        Accessible depositAccessible,
        object? animatorController,
        ITimberbornFireLogSink logSink,
        float maxWalkHours = 4f)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _walkToPositionExecutor = walkToPositionExecutor ?? throw new ArgumentNullException(nameof(walkToPositionExecutor));
        _walkToAccessibleExecutor = walkToAccessibleExecutor ?? throw new ArgumentNullException(nameof(walkToAccessibleExecutor));
        _goodCarrier = goodCarrier ?? throw new ArgumentNullException(nameof(goodCarrier));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _target = target;
        _depositAccessible = depositAccessible ?? throw new ArgumentNullException(nameof(depositAccessible));
        _animatorController = animatorController;
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
        _maxWalkHours = Math.Max(0.1f, maxWalkHours);
    }

    public void Launch()
    {
        ExecutorStatus launchStatus = _walkToPositionExecutor.Launch(_target.WalkPosition);
        if (launchStatus == ExecutorStatus.Failure)
        {
            _logSink.Warning(
                "wildfire_timberborn_fertile_ash_field_harvest_failed " +
                "reason=walk_launch_failed " +
                $"cell_index={_target.CellIndex}");
            _runtime.RecordFertileAshFieldHarvestWalkFailure(_target.CellIndex);
            _launchFailed = true;
            return;
        }

        _walking = launchStatus == ExecutorStatus.Running;
    }

    public ExecutorStatus Tick(float deltaTimeInHours)
    {
        if (_completed)
        {
            return ExecutorStatus.Success;
        }

        if (_launchFailed)
        {
            return ExecutorStatus.Failure;
        }

        if (_walking)
        {
            _walkedHours += Math.Max(0f, deltaTimeInHours);
            if (_walkedHours > _maxWalkHours)
            {
                SetWorkingAnimation(false);
                _logSink.Warning(
                    "wildfire_timberborn_fertile_ash_field_harvest_failed " +
                    $"reason={(_returningToFlag ? "return_walk_timeout" : "walk_timeout")} " +
                    $"cell_index={_target.CellIndex} " +
                    $"walk_hours={_walkedHours:0.###} " +
                    $"max_walk_hours={_maxWalkHours:0.###}");
                return ExecutorStatus.Failure;
            }

            ExecutorStatus walkStatus = _returningToFlag
                ? _walkToAccessibleExecutor.Tick(deltaTimeInHours)
                : _walkToPositionExecutor.Tick(deltaTimeInHours);
            if (walkStatus == ExecutorStatus.Running)
            {
                return ExecutorStatus.Running;
            }

            if (walkStatus == ExecutorStatus.Failure)
            {
                SetWorkingAnimation(false);
                _logSink.Warning(
                    "wildfire_timberborn_fertile_ash_field_harvest_failed " +
                    $"reason={(_returningToFlag ? "return_walk_failed" : "walk_failed")} " +
                    $"cell_index={_target.CellIndex}");
                if (!_returningToFlag)
                {
                    _runtime.RecordFertileAshFieldHarvestWalkFailure(_target.CellIndex);
                }

                return ExecutorStatus.Failure;
            }

            _walking = false;
        }

        if (_carryingHarvest)
        {
            return DepositHarvest();
        }

        SetWorkingAnimation(true);
        _harvestedHours += Math.Max(0f, deltaTimeInHours);
        if (_harvestedHours < HarvestTimeHours)
        {
            return ExecutorStatus.Running;
        }

        SetWorkingAnimation(false);
        if (!TimberbornFertileAshFieldWorkplaceBehavior.HasGathererFlagOutputRoom(
                _inventory,
                _target.GoodAmount.Amount))
        {
            _logSink.Warning(
                "wildfire_timberborn_fertile_ash_field_harvest_failed " +
                "reason=no_gatherer_output_capacity_at_completion " +
                $"cell_index={_target.CellIndex}");
            return ExecutorStatus.Failure;
        }

        if (!_runtime.TryCompleteFertileAshFieldHarvest(_target, out TimberbornAshFieldCollectionRemoval removal) ||
            removal.StrengthRemoved <= 0)
        {
            _logSink.Warning(
                "wildfire_timberborn_fertile_ash_field_harvest_failed " +
                "reason=ash_unavailable_at_completion " +
                $"cell_index={_target.CellIndex}");
            return ExecutorStatus.Failure;
        }

        _removal = removal;
        _goodCarrier.PutGoodsInHands(_target.GoodAmount, countAsAvailable: false);
        _carryingHarvest = true;
        _returningToFlag = true;
        _walkedHours = 0f;
        ExecutorStatus returnLaunchStatus = _walkToAccessibleExecutor.Launch(_depositAccessible);
        if (returnLaunchStatus == ExecutorStatus.Failure)
        {
            _logSink.Warning(
                "wildfire_timberborn_fertile_ash_field_harvest_failed " +
                "reason=return_walk_launch_failed " +
                $"cell_index={_target.CellIndex}");
            return ExecutorStatus.Failure;
        }

        _walking = returnLaunchStatus == ExecutorStatus.Running;
        if (_walking)
        {
            return ExecutorStatus.Running;
        }

        return DepositHarvest();
    }

    private ExecutorStatus DepositHarvest()
    {
        if (!TimberbornFertileAshFieldWorkplaceBehavior.HasGathererFlagOutputRoom(
                _inventory,
                _target.GoodAmount.Amount))
        {
            _logSink.Info(
                "wildfire_timberborn_fertile_ash_field_harvest_waiting " +
                "reason=waiting_for_gatherer_output_capacity_at_deposit " +
                $"cell_index={_target.CellIndex}");
            return ExecutorStatus.Running;
        }

        if (!_removal.HasValue)
        {
            _logSink.Warning(
                "wildfire_timberborn_fertile_ash_field_harvest_failed " +
                "reason=missing_harvest_removal_at_deposit " +
                $"cell_index={_target.CellIndex}");
            return ExecutorStatus.Failure;
        }

        TimberbornAshFieldCollectionRemoval removal = _removal.Value;
        _inventory.GiveIgnoringCapacity(_target.GoodAmount);
        _goodCarrier.EmptyHands();
        _completed = true;
        _logSink.Info(
            "wildfire_timberborn_fertile_ash_field_harvest_completed " +
            $"cell_index={_target.CellIndex} " +
            $"source={_target.Source} " +
            $"strength_removed={removal.StrengthRemoved} " +
            $"removed_entry={removal.RemovedEntry.ToString().ToLowerInvariant()} " +
            $"good_id={_target.GoodAmount.GoodId} " +
            $"amount={_target.GoodAmount.Amount}");
        return ExecutorStatus.Success;
    }

    private void SetWorkingAnimation(bool state)
    {
        if (_animatorController is null)
        {
            return;
        }

        Type animatorControllerType = _animatorController.GetType();
        MethodInfo? hasParameter = animatorControllerType.GetMethod("HasParameter", new[] { typeof(string) });
        MethodInfo? setBool = animatorControllerType.GetMethod("SetBool", new[] { typeof(string), typeof(bool) });
        if (hasParameter is null || setBool is null)
        {
            return;
        }

        if (hasParameter.Invoke(_animatorController, new object[] { "Working" }) is true)
        {
            setBool.Invoke(_animatorController, new object[] { "Working", state });
        }
    }

    public void Save(IEntitySaver entitySaver)
    {
    }

    public void Load(IEntityLoader entityLoader)
    {
    }
}
