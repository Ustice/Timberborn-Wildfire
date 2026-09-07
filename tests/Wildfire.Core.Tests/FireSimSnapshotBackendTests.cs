using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed class FireSimSnapshotBackendTests
{
    [Fact]
    public void NewBackendRestoresExactFieldsAuthorityPendingInputsAndSettings()
    {
        var snapshot = FireSimSnapshotValidationTests.Valid() with { Cells = [3, 0], TransportFields = [0x125, 0x234], CompanionFields = [0x6005704, 0] };
        var allocator = new Allocator();
        using var simulator = UnityComputeFireSimulator.CreateFromSnapshot(snapshot, allocator, new Dispatcher());
        var restored = simulator.CaptureSnapshot();
        Assert.Equal(snapshot.Cells, restored.Cells);
        Assert.Equal(snapshot.TransportFields, restored.TransportFields);
        Assert.Equal(snapshot.CompanionFields, restored.CompanionFields);
        Assert.Equal(snapshot.TargetIds, restored.TargetIds);
        Assert.Equal(snapshot.SlotIds, restored.SlotIds);
        Assert.Equal(snapshot.PendingChanges, restored.PendingChanges);
        Assert.Equal(snapshot.Parameters, restored.Parameters);
        Assert.Equal(snapshot.Seed, restored.Seed);
        Assert.Equal(snapshot.MaterialAuthority.Archives, restored.MaterialAuthority.Archives);
        Assert.Equal(snapshot.TransportFields, ((Buffer)simulator.BufferGrid!.NextTransportFields).Values);
        var visual = ((Buffer)simulator.BufferGrid.VisualFields).Values.Select(BitConverter.UInt32BitsToSingle).ToArray();
        Assert.Equal(4f / 7f, visual[1]);
        Assert.Equal(1f / 7f, visual[6]);
        snapshot.Cells[0] = 15;
        Assert.Equal((ushort)3, simulator.CaptureSnapshot().Cells[0]);
        simulator.Dispose();
        Assert.All(allocator.Buffers, buffer => Assert.Equal(1, buffer.Disposals));
        Assert.Throws<ObjectDisposedException>(() => simulator.Tick());
    }

    [Fact]
    public void InvalidSnapshotAndUploadFailureCannotPublishOrAlterAnExistingSimulator()
    {
        var snapshot = FireSimSnapshotValidationTests.Valid();
        using var existing = UnityComputeFireSimulator.CreateFromSnapshot(snapshot, new Allocator(), new Dispatcher());
        var invalidAllocator = new Allocator();
        Assert.Throws<ArgumentException>(() => UnityComputeFireSimulator.CreateFromSnapshot(snapshot with { SlotIds = [] }, invalidAllocator, new Dispatcher()));
        Assert.Empty(invalidAllocator.Buffers);
        var failing = new Allocator { FailUploadName = "wildfire.visual_fields" };
        Assert.Throws<InvalidOperationException>(() => UnityComputeFireSimulator.CreateFromSnapshot(snapshot, failing, new Dispatcher()));
        Assert.All(failing.Buffers, buffer => Assert.Equal(1, buffer.Disposals));
        var after = existing.CaptureSnapshot();
        Assert.Equal(snapshot.Cells, after.Cells);
        Assert.Equal(snapshot.SlotIds, after.SlotIds);
        Assert.Equal(snapshot.MaterialAuthority.Archives, after.MaterialAuthority.Archives);
    }

    private sealed class Dispatcher : IFireSimComputeDispatcher { public void Dispatch(FireSimComputeDispatch dispatch) { } }
    private sealed class Allocator : IComputeBufferAllocator
    {
        public string? FailUploadName;
        public List<Buffer> Buffers = [];
        public IComputeBufferHandle Allocate(string name, int count, int strideBytes)
        {
            var buffer = new Buffer(name, count, strideBytes, name == FailUploadName); Buffers.Add(buffer); return buffer;
        }
        public IAppendComputeBufferHandle AllocateAppend(string name, int count, int strideBytes) => (Buffer)Allocate(name, count, strideBytes);
    }
    private sealed class Buffer(string name, int count, int stride, bool failUpload) : IAppendComputeBufferHandle
    {
        public string Name => name;
        public int Count => count;
        public int StrideBytes => stride;
        public uint[] Values = new uint[count * stride / 4];
        public int Disposals;
        public void Upload(ReadOnlySpan<uint> values) { if (failUpload) throw new InvalidOperationException("upload failed"); Values = values.ToArray(); }
        public uint[] ReadElements(int firstElement, int elementCount) => Values.Skip(firstElement * stride / 4).Take(elementCount * stride / 4).ToArray();
        public void ResetAppendCounter() { }
        public int ReadAppendCounter() => 0;
        public uint[] ReadAppendedData(int elementCount) => [];
        public void Dispose() => Disposals++;
    }
}
