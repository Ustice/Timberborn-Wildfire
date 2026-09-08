using Wildfire.Core;
using Wildfire.Timberborn.Persistence;

namespace Wildfire.Timberborn.Mapping;

/// <summary>
/// Derive one whole material transition from current desired projections and one complete GPU checkpoint.
/// Caller holds the shared native guard and validates native membership/state before execution. This
/// planner neither executes a step nor certifies native eligibility, profile changes or readiness.
/// </summary>
internal static class TimberbornDesiredMaterialReconciliation
{
    // Current static profile fields: terrain/flammability and class/capacity/ash-quality/behavior.
    // Fuel, burning, burn history, heat, water, ash strength and soil are deliberately excluded.
    private const uint StaticCellMask = 0x1300u;
    private const uint StaticCompanionMask = FireSimMaterialHandoffProtocol.CompanionMaterialMask & ~0xf000u;

    internal static FireSimMaterialHandoffBatch? Plan<TSimulator>(TimberbornNativeMaterialRegistry registry,
        IReadOnlyDictionary<Guid, OwnedBodyRetention> canonicalOwners, TSimulator simulator)
        where TSimulator : IFireSimMaterialHandoffSimulator, IFireSimSnapshotSimulator
    {
        if (registry is null) throw new ArgumentNullException(nameof(registry));
        if (canonicalOwners is null) throw new ArgumentNullException(nameof(canonicalOwners));
        if (simulator is null) throw new ArgumentNullException(nameof(simulator));
        if (simulator.SnapshotCapability != FireSimSnapshotCapability.CompleteMaterialHistory)
            throw new NotSupportedException("Desired material planning requires complete GPU history.");
        var snapshot = FireSimSnapshotValidation.ValidateAndClone(simulator.CaptureSnapshot());
        if (registry.Grid != snapshot.Grid || snapshot.Grid != new FireGrid(simulator.Width, simulator.Height, simulator.Depth))
            throw new ArgumentException("Desired native and actual simulator grids must have identical dimensions.");
        var bindings = registry.CaptureBindings();
        _ = new TimberbornOwnedMaterialSnapshot(snapshot, bindings);
        var owners = canonicalOwners.ToDictionary(pair => pair.Key, pair => pair.Value);
        if (!owners.Keys.ToHashSet().SetEquals(bindings.Entities.Select(entity => entity.EntityId)) ||
            owners.Any(pair => pair.Key == Guid.Empty || !Enum.IsDefined(typeof(OwnedBodyRetention), pair.Value)))
            throw new ArgumentException("Every retained native binding requires one explicit canonical owner retention.");

        var active = snapshot.TargetIds.Select((target, cell) =>
                (Identity: new FireSimMaterialIdentity(target, snapshot.SlotIds[cell]), Cell: cell))
            .Where(pair => pair.Identity.IsOwned).ToDictionary(pair => pair.Identity, pair => pair.Cell);
        var known = snapshot.MaterialAuthority.KnownSlots.ToHashSet();
        var archives = ReadAgreedArchives(bindings, snapshot, simulator, known);
        var desired = Enumerable.Range(0, snapshot.Grid.CellCount).Select(registry.ResolveCell).ToArray();
        ValidateDesired(desired, owners, registry);

        var pending = new Queue<int>();
        for (int cell = 0; cell < desired.Length; cell++)
        {
            var expected = new FireSimMaterialIdentity(snapshot.TargetIds[cell], snapshot.SlotIds[cell]);
            if (desired[cell].Owner is { } owner)
            {
                if (expected != new FireSimMaterialIdentity(owner.TargetId, owner.SlotId)) pending.Enqueue(cell);
                continue; // Same active slot retains exact GPU state, even when its declared fresh fuel differs.
            }
            var baseline = registry.CreateBaselineRequest(cell, expected);
            if (expected.IsOwned || !SameStaticBaseline(snapshot, baseline)) pending.Enqueue(cell);
        }
        if (pending.Count == 0) return null;

        var requests = new Dictionary<int, FireSimMaterialHandoffRequest>();
        while (pending.TryDequeue(out int cell))
        {
            if (requests.ContainsKey(cell)) continue;
            var expected = new FireSimMaterialIdentity(snapshot.TargetIds[cell], snapshot.SlotIds[cell]);
            if (desired[cell].Owner is not { } owner)
            {
                requests.Add(cell, registry.CreateBaselineRequest(cell, expected));
                continue;
            }
            var incoming = new FireSimMaterialIdentity(owner.TargetId, owner.SlotId);
            if (incoming == expected)
                throw new InvalidOperationException("A captured source cannot remain active at its previous cell.");
            if (!known.Contains(incoming))
                requests.Add(cell, registry.CreateFirstActivationRequest(cell, expected, simulator));
            else if (archives.TryGetValue(incoming, out var archive))
                requests.Add(cell, FireSimMaterialHandoffRequest.RestoreArchived(cell, expected, archive));
            else if (active.TryGetValue(incoming, out int source))
            {
                requests.Add(cell, FireSimMaterialHandoffRequest.RestoreCaptured(cell, expected, incoming, source));
                pending.Enqueue(source); // Include source replacement in the same admission, including swaps.
            }
            else throw new InvalidOperationException("Known incoming material has no authoritative source or archive.");
        }
        return new(checked(snapshot.MaterialAuthority.LastAttemptToken + 1), requests.Values);
    }

