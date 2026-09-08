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
        Resize(1);
    }
    public IComputeBufferHandle Requests { get; private set; } = null!;
    public IComputeBufferHandle Receipts { get; private set; } = null!;
    public void Upload(FireSimMaterialHandoffBatch batch)
    {
        if (batch.Requests.Count > _maximum) throw new ArgumentOutOfRangeException(nameof(batch));
        if (batch.Requests.Count > Requests.Count) Resize(batch.Requests.Count);
        Requests.Upload(FireSimMaterialHandoffProtocol.EncodeRequests(batch));
        Receipts.Upload(new uint[FireSimMaterialHandoffProtocol.ReceiptWords]);
    }
    public uint[] ReadHeader() => Receipts.ReadElements(0, 1).Take(FireSimMaterialHandoffProtocol.HeaderWords).ToArray();
    public uint[] ReadReceipts(int count) => Receipts.ReadElements(0, checked(count + 1)).Skip(FireSimMaterialHandoffProtocol.ReceiptWords).ToArray();
    private void Resize(int count)
    {
        if (count <= 0 || count > _maximum) throw new ArgumentOutOfRangeException(nameof(count));
        int receiptRows = checked(count + 1);
        _ = checked(receiptRows * FireSimMaterialHandoffProtocol.ReceiptWords);
        IComputeBufferHandle requests = _allocator.Allocate("wildfire.material_requests", count, FireSimMaterialHandoffProtocol.RequestStrideBytes);
        IComputeBufferHandle receipts;
        try { receipts = _allocator.Allocate("wildfire.material_receipts", receiptRows, FireSimMaterialHandoffProtocol.ReceiptStrideBytes); }
        catch { requests.Dispose(); throw; }
        Requests?.Dispose();
        Receipts?.Dispose();
        Requests = requests;
        Receipts = receipts;
    }
    public void Dispose() { Requests.Dispose(); Receipts.Dispose(); }
}
