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

/// <summary>Queued cell and transport changes, applied in registration order.</summary>
/// <param name="AddWater">Wetness-band increment, clamped to 0..3. Applied after SetCell,
/// saturating the resulting water at 3, and before SetWater (which wins if both are supplied).
/// Null and zero leave water unchanged. This is not a quantity of host inventory water.</param>
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
    byte? SetSmokeContamination = null,
    byte? AddWater = null);
