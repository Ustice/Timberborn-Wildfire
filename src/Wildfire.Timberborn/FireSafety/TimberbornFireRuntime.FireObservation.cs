using Wildfire.Timberborn.FireSafety;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Runtime;

public sealed partial class TimberbornFireRuntime
{
    private readonly NativeResourceCoordinator _resources;
    private TimberbornFireSystem? _fireObservedSystem;
    private long _fireObservedRevision = -1;
    private FireFieldObservation? _fireObservation;
    private ITimberbornCellFieldReader? _observedCellsReader;
    private ITimberbornTransportFieldReader? _observedTransportReader;
    internal long FireObservationRevision => _resources.FieldRevision;
    internal bool FireResponseEnabled => IsAutoDispatchEnabled() && !_resources.IsIndeterminate;

    internal bool TryObserveFireField(out FireFieldObservation field)
    {
        field = null!;
        if (_resources.IsIndeterminate || InitializationState != TimberbornRuntimeInitializationState.Ready || _fireSystem is null) return false;
        if (_fireObservedSystem != _fireSystem || _fireObservedRevision != _resources.FieldRevision)
        {
            _fireObservation = new FireFieldObservation(_fireSystem.Width!.Value, _fireSystem.Height!.Value,
                _fireSystem.Depth!.Value, _observedCellsReader!.ReadFireCells(), _observedTransportReader!.ReadTransportFields());
            _fireObservedSystem = _fireSystem;
            _fireObservedRevision = _resources.FieldRevision;
        }
        if (_fireObservation is null) return false;
        field = _fireObservation;
        return true;
    }
}
