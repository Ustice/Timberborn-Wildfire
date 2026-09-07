using Wildfire.Core;
using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed partial class UnityComputeFireSimulatorTests
{
    [Fact]
    public void MaterialBatchReservesOneHeaderRowAndUsesFullRequestCapacityAfterEarlierInputs()
    {
        using var grid = ComputeBufferGrid.FromCells(2, 1, 1, [0, 0], new RecordingComputeBufferAllocator());
        var initialRequests = grid.MaterialHandoff.Requests;
        var definition = new FireSimMaterialDefinition(WildfireMaterialClass.Tree, 7,
            WildfireAshQuality.Fertile, WildfireContaminationBehavior.None, 9, 2, 1);
        var batch = new FireSimMaterialHandoffBatch(1,
            [FireSimMaterialHandoffRequest.Fresh(0, default, new(2, 21), definition),
             FireSimMaterialHandoffRequest.Fresh(1, default, new(2, 22), definition)]);
        var dispatcher = new RecordingFireSimComputeDispatcher
        {
            AfterDispatch = dispatch =>
            {
                Assert.Same(grid.MaterialTargetIds, dispatch.MaterialTargetIds);
                Assert.Same(grid.MaterialSlotIds, dispatch.MaterialSlotIds);
                Assert.Same(grid.MaterialHandoff.Requests, dispatch.MaterialRequests);
                Assert.Same(grid.MaterialHandoff.Receipts, dispatch.MaterialReceipts);
                if (dispatch.KernelName != UnityComputeFireSimulator.ApplyExternalChangesKernelName) return;
                var appliedWords = ((RecordingComputeBufferHandle)dispatch.QueuedChanges).UploadedValues;
                Assert.Equal(new uint[] { 0, 0, 1u << 23, 0, 0, FireSimMaterialHandoffProtocol.BatchMarkerMask, 2, 1 }, appliedWords);
                Assert.Equal(FireSimMaterialHandoffProtocol.EncodeRequests(batch), ((RecordingComputeBufferHandle)dispatch.MaterialRequests).UploadedValues);
                Assert.Equal(3, dispatch.MaterialReceipts.Count);
                Assert.Equal(new uint[10], ((RecordingComputeBufferHandle)dispatch.MaterialReceipts).UploadedValues);
                dispatch.MaterialReceipts.Upload(new uint[]
                {
                    1, 1, 2, uint.MaxValue, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 2, 21, definition.PackedMaterial, definition.CompanionMaterial, 1,
                    1, 0, 0, 0, 0, 2, 22, definition.PackedMaterial, definition.CompanionMaterial, 1,
                });
            }
        };
        IFireSimMaterialHandoffSimulator simulator = new UnityComputeFireSimulator(grid, dispatcher);
        simulator.RegisterChange(new(99, AddWater: 1)); // Ignored input must not shift the uploaded marker index.
        simulator.RegisterChange(new(0, AddWater: 1));
        simulator.TryHandoffMaterial(batch, receipt => Assert.Equal(2, receipt.Cells.Count));
        Assert.NotSame(initialRequests, grid.MaterialHandoff.Requests);
        Assert.Equal(2, grid.MaterialHandoff.Requests.Count);
        Assert.Equal(2, dispatcher.Dispatches.Count);
        Assert.False(simulator.TryGetMaterialArchive(new(2, 21), out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnexecutedOrStaleMaterialHeaderCannotCommitOrAllowContinuation(bool staleReceipt)
    {
        using var grid = ComputeBufferGrid.FromCells(2, 1, 1, [0, 0], new RecordingComputeBufferAllocator());
        var definition = new FireSimMaterialDefinition(WildfireMaterialClass.Tree, 7,
            WildfireAshQuality.Fertile, WildfireContaminationBehavior.None, 9, 2, 1);
        var batch = new FireSimMaterialHandoffBatch(2,
            [FireSimMaterialHandoffRequest.Fresh(0, default, new(2, 21), definition)]);
        var dispatcher = new RecordingFireSimComputeDispatcher
        {
            AfterDispatch = dispatch =>
            {
                if (staleReceipt && dispatch.KernelName == UnityComputeFireSimulator.ApplyExternalChangesKernelName)
                    dispatch.MaterialReceipts.Upload(new uint[] { 1, 1, 1, uint.MaxValue, 0, 0, 0, 0, 0, 0 });
            }
        };
        IFireSimMaterialHandoffSimulator simulator = new UnityComputeFireSimulator(grid, dispatcher);
        simulator.RegisterChange(new(1, AddWater: 1));
        int commits = 0;
        var failure = Assert.Throws<FireSimStepInputException>(() => simulator.TryHandoffMaterial(batch, _ => commits++));
        Assert.Equal(FireSimStepInputOutcome.Indeterminate, failure.Outcome);
        Assert.Equal(0, commits);
        Assert.Throws<InvalidOperationException>(() => simulator.Tick());
    }

    [Fact]
    public void ReceiptHeaderIsClearedOnReuseAndExcessRequestCountCannotMutateStorage()
    {
        var allocator = new RecordingComputeBufferAllocator();
        using var buffers = new MaterialHandoffBuffers(allocator, 2);
        var definition = new FireSimMaterialDefinition(WildfireMaterialClass.Tree, 7,
            WildfireAshQuality.Fertile, WildfireContaminationBehavior.None, 9, 2, 1);
        FireSimMaterialHandoffBatch Batch(int count) => new(1, Enumerable.Range(0, count)
            .Select(cell => FireSimMaterialHandoffRequest.Fresh(cell, default, new(2, (uint)cell + 1), definition)));
        var accepted = Batch(2);
        buffers.Upload(accepted);
        var receipts = buffers.Receipts;
        receipts.Upload(new uint[] { 1, 1, 2, uint.MaxValue, 0, 0, 0, 0, 0, 0 });
        var oldHeader = buffers.ReadHeader();
        var oldRequests = ((RecordingComputeBufferHandle)buffers.Requests).UploadedValues.ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => buffers.Upload(Batch(3)));
        Assert.Same(receipts, buffers.Receipts);
        Assert.Equal(oldHeader, buffers.ReadHeader());
        Assert.Equal(oldRequests, ((RecordingComputeBufferHandle)buffers.Requests).UploadedValues);
        buffers.Upload(accepted);
        Assert.Equal(new uint[4], buffers.ReadHeader());
        Assert.Equal(2, buffers.Requests.Count);
        Assert.Equal(3, buffers.Receipts.Count);
    }
}
