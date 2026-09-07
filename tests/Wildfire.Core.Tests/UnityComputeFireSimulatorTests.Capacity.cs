using Wildfire.Core;
using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed partial class UnityComputeFireSimulatorTests
{
    [Fact]
    public void RealGenericPreparationFailureLeavesSnapshotQueueAndAuthorityUntouchedBeforeDispatch()
    {
        var allocator = new FailingMaterialAllocator();
        using var grid = ComputeBufferGrid.FromCells(1, 1, 1, [0], allocator);
        var oldCommands = grid.QueuedChanges; var oldDeltas = grid.Deltas;
        var definition = new FireSimMaterialDefinition(WildfireMaterialClass.Tree, 7,
            WildfireAshQuality.Fertile, WildfireContaminationBehavior.None, 9, 2, 1);
        var dispatcher = new RecordingFireSimComputeDispatcher();
        var simulator = new UnityComputeFireSimulator(grid, dispatcher);
        var pending = new FireSimChange(0, AddWater: 1);
        simulator.RegisterChange(pending);
        allocator.FailDeltas = true;
        var batch = new FireSimMaterialHandoffBatch(1, [FireSimMaterialHandoffRequest.Fresh(0, default, new(2, 21), definition)]);
        var failure = Assert.Throws<FireSimStepInputException>(() => simulator.TryHandoffMaterial(batch, _ => throw new Exception()));
        Assert.Equal(FireSimStepInputOutcome.NotApplied, failure.Outcome);
        Assert.Empty(dispatcher.Dispatches);
        Assert.Same(oldCommands, grid.QueuedChanges); Assert.Same(oldDeltas, grid.Deltas);
        var saved = simulator.CaptureSnapshot();
        Assert.Equal(new[] { pending }, saved.PendingChanges);
        Assert.Equal(0u, saved.Tick);
        Assert.Equal(1u, saved.MaterialAuthority.LastAttemptToken);
        Assert.Empty(saved.MaterialAuthority.KnownSlots);
        Assert.False(simulator.IsSlotKnown(new(2, 21)));
    }

    private sealed class FailingMaterialAllocator : IComputeBufferAllocator
    {
        private readonly RecordingComputeBufferAllocator _inner = new();
        public bool FailDeltas;
        public IComputeBufferHandle Allocate(string name, int count, int strideBytes) => _inner.Allocate(name, count, strideBytes);
        public IAppendComputeBufferHandle AllocateAppend(string name, int count, int strideBytes) =>
            FailDeltas ? throw new InvalidOperationException("candidate delta allocation failed") : _inner.AllocateAppend(name, count, strideBytes);
    }

    [Fact]
    public void FullBacklogUsesGrownPairInBothKernelsAndLaterOrdinaryAshBudgetsStayFixed()
    {
        using var grid = ComputeBufferGrid.FromCells(1, 1, 1, [0], new RecordingComputeBufferAllocator());
        var oldChanges = grid.QueuedChanges; var oldDeltas = grid.Deltas;
        var definition = new FireSimMaterialDefinition(WildfireMaterialClass.Tree, 7,
            WildfireAshQuality.Fertile, WildfireContaminationBehavior.None, 9, 2, 1);
        var batch = new FireSimMaterialHandoffBatch(1, [FireSimMaterialHandoffRequest.Fresh(0, default, new(2, 21), definition)]);
        var ordinary = new FireSimChange[] { new(0, SetFuel: 7), new(0, SetFuel: 4), new(0, SetFuel: 2) };
        bool control = true;
        var dispatcher = new RecordingFireSimComputeDispatcher
        {
            AfterDispatch = dispatch =>
            {
                Assert.Same(grid.QueuedChanges, dispatch.QueuedChanges);
                Assert.Same(grid.Deltas, dispatch.Deltas);
                var changes = (RecordingComputeBufferHandle)dispatch.QueuedChanges;
                if (dispatch.KernelName != UnityComputeFireSimulator.ApplyExternalChangesKernelName) return;
                if (control)
                {
                    Assert.Equal(4u, dispatch.ChangeCount);
                    Assert.Equal(FireSimGpuProtocol.EncodeWords(ordinary.Append(new(0, MaterialHandoff: batch)).ToArray(), 4), changes.UploadedValues);
                    dispatch.MaterialReceipts.Upload(new uint[] { 1, 1, 1, uint.MaxValue, 0, 0, 0, 0, 0, 0,
                        0, 0, 0, 2, 0, 2, 21, definition.PackedMaterial, definition.CompanionMaterial, 1 });
                    var deltas = (RecordingComputeBufferHandle)dispatch.Deltas;
                    deltas.AppendCounter = 3;
                    deltas.AppendedData = [0, 0, 7, 0, 0, 0, 7, 4, 0, 0, 0, 4, 2, 0, 0];
                }
                else if ((changes.UploadedValues[1] & FireSimGpuProtocol.CollectCleanAshMask) != 0)
                    changes.UploadedValues[2] |= FireSimGpuProtocol.CollectionReceiptValidMask | (1u << 27);
            }
        };
        var simulator = new UnityComputeFireSimulator(grid, dispatcher);
        foreach (var change in ordinary) simulator.RegisterChange(change);
        var result = simulator.TryHandoffMaterial(batch, receipt => Assert.True(receipt.Accepted));
        Assert.Equal(3, result!.Value.Deltas.Count);
        Assert.NotSame(oldChanges, grid.QueuedChanges); Assert.NotSame(oldDeltas, grid.Deltas);
        Assert.Equal(4, grid.QueuedChanges.Count); Assert.Equal(5, grid.Deltas.Count);
        Assert.Equal(2, dispatcher.Dispatches.Count);
        control = false;
        simulator.RegisterChange(new(0, AddWater: 1)); simulator.RegisterChange(new(0, SetHeat: 2));
        Assert.Null(simulator.TryCollectAsh(new(0, 1), _ => throw new Exception()));
        Assert.Null(simulator.TryTickWithInput(new(0, AddWater: 1), () => throw new Exception()));
        simulator.Tick(); Assert.Equal(1, simulator.PendingChangeCount);
        simulator.Tick(); Assert.Equal(0, simulator.PendingChangeCount);
        FireSimAshCollectionReceipt? collected = null;
        simulator.TryCollectAsh(new(0, 1), receipt => collected = receipt);
        Assert.Equal(new FireSimAshCollectionReceipt(0, 1, 1), collected);
    }
}
