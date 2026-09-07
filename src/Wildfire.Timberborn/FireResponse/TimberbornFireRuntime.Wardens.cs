namespace Wildfire.Timberborn.Runtime;

public sealed partial class TimberbornFireRuntime
{
    private TimberbornFireSystem? _wardenObservedSystem;
    private uint? _wardenObservedTick;
    private TimberbornFireSimPersistenceSnapshot? _wardenField;

    internal bool TryObserveWardenField(out TimberbornFireSimPersistenceSnapshot field)
    {
        field = null!;
        if (InitializationState != TimberbornRuntimeInitializationState.Ready || _fireSystem is null) return false;
        if (_wardenObservedSystem != _fireSystem || _wardenObservedTick != _fireSystem.LastTick)
        {
            _wardenField = _fireSystem.CapturePersistentFireSimState();
            _wardenObservedSystem = _fireSystem;
            _wardenObservedTick = _fireSystem.LastTick;
        }
        if (_wardenField is null) return false;
        field = _wardenField;
        return true;
    }
}
