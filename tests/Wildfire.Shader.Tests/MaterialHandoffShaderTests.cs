using Wildfire.Core;
using Wildfire.Unity;
using static Wildfire.Shader.Tests.UnityShaderHarness;

namespace Wildfire.Shader.Tests;

public sealed class MaterialHandoffShaderTests
{
    private static readonly FireSimMaterialIdentity A0 = new(1, 11), A1 = new(1, 12), B0 = new(2, 21), B1 = new(2, 22);
    private static FireSimMaterialDefinition Definition(byte fuel) => new(WildfireMaterialClass.Tree, 7,
        WildfireAshQuality.Fertile, WildfireContaminationBehavior.None, fuel, 2, 1);
    private static uint Companion(byte history, byte ashStrength = 3) => new WildfireMaterialFieldState(
        WildfireMaterialClass.Tree, 7, history, ashStrength, WildfireAshQuality.Fertile, WildfireContaminationBehavior.None, 6).Pack();
    private static ushort Cell(byte fuel, byte heat = 9, byte water = 2) => PackedCell.Pack(fuel, heat, 2, water, 1, 7);
    private static FireSimMaterialHandoffBatch Replace(uint token = 1) => new(token,
        [FireSimMaterialHandoffRequest.Fresh(0, A0, B0, Definition(9)), FireSimMaterialHandoffRequest.Fresh(1, A1, B1, Definition(9))]);

