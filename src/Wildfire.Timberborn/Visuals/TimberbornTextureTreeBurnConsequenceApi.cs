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

public sealed partial class TimberbornTextureTreeBurnConsequenceApi : ITimberbornLiveTreeBurnConsequenceApi
{
    private static readonly string[] BurnedTexturePropertyNames = { "_MainTex", "_BaseMap" };
    private static readonly string[] TintPropertyNames = { "_Color", "_BaseColor" };
    private static readonly Color CharredTintColor = new(0.12f, 0.10f, 0.08f, 1f);
    private readonly ITimberbornFireLogSink _logSink;
    private readonly TimberbornRuntimeBurnedTextureDeriver _textureDeriver;
    private readonly EntityRegistry _entities;

    public TimberbornTextureTreeBurnConsequenceApi(
        EntityRegistry entityRegistry,
        ITimberbornFireLogSink? logSink = null,
        TimberbornRuntimeBurnedTextureDeriver? textureDeriver = null)
        : this(entityRegistry, logSink, textureDeriver, restoreLegacyLeftovers: true) { }

    public static TimberbornTextureTreeBurnConsequenceApi CreateOwned(EntityRegistry entityRegistry,
        ITimberbornFireLogSink? logSink = null, TimberbornRuntimeBurnedTextureDeriver? textureDeriver = null) =>
        new(entityRegistry, logSink, textureDeriver, restoreLegacyLeftovers: false);

    private TimberbornTextureTreeBurnConsequenceApi(EntityRegistry entityRegistry, ITimberbornFireLogSink? logSink,
        TimberbornRuntimeBurnedTextureDeriver? textureDeriver, bool restoreLegacyLeftovers)
    {
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
        _textureDeriver = textureDeriver ?? new TimberbornRuntimeBurnedTextureDeriver(_logSink);
        _entities = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
        if (!restoreLegacyLeftovers) return;
        // Restore visual presentation only. This scan is never an authorization cache for future actions.
        foreach (BlockObject blockObject in TimberbornEntityComponentCells.BlockObjects(_entities)
            .Where(blockObject => TimberbornEntityComponentCells.IsTreeName(blockObject.Name)))
        {
            if (TryGetTreeComponent(
                    blockObject,
                    StableTreeTargetId(blockObject),
                    blockObject.Name,
                    "Cuttable",
                    out Cuttable cuttable) &&
                IsInLeftoverState(cuttable))
            {
                int restored = ApplyBurnedTextures(blockObject, blockObject.Name);
                if (restored == 0)
                {
                    ApplyCharredTintToActive(blockObject);
                }
            }
        }
    }

    public TimberbornTreeBurnConsequenceResult ApplyConsequence(TimberbornTreeBurnConsequence consequence)
    {
        if (consequence.EntityId == Guid.Empty ||
            consequence.TargetKey.StableId != TimberbornBurnDamageIdentity.ForEntity(consequence.EntityId, NativeBurnTargetFamily.Tree))
            throw new InvalidOperationException("Native tree consequence requires an exact Guid family binding.");
        if (!TryResolveTree(consequence.EntityId, out BlockObject blockObject))
            return new TimberbornTreeBurnConsequenceResult(TimberbornTreeBurnConsequenceStatus.NotLive);
        return consequence.Kind switch
        {
            TimberbornTreeBurnConsequenceKind.DryTree => ApplyDryTree(consequence, blockObject),
            TimberbornTreeBurnConsequenceKind.ReduceYield => ApplyYieldLoss(consequence),
            TimberbornTreeBurnConsequenceKind.KillTree => ApplyKillTree(consequence, blockObject),
            TimberbornTreeBurnConsequenceKind.MarkBurnedVisual => ApplyBurnedVisual(consequence, blockObject),
            TimberbornTreeBurnConsequenceKind.MarkBurnedLeftover => ApplyBurnedLeftover(consequence, blockObject),
            _ => throw new ArgumentOutOfRangeException(
                nameof(consequence),
                consequence.Kind,
                "Unsupported tree burn consequence kind."),
        };
    }

