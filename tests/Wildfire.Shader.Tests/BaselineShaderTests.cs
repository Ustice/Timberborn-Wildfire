using Wildfire.Core;
using Wildfire.Unity;
using static Wildfire.Shader.Tests.UnityShaderHarness;

namespace Wildfire.Shader.Tests;

public sealed class BaselineShaderTests
{
    private static readonly FireSimBaselineDefinition[] Definitions =
        [FireSimBaselineDefinition.Empty, FireSimBaselineDefinition.SolidTerrain, FireSimBaselineDefinition.OpenSoil,
         FireSimBaselineDefinition.Water, FireSimBaselineDefinition.Badwater];

    [UnityShaderFact]
    public void EveryTypedBaselineRevealsExactMaterialAndPreservesAmbientStateBeforeSimulation()
    {
        var batch = Batch(owned: true);
        var fixture = Fixture("typed-baseline-owned", batch, owned: true);
        var capture = Capture(fixture);
        var receipt = Decode(capture, batch);
        Assert.True(receipt.Accepted);
        AssertExactApplied(fixture, receipt);
        for (int cell = 0; cell < Definitions.Length; cell++)
        {
            var archive = receipt.ArchiveOutgoing(cell);
            Assert.Equal(new FireSimMaterialIdentity(1, (uint)cell + 1), archive.Identity);
            Assert.Equal((uint)fixture.InitialCells[cell], archive.PackedCell);
            Assert.Equal(fixture.CompanionFields![cell], archive.Companion);
        }
        Assert.All(capture.FinalTargetIds!, target => Assert.Equal(0u, target));
        Assert.All(capture.FinalSlotIds!, slot => Assert.Equal(0u, slot));
        // Identical post-marker starting fields must produce identical transport. A marker reset of
        // atmospheric ash/smoke/steam would distinguish this control from the handoff execution.
        var control = Capture(fixture with
        {
            Scenario = "typed-baseline-owned-control",
            InitialCells = receipt.Cells.Select(cell => (ushort)cell.AppliedCell).ToArray(),
            CompanionFields = receipt.Cells.Select(cell => cell.AppliedCompanion).ToArray(),
            InitialTargetIds = new uint[Definitions.Length], InitialSlotIds = new uint[Definitions.Length],
            ExternalChanges = null, MaterialHandoffs = null,
        });
        Assert.Equal(control.FinalAtmosphericFields, capture.FinalAtmosphericFields);
        Assert.Equal(control.FinalPackedCells, capture.FinalPackedCells);
        Assert.Equal(control.FinalCompanionFields, capture.FinalCompanionFields);
    }

    [UnityShaderFact]
    public void ExistingUnownedCellsCanChangeBaselineWithoutInventingMaterialArchives()
    {
        var batch = Batch(owned: false);
        var fixture = Fixture("typed-baseline-unowned", batch, owned: false);
        var capture = Capture(fixture);
        var receipt = Decode(capture, batch);
        Assert.True(receipt.Accepted);
        AssertExactApplied(fixture, receipt);
        for (int cell = 0; cell < Definitions.Length; cell++)
            Assert.Throws<InvalidOperationException>(() => receipt.ArchiveOutgoing(cell));
        Assert.All(capture.FinalTargetIds!, target => Assert.Equal(0u, target));
        Assert.All(capture.FinalSlotIds!, slot => Assert.Equal(0u, slot));
    }

