using Wildfire.Core;

namespace Wildfire.Unity;

public sealed partial class UnityComputeFireSimulator : IFireSimSnapshotSimulator, IFireSimSnapshotBackend, IDisposable
{
    private bool _ownsBufferGrid;
    private bool _disposed;

    /// <summary>Restore into a new owned buffer graph; construction failure cannot change an existing simulator.</summary>
    public static UnityComputeFireSimulator CreateFromSnapshot(FireSimSnapshot snapshot,
        IComputeBufferAllocator allocator, IFireSimComputeDispatcher dispatcher, IFireSimDiagnosticSink? diagnostics = null)
    {
        var validated = FireSimSnapshotValidation.ValidateAndClone(snapshot);
        var materials = validated.TargetIds.Select((target, cell) => new WildfireMaterialField(target,
            WildfireMaterialFieldState.Unpack(validated.CompanionFields[cell]))).ToArray();
        var grid = new ComputeBufferGrid(new(validated.Grid.Width, validated.Grid.Height, validated.Grid.Depth),
            validated.Cells, materials, allocator, validated.SlotIds);
        try
        {
            grid.CurrentTransportFields.Upload(validated.TransportFields);
            grid.NextTransportFields.Upload(validated.TransportFields);
            var visual = validated.Cells.SelectMany(cell =>
            {
                var sample = FireVisualField.FromPackedCell(cell, validated.Parameters);
                return new[] { sample.Fire, sample.Smoke, sample.Ash, sample.Visibility }.Select(BitConverter.SingleToUInt32Bits);
            }).ToArray();
            grid.VisualFields.Upload(visual);
            return new(grid, dispatcher, diagnostics ?? NullFireSimDiagnosticSink.Instance,
                validated.Seed, validated.Parameters, validated) { _ownsBufferGrid = true };
        }
        catch { grid.Dispose(); throw; }
    }

    public FireSimSnapshot CaptureSnapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (BufferGrid is null || _dispatcher is null) throw new InvalidOperationException("A complete snapshot requires an initialized compute backend.");
        return _step.CaptureSnapshot(this);
    }
    FireGrid IFireSimSnapshotBackend.SnapshotGrid => Dimensions.ToFireGrid();
    FireSimParameters IFireSimSnapshotBackend.SnapshotParameters => _parameters;
    uint IFireSimSnapshotBackend.SnapshotSeed => _seed;
    FireSimSnapshotBuffers IFireSimSnapshotBackend.ReadSnapshotBuffers() => new(
        BufferGrid!.CurrentCells.ReadElements(0, Dimensions.CellCount).Select(static cell => checked((ushort)cell)).ToArray(),
        BufferGrid.CurrentTransportFields.ReadElements(0, Dimensions.CellCount),
        BufferGrid.MaterialFields.ReadElements(0, Dimensions.CellCount),
        BufferGrid.MaterialTargetIds.ReadElements(0, Dimensions.CellCount),
        BufferGrid.MaterialSlotIds.ReadElements(0, Dimensions.CellCount));

    public void Dispose()
    {
        if (_disposed) return;
        _step.ThrowIfSnapshotInProgress();
        if (_ownsBufferGrid) BufferGrid?.Dispose();
        _disposed = true;
    }
}
