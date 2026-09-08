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
        if (!FieldWorldReady()) return false;
        var system = _fireSystem!;
        long revision = _resources.FieldRevision;
        if (_fireObservedSystem != system || _fireObservedRevision != revision)
        {
            var cellsReader = _observedCellsReader;
            var transportReader = _observedTransportReader;
            if (cellsReader is null || transportReader is null) return false;
            var observed = new FireFieldObservation(system.Width!.Value, system.Height!.Value,
                system.Depth!.Value, cellsReader.ReadFireCells(), transportReader.ReadTransportFields());
            // Readers may call host code. Never label old fields with a newer world or revision.
            if (!FieldWorldReady() || !ReferenceEquals(system, _fireSystem) || revision != _resources.FieldRevision ||
                !ReferenceEquals(cellsReader, _observedCellsReader) || !ReferenceEquals(transportReader, _observedTransportReader))
                return false;
            _fireObservation = observed;
            _fireObservedSystem = system;
            _fireObservedRevision = revision;
            _ashGrowthField = null;
            _ashGrowthObservation = null;
        }
        if (_fireObservation is null) return false;
        field = _fireObservation;
        return true;
    }

    private bool FieldWorldReady() => !_resources.IsIndeterminate &&
        InitializationState == TimberbornRuntimeInitializationState.Ready && _fireSystem is not null;
}
