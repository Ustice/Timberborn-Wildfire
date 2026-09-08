using Wildfire.Core;
using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed partial class UnityComputeFireSimulatorTests
{
    [Fact]
    public void PostStepDiagnosticFailurePreservesCommittedOutcomeAndOriginalCause()
    {
        using var grid = ComputeBufferGrid.FromCells(1, 1, 1, [0], new RecordingComputeBufferAllocator());
        var originalNext = grid.NextCells;
        var dispatcher = new RecordingFireSimComputeDispatcher();
        var cause = new IOException("post-step diagnostic");
        var simulator = new UnityComputeFireSimulator(grid, dispatcher, new PostStepFailingDiagnostics(cause));
        var listener = new RecordingFireSimListener();
        using var subscription = simulator.Subscribe(listener);

        var failure = Assert.Throws<FireSimStepInputException>(() => simulator.Tick());

        Assert.Equal(FireSimStepInputOutcome.Committed, failure.Outcome);
        Assert.Same(cause, failure.InnerException);
        Assert.Same(originalNext, grid.CurrentCells);
        Assert.Equal(1u, Assert.Single(dispatcher.Dispatches).Tick);
        Assert.Empty(Assert.Single(listener.Notifications));
    }

    private sealed class PostStepFailingDiagnostics(Exception cause) : IFireSimDiagnosticSink
    {
        public void Info(string message)
        {
            if (message.StartsWith("wildfire_gpu_simulator_listeners_notified ", StringComparison.Ordinal))
                throw cause;
        }
    }
}
