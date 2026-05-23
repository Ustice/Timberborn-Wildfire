using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Stockpiles;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Consequences;

public readonly record struct TimberbornStoredGoodBurnConsequence(
    int CellIndex,
    uint Tick,
    int BurnBudget,
    int Fuel,
    int Heat,
    int Water)
{
    public bool ShouldBurnStoredGoods => BurnBudget > 0;

    public static TimberbornStoredGoodBurnConsequence FromDecision(
        uint tick,
        TimberbornFireCellDeltaDecision decision)
    {
        return new TimberbornStoredGoodBurnConsequence(
            decision.CellIndex,
            tick,
            Math.Max(0, decision.OldFuel - decision.NewFuel),
            decision.NewFuel,
            decision.NewHeat,
            decision.NewWater);
    }
}

public sealed record TimberbornStoredGoodStack(string ResourceId, int Amount);

public sealed record TimberbornStoredGoodBurnTarget(
    string StableId,
    IReadOnlyList<TimberbornStoredGoodStack> Stacks,
    bool CanMutateInventory);

public readonly record struct TimberbornStoredGoodBurnConsequenceResult(
    bool MatchedStorageCell,
    bool AppliedConsequence,
    int BurnableStackCount,
    int DestroyedItemCount,
    int HazardousGoodCount,
    int SkippedNoInventoryApiCount,
    int SkippedUnknownResourceCount,
    int SkippedNonBurnableItemCount);

public readonly record struct TimberbornStoredGoodBurnConsequenceSummary(
    int ConsideredDeltaCount,
    int MatchedStorageCellCount,
    int DuplicateStorageTargetSuppressedCount,
    int BurnableStackCount,
    int DestroyedItemCount,
    int HazardousGoodCount,
    int SkippedNoInventoryApiCount,
    int SkippedUnknownResourceCount,
    int SkippedNonBurnableItemCount)
{
    public static readonly TimberbornStoredGoodBurnConsequenceSummary Empty = new(
        ConsideredDeltaCount: 0,
        MatchedStorageCellCount: 0,
        DuplicateStorageTargetSuppressedCount: 0,
        BurnableStackCount: 0,
        DestroyedItemCount: 0,
        HazardousGoodCount: 0,
        SkippedNoInventoryApiCount: 0,
        SkippedUnknownResourceCount: 0,
        SkippedNonBurnableItemCount: 0);

    public string ToLogToken(uint tick)
    {
        return "wildfire_timberborn_stored_goods_burn_applied " +
            $"tick={tick} " +
            $"considered_deltas={ConsideredDeltaCount} " +
            $"matched_storage_cells={MatchedStorageCellCount} " +
            $"duplicate_storage_targets_suppressed={DuplicateStorageTargetSuppressedCount} " +
            $"burnable_stacks={BurnableStackCount} " +
            $"destroyed_items={DestroyedItemCount} " +
            $"hazardous_goods={HazardousGoodCount} " +
            $"skipped_no_inventory_api={SkippedNoInventoryApiCount} " +
            $"skipped_unknown_resources={SkippedUnknownResourceCount} " +
            $"skipped_non_burnable_items={SkippedNonBurnableItemCount}";
    }
}

public interface ITimberbornStoredGoodBurnConsequenceSink
{
    TimberbornStoredGoodBurnConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions);
}

public interface ITimberbornStoredGoodBurnInventoryApi
{
    TimberbornStoredGoodBurnTarget? ResolveTarget(TimberbornStoredGoodBurnConsequence consequence);

    TimberbornStoredGoodBurnConsequenceResult BurnStoredGoods(
        TimberbornStoredGoodBurnTarget target,
        int burnBudget,
        IReadOnlyList<TimberbornStoredGoodStack> stacksToDestroy);
}

public sealed class TimberbornStoredGoodBurnConsequenceSink : ITimberbornStoredGoodBurnConsequenceSink
{
    private readonly ITimberbornStoredGoodBurnInventoryApi _inventoryApi;
    private readonly TimberbornResourceFuelCatalog _resourceFuelCatalog;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly ITimberbornBurnDamageTargetStateProvider? _burnDamageTargets;

