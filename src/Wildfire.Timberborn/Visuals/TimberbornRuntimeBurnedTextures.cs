using System.Runtime.CompilerServices;
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

public static class TimberbornRuntimeBurnedTextureBehavior
{
    public const string TreeComponentProbeFailedToken = "wildfire_timberborn_tree_component_probe_failed";
    public const string TreeKillAlreadyTerminalToken = "wildfire_timberborn_tree_kill_skipped";
    public const string TreeBurnedLeftoverAlreadyTerminalToken = "wildfire_timberborn_tree_burned_leftover_skipped";
    public const string TreeBurnedTextureRendererSkippedToken =
        "wildfire_timberborn_tree_burned_texture_renderer_skipped";
    public const string TreeBurnedTextureMaterialSkippedToken =
        "wildfire_timberborn_tree_burned_texture_material_skipped";
    public const string CropBurnedResourceDeletedReason = "native_entity_service";

    public static bool ShouldSkipStaleTreeComponentProbe(Exception exception) =>
        exception is NullReferenceException or InvalidOperationException;

    public static bool ShouldSkipInvalidRendererOrMaterial(Exception exception) =>
        exception is MissingReferenceException or NullReferenceException;

    public static TimberbornCropBurnConsequenceResult DeletedBurnedResourceResult() =>
        new(
            MatchedCropTarget: true,
            YieldLost: 0,
            KilledCrop: true,
            VisualStateUpdated: true,
            FailedConsequence: false);
}

public sealed class TimberbornTextureCropBurnConsequenceApi : ITimberbornCropBurnConsequenceApi
{
    private readonly ITimberbornFireLogSink _logSink;
    private readonly TimberbornRuntimeBurnedTextureDeriver _textureDeriver;
    private readonly IBlockService? _blockService;
    private readonly EntityService? _entityService;
    private readonly Dictionary<string, BlockObject> _cropTargetsByStableId;
    private readonly Dictionary<string, Vector3Int[]> _cropTargetCellsByStableId;

