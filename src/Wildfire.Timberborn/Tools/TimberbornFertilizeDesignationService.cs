using System.Globalization;
using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.GoodStackSystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.Persistence;
using Timberborn.SimpleOutputBuildings;
using Timberborn.SingletonSystem;
using Timberborn.WorldPersistence;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Tools;

public readonly record struct TimberbornFertilizeDesignationSummary(
    int CropDesignationCount,
    int ForestryDesignationCount,
    int LastAppliedCount,
    int LastConsumedGoods,
    int LastTaintedBlockedCount,
    int LastNoInventoryCount,
    int LastSkippedCount);

public sealed class TimberbornFertilizeDesignationService : ILoadableSingleton, ISaveableSingleton, IUpdatableSingleton
{
    public const int StrengthPerGood = TimberbornFertileAshCollectionService.StrengthPerGood;
    private const int ApplicationRangeCells = 24;
    private const float ProcessIntervalSeconds = 1f;

    private readonly EntityRegistry _entityRegistry;
    private readonly TimberbornFireRuntime _fireRuntime;
    private readonly ISingletonLoader _singletonLoader;

    private readonly HashSet<int> _cropDesignations = new();
    private readonly HashSet<int> _forestryDesignations = new();
    private float _nextProcessTime;

    public TimberbornFertilizeDesignationService(
        EntityRegistry entityRegistry,
        TimberbornFireRuntime fireRuntime,
        ISingletonLoader singletonLoader)
    {
        _entityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
        _fireRuntime = fireRuntime ?? throw new ArgumentNullException(nameof(fireRuntime));
        _singletonLoader = singletonLoader ?? throw new ArgumentNullException(nameof(singletonLoader));
    }

    public TimberbornFertilizeDesignationSummary LastSummary { get; private set; }
    public int FertilizeDesignationCount => _cropDesignations
        .Concat(_forestryDesignations)
        .Distinct()
        .Count();

    public void AddFertilizeDesignation(int cellIndex)
    {
        _cropDesignations.Add(cellIndex);
        _forestryDesignations.Remove(cellIndex);
    }

    public void RemoveFertilizeDesignation(int cellIndex)
    {
        _cropDesignations.Remove(cellIndex);
        _forestryDesignations.Remove(cellIndex);
    }

    public bool HasFertilizeDesignation(int cellIndex) =>
        _cropDesignations.Contains(cellIndex) || _forestryDesignations.Contains(cellIndex);

    public void AddCropDesignation(int cellIndex) => _cropDesignations.Add(cellIndex);
    public void RemoveCropDesignation(int cellIndex) => _cropDesignations.Remove(cellIndex);
    public void AddForestryDesignation(int cellIndex) => _forestryDesignations.Add(cellIndex);
    public void RemoveForestryDesignation(int cellIndex) => _forestryDesignations.Remove(cellIndex);
    public bool HasCropDesignation(int cellIndex) => _cropDesignations.Contains(cellIndex);
    public bool HasForestryDesignation(int cellIndex) => _forestryDesignations.Contains(cellIndex);

    public void Load()
    {
        TimberbornQaCommandState state = _fireRuntime.GetState();
        if (!_singletonLoader.TryGetSingleton(
                TimberbornFertilizeDesignationPersistenceKeys.Singleton,
                out IObjectLoader loader))
        {
            return;
        }

        if (loader.Has(TimberbornFertilizeDesignationPersistenceKeys.CropDesignations))
        {
            RestoreDesignations(
                loader.Get(TimberbornFertilizeDesignationPersistenceKeys.CropDesignations),
                _cropDesignations,
                state);
        }

        if (loader.Has(TimberbornFertilizeDesignationPersistenceKeys.ForestryDesignations))
        {
            RestoreDesignations(
                loader.Get(TimberbornFertilizeDesignationPersistenceKeys.ForestryDesignations),
                _forestryDesignations,
                state);
        }

        Debug.Log(
            "wildfire_fertilize_designations_loaded " +
            $"crop_designations={_cropDesignations.Count} " +
            $"forestry_designations={_forestryDesignations.Count}");
    }

