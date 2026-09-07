using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.SimpleOutputBuildings;
using Timberborn.Stockpiles;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Consequences;

/// <summary>Legacy WF1 spatial inventory adapter. Owned-material consumers never call this fallback path.</summary>
public sealed class TimberbornStockpileStoredGoodBurnInventoryApi :
    ITimberbornStoredGoodBurnInventoryApi,
    ITimberbornStoredGoodBurnDamageInventoryApi
{
    private readonly FireGrid _grid;
    private readonly IBlockService _blockService;
    private readonly EntityRegistry? _entityRegistry;
    private readonly Dictionary<string, Stockpile> _stockpilesByStableId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Inventory> _inventoriesByStableId = new(StringComparer.Ordinal);

    public TimberbornStockpileStoredGoodBurnInventoryApi(
        FireGrid grid,
        IBlockService blockService,
        EntityRegistry? entityRegistry = null)
    {
        _grid = grid;
        _blockService = blockService ?? throw new ArgumentNullException(nameof(blockService));
        _entityRegistry = entityRegistry;
    }

    public TimberbornStoredGoodBurnTarget? ResolveTarget(TimberbornStoredGoodBurnConsequence consequence)
    {
        (int x, int y, int z) = _grid.FromIndex(consequence.CellIndex);
        Vector3Int coordinates = new(x, y, z);
        Stockpile? stockpile = _blockService
            .GetObjectsWithComponentAt<Stockpile>(coordinates)
            .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
            .FirstOrDefault();
        if (stockpile is not null)
        {
            return CreateTarget(stockpile);
        }

        SimpleOutputInventory? outputInventory = _blockService
            .GetObjectsWithComponentAt<SimpleOutputInventory>(coordinates)
            .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
            .FirstOrDefault();

        return outputInventory is null ? null : CreateTarget(outputInventory);
    }

    public TimberbornStoredGoodBurnTarget? ResolveTarget(TimberbornBurnDamageTargetState state)
    {
        if (state is null ||
            state.TargetKind is not TimberbornBurnDamageTargetKind.Storage and
                not TimberbornBurnDamageTargetKind.Structure)
        {
            return null;
        }

        TimberbornStoredGoodBurnTarget? indexedTarget = ResolveIndexedStockpileTarget(state);
        if (indexedTarget is not null)
        {
            return indexedTarget;
        }

        TimberbornStoredGoodBurnTarget? outputInventoryTarget = ResolveIndexedOutputInventoryTarget(state);
        if (outputInventoryTarget is not null)
        {
            return outputInventoryTarget;
        }

        return state.OwnedCellIndices
            .Select(_grid.FromIndex)
            .Select(coordinates => new Vector3Int(coordinates.X, coordinates.Y, coordinates.Z))
            .SelectMany(coordinates => _blockService.GetObjectsWithComponentAt<Stockpile>(coordinates))
            .GroupBy(static stockpile => RuntimeHelpers.GetHashCode(stockpile))
            .Select(static group => group.First())
            .Select(CreateTarget)
            .FirstOrDefault(target => string.Equals(target.StableId, state.TargetKey.StableId, StringComparison.Ordinal));
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
        Inventory? inventory = FindInventory(target.StableId);
        if (inventory is null)
        {
            throw new InvalidOperationException(
                $"Stored good burn inventory target disappeared before mutation for {target.StableId}.");
        }

        stacksToDestroy
            .ToList()
            .ForEach(stack =>
            {
                int availableAmount = inventory.UnreservedTakeableStock()
                    .Where(goodAmount => string.Equals(goodAmount.GoodId, stack.ResourceId, StringComparison.Ordinal))
                    .Sum(static goodAmount => Math.Max(0, goodAmount.Amount));
                int amountToDestroy = Math.Min(availableAmount, stack.Amount);
                if (amountToDestroy > 0)
                {
                    TimberbornInventoryMutations.Consume(inventory, new GoodAmount(stack.ResourceId, amountToDestroy));
                    destroyedItemCount += amountToDestroy;
                }
            });

        return new TimberbornStoredGoodBurnConsequenceResult(
            MatchedStorageCell: true,
            AppliedConsequence: destroyedItemCount > 0,
            BurnableStackCount: 0,
            DestroyedItemCount: destroyedItemCount,
            HazardousGoodCount: 0,
            ExplosiveGoodCount: 0,
            ExplosiveBlastTriggeredCount: 0,
            ContaminatedGoodCount: 0,
            ContaminationPulseCellCount: 0,
            UnknownResourceCount: 0,
            SkippedNonBurnableItemCount: 0);
    }

    private Stockpile? FindStockpile(string stableId)
    {
        return _stockpilesByStableId.TryGetValue(stableId, out Stockpile stockpile)
            ? stockpile
            : null;
    }

    private Inventory? FindInventory(string stableId)
    {
        return _inventoriesByStableId.TryGetValue(stableId, out Inventory inventory)
            ? inventory
            : FindStockpile(stableId)?.Inventory;
    }

    private TimberbornStoredGoodBurnTarget? ResolveIndexedStockpileTarget(TimberbornBurnDamageTargetState state)
    {
        if (_entityRegistry is null)
        {
            return null;
        }

        TimberbornEntityComponentCells.TimberbornEntityComponentBlockObject<Stockpile>[] stockpiles =
            TimberbornEntityComponentCells.ComponentBlockObjects<Stockpile>(_entityRegistry).ToArray();
        TimberbornStoredGoodBurnTarget? exactTarget = stockpiles
            .Select(static item => item.Component)
            .Select(CreateTarget)
            .FirstOrDefault(target => string.Equals(target.StableId, state.TargetKey.StableId, StringComparison.Ordinal));
        if (exactTarget is not null)
        {
            return exactTarget;
        }

        HashSet<int> ownedCellIndices = state.OwnedCellIndices.ToHashSet();
        return stockpiles
            .Where(item => TimberbornEntityComponentCells.OccupiedCoordinates(item.BlockObject)
                .Where(coordinates => TimberbornEntityComponentCells.IsInsideGrid(coordinates, _grid))
                .Select(coordinates => _grid.ToIndex(coordinates.x, coordinates.y, coordinates.z))
                .Any(ownedCellIndices.Contains))
            .Select(static item => item.Component)
            .Select(CreateTarget)
            .FirstOrDefault();
    }

    private TimberbornStoredGoodBurnTarget? ResolveIndexedOutputInventoryTarget(TimberbornBurnDamageTargetState state)
    {
        if (_entityRegistry is null)
        {
            return null;
        }

        HashSet<int> ownedCellIndices = state.OwnedCellIndices.ToHashSet();
        return TimberbornEntityComponentCells.ComponentBlockObjects<SimpleOutputInventory>(_entityRegistry)
            .Where(item => TimberbornEntityComponentCells.OccupiedCoordinates(item.BlockObject)
                .Where(coordinates => TimberbornEntityComponentCells.IsInsideGrid(coordinates, _grid))
                .Select(coordinates => _grid.ToIndex(coordinates.x, coordinates.y, coordinates.z))
                .Any(ownedCellIndices.Contains))
            .Select(static item => item.Component)
            .Select(CreateTarget)
            .Where(static target => target.Stacks.Count > 0)
            .FirstOrDefault();
    }

    private TimberbornStoredGoodBurnTarget CreateTarget(Stockpile stockpile)
    {
        Inventory? inventory = stockpile.Inventory;
        TimberbornStoredGoodStack[] stacks = inventory is null
            ? Array.Empty<TimberbornStoredGoodStack>()
            : inventory.Stock
                .Select(static good => new TimberbornStoredGoodStack(good.GoodId, good.Amount))
                .ToArray();

        string stableId = $"stockpile:{RuntimeHelpers.GetHashCode(stockpile)}";
        _stockpilesByStableId[stableId] = stockpile;
        if (inventory is not null)
        {
            _inventoriesByStableId[stableId] = inventory;
        }

        return new TimberbornStoredGoodBurnTarget(
            StableId: stableId,
            Stacks: stacks,
            CanMutateInventory: inventory is not null);
    }

    private TimberbornStoredGoodBurnTarget CreateTarget(SimpleOutputInventory outputInventory)
    {
        Inventory inventory = outputInventory.Inventory;
        TimberbornStoredGoodStack[] stacks = inventory.Stock
            .Select(static good => new TimberbornStoredGoodStack(good.GoodId, good.Amount))
            .ToArray();

        string stableId = $"simple_output:{RuntimeHelpers.GetHashCode(outputInventory)}";
        _inventoriesByStableId[stableId] = inventory;

        return new TimberbornStoredGoodBurnTarget(
            StableId: stableId,
            Stacks: stacks,
            CanMutateInventory: true);
    }
}