    public TimberbornTextureCropBurnConsequenceApi(
        EntityRegistry entityRegistry,
        ITimberbornFireLogSink? logSink = null,
        TimberbornRuntimeBurnedTextureDeriver? textureDeriver = null,
        IBlockService? blockService = null,
        EntityService? entityService = null,
        IEnumerable<TimberbornBurnDamageTargetRegistration>? registrations = null)
    {
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
        _textureDeriver = textureDeriver ?? new TimberbornRuntimeBurnedTextureDeriver(_logSink);
        _blockService = blockService;
        _entityService = entityService;
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
        _cropTargetCellsByStableId = BuildCropTargetCellsByStableId(registrations);
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
            _ => throw new ArgumentOutOfRangeException(
                nameof(consequence),
                consequence.Kind,
                "Unsupported crop burn consequence kind."),
        };
    }

    private TimberbornCropBurnConsequenceResult ApplyDryCrop(TimberbornCropBurnConsequence consequence)
    {
        if (!TryGetTargetOrAlreadyRemoved(consequence, out BlockObject blockObject, out TimberbornCropBurnConsequenceResult alreadyRemovedResult))
        {
            return alreadyRemovedResult;
        }

        if (!blockObject.TryGetComponent(out WateredNaturalResource wateredNaturalResource))
        {
            throw MissingCropComponent(consequence, "WateredNaturalResource");
        }

        InvokeNoArgumentMethod(wateredNaturalResource, "StartDryingOut");
        _logSink.Info(
            "wildfire_timberborn_crop_dried_by_fire " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(TextureLabel(consequence, blockObject))} " +
            $"damage_taken={consequence.DamageTaken} damage_capacity={consequence.DamageCapacity}");
        return MatchedNoMutation;
    }

    private TimberbornCropBurnConsequenceResult ApplyYieldLoss(TimberbornCropBurnConsequence consequence)
    {
        if (!TryGetTargetOrAlreadyRemoved(consequence, out BlockObject blockObject, out TimberbornCropBurnConsequenceResult alreadyRemovedResult))
        {
            return alreadyRemovedResult;
        }

        if (!TryGetYielder(blockObject, out Yielder yielder))
        {
            throw MissingCropComponent(consequence, "Yielder");
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
            FailedConsequence: false);
    }

    private TimberbornCropBurnConsequenceResult ApplyKillCrop(TimberbornCropBurnConsequence consequence)
    {
        if (!TryGetTargetOrAlreadyRemoved(consequence, out BlockObject blockObject, out TimberbornCropBurnConsequenceResult alreadyRemovedResult))
        {
            return alreadyRemovedResult;
        }

        bool killed = TryKillNaturalResource(blockObject) || TryRemoveGatherableYield(blockObject);
        if (!killed)
        {
            throw new InvalidOperationException(
                $"Crop burn consequence could not kill {consequence.TargetKey.StableId} ({consequence.SpecId}).");
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
            FailedConsequence: false);
    }

    private TimberbornCropBurnConsequenceResult ApplyBurnedVisual(TimberbornCropBurnConsequence consequence)
    {
        if (!TryGetTargetOrAlreadyRemoved(consequence, out BlockObject blockObject, out TimberbornCropBurnConsequenceResult alreadyRemovedResult))
        {
            return alreadyRemovedResult;
        }

        return ApplyBurnedTextures(
            consequence,
            blockObject,
            "wildfire_timberborn_crop_burned_texture_applied");
    }

    private TimberbornCropBurnConsequenceResult ApplyBurnedLeftover(TimberbornCropBurnConsequence consequence)
    {
        if (!TryGetTargetOrAlreadyRemoved(consequence, out BlockObject blockObject, out TimberbornCropBurnConsequenceResult alreadyRemovedResult))
        {
            return alreadyRemovedResult;
        }

        if (TryGetYielder(blockObject, out Yielder yielder))
        {
            yielder.RemoveRemainingYield();
        }

        TryRemoveGatherableYield(blockObject);
        TryClearGoodStack(blockObject);
        TryKillNaturalResource(blockObject);
        string textureLabel = TextureLabel(consequence, blockObject);
        if (consequence.TargetKind == TimberbornBurnDamageTargetKind.Resource &&
            TryDeleteBurnedResource(blockObject, consequence, textureLabel))
        {
            return new TimberbornCropBurnConsequenceResult(
                MatchedCropTarget: true,
                YieldLost: 0,
                KilledCrop: true,
                VisualStateUpdated: true,
                FailedConsequence: false);
        }

        bool modelRefreshed = TryRefreshCropNaturalResourceModel(blockObject);
        if (!modelRefreshed)
        {
            throw new InvalidOperationException(
                $"Crop burned leftover model refresh failed for {consequence.TargetKey.StableId} ({textureLabel}).");
        }

        TimberbornCropBurnConsequenceResult visualResult = ApplyBurnedTextures(
            consequence,
            blockObject,
            "wildfire_timberborn_crop_burned_leftover_texture_applied");

        _logSink.Info(
            "wildfire_timberborn_crop_burned_leftover_applied " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(textureLabel)} " +
            $"deleted=false reason=unsafe_destroy_deferred model_refreshed={modelRefreshed.ToString().ToLowerInvariant()}");
        return new TimberbornCropBurnConsequenceResult(
            MatchedCropTarget: true,
            YieldLost: 0,
            KilledCrop: true,
            VisualStateUpdated: visualResult.VisualStateUpdated,
            FailedConsequence: false);
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
            throw new InvalidOperationException(
                $"Crop burned visual produced no material updates for {consequence.TargetKey.StableId} ({textureLabel}).");
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
            FailedConsequence: false);
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

    private bool TryDeleteBurnedResource(
        BlockObject blockObject,
        TimberbornCropBurnConsequence consequence,
        string textureLabel)
    {
        if (_entityService is null)
        {
            _logSink.Warning(
                "wildfire_timberborn_crop_burned_leftover_delete_skipped " +
                $"reason=entity_service_missing stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
                $"target={TimberbornQaCommandBridge.FormatToken(textureLabel)}");
            return false;
        }

        _entityService.Delete(blockObject);
        _cropTargetsByStableId.Remove(consequence.TargetKey.StableId);
        _logSink.Info(
            "wildfire_timberborn_crop_burned_leftover_applied " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(textureLabel)} " +
            $"deleted=true reason={TimberbornRuntimeBurnedTextureBehavior.CropBurnedResourceDeletedReason}");
        return TimberbornRuntimeBurnedTextureBehavior.DeletedBurnedResourceResult().VisualStateUpdated;
    }

    private bool TryRefreshCropNaturalResourceModel(BlockObject blockObject)
    {
        if (!blockObject.TryGetComponent(out NaturalResourceModel naturalResourceModel))
        {
            return false;
        }

        InvokeNoArgumentMethod(naturalResourceModel, "ShowCurrentModel");
        return true;
    }

    private static bool TryClearGoodStack(BlockObject blockObject)
    {
        if (!blockObject.TryGetComponent(out GoodStack goodStack))
        {
            return false;
        }

        foreach (GoodAmount goodAmount in goodStack.Inventory.UnreservedTakeableStock().ToArray())
        {
            TimberbornInventoryMutations.Consume(goodStack.Inventory, goodAmount);
        }

        TryInvokeNoArgumentMethod(goodStack, "DisableGoodStack");
        return true;
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
        if (TryResolveLiveTargetFromRegisteredCells(consequence, out blockObject))
        {
            _cropTargetsByStableId[consequence.TargetKey.StableId] = blockObject;
            return true;
        }

        if (_blockService is not null && _cropTargetCellsByStableId.ContainsKey(consequence.TargetKey.StableId))
        {
            _cropTargetsByStableId.Remove(consequence.TargetKey.StableId);
            _logSink.Warning(
                "wildfire_timberborn_crop_consequence_skipped " +
                $"reason=target_not_live kind={TimberbornQaCommandBridge.FormatToken(consequence.Kind.ToString())} " +
                $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
                $"spec_id={TimberbornQaCommandBridge.FormatToken(consequence.SpecId)}");
            return false;
        }

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

    private bool IsRegisteredCropTarget(TimberbornCropBurnConsequence consequence) =>
        _blockService is not null &&
        _cropTargetCellsByStableId.ContainsKey(consequence.TargetKey.StableId);

    private bool TryGetTargetOrAlreadyRemoved(
        TimberbornCropBurnConsequence consequence,
        out BlockObject blockObject,
        out TimberbornCropBurnConsequenceResult alreadyRemovedResult)
    {
        if (TryGetTarget(consequence, out blockObject))
        {
            alreadyRemovedResult = default;
            return true;
        }

        if (!IsRegisteredCropTarget(consequence))
        {
            throw new InvalidOperationException(
                $"Crop burn consequence target is missing for {consequence.TargetKey.StableId} ({consequence.Kind}).");
        }

        _logSink.Info(
            "wildfire_timberborn_crop_consequence_already_removed " +
            $"kind={TimberbornQaCommandBridge.FormatToken(consequence.Kind.ToString())} " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"spec_id={TimberbornQaCommandBridge.FormatToken(consequence.SpecId)}");
        alreadyRemovedResult = new TimberbornCropBurnConsequenceResult(
            MatchedCropTarget: true,
            YieldLost: 0,
            KilledCrop: true,
            VisualStateUpdated: true,
            FailedConsequence: false);
        return false;
    }

    private BlockObject GetTarget(TimberbornCropBurnConsequence consequence)
    {
        if (TryGetTarget(consequence, out BlockObject blockObject))
        {
            return blockObject;
        }

        throw new InvalidOperationException(
            $"Crop burn consequence target is missing for {consequence.TargetKey.StableId} ({consequence.Kind}).");
    }

    private static Dictionary<string, Vector3Int[]> BuildCropTargetCellsByStableId(
        IEnumerable<TimberbornBurnDamageTargetRegistration>? registrations)
    {
        return (registrations ?? Array.Empty<TimberbornBurnDamageTargetRegistration>())
            .GroupBy(static registration => registration.TargetKey.StableId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .SelectMany(static registration => registration.OwnedCells)
                    .Select(static cell => new Vector3Int(cell.X, cell.Y, cell.Z))
                    .Distinct()
                    .ToArray(),
                StringComparer.Ordinal);
    }

    private bool TryResolveLiveTargetFromRegisteredCells(
        TimberbornCropBurnConsequence consequence,
        out BlockObject blockObject)
    {
        blockObject = null!;
        if (_blockService is null ||
            !_cropTargetCellsByStableId.TryGetValue(consequence.TargetKey.StableId, out Vector3Int[] cells))
        {
            return false;
        }

        blockObject = cells
            .SelectMany(coordinates => _blockService.GetObjectsWithComponentAt<BlockObject>(coordinates))
            .Where(static candidate => IsCropOrHarvestableName(candidate.Name))
            .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
            .FirstOrDefault()!;
        return blockObject is not null;
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

    private static void InvokeNoArgumentMethod(object target, string methodName)
    {
        System.Reflection.MethodInfo method = target.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic,
                binder: null,
                Type.EmptyTypes,
                modifiers: null) ??
            throw new MissingMethodException(target.GetType().FullName, methodName);

        method.Invoke(target, null);
    }

    private static InvalidOperationException MissingCropComponent(
        TimberbornCropBurnConsequence consequence,
        string componentName)
    {
        return new InvalidOperationException(
            $"Crop burn consequence requires {componentName} for {consequence.TargetKey.StableId} ({consequence.SpecId}).");
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
        FailedConsequence: false);

}

public sealed class TimberbornRuntimeBurnedTextureDeriver
{
    private readonly ITimberbornFireLogSink _logSink;
    private readonly Dictionary<EntityId, Texture2D> _burnedTexturesBySourceId = new();

    public TimberbornRuntimeBurnedTextureDeriver(ITimberbornFireLogSink? logSink = null)
    {
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
    }

    public Texture2D? DeriveBurnedTexture(Texture sourceTexture, string textureLabel)
    {
        EntityId sourceId = sourceTexture.GetEntityId();
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
