using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.Cutting;
using Timberborn.EntitySystem;
using Timberborn.Stockpiles;
using Timberborn.Yielding;
using Wildfire.Core;

namespace Wildfire.Timberborn.Consequences;

public sealed record TimberbornLiveBurnDamageTargets(
    TimberbornBurnDamageDescriptorCatalog DescriptorCatalog,
    IReadOnlyList<TimberbornBurnDamageTargetRegistration> Registrations);

public static class TimberbornBurnDamageResourceGuesses
{
    public static IReadOnlyList<TimberbornBurnDamageResourceStack> ForStructure(string name)
    {
        if (name.Contains("Metal", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { new TimberbornBurnDamageResourceStack("MetalBlock", 1) };
        }

        if (name.Contains("Tank", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new TimberbornBurnDamageResourceStack("Log", 1),
                new TimberbornBurnDamageResourceStack("Plank", 1),
            };
        }

        if (name.Contains("Warehouse", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Storage", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Pile", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Lumber", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Forester", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Gatherer", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("District", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Pump", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Building", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new TimberbornBurnDamageResourceStack("Log", 2),
                new TimberbornBurnDamageResourceStack("Plank", 2),
            };
        }

        return Array.Empty<TimberbornBurnDamageResourceStack>();
    }

    public static IReadOnlyList<TimberbornBurnDamageResourceStack> ForPathInfrastructure(string name)
    {
        return name.Contains("Path", StringComparison.OrdinalIgnoreCase)
            ? Array.Empty<TimberbornBurnDamageResourceStack>()
            : ForStructure(name);
    }

    public static IReadOnlyList<TimberbornBurnDamageResourceStack> ForPowerInfrastructure(string name)
    {
        if (name.Contains("Metal", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { new TimberbornBurnDamageResourceStack("MetalBlock", 2) };
        }

        return name.Contains("Shaft", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Power", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Wheel", StringComparison.OrdinalIgnoreCase)
                ? new[]
                {
                    new TimberbornBurnDamageResourceStack("Log", 2),
                    new TimberbornBurnDamageResourceStack("Plank", 1),
                }
                : ForStructure(name);
    }

    public static IReadOnlyList<TimberbornBurnDamageResourceStack> ForWaterInfrastructure(string name)
    {
        if (name.Contains("Dirt", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Levee", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { new TimberbornBurnDamageResourceStack("Dirt", 4) };
        }

        if (name.Contains("Floodgate", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Sluice", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Dam", StringComparison.OrdinalIgnoreCase))
        {
            return new[]
            {
                new TimberbornBurnDamageResourceStack("Log", 1),
                new TimberbornBurnDamageResourceStack("Plank", 1),
                new TimberbornBurnDamageResourceStack("Water", 5),
            };
        }

        return ForStructure(name);
    }
}

public static class TimberbornLiveBurnDamageTargetCollector
{
    private static readonly TimberbornBurnableCatalog BurnableCatalog = TimberbornBurnableCatalog.Default;

    public static TimberbornLiveBurnDamageTargets Collect(EntityRegistry entityRegistry, FireGrid grid)
    {
        if (entityRegistry is null)
        {
            throw new ArgumentNullException(nameof(entityRegistry));
        }

        TargetBuildResult[] targets = CollectTargets(entityRegistry, grid).ToArray();
        TimberbornBurnDamageDescriptor[] descriptors = targets
            .GroupBy(static target => target.Descriptor.SpecId, StringComparer.Ordinal)
            .Select(static group => group.First().Descriptor)
            .ToArray();

        return new TimberbornLiveBurnDamageTargets(
            new TimberbornBurnDamageDescriptorCatalog(descriptors),
            targets.Select(static target => target.Registration).ToArray());
    }

    private static IEnumerable<TargetBuildResult> CollectTargets(EntityRegistry entityRegistry, FireGrid grid)
    {
        IEnumerable<TargetBuildResult> storage = TimberbornEntityComponentCells.ComponentBlockObjects<Stockpile>(entityRegistry)
            .Select((item, index) => BuildTarget(
                $"stockpile:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item.Component)}",
                item.BlockObject.Name,
                TimberbornBurnDamageTargetKind.Storage,
                TimberbornBurnMaterialKind.StoredGood,
                TimberbornEntityComponentCells.OccupiedCoordinates(item.BlockObject),
                grid,
                TimberbornBurnDamageResourceGuesses.ForStructure(item.BlockObject.Name),
                ownershipPriority: 80));

        IEnumerable<TargetBuildResult> structures = TimberbornEntityComponentCells.ComponentBlockObjects<Building>(entityRegistry)
            .Where(static item => !TimberbornEntityComponentCells.IsInfrastructureName(item.BlockObject.Name))
            .Where(static item => !item.Component.TryGetComponent(out Stockpile _))
            .Select(item => BuildTarget(
                $"structure:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item.BlockObject)}",
                item.BlockObject.Name,
                TimberbornBurnDamageTargetKind.Structure,
                TimberbornBurnMaterialKind.Constructed,
                TimberbornEntityComponentCells.OccupiedCoordinates(item.BlockObject),
                grid,
                TimberbornBurnDamageResourceGuesses.ForStructure(item.BlockObject.Name),
                ownershipPriority: 50));

        IEnumerable<TargetBuildResult> infrastructure = TimberbornEntityComponentCells.BlockObjects(entityRegistry)
            .Where(static blockObject => TimberbornEntityComponentCells.IsInfrastructureName(blockObject.Name))
            .Select(blockObject => BuildTarget(
                StableInfrastructureId(blockObject),
                blockObject.Name,
                TimberbornBurnDamageTargetKind.Infrastructure,
                TimberbornBurnMaterialKind.Constructed,
                TimberbornEntityComponentCells.OccupiedCoordinates(blockObject),
                grid,
                InfrastructureResources(blockObject.Name),
                ownershipPriority: 40));

        IEnumerable<TargetBuildResult> trees = CollectTreeTargets(entityRegistry, grid);

        return trees.Concat(storage).Concat(structures).Concat(infrastructure)
            .Where(static target => target.Registration.OwnedCells.Count > 0);
    }

    private static IEnumerable<TargetBuildResult> CollectTreeTargets(EntityRegistry entityRegistry, FireGrid grid)
    {
        TimberbornEntityComponentCells.TimberbornEntityComponentBlockObject<Cuttable>[] cuttableTrees =
            TimberbornEntityComponentCells.ComponentBlockObjects<Cuttable>(entityRegistry)
                .Where(static item => TimberbornEntityComponentCells.IsTreeName(item.BlockObject.Name))
                .ToArray();
        HashSet<int> cuttableBlockHashes = cuttableTrees
            .Select(static item => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item.BlockObject))
            .ToHashSet();
        IEnumerable<TargetBuildResult> cuttableTargets = cuttableTrees
            .Select(item => BuildResourceYieldTarget(
                $"tree_cuttable:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item.BlockObject)}",
                item.BlockObject.Name,
                TimberbornBurnDamageTargetKind.Tree,
                TimberbornBurnMaterialKind.Wood,
                TimberbornEntityComponentCells.OccupiedCoordinates(item.BlockObject),
                grid,
                YieldResources(item.Component.Yielder),
                ownershipPriority: 90));
        IEnumerable<TargetBuildResult> fallbackTargets = TimberbornEntityComponentCells.BlockObjects(entityRegistry)
            .Where(static blockObject => TimberbornEntityComponentCells.IsTreeName(blockObject.Name))
            .Where(blockObject => !cuttableBlockHashes.Contains(
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(blockObject)))
            .Select(blockObject => BuildResourceYieldTarget(
                $"tree_cuttable:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(blockObject)}",
                blockObject.Name,
                TimberbornBurnDamageTargetKind.Tree,
                TimberbornBurnMaterialKind.Wood,
                TimberbornEntityComponentCells.OccupiedCoordinates(blockObject),
                grid,
                new[] { new TimberbornBurnDamageResourceStack("Log", 1) },
                ownershipPriority: 90));

        return cuttableTargets.Concat(fallbackTargets);
    }

    private static TargetBuildResult BuildTarget(
        string stableId,
        string specId,
        TimberbornBurnDamageTargetKind targetKind,
        TimberbornBurnMaterialKind materialKind,
        IEnumerable<UnityEngine.Vector3Int> coordinates,
        FireGrid grid,
        IReadOnlyList<TimberbornBurnDamageResourceStack> constructionResources,
        int ownershipPriority)
    {
        TimberbornCellCoordinates[] ownedCells = coordinates
            .Where(coordinates => TimberbornEntityComponentCells.IsInsideGrid(coordinates, grid))
            .Select(static coordinates => new TimberbornCellCoordinates(coordinates.x, coordinates.y, coordinates.z))
            .Distinct()
            .ToArray();
        TimberbornBurnableProfile burnableProfile = BurnableCatalog.Lookup(specId);
        TimberbornBurnMaterialKind resolvedMaterialKind = burnableProfile.Known && !burnableProfile.IsBurnable
            ? TimberbornBurnMaterialKind.NonBurnable
            : constructionResources.Count == 0 ? TimberbornBurnMaterialKind.NonBurnable : materialKind;
        TimberbornBurnDamageDescriptor descriptor = new(
            specId,
            targetKind,
            resolvedMaterialKind,
            constructionResources: constructionResources,
            burnableProfile: burnableProfile.Known ? burnableProfile : null);
        TimberbornBurnDamageTargetRegistration registration = new(
            new TimberbornBurnDamageTargetKey(stableId),
            specId,
            ownedCells,
            ownershipPriority);

        return new TargetBuildResult(descriptor, registration);
    }

    private static TargetBuildResult BuildResourceYieldTarget(
        string stableId,
        string specId,
        TimberbornBurnDamageTargetKind targetKind,
        TimberbornBurnMaterialKind materialKind,
        IEnumerable<UnityEngine.Vector3Int> coordinates,
        FireGrid grid,
        IReadOnlyList<TimberbornBurnDamageResourceStack> resourceYields,
        int ownershipPriority)
    {
        TimberbornCellCoordinates[] ownedCells = coordinates
            .Where(coordinates => TimberbornEntityComponentCells.IsInsideGrid(coordinates, grid))
            .Select(static coordinates => new TimberbornCellCoordinates(coordinates.x, coordinates.y, coordinates.z))
            .Distinct()
            .ToArray();
        TimberbornBurnableProfile burnableProfile = BurnableCatalog.Lookup(specId);
        TimberbornBurnMaterialKind resolvedMaterialKind = burnableProfile.Known && !burnableProfile.IsBurnable
            ? TimberbornBurnMaterialKind.NonBurnable
            : resourceYields.Count == 0 ? TimberbornBurnMaterialKind.NonBurnable : materialKind;
        TimberbornBurnDamageDescriptor descriptor = new(
            specId,
            targetKind,
            resolvedMaterialKind,
            resourceYields: resourceYields,
            burnableProfile: burnableProfile.Known ? burnableProfile : null);
        TimberbornBurnDamageTargetRegistration registration = new(
            new TimberbornBurnDamageTargetKey(stableId),
            specId,
            ownedCells,
            ownershipPriority,
            descriptor);

        return new TargetBuildResult(descriptor, registration);
    }

    private static IReadOnlyList<TimberbornBurnDamageResourceStack> YieldResources(Yielder yielder)
    {
        string resourceId = SelectResourceId(yielder);
        int amount = SelectYieldAmount(yielder);

        return string.IsNullOrWhiteSpace(resourceId) || amount <= 0
            ? new[] { new TimberbornBurnDamageResourceStack("UnknownCuttableResource", 1) }
            : new[] { new TimberbornBurnDamageResourceStack(resourceId, amount) };
    }

    private static string SelectResourceId(Yielder yielder)
    {
        string liveResourceId = yielder.Yield.GoodId;
        if (!string.IsNullOrWhiteSpace(liveResourceId))
        {
            return liveResourceId;
        }

        return yielder.YielderSpec?.Yield?.Id ?? "";
    }

    private static int SelectYieldAmount(Yielder yielder)
    {
        int liveAmount = yielder.Yield.Amount;
        if (liveAmount > 0)
        {
            return liveAmount;
        }

        return Math.Max(0, yielder.YielderSpec?.Yield?.Amount ?? 0);
    }

    private static string StableInfrastructureId(BlockObject blockObject)
    {
        int hash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(blockObject);
        string name = blockObject.Name;
        if (IsPowerInfrastructureName(name))
        {
            return $"power_infrastructure:{hash}";
        }

        if (IsWaterInfrastructureName(name))
        {
            return $"water_infrastructure:{hash}";
        }

        return $"path_infrastructure:{hash}";
    }

    private static IReadOnlyList<TimberbornBurnDamageResourceStack> InfrastructureResources(string name)
    {
        if (IsPowerInfrastructureName(name))
        {
            return TimberbornBurnDamageResourceGuesses.ForPowerInfrastructure(name);
        }

        if (IsWaterInfrastructureName(name))
        {
            return TimberbornBurnDamageResourceGuesses.ForWaterInfrastructure(name);
        }

        return TimberbornBurnDamageResourceGuesses.ForPathInfrastructure(name);
    }

    private static bool IsPowerInfrastructureName(string name)
    {
        return TimberbornInfrastructureNameClassifier.IsPowerInfrastructureName(name);
    }

    private static bool IsWaterInfrastructureName(string name)
    {
        return TimberbornInfrastructureNameClassifier.IsWaterInfrastructureName(name);
    }

    private readonly record struct TargetBuildResult(
        TimberbornBurnDamageDescriptor Descriptor,
        TimberbornBurnDamageTargetRegistration Registration);
}
