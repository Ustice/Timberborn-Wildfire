using Timberborn.InventorySystem;
using UnityEngine;
using Wildfire.Core;
using Wildfire.Timberborn.FireSafety;

namespace Wildfire.Timberborn.FireBell;

/// <summary>One explicit QA intent; native source and equipment own all quantities.</summary>
internal sealed record BorrowedWaterTrip(NaturalWaterSourceQaFactory Sources, TimberbornNaturalWaterSource Source,
    Vector3 Shore, int FireCell, Vector3 Approach, Inventory ReturnInventory)
{
    internal bool SourceUsable => Sources.IsUsable(Source, Shore);
    internal bool TargetUsable(FireSafetyField field)
    {
        if (!field.Ready || !field.TryObserve(out var observation) || !field.IsBurning(FireCell)) return false;
        var coordinate = new FireGrid(observation.Width, observation.Height, observation.Depth).FromIndex(FireCell);
        var center = new Vector3(coordinate.X + .5f, coordinate.Z, coordinate.Y + .5f);
        // Explicit finite fixture uses the already-proven Warden working-distance envelope.
        // This is not Folktails coverage or automatic target selection.
        return Approach.y == center.y &&
            (Approach - center).sqrMagnitude <= 4 && field.SafePosition(Approach);
    }
}
