namespace Wildfire.Timberborn.Consequences;

public sealed partial class TimberbornTreeBurnConsequenceSink
{
    private readonly Dictionary<TimberbornBurnDamageTargetKey, OwnedCharredPresentation> _restoredPresentation = new();
    internal OwnedNaturalProgress CaptureProgress(OwnedConsequenceOwner owner)
    {
        var key = owner.TargetKey;
        var presentation = _leftoverTargets.Contains(key) ? OwnedCharredPresentation.BurnedLeftover :
            _burnedVisualTargets.Contains(key) ? OwnedCharredPresentation.BurnedBody : _restoredPresentation.GetValueOrDefault(key);
        return new(owner.EntityId, _appliedYieldLossByTarget.GetValueOrDefault(key), _driedTargets.Contains(key),
            _killedTargets.Contains(key), _leftoverTargets.Contains(key), presentation);
    }
    internal void RecordRehydratedPresentation(OwnedConsequenceOwner owner, OwnedPresentationResult result)
    {
        if (result is OwnedPresentationResult.Applied or OwnedPresentationResult.AlreadySatisfied)
            _burnedVisualTargets.Add(owner.TargetKey);
    }
    internal void RestoreProgress(OwnedConsequenceOwner owner, OwnedNaturalProgress progress)
    {
        var key = owner.TargetKey;
        if (progress.AppliedYieldLoss > 0) _appliedYieldLossByTarget.Add(key, progress.AppliedYieldLoss);
        if (progress.DryRequestSatisfied) _driedTargets.Add(key);
        if (progress.DeathRequestSatisfied) _killedTargets.Add(key);
        if (progress.LeftoverRequestSatisfied) _leftoverTargets.Add(key);
        _restoredPresentation.Add(key, progress.DesiredPresentation);
        // Applied renderer cache stays empty. Presentation rehydration never replays compound native effects.
    }
}
