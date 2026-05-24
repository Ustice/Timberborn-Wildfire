namespace Wildfire.Core;

public interface IGpuFireSimulator
{
    int Width { get; }

    int Height { get; }

    int Depth { get; }

    void RegisterChange(FireSimChange change);

    GpuFireStepResult Tick();

    IDisposable Subscribe(IFireSimListener listener);
}

public readonly record struct GpuFireStepResult(IReadOnlyList<CellDelta> Deltas, uint Tick);

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
    byte? AddAsh = null,
    byte? RemoveAsh = null,
    byte? SetAsh = null,
    byte? SetAshContamination = null,
    byte? SetWater = null,
    byte? SetFuel = null,
    byte? SetHeat = null,
    byte? SetFlammability = null,
    byte? SetBurningLevel = null,
    byte? SetTerrain = null,
    byte? SetSmoke = null,
    byte? SetSmokeContamination = null);
