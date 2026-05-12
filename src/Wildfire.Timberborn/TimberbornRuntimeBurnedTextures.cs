using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using Timberborn.Cutting;
using Timberborn.EntitySystem;
using Timberborn.Gathering;
using Timberborn.Goods;
using Timberborn.NaturalResourcesLifecycle;
using Timberborn.NaturalResourcesMoisture;
using Timberborn.Yielding;
using UnityEngine;

namespace Wildfire.Timberborn;

public sealed class TimberbornTextureTreeBurnConsequenceApi : ITimberbornTreeBurnConsequenceApi
{
    private static readonly string[] BurnedTexturePropertyNames = { "_MainTex", "_BaseMap" };
    private readonly ITimberbornFireLogSink _logSink;
    private readonly TimberbornRuntimeBurnedTextureDeriver _textureDeriver;
    private readonly Dictionary<string, BlockObject> _treeTargetsByStableId;

    public TimberbornTextureTreeBurnConsequenceApi(
        EntityRegistry entityRegistry,
        ITimberbornFireLogSink? logSink = null,
        TimberbornRuntimeBurnedTextureDeriver? textureDeriver = null)
    {
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
        _textureDeriver = textureDeriver ?? new TimberbornRuntimeBurnedTextureDeriver(_logSink);
        _treeTargetsByStableId = TimberbornEntityComponentCells.BlockObjects(
                entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry)))
            .Where(static blockObject => TimberbornEntityComponentCells.IsTreeName(blockObject.Name))
            .GroupBy(static blockObject => StableTreeTargetId(blockObject), StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.OrderBy(static blockObject => RuntimeHelpers.GetHashCode(blockObject)).First(),
                StringComparer.Ordinal);
    }

    public TimberbornTreeBurnConsequenceResult ApplyConsequence(TimberbornTreeBurnConsequence consequence)
    {
        return consequence.Kind switch
        {
            TimberbornTreeBurnConsequenceKind.DryTree => ApplyDryTree(consequence),
            TimberbornTreeBurnConsequenceKind.KillTree => ApplyKillTree(consequence),
            TimberbornTreeBurnConsequenceKind.MarkBurnedVisual => ApplyBurnedVisual(consequence),
            TimberbornTreeBurnConsequenceKind.MarkBurnedLeftover => ApplyBurnedLeftover(consequence),
            _ => new TimberbornTreeBurnConsequenceResult(Applied: false, SafeApiUnavailable: true),
        };
    }

    private TimberbornTreeBurnConsequenceResult ApplyDryTree(TimberbornTreeBurnConsequence consequence)
    {
        if (!TryGetTarget(consequence, out BlockObject blockObject))
        {
            return new TimberbornTreeBurnConsequenceResult(Applied: false, SafeApiUnavailable: true);
        }

        if (!blockObject.TryGetComponent(out WateredNaturalResource wateredNaturalResource))
        {
            _logSink.Warning(
                "wildfire_timberborn_tree_dry_skipped " +
                $"reason=watered_resource_missing stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
                $"spec_id={TimberbornQaCommandBridge.FormatToken(consequence.SpecId)}");
            return new TimberbornTreeBurnConsequenceResult(Applied: false, SafeApiUnavailable: true);
        }

        if (!TryInvokeNoArgumentMethod(wateredNaturalResource, "StartDryingOut"))
        {
            _logSink.Warning(
                "wildfire_timberborn_tree_dry_skipped " +
                $"reason=start_drying_unavailable stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
                $"spec_id={TimberbornQaCommandBridge.FormatToken(consequence.SpecId)}");
            return new TimberbornTreeBurnConsequenceResult(Applied: false, SafeApiUnavailable: true);
        }

        _logSink.Info(
            "wildfire_timberborn_tree_dried_by_fire " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(TextureLabel(consequence, blockObject))} " +
            $"damage_taken={consequence.DamageTaken} damage_capacity={consequence.DamageCapacity}");
        return new TimberbornTreeBurnConsequenceResult(Applied: true, SafeApiUnavailable: false);
    }

    private TimberbornTreeBurnConsequenceResult ApplyKillTree(TimberbornTreeBurnConsequence consequence)
    {
        if (!TryGetTarget(consequence, out BlockObject blockObject))
        {
            return new TimberbornTreeBurnConsequenceResult(Applied: false, SafeApiUnavailable: true);
        }

        if (!blockObject.TryGetComponent(out LivingNaturalResource livingNaturalResource))
        {
            _logSink.Warning(
                "wildfire_timberborn_tree_kill_skipped " +
                $"reason=living_resource_missing stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
                $"spec_id={TimberbornQaCommandBridge.FormatToken(consequence.SpecId)}");
            return new TimberbornTreeBurnConsequenceResult(Applied: false, SafeApiUnavailable: true);
        }

        if (!livingNaturalResource.IsDead)
        {
            livingNaturalResource.Die();
        }

        _logSink.Info(
            "wildfire_timberborn_tree_killed_by_fire " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(TextureLabel(consequence, blockObject))} " +
            $"damage_taken={consequence.DamageTaken} damage_capacity={consequence.DamageCapacity}");
        return new TimberbornTreeBurnConsequenceResult(Applied: true, SafeApiUnavailable: false);
    }

    private TimberbornTreeBurnConsequenceResult ApplyBurnedVisual(TimberbornTreeBurnConsequence consequence)
    {
        if (!TryGetTarget(consequence, out BlockObject blockObject))
        {
            return new TimberbornTreeBurnConsequenceResult(Applied: false, SafeApiUnavailable: true);
        }

        string textureLabel = TextureLabel(consequence, blockObject);
        int updatedMaterialCount = ApplyBurnedTextures(blockObject, textureLabel);
        if (updatedMaterialCount == 0)
        {
            _logSink.Warning(
                "wildfire_timberborn_tree_burned_texture_skipped " +
                $"reason=renderer_material_missing stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
                $"target={TimberbornQaCommandBridge.FormatToken(textureLabel)}");
            return new TimberbornTreeBurnConsequenceResult(Applied: false, SafeApiUnavailable: true);
        }

        _logSink.Info(
            "wildfire_timberborn_tree_burned_texture_applied " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(textureLabel)} " +
            $"materials={updatedMaterialCount}");
        return new TimberbornTreeBurnConsequenceResult(Applied: true, SafeApiUnavailable: false);
    }

    private TimberbornTreeBurnConsequenceResult ApplyBurnedLeftover(TimberbornTreeBurnConsequence consequence)
    {
        if (!TryGetTarget(consequence, out BlockObject blockObject))
        {
            return new TimberbornTreeBurnConsequenceResult(Applied: false, SafeApiUnavailable: true);
        }

        if (!blockObject.TryGetComponent(out Cuttable cuttable))
        {
            _logSink.Warning(
                "wildfire_timberborn_tree_burned_leftover_skipped " +
                $"reason=cuttable_missing stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
                $"spec_id={TimberbornQaCommandBridge.FormatToken(consequence.SpecId)}");
            return new TimberbornTreeBurnConsequenceResult(Applied: false, SafeApiUnavailable: true);
        }

        cuttable.Yielder.RemoveRemainingYield();
        cuttable.ShowLeftoverModel();

        string textureLabel = TextureLabel(consequence, blockObject);
        int updatedMaterialCount = ApplyBurnedTextures(blockObject, textureLabel);
        if (updatedMaterialCount == 0)
        {
            _logSink.Warning(
                "wildfire_timberborn_tree_burned_leftover_texture_skipped " +
                $"reason=renderer_material_missing stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
                $"target={TimberbornQaCommandBridge.FormatToken(textureLabel)}");
        }

        _logSink.Info(
            "wildfire_timberborn_tree_burned_leftover_applied " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(textureLabel)} " +
            $"materials={updatedMaterialCount}");
        return new TimberbornTreeBurnConsequenceResult(Applied: true, SafeApiUnavailable: false);
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
        if (source is null || IsBurnedMaterial(source))
        {
            return source;
        }

        TexturePropertyBinding? textureBinding = BurnedTexturePropertyNames
            .Select(propertyName => TryGetTexture(source, propertyName))
            .FirstOrDefault(static binding => binding.HasValue);
        if (!textureBinding.HasValue)
        {
            return source;
        }

        Texture2D? burnedTexture = _textureDeriver.DeriveBurnedTexture(textureBinding.Value.Texture, textureLabel);
        return burnedTexture is null
            ? source
            : CreateBurnedMaterial(source, burnedTexture, textureBinding.Value.PropertyName);
    }

    private static Material CreateBurnedMaterial(Material source, Texture burnedTexture, string propertyName)
    {
        Material material = new(source)
        {
            name = $"{source.name} Wildfire Burned",
            hideFlags = HideFlags.HideAndDontSave,
        };
        material.SetTexture(propertyName, burnedTexture);
        return material;
    }

    private static bool IsBurnedMaterial(Material material)
    {
        return material.name.EndsWith(" Wildfire Burned", StringComparison.Ordinal);
    }

    private static TexturePropertyBinding? TryGetTexture(Material material, string propertyName)
    {
        if (!material.HasProperty(propertyName))
        {
            return null;
        }

        Texture texture = material.GetTexture(propertyName);
        return texture is null ? null : new TexturePropertyBinding(propertyName, texture);
    }

    private readonly record struct TexturePropertyBinding(string PropertyName, Texture Texture);

    private static string StableTreeTargetId(BlockObject blockObject)
    {
        return $"tree_cuttable:{RuntimeHelpers.GetHashCode(blockObject)}";
    }

    private bool TryGetTarget(TimberbornTreeBurnConsequence consequence, out BlockObject blockObject)
    {
        if (_treeTargetsByStableId.TryGetValue(consequence.TargetKey.StableId, out blockObject!))
        {
            return true;
        }

        _logSink.Warning(
            "wildfire_timberborn_tree_consequence_skipped " +
            $"reason=target_missing kind={TimberbornQaCommandBridge.FormatToken(consequence.Kind.ToString())} " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"spec_id={TimberbornQaCommandBridge.FormatToken(consequence.SpecId)}");
        return false;
    }

    private static string TextureLabel(TimberbornTreeBurnConsequence consequence, BlockObject blockObject)
    {
        return string.IsNullOrWhiteSpace(consequence.SpecId)
            ? blockObject.Name
            : consequence.SpecId;
    }

    private static bool TryInvokeNoArgumentMethod(object target, string methodName)
    {
        try
        {
            System.Reflection.MethodInfo? method = target.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic,
                binder: null,
                Type.EmptyTypes,
                modifiers: null);
            if (method is null)
            {
                return false;
            }

            method.Invoke(target, null);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class TimberbornTextureCropBurnConsequenceApi : ITimberbornCropBurnConsequenceApi
{
    private readonly ITimberbornFireLogSink _logSink;
    private readonly TimberbornRuntimeBurnedTextureDeriver _textureDeriver;
    private readonly Dictionary<string, BlockObject> _cropTargetsByStableId;

    public TimberbornTextureCropBurnConsequenceApi(
        EntityRegistry entityRegistry,
        ITimberbornFireLogSink? logSink = null,
        TimberbornRuntimeBurnedTextureDeriver? textureDeriver = null)
    {
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
        _textureDeriver = textureDeriver ?? new TimberbornRuntimeBurnedTextureDeriver(_logSink);
        _cropTargetsByStableId = TimberbornEntityComponentCells.BlockObjects(
                entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry)))
            .Where(static blockObject => IsCropOrHarvestableName(blockObject.Name))
            .SelectMany(static blockObject => StableCropTargetIds(blockObject)
                .Select(stableId => new KeyValuePair<string, BlockObject>(stableId, blockObject)))
            .GroupBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .Select(static pair => pair.Value)
                    .OrderBy(static blockObject => RuntimeHelpers.GetHashCode(blockObject))
                    .First(),
                StringComparer.Ordinal);
    }

    public TimberbornCropBurnConsequenceResult ApplyConsequence(TimberbornCropBurnConsequence consequence)
    {
        return consequence.Kind switch
        {
            TimberbornCropBurnConsequenceKind.DryCrop => ApplyDryCrop(consequence),
            TimberbornCropBurnConsequenceKind.ReduceYield => ApplyYieldLoss(consequence),
            TimberbornCropBurnConsequenceKind.KillCrop => ApplyKillCrop(consequence),
            TimberbornCropBurnConsequenceKind.MarkBurnedVisual => ApplyBurnedVisual(consequence),
            TimberbornCropBurnConsequenceKind.MarkBurnedLeftover => ApplyBurnedLeftover(consequence),
            _ => SkippedUnsafe,
        };
    }

    private TimberbornCropBurnConsequenceResult ApplyDryCrop(TimberbornCropBurnConsequence consequence)
    {
        if (!TryGetTarget(consequence, out BlockObject blockObject))
        {
            return SkippedUnsafe;
        }

        if (!blockObject.TryGetComponent(out WateredNaturalResource wateredNaturalResource) ||
            !TryInvokeNoArgumentMethod(wateredNaturalResource, "StartDryingOut"))
        {
            _logSink.Warning(
                "wildfire_timberborn_crop_dry_skipped " +
                $"reason=watered_resource_unavailable stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
                $"spec_id={TimberbornQaCommandBridge.FormatToken(consequence.SpecId)}");
            return SkippedUnsafe;
        }

        _logSink.Info(
            "wildfire_timberborn_crop_dried_by_fire " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(TextureLabel(consequence, blockObject))} " +
            $"damage_taken={consequence.DamageTaken} damage_capacity={consequence.DamageCapacity}");
        return MatchedNoMutation;
    }

    private TimberbornCropBurnConsequenceResult ApplyYieldLoss(TimberbornCropBurnConsequence consequence)
    {
        if (!TryGetTarget(consequence, out BlockObject blockObject) ||
            !TryGetYielder(blockObject, out Yielder yielder))
        {
            return SkippedUnsafe;
        }

        int yieldLost = Math.Min(
            Math.Max(0, consequence.YieldLost),
            Math.Max(0, yielder.Yield.Amount));
        if (yieldLost <= 0)
        {
            return MatchedNoMutation;
        }

        string goodId = yielder.Yield.GoodId;
        yielder.DecreaseYield(new GoodAmount(goodId, yieldLost));
        _logSink.Info(
            "wildfire_timberborn_crop_yield_reduced_by_fire " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(TextureLabel(consequence, blockObject))} " +
            $"resource={TimberbornQaCommandBridge.FormatToken(goodId)} " +
            $"yield_lost={yieldLost} remaining_yield={Math.Max(0, yielder.Yield.Amount)}");
        return new TimberbornCropBurnConsequenceResult(
            MatchedCropTarget: true,
            YieldLost: yieldLost,
            KilledCrop: false,
            VisualStateUpdated: false,
            SkippedUnsafeApi: false);
    }

    private TimberbornCropBurnConsequenceResult ApplyKillCrop(TimberbornCropBurnConsequence consequence)
    {
        if (!TryGetTarget(consequence, out BlockObject blockObject))
        {
            return SkippedUnsafe;
        }

        bool killed = TryKillNaturalResource(blockObject) || TryRemoveGatherableYield(blockObject);
        if (!killed)
        {
            _logSink.Warning(
                "wildfire_timberborn_crop_kill_skipped " +
                $"reason=death_api_unavailable stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
                $"spec_id={TimberbornQaCommandBridge.FormatToken(consequence.SpecId)}");
            return SkippedUnsafe;
        }

        _logSink.Info(
            "wildfire_timberborn_crop_killed_by_fire " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(TextureLabel(consequence, blockObject))} " +
            $"damage_taken={consequence.DamageTaken} damage_capacity={consequence.DamageCapacity}");
        return new TimberbornCropBurnConsequenceResult(
            MatchedCropTarget: true,
            YieldLost: 0,
            KilledCrop: true,
            VisualStateUpdated: false,
            SkippedUnsafeApi: false);
    }

    private TimberbornCropBurnConsequenceResult ApplyBurnedVisual(TimberbornCropBurnConsequence consequence)
    {
        if (!TryGetTarget(consequence, out BlockObject blockObject))
        {
            return SkippedUnsafe;
        }

        return ApplyBurnedTextures(consequence, blockObject, "wildfire_timberborn_crop_burned_texture_applied");
    }

    private TimberbornCropBurnConsequenceResult ApplyBurnedLeftover(TimberbornCropBurnConsequence consequence)
    {
        if (!TryGetTarget(consequence, out BlockObject blockObject))
        {
            return SkippedUnsafe;
        }

        if (TryGetYielder(blockObject, out Yielder yielder))
        {
            yielder.RemoveRemainingYield();
        }

        TryRemoveGatherableYield(blockObject);
        TryKillNaturalResource(blockObject);
        return ApplyBurnedTextures(consequence, blockObject, "wildfire_timberborn_crop_burned_leftover_applied");
    }

    private TimberbornCropBurnConsequenceResult ApplyBurnedTextures(
        TimberbornCropBurnConsequence consequence,
        BlockObject blockObject,
        string logToken)
    {
        string textureLabel = TextureLabel(consequence, blockObject);
        int updatedMaterialCount = ApplyBurnedTextures(blockObject, textureLabel);
        if (updatedMaterialCount == 0)
        {
            _logSink.Warning(
                "wildfire_timberborn_crop_burned_texture_skipped " +
                $"reason=renderer_material_missing stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
                $"target={TimberbornQaCommandBridge.FormatToken(textureLabel)}");
            return SkippedUnsafe;
        }

        _logSink.Info(
            $"{logToken} " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(textureLabel)} " +
            $"materials={updatedMaterialCount}");
        return new TimberbornCropBurnConsequenceResult(
            MatchedCropTarget: true,
            YieldLost: 0,
            KilledCrop: false,
            VisualStateUpdated: true,
            SkippedUnsafeApi: false);
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

    private static bool TryKillNaturalResource(BlockObject blockObject)
    {
        if (!blockObject.TryGetComponent(out LivingNaturalResource livingNaturalResource))
        {
            return false;
        }

        if (!livingNaturalResource.IsDead)
        {
            livingNaturalResource.Die();
        }

        return true;
    }

    private static bool TryRemoveGatherableYield(BlockObject blockObject)
    {
        if (!blockObject.TryGetComponent(out GatherableYieldGrower gatherableYieldGrower))
        {
            return false;
        }

        return TryInvokeNoArgumentMethod(gatherableYieldGrower, "RemoveYield");
    }

    private static bool TryGetYielder(BlockObject blockObject, out Yielder yielder)
    {
        if (blockObject.TryGetComponent(out Gatherable gatherable))
        {
            yielder = gatherable.Yielder;
            return true;
        }

        if (blockObject.TryGetComponent(out Yielder directYielder))
        {
            yielder = directYielder;
            return true;
        }

        if (blockObject.TryGetComponent(out Cuttable cuttable))
        {
            yielder = cuttable.Yielder;
            return true;
        }

        yielder = null!;
        return false;
    }

    private static IEnumerable<string> StableCropTargetIds(BlockObject blockObject)
    {
        int hashCode = RuntimeHelpers.GetHashCode(blockObject);
        return new[]
        {
            $"crop_harvestable:{hashCode}",
            $"selected_crop_harvestable:{hashCode}",
        };
    }

    private bool TryGetTarget(TimberbornCropBurnConsequence consequence, out BlockObject blockObject)
    {
        if (_cropTargetsByStableId.TryGetValue(consequence.TargetKey.StableId, out blockObject!))
        {
            return true;
        }

        _logSink.Warning(
            "wildfire_timberborn_crop_consequence_skipped " +
            $"reason=target_missing kind={TimberbornQaCommandBridge.FormatToken(consequence.Kind.ToString())} " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"spec_id={TimberbornQaCommandBridge.FormatToken(consequence.SpecId)}");
        return false;
    }

    private static string TextureLabel(TimberbornCropBurnConsequence consequence, BlockObject blockObject)
    {
        return string.IsNullOrWhiteSpace(consequence.SpecId)
            ? blockObject.Name
            : consequence.SpecId;
    }

    private static bool TryInvokeNoArgumentMethod(object target, string methodName)
    {
        try
        {
            System.Reflection.MethodInfo? method = target.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic,
                binder: null,
                Type.EmptyTypes,
                modifiers: null);
            if (method is null)
            {
                return false;
            }

            method.Invoke(target, null);
            return true;
        }
        catch
        {
            return false;
        }
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

    private static TimberbornCropBurnConsequenceResult MatchedNoMutation => new(
        MatchedCropTarget: true,
        YieldLost: 0,
        KilledCrop: false,
        VisualStateUpdated: false,
        SkippedUnsafeApi: false);

    private static TimberbornCropBurnConsequenceResult SkippedUnsafe => new(
        MatchedCropTarget: false,
        YieldLost: 0,
        KilledCrop: false,
        VisualStateUpdated: false,
        SkippedUnsafeApi: true);
}