    [UnityShaderFact]
    public void WholeOwnerReplacementCapturesDistinctBurnedSlotsAndPreservesEnvironment()
    {
        var batch = Replace();
        var fixture = Fixture("material-fresh-owner", batch);
        var capture = Capture(fixture);
        var receipt = Receipt(capture, batch);
        Assert.True(receipt.Accepted);
        Assert.Equal(new uint[] { 2, 2 }, capture.FinalTargetIds);
        Assert.Equal(new uint[] { 21, 22 }, capture.FinalSlotIds);
        Assert.Equal(3u, receipt.ArchiveOutgoing(0).PackedCell & 15u);
        Assert.Equal(0u, receipt.ArchiveOutgoing(1).PackedCell & 15u);
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal((uint)fixture.InitialCells[i], receipt.Cells[i].PriorCell);
            Assert.Equal(fixture.CompanionFields![i], receipt.Cells[i].PriorCompanion);
            Assert.Equal((uint)(fixture.InitialCells[i] & 0x0cf0) | Definition(9).PackedMaterial, receipt.Cells[i].AppliedCell);
            Assert.Equal(0u, receipt.Cells[i].AppliedCell >> 13); // Activation does not inherit burning level.
        }
        Assert.All(Assert.Single(capture.Ticks).Deltas, delta => Assert.Equal(2u, delta.TargetId));
    }

    [UnityShaderFact]
    public void ActualGpuArchivesRestoreHiddenFuelAndHistoryWithoutRefillingAcrossCycles()
    {
        var replace = Replace();
        var first = Capture(Fixture("material-hide", replace));
        var archived = Receipt(first, replace);
        var restore = new FireSimMaterialHandoffBatch(2,
            [FireSimMaterialHandoffRequest.RestoreArchived(0, B0, archived.ArchiveOutgoing(0)),
             FireSimMaterialHandoffRequest.RestoreArchived(1, B1, archived.ArchiveOutgoing(1))]);
        var second = Capture(Continue("material-reexpose", first, restore));
        var restored = Receipt(second, restore);
        Assert.True(restored.Accepted);
        Assert.Equal(3u, restored.Cells[0].AppliedCell & 15u);
        Assert.Equal(0u, restored.Cells[1].AppliedCell & 15u);
        Assert.Equal(5u, (restored.Cells[0].AppliedCompanion >> 12) & 15u);
        Assert.Equal(9u, (restored.Cells[1].AppliedCompanion >> 12) & 15u);
        Assert.Equal(new uint[] { 11, 12 }, second.FinalSlotIds);
        // A third transition consumes the second GPU receipt, never fabricated retained material.
        var hideAgain = new FireSimMaterialHandoffBatch(3,
            [FireSimMaterialHandoffRequest.RestoreArchived(0, A0, restored.ArchiveOutgoing(0)),
             FireSimMaterialHandoffRequest.RestoreArchived(1, A1, restored.ArchiveOutgoing(1))]);
        var third = Capture(Continue("material-hide-again", second, hideAgain));
        var recaptured = Receipt(third, hideAgain);
        Assert.Equal(second.FinalPackedCells[0], recaptured.ArchiveOutgoing(0).PackedCell);
        Assert.Equal(0u, recaptured.ArchiveOutgoing(1).PackedCell & 15u);
    }

    [UnityShaderFact]
    public void SameOwnerSlotSwapCapturesBothSourcesBeforeEitherWrite()
    {
        var swap = new FireSimMaterialHandoffBatch(1,
            [FireSimMaterialHandoffRequest.RestoreCaptured(0, A0, A1, 1),
             FireSimMaterialHandoffRequest.RestoreCaptured(1, A1, A0, 0)]);
        var receipt = Receipt(Capture(Fixture("material-slot-swap", swap)), swap);
        Assert.True(receipt.Accepted);
        Assert.Equal(0u, receipt.Cells[0].AppliedCell & 15u);
        Assert.Equal(3u, receipt.Cells[1].AppliedCell & 15u);
        Assert.Equal(9u, (receipt.Cells[0].AppliedCompanion >> 12) & 15u);
        Assert.Equal(5u, (receipt.Cells[1].AppliedCompanion >> 12) & 15u);
        Assert.Throws<InvalidOperationException>(() => receipt.ArchiveOutgoing(0));
        Assert.Throws<InvalidOperationException>(() => receipt.ArchiveOutgoing(1));
    }

    [UnityShaderFact]
    public void OneStaleSlotRejectsEveryMaterialWriteButEarlierWaterInputCommits()
    {
        var batch = Replace();
        var fixture = Fixture("material-stale-slot", batch, [new(0, AddWater: 1)]);
        fixture.MaterialHandoffs![0].Requests[12] = 999; // Actual request disagrees with GPU cell1 slot12.
        var capture = Capture(fixture);
        var receipt = Receipt(capture, batch);
        Assert.False(receipt.Accepted);
        Assert.Equal(3u, (receipt.Cells[0].PriorCell >> 10) & 3u);
        Assert.All(receipt.Cells, cell =>
        {
            Assert.Equal(cell.Prior, cell.Applied);
            Assert.Equal(cell.PriorCell, cell.AppliedCell);
            Assert.Equal(cell.PriorCompanion, cell.AppliedCompanion);
        });
        Assert.Equal(new uint[] { 1, 1 }, capture.FinalTargetIds);
        Assert.Contains(Assert.Single(capture.Ticks).Deltas, delta => delta.CellIndex == 0 && delta.TargetId == 1 && ((delta.NewCell >> 10) & 3) == 3);
    }

    [UnityShaderFact]
    public void RefreshKeepsExhaustedFuelAndHistoryAndProfileMismatchRejects()
    {
        var batch = new FireSimMaterialHandoffBatch(1, [FireSimMaterialHandoffRequest.Refresh(1, A1, Definition(0))]);
        var fixture = Fixture("material-exhausted-refresh", batch);
        var receipt = Receipt(Capture(fixture), batch);
        Assert.True(receipt.Accepted);
        Assert.Equal(0u, receipt.Cells[0].AppliedCell & 15u);
        Assert.Equal(receipt.Cells[0].PriorCompanion, receipt.Cells[0].AppliedCompanion);
        fixture = fixture with { Scenario = "material-refresh-profile-reject" };
        fixture.MaterialHandoffs![0].Requests[7] ^= 1u << 8; // Capacity changes are explicitly unsupported refresh.
        var rejected = Receipt(Capture(fixture), batch);
        Assert.False(rejected.Accepted);
        Assert.Equal(rejected.Cells[0].PriorCell, rejected.Cells[0].AppliedCell);
    }

    [UnityShaderFact]
    public void DeltasBeforeAndAfterMarkerRetainTheirOriginatingOwner()
    {
        var batch = Replace();
        var fixture = Fixture("material-ordered-owner-deltas", batch, [new(0, AddHeat: 1)]);
        var capture = Capture(fixture);
        var receipt = Receipt(capture, batch);
        Assert.True(receipt.Accepted);
        var deltas = Assert.Single(capture.Ticks).Deltas.Where(delta => delta.CellIndex == 0).ToArray();
        Assert.Equal(1u, deltas[0].TargetId);
        Assert.Contains(deltas.Skip(1), delta => delta.TargetId == 2);
        Assert.DoesNotContain(deltas, delta => delta.OldCell == receipt.Cells[0].PriorCell && delta.NewCell == receipt.Cells[0].AppliedCell);
        // This proves readback identity only: production native consequence decisions still discard metadata.
    }

    [UnityShaderFact]
    public void RemovingBothSlotsLeavesExplicitTerrainOrAirAndArchivesExactPriorMaterial()
    {
        var batch = new FireSimMaterialHandoffBatch(1,
            [FireSimMaterialHandoffRequest.Remove(0, A0, true), FireSimMaterialHandoffRequest.Remove(1, A1, false)]);
        var capture = Capture(Fixture("material-remove-terrain-air", batch));
        var receipt = Receipt(capture, batch);
        Assert.True(receipt.Accepted);
        Assert.Equal(0x1000u, receipt.Cells[0].AppliedCell & FireSimMaterialHandoffProtocol.PackedMaterialMask);
        Assert.Equal(0u, receipt.Cells[1].AppliedCell & FireSimMaterialHandoffProtocol.PackedMaterialMask);
        Assert.Equal(1u, receipt.Cells[0].AppliedCompanion & FireSimMaterialHandoffProtocol.CompanionMaterialMask);
        Assert.Equal(0u, receipt.Cells[1].AppliedCompanion & FireSimMaterialHandoffProtocol.CompanionMaterialMask);
        Assert.Equal(new uint[] { 0, 0 }, capture.FinalTargetIds);
        Assert.Equal(new uint[] { 0, 0 }, capture.FinalSlotIds);
        Assert.Equal(A0, receipt.ArchiveOutgoing(0).Identity);
        Assert.Equal(A1, receipt.ArchiveOutgoing(1).Identity);
        Assert.All(Assert.Single(capture.Ticks).Deltas, delta => Assert.Equal(0u, delta.TargetId));
    }

    [UnityShaderFact]
    public void UnknownModeRejectsWholeBatchWithoutInterpretingMaterialAsOrdinaryMasks()
    {
        var batch = Replace();
        var fixture = Fixture("material-unknown-mode", batch);
        fixture.MaterialHandoffs![0].Requests[15] = 99;
        var receipt = Receipt(Capture(fixture), batch);
        Assert.False(receipt.Accepted);
        Assert.Equal(8u, receipt.Cells[1].Status);
        Assert.All(receipt.Cells, cell => Assert.Equal(cell.PriorCell, cell.AppliedCell));
    }

    private static FireSimMaterialHandoffReceipt Receipt(ShaderSnapshotCapture capture, FireSimMaterialHandoffBatch batch)
    {
        var tick = Assert.Single(capture.Ticks);
        return FireSimMaterialHandoffProtocol.DecodeReceipt(batch, tick.MaterialHeader!, tick.MaterialReceipts!);
    }
    private static ShaderSnapshotFixture Fixture(string name, FireSimMaterialHandoffBatch batch, FireSimChange[]? preceding = null)
    {
        int count = Math.Max(2, (preceding?.Length ?? 0) + 1);
        ushort[] cells = new ushort[count]; cells[0] = Cell(3); cells[1] = Cell(0, 5, 1);
        uint[] companion = new uint[count]; companion[0] = Companion(5); companion[1] = Companion(9, 2);
        uint[] targets = new uint[count]; targets[0] = targets[1] = 1;
        uint[] slots = new uint[count]; slots[0] = 11; slots[1] = 12;
        return new(1, name, 89, new(count, 1, 1), new(0, 0, count), cells,
            CompanionFields: companion, InitialTargetIds: targets, InitialSlotIds: slots,
            ExternalChanges: [ShaderSnapshotExternalChanges.Encode(1, [.. preceding ?? [], new(0, MaterialHandoff: batch)])],
            MaterialHandoffs: [ShaderSnapshotMaterialHandoff.Encode(1, batch)]);
    }
    private static ShaderSnapshotFixture Continue(string name, ShaderSnapshotCapture prior, FireSimMaterialHandoffBatch batch) =>
        new(1, name, prior.Seed, prior.Grid, new(0, 0, prior.Grid.CellCount), prior.FinalPackedCells,
            InitialAtmosphericFields: prior.FinalAtmosphericFields, CompanionFields: prior.FinalCompanionFields,
            InitialTargetIds: prior.FinalTargetIds, InitialSlotIds: prior.FinalSlotIds,
            ExternalChanges: [ShaderSnapshotExternalChanges.Encode(1, new FireSimChange(0, MaterialHandoff: batch))],
            MaterialHandoffs: [ShaderSnapshotMaterialHandoff.Encode(1, batch)]);
}
