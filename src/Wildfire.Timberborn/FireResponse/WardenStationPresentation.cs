namespace Wildfire.Timberborn.FireResponse;

public readonly record struct WardenStationViewState(bool UnsafeWaterState, bool Finished, bool Operational,
    bool ResponseEnabled, int AssignedWorkers, bool Restocking, WardenPhase Phase, WardenResponseReason Reason);

public static class WardenStationPresentation
{
    public static string StatusKey(WardenStationViewState state)
    {
        if (state.UnsafeWaterState) return "Wildfire.Warden.Recovery";
        if (!state.Finished) return "Wildfire.Warden.Construction";
        // Returning remains useful information even after pausing the station or disabling fire.
        if (state.Phase == WardenPhase.Returning) return "Wildfire.Warden.Returning";
        if (!state.Operational) return "Wildfire.Warden.Paused";
        if (!state.ResponseEnabled) return "Wildfire.Warden.Disabled";
        if (state.AssignedWorkers == 0) return "Wildfire.Warden.NoWorker";
        if (state.Restocking) return "Wildfire.Warden.Restocking";
        return state.Phase switch
        {
            WardenPhase.Fetching => "Wildfire.Warden.Fetching",
            WardenPhase.Approaching => "Wildfire.Warden.Approaching",
            WardenPhase.Applying => "Wildfire.Warden.Preparing",
            WardenPhase.AwaitingApplication => "Wildfire.Warden.Applying",
            _ => ReasonKey(state.Reason, "Ready")
        };
    }

    public static string ReasonKey(WardenResponseReason reason, string fallback) =>
        "Wildfire.Warden." + (reason == WardenResponseReason.None ? fallback : reason.ToString());
}
