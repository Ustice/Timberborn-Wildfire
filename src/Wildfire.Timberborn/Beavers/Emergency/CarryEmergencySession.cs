using Timberborn.WorldPersistence;

namespace Wildfire.Timberborn.Beavers.Emergency;

/// <summary>Game-context lifetime; guards saving even if the affected entity has been removed.</summary>
public sealed class CarryEmergencySession : ISaveableSingleton
{
    public const string OptInSwitch = "--wildfire-enable-carry-emergency";
    public bool AdmissionsEnabled { get; } = Environment.GetCommandLineArgs().Contains(OptInSwitch, StringComparer.Ordinal);
    public CarryEmergencySafety Safety { get; } = new();
    private CarryEmergencyBehaviorAccess? _access;
    public CarryEmergencyBehaviorAccess Access => _access ??= CarryEmergencyBehaviorAccess.CreateVerified();
    public void Save(ISingletonSaver saver) => Safety.ThrowIfSaveUnsafe();
}
