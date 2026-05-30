using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.GoodStackSystem;
using Timberborn.InventorySystem;
using Timberborn.SimpleOutputBuildings;
using Timberborn.Stockpiles;
using Wildfire.Timberborn.Ash;

namespace Wildfire.Timberborn.Qa;

public sealed class TimberbornQaInventoryAdjustmentApi : ITimberbornQaInventoryAdjuster
{
    private const string ExplosivesGoodId = "Explosives";
    private const string BadwaterGoodId = "Badwater";
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
        QaInventoryTarget[] targets = FindTargets();
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
            ExplosivesAdded: SumAdded(results, ExplosivesGoodId),
            BadwaterAdded: SumAdded(results, BadwaterGoodId),
            FertileAshAdded: SumAdded(results, TimberbornAshFieldService.FertileAshGoodId),
            LogsAdded: SumAdded(results, LogGoodId));
    }

    private QaInventoryTarget[] FindTargets()
    {
        QaInventoryTarget[] stockpiles = TimberbornEntityComponentCells
            .ComponentBlockObjects<Stockpile>(_entityRegistry)
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
            .ComponentBlockObjects<SimpleOutputInventory>(_entityRegistry)
            .Select(static item => new QaInventoryTarget(
                $"simple_output:{RuntimeHelpers.GetHashCode(item.Component)}",
                item.BlockObject,
                item.Component.Inventory))
            .ToArray();
        QaInventoryTarget[] goodStacks = TimberbornEntityComponentCells
            .ComponentBlockObjects<GoodStack>(_entityRegistry)
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

    private static IEnumerable<QaInventoryAdjustment> CreateAdjustments(
        string profile,
        IReadOnlyList<QaInventoryTarget> targets)
    {
        if (profile is TimberbornQaInventoryAdjustmentProfiles.StoredMaterials or
            TimberbornQaInventoryAdjustmentProfiles.AllConsequences)
        {
            yield return new QaInventoryAdjustment(targets[0], ExplosivesGoodId, TargetGoodAmount);
            yield return new QaInventoryAdjustment(targets[Math.Min(1, targets.Count - 1)], BadwaterGoodId, TargetGoodAmount);
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
