using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Consequences;

/// <summary>Legacy dispatcher boundary; owned batch consumers use raw APIs under their single outer guard.</summary>
public sealed class TimberbornLegacyTreeMutationGuard : ITimberbornTreeBurnConsequenceApi
{
    private readonly INativeResourceMutationGuard _guard;
    private readonly ITimberbornTreeBurnConsequenceApi _api;
    public TimberbornLegacyTreeMutationGuard(INativeResourceMutationGuard guard, ITimberbornTreeBurnConsequenceApi api)
    {
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }
    public TimberbornTreeBurnConsequenceResult ApplyConsequence(TimberbornTreeBurnConsequence consequence)
    {
        TimberbornTreeBurnConsequenceResult result = default;
        _guard.TransferInventory(() =>
        {
            result = _api.ApplyConsequence(consequence);
            if (result.Failed) throw new InvalidOperationException("Native tree consequence reported failure.");
        });
        return result;
    }
}

public sealed class TimberbornLegacyCropMutationGuard : ITimberbornCropBurnConsequenceApi
{
    private readonly INativeResourceMutationGuard _guard;
    private readonly ITimberbornCropBurnConsequenceApi _api;
    public TimberbornLegacyCropMutationGuard(INativeResourceMutationGuard guard, ITimberbornCropBurnConsequenceApi api)
    {
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }
    public TimberbornCropBurnConsequenceResult ApplyConsequence(TimberbornCropBurnConsequence consequence)
    {
        TimberbornCropBurnConsequenceResult result = default;
        _guard.TransferInventory(() =>
        {
            result = _api.ApplyConsequence(consequence);
            result.ValidateReceipt();
            if (result.FailedConsequence) throw new InvalidOperationException("Native crop consequence reported failure.");
        });
        return result;
    }
}
