using Wildfire.Core;

namespace Wildfire.Timberborn.Tests;

// Scripted managed backend, not a CPU fire implementation. Real Core queue/receipt/authority code executes.
internal sealed class RetiredDetachmentSimulatorFixture : IFireSimMaterialHandoffSimulator, IFireSimSnapshotSimulator,
    IFireSimMaterialHandoffBackend, IFireSimSnapshotBackend
{
    internal readonly FireSimStepCoordinator Coordinator;
    private readonly FireSimSnapshot _initial;
    private FireSimMaterialHandoffBatch? _batch;
    private ushort[] _cells;
    private uint[] _targets, _slots, _companions;
    private uint[] _receipt = [];
    private readonly List<CellDelta> _deltas = [];
    internal int Uploads, Applies, Simulations, Delivered;
    internal bool Accepted = true;
    internal string? FailAt;
    public int MaterialHandoffCapacity { get; set; }
    internal RetiredDetachmentSimulatorFixture(FireSimSnapshot initial, int changeCapacity = 4)
    {
        _initial = FireSimSnapshotValidation.ValidateAndClone(initial);
        _cells = initial.Cells.ToArray(); _targets = initial.TargetIds.ToArray(); _slots = initial.SlotIds.ToArray();
        _companions = initial.CompanionFields.ToArray(); MaterialHandoffCapacity = initial.Cells.Length;
        Coordinator = new(initial, changeCapacity);
    }
    public int Width => _initial.Grid.Width;
    public int Height => _initial.Grid.Height;
    public int Depth => _initial.Grid.Depth;
    public FireSimSnapshotCapability SnapshotCapability => Coordinator.SnapshotCapability;
    public FireSimSnapshot CaptureSnapshot() => Coordinator.CaptureSnapshot(this);
    public void RegisterChange(FireSimChange change) => Coordinator.RegisterChange(change);
    public GpuFireStepResult Tick() => Coordinator.Tick(this);
    public IDisposable Subscribe(IFireSimListener listener) => Coordinator.Subscribe(listener);
    public GpuFireStepResult? TryTickWithInput(FireSimChange input, Action commit) => Coordinator.TryTickWithInput(this, input, commit);
    public GpuFireStepResult? TryHandoffMaterial(FireSimMaterialHandoffBatch batch, Action<FireSimMaterialHandoffReceipt> commit) =>
        Coordinator.TryHandoffMaterial(this, batch, commit);
    public bool IsSlotKnown(FireSimMaterialIdentity identity) => Coordinator.IsSlotKnown(identity);
    public bool TryGetMaterialArchive(FireSimMaterialIdentity identity, out FireSimMaterialArchive archive) => Coordinator.TryGetMaterialArchive(identity, out archive!);
    public void UploadMaterialHandoff(FireSimMaterialHandoffBatch batch)
    { Uploads++; if (FailAt == "Upload") throw new ApplicationException("upload failed"); _batch = batch; }
    public uint[] ReadMaterialHandoffHeader()
    {
        if (FailAt == "Receipt") throw new ApplicationException("receipt read failed");
        return [_batch!.Token, Accepted ? 1u : 2u, (uint)_batch.Requests.Count, Accepted ? uint.MaxValue : 0];
    }
    public uint[] ReadMaterialHandoffReceipts(int count) => _receipt;
    public void ResetDeltaCounter(uint tick) => _deltas.Clear();
    public void ApplyExternalChanges(uint tick, FireSimChange[] changes)
    {
        Applies++; if (FailAt == "Apply") throw new ApplicationException("apply failed after possible write");
        foreach (var change in changes)
        {
            if (change.MaterialHandoff is not null) { ApplyMaterial(); continue; }
            if (change.SetFuel is { } fuel)
            {
                int cell = change.CellIndex; ushort previous = _cells[cell];
                _cells[cell] = (ushort)((previous & ~15) | fuel);
                _deltas.Add(new(cell, previous, _cells[cell], _targets[cell], _slots[cell]));
            }
        }
    }
    private void ApplyMaterial()
    {
        var cells = _cells.ToArray(); var companions = _companions.ToArray();
        var targets = _targets.ToArray(); var slots = _slots.ToArray();
        var rows = new List<uint>();
        foreach (var request in _batch!.Requests)
        {
            int cell = request.CellIndex;
            uint material = request.PackedMaterial, companion = request.CompanionMaterial;
            if (request.Mode == FireSimMaterialHandoffMode.CapturedSource)
            {
                material = (uint)cells[request.SourceCellIndex] & FireSimMaterialHandoffProtocol.PackedMaterialMask;
                companion = companions[request.SourceCellIndex] & FireSimMaterialHandoffProtocol.CompanionMaterialMask;
            }
            if (Accepted)
            {
                _cells[cell] = (ushort)((cells[cell] & 0x0cf0u) | material);
                _companions[cell] = (companions[cell] & ~FireSimMaterialHandoffProtocol.CompanionMaterialMask) | companion;
                _targets[cell] = request.Incoming.TargetId; _slots[cell] = request.Incoming.SlotId;
            }
            rows.AddRange([(uint)cell, targets[cell], slots[cell], cells[cell], companions[cell],
                _targets[cell], _slots[cell], _cells[cell], _companions[cell], Accepted ? 1u : 2u]);
        }
        _receipt = rows.ToArray();
    }
    public void Simulate(uint tick) { Simulations++; }
    public CellDelta[] ReadDeltas(uint tick) => _deltas.ToArray();
    public void SwapBuffers(uint tick) { }
    public FireGrid SnapshotGrid => _initial.Grid;
    public FireSimParameters SnapshotParameters => _initial.Parameters;
    public uint SnapshotSeed => _initial.Seed;
    public FireSimSnapshotBuffers ReadSnapshotBuffers() => new(_cells, _initial.TransportFields, _companions, _targets, _slots);
}
