namespace Wildfire.Core;

/// <summary>Complete simulator state. Legacy cells-only data is not a complete snapshot.</summary>
public sealed record FireSimSnapshot(
    int Version,
    FireGrid Grid,
    uint Tick,
    FireSimParameters Parameters,
    uint Seed,
    ushort[] Cells,
    uint[] TransportFields,
    uint[] CompanionFields,
    uint[] TargetIds,
    uint[] SlotIds,
    FireSimMaterialAuthoritySnapshot MaterialAuthority,
    FireSimChange[] PendingChanges)
{
    public const int CurrentVersion = 1;
}

public sealed record FireSimMaterialAuthoritySnapshot(
    uint LastAttemptToken,
    FireSimMaterialIdentity[] KnownSlots,
    FireSimMaterialArchiveSnapshot[] Archives);

/// <summary>Persisted GPU-authored bytes; only complete validated restore may reconstitute archive authority.</summary>
public sealed record FireSimMaterialArchiveSnapshot(
    FireSimMaterialIdentity Identity, uint CaptureToken, int SourceCellIndex, uint PackedCell, uint Companion);

public interface IFireSimSnapshotSimulator
{
    FireSimSnapshot CaptureSnapshot();
}

public sealed record FireSimSnapshotBuffers(
    ushort[] Cells, uint[] TransportFields, uint[] CompanionFields, uint[] TargetIds, uint[] SlotIds);

public interface IFireSimSnapshotBackend : IFireSimStepBackend
{
    FireGrid SnapshotGrid { get; }
    FireSimParameters SnapshotParameters { get; }
    uint SnapshotSeed { get; }
    FireSimSnapshotBuffers ReadSnapshotBuffers();
}
