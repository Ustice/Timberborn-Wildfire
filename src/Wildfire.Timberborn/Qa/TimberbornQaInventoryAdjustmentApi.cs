using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.GoodStackSystem;
using Timberborn.InventorySystem;
using Timberborn.SimpleOutputBuildings;
using Timberborn.Stockpiles;
using Wildfire.Core;
using Wildfire.Timberborn.Ash;

namespace Wildfire.Timberborn.Qa;

public sealed class TimberbornQaInventoryAdjustmentApi : ITimberbornQaInventoryAdjuster
{
    private const string LogGoodId = "Log";
    private const int TargetGoodAmount = 12;

    private readonly EntityRegistry _entityRegistry;

    public TimberbornQaInventoryAdjustmentApi(EntityRegistry entityRegistry)
    {
        _entityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
    }

    public TimberbornQaInventoryAdjustmentResult AdjustInventory(string profile)
    {
        string normalizedProfile = profile.Trim().ToLowerInvariant();
        QaInventoryTarget[] targets = FindTargets(_entityRegistry);
        if (targets.Length == 0)
        {
            throw new InvalidOperationException("No live Timberborn inventory targets were found for QA adjustment.");
        }

        QaInventoryAdjustment[] adjustments = CreateAdjustments(normalizedProfile, targets).ToArray();
        if (adjustments.Length == 0)
        {
            throw new ArgumentException(
                $"QA inventory adjustment profile must be one of: {string.Join(", ", TimberbornQaInventoryAdjustmentProfiles.All)}.",
                nameof(profile));
        }

        QaInventoryAdjustmentResult[] results = adjustments
            .Select(static adjustment => adjustment.Apply())
            .ToArray();

        return new TimberbornQaInventoryAdjustmentResult(
            normalizedProfile,
            TargetsScanned: targets.Length,
            TargetsAdjusted: results
                .Select(static result => result.TargetKey)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            ExplosivesAdded: SumAdded(results, TimberbornQaStoredMaterialGoodIds.Explosives),
            BadwaterAdded: SumAdded(results, TimberbornQaStoredMaterialGoodIds.Badwater),
            FertileAshAdded: SumAdded(results, TimberbornAshFieldService.FertileAshGoodId),
            LogsAdded: SumAdded(results, LogGoodId));
    }

    private static QaInventoryTarget[] FindTargets(EntityRegistry entityRegistry)
    {
        QaInventoryTarget[] stockpiles = TimberbornEntityComponentCells
            .ComponentBlockObjects<Stockpile>(entityRegistry)
            .Select(static item => item.Component.Inventory is null
                ? null
                : new QaInventoryTarget(
                    $"stockpile:{RuntimeHelpers.GetHashCode(item.Component)}",
                    item.BlockObject,
                    item.Component.Inventory))
            .Where(static target => target is not null)
            .Select(static target => target!)
            .ToArray();
        QaInventoryTarget[] outputInventories = TimberbornEntityComponentCells
            .ComponentBlockObjects<SimpleOutputInventory>(entityRegistry)
            .Select(static item => new QaInventoryTarget(
                $"simple_output:{RuntimeHelpers.GetHashCode(item.Component)}",
                item.BlockObject,
                item.Component.Inventory))
            .ToArray();
        QaInventoryTarget[] goodStacks = TimberbornEntityComponentCells
            .ComponentBlockObjects<GoodStack>(entityRegistry)
            .Select(static item => new QaInventoryTarget(
                $"good_stack:{RuntimeHelpers.GetHashCode(item.Component)}",
                item.BlockObject,
                item.Component.Inventory))
            .ToArray();

        return stockpiles
            .Concat(outputInventories)
            .Concat(goodStacks)
            .GroupBy(static target => RuntimeHelpers.GetHashCode(target.Inventory))
            .Select(static group => group.First())
            .OrderBy(static target => TargetPriority(target.BlockObject))
            .ThenBy(static target => target.TargetKey, StringComparer.Ordinal)
            .ToArray();
    }

    public static TimberbornQaStoredMaterialInventoryTarget[] FindStoredMaterialTargets(
        EntityRegistry entityRegistry,
        FireGrid grid,
        string target)
    {
        if (entityRegistry is null)
        {
            throw new ArgumentNullException(nameof(entityRegistry));
        }

        string normalizedTarget = TimberbornQaStoredMaterialStimulusTargets.Normalize(target);
        string[] goodIds = StoredMaterialGoodIds(normalizedTarget);

        return FindTargets(entityRegistry)
            .SelectMany(inventoryTarget => goodIds
                .Select(goodId => CreateStoredMaterialTarget(inventoryTarget, grid, goodId))
                .Where(static storedTarget => storedTarget is not null)
                .Select(static storedTarget => storedTarget!))
            .OrderBy(static storedTarget => TargetPriority(storedTarget.BlockObject))
            .ThenBy(static storedTarget => storedTarget.TargetKey, StringComparer.Ordinal)
            .ThenBy(static storedTarget => GoodPriority(storedTarget.GoodId))
            .ToArray();
    }

