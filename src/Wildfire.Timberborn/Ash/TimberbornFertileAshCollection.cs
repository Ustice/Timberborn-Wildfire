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

public readonly record struct TimberbornFertileAshFieldHarvestTarget(int CellIndex, Vector3 WalkPosition);

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
