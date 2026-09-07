using Wildfire.Timberborn.Persistence;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedConsequenceHistoryCodecTests
{
    [Fact]
    public void NonemptyHistoryRoundtripsInsideOwnedTwoWithoutDuplicatingBodyDamage()
    {
        var source = Fixture();
        var encoded = TimberbornWildfirePersistenceCodec.Encode(source);
        var restored = TimberbornWildfirePersistenceCodec.Decode(encoded);
        var history = restored.OwnedMaterial!.History!;
        Assert.Equal(OwnedConsequenceHistoryCapability.Complete, restored.OwnedMaterial.HistoryCapability);
        Assert.Equal(source.Consequences.BurnDamageStates, restored.Consequences.BurnDamageStates);
        Assert.Equal(2, BitConverter.ToInt32(Convert.FromBase64String(encoded.Split('\n')[1].Split('\t')[1])));
        Assert.Equal(1, history.StorageCredits.Single().FractionalBudget);
        Assert.Equal("Log", history.StorageCredits.Single().ResourceId);
        Assert.Equal(100, history.Owners.Single().Profile!.Capacity);
        Assert.Equal(encoded, TimberbornWildfirePersistenceCodec.Encode(restored));
    }
    [Fact]
    public void LegacyOwnedOneNeverPretendsEmptyCompleteHistory()
    {
        var original = OwnedMaterialPersistenceTests.Fixture();
        var restored = TimberbornWildfirePersistenceCodec.Decode(TimberbornWildfirePersistenceCodec.Encode(original));
        Assert.Null(restored.OwnedMaterial!.History);
        Assert.Equal(OwnedConsequenceHistoryCapability.Unavailable, restored.OwnedMaterial.HistoryCapability);
    }
    [Theory]
    [InlineData("missing")][InlineData("duplicate")][InlineData("extra")][InlineData("negative")][InlineData("over")][InlineData("future")]
    public void CompleteAssociationRejectsInvalidBodyRows(string mode)
    {
        var source = Fixture(); var row = source.Consequences.BurnDamageStates.Single();
        var rows = mode switch
        {
            "missing" => Array.Empty<TimberbornBurnDamagePersistenceEntry>(),
            "duplicate" => new[] {row,row},
            "extra" => new[] {row,row with { TargetKey = "structure:entity:" + Guid.NewGuid().ToString("D") }},
            "negative" => new[] { row with { DamageTaken = -1 } },
            "over" => new[] { row with { DamageTaken = 101 } },
            _ => new[] { row with { LastDamagedTick = 38 } },
        };
        Assert.Throws<ArgumentException>(() => TimberbornWildfirePersistenceCodec.Encode(source with { Consequences = new(rows) }));
    }
    [Fact]
    public void RetiredOriginKeepsItsFamilyWithoutInventingBody()
    {
        var source = Fixture(); var material = source.OwnedMaterial!; var owner = material.History!.Owners.Single();
        var retired = new OwnedConsequenceOwner(owner.EntityId, owner.Family, OwnedBodyRetention.RetiredNativeOwner, null);
        var snapshot = source with { OwnedMaterial = new(material.CaptureSimulation(), material.Bindings,
            new([retired], [], material.History.StorageCredits)), Consequences = new([]) };
        var restored = TimberbornWildfirePersistenceCodec.Decode(TimberbornWildfirePersistenceCodec.Encode(snapshot));
        Assert.Equal(owner.EntityId, restored.OwnedMaterial!.History!.Owners.Single().EntityId);
        Assert.Empty(restored.Consequences.BurnDamageStates);
        Assert.Null(restored.OwnedMaterial.History.Owners.Single().Profile);
        Assert.Throws<ArgumentException>(() => new OwnedConsequenceOwner(owner.EntityId, owner.Family, OwnedBodyRetention.RetainedBody, null));
        Assert.Throws<ArgumentException>(() => new OwnedConsequenceOwner(owner.EntityId, owner.Family, OwnedBodyRetention.RetiredNativeOwner, owner.Profile));
    }
    internal static TimberbornWildfirePersistenceSnapshot Fixture()
    {
        var source = OwnedMaterialPersistenceTests.Fixture(); var material = source.OwnedMaterial!;
        Guid id = material.Bindings.Entities.Single().EntityId;
        var profile = new OwnedBodyAccountingProfile("Warehouse", TimberbornBurnDamageTargetKind.Storage,
            TimberbornBurnMaterialKind.Constructed, 100, 2, 3, [], ["Log"], null, [], [new("Log", 50)]);
        var history = new TimberbornOwnedConsequenceSnapshot([new(id, NativeBurnTargetFamily.Stockpile, OwnedBodyRetention.RetainedBody, profile)], [], [new(id,"Log",1,2)]);
        return source with { OwnedMaterial = new(material.CaptureSimulation(), material.Bindings, history),
            Consequences = new([new(history.Owners.Single().TargetKey.StableId, 9, 35)]) };
    }
}
