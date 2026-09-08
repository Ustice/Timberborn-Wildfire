namespace Wildfire.Timberborn.Consequences;

public enum OwnedPresentationResult { NotLive, Applied, AlreadySatisfied, Unavailable }

/// <summary>Only presentation rehydration; no yield, inventory, harvest, drying, or death operation is exposed.</summary>
public interface ITimberbornOwnedPresentationApi
{
    OwnedPresentationResult Rehydrate(Guid entityId, NativeBurnTargetFamily family, OwnedCharredPresentation desired);
}

public sealed partial class TimberbornOwnedDeltaConsumer
{
    public IReadOnlyList<OwnedPresentationResult> RehydratePresentation(ITimberbornOwnedPresentationApi presentation)
    {
        if (presentation is null) throw new ArgumentNullException(nameof(presentation));
        var history = CaptureHistory();
        var owners = history.Owners.ToDictionary(owner => owner.EntityId);
        _consuming = true;
        try
        {
            return history.Natural.Where(progress => progress.DesiredPresentation != OwnedCharredPresentation.None)
                .Select(progress =>
                {
                    var owner = owners[progress.EntityId];
                    var result = presentation.Rehydrate(progress.EntityId, owner.Family, progress.DesiredPresentation);
                    if (!Enum.IsDefined(typeof(OwnedPresentationResult), result)) throw new InvalidOperationException("Invalid presentation receipt.");
                    if (owner.Family == NativeBurnTargetFamily.Tree) _trees.RecordRehydratedPresentation(owner, result);
                    else _crops.RecordRehydratedPresentation(owner, result);
                    return result;
                }).ToArray();
        }
        finally { _consuming = false; }
    }
}
