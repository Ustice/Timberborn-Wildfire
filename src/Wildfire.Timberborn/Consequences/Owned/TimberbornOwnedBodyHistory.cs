namespace Wildfire.Timberborn.Consequences;

public sealed partial class TimberbornBurnDamageService
{
    internal OwnedBodyAccountingProfile CaptureOwnedProfile(TimberbornBurnDamageTargetKey key)
    {
        var state = _states[key]; var descriptor = _registrations[key].Descriptor;
        return new(state.SpecId, state.TargetKind, state.MaterialKind, state.DamageCapacity, state.FuelValue,
            state.Flammability, state.MissingResourceIds, state.AccountedResourceIds, descriptor.BurnableProfile,
            descriptor.ResourceYields, descriptor.ConstructionResources);
    }

    internal void RestoreOwnedBodyHistory(TimberbornOwnedConsequenceSnapshot history, TimberbornConsequencePersistenceSnapshot damage)
    {
        var retained = history.Owners.Where(owner => owner.Retention == OwnedBodyRetention.RetainedBody).ToArray();
        if (_states.Count != retained.Length || retained.Any(owner => !_states.ContainsKey(owner.TargetKey) ||
            !owner.Profile!.Matches(CaptureOwnedProfile(owner.TargetKey))))
            throw new ArgumentException("Restored native body definition differs from the complete saved accounting profile.");
        // Whole-envelope validation has already checked exact membership/ranges; no legacy clamping.
        foreach (var entry in damage.BurnDamageStates)
        {
            var key = new TimberbornBurnDamageTargetKey(entry.TargetKey);
            _states[key] = _states[key] with { DamageTaken = entry.DamageTaken, LastDamagedTick = entry.LastDamagedTick };
        }
    }
}
