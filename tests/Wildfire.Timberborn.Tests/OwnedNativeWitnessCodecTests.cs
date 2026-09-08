using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Persistence;
using F=Wildfire.Timberborn.Tests.OwnedNativeRestoreFixture;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedNativeWitnessCodecTests
{
    [Fact]
    public void OwnedThreeRoundtripsWitnessesWithoutCurrentQuantityOrSecondBodyLedger()
    {
        var f=new F();f.Consumer.Consume(1,[f.Delta(2)]);var source=f.Snapshot();
        var encoded=TimberbornWildfirePersistenceCodec.Encode(source);var restored=TimberbornWildfirePersistenceCodec.Decode(encoded);
        Assert.StartsWith("WF\t2\n",encoded);
        Assert.Equal(3,BitConverter.ToInt32(Convert.FromBase64String(encoded.Split('\n')[1].Split('\t')[1])));
        var history=restored.OwnedMaterial!.History!;
        Assert.Equal(OwnedNativeCompatibilityCapability.Unavailable,history.NativeCompatibility);
        Assert.Equivalent(source.OwnedMaterial!.History!.NativeDefinitions,history.NativeDefinitions,strict:true);
        Assert.Single(restored.Consequences.BurnDamageStates);Assert.Equal(2,restored.Consequences.BurnDamageStates.Single().DamageTaken);
        Assert.Equal(encoded,TimberbornWildfirePersistenceCodec.Encode(restored));
    }

    [Theory]
    [InlineData(1)][InlineData(2)]
    public void OlderEvidenceStaysUnavailableAndPreservesOriginalBytes(int version)
    {
        var source=version==1 ? OwnedMaterialPersistenceTests.Fixture() : OwnedConsequenceHistoryCodecTests.Fixture();
        var encoded=TimberbornWildfirePersistenceCodec.Encode(source);var decoded=TimberbornWildfirePersistenceCodec.Decode(encoded);
        Assert.Equal(encoded,TimberbornWildfirePersistenceCodec.Encode(decoded));
        Assert.Null(decoded.OwnedMaterial!.History?.NativeDefinitions);
        var f=new F();int calls=0;
        Assert.Throws<NotSupportedException>(()=>TimberbornOwnedWorldSession<F.Simulator>.PrepareRestore(decoded,[],s=>{calls++;return new(s);},
            (_,_)=>{calls++;return new[]{F.Facts()};},f.Effects,f.Guard));
        Assert.Equal(0,calls);Assert.False(f.Guard.IsIndeterminate);
        var runtime=new TimberbornRuntimePersistence();runtime.Load(()=>encoded);
        Assert.Equal(encoded,runtime.EncodeForSave(TimberbornRuntimeInitializationState.Ready,()=>throw new Exception("no replacement")));
    }

    [Fact]
    public void TypedWitnessCollectionRejectsMissingExtraDuplicateAndWrongIdentity()
    {
        var f=new F();var history=f.Consumer.CaptureHistory();var witness=history.NativeDefinitions!.Definitions.Single();
        Assert.Throws<ArgumentException>(()=>new OwnedNativeDefinitionSet([witness,witness]));
        Assert.Throws<ArgumentException>(()=>new TimberbornOwnedConsequenceSnapshot(history.Owners,history.Natural,[],new([])));
        var extra=new OwnedNativeDefinitionWitness(Guid.NewGuid(),witness.SpecId,witness.Shape,witness.BodyProfile,
            witness.LocalFootprint,witness.Yields,null);
        Assert.Throws<ArgumentException>(()=>new TimberbornOwnedConsequenceSnapshot(history.Owners,history.Natural,[],new([extra])));
        Assert.Throws<ArgumentException>(()=>new TimberbornOwnedConsequenceSnapshot(history.Owners,history.Natural,[],new([witness,extra])));
    }

    [Fact]
    public void StaticBuildingCostRejectsDefaultInvalidStackAtConstruction()
    {
        Assert.Throws<ArgumentException>(()=>new OwnedNativeDefinitionWitness(F.Id,"Warehouse",TimberbornInitialBodyShape.Stockpile,
            TimberbornBurnableCatalog.Default.Lookup("Warehouse"),[new(0,0,0)],[],[default(TimberbornBurnDamageResourceStack)]));
    }

    [Theory]
    [InlineData("count")][InlineData("truncated")][InlineData("trailing")][InlineData("version")]
    public void MalformedWitnessPayloadRejectsAndOriginalEncodingSurvives(string mode)
    {
        var encoded=TimberbornWildfirePersistenceCodec.Encode(new F().Snapshot());var lines=encoded.Split('\n');
        var bytes=Convert.FromBase64String(lines[1].Split('\t')[1]);
        if(mode=="truncated")bytes=bytes[..^1];
        else if(mode=="trailing")bytes=[..bytes,0];
        else if(mode=="version")BitConverter.GetBytes(4).CopyTo(bytes,0);
        else
        {
            // OWNED2 prefix is byte-identical apart from envelope version; next int is witness count.
            var source=new F().Snapshot();var m=source.OwnedMaterial!;var h=m.History!;
            var legacy=source with {OwnedMaterial=new(m.CaptureSimulation(),m.Bindings,new(h.Owners,h.Natural,h.StorageCredits))};
            int prefix=Convert.FromBase64String(TimberbornWildfirePersistenceCodec.Encode(legacy).Split('\n')[1].Split('\t')[1]).Length;
            BitConverter.GetBytes(int.MaxValue).CopyTo(bytes,prefix);
        }
        lines[1]="OWNED\t"+Convert.ToBase64String(bytes);var malformed=string.Join("\n",lines);
        Assert.Throws<FormatException>(()=>TimberbornWildfirePersistenceCodec.Decode(malformed));
        var runtime=new TimberbornRuntimePersistence();runtime.Load(()=>malformed);
        Assert.Equal(malformed,runtime.EncodeForSave(TimberbornRuntimeInitializationState.Ready,()=>throw new Exception("no replacement")));
    }
}
