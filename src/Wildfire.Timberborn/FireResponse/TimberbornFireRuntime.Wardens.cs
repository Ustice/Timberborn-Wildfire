using Wildfire.Timberborn.FireResponse;

namespace Wildfire.Timberborn.Runtime;

public sealed partial class TimberbornFireRuntime
{
    private readonly WardenDeliveryService _wardenDelivery;
    private TimberbornFireSystem? _wardenObservedSystem;
    private long _wardenObservedRevision = -1;
    private TimberbornFireSimPersistenceSnapshot? _wardenField;

    internal bool TryObserveWardenField(out TimberbornFireSimPersistenceSnapshot field)
    {
        field = null!;
        if (_wardenDelivery.IsIndeterminate || InitializationState != TimberbornRuntimeInitializationState.Ready || _fireSystem is null) return false;
        if (_wardenObservedSystem != _fireSystem || _wardenObservedRevision != _wardenDelivery.FieldRevision)
        {
            _wardenField = _fireSystem.CapturePersistentFireSimState();
            _wardenObservedSystem = _fireSystem;
            _wardenObservedRevision = _wardenDelivery.FieldRevision;
        }
        if (_wardenField is null) return false;
        field = _wardenField;
        return true;
    }
}