    public void Save(ISingletonSaver singletonSaver)
    {
        IObjectSaver saver = singletonSaver.GetSingleton(TimberbornFertilizeDesignationPersistenceKeys.Singleton);
        saver.Set(
            TimberbornFertilizeDesignationPersistenceKeys.CropDesignations,
            SerializeDesignations(_cropDesignations));
        saver.Set(
            TimberbornFertilizeDesignationPersistenceKeys.ForestryDesignations,
            SerializeDesignations(_forestryDesignations));
    }

    public void UpdateSingleton()
    {
        if (Time.time < _nextProcessTime)
        {
            return;
        }

        _nextProcessTime = Time.time + ProcessIntervalSeconds;

        TimberbornQaCommandState state = _fireRuntime.GetState();
        if (!state.IsSimulatorIntegrated || state.Width is null || state.Height is null || state.Depth is null)
        {
            return;
        }

        FireGrid grid = new(state.Width.Value, state.Height.Value, state.Depth.Value);
        InventoryTarget[] inventories = CollectFertileAshInventories();

        int applied = 0;
        int consumed = 0;
        int taintedBlocked = 0;
        int noInventory = 0;
        int skipped = 0;

        ProcessDesignations(
            _cropDesignations.Concat(_forestryDesignations).Distinct(),
            grid,
            inventories,
            ref applied,
            ref consumed,
            ref taintedBlocked,
            ref noInventory,
            ref skipped);

        LastSummary = new TimberbornFertilizeDesignationSummary(
            CropDesignationCount: _cropDesignations.Count,
            ForestryDesignationCount: _forestryDesignations.Count,
            LastAppliedCount: applied,
            LastConsumedGoods: consumed,
            LastTaintedBlockedCount: taintedBlocked,
            LastNoInventoryCount: noInventory,
            LastSkippedCount: skipped);

        if (applied > 0 || taintedBlocked > 0)
        {
            Debug.Log(
                "wildfire_fertilize_designations_processed " +
                $"crop_designations={_cropDesignations.Count} " +
                $"forestry_designations={_forestryDesignations.Count} " +
                $"applied={applied} " +
                $"consumed_goods={consumed} " +
                $"tainted_blocked={taintedBlocked} " +
                $"no_inventory={noInventory} " +
                $"skipped={skipped}");
        }
    }

    private void ProcessDesignations(
        IEnumerable<int> designations,
        FireGrid grid,
        InventoryTarget[] inventories,
        ref int applied,
        ref int consumed,
        ref int taintedBlocked,
        ref int noInventory,
        ref int skipped)
    {
        foreach (int cellIndex in designations)
        {
            if (cellIndex < 0 || cellIndex >= grid.CellCount)
            {
                skipped++;
                continue;
            }

            if (!_fireRuntime.TryResolveFertileAshApplicationCell(cellIndex, out int applicationCellIndex))
            {
                skipped++;
                continue;
            }

            if (_fireRuntime.IsCellTaintedAsh(applicationCellIndex))
            {
                taintedBlocked++;
                continue;
            }

            (int cx, int cy, int cz) = grid.FromIndex(applicationCellIndex);
            InventoryTarget? source = FindNearbyInventoryWithFertileAsh(inventories, cx, cy, cz);
            if (source is null)
            {
                noInventory++;
                continue;
            }

            try
            {
                source.Value.Inventory.Take(new GoodAmount(TimberbornAshFieldService.FertileAshGoodId, 1));
                _fireRuntime.ApplyPlayerFertileAshDesignation(applicationCellIndex, StrengthPerGood);
                applied++;
                consumed++;
            }
            catch (Exception exception)
            {
                skipped++;
                Debug.LogWarning(
                    "wildfire_fertilize_designation_apply_failed " +
                    $"cell_index={cellIndex} " +
                    $"message={TimberbornQaCommandBridge.FormatToken(exception.GetType().Name)}");
            }
        }
    }

    private InventoryTarget[] CollectFertileAshInventories()
    {
        return _entityRegistry.Entities
            .Select(CreateInventoryTargetSafely)
            .Where(static t => t.HasValue)
            .Select(static t => t!.Value)
            .ToArray();
    }

