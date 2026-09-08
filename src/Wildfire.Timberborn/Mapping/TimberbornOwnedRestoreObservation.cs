namespace Wildfire.Timberborn.Mapping;

/// <summary>Transient native state evidence. Initial exclusion is recorded, never interpreted as material removal.</summary>
internal sealed class TimberbornRetainedBodyObservation
{
    internal TimberbornRetainedBodyObservation(Guid entityId, TimberbornInitialCaptureExclusion? exclusion,
        bool? isDead, bool supportsRetainedTreeMaterial, IEnumerable<TimberbornInventoryDeclaration> inventories)
    {
        if (entityId == Guid.Empty || exclusion is { } reason && !Enum.IsDefined(typeof(TimberbornInitialCaptureExclusion), reason))
            throw new ArgumentException("Retained observation requires an exact native identity and known state.");
        var roles = inventories.OrderBy(role => role.Role).ToArray();
        if (roles.Select(role => role.Role).Distinct().Count() != roles.Length ||
            roles.Select(role => role.ComponentName).Distinct(StringComparer.Ordinal).Count() != roles.Length)
            throw new ArgumentException("Inventory declaration identities must be unique.");
        EntityId = entityId;
        Exclusion = exclusion;
        IsDead = isDead;
        SupportsRetainedTreeMaterial = supportsRetainedTreeMaterial;
        Inventories = Array.AsReadOnly(roles);
    }
    internal Guid EntityId { get; }
    internal TimberbornInitialCaptureExclusion? Exclusion { get; }
    internal bool? IsDead { get; }
    internal bool SupportsRetainedTreeMaterial { get; }
    internal IReadOnlyList<TimberbornInventoryDeclaration> Inventories { get; }
    internal bool SameReadings(TimberbornRetainedBodyObservation other) => EntityId == other.EntityId &&
        Exclusion == other.Exclusion && IsDead == other.IsDead &&
        SupportsRetainedTreeMaterial == other.SupportsRetainedTreeMaterial && Inventories.SequenceEqual(other.Inventories);
}

/// <summary>One settled native observation, separate from saved accounting and GPU material history.</summary>
internal sealed class TimberbornOwnedRestoreObservation
{
    internal TimberbornOwnedRestoreObservation(TimberbornInitialWorldCapture currentWorld,
        IEnumerable<TimberbornInitialMaterialBody> retainedBodies, IEnumerable<TimberbornRetainedBodyObservation> states)
    {
        CurrentWorld = currentWorld ?? throw new ArgumentNullException(nameof(currentWorld));
        var bodies = retainedBodies.OrderBy(body => body.EntityId).ToArray();
        var readings = states.OrderBy(state => state.EntityId).ToArray();
        if (bodies.Select(body => body.EntityId).Distinct().Count() != bodies.Length ||
            !bodies.Select(body => body.EntityId).SequenceEqual(readings.Select(state => state.EntityId)))
            throw new ArgumentException("Every retained body requires exactly one state observation.");
        RetainedBodies = Array.AsReadOnly(bodies);
        States = Array.AsReadOnly(readings);
        InventoryDeclarations = new(readings.Select(state => new TimberbornBodyInventoryDeclarations(state.EntityId, state.Inventories)));
    }
    internal TimberbornInventoryDeclarationCapture InventoryDeclarations { get; }
    internal TimberbornInitialWorldCapture CurrentWorld { get; }
    internal IReadOnlyList<TimberbornInitialMaterialBody> RetainedBodies { get; }
    internal IReadOnlyList<TimberbornRetainedBodyObservation> States { get; }
    internal bool SameReadings(TimberbornOwnedRestoreObservation other) => CurrentWorld.SameReadings(other.CurrentWorld) &&
        RetainedBodies.Count == other.RetainedBodies.Count &&
        RetainedBodies.Zip(other.RetainedBodies, (a, b) => a.SameReadings(b)).All(equal => equal) &&
        States.Zip(other.States, (a, b) => a.SameReadings(b)).All(equal => equal);

    internal void RequireSupported(IReadOnlyList<Guid> requiredIds)
    {
        if (CurrentWorld.Environment.OwnedDomain is null)
            throw new NotSupportedException("Complete restore requires explicit native total-world domain evidence.");
        if (CurrentWorld.InventoryDeclarations is null)
            throw new NotSupportedException("Complete current world declaration evidence is unavailable.");
        var ids = requiredIds.OrderBy(id => id).ToArray();
        if (!ids.SequenceEqual(RetainedBodies.Select(body => body.EntityId)))
            throw new ArgumentException("Restore requires exact saved retained native membership.");
        if (!ids.SequenceEqual(CurrentWorld.Bodies.Select(body => body.EntityId)))
            throw new NotSupportedException("Current material membership needs explicit new-owner or retained-state admission.");
        // Unfinished/leftover/deleted exclusions can still represent physical material. No automatic
        // omission policy is established here, even for an unregistered native owner.
        if (CurrentWorld.Excluded.Count != 0 || States.Any(state => state.Exclusion is not null))
            throw new NotSupportedException("Excluded or terminal native body state needs an explicit material lifecycle decision.");
        if (!RetainedBodies.Zip(CurrentWorld.Bodies, (a, b) => a.SameReadings(b)).All(equal => equal))
            throw new ArgumentException("Required native facts differ from the full-world observation.");
        if (!InventoryDeclarations.SameReadings(CurrentWorld.InventoryDeclarations))
            throw new ArgumentException("Full-world and retained inventory declarations differ.");
        InventoryDeclarations.RequireSupportedMaterialBodies(RetainedBodies);
        for (int index = 0; index < RetainedBodies.Count; index++)
        {
            var body = RetainedBodies[index];
            var state = States[index];
            // Disabled inventories still contain physical Stock; logistics eligibility belongs to
            // the mutation sink. Disabled Yielder publicly reports zero and needs separate evidence.
            if ((state.IsDead == true || body.Yields.Any(yield => !yield.YieldEnabled)) &&
                (body.Shape != TimberbornInitialBodyShape.Tree || !state.SupportsRetainedTreeMaterial))
                throw new NotSupportedException("Terminal or disabled native yield lacks verified retained-tree material evidence.");
        }
    }

    /// <summary>Derive anew inside the current observation scope; never persist this first-activation permission.</summary>
    internal IReadOnlyCollection<Guid> CaptureFreshEligibleOwners(IReadOnlyList<Guid> requiredIds)
    {
        RequireSupported(requiredIds);
        return Array.AsReadOnly(RetainedBodies.Where((body, index) =>
            IsFreshEligible(body, States[index]))
            .Select(body => body.EntityId).ToArray());
    }

    private static bool IsFreshEligible(TimberbornInitialMaterialBody body, TimberbornRetainedBodyObservation state)
    {
        if (body.Shape != TimberbornInitialBodyShape.Tree)
            return state.IsDead != true && body.Yields.All(yield => yield.YieldEnabled);
        if (state.IsDead != false || !state.SupportsRetainedTreeMaterial) return false;
        // Native first fruit/resin growth can remain disabled after the Cuttable wood is mature.
        // That auxiliary harvest cycle neither removes the trunk nor authorizes its later refill.
        var cuttable = body.Yields.Where(yield => yield.Role == TimberbornCapturedYieldRole.Cuttable).ToArray();
        return cuttable.Length == 1 && cuttable[0].YieldEnabled && cuttable[0].ActualAmount > 0;
    }

}
