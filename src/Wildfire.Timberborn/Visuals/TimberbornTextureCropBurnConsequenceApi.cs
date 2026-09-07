using System.Reflection;
using Timberborn.BlockSystem;
using Timberborn.Cutting;
using Timberborn.EntitySystem;
using Timberborn.Gathering;
using Timberborn.GoodStackSystem;
using Timberborn.Goods;
using Timberborn.NaturalResourcesLifecycle;
using Timberborn.NaturalResourcesModelSystem;
using Timberborn.NaturalResourcesMoisture;
using Timberborn.Yielding;
using UnityEngine;

namespace Wildfire.Timberborn.Visuals;

/// <summary>Exact native crop actions. Calls that mutate resources require the shared outer resource guard.</summary>
public sealed class TimberbornTextureCropBurnConsequenceApi : ITimberbornLiveCropBurnConsequenceApi
{
    private readonly EntityRegistry _entities;
    private readonly EntityService _entityService;
    private readonly TimberbornRuntimeBurnedTextureDeriver _textureDeriver;
    private readonly ITimberbornFireLogSink _logSink;

    public TimberbornTextureCropBurnConsequenceApi(EntityRegistry entities, EntityService entityService,
        ITimberbornFireLogSink? logSink = null, TimberbornRuntimeBurnedTextureDeriver? textureDeriver = null)
    {
        _entities = entities ?? throw new ArgumentNullException(nameof(entities));
        _entityService = entityService ?? throw new ArgumentNullException(nameof(entityService));
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
        _textureDeriver = textureDeriver ?? new TimberbornRuntimeBurnedTextureDeriver(_logSink);
    }

    public bool IsLive(Guid id) => TryResolveCrop(id, out _);

    public TimberbornCropBurnConsequenceResult ApplyConsequence(TimberbornCropBurnConsequence consequence)
    {
        bool canonical = TimberbornBurnDamageIdentity.TryGetEntity(consequence.TargetKey.StableId,
            NativeBurnTargetFamily.Crop, out Guid id);
        bool selected = !canonical && TimberbornBurnDamageIdentity.TryGetEntity(consequence.TargetKey.StableId,
            NativeBurnTargetFamily.SelectedCrop, out id);
        if ((!canonical && !selected) || consequence.EntityId == Guid.Empty || id != consequence.EntityId)
            throw new InvalidOperationException("Native crop consequence requires an exact Guid crop family binding.");
        if (!TryResolveCrop(id, out var crop)) return new(TimberbornCropBurnConsequenceStatus.NotLive);
        return consequence.Kind switch
        {
            TimberbornCropBurnConsequenceKind.ReduceYield => new(TimberbornCropBurnConsequenceStatus.Unavailable),
            TimberbornCropBurnConsequenceKind.DryCrop => Dry(crop),
            TimberbornCropBurnConsequenceKind.KillCrop => Kill(crop),
            TimberbornCropBurnConsequenceKind.MarkBurnedVisual => BurnVisual(crop),
            TimberbornCropBurnConsequenceKind.MarkBurnedLeftover => BurnWhole(consequence, crop),
            _ => throw new ArgumentOutOfRangeException(nameof(consequence)),
        };
    }

    private bool TryResolveCrop(Guid id, out BlockObject crop)
    {
        crop = null!;
        var entity = _entities.GetEntity(id);
        if (entity is null || entity.Deleted || !entity.Initialized || !entity) return false;
        if (entity.EntityId != id) throw new InvalidOperationException("Native crop registry returned a different identity.");
        if (!entity.TryGetComponent(out crop) || !IsCropOrHarvestableName(crop.Name))
            throw new InvalidOperationException("Native crop origin has a different component family.");
        return true;
    }

    private static TimberbornCropBurnConsequenceResult Dry(BlockObject crop)
    {
        if (!crop.TryGetComponent(out LivingNaturalResource living) ||
            !crop.TryGetComponent(out WateredNaturalResource watered)) return new(TimberbornCropBurnConsequenceStatus.Unavailable);
        if (living.IsDead || watered.DyingProgress.IsDying) return new(TimberbornCropBurnConsequenceStatus.AlreadySatisfied);
        Invoke(watered, "StartDryingOut");
        return new(TimberbornCropBurnConsequenceStatus.Applied);
    }

    private static TimberbornCropBurnConsequenceResult Kill(BlockObject crop)
    {
        if (!crop.TryGetComponent(out LivingNaturalResource living)) return new(TimberbornCropBurnConsequenceStatus.Unavailable);
        return ApplyNativeDeath(living);
    }