    public TimberbornStoredGoodBurnConsequenceSink(
        ITimberbornStoredGoodBurnInventoryApi inventoryApi,
        TimberbornResourceFuelCatalog? resourceFuelCatalog = null,
        ITimberbornFireLogSink? logSink = null,
        ITimberbornBurnDamageTargetStateProvider? burnDamageTargets = null)
    {
        _inventoryApi = inventoryApi ?? throw new ArgumentNullException(nameof(inventoryApi));
        _resourceFuelCatalog = resourceFuelCatalog ?? TimberbornResourceFuelCatalog.Default;
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
        _burnDamageTargets = burnDamageTargets;
    }

    public TimberbornStoredGoodBurnConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        TimberbornStoredGoodBurnConsequence[] consequences = decisions
            .Select(decision => TimberbornStoredGoodBurnConsequence.FromDecision(tick, decision))
            .Where(static consequence => consequence.ShouldBurnStoredGoods)
            .ToArray();
        ResolvedTarget[] resolvedTargets = consequences
            .Select(ResolveTarget)
            .ToArray();
        ResolvedTarget[] matchedTargets = resolvedTargets
            .Where(static resolvedTarget => resolvedTarget.Target is not null || resolvedTarget.HasOwnedStorageWithoutInventoryApi)
            .ToArray();
        ResolvedTarget[] uniqueTargets = matchedTargets
            .GroupBy(static resolvedTarget => resolvedTarget.StableId, StringComparer.Ordinal)
            .Select(static group => group
                .OrderByDescending(static resolvedTarget => resolvedTarget.Consequence.BurnBudget)
                .ThenByDescending(static resolvedTarget => resolvedTarget.Consequence.Heat)
                .ThenBy(static resolvedTarget => resolvedTarget.Consequence.CellIndex)
                .First())
            .ToArray();
        TimberbornStoredGoodBurnConsequenceResult[] results = uniqueTargets
            .Select(BurnTarget)
            .ToArray();

        TimberbornStoredGoodBurnConsequenceSummary summary = new(
            ConsideredDeltaCount: consequences.Length,
            MatchedStorageCellCount: matchedTargets.Length,
            DuplicateStorageTargetSuppressedCount: matchedTargets.Length - uniqueTargets.Length,
            BurnableStackCount: results.Sum(static result => result.BurnableStackCount),
            DestroyedItemCount: results.Sum(static result => result.DestroyedItemCount),
            HazardousGoodCount: results.Sum(static result => result.HazardousGoodCount),
            SkippedNoInventoryApiCount: results.Sum(static result => result.SkippedNoInventoryApiCount),
            SkippedUnknownResourceCount: results.Sum(static result => result.SkippedUnknownResourceCount),
            SkippedNonBurnableItemCount: results.Sum(static result => result.SkippedNonBurnableItemCount));
        if (TimberbornReleaseLogNoisePolicy.ShouldLogConsequenceSummary(
            summary.MatchedStorageCellCount,
            summary.DestroyedItemCount,
            summary.HazardousGoodCount,
            summary.SkippedNoInventoryApiCount,
            summary.SkippedUnknownResourceCount))
        {
            _logSink.Info(summary.ToLogToken(tick));
        }