    // Deliberately unwired: generation-aware request lifetime and save authority must precede activation.
    internal TimberbornPartialYieldLossResult ApplyPositivePartialYieldLoss(TimberbornTreeBurnConsequence consequence)
    {
        if (consequence.Kind != TimberbornTreeBurnConsequenceKind.ReduceYield || consequence.EntityId == Guid.Empty ||
            consequence.TargetKey.StableId != TimberbornBurnDamageIdentity.ForEntity(consequence.EntityId, NativeBurnTargetFamily.Tree))
            throw new InvalidOperationException("Partial tree yield loss requires an exact Guid resource action.");
        if (!TryResolveTree(consequence.EntityId, out var tree)) return new(TimberbornPartialYieldLossStatus.NotLive);
        if (!tree.TryGetComponent(out Cuttable cuttable)) return new(TimberbornPartialYieldLossStatus.Unavailable);
        string name = cuttable.YielderSpec.YielderComponentName;
        var yielder = TimberbornPartialYieldLoss.SelectNamed(tree.AllComponents.OfType<Yielder>(), name);
        if (!ReferenceEquals(cuttable.Yielder, yielder))
            throw new InvalidOperationException("Native cuttable points at another named yielder.");
        return TimberbornPartialYieldLoss.Apply(yielder, name, consequence.YieldResourceId, consequence.YieldLost);
    }

