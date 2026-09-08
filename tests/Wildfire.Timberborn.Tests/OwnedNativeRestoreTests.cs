using System.Reflection;
using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Persistence;
using F=Wildfire.Timberborn.Tests.OwnedNativeRestoreFixture;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedNativeRestoreTests
{
    [Theory]
    [InlineData(5,3,true,15)]
    [InlineData(3,3,true,9)]
    [InlineData(5,0,true,15)]
    [InlineData(5,0,false,15)]
    public void SavedAccountingNeverReformsFromCurrentQuantity(int initial,int current,bool enabled,int capacity)
    {
        var f=new F(initial);f.Consumer.Consume(1,[f.Delta(2)]);
        var snapshot=TimberbornWildfirePersistenceCodec.Decode(TimberbornWildfirePersistenceCodec.Encode(f.Snapshot()));
        using var restored=f.Restore(snapshot,[F.Facts(current,enabled:enabled)],out _);
        Assert.Equal(capacity,restored.Damage.States[F.Key].DamageCapacity);
        Assert.Equal(2,restored.Damage.States[F.Key].DamageTaken);
        Assert.Equal(initial,restored.Consumer.CaptureHistory().Owners.Single().Profile!.ResourceYields.Single().Amount);
        Assert.Equal(5,restored.Consumer.CaptureHistory().NativeDefinitions!.Definitions.Single().Yields.Single().Amount);
    }

    [Fact]
    public void ActualNativeSavedThreeRemainsUntouchedDuringWitnessedRestore()
    {
        using var native=new NativePartialYieldFixture();
        native.Apply(2); // Existing native fixture establishes a preexisting saved partial quantity.
        var loaded=native.SaveLoad();
        int events=0;
        native.YielderType.GetEvent("YieldAdded")!.AddEventHandler(loaded,(EventHandler)((_,_)=>events++));
        native.YielderType.GetEvent("YieldDecreased")!.AddEventHandler(loaded,(EventHandler)((_,_)=>events++));
        var f=new F();f.Consumer.Consume(1,[f.Delta(2)]);var snapshot=f.Snapshot();
        f.Native.CropCalls.Clear();
        var helper=native.ModType("Wildfire.Timberborn.Runtime.TimberbornInitialWorldProjectionProvider")
            .GetMethod("CaptureNamedYield",BindingFlags.Static|BindingFlags.NonPublic)!;
        var role=Enum.Parse(native.ModType("Wildfire.Timberborn.Mapping.TimberbornCapturedYieldRole"),"Gatherable");
        foreach(bool enabled in new[]{true,false})
        {
            native.EnabledField.SetValue(loaded,enabled);
            using var restored=TimberbornOwnedWorldSession<F.Simulator>.PrepareDiagnosticRestore(snapshot,[],s=>new(s),(_,ids)=>
            {
                Assert.Equal(new[]{F.Id},ids);
                var fact=helper.Invoke(null,[loaded,role,false])!;
                int actual=(int)fact.GetType().GetProperty("ActualAmount")!.GetValue(fact)!;
                int declared=(int)fact.GetType().GetProperty("DeclaredAmount")!.GetValue(fact)!;
                return new[]{F.Facts(actual,declared,enabled)};
            },f.Effects,f.Guard);
            Assert.Equal(15,restored.Damage.States[F.Key].DamageCapacity);Assert.Equal(2,restored.Damage.States[F.Key].DamageTaken);
            Assert.Equal(3,native.RawQuantity(loaded));Assert.Equal(5,native.InitialQuantity(loaded));Assert.Equal(0,events);
        }
        Assert.Empty(f.Native.CropCalls);Assert.Empty(f.Native.InventoryCalls);
    }

    [Theory]
    [InlineData("amount")][InlineData("role")][InlineData("name")][InlineData("resource")][InlineData("spec")][InlineData("footprint")][InlineData("missing")]
    public void NativeStaticMismatchRejectsBeforeBackendOrEffects(string change)
    {
        var f=new F();f.Consumer.Consume(1,[f.Delta(2)]);var snapshot=f.Snapshot();
        var facts=change switch
        {
            "amount"=>F.Facts(declared:7),
            "role"=>F.Facts(role:TimberbornCapturedYieldRole.Cuttable,remove:true),
            "name"=>F.Facts(name:"OtherYield"),
            "resource"=>F.Facts(good:"Log"),
            "spec"=>F.Facts(spec:"OtherCrop"),
            "footprint"=>F.Facts(local:new(1,0,0)),
            _=>F.Facts(),
        };
        int backend=0;f.Native.CropCalls.Clear();
        Assert.Throws<ArgumentException>(()=>TimberbornOwnedWorldSession<F.Simulator>.PrepareDiagnosticRestore(snapshot,[],s=>{backend++;return new(s);},
            (_,_)=>change=="missing" ? [] : [facts],f.Effects,f.Guard));
        Assert.Equal(0,backend);Assert.Equal(2,f.Damage.States[F.Key].DamageTaken);
        Assert.Empty(f.Native.CropCalls);Assert.False(f.Guard.IsIndeterminate);
    }

    [Fact]
    public void CallbackMutationCannotInterleaveNativeFactsAndBackendStaging()
    {
        var f=new F();var snapshot=f.Snapshot();int writes=0;
        Assert.Throws<InvalidOperationException>(()=>TimberbornOwnedWorldSession<F.Simulator>.PrepareDiagnosticRestore(snapshot,[],s=>new(s),(_,_)=>
        {
            f.Guard.TransferInventory(()=>writes++);return new[]{F.Facts()};
        },f.Effects,f.Guard));
        Assert.Equal(0,writes);Assert.False(f.Guard.IsIndeterminate);
        using var restored=f.Restore(snapshot,[F.Facts()],out _); // Read failure released guard.
        Assert.Equal(15,restored.Damage.States[F.Key].DamageCapacity);
    }

    [Fact]
    public void LateDisappearanceDisposesBackendAndReadFailureDoesNotPoison()
    {
        var f=new F();var snapshot=f.Snapshot();var simulator=new F.Simulator(snapshot.OwnedMaterial!.CaptureSimulation());
        Assert.Throws<ArgumentException>(()=>TimberbornOwnedWorldSession<F.Simulator>.PrepareDiagnosticRestore(snapshot,[],_=>
        {
            f.Native.Live.Remove(F.Id);return simulator;
        },(_,_)=>[F.Facts()],f.Effects,f.Guard));
        Assert.Equal(1,simulator.Disposals);Assert.False(f.Guard.IsIndeterminate);
    }

    [Fact]
    public void VanishedRetainedBodyCannotEmitANewWitnessedSave()
    {
        var f=new F();f.Native.Live.Remove(F.Id);
        Assert.Throws<InvalidOperationException>(()=>f.Consumer.CaptureHistory());
        Assert.False(f.Guard.IsIndeterminate);
        f.Native.Live.Add(F.Id);Assert.Equal(OwnedNativeCompatibilityCapability.Unavailable,f.Consumer.CaptureHistory().NativeCompatibility);
    }
}
