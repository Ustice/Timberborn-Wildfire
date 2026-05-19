using System.Reflection;
using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.Gathering;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.SoilContaminationSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public readonly record struct TimberbornTaintedAshSoilPoisoningCandidate(
    int CellIndex,
    int Strength);

public readonly record struct TimberbornTaintedAshSoilPoisoningSummary(
    int CandidateCellCount,
    int AppliedCellCount,
    int SkippedNoSafeApiCount)
{
    public static readonly TimberbornTaintedAshSoilPoisoningSummary Empty = new(
        CandidateCellCount: 0,
        AppliedCellCount: 0,
        SkippedNoSafeApiCount: 0);

    public string ToLogToken(uint tick)
    {
        return "wildfire_timberborn_tainted_ash_soil_poisoning_applied " +
            $"tick={tick} " +
            $"candidate_cells={CandidateCellCount} " +
            $"applied_cells={AppliedCellCount} " +
            $"skipped_no_safe_api={SkippedNoSafeApiCount}";
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

        return new TimberbornTaintedAshSoilPoisoningSummary(
            CandidateCellCount: candidates.Count,
            AppliedCellCount: 0,
            SkippedNoSafeApiCount: candidates.Count);
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

        LastSummary = _adapter.ApplyPoisoning(tick, candidates);
        if (LastSummary.CandidateCellCount > 0 ||
            LastSummary.AppliedCellCount > 0 ||
            LastSummary.SkippedNoSafeApiCount > 0)
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

public sealed class TimberbornSoilContaminationAshPoisoningAdapter : ITimberbornTaintedAshSoilPoisoningAdapter
{
    private const float MaxMapContamination = 0.9f;

    private readonly ISoilContaminationService _soilContaminationService;
    private readonly Func<FireGrid?> _gridProvider;
    private readonly ITimberbornFireLogSink _logSink;

    public TimberbornSoilContaminationAshPoisoningAdapter(
        ISoilContaminationService soilContaminationService,
        Func<FireGrid?> gridProvider,
        ITimberbornFireLogSink? logSink = null)
    {
        _soilContaminationService = soilContaminationService ??
            throw new ArgumentNullException(nameof(soilContaminationService));
        _gridProvider = gridProvider ?? throw new ArgumentNullException(nameof(gridProvider));
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
        MethodInfo? updateMethod = _soilContaminationService
            .GetType()
            .GetMethod(
                "UpdateContamination",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                new[] { typeof(Vector3Int), typeof(float) },
                modifiers: null);
        if (!grid.HasValue || updateMethod is null)
        {
            return new TimberbornTaintedAshSoilPoisoningSummary(
                CandidateCellCount: candidates.Count,
                AppliedCellCount: 0,
                SkippedNoSafeApiCount: candidates.Count);
        }

        int applied = 0;
        int skipped = 0;
        candidates.ToList().ForEach(candidate =>
        {
            try
            {
                (int x, int y, int z) = grid.Value.FromIndex(candidate.CellIndex);
                float current = _soilContaminationService.Contamination(candidate.CellIndex);
                float target = Math.Max(current, MaxMapContamination);
                updateMethod.Invoke(_soilContaminationService, new object[] { new Vector3Int(x, y, z), target });
                applied++;
            }
            catch (Exception exception)
            {
                skipped++;
                _logSink.Warning(
                    "wildfire_timberborn_tainted_ash_soil_poisoning_failed " +
                    $"tick={tick} " +
                    $"cell_index={candidate.CellIndex} " +
                    $"message={TimberbornQaCommandBridge.FormatToken(exception.GetType().Name)}");
            }
        });

        return new TimberbornTaintedAshSoilPoisoningSummary(
            CandidateCellCount: candidates.Count,
            AppliedCellCount: applied,
            SkippedNoSafeApiCount: skipped);
    }
}

public readonly record struct TimberbornFertileAshCollectionCandidate(
    int CellIndex,
    int Strength);

public readonly record struct TimberbornFertileAshCollectedCell(
    int CellIndex,
    int StrengthToRemove,
    int GoodAmount);

public readonly record struct TimberbornFertileAshCollectionAdapterResult(
    int GathererPostCount,
    int CandidateCellCount,
    int ReachableCellCount,
    int CollectedGoodCount,
    int SkippedInventoryApiCount,
    IReadOnlyList<TimberbornFertileAshCollectedCell> CollectedCells);

public readonly record struct TimberbornFertileAshCollectionSummary(
    int GathererPostCount,
    int CandidateCellCount,
    int ReachableCellCount,
    int CollectedGoodCount,
    int DepletedAshCellCount,
    int SkippedTaintedOrSpentCellCount,
    int SkippedInventoryApiCount)
{
    public static readonly TimberbornFertileAshCollectionSummary Empty = new(
        GathererPostCount: 0,
        CandidateCellCount: 0,
        ReachableCellCount: 0,
        CollectedGoodCount: 0,
        DepletedAshCellCount: 0,
        SkippedTaintedOrSpentCellCount: 0,
        SkippedInventoryApiCount: 0);

    public string ToLogToken(uint tick)
    {
        return "wildfire_timberborn_fertile_ash_collection_applied " +
            $"tick={tick} " +
            $"gatherer_posts={GathererPostCount} " +
            $"candidate_cells={CandidateCellCount} " +
            $"reachable_cells={ReachableCellCount} " +
            $"collected_goods={CollectedGoodCount} " +
            $"depleted_ash_cells={DepletedAshCellCount} " +
            $"skipped_tainted_or_spent_cells={SkippedTaintedOrSpentCellCount} " +
            $"skipped_inventory_api={SkippedInventoryApiCount}";
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

        return new TimberbornFertileAshCollectionAdapterResult(
            GathererPostCount: 0,
            CandidateCellCount: candidates.Count,
            ReachableCellCount: 0,
            CollectedGoodCount: 0,
            SkippedInventoryApiCount: candidates.Count,
            CollectedCells: Array.Empty<TimberbornFertileAshCollectedCell>());
    }
}

public sealed class TimberbornFertileAshCollectionService
{
    public const int StrengthPerGood = 25;

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
            .Where(static entry => entry.Quality == WildfireAshQuality.Fertile && entry.Strength > 0)
            .Select(static entry => new TimberbornFertileAshCollectionCandidate(entry.CellIndex, entry.Strength))
            .ToArray();
        TimberbornFertileAshCollectionAdapterResult adapterResult = _adapter.Collect(tick, candidates);
        TimberbornAshFieldCollectionRemoval[] removals = adapterResult.CollectedCells
            .Select(cell =>
            {
                onCollectedCell?.Invoke(cell);
                return ashFieldService.RemoveCollectedFertileStrength(cell.CellIndex, cell.StrengthToRemove);
            })
            .ToArray();
        LastSummary = new TimberbornFertileAshCollectionSummary(
            GathererPostCount: adapterResult.GathererPostCount,
            CandidateCellCount: adapterResult.CandidateCellCount,
            ReachableCellCount: adapterResult.ReachableCellCount,
            CollectedGoodCount: adapterResult.CollectedGoodCount,
            DepletedAshCellCount: removals.Count(static removal => removal.RemovedEntry),
            SkippedTaintedOrSpentCellCount: skippedTaintedOrSpent,
            SkippedInventoryApiCount: adapterResult.SkippedInventoryApiCount);

        if (LastSummary.GathererPostCount > 0 ||
            LastSummary.CandidateCellCount > 0 ||
            LastSummary.CollectedGoodCount > 0 ||
            LastSummary.SkippedInventoryApiCount > 0)
        {
            _logSink.Info(LastSummary.ToLogToken(tick));
        }

        return LastSummary;
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
            return Unavailable(candidates.Count);
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
                SkippedInventoryApiCount: candidates.Count,
                CollectedCells: Array.Empty<TimberbornFertileAshCollectedCell>());
        }

        HashSet<int> collectedCellIndices = new();
        List<TimberbornFertileAshCollectedCell> collectedCells = new();
        int reachableCells = 0;
        int skippedInventoryApi = 0;
        gatherers.ToList().ForEach(gatherer =>
        {
            TimberbornFertileAshCollectionCandidate? candidate = candidates
                .Where(candidate => !collectedCellIndices.Contains(candidate.CellIndex))
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
            try
            {
                gatherer.Inventory.GiveIgnoringCapacity(
                    new GoodAmount(TimberbornAshFieldService.FertileAshGoodId, goodsToCollect));
                int strengthToRemove = Math.Min(
                    candidate.Value.Strength,
                    goodsToCollect * TimberbornFertileAshCollectionService.StrengthPerGood);
                collectedCellIndices.Add(candidate.Value.CellIndex);
                collectedCells.Add(new TimberbornFertileAshCollectedCell(
                    candidate.Value.CellIndex,
                    strengthToRemove,
                    goodsToCollect));
            }
            catch (Exception exception)
            {
                skippedInventoryApi++;
                _logSink.Warning(
                    "wildfire_timberborn_fertile_ash_collection_inventory_failed " +
                    $"tick={tick} " +
                    $"cell_index={candidate.Value.CellIndex} " +
                    $"gatherer_id={gatherer.StableId} " +
                    $"message={TimberbornQaCommandBridge.FormatToken(exception.GetType().Name)}");
            }
        });

        return new TimberbornFertileAshCollectionAdapterResult(
            GathererPostCount: gatherers.Length,
            CandidateCellCount: candidates.Count,
            ReachableCellCount: reachableCells,
            CollectedGoodCount: collectedCells.Sum(static cell => cell.GoodAmount),
            SkippedInventoryApiCount: skippedInventoryApi,
            CollectedCells: collectedCells.ToArray());
    }

    private static TimberbornFertileAshCollectionAdapterResult Unavailable(int candidateCount)
    {
        return new TimberbornFertileAshCollectionAdapterResult(
            GathererPostCount: 0,
            CandidateCellCount: candidateCount,
            ReachableCellCount: 0,
            CollectedGoodCount: 0,
            SkippedInventoryApiCount: candidateCount,
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
