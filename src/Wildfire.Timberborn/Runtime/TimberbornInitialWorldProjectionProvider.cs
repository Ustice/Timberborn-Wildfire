using Timberborn.BlockSystem;
using Timberborn.Buildings;
using Timberborn.Cutting;
using Timberborn.EntitySystem;
using Timberborn.Forestry;
using Timberborn.Gathering;
using Timberborn.GoodStackSystem;
using Timberborn.InventorySystem;
using Timberborn.SimpleOutputBuildings;
using Timberborn.Stockpiles;
using Timberborn.Yielding;
using Wildfire.Core;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Runtime;

/// <summary>
/// Capture-only input for the future owned initializer and saved-owner restore. No fire fields, tokens, body damage or native
/// effects are published. Call on the native thread at a settled load boundary without an async gap.
/// </summary>
public sealed class TimberbornInitialWorldProjectionProvider
{
    private readonly EntityRegistry _entities;
    private readonly TimberbornInitialEnvironmentCaptureProvider _environment;
    private readonly INativeResourceMutationGuard _guard;
    public TimberbornInitialWorldProjectionProvider(EntityRegistry entities,
        TimberbornInitialEnvironmentCaptureProvider environment, INativeResourceMutationGuard guard)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
    }

    public TimberbornInitialWorldCapture Capture(FireGrid grid) => _guard.CaptureAtRest(() => CaptureAtRest(grid));

    private TimberbornInitialWorldCapture CaptureAtRest(FireGrid grid)
    {
        var environment = _environment.Capture(grid);
        var entities = _entities.Entities.ToArray(); // Native property is a view of a mutable instantiation list.
        var bodies = new List<TimberbornInitialMaterialBody>();
        var excluded = new List<TimberbornInitialExcludedEntity>();
        var waterSources = new List<TimberbornInitialWaterSource>();
        var validate = new List<Action>();
        foreach (var entity in entities)
        {
            if (!entity.TryGetComponent<BlockObject>(out var block))
            {
                validate.Add(() =>
                {
                    if (entity.TryGetComponent<BlockObject>(out _))
                        throw new InvalidOperationException("Native entity acquired a block during initial capture.");
                });
                continue;
            }
            var id = entity.EntityId;
            if (id == Guid.Empty) throw new InvalidOperationException("Native material has no settled Guid.");
            if (!ReferenceEquals(block.GetComponent<EntityComponent>(), entity))
                throw new InvalidOperationException("Native block references another entity.");
            var reason = Exclusion(entity, block);
            if (reason == TimberbornInitialCaptureExclusion.EnvironmentalWaterSource)
            {
                var waterPlacement = block.Placement;
                var waterBlocks = block.Blocks;
                var source = new TimberbornInitialWaterSource(id, block.Name, TimberbornEntityComponentCells.IsBadwaterSourceName(block.Name),
                    TimberbornNativeMaterialFootprint.Project(waterBlocks, waterPlacement, grid));
                waterSources.Add(source);
                validate.Add(() =>
                {
                    if (!ReferenceEquals(_entities.GetEntity(id), entity) || entity.EntityId != id || block.Name != source.SpecId ||
                        !entity.TryGetComponent<BlockObject>(out var currentBlock) || !ReferenceEquals(currentBlock, block) ||
                        Exclusion(entity, block) != TimberbornInitialCaptureExclusion.EnvironmentalWaterSource ||
                        !block.Placement.Equals(waterPlacement) || !ReferenceEquals(block.Blocks, waterBlocks))
                        throw new InvalidOperationException("Native water source changed during initial capture.");
                });
                continue;
            }
            if (reason is { } omission)
            {
                var excludedName = block.Name;
                excluded.Add(new(id, excludedName, omission));
                validate.Add(() =>
                {
                    if (!ReferenceEquals(_entities.GetEntity(id), entity) || entity.EntityId != id ||
                        !entity.TryGetComponent<BlockObject>(out var currentBlock) || !ReferenceEquals(currentBlock, block) ||
                        block.Name != excludedName || Exclusion(entity, block) != omission)
                        throw new InvalidOperationException("Excluded native body eligibility changed during initial capture.");
                });
                continue;
            }
            var placement = block.Placement;
            var blocks = block.Blocks;
            var body = CaptureBodyFacts(entity, block, grid);
            bodies.Add(body);
            validate.Add(() =>
            {
                if (!ReferenceEquals(_entities.GetEntity(id), entity) || entity.EntityId != id ||
                    !entity.TryGetComponent<BlockObject>(out var currentBlock) || !ReferenceEquals(currentBlock, block) ||
                    Exclusion(entity, block) is not null || !block.Placement.Equals(placement) || !ReferenceEquals(block.Blocks, blocks) ||
                    !SameBodyFacts(body, CaptureBodyFacts(entity, block, grid)))
                    throw new InvalidOperationException("Native body changed during initial capture; no projection was published.");
            });
        }
        _environment.RequireUnchanged(environment);
        foreach (var check in validate) check();
        var current = _entities.Entities;
        if (current.Count != entities.Length || current.Where((entity, index) => !ReferenceEquals(entity, entities[index])).Any())
            throw new InvalidOperationException("Native entity membership changed during initial capture.");
        return new TimberbornInitialWorldCapture(grid, bodies, excluded, waterSources, environment);
    }

    /// <summary>
    /// Called inside the owned restore session's existing CaptureAtRest guard; intentionally does not
    /// enter a nested guard. Reads required saved Guids, including disabled/leftover bodies. It does not
    /// admit fresh material, compare saved capacities, or replay initial-world eligibility filters.
    /// </summary>
    public IReadOnlyList<TimberbornInitialMaterialBody> CaptureRetainedBodies(FireGrid grid, IReadOnlyList<Guid> requiredIds)
    {
        if (requiredIds is null) throw new ArgumentNullException(nameof(requiredIds));
        var ids = requiredIds.OrderBy(id => id).ToArray();
        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Retained capture requires unique nonempty native Guids.");
        _environment.RequireSettled(grid);
        var bodies = new List<TimberbornInitialMaterialBody>();
        var validate = new List<Action>();
        foreach (var id in ids)
        {
            var entity = RequireRetainedEntity(id);
            var block = RequireRetainedBlock(entity);
            var placement = block.Placement;
            var blocks = block.Blocks;
            bool finished = block.IsFinished;
            var body = CaptureBodyFacts(entity, block, grid);
            bodies.Add(body);
            validate.Add(() =>
            {
                if (!ReferenceEquals(RequireRetainedEntity(id), entity) || !ReferenceEquals(RequireRetainedBlock(entity), block) ||
                    !block.Placement.Equals(placement) || !ReferenceEquals(block.Blocks, blocks) || block.IsFinished != finished ||
                    !SameBodyFacts(body, CaptureBodyFacts(entity, block, grid)))
                    throw new InvalidOperationException("Retained native body changed during restore capture.");
            });
        }
        foreach (var check in validate) check();
        _environment.RequireSettled(grid);
        return Array.AsReadOnly(bodies.ToArray());
    }

    private EntityComponent RequireRetainedEntity(Guid id)
    {
        var entity = _entities.GetEntity(id);
        if (entity is null || entity.EntityId != id || entity.Deleted || !entity.Initialized || !entity)
            throw new InvalidOperationException("Required saved native body is missing, deleted or not settled.");
        return entity;
    }

    private static BlockObject RequireRetainedBlock(EntityComponent entity)
    {
        if (!entity.TryGetComponent<BlockObject>(out var block) || !ReferenceEquals(block.GetComponent<EntityComponent>(), entity) ||
            block.IsPreview || !block.Positioned)
            throw new InvalidOperationException("Required saved body lacks its exact positioned native block.");
        return block;
    }

    private static TimberbornInitialMaterialBody CaptureBodyFacts(EntityComponent entity, BlockObject block, FireGrid grid) =>
        new(entity.EntityId, block.Name, Shape(entity, block.Name), TimberbornNativeMaterialFootprint.Project(block.Blocks, block.Placement, grid),
            CaptureYields(entity), CaptureInventories(entity), CaptureConstruction(entity));

    private static bool SameBodyFacts(TimberbornInitialMaterialBody left, TimberbornInitialMaterialBody right) =>
        left.EntityId == right.EntityId && left.SpecId == right.SpecId && left.Shape == right.Shape &&
        left.Footprint.SequenceEqual(right.Footprint) && left.Yields.SequenceEqual(right.Yields) &&
        SameInventories(left.Inventories, right.Inventories) && SameConstruction(left.ConstructionResources, right.ConstructionResources);

    private static IReadOnlyList<TimberbornBurnDamageResourceStack>? CaptureConstruction(EntityComponent entity)
    {
        if (!entity.TryGetComponent<Building>(out var building)) return null;
        if (!ReferenceEquals(building.GetComponent<EntityComponent>(), entity))
            throw new InvalidOperationException("Construction definition belongs to another native entity.");
        return CaptureBuildingCost(building.Spec);
    }

    private static IReadOnlyList<TimberbornBurnDamageResourceStack> CaptureBuildingCost(BuildingSpec spec)
    {
        if (spec is null || spec.BuildingCost.IsDefault)
            throw new InvalidOperationException("Native building has no settled construction definition.");
        return spec.BuildingCost.Select(cost => new TimberbornBurnDamageResourceStack(cost.Id, cost.Amount))
            .OrderBy(cost => cost.ResourceId, StringComparer.Ordinal).ToArray();
    }

    private static bool SameConstruction(IReadOnlyList<TimberbornBurnDamageResourceStack>? left,
        IReadOnlyList<TimberbornBurnDamageResourceStack>? right) =>
        left is null ? right is null : right is not null && left.SequenceEqual(right);

    private static bool SameInventories(IReadOnlyList<TimberbornInventoryMaterial> left, IReadOnlyList<TimberbornInventoryMaterial> right) =>
        left.Count == right.Count && left.Select((inventory, index) => inventory.Role == right[index].Role &&
            inventory.Enabled == right[index].Enabled && inventory.Stock.SequenceEqual(right[index].Stock)).All(equal => equal);

    private static TimberbornInitialCaptureExclusion? Exclusion(EntityComponent entity, BlockObject block)
    {
        if (entity.Deleted || !entity) return TimberbornInitialCaptureExclusion.Deleted;
        if (!entity.Initialized) return TimberbornInitialCaptureExclusion.Uninitialized;
        if (block.IsPreview) return TimberbornInitialCaptureExclusion.Preview;
        if (!block.Positioned) return TimberbornInitialCaptureExclusion.Unpositioned;
        if (!block.IsFinished) return TimberbornInitialCaptureExclusion.Unfinished;
        if (TimberbornEntityComponentCells.IsWaterSourceName(block.Name) || TimberbornEntityComponentCells.IsBadwaterSourceName(block.Name))
            return TimberbornInitialCaptureExclusion.EnvironmentalWaterSource;
        if (TimberbornEntityComponentCells.IsTreeName(block.Name) && !TimberbornEntityComponentCells.IsTreeFuelSource(block))
            return TimberbornInitialCaptureExclusion.TreeLeftover;
        return null;
    }

    private static TimberbornInitialBodyShape Shape(EntityComponent entity, string name)
    {
        if (entity.TryGetComponent<Stockpile>(out _)) return TimberbornInitialBodyShape.Stockpile;
        if (TimberbornEntityComponentCells.IsInfrastructureName(name)) return TimberbornInitialBodyShape.Infrastructure;
        if (entity.TryGetComponent<Building>(out _)) return TimberbornInitialBodyShape.Structure;
        if (entity.TryGetComponent<TreeComponent>(out _) || TimberbornEntityComponentCells.IsTreeName(name)) return TimberbornInitialBodyShape.Tree;
        var profile = TimberbornBurnableCatalog.Default.Lookup(name);
        if (profile.Known && profile.Type == "crop") return TimberbornInitialBodyShape.Crop;
        if (profile.Known && profile.Type == "bush") return TimberbornInitialBodyShape.Vegetation;
        if (entity.TryGetComponent<GoodStack>(out _)) return TimberbornInitialBodyShape.GoodStack;
        return TimberbornInitialBodyShape.Unknown;
    }

    private static IReadOnlyList<TimberbornNamedYieldMaterial> CaptureYields(EntityComponent entity)
    {
        entity.TryGetComponent<Cuttable>(out var cuttable);
        entity.TryGetComponent<Gatherable>(out var gatherable);
        var named = entity.GetComponentsAllocating<Yielder>();
        if (cuttable is not null && !named.Any(yielder => ReferenceEquals(yielder, cuttable.Yielder)) ||
            gatherable is not null && !named.Any(yielder => ReferenceEquals(yielder, gatherable.Yielder)))
            throw new InvalidOperationException("Native yield role points outside its body's named yielders.");
        return named.Select(yielder =>
        {
            if (!ReferenceEquals(yielder.GetComponent<EntityComponent>(), entity))
                throw new InvalidOperationException("Named yield references another native body.");
            var role = ReferenceEquals(cuttable?.Yielder, yielder) ? TimberbornCapturedYieldRole.Cuttable :
                ReferenceEquals(gatherable?.Yielder, yielder) ? TimberbornCapturedYieldRole.Gatherable : TimberbornCapturedYieldRole.Unclassified;
            return CaptureNamedYield(yielder, role, role == TimberbornCapturedYieldRole.Cuttable && cuttable!.RemoveOnCut);
        }).ToArray();
    }

    private static TimberbornNamedYieldMaterial CaptureNamedYield(Yielder yielder, TimberbornCapturedYieldRole role, bool removeOnCut)
    {
        var spec = yielder.YielderSpec ?? throw new InvalidOperationException("Native yielder has no settled definition.");
        if (spec.Yield is null || spec.YielderComponentName != yielder.ComponentName)
            throw new InvalidOperationException("Native yielder name does not match its configured definition.");
        var actual = yielder.Yield;
        return new(role, yielder.ComponentName, actual.GoodId, actual.Amount, spec.Yield.Id, spec.Yield.Amount, removeOnCut, yielder.Enabled);
    }

    private static IReadOnlyList<TimberbornInventoryMaterial> CaptureInventories(EntityComponent entity)
    {
        var captures = new List<TimberbornInventoryMaterial>();
        if (entity.TryGetComponent<Stockpile>(out var stockpile)) Capture(TimberbornCapturedInventoryRole.Stockpile, stockpile.Inventory);
        if (entity.TryGetComponent<SimpleOutputInventory>(out var output)) Capture(TimberbornCapturedInventoryRole.SimpleOutput, output.Inventory);
        if (entity.TryGetComponent<GoodStack>(out var stack)) Capture(TimberbornCapturedInventoryRole.GoodStack, stack.Inventory);
        return captures;

        void Capture(TimberbornCapturedInventoryRole role, Inventory inventory)
        {
            if (inventory is null || !inventory || !ReferenceEquals(inventory.GetComponent<EntityComponent>(), entity))
                throw new InvalidOperationException("Native inventory role lacks its exact live body-owned inventory.");
            // Stock is physical material, including reservations; mutation-time availability is a separate contract.
            var material = CaptureInventoryMaterial(role, inventory);
            if (material is not null) captures.Add(material);
        }
    }
    // A harvestable's dormant empty GoodStack is a capability, not another physical material part.
    private static TimberbornInventoryMaterial? CaptureInventoryMaterial(TimberbornCapturedInventoryRole role, Inventory inventory)
    {
        var material = new TimberbornInventoryMaterial(role, inventory.Enabled,
            inventory.Stock.Select(good => new TimberbornStoredGoodStack(good.GoodId, good.Amount)));
        return role == TimberbornCapturedInventoryRole.GoodStack && !material.Enabled && material.Stock.Count == 0 ? null : material;
    }
}
