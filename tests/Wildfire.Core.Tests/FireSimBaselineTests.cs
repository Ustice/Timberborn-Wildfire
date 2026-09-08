namespace Wildfire.Core.Tests;

public sealed class FireSimBaselineTests
{
    private static readonly FireSimBaselineDefinition[] Baselines =
        [FireSimBaselineDefinition.Empty, FireSimBaselineDefinition.SolidTerrain, FireSimBaselineDefinition.OpenSoil,
         FireSimBaselineDefinition.Water, FireSimBaselineDefinition.Badwater];

    [Fact]
    public void CanonicalDefinitionsMatchProfilesWithoutCarryingAmbientWater()
    {
        Assert.Equal(default, FireSimBaselineDefinition.Empty);
        foreach (var baseline in Baselines)
        {
            var profile = WildfireMaterialFieldSchema.Default.Lookup(baseline.MaterialClass);
            var material = new FireSimMaterialDefinition(profile.MaterialClass, profile.BurnCapacity, profile.AshQuality,
                profile.ContaminationBehavior, 0, 0, (byte)(baseline.PackedMaterial >> 12));
            Assert.Equal(baseline, new FireSimBaselineDefinition(material));
            Assert.Equal(0u, baseline.PackedMaterial & 0xefffu); // Only the terrain material bit may be set.
            Assert.Equal(0u, baseline.CompanionMaterial & ~FireSimMaterialHandoffProtocol.CompanionMaterialMask);
        }
    }

