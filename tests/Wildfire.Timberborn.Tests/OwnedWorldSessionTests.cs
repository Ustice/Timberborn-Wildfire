using Wildfire.Core;
using Wildfire.Timberborn.Persistence;
using Wildfire.Timberborn.Mapping;
using F = Wildfire.Timberborn.Tests.OwnedConsequenceBatchTests.Fixture;

namespace Wildfire.Timberborn.Tests;

public sealed partial class OwnedWorldSessionTests
{
    [Fact]
    public void ActualReceiptAndCreditSurviveNewSessionWithoutReplayingNativeEffects()
    {
        var original = new F(); var tree = original.Registrations[0].EntityId; var stock = original.Registrations[2].EntityId;
        original.Native.TreeYieldReceipt = 1;
        original.Consumer.Consume(1, [original.Delta(tree,15), original.Delta(stock,1)]);
        var saved = Snapshot(original); var native = new F(); native.Native.TreeYieldReceipt = 1;
        using var restored = Restore(saved, native, out _);
        Assert.Empty(native.Native.TreeCalls); // Restore never calls yield, death, or compound leftover.
        Assert.Empty(native.Native.InventoryCalls);
        var reencoded = TimberbornWildfirePersistenceCodec.Encode(restored.Capture(saved.AshField,saved.BeaverBehavior));
        Assert.Equal(TimberbornWildfirePersistenceCodec.Encode(saved), reencoded);
        Assert.Equal(1, restored.Consumer.CaptureHistory().Natural.Single(item => item.EntityId == tree).AppliedYieldLoss);
        var results = restored.Consumer.RehydratePresentation(new PresentationFake());
        Assert.Contains(OwnedPresentationResult.Applied, results);
        Assert.Empty(native.Native.TreeCalls);
        restored.Consumer.Consume(38, [original.Delta(tree,15), original.Delta(stock,1)]);
        Assert.Equal(9,native.Native.Amounts[stock]); // Saved one-band credit plus new band buys one cost-two Log.
        Assert.Single(native.Native.TreeCalls); // Only new incremental yield loss; no death/leftover replay.
        Assert.Equal(1,native.Native.TreeCalls.Single().YieldLost);
    }
    [Fact]
    public void RetiredOriginRestoresWithoutBodyAndCannotBindAnotherNativeOwner()
    {
        var original = new F(); var tree = original.Registrations[0].EntityId;
        var key = new TimberbornBurnDamageTargetKey(TimberbornBurnDamageIdentity.ForEntity(tree,NativeBurnTargetFamily.Tree));
        original.Damage.RemoveTarget(key); original.Native.Live.Remove(tree);
        var saved = Snapshot(original); var native = new F(); native.Damage.RemoveTarget(key); native.Native.Live.Remove(tree);
        using var restored = Restore(saved,native,out _);
        var result = restored.Consumer.Consume(38,[original.Delta(tree,15)]);
        Assert.Equal(1,result.NotLiveOwners); Assert.Empty(native.Native.TreeCalls);
        Assert.DoesNotContain(key,restored.Damage.States.Keys);
        var replacement = Guid.NewGuid(); native.Native.Live.Add(replacement);
        Assert.Equal(1,restored.Consumer.Consume(39,[original.Delta(tree,15)]).NotLiveOwners);
    }
    [Fact]
    public void LateRestoreFailureDisposesNewSimulatorWithoutPoisoningOrPublishing()
    {
        var original = new F(); var stock = original.Registrations[2].EntityId;
        original.Consumer.Consume(1,[original.Delta(stock,1)]);
        var saved = Snapshot(original); var native = new F(); var simulator = new Simulator(saved.OwnedMaterial!.CaptureSimulation());
        Assert.Throws<ArgumentException>(() => TimberbornOwnedWorldSession<Simulator>.PrepareRestore(saved,[], _=>simulator,
            _=>native.Damage,native.Effects,native.Guard,new TimberbornResourceFuelCatalog([new("Log",3,3,false,false,true)])));
        Assert.Equal(1,simulator.Disposals); Assert.False(native.Guard.IsIndeterminate);
        Assert.All(native.Damage.States.Values, state=>Assert.Equal(0,state.DamageTaken));
        Assert.Empty(native.Native.InventoryCalls);
    }
    [Fact]
    public void ProfileFailurePrecedesBackendAndDoesNotClampSavedDamage()
    {
        var original = new F(); var saved = Snapshot(original); var native = new F();
        native.Damage.RemoveTarget(new(TimberbornBurnDamageIdentity.ForEntity(native.Registrations[0].EntityId,NativeBurnTargetFamily.Tree)));
        int created=0;
        Assert.Throws<ArgumentException>(() => TimberbornOwnedWorldSession<Simulator>.PrepareRestore(saved,[], _=>{created++;return new(saved.OwnedMaterial!.CaptureSimulation());},
            _=>native.Damage,native.Effects,native.Guard));
        Assert.Equal(0,created); Assert.False(native.Guard.IsIndeterminate);
    }
    [Fact]
    public void CaptureFailureIsReadOnlyAndCallbackCaptureCannotInterleaveDelivery()
    {
        var f = new F(); var saved = Snapshot(f);
        using var session = Restore(saved,f,out var simulator);
        var error = new ApplicationException("readback failed before save encoding");
        simulator.CaptureFailure=error;
        Assert.Same(error,Assert.Throws<ApplicationException>(()=>session.Capture(saved.AshField,saved.BeaverBehavior)));
        Assert.False(f.Guard.IsIndeterminate);
        simulator.CaptureFailure=null;
        f.Native.AfterTree=()=>Assert.Throws<InvalidOperationException>(()=>session.Capture(saved.AshField,saved.BeaverBehavior));
        session.Consumer.Consume(38,[f.Delta(f.Registrations[0].EntityId,15)]);
        Assert.False(f.Guard.IsIndeterminate);
    }
    [Fact]
    public void SimulatorReadCannotCommitAReentrantConsequenceBetweenSnapshotParts()
    {
        var f=new F(); var saved=Snapshot(f); using var session=Restore(saved,f,out var simulator);
        var stock=f.Registrations[2].EntityId;
        simulator.DuringCapture=()=>session.Consumer.Consume(37,[f.Delta(stock,1)]);
        Assert.Throws<InvalidOperationException>(()=>session.Capture(saved.AshField,saved.BeaverBehavior));
        Assert.All(session.Damage.States.Values,state=>Assert.Equal(0,state.DamageTaken));
        Assert.False(f.Guard.IsIndeterminate);
        simulator.DuringCapture=null;
        Assert.Empty(session.Capture(saved.AshField,saved.BeaverBehavior).OwnedMaterial!.History!.StorageCredits);
    }
    [Fact]
    public void BodyLivenessReadCannotRegisterOrMutateResources()
    {
        var f=new F(); var retired=f.Registrations[0].EntityId;
        f.Damage.RemoveTarget(new(TimberbornBurnDamageIdentity.ForEntity(retired,NativeBurnTargetFamily.Tree)));
        f.Native.Live.Remove(retired);
        int mutations=0;
        f.Native.DuringIsLive=()=>
        {
            Assert.Throws<InvalidOperationException>(()=>f.Consumer.Register(f.Registrations[1]));
            Assert.Throws<InvalidOperationException>(()=>f.Guard.TransferInventory(()=>mutations++));
            Assert.Throws<InvalidOperationException>(()=>f.Guard.ResetForWorldLoad());
        };
        f.Consumer.CaptureHistory();
        Assert.Equal(0,mutations); Assert.False(f.Guard.IsIndeterminate);
    }
    [Fact]
    public void LostLiveBodyDefinitionCannotBeCapturedAsRetiredHistory()
    {
        var f=new F();
        f.Damage.RemoveTarget(new(TimberbornBurnDamageIdentity.ForEntity(f.Registrations[0].EntityId,NativeBurnTargetFamily.Tree)));
        Assert.Throws<InvalidOperationException>(()=>f.Consumer.CaptureHistory());
        Assert.False(f.Guard.IsIndeterminate);
    }
    [Fact]
    public void MaterialOnlyRestoreCannotInvokeAnyFactory()
    {
        var saved = OwnedMaterialPersistenceTests.Fixture(); var f=new F();
        Assert.Throws<NotSupportedException>(()=>TimberbornOwnedWorldSession<Simulator>.PrepareRestore(saved,[],
            _=>throw new Exception("not called"),_=>throw new Exception("not called"),f.Effects,f.Guard));
    }
    private static TimberbornOwnedWorldSession<Simulator> Restore(TimberbornWildfirePersistenceSnapshot saved,F f,out Simulator simulator)
    {
        simulator=new(saved.OwnedMaterial!.CaptureSimulation()); var result=simulator;
        return TimberbornOwnedWorldSession<Simulator>.PrepareRestore(saved,[],_=>result,_=>f.Damage,f.Effects,f.Guard,
            new TimberbornResourceFuelCatalog([new("Log",2,3,false,false,true)]));
    }
    internal static TimberbornWildfirePersistenceSnapshot Snapshot(F f)
    {
        var bindings=f.Registry.CaptureBindings(); var targets=new uint[8]; var slots=new uint[8];
        var known=new List<FireSimMaterialIdentity>();
        for(int i=0;i<f.Registrations.Length;i++)
        {
            var owner=bindings.Entities.Single(item=>item.EntityId==f.Registrations[i].EntityId);
            foreach(var slot in owner.Slots)
            { int cell=i+slot.LocalCoordinates.X*4; targets[cell]=owner.TargetId; slots[cell]=slot.SlotId; known.Add(new(owner.TargetId,slot.SlotId)); }
        }
        var sim=new FireSimSnapshot(1,new(4,2,1),37,FireSimParameters.Default,0,new ushort[8],new uint[8],new uint[8],targets,slots,
            new(0,known.ToArray(),[]),[]);
        var empty=TimberbornWildfirePersistenceSnapshot.Empty;
        return empty with { PersistenceVersion=2,OwnedMaterial=new(sim,bindings,f.Consumer.CaptureHistory()),
            Consequences=TimberbornWildfirePersistenceCodec.CaptureConsequences(f.Damage) };
    }
    private sealed class PresentationFake : ITimberbornOwnedPresentationApi
    { public OwnedPresentationResult Rehydrate(Guid id,NativeBurnTargetFamily family,OwnedCharredPresentation desired)=>OwnedPresentationResult.Applied; }
    private sealed class Simulator(FireSimSnapshot saved) : IGpuFireSimulator,IFireSimSnapshotSimulator,IDisposable
    {
        internal int Disposals; internal Exception? CaptureFailure; internal Action? DuringCapture;
        public int Width=>saved.Grid.Width;public int Height=>saved.Grid.Height;public int Depth=>saved.Grid.Depth;
        public FireSimSnapshotCapability SnapshotCapability=>FireSimSnapshotCapability.CompleteMaterialHistory;
        public FireSimSnapshot CaptureSnapshot() { DuringCapture?.Invoke(); return CaptureFailure is {} error ? throw error : saved; }
        public void Dispose()=>Disposals++;
        public void RegisterChange(FireSimChange change)=>throw new NotSupportedException();
        public GpuFireStepResult Tick()=>throw new NotSupportedException();
        public IDisposable Subscribe(IFireSimListener listener)=>throw new NotSupportedException();
    }
}
