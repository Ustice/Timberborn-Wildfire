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
using Timberborn.Navigation;
using Timberborn.SimpleOutputBuildings;
using Timberborn.TemplateSystem;
using Timberborn.WalkingSystem;
using Timberborn.WorkSystem;
using Timberborn.WorldPersistence;
using Timberborn.SoilContaminationSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

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
            .Where(static entry => entry.Quality == WildfireAshQuality.Fertile && entry.Strength > 0)
            .Select(static entry => new TimberbornFertileAshCollectionCandidate(entry.CellIndex, entry.Strength))
            .ToArray();
        TimberbornFertileAshCollectionAdapterResult adapterResult = _adapter.Collect(tick, candidates);
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
            SkippedInventoryApiCount: 0,
            CollectedCells: Array.Empty<TimberbornFertileAshCollectedCell>());
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
            return;
        }

        FieldInfo? field = typeof(Workplace).GetField(
            "_workplaceBehaviors",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.GetValue(_workplace) is not List<WorkplaceBehavior> workplaceBehaviors)
        {
            return;
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
