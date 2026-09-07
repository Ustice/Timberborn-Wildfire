using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Simulation;

public sealed partial class TimberbornComputeFireSimulator : IFireSimSnapshotSimulator, IFireSimSnapshotBackend
{
    /// <summary>Construct complete state without publishing an external visual binding or changing a live simulator.</summary>
    public static TimberbornComputeFireSimulator CreateFromSnapshot(FireSimSnapshot snapshot, ComputeShader shader,
        ITimberbornFireLogSink logSink, ITimberbornWindProvider? windProvider = null)
    {
        var validated = FireSimSnapshotValidation.ValidateAndClone(snapshot);
        var materials = validated.TargetIds.Select((target, cell) => new WildfireMaterialField(target,
            WildfireMaterialFieldState.Unpack(validated.CompanionFields[cell]))).ToArray();
        return new(validated.Grid, validated.Cells, shader, logSink, NullTimberbornGpuVisualFieldSurface.Instance,
            validated.Parameters, materials, windProvider ?? NullTimberbornWindProvider.Instance, validated);
    }

    public FireSimSnapshotCapability SnapshotCapability => _step.SnapshotCapability;

    public FireSimSnapshot CaptureSnapshot()
    {
        ThrowIfDisposed();
        return _step.CaptureSnapshot(this);
    }

    FireGrid IFireSimSnapshotBackend.SnapshotGrid => Grid;
    FireSimParameters IFireSimSnapshotBackend.SnapshotParameters => _parameters;
    uint IFireSimSnapshotBackend.SnapshotSeed => _seed;
    FireSimSnapshotBuffers IFireSimSnapshotBackend.ReadSnapshotBuffers() => new(
        ReadSnapshotWords(_readCells, Grid.CellCount).Select(static cell => checked((ushort)cell)).ToArray(),
        ReadSnapshotWords(_readTransportFields, Grid.CellCount), ReadSnapshotWords(_materialFields, Grid.CellCount),
        ReadSnapshotWords(_materialTargetIds, Grid.CellCount), ReadSnapshotWords(_materialHandoff!.Slots, Grid.CellCount));

    private static uint[] ReadSnapshotWords(ComputeBuffer buffer, int count)
    {
        uint[] values = new uint[count];
        buffer.GetData(values);
        return values;
    }
}