    [UnityShaderFact]
    public void NoncanonicalBaselineWordsRejectTheEntireBatchBeforeAnyMaterialWrite()
    {
        (string Name, int Word, uint Value)[] invalid =
        [
            ("fuel", 6, 1u), ("flammability", 6, 1u << 8), ("burning", 6, 1u << 13),
            ("capacity", 7, 1u << 8), ("history", 7, 1u << 12),
            ("ash", 7, 1u << 16), ("soil", 7, 1u << 25),
            ("water-without-policy", 7, 8u), ("noncanonical-terrain", 6, 1u << 12),
            ("owned-incoming", 3, 99u),
        ];
        foreach (var invalidField in invalid)
        {
            var batch = new FireSimMaterialHandoffBatch(1,
                [FireSimMaterialHandoffRequest.SetBaseline(0, new(1, 1), FireSimBaselineDefinition.Water),
                 FireSimMaterialHandoffRequest.SetBaseline(1, new(1, 2), FireSimBaselineDefinition.Empty)]);
            var fixture = Fixture("typed-baseline-reject-" + invalidField.Name, batch, owned: true);
            var request = fixture.MaterialHandoffs![0].Requests;
            request[FireSimMaterialHandoffProtocol.RequestWords + invalidField.Word] = invalidField.Value;
            var receipt = Decode(Capture(fixture), batch);
            Assert.False(receipt.Accepted);
            Assert.All(receipt.Cells, cell =>
            {
                Assert.Equal(cell.Prior, cell.Applied);
                Assert.Equal(cell.PriorCell, cell.AppliedCell);
                Assert.Equal(cell.PriorCompanion, cell.AppliedCompanion);
            });
        }
    }

    private static void AssertExactApplied(ShaderSnapshotFixture fixture, FireSimMaterialHandoffReceipt receipt)
    {
        for (int cell = 0; cell < receipt.Cells.Count; cell++)
        {
            var row = receipt.Cells[cell];
            Assert.Equal((uint)fixture.InitialCells[cell], row.PriorCell);
            Assert.Equal(fixture.CompanionFields![cell], row.PriorCompanion);
            Assert.Equal((row.PriorCell & 0x0cf0u) | Definitions[cell].PackedMaterial, row.AppliedCell);
            Assert.Equal((row.PriorCompanion & ~FireSimMaterialHandoffProtocol.CompanionMaterialMask) |
                Definitions[cell].CompanionMaterial, row.AppliedCompanion);
        }
    }

    private static FireSimMaterialHandoffBatch Batch(bool owned) => new(1, Definitions.Select((definition, cell) =>
        FireSimMaterialHandoffRequest.SetBaseline(cell, owned ? new(1, (uint)cell + 1) : default, definition)));

    private static ShaderSnapshotFixture Fixture(string scenario, FireSimMaterialHandoffBatch batch, bool owned)
    {
        int count = Definitions.Length;
        var cells = Enumerable.Range(0, count).Select(i => PackedCell.Pack(owned ? 3 : 0, 9, owned ? 2 : 0, 2, 1, 0)).ToArray();
        var companion = Enumerable.Range(0, count).Select(i => new WildfireMaterialFieldState(
            owned ? WildfireMaterialClass.Tree : WildfireMaterialClass.Terrain, (byte)(owned ? 7 : 0),
            (byte)(owned ? 5 : 0), 3, WildfireAshQuality.None, WildfireContaminationBehavior.None, 6).Pack()).ToArray();
        uint transport = new WildfireTransportFieldState(5, 4, 2, 2, 3, false).Pack();
        return new(1, scenario, 89, new(count, 1, 1), new(0, 0, count), cells,
            CompanionFields: companion, InitialAtmosphericFields: Enumerable.Repeat(transport, count).ToArray(),
            InitialTargetIds: Enumerable.Repeat(owned ? 1u : 0u, count).ToArray(),
            InitialSlotIds: Enumerable.Range(0, count).Select(i => owned ? (uint)i + 1 : 0u).ToArray(),
            ExternalChanges: [ShaderSnapshotExternalChanges.Encode(1, new FireSimChange(0, MaterialHandoff: batch))],
            MaterialHandoffs: [ShaderSnapshotMaterialHandoff.Encode(1, batch)]);
    }

    private static FireSimMaterialHandoffReceipt Decode(ShaderSnapshotCapture capture, FireSimMaterialHandoffBatch batch)
    {
        var tick = Assert.Single(capture.Ticks);
        return FireSimMaterialHandoffProtocol.DecodeReceipt(batch, tick.MaterialHeader!, tick.MaterialReceipts!);
    }
}
