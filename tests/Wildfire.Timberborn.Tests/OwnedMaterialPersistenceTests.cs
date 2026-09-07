using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Persistence;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedMaterialPersistenceTests
{
    private static readonly Guid Owner = Guid.Parse("eec26401-4b90-4816-b2f9-e16f783d7c29");

    [Fact]
    public void PairedCodecPreservesNonzeroActiveArchivedAndNeverActivatedBindings()
    {
        var source = Fixture();
        string encoded = TimberbornWildfirePersistenceCodec.Encode(source);
        var restored = TimberbornWildfirePersistenceCodec.Decode(encoded);
        Assert.StartsWith("WF\t2\nOWNED\t", encoded);
        Assert.DoesNotContain("\nFIRE\t", encoded);
        Assert.Null(restored.FireSim);
        var simulation = restored.OwnedMaterial!.CaptureSimulation();
        Assert.Equal(new FireSimMaterialIdentity(7, 11), simulation.MaterialAuthority.KnownSlots[0]);
        Assert.Equal(new FireSimMaterialIdentity(7, 12), simulation.MaterialAuthority.Archives.Single().Identity);
        Assert.Equal((uint)91, simulation.MaterialAuthority.Archives.Single().CaptureToken);
        Assert.Equal((uint)0x3456, simulation.MaterialAuthority.Archives.Single().PackedCell);
        Assert.Equal((uint)0x4303, simulation.MaterialAuthority.Archives.Single().Companion);
        Assert.Equal((ushort)0x1234, simulation.Cells[0]);
        Assert.Equal((uint)0x5678, simulation.TransportFields[0]);
        Assert.Equal((uint)0x1204, simulation.CompanionFields[0]);
        Assert.Equal((uint)37, simulation.Tick);
        Assert.Equal((uint)123, simulation.Seed);
        Assert.Equal(FireSimParameters.Default with { IgnitionPoint = 9 }, simulation.Parameters);
        Assert.Equal(source.OwnedMaterial!.CaptureSimulation().PendingChanges, simulation.PendingChanges);
        Assert.Equal(Owner, restored.OwnedMaterial.Bindings.Entities.Single().EntityId);
        Assert.Equal(3, restored.OwnedMaterial.Bindings.Entities.Single().Slots.Count);
        Assert.Equal(encoded, TimberbornWildfirePersistenceCodec.Encode(restored));
    }

    [Fact]
    public void CrossValidationRejectsMissingArchivedSlotAndTokenAliasingBeforeFactory()
    {
        var owned = Fixture().OwnedMaterial!;
        var binding = owned.Bindings.Entities.Single();
        Assert.Throws<ArgumentException>(() => new TimberbornOwnedMaterialSnapshot(owned.CaptureSimulation(),
            owned.Bindings with { Entities = new[] { binding with { Slots = binding.Slots.Where(s => s.SlotId != 12).ToArray() } } }));
        Assert.Throws<ArgumentException>(() => new TimberbornOwnedMaterialSnapshot(owned.CaptureSimulation(),
            owned.Bindings with { Entities = new[] { binding, binding with { EntityId = Guid.NewGuid() } } }));
    }

    [Fact]
    public void CaptureAndStagingDefendAgainstAliasMutationAndNeverPublishFailedFactory()
    {
        var owned = Fixture().OwnedMaterial!;
        var copy = owned.CaptureSimulation();
        copy.Cells[0] = 0; copy.TargetIds[0] = 0; copy.MaterialAuthority.KnownSlots[0] = default;
        Assert.Equal((ushort)0x1234, owned.CaptureSimulation().Cells[0]);
        int calls = 0;
        Assert.Throws<ArgumentOutOfRangeException>(() => owned.PrepareRestore(new[] { 2 }, _ => { calls++; return new FakeSimulator(); }));
        Assert.Equal(0, calls);
        var failure = new InvalidOperationException("Constructor failed before publication.");
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => owned.PrepareRestore(Array.Empty<int>(), _ => throw failure)));
        var simulator = new FakeSimulator();
        var staged = owned.PrepareRestore(new[] { 1 }, snapshot =>
        {
            Assert.Equal((uint)7, snapshot.TargetIds[0]);
            snapshot.Cells[0] = 0; // A factory receives its own copy.
            return simulator;
        });
        Assert.Same(simulator, staged.Simulator);
        Assert.True(staged.Registry.TryResolveOrigin(7, out Guid id));
        Assert.Equal(Owner, id);
        Assert.Equal(WildfireMaterialClass.Terrain, staged.Registry.ResolveCell(1).Profile.MaterialClass);
        Assert.Equal((ushort)0x1234, owned.CaptureSimulation().Cells[0]);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("trailing")]
    [InlineData("truncated")]
    [InlineData("negative-count")]
    [InlineData("oversized-count")]
    [InlineData("wrong-grid-count")]
    [InlineData("overflow-grid")]
    [InlineData("schema")]
    [InlineData("legacy-and-owned")]
    public void MalformedPayloadFailsAndRuntimePreservesOriginal(string corruption)
    {
        string valid = TimberbornWildfirePersistenceCodec.Encode(Fixture());
        string ownedLine = valid.Split('\n').Single(line => line.StartsWith("OWNED\t"));
        byte[] bytes = Convert.FromBase64String(ownedLine[6..]);
        string malformed;
        if (corruption == "duplicate") malformed = valid + "\n" + ownedLine;
        else if (corruption == "legacy-and-owned") malformed = valid + "\nFIRE\t1\t1\t1\t0\tAAA=\tAAAAAA==";
        else
        {
            switch (corruption)
            {
                case "trailing": bytes = bytes.Concat(new byte[] { 0 }).ToArray(); break;
                case "truncated": bytes = bytes[..^1]; break;
                case "negative-count": BitConverter.GetBytes(-1).CopyTo(bytes, 100); break;
                case "oversized-count": BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, 100); break;
                case "wrong-grid-count": BitConverter.GetBytes(1).CopyTo(bytes, 100); break;
                case "overflow-grid": BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, 8); break;
                case "schema": BitConverter.GetBytes(2).CopyTo(bytes, 0); break;
            }
            malformed = valid.Replace(ownedLine, "OWNED\t" + Convert.ToBase64String(bytes));
        }
        Assert.Throws<FormatException>(() => TimberbornWildfirePersistenceCodec.Decode(malformed));
        var runtime = new TimberbornRuntimePersistence();
        runtime.Load(() => malformed);
        Assert.NotNull(runtime.LoadFailure);
        Assert.Equal(malformed, runtime.EncodeForSave(TimberbornRuntimeInitializationState.Ready,
            () => throw new Exception("Malformed load must not invoke capture.")));
    }

    [Fact]
    public void ValidOwnedPayloadIsExplicitlyPreservedBeforeLegacyInitialization()
    {
        string encoded = TimberbornWildfirePersistenceCodec.Encode(Fixture());
        var runtime = new TimberbornRuntimePersistence();
        runtime.Load(() => encoded);
        Assert.IsType<NotSupportedException>(runtime.LoadFailure);
        Assert.NotNull(runtime.LoadedSnapshot!.OwnedMaterial);
        Assert.Equal(encoded, runtime.EncodeForSave(TimberbornRuntimeInitializationState.Ready,
            () => throw new Exception("WF2 must not be replaced by a legacy capture.")));
        Assert.Throws<FormatException>(() => TimberbornWildfirePersistenceCodec.Encode(Fixture() with { PersistenceVersion = 1 }));
        Assert.Throws<FormatException>(() => TimberbornWildfirePersistenceCodec.Decode("WF\t2"));
    }

    internal static TimberbornWildfirePersistenceSnapshot Fixture()
    {
        var active = new FireSimMaterialIdentity(7, 11);
        var archived = new FireSimMaterialIdentity(7, 12);
        var simulation = new FireSimSnapshot(1, new FireGrid(2, 1, 1), 37,
            FireSimParameters.Default with { IgnitionPoint = 9 }, 123,
            new ushort[] { 0x1234, 0 }, new uint[] { 0x5678, 0 }, new uint[] { 0x1204, 0 },
            new uint[] { 7, 0 }, new uint[] { 11, 0 },
            new FireSimMaterialAuthoritySnapshot(93, new[] { active, archived },
                new[] { new FireSimMaterialArchiveSnapshot(archived, 91, 1, 0x3456, 0x4303) }),
            new[] { new FireSimChange(0, SetCell: 0, SetWater: 0, AddWater: 2), new FireSimChange(0, AddWater: 1),
                new FireSimChange(99, SetSmoke: 0, SetFuel: 4),
                new FireSimChange(1, SetCell: 0x4321, AddHeat: 2, AddFuel: 3, AddAsh: 1, RemoveAsh: 2,
                    SetAsh: 1, SetAshContamination: 2, SetWater: 1, SetFuel: 5, SetHeat: 4, SetFlammability: 2,
                    SetBurningLevel: 1, SetTerrain: 1, SetSmoke: 3, SetSmokeContamination: 1, AddWater: 3) });
        var bindings = new TimberbornMaterialBindingSnapshot(1, 8, new[] { new TimberbornMaterialEntityBinding(Owner, 7, 14,
            new[] { new TimberbornMaterialSlotBinding(new(0, 0, 0), 11), new TimberbornMaterialSlotBinding(new(1, 0, 0), 12),
                new TimberbornMaterialSlotBinding(new(2, 0, 0), 13) }) });
        return TimberbornWildfirePersistenceSnapshot.Empty with { PersistenceVersion = 2,
            OwnedMaterial = new TimberbornOwnedMaterialSnapshot(simulation, bindings) };
    }

    private sealed class FakeSimulator : IGpuFireSimulator
    {
        public int Width => 2; public int Height => 1; public int Depth => 1;
        public void RegisterChange(FireSimChange change) => throw new NotSupportedException();
        public GpuFireStepResult Tick() => throw new NotSupportedException();
        public IDisposable Subscribe(IFireSimListener listener) => throw new NotSupportedException();
    }
}
