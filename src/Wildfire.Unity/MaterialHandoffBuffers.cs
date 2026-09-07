using Wildfire.Core;

namespace Wildfire.Unity;

public sealed class MaterialHandoffBuffers : IDisposable
{
    private readonly IComputeBufferAllocator _allocator;
    private readonly int _maximum;
    public MaterialHandoffBuffers(IComputeBufferAllocator allocator, int maximum)
    {
        _allocator = allocator;
        _maximum = maximum;
        Header = allocator.Allocate("wildfire.material_header", 1, FireSimMaterialHandoffProtocol.HeaderWords * sizeof(uint));
        try { Resize(1); }
        catch { Header.Dispose(); throw; }
    }
    public IComputeBufferHandle Requests { get; private set; } = null!;
    public IComputeBufferHandle Receipts { get; private set; } = null!;
    public IComputeBufferHandle Header { get; }
    public void Upload(FireSimMaterialHandoffBatch batch)
    {
        if (batch.Requests.Count > _maximum) throw new ArgumentOutOfRangeException(nameof(batch));
        if (batch.Requests.Count > Requests.Count) Resize(batch.Requests.Count);
        Requests.Upload(FireSimMaterialHandoffProtocol.EncodeRequests(batch));
        Header.Upload(new uint[FireSimMaterialHandoffProtocol.HeaderWords]);
    }
    private void Resize(int count)
    {
        IComputeBufferHandle requests = _allocator.Allocate("wildfire.material_requests", count, FireSimMaterialHandoffProtocol.RequestStrideBytes);
        IComputeBufferHandle receipts;
        try { receipts = _allocator.Allocate("wildfire.material_receipts", count, FireSimMaterialHandoffProtocol.ReceiptStrideBytes); }
        catch { requests.Dispose(); throw; }
        Requests?.Dispose();
        Receipts?.Dispose();
        Requests = requests;
        Receipts = receipts;
    }
    public void Dispose() { Requests.Dispose(); Receipts.Dispose(); Header.Dispose(); }
}