        return summary;
    }

    private TimberbornStoredGoodBurnConsequenceResult BurnTarget(ResolvedTarget resolvedTarget)
    {
        if (resolvedTarget.Target is null)
        {
            return new TimberbornStoredGoodBurnConsequenceResult(
                MatchedStorageCell: true,
                AppliedConsequence: false,
                BurnableStackCount: 0,
                DestroyedItemCount: 0,
                HazardousGoodCount: 0,
                SkippedNoInventoryApiCount: resolvedTarget.Consequence.BurnBudget,
                SkippedUnknownResourceCount: 0,
                SkippedNonBurnableItemCount: 0);
        }

        TimberbornStoredGoodBurnTarget target = resolvedTarget.Target;
        ClassifiedStack[] classifiedStacks = target.Stacks
            .Where(static stack => stack.Amount > 0)
            .Select(stack => new ClassifiedStack(stack, _resourceFuelCatalog.Lookup(stack.ResourceId)))
            .ToArray();
        int skippedUnknownResourceCount = classifiedStacks
            .Count(static stack => !stack.Profile.Known);
        int hazardousGoodCount = classifiedStacks
            .Where(static stack => stack.Profile.Explosive || stack.Profile.Contaminated)
            .Sum(static stack => stack.Stack.Amount);
        int skippedNonBurnableItemCount = classifiedStacks
            .Where(static stack => stack.Profile.FuelValue == 0)
            .Sum(static stack => stack.Stack.Amount);
        TimberbornStoredGoodStack[] burnableStacks = classifiedStacks
            .Where(static stack => stack.Profile.FuelValue > 0)
            .Where(static stack => stack.Profile.Known)
            .Where(static stack => !stack.Profile.Explosive)
            .Where(static stack => !stack.Profile.Contaminated)
            .OrderByDescending(static stack => stack.Profile.Flammability)
            .ThenByDescending(static stack => stack.Profile.FuelValue)
            .ThenBy(static stack => stack.Stack.ResourceId, StringComparer.Ordinal)
            .Select(static stack => stack.Stack)
            .ToArray();
        TimberbornStoredGoodStack[] stacksToDestroy = SelectStacksToDestroy(
            burnableStacks,
            resolvedTarget.Consequence.BurnBudget);

        if (!target.CanMutateInventory)
        {
            return new TimberbornStoredGoodBurnConsequenceResult(
                MatchedStorageCell: true,
                AppliedConsequence: false,
                BurnableStackCount: burnableStacks.Length,
                DestroyedItemCount: 0,
                HazardousGoodCount: hazardousGoodCount,
                SkippedNoInventoryApiCount: burnableStacks.Sum(static stack => stack.Amount),
                SkippedUnknownResourceCount: skippedUnknownResourceCount,
                SkippedNonBurnableItemCount: skippedNonBurnableItemCount);
        }

        return _inventoryApi.BurnStoredGoods(
            target,
            resolvedTarget.Consequence.BurnBudget,
            stacksToDestroy) with
        {
            BurnableStackCount = burnableStacks.Length,
            HazardousGoodCount = hazardousGoodCount,
            SkippedUnknownResourceCount = skippedUnknownResourceCount,
            SkippedNonBurnableItemCount = skippedNonBurnableItemCount,
        };
    }

    private static TimberbornStoredGoodStack[] SelectStacksToDestroy(
        IReadOnlyList<TimberbornStoredGoodStack> burnableStacks,
        int burnBudget)
    {
        int remainingBudget = Math.Max(0, burnBudget);
        return burnableStacks
            .Select(stack =>
            {
                int amount = Math.Min(stack.Amount, remainingBudget);
                remainingBudget -= amount;
                return new TimberbornStoredGoodStack(stack.ResourceId, amount);
            })
            .Where(static stack => stack.Amount > 0)
            .ToArray();
    }

    private ResolvedTarget ResolveTarget(TimberbornStoredGoodBurnConsequence consequence)
    {
        TimberbornStoredGoodBurnTarget? target = _inventoryApi.ResolveTarget(consequence);
        if (_burnDamageTargets is null)
        {
            return new ResolvedTarget(consequence, target, BurnDamageState: null);
        }

        if (!_burnDamageTargets.TryGetStateForCell(consequence.CellIndex, out TimberbornBurnDamageTargetState state) ||
            state.TargetKind != TimberbornBurnDamageTargetKind.Storage)
        {
            return new ResolvedTarget(consequence, Target: null, BurnDamageState: null);
        }

        if (target is not null &&
            string.Equals(target.StableId, state.TargetKey.StableId, StringComparison.Ordinal))
        {
            return new ResolvedTarget(consequence, target, state);
        }

        return new ResolvedTarget(consequence, Target: null, state);
    }

    private readonly record struct ResolvedTarget(
        TimberbornStoredGoodBurnConsequence Consequence,
        TimberbornStoredGoodBurnTarget? Target,
        TimberbornBurnDamageTargetState? BurnDamageState)
    {
        public string StableId => Target?.StableId ?? BurnDamageState?.TargetKey.StableId ?? string.Empty;

        public bool HasOwnedStorageWithoutInventoryApi => Target is null && BurnDamageState is not null;
    }

    private readonly record struct ClassifiedStack(
        TimberbornStoredGoodStack Stack,
        TimberbornResourceFuelProfile Profile);
}