    private static IEnumerable<QaInventoryAdjustment> CreateAdjustments(
        string profile,
        IReadOnlyList<QaInventoryTarget> targets)
    {
        if (profile is TimberbornQaInventoryAdjustmentProfiles.StoredMaterials or
            TimberbornQaInventoryAdjustmentProfiles.AllConsequences)
        {
            yield return new QaInventoryAdjustment(targets[0], TimberbornQaStoredMaterialGoodIds.Explosives, TargetGoodAmount);
            yield return new QaInventoryAdjustment(targets[Math.Min(1, targets.Count - 1)], TimberbornQaStoredMaterialGoodIds.Badwater, TargetGoodAmount);
        }

        if (profile is TimberbornQaInventoryAdjustmentProfiles.PersistenceMatrix or
            TimberbornQaInventoryAdjustmentProfiles.AllConsequences)
        {
            yield return new QaInventoryAdjustment(
                targets[Math.Min(2, targets.Count - 1)],
                TimberbornAshFieldService.FertileAshGoodId,
                TargetGoodAmount);
            yield return new QaInventoryAdjustment(targets[Math.Min(3, targets.Count - 1)], LogGoodId, TargetGoodAmount);
        }
    }

    private static int TargetPriority(BlockObject blockObject)
    {
        string name = blockObject.Name ?? string.Empty;
        return name.Contains("Pile", StringComparison.OrdinalIgnoreCase)
            ? 0
            : name.Contains("Warehouse", StringComparison.OrdinalIgnoreCase)
                ? 1
                : name.Contains("Tank", StringComparison.OrdinalIgnoreCase)
                    ? 2
                    : 3;
    }

    private static TimberbornQaStoredMaterialInventoryTarget? CreateStoredMaterialTarget(
        QaInventoryTarget target,
        FireGrid grid,
        string goodId)
    {
        int stockBefore = Math.Max(0, target.Inventory.AmountInStock(goodId));
        if (stockBefore <= 0)
        {
            return null;
        }

        int[] cellIndices = TimberbornEntityComponentCells.OccupiedCoordinates(target.BlockObject)
            .Where(coordinates => TimberbornEntityComponentCells.IsInsideGrid(coordinates, grid))
            .Select(coordinates => grid.ToIndex(coordinates.x, coordinates.y, coordinates.z))
            .Distinct()
            .OrderBy(static cellIndex => cellIndex)
            .ToArray();

        return cellIndices.Length == 0
            ? null
            : new TimberbornQaStoredMaterialInventoryTarget(
                target.TargetKey,
                target.BlockObject.Name,
                goodId,
                stockBefore,
                cellIndices,
                target.BlockObject);
    }

    private static string[] StoredMaterialGoodIds(string target)
    {
        return target switch
        {
            TimberbornQaStoredMaterialStimulusTargets.Explosive => new[] { TimberbornQaStoredMaterialGoodIds.Explosives },
            TimberbornQaStoredMaterialStimulusTargets.Contaminated => new[] { TimberbornQaStoredMaterialGoodIds.Badwater },
            TimberbornQaStoredMaterialStimulusTargets.AllTargets => new[] { TimberbornQaStoredMaterialGoodIds.Explosives, TimberbornQaStoredMaterialGoodIds.Badwater },
            _ => throw new ArgumentException("Unknown stored-material QA target.", nameof(target)),
        };
    }

    private static int GoodPriority(string goodId)
    {
        return string.Equals(goodId, TimberbornQaStoredMaterialGoodIds.Explosives, StringComparison.Ordinal)
            ? 0
            : 1;
    }

    private static int SumAdded(IReadOnlyList<QaInventoryAdjustmentResult> results, string goodId)
    {
        return results
            .Where(result => string.Equals(result.GoodId, goodId, StringComparison.Ordinal))
            .Sum(static result => result.AddedAmount);
    }

    private sealed record QaInventoryTarget(string TargetKey, BlockObject BlockObject, Inventory Inventory);

    private sealed record QaInventoryAdjustment(QaInventoryTarget Target, string GoodId, int DesiredAmount)
    {
        public QaInventoryAdjustmentResult Apply()
        {
            int currentAmount = Math.Max(0, Target.Inventory.AmountInStock(GoodId));
            int addedAmount = Math.Max(0, DesiredAmount - currentAmount);
            if (addedAmount > 0)
            {
                Target.Inventory.GiveIgnoringCapacity(new GoodAmount(GoodId, addedAmount));
            }

            return new QaInventoryAdjustmentResult(Target.TargetKey, GoodId, addedAmount);
        }
    }

    private sealed record QaInventoryAdjustmentResult(string TargetKey, string GoodId, int AddedAmount);
}

public static class TimberbornQaStoredMaterialGoodIds
{
    public const string Explosives = "Explosives";
    public const string Badwater = "Badwater";
}

public sealed record TimberbornQaStoredMaterialInventoryTarget(
    string TargetKey,
    string TargetSpecId,
    string GoodId,
    int StockBefore,
    IReadOnlyList<int> CellIndices,
    BlockObject BlockObject);
