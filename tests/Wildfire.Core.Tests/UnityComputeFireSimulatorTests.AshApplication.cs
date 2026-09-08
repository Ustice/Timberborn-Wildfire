using Wildfire.Core;
using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed partial class UnityComputeFireSimulatorTests
{
    [Theory]
    [InlineData(FireSimAshApplicationOutcome.Applied)]
    [InlineData(FireSimAshApplicationOutcome.InvalidSurface)]
    public void ApplicationReadbackUsesAppendedStructuredCommandAndPreservesHighOutcomeBits(FireSimAshApplicationOutcome outcome)
    {
        using var grid = ComputeBufferGrid.FromCells(2, 1, 1, [0, 0], new RecordingComputeBufferAllocator());
        var changes = (RecordingComputeBufferHandle)grid.QueuedChanges;
        var originalNext = grid.NextCells;
        var dispatcher = new RecordingFireSimComputeDispatcher
        {
            AfterDispatch = dispatch =>
            {
                if (dispatch.KernelName == UnityComputeFireSimulator.ApplyExternalChangesKernelName)
                    changes.UploadedValues[6] |= (1u << 29) | ((uint)outcome << 30) |
                        (outcome == FireSimAshApplicationOutcome.Applied ? 1u << 27 : 0);
            }
        };
        IFireSimAshApplicationSimulator simulator = new UnityComputeFireSimulator(grid, dispatcher);
        simulator.RegisterChange(new(0, RemoveAsh: 1));
        int applied = 0;
        var result = simulator.TryApplyCleanAsh(new(1, 2), value =>
        {
            Assert.Same(originalNext, grid.CurrentCells);
            Assert.Equal(2, dispatcher.Dispatches.Count);
            applied += value.Added;
        });
        Assert.Equal(new FireSimAshApplicationReceipt(1, 2, (byte)applied, outcome), result!.Value.Receipt);
        Assert.Equal(outcome == FireSimAshApplicationOutcome.Applied ? 1 : 0, applied);
        Assert.Equal(2u, dispatcher.Dispatches[0].ChangeCount);
    }
}