    internal static TimberbornCropBurnConsequenceResult ApplyNativeDeath(LivingNaturalResource living)
    {
        if (living.IsDead) return new(TimberbornCropBurnConsequenceStatus.AlreadySatisfied);
        living.Die();
        return new(TimberbornCropBurnConsequenceStatus.Applied, KilledCrop: true);
    }

    private TimberbornCropBurnConsequenceResult BurnVisual(BlockObject crop)
    {
        int changed = ApplyBurnedTextures(crop, crop.Name);
        if (changed > 0) return new(TimberbornCropBurnConsequenceStatus.Applied, VisualStateUpdated: true);
        bool alreadyBurned = crop.Transform.GetComponentsInChildren<Renderer>(includeInactive: true)
            .SelectMany(renderer => renderer.sharedMaterials).Any(material => material is not null && IsBurnedMaterial(material));
        return new(alreadyBurned ? TimberbornCropBurnConsequenceStatus.AlreadySatisfied : TimberbornCropBurnConsequenceStatus.Unavailable);
    }

    private TimberbornCropBurnConsequenceResult BurnWhole(TimberbornCropBurnConsequence consequence, BlockObject crop)
    {
        // Whole-yield clearing is a different native API from harvest-triggering DecreaseYield.
        // Preflight every required capability before the first mutation; unavailable is not partial success.
        bool hasYield = TryGetYielder(crop, out var yielder);
        bool hasLiving = crop.TryGetComponent(out LivingNaturalResource living);
        bool hasGrower = crop.TryGetComponent(out GatherableYieldGrower grower);
        bool delete = consequence.TargetKind == TimberbornBurnDamageTargetKind.Resource;
        if ((!hasLiving && !hasGrower) || (!delete && !crop.TryGetComponent(out NaturalResourceModel _)))
            return new(TimberbornCropBurnConsequenceStatus.Unavailable);
        bool hasStack = crop.TryGetComponent(out GoodStack stack);
        if (hasStack && !CanClearStack(stack)) return new(TimberbornCropBurnConsequenceStatus.Unavailable);

        int yieldLost = hasYield ? RemoveWholeYield(yielder) : 0;
        RequireOrigin(consequence.EntityId, crop);
        if (hasGrower)
        {
            RequireComponent(consequence.EntityId, crop, grower);
            Invoke(grower, "RemoveYield");
        }
        int destroyed = hasStack ? ClearStack(consequence.EntityId, crop, stack) : 0;
        RequireOrigin(consequence.EntityId, crop);
        bool killed = false;
        if (hasLiving)
        {
            RequireComponent(consequence.EntityId, crop, living);
            killed = ApplyNativeDeath(living).KilledCrop;
        }
        RequireOrigin(consequence.EntityId, crop);
        if (delete)
        {
            _entityService.Delete(crop);
            return new(TimberbornCropBurnConsequenceStatus.Applied, yieldLost, killed, Deleted: true, DestroyedGoodCount: destroyed);
        }
        var model = crop.GetComponent<NaturalResourceModel>();
        RequireComponent(consequence.EntityId, crop, model);
        Invoke(model, "ShowCurrentModel");
        RequireOrigin(consequence.EntityId, crop);
        var visual = BurnVisual(crop);
        if (!visual.Satisfied)
            throw new InvalidOperationException("Crop whole-burn mutated native state but its visual stage is unavailable; completion is unknown.");
        return new(TimberbornCropBurnConsequenceStatus.Applied, yieldLost, killed, visual.VisualStateUpdated,
            DestroyedGoodCount: destroyed);
    }

    internal static int RemoveWholeYield(Yielder yielder)
    {
        int before = Math.Max(0, yielder.Yield.Amount);
        if (before == 0) return 0;
        yielder.RemoveRemainingYield();
        if (yielder.Yield.Amount != 0) throw new InvalidOperationException("Native whole-yield removal did not complete.");
        return before;
    }

    private static bool CanClearStack(GoodStack stack)
    {
        var available = stack.Inventory.UnreservedTakeableStock().ToDictionary(item => item.GoodId, item => item.Amount, StringComparer.Ordinal);
        return stack.Inventory.Stock.All(item => item.Amount == 0 || (available.TryGetValue(item.GoodId, out int amount) && amount == item.Amount));
    }

