namespace Wildfire.Timberborn.FireResponse;

// Player-facing reasons are typed independently of detailed diagnostic log messages.
public enum WardenResponseReason
{
    None, Unavailable, OtherWork, NoAccess, NoSafeFire, NoWater, UnsafeRoute,
    Disabled, Interrupted, TargetGone, TargetUnavailable, Applied, Complete, TimedOut, WaterRetained, NoSafeReturn
}