    [Fact]
    public void NoncanonicalMaterialFieldsCannotBecomeBaselineDefinitions()
    {
        FireSimMaterialDefinition[] invalid =
        [
            new(WildfireMaterialClass.Empty, 0, 0, 0, 1, 0, 0),
            new(WildfireMaterialClass.Empty, 1, 0, 0, 0, 0, 0),
            new(WildfireMaterialClass.Empty, 0, 0, 0, 0, 1, 0),
            new(WildfireMaterialClass.Empty, 0, 0, 0, 0, 0, 1),
            new(WildfireMaterialClass.Terrain, 0, WildfireAshQuality.Fertile, 0, 0, 0, 0),
            new(WildfireMaterialClass.Terrain, 0, 0, WildfireContaminationBehavior.TaintedSource, 0, 0, 1),
            new(WildfireMaterialClass.Water, 0, 0, 0, 0, 0, 0),
            new(WildfireMaterialClass.Water, 0, 0, WildfireContaminationBehavior.SuppressesWithoutCleaning, 0, 0, 1),
            new(WildfireMaterialClass.Badwater, 0, 0, WildfireContaminationBehavior.TaintedSource, 0, 0, 0),
            new(WildfireMaterialClass.Tree, 0, 0, 0, 0, 0, 1),
        ];
        foreach (var material in invalid)
            Assert.Throws<ArgumentException>(() => new FireSimBaselineDefinition(material));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BaselinesPreserveAmbientStateAndOnlyArchiveAnOutgoingOwnedSlot(bool initiallyOwned)
    {
        foreach (var baseline in Baselines)
        {
            var owner = initiallyOwned ? new FireSimMaterialIdentity(7, 9) : default;
            var backend = new Backend(owner);
            var step = new FireSimStepCoordinator(1, 1, [owner]);
            var batch = new FireSimMaterialHandoffBatch(1, [FireSimMaterialHandoffRequest.SetBaseline(0, owner, baseline)]);
            FireSimMaterialHandoffReceipt? receipt = null;
            Assert.NotNull(step.TryHandoffMaterial(backend, batch, value => receipt = value));
            var row = Assert.Single(receipt!.Cells);
            Assert.Equal(0x0cf0u & backend.PriorCell, row.AppliedCell & 0x0cf0u);
            Assert.Equal(baseline.PackedMaterial, row.AppliedCell & FireSimMaterialHandoffProtocol.PackedMaterialMask);
            Assert.Equal(backend.PriorCompanion & ~FireSimMaterialHandoffProtocol.CompanionMaterialMask,
                row.AppliedCompanion & ~FireSimMaterialHandoffProtocol.CompanionMaterialMask);
            var snapshot = step.CaptureSnapshot(backend);
            Assert.Equal(backend.Transport, Assert.Single(snapshot.TransportFields));
            Assert.Equal(0u, Assert.Single(snapshot.TargetIds));
            Assert.Equal(0u, Assert.Single(snapshot.SlotIds));
            Assert.Empty(snapshot.PendingChanges);
            if (initiallyOwned)
            {
                Assert.Equal(owner, Assert.Single(snapshot.MaterialAuthority.KnownSlots));
                var archive = Assert.Single(snapshot.MaterialAuthority.Archives);
                Assert.Equal(backend.PriorCell, archive.PackedCell);
                Assert.Equal(backend.PriorCompanion, archive.Companion);
            }
            else
            {
                Assert.Empty(snapshot.MaterialAuthority.KnownSlots);
                Assert.Empty(snapshot.MaterialAuthority.Archives);
                Assert.Throws<InvalidOperationException>(() => receipt.ArchiveOutgoing(0));
            }
        }
    }

    [Fact]
    public void StaleUnownedExpectationRejectsBeforeDispatchAndLegacyRemoveStillRequiresOwner()
    {
        var owner = new FireSimMaterialIdentity(7, 9);
        var backend = new Backend(owner);
        var step = new FireSimStepCoordinator(1, 1, [owner]);
        var batch = new FireSimMaterialHandoffBatch(1, [FireSimMaterialHandoffRequest.SetBaseline(0, default, FireSimBaselineDefinition.Water)]);
        Assert.Throws<ArgumentException>(() => step.TryHandoffMaterial(backend, batch, _ => { }));
        Assert.Equal(0, backend.Uploads);
        Assert.Throws<ArgumentOutOfRangeException>(() => FireSimMaterialHandoffRequest.Remove(0, default, false));
    }

    [Theory]
    [InlineData(7, 1u)] // Inject fuel into accepted result.
    [InlineData(7, 1u << 13)] // Inject burning level.
    [InlineData(8, 1u << 8)] // Inject capacity.
    [InlineData(8, 1u << 12)] // Inject material burn history.
    [InlineData(8, 1u << 25)] // Change local soil contamination.
    public void InvalidAcceptedBaselineReceiptCannotCommit(int word, uint bit)
    {
        var backend = new Backend(default) { CorruptWord = word, CorruptBit = bit };
        var step = new FireSimStepCoordinator(1, 1, [default]);
        var batch = new FireSimMaterialHandoffBatch(1, [FireSimMaterialHandoffRequest.SetBaseline(0, default, FireSimBaselineDefinition.OpenSoil)]);
        int commits = 0;
        var failure = Assert.Throws<FireSimStepInputException>(() => step.TryHandoffMaterial(backend, batch, _ => commits++));
        Assert.Equal(FireSimStepInputOutcome.Indeterminate, failure.Outcome);
        Assert.Equal(0, commits);
    }

    // Managed receipt/authority seam only; shader execution is covered by BaselineShaderTests.
    private sealed class Backend(FireSimMaterialIdentity owner) : IFireSimMaterialHandoffBackend, IFireSimSnapshotBackend
    {
        public readonly uint PriorCell = PackedCell.Pack(0, 9, 0, 2, 1, 0);
        public readonly uint PriorCompanion = new WildfireMaterialFieldState(WildfireMaterialClass.Terrain, 0, 0, 7,
            WildfireAshQuality.None, WildfireContaminationBehavior.None, 6).Pack();
        public readonly uint Transport = new WildfireTransportFieldState(2, 3, 1, 2, 4, false).Pack();
        private FireSimMaterialHandoffBatch? _batch;
        private uint[] _receipt = [];
        public int Uploads, CorruptWord = -1;
        public uint CorruptBit;
        public int MaterialHandoffCapacity => 1;
        public FireGrid SnapshotGrid => new(1, 1, 1);
        public FireSimParameters SnapshotParameters => FireSimParameters.Default;
        public uint SnapshotSeed => 0;
        public void PrepareMaterialHandoff(FireSimMaterialHandoffBatch batch, int orderedCommandCount) { _batch = batch; Uploads++; }
        public uint[] ReadMaterialHandoffHeader() => [_batch!.Token, 1, 1, uint.MaxValue];
        public uint[] ReadMaterialHandoffReceipts(int count) => _receipt;
        public void ApplyExternalChanges(uint tick, FireSimChange[] changes)
        {
            var request = Assert.Single(_batch!.Requests);
            _receipt = [0, owner.TargetId, owner.SlotId, PriorCell, PriorCompanion, 0, 0,
                (PriorCell & 0x0cf0u) | request.PackedMaterial,
                (PriorCompanion & ~FireSimMaterialHandoffProtocol.CompanionMaterialMask) | request.CompanionMaterial, 1];
            if (CorruptWord >= 0) _receipt[CorruptWord] ^= CorruptBit;
        }
        public FireSimSnapshotBuffers ReadSnapshotBuffers() => new([(ushort)_receipt[7]], [Transport], [_receipt[8]], [0], [0]);
        public void ResetDeltaCounter(uint tick) { }
        public void Simulate(uint tick) { }
        public CellDelta[] ReadDeltas(uint tick) => [];
        public void SwapBuffers(uint tick) { }
    }
}