    private int ClearStack(Guid id, BlockObject crop, GoodStack stack)
    {
        RequireComponent(id, crop, stack);
        int destroyed = 0;
        foreach (var item in stack.Inventory.UnreservedTakeableStock().ToArray())
        {
            RequireComponent(id, crop, stack);
            TimberbornInventoryMutations.Consume(stack.Inventory, item);
            destroyed += item.Amount;
        }
        RequireComponent(id, crop, stack);
        if (stack.Inventory.Stock.Any(item => item.Amount > 0))
            throw new InvalidOperationException("Crop stock changed during whole-burn; cannot disable a nonempty inventory.");
        Invoke(stack, "DisableGoodStack");
        return destroyed;
    }

    private void RequireOrigin(Guid id, BlockObject expected)
    {
        if (!TryResolveCrop(id, out var current) || !ReferenceEquals(current, expected))
            throw new InvalidOperationException("Crop origin disappeared during compound native mutation; completion is unknown.");
    }
    private void RequireComponent<T>(Guid id, BlockObject crop, T expected) where T : class
    {
        RequireOrigin(id, crop);
        if (!crop.TryGetComponent(out T current) || !ReferenceEquals(current, expected))
            throw new InvalidOperationException("Crop component changed during compound native mutation; completion is unknown.");
    }
    private static bool TryGetYielder(BlockObject crop, out Yielder yielder)
    {
        if (crop.TryGetComponent(out Gatherable gatherable)) { yielder = gatherable.Yielder; return true; }
        if (crop.TryGetComponent(out yielder)) return true;
        if (crop.TryGetComponent(out Cuttable cuttable)) { yielder = cuttable.Yielder; return true; }
        yielder = null!;
        return false;
    }
    private static void Invoke(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, Type.EmptyTypes, modifiers: null) ?? throw new MissingMethodException(target.GetType().FullName, methodName);
        method.Invoke(target, null);
    }
    private int ApplyBurnedTextures(BlockObject blockObject, string textureLabel)
    {
        return blockObject.Transform
            .GetComponentsInChildren<Renderer>(includeInactive: true)
            .Sum(renderer => ApplyBurnedTextures(renderer, textureLabel));
    }

    private int ApplyBurnedTextures(Renderer renderer, string textureLabel)
    {
        Material?[] materials = renderer.sharedMaterials;
        Material?[] updatedMaterials = materials
            .Select(material => CreateBurnedMaterialOrOriginal(material, textureLabel))
            .ToArray();
        int updatedMaterialCount = Enumerable.Range(0, materials.Length)
            .Count(index => !ReferenceEquals(materials[index], updatedMaterials[index]));

        if (updatedMaterialCount > 0)
        {
            renderer.sharedMaterials = updatedMaterials;
        }

        return updatedMaterialCount;
    }

    private Material? CreateBurnedMaterialOrOriginal(Material? source, string textureLabel)
    {
        if (source is null ||
            IsBurnedMaterial(source) ||
            !source.HasProperty("_MainTex") ||
            source.mainTexture is null)
        {
            return source;
        }

        Texture2D? burnedTexture = _textureDeriver.DeriveBurnedTexture(source.mainTexture, textureLabel);
        return burnedTexture is null ? source : CreateBurnedMaterial(source, burnedTexture);
    }

    private static Material CreateBurnedMaterial(Material source, Texture burnedTexture)
    {
        Material material = new(source)
        {
            name = $"{source.name} Wildfire Burned",
            mainTexture = burnedTexture,
            hideFlags = HideFlags.HideAndDontSave,
        };
        return material;
    }

    private static bool IsBurnedMaterial(Material material)
    {
        return material.name.EndsWith(" Wildfire Burned", StringComparison.Ordinal);
    }

    private static bool IsCropOrHarvestableName(string name)
    {
        return CropOrHarvestableNameTokens.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase)) &&
            !TreeNameTokens.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly string[] CropOrHarvestableNameTokens =
    {
        "Blueberry",
        "Bush",
        "Canola",
        "Carrot",
        "Cassava",
        "Cattail",
        "Coffee",
        "Corn",
        "Dandelion",
        "Eggplant",
        "Kohlrabi",
        "Potato",
        "Soybean",
        "Spadderdock",
        "Sunflower",
        "Wheat",
    };

    private static readonly string[] TreeNameTokens =
    {
        "Birch",
        "ChestnutTree",
        "Mangrove",
        "Maple",
        "Oak",
        "Pine",
        "Tree",
    };

}
