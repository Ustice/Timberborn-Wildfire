using Timberborn.Cutting;

namespace Wildfire.Timberborn.Visuals;

public sealed class TimberbornOwnedPresentationApi : ITimberbornOwnedPresentationApi
{
    private readonly TimberbornTextureTreeBurnConsequenceApi _trees;
    private readonly TimberbornTextureCropBurnConsequenceApi _crops;
    public TimberbornOwnedPresentationApi(TimberbornTextureTreeBurnConsequenceApi trees, TimberbornTextureCropBurnConsequenceApi crops)
    { _trees = trees ?? throw new ArgumentNullException(nameof(trees)); _crops = crops ?? throw new ArgumentNullException(nameof(crops)); }
    public OwnedPresentationResult Rehydrate(Guid id, NativeBurnTargetFamily family, OwnedCharredPresentation desired)
    {
        if (id == Guid.Empty || desired is not (OwnedCharredPresentation.BurnedBody or OwnedCharredPresentation.BurnedLeftover))
            throw new ArgumentException("Invalid owned presentation request.");
        return family switch
        {
            NativeBurnTargetFamily.Tree => _trees.RehydrateOwnedPresentation(id, desired),
            NativeBurnTargetFamily.Crop => _crops.RehydrateOwnedPresentation(id, desired),
            _ => throw new ArgumentException("Only natural owners have charred presentation."),
        };
    }
}

public sealed partial class TimberbornTextureTreeBurnConsequenceApi
{
    internal OwnedPresentationResult RehydrateOwnedPresentation(Guid id, OwnedCharredPresentation desired)
    {
        if (!TryResolveTree(id, out var tree)) return OwnedPresentationResult.NotLive;
        // Restore the native model first; never call Cut to manufacture the desired presentation.
        if (desired == OwnedCharredPresentation.BurnedLeftover &&
            (!tree.TryGetComponent(out Cuttable cuttable) || !IsInLeftoverState(cuttable))) return OwnedPresentationResult.Unavailable;
        if (HasBurnedMaterial(tree)) return OwnedPresentationResult.AlreadySatisfied;
        int changed = ApplyBurnedTextures(tree, tree.Name);
        if (changed == 0) changed = ApplyCharredTintToActive(tree);
        return changed > 0 ? OwnedPresentationResult.Applied : OwnedPresentationResult.Unavailable;
    }
}

public sealed partial class TimberbornTextureCropBurnConsequenceApi
{
    internal OwnedPresentationResult RehydrateOwnedPresentation(Guid id, OwnedCharredPresentation desired)
    {
        if (!TryResolveCrop(id, out var crop)) return OwnedPresentationResult.NotLive;
        var result = BurnVisual(crop); // Materials only: never BurnWhole, RemoveYield, Die, or Delete.
        return result.Status switch
        {
            TimberbornCropBurnConsequenceStatus.Applied => OwnedPresentationResult.Applied,
            TimberbornCropBurnConsequenceStatus.AlreadySatisfied => OwnedPresentationResult.AlreadySatisfied,
            _ => OwnedPresentationResult.Unavailable,
        };
    }
}
