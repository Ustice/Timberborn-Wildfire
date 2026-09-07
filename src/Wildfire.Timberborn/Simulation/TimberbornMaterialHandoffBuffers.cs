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
    }
    public uint[] ReadReceipts(int count)
    {
        uint[] words = new uint[checked(count * FireSimMaterialHandoffProtocol.ReceiptWords)];
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
        ComputeBuffer requests = new(count, FireSimMaterialHandoffProtocol.RequestStrideBytes, ComputeBufferType.Structured);
        ComputeBuffer receipts;
        try { receipts = new(count, FireSimMaterialHandoffProtocol.ReceiptStrideBytes, ComputeBufferType.Structured); }
        catch { requests.Release(); throw; }
        Requests?.Release();
        Receipts?.Release();
        Requests = requests;
        Receipts = receipts;
    }
    public void Dispose() { Slots?.Release(); Requests?.Release(); Receipts?.Release(); }
}
