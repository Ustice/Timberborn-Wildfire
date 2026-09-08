namespace Wildfire.Core;

/// <summary>Explicit bounded buffer layouts; ordinary commands remain four uints/16 bytes.</summary>
public static class FireSimMaterialHandoffProtocol
{
    public const int RequestWords = 10;
    public const int ReceiptWords = 10;
    public const int HeaderWords = 4;
    public const int RequestStrideBytes = RequestWords * sizeof(uint);
    public const int ReceiptStrideBytes = ReceiptWords * sizeof(uint);
    public const uint BatchMarkerMask = 1u << 31;
    public const uint UnusedSource = uint.MaxValue;
    public const uint PackedMaterialMask = 0x130fu;
    public const uint CompanionMaterialMask = 0x01f0ffffu;
    public const uint Accepted = 1;
    public const uint Rejected = 2;

    public static FireSimGpuChange EncodeMarker(FireSimMaterialHandoffBatch batch) =>
        new(0, BatchMarkerMask, checked((uint)batch.Requests.Count), batch.Token);

    public static uint[] EncodeRequests(FireSimMaterialHandoffBatch batch)
    {
        if (batch is null) throw new ArgumentNullException(nameof(batch));
        Dictionary<int, int> indices = batch.Requests.Select((request, index) => (request.CellIndex, index))
            .ToDictionary(static pair => pair.CellIndex, static pair => pair.index);
        uint[] words = new uint[checked(batch.Requests.Count * RequestWords)];
        for (int index = 0; index < batch.Requests.Count; index++)
        {
            FireSimMaterialHandoffRequest request = batch.Requests[index];
            int offset = index * RequestWords;
            words[offset] = checked((uint)request.CellIndex);
            words[offset + 1] = request.Expected.TargetId;
            words[offset + 2] = request.Expected.SlotId;
            words[offset + 3] = request.Incoming.TargetId;
            words[offset + 4] = request.Incoming.SlotId;
            words[offset + 5] = (uint)request.Mode;
            words[offset + 6] = request.PackedMaterial;
            words[offset + 7] = request.CompanionMaterial;
            words[offset + 8] = request.Mode == FireSimMaterialHandoffMode.CapturedSource
                ? checked((uint)indices[request.SourceCellIndex]) : UnusedSource;
            // Word9 is reserved zero; target and stable local-slot identities are explicit.
        }
        return words;
    }

    public static FireSimMaterialHandoffReceipt DecodeReceipt(FireSimMaterialHandoffBatch batch,
        ReadOnlySpan<uint> header, ReadOnlySpan<uint> words)
    {
        if (batch is null) throw new ArgumentNullException(nameof(batch));
        if (header.Length != HeaderWords || words.Length != checked(batch.Requests.Count * ReceiptWords) ||
            header[0] != batch.Token || header[2] != batch.Requests.Count ||
            (header[1] != Accepted && header[1] != Rejected) ||
            (header[1] == Accepted ? header[3] != UnusedSource : header[3] >= batch.Requests.Count))
            throw InvalidReceipt();

        FireSimMaterialCellReceipt[] receipts = new FireSimMaterialCellReceipt[batch.Requests.Count];
        for (int index = 0; index < receipts.Length; index++)
        {
            int offset = index * ReceiptWords;
            if (words[offset] > int.MaxValue) throw InvalidReceipt();
            receipts[index] = new((int)words[offset], Identity(words[offset + 1], words[offset + 2]), words[offset + 3], words[offset + 4],
                Identity(words[offset + 5], words[offset + 6]), words[offset + 7], words[offset + 8], words[offset + 9]);
        }
        for (int index = 0; index < receipts.Length; index++)
        {
            FireSimMaterialCellReceipt receipt = receipts[index];
            FireSimMaterialHandoffRequest request = batch.Requests[index];
            if (receipt.CellIndex != request.CellIndex || receipt.PriorCell > ushort.MaxValue || receipt.AppliedCell > ushort.MaxValue ||
                (receipt.PriorCompanion & 0xf0000000u) != 0 || (receipt.AppliedCompanion & 0xf0000000u) != 0 ||
                receipt.Status < Accepted || receipt.Status > 8u)
                throw InvalidReceipt();
            if (header[1] == Rejected)
            {
                if (receipt.Applied != receipt.Prior || receipt.AppliedCell != receipt.PriorCell ||
                    receipt.AppliedCompanion != receipt.PriorCompanion || (index == header[3] && receipt.Status == Accepted))
                    throw InvalidReceipt();
                continue;
            }
            ValidateAccepted(batch, request, receipt, receipts);
        }
        return new(batch.Token, header[1] == Accepted, Array.AsReadOnly(receipts));
    }

