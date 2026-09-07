namespace Wildfire.Core;

public interface IFireSimMaterialHandoffSimulator : IFireSimStepInputSimulator
{
    GpuFireStepResult? TryHandoffMaterial(FireSimMaterialHandoffBatch batch, Action<FireSimMaterialHandoffReceipt> commit);
    bool TryGetMaterialArchive(FireSimMaterialIdentity identity, out FireSimMaterialArchive archive);
}

public interface IFireSimMaterialHandoffBackend : IFireSimStepBackend
{
    int MaterialHandoffCapacity { get; }
    void UploadMaterialHandoff(FireSimMaterialHandoffBatch batch);
    uint[] ReadMaterialHandoffHeader();
    uint[] ReadMaterialHandoffReceipts(int count);
}

/// <summary>Host-side authority and exact archived bytes; this class does not evolve material/fire.</summary>
internal sealed class FireSimMaterialHandoffSession
{
    private readonly int _cellCount;
    private Dictionary<int, FireSimMaterialIdentity> _active;
    private HashSet<FireSimMaterialIdentity> _known;
    private Dictionary<FireSimMaterialIdentity, FireSimMaterialArchive> _archives = new();
    private uint _lastAttemptToken;

    public FireSimMaterialHandoffSession(int cellCount, IReadOnlyList<FireSimMaterialIdentity>? initial)
    {
        _cellCount = cellCount;
        if (initial is not null && initial.Count != cellCount) throw new ArgumentException("Initial material identities must match the grid.");
        _active = (initial ?? Enumerable.Repeat(default(FireSimMaterialIdentity), cellCount).ToArray())
            .Select((identity, cell) => (identity, cell)).Where(static pair => pair.identity.IsOwned)
            .ToDictionary(static pair => pair.cell, static pair => pair.identity);
        _known = new(_active.Values);
        if (_known.Count != _active.Count) throw new ArgumentException("Initial material slots must be unique.");
    }

    internal FireSimMaterialHandoffSession(FireSimSnapshot snapshot)
        : this(snapshot.Cells.Length, snapshot.TargetIds.Select((target, cell) =>
            new FireSimMaterialIdentity(target, snapshot.SlotIds[cell])).ToArray())
    {
        _known = new(snapshot.MaterialAuthority.KnownSlots);
        _lastAttemptToken = snapshot.MaterialAuthority.LastAttemptToken;
        _archives = snapshot.MaterialAuthority.Archives.ToDictionary(static entry => entry.Identity,
            static entry => new FireSimMaterialArchive(entry.CaptureToken, entry.SourceCellIndex,
                entry.Identity, entry.PackedCell, entry.Companion));
    }

    internal FireSimMaterialAuthoritySnapshot CaptureAuthority() => new(_lastAttemptToken,
        _known.OrderBy(static id => id.TargetId).ThenBy(static id => id.SlotId).ToArray(),
        _archives.Values.OrderBy(static entry => entry.Identity.TargetId).ThenBy(static entry => entry.Identity.SlotId)
            .Select(static entry => new FireSimMaterialArchiveSnapshot(entry.Identity, entry.CaptureToken,
                entry.SourceCellIndex, entry.PackedCell, entry.Companion)).ToArray());

    internal bool MatchesActive(uint[] targets, uint[] slots)
    {
        if (targets.Length != _cellCount || slots.Length != _cellCount) return false;
        for (int cell = 0; cell < _cellCount; cell++)
        {
            _active.TryGetValue(cell, out var owner);
            if (owner.TargetId != targets[cell] || owner.SlotId != slots[cell]) return false;
        }
        return true;
    }

    public bool TryGetArchive(FireSimMaterialIdentity identity, out FireSimMaterialArchive archive) => _archives.TryGetValue(identity, out archive!);

    public bool Prepare(FireSimMaterialHandoffBatch batch, int capacity)
    {
        if (batch is null) throw new ArgumentNullException(nameof(batch));
        if (batch.Token <= _lastAttemptToken) throw new ArgumentException("Material transaction token was already attempted or is stale.");
        if (!batch.ValidateAdmission(_cellCount, capacity, _known, _archives.Values.ToArray())) return false;
        foreach (FireSimMaterialHandoffRequest request in batch.Requests)
        {
            _active.TryGetValue(request.CellIndex, out FireSimMaterialIdentity current);
            if (current != request.Expected) throw new ArgumentException("Expected material slot does not match current host ownership.");
        }
        ProjectActive(batch); // Validate aliases before upload, including cells outside this batch.
        return true;
    }

    public void BeginAttempt(uint token) => _lastAttemptToken = token;

    public void Commit(FireSimMaterialHandoffBatch batch, FireSimMaterialHandoffReceipt receipt)
    {
        if (!receipt.Accepted)
        {
            // A rejected operation is safe only while GPU ownership still agrees with host authority.
            // Report actual prior identities; never adopt them silently or continue with a divergent map.
            for (int index = 0; index < receipt.Cells.Count; index++)
                if (receipt.Cells[index].Prior != batch.Requests[index].Expected)
                    throw new InvalidOperationException("Rejected material receipt exposes divergent GPU ownership; reconcile before further simulation.");
            return; // Ordinary inputs and simulation still completed under the unchanged material map.
        }
        Dictionary<int, FireSimMaterialIdentity> active = ProjectActive(batch);
        HashSet<FireSimMaterialIdentity> known = new(_known);
        Dictionary<FireSimMaterialIdentity, FireSimMaterialArchive> archives = new(_archives);
        foreach (FireSimMaterialHandoffRequest request in batch.Requests)
        {
            if (request.Incoming.IsOwned) known.Add(request.Incoming);
            if (request.Archive is not null) archives.Remove(request.Archive.Identity);
        }
        HashSet<FireSimMaterialIdentity> activeSlots = new(active.Values);
        for (int index = 0; index < receipt.Cells.Count; index++)
        {
            FireSimMaterialIdentity prior = receipt.Cells[index].Prior;
            if (prior.IsOwned && !activeSlots.Contains(prior)) archives.Add(prior, receipt.ArchiveOutgoing(index));
        }
        _active = active;
        _known = known;
        _archives = archives;
    }

    private Dictionary<int, FireSimMaterialIdentity> ProjectActive(FireSimMaterialHandoffBatch batch)
    {
        Dictionary<int, FireSimMaterialIdentity> active = new(_active);
        foreach (FireSimMaterialHandoffRequest request in batch.Requests) active.Remove(request.CellIndex);
        foreach (FireSimMaterialHandoffRequest request in batch.Requests)
            if (request.Incoming.IsOwned) active.Add(request.CellIndex, request.Incoming);
        if (active.Values.Distinct().Count() != active.Count)
            throw new ArgumentException("Incoming material slot is still active at another cell.");
        return active;
    }
}