public sealed class TimberbornRuntimeBurnedTextureDeriver
{
    private readonly ITimberbornFireLogSink _logSink;
    private readonly Dictionary<int, Texture2D> _burnedTexturesBySourceId = new();

    public TimberbornRuntimeBurnedTextureDeriver(ITimberbornFireLogSink? logSink = null)
    {
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
    }

    public Texture2D? DeriveBurnedTexture(Texture sourceTexture, string textureLabel)
    {
        int sourceId = sourceTexture.GetInstanceID();
        if (_burnedTexturesBySourceId.TryGetValue(sourceId, out Texture2D? cachedTexture))
        {
            return cachedTexture;
        }

        try
        {
            Texture2D burnedTexture = CreateBurnedTexture(sourceTexture, textureLabel);
            _burnedTexturesBySourceId[sourceId] = burnedTexture;
            _logSink.Info(
                "wildfire_timberborn_burned_texture_derived " +
                $"target={TimberbornQaCommandBridge.FormatToken(textureLabel)} " +
                $"source_texture={TimberbornQaCommandBridge.FormatToken(sourceTexture.name)} " +
                $"width={burnedTexture.width} height={burnedTexture.height}");
            return burnedTexture;
        }
        catch (Exception exception)
        {
            _logSink.Warning(
                "wildfire_timberborn_burned_texture_derive_failed " +
                $"target={TimberbornQaCommandBridge.FormatToken(textureLabel)} " +
                $"source_texture={TimberbornQaCommandBridge.FormatToken(sourceTexture.name)} " +
                $"reason={TimberbornQaCommandBridge.FormatToken(exception.GetType().Name)}");
            return null;
        }
    }

