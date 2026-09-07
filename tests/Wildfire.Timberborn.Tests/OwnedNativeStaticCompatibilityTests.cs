using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Persistence;
using F=Wildfire.Timberborn.Tests.OwnedConsequenceBatchTests.Fixture;
using Simulator=Wildfire.Timberborn.Tests.OwnedNativeRestoreFixture.Simulator;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedNativeStaticCompatibilityTests
{
    [Fact]
    public void DeclaredConstructionCostMismatchRejectsWithoutTouchingNativeStock()
    {
        var f=new F(witnessed:true);var saved=OwnedWorldSessionTests.Snapshot(f);var facts=f.NativeBodies().ToArray();
        var warehouse=facts[2];facts[2]=Copy(warehouse,cost:[new("Log",21)]);
        int created=0;
        Assert.Throws<ArgumentException>(()=>TimberbornOwnedWorldSession<Simulator>.PrepareRestore(saved,[],s=>{created++;return new(s);},
            (_,_)=>facts,f.Effects,f.Guard));
        Assert.Equal(0,created);Assert.Empty(f.Native.InventoryCalls);Assert.False(f.Guard.IsIndeterminate);
    }

    [Fact]
    public void CurrentInventoryAndPhysicalPlacementDoNotChangeStaticDefinitionOrSavedCapacity()
    {
        var f=new F(witnessed:true);var saved=OwnedWorldSessionTests.Snapshot(f);var facts=f.NativeBodies().ToArray();
        var warehouse=facts[2];
        facts[2]=Copy(warehouse,inventory:[new(TimberbornCapturedInventoryRole.Stockpile,false,[new("Log",3)])],
            footprint:warehouse.Footprint.Select(slot=>new TimberbornMaterialFootprintSlot(slot.LocalCoordinates,slot.CellIndex==2?6:2)).ToArray());
        f.Native.Amounts[warehouse.EntityId]=3;
        using var restored=TimberbornOwnedWorldSession<Simulator>.PrepareRestore(saved,[],s=>new(s),(_,_)=>facts,f.Effects,f.Guard);
        var key=new TimberbornBurnDamageTargetKey(TimberbornBurnDamageIdentity.ForEntity(warehouse.EntityId,NativeBurnTargetFamily.Stockpile));
        Assert.Equal(f.Damage.States[key].DamageCapacity,restored.Damage.States[key].DamageCapacity);
        Assert.Equal(3,f.Native.Amounts[warehouse.EntityId]);Assert.Empty(f.Native.InventoryCalls);
    }

    [Fact]
    public void RetainedLeftoverRestoresSavedAccountingWithoutCompoundVisualReplay()
    {
        var f=new F(witnessed:true);var tree=f.Registrations[0].EntityId;
        // Finish the synthetic body's burn while fake native leftover retains the exact same Guid.
        for(uint tick=1;tick<=8;tick++)f.Consumer.Consume(tick,[f.Delta(tree,15)]);
        var saved=OwnedWorldSessionTests.Snapshot(f);var facts=f.NativeBodies().ToArray();var body=facts[0];
        facts[0]=Copy(body,yields:body.Yields.Select(y=>new TimberbornNamedYieldMaterial(y.Role,y.ComponentName,y.DeclaredGoodId,
            0,y.DeclaredGoodId,y.DeclaredAmount,y.RemoveOnCut,false)).ToArray());
        f.Native.TreeCalls.Clear();
        using var restored=TimberbornOwnedWorldSession<Simulator>.PrepareRestore(saved,[],s=>new(s),(_,ids)=>
        {
            Assert.Contains(tree,ids);return facts;
        },f.Effects,f.Guard);
        Assert.Equal(OwnedCharredPresentation.BurnedLeftover,restored.Consumer.CaptureHistory().Natural.Single(p=>p.EntityId==tree).DesiredPresentation);
        Assert.Empty(f.Native.TreeCalls);Assert.Empty(f.Native.InventoryCalls);
        Assert.Equal(120,restored.Damage.States[new(TimberbornBurnDamageIdentity.ForEntity(tree,NativeBurnTargetFamily.Tree))].DamageTaken);
    }

    [Fact]
    public void SavedCapacityTamperingDoesNotGetAcceptedOrClampedAgainstCurrentQuantity()
    {
        var f=new OwnedNativeRestoreFixture();f.Consumer.Consume(1,[f.Delta(2)]);var saved=f.Snapshot();var m=saved.OwnedMaterial!;var h=m.History!;
        var owner=h.Owners.Single();var p=owner.Profile!;
        var changed=new OwnedBodyAccountingProfile(p.SpecId,p.TargetKind,p.MaterialKind,14,p.FuelValue,p.Flammability,
            p.MissingResources,p.AccountedResources,p.BurnableProfile,p.ResourceYields,p.ConstructionResources);
        saved=saved with {OwnedMaterial=new(m.CaptureSimulation(),m.Bindings,new([new(owner.EntityId,owner.Family,owner.Retention,changed)],
            h.Natural,h.StorageCredits,h.NativeDefinitions))};
        int created=0;
        Assert.Throws<ArgumentException>(()=>TimberbornOwnedWorldSession<Simulator>.PrepareRestore(saved,[],s=>{created++;return new(s);},
            (_,_)=>[OwnedNativeRestoreFixture.Facts()],f.Effects,f.Guard));
        Assert.Equal(0,created);Assert.Equal(15,f.Damage.States[OwnedNativeRestoreFixture.Key].DamageCapacity);
    }

    private static TimberbornInitialMaterialBody Copy(TimberbornInitialMaterialBody body,
        IReadOnlyList<TimberbornBurnDamageResourceStack>? cost=null,IReadOnlyList<TimberbornNamedYieldMaterial>? yields=null,
        IReadOnlyList<TimberbornInventoryMaterial>? inventory=null,IReadOnlyList<TimberbornMaterialFootprintSlot>? footprint=null)=>
        new(body.EntityId,body.SpecId,body.Shape,footprint??body.Footprint,yields??body.Yields,inventory??body.Inventories,cost??body.ConstructionResources);
}
