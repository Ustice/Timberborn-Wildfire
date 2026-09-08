namespace Wildfire.Core;

public interface IGpuFireSimulator
{
    int Width { get; }

    int Height { get; }

    int Depth { get; }

    void RegisterChange(FireSimChange change);

    /// <summary>Admitted-step failures report FireSimStepInputException with the actual step outcome.
    /// Validation before admission remains an ordinary argument/state failure.</summary>
    GpuFireStepResult Tick();

    IDisposable Subscribe(IFireSimListener listener);
}

public readonly record struct GpuFireStepResult(IReadOnlyList<CellDelta> Deltas, uint Tick);

public readonly record struct CellDelta(int CellIndex, ushort OldCell, ushort NewCell, uint TargetId = 0, uint SlotId = 0);

public interface IFireSimListener
{
    void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas);
}

/// <summary>Queued cell and transport changes, applied in registration order.</summary>
/// <param name="AddWater">Wetness-band increment, clamped to 0..3. Applied after SetCell,
/// saturating the resulting water at 3, and before SetWater (which wins if both are supplied).
/// Null and zero leave water unchanged. This is not a quantity of host inventory water.</param>
/// <param name="CollectCleanAsh">Exclusive conditional collection request, strictly 0..3.
/// Only TryCollectAsh admits this operation; generic queues reject it. No other fields may be supplied.</param>
/// <param name="ApplyCleanAshLimit">Exclusive one-unit clean ash application with limit 1..3.
/// Only TryApplyCleanAsh admits this receipt-bearing operation; generic queues reject it.</param>
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
    byte? AddWater = null,
    byte? CollectCleanAsh = null,
    FireSimMaterialHandoffBatch? MaterialHandoff = null,
    byte? ApplyCleanAshLimit = null);
