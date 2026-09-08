using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Simulation;

internal sealed class TimberbornMaterialHandoffBuffers : IDisposable
{
    private readonly int _maximum;
    public TimberbornMaterialHandoffBuffers(IReadOnlyList<FireSimMaterialIdentity> initial)
    {
        _maximum = initial.Count;
        try
        {
            Slots = new ComputeBuffer(initial.Count, sizeof(uint), ComputeBufferType.Structured);
            Slots.SetData(initial.Select(static identity => identity.SlotId).ToArray());
            Resize(1);
        }
        catch { Dispose(); throw; }
    }
    public ComputeBuffer Slots { get; private set; } = null!;
    public ComputeBuffer Requests { get; private set; } = null!;
    public ComputeBuffer Receipts { get; private set; } = null!;
    public void Upload(FireSimMaterialHandoffBatch batch)
    {
        if (batch.Requests.Count > _maximum) throw new ArgumentOutOfRangeException(nameof(batch));
        if (batch.Requests.Count > Requests.count) Resize(batch.Requests.Count);
        Requests.SetData(FireSimMaterialHandoffProtocol.EncodeRequests(batch));
        Receipts.SetData(new uint[FireSimMaterialHandoffProtocol.ReceiptWords]);
    }
    public uint[] ReadHeader() => ReadWords(1).Take(FireSimMaterialHandoffProtocol.HeaderWords).ToArray();
    public uint[] ReadReceipts(int count) => ReadWords(checked(count + 1)).Skip(FireSimMaterialHandoffProtocol.ReceiptWords).ToArray();
    private uint[] ReadWords(int rows)
    {
        uint[] words = new uint[checked(rows * FireSimMaterialHandoffProtocol.ReceiptWords)];
        // Read from raw offset zero: the header shares the 40-byte row stride, not a separate buffer.
        Receipts.GetData(words, 0, 0, words.Length);
        return words;
    }
    public void Bind(ComputeShader shader, int kernel)
    {
        shader.SetBuffer(kernel, "MaterialSlotIds", Slots);
        shader.SetBuffer(kernel, "MaterialRequests", Requests);
        shader.SetBuffer(kernel, "MaterialReceipts", Receipts);
        shader.SetInt("MaterialRequestCapacity", Requests.count);
    }
    private void Resize(int count)
    {
        if (count <= 0 || count > _maximum) throw new ArgumentOutOfRangeException(nameof(count));
        int receiptRows = checked(count + 1);
        _ = checked(receiptRows * FireSimMaterialHandoffProtocol.ReceiptWords);
        ComputeBuffer requests = new(count, FireSimMaterialHandoffProtocol.RequestStrideBytes, ComputeBufferType.Structured);
        ComputeBuffer receipts;
        try { receipts = new(receiptRows, FireSimMaterialHandoffProtocol.ReceiptStrideBytes, ComputeBufferType.Structured); }
        catch { requests.Release(); throw; }
        Requests?.Release();
        Receipts?.Release();
        Requests = requests;
        Receipts = receipts;
    }
    public void Dispose() { Slots?.Release(); Requests?.Release(); Receipts?.Release(); }
}