    private TimberbornTreeBurnConsequenceResult ApplyDryTree(TimberbornTreeBurnConsequence consequence, BlockObject blockObject)
    {

        if (!TryGetTreeComponent(
                blockObject,
                consequence.TargetKey.StableId,
                consequence.SpecId,
                "WateredNaturalResource",
                out WateredNaturalResource wateredNaturalResource))
        {
            throw MissingTreeComponent(consequence, "WateredNaturalResource");
        }

        InvokeNoArgumentMethod(wateredNaturalResource, "StartDryingOut");
        _logSink.Info(
            "wildfire_timberborn_tree_dried_by_fire " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(TextureLabel(consequence, blockObject))} " +
            $"damage_taken={consequence.DamageTaken} damage_capacity={consequence.DamageCapacity}");
        return new TimberbornTreeBurnConsequenceResult(TimberbornTreeBurnConsequenceStatus.Applied);
    }

    private TimberbornTreeBurnConsequenceResult ApplyYieldLoss(TimberbornTreeBurnConsequence consequence)
    {
        _logSink.Info(
            "wildfire_timberborn_tree_yield_reduce_skipped " +
            "reason=native_cuttable_yield_decrease_triggers_cut " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"spec_id={TimberbornQaCommandBridge.FormatToken(consequence.SpecId)} " +
            $"requested_yield_loss={consequence.YieldLost} remaining_yield={consequence.RemainingYield}");
        return new TimberbornTreeBurnConsequenceResult(TimberbornTreeBurnConsequenceStatus.Unavailable);
    }

    private TimberbornTreeBurnConsequenceResult ApplyKillTree(TimberbornTreeBurnConsequence consequence, BlockObject blockObject)
    {

        if (!TryGetTreeComponent(
                blockObject,
                consequence.TargetKey.StableId,
                consequence.SpecId,
                "LivingNaturalResource",
                out LivingNaturalResource livingNaturalResource))
        {
            _logSink.Warning(
                $"{TimberbornRuntimeBurnedTextureBehavior.TreeKillAlreadyTerminalToken} " +
                "reason=missing_living_natural_resource " +
                $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
                $"spec_id={TimberbornQaCommandBridge.FormatToken(consequence.SpecId)} " +
                $"damage_taken={consequence.DamageTaken} damage_capacity={consequence.DamageCapacity}");
            return new TimberbornTreeBurnConsequenceResult(TimberbornTreeBurnConsequenceStatus.Unavailable);
        }

        var result = ApplyNativeDeath(livingNaturalResource);
        if (!result.Applied) return result;
        _logSink.Info(
            "wildfire_timberborn_tree_killed_by_fire " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(TextureLabel(consequence, blockObject))} " +
            $"damage_taken={consequence.DamageTaken} damage_capacity={consequence.DamageCapacity}");
        return new TimberbornTreeBurnConsequenceResult(TimberbornTreeBurnConsequenceStatus.Applied);
    }

    internal static TimberbornTreeBurnConsequenceResult ApplyNativeDeath(LivingNaturalResource resource)
    {
        if (resource.IsDead) return new(TimberbornTreeBurnConsequenceStatus.AlreadySatisfied);
        resource.Die(); // Native state changes before Died callbacks; exceptions must propagate unchanged.
        return new(TimberbornTreeBurnConsequenceStatus.Applied);
    }

    private TimberbornTreeBurnConsequenceResult ApplyBurnedVisual(TimberbornTreeBurnConsequence consequence, BlockObject blockObject)
    {

        string textureLabel = TextureLabel(consequence, blockObject);
        int updatedMaterialCount = ApplyBurnedTextures(blockObject, textureLabel);
        if (updatedMaterialCount == 0)
        {
            throw new InvalidOperationException(
                $"Tree burned visual produced no material updates for {consequence.TargetKey.StableId} ({textureLabel}).");
        }

        _logSink.Info(
            "wildfire_timberborn_tree_burned_texture_applied " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(textureLabel)} " +
            $"materials={updatedMaterialCount}");
        return new TimberbornTreeBurnConsequenceResult(TimberbornTreeBurnConsequenceStatus.Applied);
    }

    private TimberbornTreeBurnConsequenceResult ApplyBurnedLeftover(TimberbornTreeBurnConsequence consequence, BlockObject blockObject)
    {

        if (!TryGetTreeComponent(
                blockObject,
                consequence.TargetKey.StableId,
                consequence.SpecId,
                "Cuttable",
                out Cuttable cuttable))
        {
            _logSink.Warning(
                $"{TimberbornRuntimeBurnedTextureBehavior.TreeBurnedLeftoverAlreadyTerminalToken} " +
                "reason=missing_cuttable_unavailable " +
                $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
                $"spec_id={TimberbornQaCommandBridge.FormatToken(consequence.SpecId)} " +
                $"damage_taken={consequence.DamageTaken} damage_capacity={consequence.DamageCapacity}");
            return new TimberbornTreeBurnConsequenceResult(TimberbornTreeBurnConsequenceStatus.Unavailable);
        }

        InvokeNoArgumentMethod(cuttable, "Cut");
        RequireCompoundOrigin(consequence, blockObject);
        if (TryGetTreeComponent(
                blockObject,
                consequence.TargetKey.StableId,
                consequence.SpecId,
                "GoodStack",
                out GoodStack goodStack))
        {
            foreach (GoodAmount goodAmount in goodStack.Inventory.UnreservedTakeableStock().ToArray())
            {
                RequireCompoundComponent(consequence, blockObject, goodStack);
                TimberbornInventoryMutations.Consume(goodStack.Inventory, goodAmount);
            }

            RequireCompoundComponent(consequence, blockObject, goodStack);
            InvokeNoArgumentMethod(goodStack, "DisableGoodStack");
        }
        string textureLabel = TextureLabel(consequence, blockObject);
        RequireCompoundOrigin(consequence, blockObject);
        bool modelRefreshed = TryRefreshNaturalResourceModel(blockObject, consequence, out string modelRefreshReason);
        RequireCompoundComponent(consequence, blockObject, cuttable);
        InvokeNoArgumentMethod(cuttable, "ShowLeftoverModel");
        RequireCompoundOrigin(consequence, blockObject);
        bool leftoverModelActive = IsInLeftoverState(cuttable);
        int updatedMaterialCount = modelRefreshed && leftoverModelActive
            ? ApplyBurnedTextures(blockObject, textureLabel)
            : 0;
        if (!modelRefreshed || !leftoverModelActive)
        {
            string reason = modelRefreshed
                ? "leftover_model_inactive_after_refresh"
                : modelRefreshReason;
            throw new InvalidOperationException(
                $"Tree burned leftover model did not become active for {consequence.TargetKey.StableId}: {reason}.");
        }

        if (updatedMaterialCount == 0)
        {
            RequireCompoundOrigin(consequence, blockObject);
            updatedMaterialCount = ApplyCharredTintToActive(blockObject);
            if (updatedMaterialCount == 0 && !HasBurnedMaterial(blockObject))
            {
                throw new InvalidOperationException(
                    $"Tree burned leftover visual produced no material updates for {consequence.TargetKey.StableId} ({textureLabel}).");
            }
        }

        _logSink.Info(
            "wildfire_timberborn_tree_burned_leftover_applied " +
            $"stable_id={TimberbornQaCommandBridge.FormatToken(consequence.TargetKey.StableId)} " +
            $"target={TimberbornQaCommandBridge.FormatToken(textureLabel)} " +
            $"materials={updatedMaterialCount} model_refreshed=true leftover_model_active=true");
        return new TimberbornTreeBurnConsequenceResult(TimberbornTreeBurnConsequenceStatus.Applied);
    }

    private void RequireCompoundOrigin(TimberbornTreeBurnConsequence consequence, BlockObject expected)
    {
        if (!TryResolveTree(consequence.EntityId, out var current) || !ReferenceEquals(current, expected))
            throw new InvalidOperationException("Native tree origin disappeared during a compound leftover action; completion is unknown.");
    }

    private void RequireCompoundComponent<T>(TimberbornTreeBurnConsequence consequence, BlockObject owner, T expected)
        where T : class
    {
        RequireCompoundOrigin(consequence, owner);
        if (!owner.TryGetComponent(out T current) || !ReferenceEquals(current, expected))
            throw new InvalidOperationException("Native tree component changed during a compound leftover action; completion is unknown.");
    }

    private static bool HasBurnedMaterial(BlockObject blockObject)
    {
        return blockObject.Transform
            .GetComponentsInChildren<Renderer>(includeInactive: false)
            .Any(static renderer => renderer.sharedMaterials
                .Any(static material => material is not null && IsBurnedMaterial(material)));
    }

    private int ApplyCharredTintToActive(BlockObject blockObject)
    {
        return blockObject.Transform
            .GetComponentsInChildren<Renderer>(includeInactive: false)
            .Sum(static renderer => ApplyCharredTint(renderer));
    }

    private static int ApplyCharredTint(Renderer renderer)
    {
        Material?[] materials = renderer.sharedMaterials;
        int count = 0;
        Material?[] updated = materials.Select(material =>
        {
            if (material is null || IsBurnedMaterial(material))
            {
                return material;
            }

            string? tintProp = TintPropertyNames.FirstOrDefault(p => material.HasProperty(p));
            if (tintProp is null)
            {
                return material;
            }

            Material tinted = new(material)
            {
                name = $"{material.name} Wildfire Burned",
                hideFlags = HideFlags.HideAndDontSave,
            };
            tinted.SetColor(tintProp, CharredTintColor);
            count++;
            return tinted;
        }).ToArray();

        if (count > 0)
        {
            renderer.sharedMaterials = updated;
        }

        return count;
    }

    private static bool IsInLeftoverState(Cuttable cuttable)
    {
        try
        {
            System.Reflection.FieldInfo? field = cuttable.GetType().GetField(
                "_leftoverModel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field?.GetValue(cuttable) is GameObject leftoverModel)
            {
                return leftoverModel.activeSelf;
            }
        }
        catch
        {
            // ignore reflection failures
        }

        return false;
    }

    private bool TryRefreshNaturalResourceModel(
        BlockObject blockObject,
        TimberbornTreeBurnConsequence consequence,
        out string reason)
    {
        object? naturalResourceModel = TryGetTreeComponent(
                blockObject,
                consequence.TargetKey.StableId,
                consequence.SpecId,
                "NaturalResourceModel",
                out NaturalResourceModel typedNaturalResourceModel)
            ? typedNaturalResourceModel
            : blockObject.Transform
                .GetComponentsInChildren<Component>(includeInactive: true)
                .FirstOrDefault(static component =>
                    component is not null &&
                    component.GetType().FullName == "Timberborn.NaturalResourcesModelSystem.NaturalResourceModel");
        if (naturalResourceModel is null)
        {
            reason = "natural_resource_model_missing";
            return false;
        }

        InvokeNoArgumentMethod(naturalResourceModel, "ShowCurrentModel");
        reason = "refreshed";
        return true;
    }

    private int ApplyBurnedTextures(BlockObject blockObject, string textureLabel)
    {
        return GetLiveRenderers(blockObject)
            .Sum(renderer => ApplyBurnedTextures(renderer, textureLabel));
    }

    private int ApplyBurnedTextures(Renderer renderer, string textureLabel)
    {
        try
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
        catch (Exception exception) when (TimberbornRuntimeBurnedTextureBehavior.ShouldSkipInvalidRendererOrMaterial(exception))
        {
            _logSink.Warning(
                $"{TimberbornRuntimeBurnedTextureBehavior.TreeBurnedTextureRendererSkippedToken} " +
                $"reason=renderer_invalid target={TimberbornQaCommandBridge.FormatToken(textureLabel)} " +
                $"message={TimberbornQaCommandBridge.FormatToken(exception.Message)}");
            return 0;
        }
    }

    private Material? CreateBurnedMaterialOrOriginal(Material? source, string textureLabel)
    {
        if (source == null || IsBurnedMaterial(source))
        {
            return source;
        }

        try
        {
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
        catch (Exception exception) when (TimberbornRuntimeBurnedTextureBehavior.ShouldSkipInvalidRendererOrMaterial(exception))
        {
            _logSink.Warning(
                $"{TimberbornRuntimeBurnedTextureBehavior.TreeBurnedTextureMaterialSkippedToken} " +
                $"reason=material_invalid target={TimberbornQaCommandBridge.FormatToken(textureLabel)} " +
                $"message={TimberbornQaCommandBridge.FormatToken(exception.Message)}");
            return source;
        }
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
        return material != null && material.name.EndsWith(" Wildfire Burned", StringComparison.Ordinal);
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

    private Renderer[] GetLiveRenderers(BlockObject blockObject)
    {
        try
        {
            Transform transform = blockObject.Transform;
            if (transform == null)
            {
                return Array.Empty<Renderer>();
            }

            return transform
                .GetComponentsInChildren<Renderer>(includeInactive: true)
                .Where(static renderer => renderer != null)
                .ToArray();
        }
        catch (Exception exception) when (exception is MissingReferenceException or NullReferenceException)
        {
            _logSink.Warning(
                "wildfire_timberborn_tree_burned_texture_skipped " +
                "reason=target_transform_invalid " +
                $"target={TimberbornQaCommandBridge.FormatToken(blockObject != null ? blockObject.Name : "unknown")} " +
                $"message={TimberbornQaCommandBridge.FormatToken(exception.Message)}");
            return Array.Empty<Renderer>();
        }
    }

    private static string StableTreeTargetId(BlockObject blockObject) => TimberbornBurnDamageIdentity.ForEntity(
        blockObject.GetComponent<EntityComponent>().EntityId, NativeBurnTargetFamily.Tree);

    public bool IsLive(Guid entityId) => TryResolveTree(entityId, out _);

    private bool TryResolveTree(Guid entityId, out BlockObject blockObject)
    {
        blockObject = null!;
        EntityComponent? entity = _entities.GetEntity(entityId);
        if (entity is null || entity.Deleted || !entity.Initialized || !entity)
            return false;
        if (entity.EntityId != entityId)
            throw new InvalidOperationException("Native tree registry returned a different entity identity.");
        if (!entity.TryGetComponent(out blockObject) || !TimberbornEntityComponentCells.IsTreeName(blockObject.Name))
            throw new InvalidOperationException("Native tree origin no longer has its registered tree component family.");
        return true;
    }

    private static string TextureLabel(TimberbornTreeBurnConsequence consequence, BlockObject blockObject)
    {
        return string.IsNullOrWhiteSpace(consequence.SpecId)
            ? blockObject.Name
            : consequence.SpecId;
    }

    private bool TryGetTreeComponent<T>(
        BlockObject blockObject,
        string stableId,
        string specId,
        string componentName,
        out T component)
    {
        try
        {
            return blockObject.TryGetComponent(out component);
        }
        catch (Exception exception) when (TimberbornRuntimeBurnedTextureBehavior.ShouldSkipStaleTreeComponentProbe(exception))
        {
            component = default!;
            _logSink.Warning(
                $"{TimberbornRuntimeBurnedTextureBehavior.TreeComponentProbeFailedToken} " +
                $"component={TimberbornQaCommandBridge.FormatToken(componentName)} " +
                $"stable_id={TimberbornQaCommandBridge.FormatToken(stableId)} " +
                $"spec_id={TimberbornQaCommandBridge.FormatToken(specId)} " +
                $"message={TimberbornQaCommandBridge.FormatToken(exception.Message)}");
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

    private static InvalidOperationException MissingTreeComponent(
        TimberbornTreeBurnConsequence consequence,
        string componentName)
    {
        return new InvalidOperationException(
            $"Tree burn consequence requires {componentName} for {consequence.TargetKey.StableId} ({consequence.SpecId}).");
    }
}

