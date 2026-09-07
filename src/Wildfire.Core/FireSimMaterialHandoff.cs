namespace Wildfire.Core;

public readonly record struct FireSimMaterialDefinition
{
    public FireSimMaterialDefinition(WildfireMaterialClass materialClass, byte capacity,
        WildfireAshQuality ashQuality, WildfireContaminationBehavior contaminationBehavior,
        byte fuel, byte flammability, byte terrain)
    {
        if (!Enum.IsDefined(typeof(WildfireMaterialClass), materialClass) ||
            !Enum.IsDefined(typeof(WildfireAshQuality), ashQuality) ||
            !Enum.IsDefined(typeof(WildfireContaminationBehavior), contaminationBehavior))
            throw new ArgumentException("Unknown material profile enum.");
        if (capacity > 15 || fuel > 15 || flammability > 3 || terrain > 1)
            throw new ArgumentOutOfRangeException(nameof(fuel), "Material profile exceeds packed ranges.");
        PackedMaterial = (uint)fuel | ((uint)flammability << 8) | ((uint)terrain << 12);
        CompanionMaterial = new WildfireMaterialFieldState(materialClass, capacity, 0, 0,
            ashQuality, contaminationBehavior).Pack();
    }

    public uint PackedMaterial { get; }
    public uint CompanionMaterial { get; }
}

/// <summary>Session material identity: an entity target plus one stable local-footprint slot.</summary>
public readonly record struct FireSimMaterialIdentity
{
    public FireSimMaterialIdentity(uint targetId, uint slotId)
    {
        if ((targetId == 0) != (slotId == 0)) throw new ArgumentException("Unowned target and slot IDs must both be zero.");
        TargetId = targetId;
        SlotId = slotId;
    }
    public uint TargetId { get; }
    public uint SlotId { get; }
    public bool IsOwned => TargetId != 0;
}

public enum FireSimMaterialHandoffMode : uint
{
    Refresh = 0,
    Fresh = 1,
    Archived = 2,
    CapturedSource = 3,
    Baseline = 4,
}

/// <summary>One immutable GPU-authored outgoing state. Only validated accepted receipts create archives.</summary>
public sealed class FireSimMaterialArchive
{
    internal FireSimMaterialArchive(uint token, int cellIndex, FireSimMaterialIdentity identity, uint cell, uint companion)
    {
        CaptureToken = token;
        SourceCellIndex = cellIndex;
        Identity = identity;
        PackedCell = cell;
        Companion = companion;
    }

    public uint CaptureToken { get; }
    public int SourceCellIndex { get; }
    public FireSimMaterialIdentity Identity { get; }
    public uint PackedCell { get; }
    public uint Companion { get; }
}

public sealed class FireSimMaterialHandoffRequest
{
    private FireSimMaterialHandoffRequest(int cellIndex, FireSimMaterialIdentity expected, FireSimMaterialIdentity incoming,
        FireSimMaterialHandoffMode mode, uint material = 0, uint companion = 0,
        int sourceCellIndex = -1, FireSimMaterialArchive? archive = null)
    {
        if (cellIndex < 0) throw new ArgumentOutOfRangeException(nameof(cellIndex));
        CellIndex = cellIndex;
        Expected = expected;
        Incoming = incoming;
        Mode = mode;
        PackedMaterial = material;
        CompanionMaterial = companion;
        SourceCellIndex = sourceCellIndex;
        Archive = archive;
    }

    public int CellIndex { get; }
    public FireSimMaterialIdentity Expected { get; }
    public FireSimMaterialIdentity Incoming { get; }
    public FireSimMaterialHandoffMode Mode { get; }
    public uint PackedMaterial { get; }
    public uint CompanionMaterial { get; }
    public int SourceCellIndex { get; }
    public FireSimMaterialArchive? Archive { get; }

    public static FireSimMaterialHandoffRequest Fresh(int cell, FireSimMaterialIdentity expectedOwner, FireSimMaterialIdentity newOwner, FireSimMaterialDefinition material)
    {
        RequireOwner(newOwner);
        if (expectedOwner == newOwner) throw new ArgumentException("A fresh owner must differ from the current owner.");
        return new(cell, expectedOwner, newOwner, FireSimMaterialHandoffMode.Fresh, material.PackedMaterial, material.CompanionMaterial);
    }

    public static FireSimMaterialHandoffRequest Refresh(int cell, FireSimMaterialIdentity owner, FireSimMaterialDefinition material)
    {
        RequireOwner(owner);
        if ((material.PackedMaterial & 15u) != 0) throw new ArgumentException("Refresh cannot supply fuel.");
        return new(cell, owner, owner, FireSimMaterialHandoffMode.Refresh, material.PackedMaterial, material.CompanionMaterial);
    }

    public static FireSimMaterialHandoffRequest RestoreArchived(int cell, FireSimMaterialIdentity expectedOwner, FireSimMaterialArchive archive)
    {
        if (archive is null) throw new ArgumentNullException(nameof(archive));
        if (expectedOwner == archive.Identity) throw new ArgumentException("An active slot cannot also be restored from its inactive archive.");
        return new(cell, expectedOwner, archive.Identity, FireSimMaterialHandoffMode.Archived,
            archive.PackedCell & FireSimMaterialHandoffProtocol.PackedMaterialMask,
            archive.Companion & FireSimMaterialHandoffProtocol.CompanionMaterialMask, archive: archive);
    }

