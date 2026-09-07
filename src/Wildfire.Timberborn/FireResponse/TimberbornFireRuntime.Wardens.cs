using Wildfire.Timberborn.Resources;
using Wildfire.Timberborn.FireResponse;

namespace Wildfire.Timberborn.Runtime;

public sealed partial class TimberbornFireRuntime
{
    private readonly NativeResourceCoordinator _resources;
    private TimberbornFireSystem? _wardenObservedSystem;
    private long _wardenObservedRevision = -1;
    private WardenFieldObservation? _wardenField;
    private ITimberbornCellFieldReader? _wardenCells;
    private ITimberbornTransportFieldReader? _wardenTransport;
    internal long WardenFieldRevision => _resources.FieldRevision;
    internal bool WardenResponseEnabled => IsAutoDispatchEnabled() && !_resources.IsIndeterminate;

    internal bool TryObserveWardenField(out WardenFieldObservation field)
    {
        field = null!;
        if (_resources.IsIndeterminate || InitializationState != TimberbornRuntimeInitializationState.Ready || _fireSystem is null) return false;
        if (_wardenObservedSystem != _fireSystem || _wardenObservedRevision != _resources.FieldRevision)
        {
            _wardenField = new WardenFieldObservation(_fireSystem.Width!.Value, _fireSystem.Height!.Value,
                _fireSystem.Depth!.Value, _wardenCells!.ReadFireCells(), _wardenTransport!.ReadTransportFields());
            _wardenObservedSystem = _fireSystem;
            _wardenObservedRevision = _resources.FieldRevision;
        }
        if (_wardenField is null) return false;
        field = _wardenField;
        return true;
    }
}
