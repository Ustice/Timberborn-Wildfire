using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using Wildfire.Core;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Ash;

// Intentionally unbound: native planted-cell mapping and current-world readiness still need engine proof.
internal sealed class TimberbornAshGrowthTick : ITickableSingleton, ILateTickable
{
    private readonly IDayNightCycle _clock;
    private readonly NativeResourceCoordinator _resources;
    private readonly TimberbornGrowableAshGrowthAdapter _growth;
    private readonly Func<TimberbornAshGrowthObservation?> _readReady;

    internal TimberbornAshGrowthTick(IDayNightCycle clock, NativeResourceCoordinator resources,
        TimberbornGrowableAshGrowthAdapter growth, Func<TimberbornAshGrowthObservation?> readReady) =>
        (_clock, _resources, _growth, _readReady) = (clock, resources, growth, readReady);

    internal TimberbornAshGrowthApplicationResult LastApplication { get; private set; }

    public void Tick()
    {
        var prepared = _resources.CaptureAtRest(() =>
        {
            var observation = _readReady();
            if (observation is null) return null;
            float elapsedDays = _clock.FixedDeltaTimeInHours / 24f;
            TimberbornAshGrowthRate.Validate(elapsedDays, 1, 1);
            if (elapsedDays == 0) return null;
            return _growth.Prepare(observation.Grid, elapsedDays, observation.Requests);
        });
        if (prepared is null) return;
        _resources.TransferInventory(() => LastApplication = prepared.Apply());
    }
}

internal sealed class TimberbornAshGrowthObservation
{
    internal FireGrid Grid { get; }
    internal IReadOnlyList<TimberbornAshGrowthBonusRequest> Requests { get; }
    internal TimberbornAshGrowthObservation(FireGrid grid, IEnumerable<TimberbornAshGrowthBonusRequest> requests) =>
        (Grid, Requests) = (grid, Array.AsReadOnly(requests.ToArray()));
}
