namespace Wildfire.Core;

public interface IFireSimulator
{
    int Width { get; }

    int Height { get; }

    int Depth { get; }

    ReadOnlySpan<ushort> Cells { get; }

    void RegisterChange(FireSimChange change);

    FireStepResult Tick();

    IDisposable Subscribe(IFireSimListener listener);
}

public readonly record struct FireStepResult(IReadOnlyList<CellDelta> Deltas, uint Tick);

public readonly record struct CellDelta(int CellIndex, ushort OldCell, ushort NewCell);

public interface IFireSimListener
{
    void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas);
}

public readonly record struct FireSimChange(
    int CellIndex,
    ushort? SetCell = null,
    byte? AddHeat = null,
    byte? AddFuel = null,
    byte? SetWater = null,
    byte? SetFuel = null,
    byte? SetHeat = null,
    byte? SetFlammability = null,
    byte? SetHeatLoss = null,
    byte? SetTerrain = null);
