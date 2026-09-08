using System.Text;
using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Persistence;
using F = Wildfire.Timberborn.Tests.OwnedNativeRestoreFixture;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedInventoryWitnessCodecTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnedFourRoundtripsExplicitEmptyOrNamedInventoryEvidence(bool named)
    {
        var source = WithDeclarations(new F().Snapshot(), named ? [new(TimberbornNativeInventoryRole.GoodStack, "Inventory.A")] : []);
        var encoded = TimberbornWildfirePersistenceCodec.Encode(source);
        var decoded = TimberbornWildfirePersistenceCodec.Decode(encoded);
        Assert.Equal(4, Envelope(encoded));
        Assert.Equal(OwnedNativeCompatibilityCapability.Complete, decoded.OwnedMaterial!.History!.NativeCompatibility);
        Assert.Equal(named ? 1 : 0, decoded.OwnedMaterial.History.NativeDefinitions!.Definitions.Single().InventoryDeclarations!.Count);
        Assert.Equal(encoded, TimberbornWildfirePersistenceCodec.Encode(decoded));
        Assert.Equivalent(source.Consequences, decoded.Consequences, strict: true);
    }

    [Fact]
    public void WardenDeclarationExactNativeNameSurvivesOwnedFourCodec()
    {
        using var native = new NativeInventoryRoleFixture(input: true);
        native.InitializeNamedInventory("Station.ActualName");
        var type = native.Stock.Resources.GetType().Assembly.GetType("Wildfire.Timberborn.FireResponse.WardenStation")!;
        var station = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
        native.Call(station, "InitializeInventory", native.Inventory);
        var inventory = native.Property(station, "Inventory")!;
        string name = (string)native.Property(inventory, "ComponentName")!;
        // Codec-only body fixture: this does not compile or admit station material/accounting.
        var encoded = TimberbornWildfirePersistenceCodec.Encode(WithDeclarations(new F().Snapshot(),
            [new(TimberbornNativeInventoryRole.WardenStation, name)]));
        var decoded = TimberbornWildfirePersistenceCodec.Decode(encoded);
        var declaration = decoded.OwnedMaterial!.History!.NativeDefinitions!.Definitions.Single().InventoryDeclarations!.Single();
        Assert.Equal(TimberbornNativeInventoryRole.WardenStation, declaration.Role);
        Assert.Equal(name, declaration.ComponentName); Assert.Equal(4, Envelope(encoded));
        Assert.Equal(encoded, TimberbornWildfirePersistenceCodec.Encode(decoded));
        Assert.Equal(0, native.Physical);
    }

    [Fact]
    public void LegacyEmptyOwnerSetDoesNotAcquireCompletenessByVacuousInference()
    {
        var legacy = new TimberbornOwnedConsequenceSnapshot([], [], [], new([]));
        var complete = new TimberbornOwnedConsequenceSnapshot([], [], [], OwnedNativeDefinitionSet.WithInventoryDeclarations([]));
        Assert.Equal(OwnedNativeCompatibilityCapability.Unavailable, legacy.NativeCompatibility);
        Assert.Equal(OwnedNativeCompatibilityCapability.Complete, complete.NativeCompatibility);
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public void ZeroOwnerEnvelopeRetainsExplicitEvidenceVersion(bool complete, int version)
    {
        var definitions = complete ? OwnedNativeDefinitionSet.WithInventoryDeclarations([]) : new OwnedNativeDefinitionSet([]);
        var history = new TimberbornOwnedConsequenceSnapshot([], [], [], definitions);
        var simulation = new FireSimSnapshot(1, new(1, 1, 1), 0, FireSimParameters.Default, 7,
            [0], [0], [0], [0], [0], new(0, [], []), []);
        var source = TimberbornWildfirePersistenceSnapshot.Empty with
        {
            PersistenceVersion = 2,
            OwnedMaterial = new(simulation, new(1, 1, []), history),
        };
        var encoded = TimberbornWildfirePersistenceCodec.Encode(source);
        var decoded = TimberbornWildfirePersistenceCodec.Decode(encoded);
        Assert.Equal(version, Envelope(encoded));
        Assert.Equal(complete, decoded.OwnedMaterial!.History!.NativeDefinitions!.HasInventoryDeclarations);
        Assert.Empty(decoded.OwnedMaterial.History.NativeDefinitions.Definitions);
        Assert.Equal(encoded, TimberbornWildfirePersistenceCodec.Encode(decoded));
    }

    [Fact]
    public void MissingLegacyDeclarationsNeverEqualExplicitEmptyAndCannotBeMixedOrDropped()
    {
        var legacy = OwnedNativeDefinitionWitness.Capture(F.Facts());
        var known = OwnedNativeDefinitionWitness.Capture(F.Facts(), []);
        Assert.Null(legacy.InventoryDeclarations); Assert.Empty(known.InventoryDeclarations!);
        Assert.False(legacy.Matches(known)); Assert.False(known.Matches(legacy));
        Assert.Throws<ArgumentException>(() => OwnedNativeDefinitionSet.WithInventoryDeclarations([legacy]));
        Assert.Throws<ArgumentException>(() => new OwnedNativeDefinitionSet([known]));
        Assert.Throws<ArgumentNullException>(() => OwnedNativeDefinitionWitness.Capture(F.Facts(), null!));
    }

    [Fact]
    public void ExactRoleAndNameAffectStaticMatchButActualQuantityAndAvailabilityDoNot()
    {
        var declarations = new[] { new TimberbornInventoryDeclaration(TimberbornNativeInventoryRole.GoodStack, "Inventory.A") };
        var original = OwnedNativeDefinitionWitness.Capture(F.Facts(actual: 3), declarations);
        declarations[0] = new(TimberbornNativeInventoryRole.GoodStack, "changed");
        Assert.True(original.Matches(OwnedNativeDefinitionWitness.Capture(F.Facts(actual: 0, enabled: false),
            [new(TimberbornNativeInventoryRole.GoodStack, "Inventory.A")])));
        Assert.False(original.Matches(OwnedNativeDefinitionWitness.Capture(F.Facts(), declarations)));
        Assert.False(original.Matches(OwnedNativeDefinitionWitness.Capture(F.Facts(),
            [new(TimberbornNativeInventoryRole.RecoveredGoodStack, "Inventory.A")])));
        Assert.False(original.Matches(OwnedNativeDefinitionWitness.Capture(F.Facts(), [])));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AmbiguousDeclaredRoleOrNameRejectsBeforeEncoding(bool duplicateRole)
    {
        Assert.Throws<ArgumentException>(() => OwnedNativeDefinitionWitness.Capture(F.Facts(),
            [new(TimberbornNativeInventoryRole.GoodStack, "Inventory.A"),
             new(duplicateRole ? TimberbornNativeInventoryRole.GoodStack : TimberbornNativeInventoryRole.Manufactory,
                 duplicateRole ? "Inventory.B" : "Inventory.A")]));
    }

    [Fact]
    public void SettledRetirementPreservesOriginalStaticDeclarationsWithoutPreservingBodyLedger()
    {
        var f = new F(); var source = WithDeclarations(f.Snapshot(), [new(TimberbornNativeInventoryRole.GoodStack, "Inventory.A")]);
        var consumer = f.Guard.CaptureAtRest(() => TimberbornOwnedDeltaConsumer.CreateFromHistory(
            f.Registry, f.Damage, f.Effects, f.Guard, source.OwnedMaterial!.History!));
        f.Native.Live.Remove(F.Id);
        Assert.True(consumer.RetireNativeOwner(F.Id)); Assert.False(consumer.RetireNativeOwner(F.Id));
        var history = consumer.CaptureHistory();
        Assert.Equal(OwnedBodyRetention.RetiredNativeOwner, history.Owners.Single().Retention);
        Assert.Null(history.Owners.Single().Profile);
        Assert.Equal("Inventory.A", history.NativeDefinitions!.Definitions.Single().InventoryDeclarations!.Single().ComponentName);
        Assert.Throws<ArgumentException>(() => new TimberbornOwnedConsequenceSnapshot(history.Owners, history.Natural, [],
            OwnedNativeDefinitionSet.WithInventoryDeclarations([])));
        var retired = source with
        {
            OwnedMaterial = new(source.OwnedMaterial!.CaptureSimulation(), source.OwnedMaterial.Bindings, history),
            Consequences = TimberbornWildfirePersistenceCodec.CaptureConsequences(f.Damage),
        };
        Assert.Empty(retired.Consequences.BurnDamageStates);
        var encoded = TimberbornWildfirePersistenceCodec.Encode(retired);
        var decoded = TimberbornWildfirePersistenceCodec.Decode(encoded);
        Assert.Equal(encoded, TimberbornWildfirePersistenceCodec.Encode(decoded));
        Assert.Single(decoded.OwnedMaterial!.History!.NativeDefinitions!.Definitions);
        Assert.Empty(decoded.Consequences.BurnDamageStates);
    }

    [Theory]
    [InlineData("unknown-role")]
    [InlineData("duplicate-role")]
    [InlineData("duplicate-name")]
    [InlineData("truncated")]
    [InlineData("downgrade")]
    public void CorruptInventoryEvidenceRejectsWithoutDiagnosticUpgrade(string mode)
    {
        var source = WithDeclarations(new F().Snapshot(),
            [new(TimberbornNativeInventoryRole.GoodStack, "Inventory.A"), new(TimberbornNativeInventoryRole.Manufactory, "Inventory.B")]);
        var encoded = TimberbornWildfirePersistenceCodec.Encode(source); var lines = encoded.Split('\n');
        var bytes = Convert.FromBase64String(lines[1].Split('\t')[1]);
        int secondName = bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes("Inventory.B"));
        Assert.True(secondName > 8);
        if (mode == "unknown-role") BitConverter.GetBytes(999).CopyTo(bytes, secondName - 8);
        else if (mode == "duplicate-role") BitConverter.GetBytes((int)TimberbornNativeInventoryRole.GoodStack).CopyTo(bytes, secondName - 8);
        else if (mode == "duplicate-name") bytes[secondName + "Inventory.".Length] = (byte)'A';
        else if (mode == "truncated") bytes = bytes[..^1];
        else BitConverter.GetBytes(3).CopyTo(bytes, 0);
        lines[1] = "OWNED\t" + Convert.ToBase64String(bytes);
        Assert.Throws<FormatException>(() => TimberbornWildfirePersistenceCodec.Decode(string.Join("\n", lines)));
        Assert.Equal(encoded, TimberbornWildfirePersistenceCodec.Encode(TimberbornWildfirePersistenceCodec.Decode(encoded)));
    }

    internal static TimberbornWildfirePersistenceSnapshot WithDeclarations(TimberbornWildfirePersistenceSnapshot source,
        IReadOnlyList<TimberbornInventoryDeclaration> declarations)
    {
        var material = source.OwnedMaterial!; var history = material.History!;
        var definitions = OwnedNativeDefinitionSet.WithInventoryDeclarations([OwnedNativeDefinitionWitness.Capture(F.Facts(), declarations)]);
        return source with { OwnedMaterial = new(material.CaptureSimulation(), material.Bindings,
            new(history.Owners, history.Natural, history.StorageCredits, definitions)) };
    }
    private static int Envelope(string encoded) => BitConverter.ToInt32(Convert.FromBase64String(encoded.Split('\n')[1].Split('\t')[1]));
}
