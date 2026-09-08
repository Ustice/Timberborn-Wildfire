using Wildfire.Core;
using Wildfire.Timberborn.Ash;
using Wildfire.Timberborn.FireSafety;

namespace Wildfire.Timberborn.Runtime;

public sealed partial class TimberbornFireRuntime
{
    private FireFieldObservation? _ashGrowthField;
    private TimberbornAshGrowthObservation? _ashGrowthObservation;

    // Called inside the late ticker's existing CaptureAtRest; never derives readiness from restored ash entries.
    internal TimberbornAshGrowthObservation? ReadAshGrowthObservation()
    {
        if (!IsWildfireEnabled() || !TryObserveFireField(out var field)) return null;
        if (!ReferenceEquals(field, _ashGrowthField))
        {
            var grid = new FireGrid(field.Width, field.Height, field.Depth);
            long count = checked((long)grid.Width * grid.Height * grid.Depth);
            if (grid.Width <= 0 || grid.Height <= 0 || grid.Depth <= 0 || count > int.MaxValue ||
                field.Cells.Count != count || field.TransportFields.Count != count)
                throw new InvalidOperationException("Ash growth requires complete current-world field observations.");
            var requests = new List<TimberbornAshGrowthBonusRequest>();
            for (int cell = 0; cell < count; cell++)
            {
                var state = WildfireTransportFieldState.Unpack(field.TransportFields[cell]);
                if (state.Ash > TimberbornAshFieldService.MaxStrength)
                    throw new InvalidOperationException("Current ash strength exceeds the simulator application range.");
                if (state.Ash == 0 || state.AshContamination != 0) continue;
                var entry = new TimberbornAshFieldEntry(cell, WildfireAshQuality.Fertile, state.Ash,
                    TimberbornAshSourceKind.Unknown, 0, 0, TimberbornAshFieldEntry.CurrentPersistenceVersion);
                requests.Add(new(cell, entry.GrowthMultiplier(), entry.Quality, entry.Strength));
            }
            // Lists/readers/settings are adapter boundaries. Recheck authority before publishing derived data.
            if (!IsWildfireEnabled() || !FieldWorldReady() || !ReferenceEquals(field, _fireObservation) ||
                !ReferenceEquals(_fireObservedSystem, _fireSystem) || _fireObservedRevision != _resources.FieldRevision)
                return null;
            _ashGrowthObservation = new(grid, requests);
            _ashGrowthField = field;
        }
        return _ashGrowthObservation;
    }
}
