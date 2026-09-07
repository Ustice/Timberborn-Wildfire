using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Simulation;

public sealed partial class TimberbornComputeFireSimulator
{
    private void ReserveStepCapacity(int orderedCommandCount)
    {
        int deltaCount = FireSimGpuProtocol.ValidateStepBufferCapacity(Grid.CellCount, orderedCommandCount);
        if (_externalChanges.count >= orderedCommandCount && _deltas.count >= deltaCount) return;
        ComputeBuffer? commands = null;
        ComputeBuffer? deltas = null;
        try
        {
            commands = CreateBuffer(orderedCommandCount, ChangeStrideBytes, ComputeBufferType.Structured);
            deltas = CreateBuffer(deltaCount, DeltaStrideBytes, ComputeBufferType.Append);
        }
        catch
        {
            commands?.Release();
            deltas?.Release();
            throw;
        }
        var oldCommands = _externalChanges;
        var oldDeltas = _deltas;
        _externalChanges = commands;
        _deltas = deltas;
        // BindKernel always rebinds current handles before dispatch; no field data is copied/reset.
        try { oldCommands.Release(); }
        finally { oldDeltas.Release(); }
    }
}