    public static FireSimMaterialHandoffRequest RestoreCaptured(int cell, FireSimMaterialIdentity expectedOwner, FireSimMaterialIdentity incomingOwner, int sourceCell)
    {
        RequireOwner(incomingOwner);
        if (sourceCell < 0 || sourceCell == cell) throw new ArgumentOutOfRangeException(nameof(sourceCell));
        return new(cell, expectedOwner, incomingOwner, FireSimMaterialHandoffMode.CapturedSource, sourceCellIndex: sourceCell);
    }

    public static FireSimMaterialHandoffRequest Remove(int cell, FireSimMaterialIdentity expectedOwner, bool terrain)
    {
        RequireOwner(expectedOwner);
        return new(cell, expectedOwner, default, FireSimMaterialHandoffMode.Baseline,
            terrain ? 1u << 12 : 0, terrain ? (uint)WildfireMaterialClass.Terrain : 0);
    }

    private static void RequireOwner(FireSimMaterialIdentity owner)
    {
        if (!owner.IsOwned) throw new ArgumentOutOfRangeException(nameof(owner), "Material owner must be nonzero.");
    }
}

/// <summary>
/// Whole transition, sorted by destination cell. Prototype admission requires session-known owners;
/// returning owners need a known archive or a captured source, never a fresh activation shortcut.
/// </summary>
public sealed class FireSimMaterialHandoffBatch
{
    public FireSimMaterialHandoffBatch(uint token, IEnumerable<FireSimMaterialHandoffRequest> requests)
    {
        if (token == 0) throw new ArgumentOutOfRangeException(nameof(token));
        if (requests is null) throw new ArgumentNullException(nameof(requests));
        FireSimMaterialHandoffRequest[] sorted = requests.OrderBy(static request => request.CellIndex).ToArray();
        if (sorted.Length == 0) throw new ArgumentException("Material handoff cannot be empty.", nameof(requests));
        Dictionary<int, FireSimMaterialHandoffRequest> byCell = sorted.ToDictionary(static request => request.CellIndex);
        HashSet<int> consumedSources = new();
        HashSet<FireSimMaterialIdentity> outgoingSlots = new();
        HashSet<FireSimMaterialIdentity> incomingSlots = new();
        HashSet<(uint Token, int Cell)> consumedArchives = new();
        foreach (FireSimMaterialHandoffRequest request in sorted)
        {
            if (request.Expected.IsOwned && !outgoingSlots.Add(request.Expected))
                throw new ArgumentException("An active material slot cannot occupy two source cells.");
            if (request.Incoming.IsOwned && !incomingSlots.Add(request.Incoming))
                throw new ArgumentException("A material slot cannot activate two destination cells.");
            if (request.Mode == FireSimMaterialHandoffMode.CapturedSource &&
                (!byCell.TryGetValue(request.SourceCellIndex, out FireSimMaterialHandoffRequest source) ||
                 source.Expected != request.Incoming || source.Mode == FireSimMaterialHandoffMode.Refresh ||
                 !consumedSources.Add(request.SourceCellIndex)))
                throw new ArgumentException("Captured source must be present, relinquished, match its owner and be consumed once.");
            if (request.Archive is { } archive && !consumedArchives.Add((archive.CaptureToken, archive.SourceCellIndex)))
                throw new ArgumentException("An archived material state cannot activate two destination cells.");
        }
        Token = token;
        Requests = Array.AsReadOnly(sorted);
    }

    public uint Token { get; }
    public IReadOnlyList<FireSimMaterialHandoffRequest> Requests { get; }

    /// <returns>False only when the complete transition does not fit; nothing may be partially admitted.</returns>
    public bool ValidateAdmission(int cellCount, int capacity, IReadOnlyCollection<FireSimMaterialIdentity> knownSlots,
        IReadOnlyCollection<FireSimMaterialArchive> availableArchives)
    {
        if (cellCount <= 0 || capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (knownSlots is null) throw new ArgumentNullException(nameof(knownSlots));
        if (availableArchives is null) throw new ArgumentNullException(nameof(availableArchives));
        HashSet<FireSimMaterialIdentity> known = new(knownSlots);
        HashSet<uint> knownOwners = new(known.Select(static identity => identity.TargetId));
        foreach (FireSimMaterialHandoffRequest request in Requests)
        {
            if (request.CellIndex >= cellCount) throw new ArgumentOutOfRangeException(nameof(cellCount), "Handoff cell is outside the grid.");
            if (request.Expected.IsOwned && !known.Contains(request.Expected))
                throw new ArgumentException("Expected owner is not known to this simulator session.");
            if (request.Mode == FireSimMaterialHandoffMode.Fresh && knownOwners.Contains(request.Incoming.TargetId))
                throw new ArgumentException("Previously known owners require retained state, not fresh fuel.");
            if (request.Mode is FireSimMaterialHandoffMode.Archived or FireSimMaterialHandoffMode.CapturedSource && !known.Contains(request.Incoming))
                throw new ArgumentException("Retained owner is not known to this simulator session.");
            if (request.Archive is { } archive && !availableArchives.Contains(archive))
                throw new ArgumentException("Retained archive is absent or already consumed at the host boundary.");
        }
        return Requests.Count <= capacity;
    }
}
