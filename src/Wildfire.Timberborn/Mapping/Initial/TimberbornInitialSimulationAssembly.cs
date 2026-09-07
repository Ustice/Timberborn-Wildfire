using Wildfire.Core;

namespace Wildfire.Timberborn.Mapping;

internal static class TimberbornInitialSimulationAssembly
{
    internal static FireSimSnapshot Build(TimberbornNativeMaterialRegistry registry,
        TimberbornInitialEnvironmentProjection environment, TimberbornInitialOwnedPlan plan)
    {
        var grid = environment.MaterialBaseline.Grid;
        var cells = new ushort[grid.CellCount]; var companions = new uint[grid.CellCount];
        var targets = new uint[grid.CellCount]; var slots = new uint[grid.CellCount];
        var known = new HashSet<FireSimMaterialIdentity>();
        for (int cell = 0; cell < grid.CellCount; cell++)
        {
            var resolved = registry.ResolveCell(cell);
            cells[cell] = resolved.PackedDefinition; // Profile defaults are not the physical terrain domain or initial wetness.
            companions[cell] = WildfireMaterialFieldState.FromMaterialProfile(resolved.Profile).Pack();
            if (resolved.Owner is not { } owner) continue;
            targets[cell] = owner.TargetId; slots[cell] = owner.SlotId;
            known.Add(new(owner.TargetId, owner.SlotId));
        }
        var fields = environment.OverlayInitialFields(grid, cells, companions);
        return FireSimSnapshotValidation.ValidateAndClone(new(FireSimSnapshot.CurrentVersion, grid, 0, plan.Parameters, plan.Seed,
            fields.Cells, new uint[grid.CellCount], fields.CompanionFields, targets, slots,
            new(0, known.OrderBy(identity => identity.TargetId).ThenBy(identity => identity.SlotId).ToArray(), Array.Empty<FireSimMaterialArchiveSnapshot>()),
            Array.Empty<FireSimChange>()));
    }

}