public sealed class NullTimberbornStoredGoodBurnConsequenceSink : ITimberbornStoredGoodBurnConsequenceSink
{
    public static readonly NullTimberbornStoredGoodBurnConsequenceSink Instance = new();

    private NullTimberbornStoredGoodBurnConsequenceSink()
    {
    }

    public TimberbornStoredGoodBurnConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        return TimberbornStoredGoodBurnConsequenceSummary.Empty;
    }
}

public sealed class TimberbornStockpileStoredGoodBurnInventoryApi : ITimberbornStoredGoodBurnInventoryApi
{
    private readonly FireGrid _grid;
    private readonly IBlockService _blockService;
    private readonly Dictionary<string, Stockpile> _stockpilesByStableId = new(StringComparer.Ordinal);

    public TimberbornStockpileStoredGoodBurnInventoryApi(FireGrid grid, IBlockService blockService)
    {
        _grid = grid;
        _blockService = blockService ?? throw new ArgumentNullException(nameof(blockService));
    }

    public TimberbornStoredGoodBurnTarget? ResolveTarget(TimberbornStoredGoodBurnConsequence consequence)
    {
        (int x, int y, int z) = _grid.FromIndex(consequence.CellIndex);
        Vector3Int coordinates = new(x, y, z);
        Stockpile? stockpile = _blockService
            .GetObjectsWithComponentAt<Stockpile>(coordinates)
            .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
            .FirstOrDefault();

        if (stockpile is null)
        {
            return null;
        }

        Inventory? inventory = stockpile.Inventory;
        TimberbornStoredGoodStack[] stacks = inventory is null
            ? Array.Empty<TimberbornStoredGoodStack>()
            : inventory.Stock
                .Select(static good => new TimberbornStoredGoodStack(good.GoodId, good.Amount))
                .ToArray();

        string stableId = $"stockpile:{RuntimeHelpers.GetHashCode(stockpile)}";
        _stockpilesByStableId[stableId] = stockpile;

        return new TimberbornStoredGoodBurnTarget(
            StableId: stableId,
            Stacks: stacks,
            CanMutateInventory: inventory is not null);
    }

    public TimberbornStoredGoodBurnConsequenceResult BurnStoredGoods(
        TimberbornStoredGoodBurnTarget target,
        int burnBudget,
        IReadOnlyList<TimberbornStoredGoodStack> stacksToDestroy)
    {
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        int destroyedItemCount = 0;
        Stockpile? stockpile = FindStockpile(target.StableId);
        Inventory? inventory = stockpile?.Inventory;
        if (inventory is null)
        {
            return new TimberbornStoredGoodBurnConsequenceResult(
                MatchedStorageCell: true,
                AppliedConsequence: false,
                BurnableStackCount: 0,
                DestroyedItemCount: 0,
                HazardousGoodCount: 0,
                SkippedNoInventoryApiCount: stacksToDestroy.Sum(static stack => stack.Amount),
                SkippedUnknownResourceCount: 0,
                SkippedNonBurnableItemCount: 0);
        }

        stacksToDestroy
            .ToList()
            .ForEach(stack =>
            {
                int availableAmount = inventory.AmountInStock(stack.ResourceId);
                int amountToDestroy = Math.Min(availableAmount, stack.Amount);
                if (amountToDestroy > 0)
                {
                    inventory.Take(new GoodAmount(stack.ResourceId, amountToDestroy));
                    destroyedItemCount += amountToDestroy;
                }
            });

        return new TimberbornStoredGoodBurnConsequenceResult(
            MatchedStorageCell: true,
            AppliedConsequence: destroyedItemCount > 0,
            BurnableStackCount: 0,
            DestroyedItemCount: destroyedItemCount,
            HazardousGoodCount: 0,
            SkippedNoInventoryApiCount: 0,
            SkippedUnknownResourceCount: 0,
            SkippedNonBurnableItemCount: 0);
    }

    private Stockpile? FindStockpile(string stableId)
    {
        return _stockpilesByStableId.TryGetValue(stableId, out Stockpile stockpile)
            ? stockpile
            : null;
    }
}
