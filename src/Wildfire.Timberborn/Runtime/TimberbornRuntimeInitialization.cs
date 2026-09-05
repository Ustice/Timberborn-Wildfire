using Wildfire.Core;

namespace Wildfire.Timberborn.Runtime;

public enum TimberbornRuntimeInitializationState
{
    Unloaded,
    WaitingForWorld,
    Initializing,
    Ready,
    Unsupported,
    Failed,
}

/// <summary>
/// Owns the decision to initialize a loaded world. Only readiness waits retry;
/// rejection and failure remain terminal until an explicit load/reset.
/// </summary>
public sealed class TimberbornRuntimeInitialization
{
    public TimberbornRuntimeInitializationState State { get; private set; }
    public FireGrid? Grid { get; private set; }
    public Exception? Failure { get; private set; }
    public int ReadinessChecks { get; private set; }

    public void Load() => Reset(TimberbornRuntimeInitializationState.WaitingForWorld);

    public void Unload() => Reset(TimberbornRuntimeInitializationState.Unloaded);

    public void Fail(Exception exception)
    {
        Failure = exception ?? throw new ArgumentNullException(nameof(exception));
        State = TimberbornRuntimeInitializationState.Failed;
    }

    public void Update(Func<FireGrid> readGrid, Func<bool> isWorldReady, Action<FireGrid> initialize)
    {
        if (State != TimberbornRuntimeInitializationState.WaitingForWorld)
        {
            return;
        }

        try
        {
            ReadinessChecks++;
            FireGrid grid = readGrid();
            if (grid.Width <= 0 || grid.Height <= 0 || grid.Depth <= 0)
            {
                return;
            }

            Grid = grid;
            // Do not use FireGrid.CellCount here: its int product can overflow
            // before oversized maps are rejected. Short circuit before multiplying depth.
            long planeCells = (long)grid.Width * grid.Height;
            if (planeCells > TimberbornAutoDispatchPolicy.CellLimit ||
                planeCells * grid.Depth > TimberbornAutoDispatchPolicy.CellLimit)
            {
                State = TimberbornRuntimeInitializationState.Unsupported;
                return;
            }

            if (!isWorldReady())
            {
                return;
            }

            State = TimberbornRuntimeInitializationState.Initializing;
            initialize(grid);
            State = TimberbornRuntimeInitializationState.Ready;
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private void Reset(TimberbornRuntimeInitializationState state)
    {
        State = state;
        Grid = null;
        Failure = null;
        ReadinessChecks = 0;
    }
}
