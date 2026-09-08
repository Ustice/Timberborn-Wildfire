namespace Wildfire.Timberborn.Consequences;

public sealed partial class TimberbornBurnDamageService
{
    internal OwnedBodyAccountingProfile CaptureOwnedProfile(TimberbornBurnDamageTargetKey key)
    {
        var state = _states[key];
        var descriptor = _registrations[key].Descriptor;
        return new(state.SpecId, state.TargetKind, state.MaterialKind, state.DamageCapacity, state.FuelValue,
            state.Flammability, state.MissingResourceIds, state.AccountedResourceIds, descriptor.BurnableProfile,
            descriptor.ResourceYields, descriptor.ConstructionResources);
    }

    internal static TimberbornBurnDamageService CreateFromSavedOwnedDefinitions(Wildfire.Core.FireGrid grid,
        TimberbornOwnedConsequenceSnapshot history, TimberbornConsequencePersistenceSnapshot damage,
        IReadOnlyList<TimberbornInitialMaterialBody> facts)
        => CreateFromObservedDefinitions(grid, history, damage, facts,
            new OwnedNativeDefinitionSet(facts.Select(OwnedNativeDefinitionWitness.Capture)));

    internal static TimberbornBurnDamageService CreateFromCompleteSavedOwnedDefinitions(Wildfire.Core.FireGrid grid,
        TimberbornOwnedConsequenceSnapshot history, TimberbornConsequencePersistenceSnapshot damage,
        IReadOnlyList<TimberbornInitialMaterialBody> facts, TimberbornInventoryDeclarationCapture inventories)
    {
        inventories.RequireOwners(facts.Select(body => body.EntityId));
        return CreateFromObservedDefinitions(grid, history, damage, facts,
            OwnedNativeDefinitionSet.WithInventoryDeclarations(facts.Select(body =>
                OwnedNativeDefinitionWitness.Capture(body, inventories.Get(body.EntityId)))));
    }

    private static TimberbornBurnDamageService CreateFromObservedDefinitions(Wildfire.Core.FireGrid grid,
        TimberbornOwnedConsequenceSnapshot history, TimberbornConsequencePersistenceSnapshot damage,
        IReadOnlyList<TimberbornInitialMaterialBody> facts, OwnedNativeDefinitionSet observed)
    {
        var definitions = history.NativeDefinitions ?? throw new NotSupportedException("Saved native definition evidence is unavailable.");
        var retained = history.Owners.Where(o => o.Retention == OwnedBodyRetention.RetainedBody).ToArray();
        if (facts.Count != retained.Length || facts.Select(f => f.EntityId).Distinct().Count() != facts.Count)
            throw new ArgumentException("Restore requires exactly one current fact record for each retained native owner.");
        var byId = facts.ToDictionary(f => f.EntityId);
        var registrations = new List<TimberbornBurnDamageTargetRegistration>();
        foreach (var owner in retained)
        {
            var profile = owner.Profile!;
            if (!byId.TryGetValue(owner.EntityId, out var body) || body.SpecId != profile.SpecId || body.Family != owner.Family ||
                body.PhysicalBodyKind != profile.TargetKind || !definitions.Get(owner.EntityId).Matches(observed.Get(body.EntityId)))
                throw new ArgumentException("A required native body's static definition differs from its saved witness.");
            var descriptor = new TimberbornBurnDamageDescriptor(profile.SpecId, profile.TargetKind, profile.MaterialKind,
                profile.ResourceYields, profile.ConstructionResources, profile.BurnableProfile);
            var cells = body.Footprint.Select(slot =>
            {
                var c = grid.FromIndex(slot.CellIndex);
                return new TimberbornCellCoordinates(c.X, c.Y, c.Z);
            }).ToArray();
            registrations.Add(new(owner.TargetKey, profile.SpecId, cells, 0, descriptor));
        }
        var restored = new TimberbornBurnDamageService(new TimberbornBurnDamageDescriptorCatalog(Array.Empty<TimberbornBurnDamageDescriptor>()));
        restored.RegisterTargets(grid, registrations);
        restored.RestoreOwnedBodyHistory(history, damage);
        return restored;
    }

    private void RestoreOwnedBodyHistory(TimberbornOwnedConsequenceSnapshot history, TimberbornConsequencePersistenceSnapshot damage)
    {
        var retained = history.Owners.Where(owner => owner.Retention == OwnedBodyRetention.RetainedBody).ToArray();
        if (_states.Count != retained.Length || retained.Any(owner => !_states.ContainsKey(owner.TargetKey) ||
            !owner.Profile!.Matches(CaptureOwnedProfile(owner.TargetKey))))
            throw new ArgumentException("Restored native body definition differs from the complete saved accounting profile.");
        // Whole-envelope validation has already checked exact membership/ranges; no legacy clamping.
        foreach (var entry in damage.BurnDamageStates)
        {
            var key = new TimberbornBurnDamageTargetKey(entry.TargetKey);
            _states[key] = _states[key] with
            {
                DamageTaken = entry.DamageTaken,
                LastDamagedTick = entry.LastDamagedTick
            };
        }
    }
}