    private static bool SameStaticBaseline(FireSimSnapshot snapshot, FireSimMaterialHandoffRequest baseline) =>
        ((uint)snapshot.Cells[baseline.CellIndex] & StaticCellMask) == (baseline.PackedMaterial & StaticCellMask) &&
        (snapshot.CompanionFields[baseline.CellIndex] & StaticCompanionMask) == (baseline.CompanionMaterial & StaticCompanionMask);

    private static void ValidateDesired(IEnumerable<TimberbornResolvedMaterialCell> cells,
        IReadOnlyDictionary<Guid, OwnedBodyRetention> owners, TimberbornNativeMaterialRegistry registry)
    {
        var seen = new HashSet<FireSimMaterialIdentity>();
        var contributingOwners = new HashSet<Guid>();
        foreach (var cell in cells)
        {
            foreach (var contributor in cell.Contributors)
            {
                var owner = contributor.Owner;
                contributingOwners.Add(owner.EntityId);
                if (!owners.TryGetValue(owner.EntityId, out var retention) || retention != OwnedBodyRetention.RetainedBody ||
                    !registry.TryResolveOrigin(owner.TargetId, out var entity) || entity != owner.EntityId ||
                    !registry.IsSlotBound(owner.TargetId, owner.SlotId))
                    throw new InvalidOperationException("Every desired contributor requires its exact retained canonical native binding.");
            }
            if (cell.Owner is { } selected && !seen.Add(new(selected.TargetId, selected.SlotId)))
                throw new InvalidOperationException("A desired material slot cannot occupy two cells.");
        }
        if (!contributingOwners.SetEquals(owners.Where(pair => pair.Value == OwnedBodyRetention.RetainedBody).Select(pair => pair.Key)))
            throw new InvalidOperationException("Every retained native owner requires a desired projection, including wholly hidden owners; absence cannot imply suspension.");
    }

    private static Dictionary<FireSimMaterialIdentity, FireSimMaterialArchive> ReadAgreedArchives<TSimulator>(
        TimberbornMaterialBindingSnapshot bindings, FireSimSnapshot snapshot, TSimulator simulator,
        HashSet<FireSimMaterialIdentity> known) where TSimulator : IFireSimMaterialHandoffSimulator
    {
        var saved = snapshot.MaterialAuthority.Archives.ToDictionary(archive => archive.Identity);
        var available = new Dictionary<FireSimMaterialIdentity, FireSimMaterialArchive>();
        foreach (var identity in bindings.Entities.SelectMany(entity => entity.Slots.Select(slot => new FireSimMaterialIdentity(entity.TargetId, slot.SlotId))))
        {
            bool hasArchive = simulator.TryGetMaterialArchive(identity, out var archive);
            if (simulator.IsSlotKnown(identity) != known.Contains(identity) || hasArchive != saved.ContainsKey(identity))
                throw new InvalidOperationException("Coordinator material authority changed since the captured checkpoint.");
            if (!hasArchive) continue;
            var expected = saved[identity];
            if (archive.Identity != identity || archive.CaptureToken != expected.CaptureToken ||
                archive.SourceCellIndex != expected.SourceCellIndex || archive.PackedCell != expected.PackedCell || archive.Companion != expected.Companion)
                throw new InvalidOperationException("Coordinator archive differs from the complete checkpoint.");
            available.Add(identity, archive); // Keep the coordinator's exact single-use object, not a reconstructed DTO.
        }
        return available;
    }
}
