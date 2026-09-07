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

namespace Wildfire.Timberborn.Runtime;

/// <summary>
/// Capture-only input for the future owned initializer. No fire fields, tokens, body damage or native
/// effects are published. Call on the native thread at a settled load boundary without an async gap.
/// </summary>
public sealed class TimberbornInitialWorldProjectionProvider
{
    private readonly EntityRegistry _entities;
    public TimberbornInitialWorldProjectionProvider(EntityRegistry entities) =>
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));

    public TimberbornInitialWorldCapture Capture(FireGrid grid)
    {
        var entities = _entities.Entities.ToArray(); // Native property is a view of a mutable instantiation list.
        var bodies = new List<TimberbornInitialMaterialBody>();
        var excluded = new List<TimberbornInitialExcludedEntity>();
        var waterSources = new List<TimberbornInitialWaterSource>();
        var validate = new List<Action>();
        foreach (var entity in entities)
        {
            if (!entity.TryGetComponent<BlockObject>(out var block)) continue;
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
                        Exclusion(entity, block) != TimberbornInitialCaptureExclusion.EnvironmentalWaterSource ||
                        !block.Placement.Equals(waterPlacement) || !ReferenceEquals(block.Blocks, waterBlocks))
                        throw new InvalidOperationException("Native water source changed during initial capture.");
                });
                continue;
            }
            if (reason is { } omission) { excluded.Add(new(id, block.Name, omission)); continue; }
            var placement = block.Placement;
            var blocks = block.Blocks;
            var footprint = TimberbornNativeMaterialFootprint.Project(blocks, placement, grid);
            var yields = CaptureYields(entity);
            var inventories = CaptureInventories(entity);
            var body = new TimberbornInitialMaterialBody(id, block.Name, Shape(entity, block.Name), footprint, yields, inventories);
            bodies.Add(body);
            validate.Add(() =>
            {
                if (!ReferenceEquals(_entities.GetEntity(id), entity) || entity.EntityId != id ||
                    Exclusion(entity, block) is not null || !block.Placement.Equals(placement) || !ReferenceEquals(block.Blocks, blocks) ||
                    block.Name != body.SpecId || Shape(entity, block.Name) != body.Shape || !CaptureYields(entity).SequenceEqual(yields) ||
                    !SameInventories(inventories, CaptureInventories(entity)))
                    throw new InvalidOperationException("Native body changed during initial capture; no projection was published.");
            });
        }
        foreach (var check in validate) check();
        var current = _entities.Entities;
        if (current.Count != entities.Length || current.Where((entity, index) => !ReferenceEquals(entity, entities[index])).Any())
            throw new InvalidOperationException("Native entity membership changed during initial capture.");
        return new TimberbornInitialWorldCapture(grid, bodies, excluded, waterSources);
    }

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