    private static InventoryTarget? CreateInventoryTarget(EntityComponent entity)
    {
        Inventory? inventory = null;
        int stableId = 0;
        if (entity.TryGetComponent(out GoodStack goodStack))
        {
            inventory = goodStack.Inventory;
            stableId = RuntimeHelpers.GetHashCode(goodStack);
        }
        else if (entity.TryGetComponent(out SimpleOutputInventory simpleOutputInventory))
        {
            inventory = simpleOutputInventory.Inventory;
            stableId = RuntimeHelpers.GetHashCode(simpleOutputInventory);
        }

        if (inventory is null)
        {
            return null;
        }

        if (!HasUnreservedFertileAsh(inventory))
        {
            return null;
        }

        if (!entity.TryGetComponent(out BlockObject blockObject))
        {
            return null;
        }

        Vector3Int[] coords = blockObject.PositionedBlocks.GetOccupiedCoordinates().ToArray();
        if (coords.Length == 0)
        {
            return null;
        }

        Vector3Int center = coords
            .OrderBy(static c => c.x)
            .ThenBy(static c => c.y)
            .ThenBy(static c => c.z)
            .Skip(coords.Length / 2)
            .First();

        return new InventoryTarget(
            stableId,
            center,
            inventory);
    }

    private static InventoryTarget? CreateInventoryTargetSafely(EntityComponent entity)
    {
        try
        {
            return CreateInventoryTarget(entity);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "wildfire_fertilize_inventory_scan_skipped " +
                $"reason={TimberbornQaCommandBridge.FormatToken(exception.GetType().Name)}");
            return null;
        }
    }

    private static InventoryTarget? FindNearbyInventoryWithFertileAsh(
        InventoryTarget[] inventories,
        int cx,
        int cy,
        int cz)
    {
        return inventories
            .Where(inv =>
            {
                int dx = inv.Center.x - cx;
                int dy = inv.Center.y - cy;
                int dz = inv.Center.z - cz;
                return (dx * dx) + (dy * dy) + (dz * dz) <=
                    ApplicationRangeCells * ApplicationRangeCells;
            })
            .Where(inv => inv.Inventory.Stock
                .Any(static ga => ga.GoodId == TimberbornAshFieldService.FertileAshGoodId && ga.Amount > 0) &&
                HasUnreservedFertileAsh(inv.Inventory))
            .OrderBy(inv =>
            {
                int dx = inv.Center.x - cx;
                int dy = inv.Center.y - cy;
                int dz = inv.Center.z - cz;
                return (dx * dx) + (dy * dy) + (dz * dz);
            })
            .Select(static inv => (InventoryTarget?)inv)
            .FirstOrDefault();
    }

    private static bool HasUnreservedFertileAsh(Inventory inventory)
    {
        return inventory.HasUnreservedStock(new GoodAmount(TimberbornAshFieldService.FertileAshGoodId, 1));
    }

    private static string SerializeDesignations(HashSet<int> designations)
    {
        return string.Join(",", designations.OrderBy(static i => i)
            .Select(static i => i.ToString(CultureInfo.InvariantCulture)));
    }

    private static void RestoreDesignations(
        string serialized,
        HashSet<int> target,
        TimberbornQaCommandState state)
    {
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return;
        }

        int? maxCellIndex = state.IsSimulatorIntegrated && state.Width.HasValue && state.Height.HasValue && state.Depth.HasValue
            ? state.Width.Value * state.Height.Value * state.Depth.Value - 1
            : null;

        foreach (string part in serialized.Split(','))
        {
            if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cellIndex) &&
                cellIndex >= 0 &&
                (maxCellIndex is null || cellIndex <= maxCellIndex.Value))
            {
                target.Add(cellIndex);
            }
        }
    }

    private readonly record struct InventoryTarget(int StableId, Vector3Int Center, Inventory Inventory);
}

public static class TimberbornFertilizeDesignationPersistenceKeys
{
    public static readonly SingletonKey Singleton = new("WildfireFertilizeDesignations");
    public static readonly PropertyKey<string> CropDesignations = new("CropDesignations");
    public static readonly PropertyKey<string> ForestryDesignations = new("ForestryDesignations");
}