    private static Texture2D CreateBurnedTexture(Texture sourceTexture, string textureLabel)
    {
        RenderTexture renderTexture = RenderTexture.GetTemporary(
            sourceTexture.width,
            sourceTexture.height,
            depthBuffer: 0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB);
        RenderTexture? previousRenderTexture = RenderTexture.active;

        try
        {
            Graphics.Blit(sourceTexture, renderTexture);
            RenderTexture.active = renderTexture;
            Texture2D readableTexture = new(
                sourceTexture.width,
                sourceTexture.height,
                TextureFormat.RGBA32,
                mipChain: true)
            {
                name = $"{textureLabel} RuntimeBurned",
                wrapMode = sourceTexture.wrapMode,
                filterMode = sourceTexture.filterMode,
                hideFlags = HideFlags.HideAndDontSave,
            };
            readableTexture.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0);
            readableTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            int width = readableTexture.width;
            int height = readableTexture.height;
            Color32[] pixels = readableTexture.GetPixels32();
            Color32[] burnedPixels = pixels
                .Select((pixel, index) => CharPixel(pixel, index % width, index / width, width, height))
                .ToArray();
            readableTexture.SetPixels32(burnedPixels);
            readableTexture.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            return readableTexture;
        }
        finally
        {
            RenderTexture.active = previousRenderTexture;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    private static Color32 CharPixel(Color32 pixel, int x, int y, int width, int height)
    {
        float u = width <= 1 ? 0f : x / (float)(width - 1);
        float v = height <= 1 ? 0f : y / (float)(height - 1);
        float luminance = ((pixel.r * 0.2126f) + (pixel.g * 0.7152f) + (pixel.b * 0.0722f)) / 255f;
        float verticalGrain = Mathf.PerlinNoise(u * 24f, v * 5f);
        float barkGrooves = Mathf.PerlinNoise(u * 72f, v * 16f);
        float ashNoise = Mathf.PerlinNoise((u + 17.13f) * 180f, (v + 3.91f) * 180f);
        float exposedNoise = Mathf.PerlinNoise((u + 41.7f) * 42f, (v + 9.25f) * 9f);
        float charcoal = Mathf.Clamp01(0.035f + luminance * 0.11f + verticalGrain * 0.045f - barkGrooves * 0.035f);
        Color color = new(charcoal, charcoal * 0.92f, charcoal * 0.78f, pixel.a / 255f);

        if (exposedNoise > 0.78f && luminance > 0.18f)
        {
            float exposed = Mathf.InverseLerp(0.78f, 1f, exposedNoise) *
                Mathf.InverseLerp(0.18f, 0.55f, luminance);
            color = Color.Lerp(color, new Color(0.32f, 0.14f, 0.055f, pixel.a / 255f), exposed * 0.42f);
        }

        if (ashNoise > 0.9f)
        {
            float ash = Mathf.InverseLerp(0.9f, 1f, ashNoise);
            color = Color.Lerp(color, new Color(0.28f, 0.27f, 0.25f, pixel.a / 255f), ash * 0.32f);
        }

        return new Color32(ToByte(color.r), ToByte(color.g), ToByte(color.b), pixel.a);
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
    }
}
