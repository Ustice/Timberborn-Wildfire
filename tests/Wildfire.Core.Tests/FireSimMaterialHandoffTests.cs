namespace Wildfire.Core.Tests;

public sealed class FireSimMaterialHandoffTests
{
    private static readonly FireSimMaterialIdentity A0 = new(1, 11);
    private static readonly FireSimMaterialIdentity A1 = new(1, 12);
    private static readonly FireSimMaterialIdentity B0 = new(2, 21);
    private static readonly FireSimMaterialIdentity B1 = new(2, 22);

    [Fact]
    public void WholeFootprintAdmissionRejectsPartialCapacityAndPreviouslyKnownFreshSlot()
    {
        FireSimMaterialHandoffBatch batch = Replacement();
        Assert.False(batch.ValidateAdmission(2, 1, [A0, A1], []));
        Assert.True(batch.ValidateAdmission(2, 2, [A0, A1], []));
        Assert.Throws<ArgumentException>(() => batch.ValidateAdmission(2, 2, [A0, A1, B0], []));
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.ValidateAdmission(1, 2, [A0, A1], []));
    }

    [Fact]
    public void FreshAdmissionAllowsOnlyNeverActivatedPairsEvenWhenTheirTargetIsAlreadyKnown()
    {
        var newSlot = new FireSimMaterialIdentity(1, 13);
        var batch = new FireSimMaterialHandoffBatch(1,
            [FireSimMaterialHandoffRequest.Fresh(0, A0, A1, Definition()),
             FireSimMaterialHandoffRequest.Fresh(1, default, newSlot, Definition())]);
        Assert.True(batch.ValidateAdmission(2, 2, [A0], []));
        Assert.Throws<ArgumentException>(() => batch.ValidateAdmission(2, 2, [A0, A1], []));
        Assert.Throws<ArgumentException>(() => new FireSimMaterialHandoffBatch(1,
            [FireSimMaterialHandoffRequest.Fresh(0, A0, A1, Definition()),
             FireSimMaterialHandoffRequest.Fresh(1, default, A1, Definition())]));
    }

    [Fact]
    public void LayoutExplicitlyIncludesTargetAndSlotWithoutChangingNormalCommandBytes()
    {
        FireSimMaterialHandoffBatch batch = Replacement();
        FireSimGpuChange marker = FireSimMaterialHandoffProtocol.EncodeMarker(batch);
        Assert.Equal(16, FireSimGpuProtocol.ChangeStrideBytes);
        Assert.Equal(40, FireSimMaterialHandoffProtocol.RequestStrideBytes);
        Assert.Equal(40, FireSimMaterialHandoffProtocol.ReceiptStrideBytes);
        Assert.Equal(0u, marker.CellIndex);
        Assert.Equal(0x80000000u, marker.SetMask);
        Assert.Equal(2u, marker.AddFields);
        Assert.Equal(71u, marker.SetValues);
        uint[] words = FireSimMaterialHandoffProtocol.EncodeRequests(batch);
        Assert.Equal(new uint[] { 0, 1, 11, 2, 21, 1, 0x1209, 0x600705, uint.MaxValue, 0 }, words.Take(10));
        Assert.Equal(new uint[] { 7, 0, 0x01800000, 0 }, FireSimGpuProtocol.EncodeWords([new(7, AddWater: 3)], 1));
        Assert.Equal(new uint[] { 7, 0x800, 0x06000000, 0 }, FireSimGpuProtocol.EncodeWords([new(7, CollectCleanAsh: 3)], 1));
    }

    [Fact]
    public void ArchivesBindDistinctSlotsOfOneOwnerAndRestoreExactDepletedState()
    {
        FireSimMaterialHandoffReceipt receipt = CaptureReplacement();
        FireSimMaterialArchive first = receipt.ArchiveOutgoing(0);
        FireSimMaterialArchive exhausted = receipt.ArchiveOutgoing(1);
        Assert.Same(first, receipt.ArchiveOutgoing(0));
        Assert.Equal(A0, first.Identity);
        Assert.Equal(A1, exhausted.Identity);
        FireSimMaterialHandoffBatch restore = new(72,
            [FireSimMaterialHandoffRequest.RestoreArchived(0, B0, first), FireSimMaterialHandoffRequest.RestoreArchived(1, B1, exhausted)]);
        Assert.True(restore.ValidateAdmission(2, 2, [A0, A1, B0, B1], [first, exhausted]));
        uint[] words = FireSimMaterialHandoffProtocol.EncodeRequests(restore);
        Assert.Equal(3u, words[6] & 15u);
        Assert.Equal(5u, (words[7] >> 12) & 15u);
        Assert.Equal(0u, words[16] & 15u);
        Assert.Equal(9u, (words[17] >> 12) & 15u);
        Assert.Throws<ArgumentException>(() => restore.ValidateAdmission(2, 2, [A0, A1, B0, B1], [first]));
        Assert.Throws<ArgumentException>(() => restore.ValidateAdmission(2, 2, [A0, B0, B1], [first, exhausted]));
        Assert.Throws<ArgumentException>(() => FireSimMaterialHandoffRequest.RestoreArchived(0, A0, first));
    }

    [Fact]
    public void DuplicateCellsAndDuplicatedActiveOrIncomingSlotsReject()
    {
        Assert.Throws<ArgumentException>(() => new FireSimMaterialHandoffBatch(1,
            [FireSimMaterialHandoffRequest.Remove(0, A0, true), FireSimMaterialHandoffRequest.Remove(0, A1, true)]));
        Assert.Throws<ArgumentException>(() => new FireSimMaterialHandoffBatch(1,
            [FireSimMaterialHandoffRequest.Remove(0, A0, true), FireSimMaterialHandoffRequest.Remove(1, A0, true)]));
        FireSimMaterialArchive archive = CaptureReplacement().ArchiveOutgoing(0);
        Assert.Throws<ArgumentException>(() => new FireSimMaterialHandoffBatch(1,
            [FireSimMaterialHandoffRequest.RestoreArchived(0, B0, archive), FireSimMaterialHandoffRequest.RestoreArchived(1, B1, archive)]));
    }

    [Fact]
    public void ArchiveCannotBeReplayedAfterAnAcceptedLaterBatchConsumesIt()
    {
        FireSimMaterialArchive oldA = CaptureReplacement().ArchiveOutgoing(0);
        List<FireSimMaterialArchive> available = [oldA];
        FireSimMaterialHandoffBatch restore = new(72, [FireSimMaterialHandoffRequest.RestoreArchived(0, B0, oldA)]);
        Assert.True(restore.ValidateAdmission(2, 2, [A0, B0], available));
        FireSimMaterialHandoffReceipt accepted = Decode(restore,
            Row(0, B0, Cell(9, 0), Companion(0), A0, Cell(3, 0), Companion(5)));

        // Future coordinator owns this atomic archive transfer; callers do not manufacture authority.
        available.Remove(oldA);
        available.Add(accepted.ArchiveOutgoing(0));
        FireSimMaterialHandoffBatch replay = new(73, [FireSimMaterialHandoffRequest.RestoreArchived(1, default, oldA)]);
        Assert.Throws<ArgumentException>(() => replay.ValidateAdmission(2, 2, [A0, B0], available));
        Assert.Equal(B0, Assert.Single(available).Identity);
    }

    [Fact]
    public void SameOwnerDifferentSlotDoesNotSatisfyAnExpectedActiveIdentity()
    {
        FireSimMaterialHandoffBatch batch = new(74, [FireSimMaterialHandoffRequest.Remove(0, A0, true)]);
        uint[] receipt = Row(0, A1, Cell(0), Companion(9), default,
            PackedCell.Pack(0, 9, 0, 2, 1, 0), Companion(9) & ~FireSimMaterialHandoffProtocol.CompanionMaterialMask | 1u);
        Assert.Throws<InvalidOperationException>(() => Decode(batch, receipt));
    }

    [Fact]
    public void SameOwnerSwapUsesCapturedSourceSlotAndCannotAlsoArchiveMovedMaterial()
    {
        FireSimMaterialHandoffBatch swap = new(88,
            [FireSimMaterialHandoffRequest.RestoreCaptured(1, A1, A0, 0), FireSimMaterialHandoffRequest.RestoreCaptured(0, A0, A1, 1)]);
        Assert.True(swap.ValidateAdmission(2, 2, [A0, A1], []));
        uint[] requests = FireSimMaterialHandoffProtocol.EncodeRequests(swap);
        Assert.Equal(1u, requests[8]);
        Assert.Equal(0u, requests[18]);
        uint[] words = [.. Row(0, A0, Cell(3), Companion(5), A1, Cell(0, burning: 0), Companion(9)),
                        .. Row(1, A1, Cell(0), Companion(9), A0, Cell(3, burning: 0), Companion(5))];
        FireSimMaterialHandoffReceipt receipt = Decode(swap, words);
        Assert.Equal(0u, receipt.Cells[0].AppliedCell & 15u);
        Assert.Equal(3u, receipt.Cells[1].AppliedCell & 15u);
        Assert.Throws<InvalidOperationException>(() => receipt.ArchiveOutgoing(0));
        Assert.Throws<InvalidOperationException>(() => receipt.ArchiveOutgoing(1));
    }

    [Fact]
    public void CapturedSourceRequiresExactSlotAndRelinquishment()
    {
        Assert.Throws<ArgumentException>(() => new FireSimMaterialHandoffBatch(1,
            [FireSimMaterialHandoffRequest.Remove(0, A0, true), FireSimMaterialHandoffRequest.RestoreCaptured(1, B1, A1, 0)]));
        Assert.Throws<ArgumentException>(() => new FireSimMaterialHandoffBatch(1,
            [FireSimMaterialHandoffRequest.Refresh(0, A0, Definition(0)), FireSimMaterialHandoffRequest.RestoreCaptured(1, B1, A0, 0)]));
    }

    [Fact]
    public void RejectedWholeBatchCannotPublishPartialMaterialOrProduceArchives()
    {
        FireSimMaterialHandoffBatch batch = Replacement();
        uint[] rows = [.. Row(0, A0, Cell(3), Companion(5), A0, Cell(3), Companion(5)),
                       .. Row(1, B1, Cell(0), Companion(9), B1, Cell(0), Companion(9), status: 2)];
        FireSimMaterialHandoffReceipt receipt = FireSimMaterialHandoffProtocol.DecodeReceipt(batch, [71, 2, 2, 1], rows);
        Assert.False(receipt.Accepted);
        Assert.Throws<InvalidOperationException>(() => receipt.ArchiveOutgoing(0));
        rows[7] ^= 1; // Even a first-cell partial write invalidates a rejected receipt.
        Assert.Throws<InvalidOperationException>(() => FireSimMaterialHandoffProtocol.DecodeReceipt(batch, [71, 2, 2, 1], rows));
    }

    [Fact]
    public void ReceiptRejectsSlotTamperingEnvironmentalChangesAndMissingRows()
    {
        FireSimMaterialHandoffBatch batch = Replacement();
        foreach (int word in new[] { 2, 6, 7, 8, 9 })
        {
            uint[] rows = ReplacementRows();
            rows[word] ^= word == 7 ? 0x10u : 1u;
            Assert.Throws<InvalidOperationException>(() => Decode(batch, rows));
        }
        Assert.Throws<InvalidOperationException>(() => Decode(batch, ReplacementRows()[..10]));
        Assert.Throws<InvalidOperationException>(() => FireSimMaterialHandoffProtocol.DecodeReceipt(batch, [70, 1, 2, uint.MaxValue], ReplacementRows()));
        Assert.Throws<InvalidOperationException>(() => FireSimMaterialHandoffProtocol.DecodeReceipt(batch, [71, 0, 2, uint.MaxValue], ReplacementRows()));
    }

    [Fact]
    public void UnchangedRefreshCannotRefillFuelOrArchiveActiveState()
    {
        Assert.Throws<ArgumentException>(() => FireSimMaterialHandoffRequest.Refresh(0, A0, Definition(4)));
        FireSimMaterialHandoffBatch batch = new(90, [FireSimMaterialHandoffRequest.Refresh(0, A0, Definition(0))]);
        FireSimMaterialHandoffReceipt receipt = Decode(batch, Row(0, A0, Cell(0), Companion(9), A0, Cell(0), Companion(9)));
        Assert.Equal(Cell(0), receipt.Cells[0].AppliedCell);
        Assert.Throws<InvalidOperationException>(() => receipt.ArchiveOutgoing(0));
    }

    private static FireSimMaterialDefinition Definition(byte fuel = 9) => new(WildfireMaterialClass.Building, 7,
        WildfireAshQuality.Spent, WildfireContaminationBehavior.TaintIfSourceContaminated, fuel, 2, 1);
    private static uint Cell(byte fuel, byte burning = 2) => PackedCell.Pack(fuel, 9, 2, 2, 1, burning);
    private static uint Companion(byte history) => new WildfireMaterialFieldState(WildfireMaterialClass.Building, 7,
        history, 9, WildfireAshQuality.Spent, WildfireContaminationBehavior.TaintIfSourceContaminated, 6).Pack();
    private static FireSimMaterialHandoffBatch Replacement() => new(71,
        [FireSimMaterialHandoffRequest.Fresh(1, A1, B1, Definition()), FireSimMaterialHandoffRequest.Fresh(0, A0, B0, Definition())]);
    private static uint[] ReplacementRows() => [.. Row(0, A0, Cell(3), Companion(5), B0, Cell(9, 0), Companion(0)),
                                               .. Row(1, A1, Cell(0), Companion(9), B1, Cell(9, 0), Companion(0))];
    private static FireSimMaterialHandoffReceipt CaptureReplacement() => Decode(Replacement(), ReplacementRows());
    private static FireSimMaterialHandoffReceipt Decode(FireSimMaterialHandoffBatch batch, uint[] words) =>
        FireSimMaterialHandoffProtocol.DecodeReceipt(batch, [batch.Token, 1, (uint)batch.Requests.Count, uint.MaxValue], words);
    // Deliberately synthetic records validate protocol invariants only; they are not GPU execution evidence.
    private static uint[] Row(int cell, FireSimMaterialIdentity prior, uint priorCell, uint priorCompanion,
        FireSimMaterialIdentity applied, uint appliedCell, uint appliedCompanion, uint status = 1) =>
        [(uint)cell, prior.TargetId, prior.SlotId, priorCell, priorCompanion, applied.TargetId, applied.SlotId, appliedCell, appliedCompanion, status];
}
