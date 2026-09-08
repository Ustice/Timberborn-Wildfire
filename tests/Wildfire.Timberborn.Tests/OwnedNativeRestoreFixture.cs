using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Persistence;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

// Synthetic accounting choices isolate restoration authority; they do not define native crop fuel policy.
internal sealed class OwnedNativeRestoreFixture
{
    internal static readonly Guid Id=new("00000000-0000-0000-0000-000000000002");
    internal readonly FireGrid Grid=new(1,1,1);
    internal readonly NativeResourceTransaction Guard=new();
    internal readonly OwnedConsequenceBatchTests.NativeFake Native=new();
    internal readonly TimberbornOwnedNativeEffects Effects;
    internal readonly TimberbornNativeMaterialRegistry Registry;
    internal readonly TimberbornBurnDamageService Damage;
    internal readonly TimberbornOwnedDeltaConsumer Consumer;
    internal static TimberbornBurnDamageTargetKey Key=>new(TimberbornBurnDamageIdentity.ForEntity(Id,NativeBurnTargetFamily.Crop));
    internal OwnedNativeRestoreFixture(int accountingAmount=5)
    {
        Registry=new(Grid,[]);
        Registry.Reconcile([new(Id,[new(new(0,0,0),0)],[TimberbornMaterialPart.Crop("Carrot")])],[]);
        Damage=new(new TimberbornBurnDamageDescriptorCatalog([new("Carrot",TimberbornBurnDamageTargetKind.Crop,
            TimberbornBurnMaterialKind.Organic,resourceYields:[new("Carrot",accountingAmount)])]));
        Damage.RegisterTargets(Grid,[new(Key,"Carrot",[new(0,0,0)],0)]);
        Effects=new(Native,Native,Native,Native,Native);
        Consumer=TimberbornOwnedDeltaConsumer.CreateWithNativeDefinitions(Registry,Damage,Effects,Guard,[Facts(accountingAmount)]);
    }
    internal static TimberbornInitialMaterialBody Facts(int actual=3,int declared=5,bool enabled=true,string name="Gatherable",
        string good="Carrot",string spec="Carrot",bool remove=false,TimberbornCapturedYieldRole role=TimberbornCapturedYieldRole.Gatherable,
        TimberbornCellCoordinates? local=null)=>new(Id,spec,TimberbornInitialBodyShape.Crop,[new(local??new(0,0,0),0)],
            [new(role,name,good,actual,good,declared,remove,enabled)],[],null);
    internal CellDelta Delta(int amount)=>new(0,PackedCell.Pack(15,3,3,0,0,1),PackedCell.Pack(15-amount,3,3,0,0,1),1,1);
    internal TimberbornWildfirePersistenceSnapshot Snapshot()
    {
        var sim=new FireSimSnapshot(1,Grid,37,FireSimParameters.Default,0,[0],[0],[0],[1],[1],new(0,[new(1,1)],[]),[]);
        return TimberbornWildfirePersistenceSnapshot.Empty with {PersistenceVersion=2,OwnedMaterial=new(sim,Registry.CaptureBindings(),Consumer.CaptureHistory()),
            Consequences=TimberbornWildfirePersistenceCodec.CaptureConsequences(Damage)};
    }
    internal TimberbornOwnedWorldSession<Simulator> Restore(TimberbornWildfirePersistenceSnapshot snapshot,
        IReadOnlyList<TimberbornInitialMaterialBody> facts,out Simulator simulator)
    {
        var created=new Simulator(snapshot.OwnedMaterial!.CaptureSimulation());simulator=created;
        return TimberbornOwnedWorldSession<Simulator>.PrepareDiagnosticRestore(snapshot,[],_=>created,(_,_)=>facts,Effects,Guard);
    }
    internal sealed class Simulator(FireSimSnapshot saved):IGpuFireSimulator,IFireSimSnapshotSimulator,IDisposable
    {
        internal int Disposals;
        public int Width=>saved.Grid.Width;public int Height=>saved.Grid.Height;public int Depth=>saved.Grid.Depth;
        public FireSimSnapshotCapability SnapshotCapability=>FireSimSnapshotCapability.CompleteMaterialHistory;
        public FireSimSnapshot CaptureSnapshot()=>saved;
        public void Dispose()=>Disposals++;
        public void RegisterChange(FireSimChange change)=>throw new NotSupportedException();
        public GpuFireStepResult Tick()=>throw new NotSupportedException();
        public IDisposable Subscribe(IFireSimListener listener)=>throw new NotSupportedException();
    }
}