    private static void ValidateAccepted(FireSimMaterialHandoffBatch batch, FireSimMaterialHandoffRequest request,
        FireSimMaterialCellReceipt receipt, IReadOnlyList<FireSimMaterialCellReceipt> receipts)
    {
        if (receipt.Status != Accepted || receipt.Prior != request.Expected || receipt.Applied != request.Incoming)
            throw InvalidReceipt();
        if (request.Mode == FireSimMaterialHandoffMode.Refresh)
        {
            if (receipt.PriorCell != receipt.AppliedCell || receipt.PriorCompanion != receipt.AppliedCompanion ||
                (receipt.PriorCell & (PackedMaterialMask & ~15u)) != request.PackedMaterial ||
                (receipt.PriorCompanion & (CompanionMaterialMask & ~0xf000u)) != request.CompanionMaterial)
                throw InvalidReceipt();
            return;
        }
        uint material = request.PackedMaterial;
        uint companion = request.CompanionMaterial;
        if (request.Mode == FireSimMaterialHandoffMode.CapturedSource)
        {
            int sourceIndex = batch.Requests.Select((value, index) => (value.CellIndex, index))
                .First(pair => pair.CellIndex == request.SourceCellIndex).index;
            FireSimMaterialCellReceipt source = receipts[sourceIndex];
            if (source.Prior != request.Incoming) throw InvalidReceipt();
            material = source.PriorCell & PackedMaterialMask;
            companion = source.PriorCompanion & CompanionMaterialMask;
        }
        // Validate transfer/preservation masks, not fire evolution. Receipt is captured before simulation.
        uint expectedCell = (receipt.PriorCell & 0x0cf0u) | material;
        uint expectedCompanion = (receipt.PriorCompanion & ~CompanionMaterialMask) | companion;
        if (receipt.AppliedCell != expectedCell || receipt.AppliedCompanion != expectedCompanion)
            throw InvalidReceipt();
    }

    private static FireSimMaterialIdentity Identity(uint target, uint slot)
    {
        if ((target == 0) != (slot == 0)) throw InvalidReceipt();
        return new(target, slot);
    }

    private static InvalidOperationException InvalidReceipt() => new("Missing or invalid GPU material handoff receipt; do not replay the batch.");
}

public readonly record struct FireSimMaterialCellReceipt(int CellIndex, FireSimMaterialIdentity Prior, uint PriorCell,
    uint PriorCompanion, FireSimMaterialIdentity Applied, uint AppliedCell, uint AppliedCompanion, uint Status);

public sealed class FireSimMaterialHandoffReceipt
{
    private readonly Dictionary<int, FireSimMaterialArchive> _outgoingArchives = new();
    internal FireSimMaterialHandoffReceipt(uint token, bool accepted, IReadOnlyList<FireSimMaterialCellReceipt> cells)
    {
        Token = token;
        Accepted = accepted;
        Cells = cells;
        if (accepted)
        {
            HashSet<FireSimMaterialIdentity> activeIncoming = new(cells.Select(static cell => cell.Applied));
            for (int index = 0; index < cells.Count; index++)
            {
                FireSimMaterialCellReceipt cell = cells[index];
                if (cell.Prior.IsOwned && !activeIncoming.Contains(cell.Prior))
                    _outgoingArchives[index] = new(token, cell.CellIndex, cell.Prior, cell.PriorCell, cell.PriorCompanion);
            }
        }
    }

    public uint Token { get; }
    public bool Accepted { get; }
    public IReadOnlyList<FireSimMaterialCellReceipt> Cells { get; }

    public FireSimMaterialArchive ArchiveOutgoing(int index)
    {
        if (!_outgoingArchives.TryGetValue(index, out FireSimMaterialArchive archive))
            throw new InvalidOperationException("Only material that leaves the active footprint in an accepted handoff can be archived.");
        return archive;
    }
}
