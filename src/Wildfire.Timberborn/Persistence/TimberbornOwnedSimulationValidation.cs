using Wildfire.Core;

namespace Wildfire.Timberborn.Persistence;

internal static class TimberbornOwnedSimulationValidation
{
    // Both snapshots must have passed structural validation. Collection ordering is meaningful only
    // for cells and pending commands; known identities and archives are sets keyed by material identity.
    internal static void RequireUnchanged(FireSimSnapshot expected, FireSimSnapshot actual)
    {
        if (actual.Version != expected.Version || actual.Grid != expected.Grid || actual.Tick != expected.Tick ||
            actual.Parameters != expected.Parameters || actual.Seed != expected.Seed ||
            !actual.Cells.SequenceEqual(expected.Cells) || !actual.CompanionFields.SequenceEqual(expected.CompanionFields) ||
            !actual.TransportFields.SequenceEqual(expected.TransportFields) || !actual.TargetIds.SequenceEqual(expected.TargetIds) ||
            !actual.SlotIds.SequenceEqual(expected.SlotIds) || !actual.PendingChanges.SequenceEqual(expected.PendingChanges) ||
            actual.MaterialAuthority.LastAttemptToken != expected.MaterialAuthority.LastAttemptToken ||
            !actual.MaterialAuthority.KnownSlots.ToHashSet().SetEquals(expected.MaterialAuthority.KnownSlots) ||
            !OrderedArchives(actual).SequenceEqual(OrderedArchives(expected)))
            throw new ArgumentException("New backend did not preserve the complete prepared snapshot.");
    }

    private static IEnumerable<FireSimMaterialArchiveSnapshot> OrderedArchives(FireSimSnapshot snapshot) =>
        snapshot.MaterialAuthority.Archives.OrderBy(archive => archive.Identity.TargetId).ThenBy(archive => archive.Identity.SlotId);
}
