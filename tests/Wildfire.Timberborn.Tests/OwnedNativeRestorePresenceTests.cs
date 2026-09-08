using Wildfire.Timberborn.Persistence;
using F=Wildfire.Timberborn.Tests.OwnedNativeRestoreFixture;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedNativeRestorePresenceTests
{
    [Theory]
    [InlineData(TimberbornOwnedBodyPresence.Absent)]
    [InlineData(TimberbornOwnedBodyPresence.Deleted)]
    [InlineData(TimberbornOwnedBodyPresence.Uninitialized)]
    [InlineData(TimberbornOwnedBodyPresence.InvalidNativeReference)]
    public void NonLiveRetainedOwnerCannotProduceCompleteHistory(TimberbornOwnedBodyPresence presence)
    {
        var f=new F();f.Native.PresenceOverrides[F.Id]=presence;
        Assert.Throws<InvalidOperationException>(()=>f.Consumer.CaptureHistory());
        Assert.False(f.Guard.IsIndeterminate);
        f.Native.PresenceOverrides.Clear();Assert.NotNull(f.Consumer.CaptureHistory().NativeDefinitions);
    }

    [Theory]
    [InlineData(TimberbornOwnedBodyPresence.Live)]
    [InlineData(TimberbornOwnedBodyPresence.Deleted)]
    [InlineData(TimberbornOwnedBodyPresence.Uninitialized)]
    [InlineData(TimberbornOwnedBodyPresence.InvalidNativeReference)]
    public void RetiredHistoryRejectsEveryRegistryPresentState(TimberbornOwnedBodyPresence presence)
    {
        var f=new F();var source=f.Snapshot();var m=source.OwnedMaterial!;var h=m.History!;
        var retired=new OwnedConsequenceOwner(F.Id,NativeBurnTargetFamily.Crop,OwnedBodyRetention.RetiredNativeOwner,null);
        var snapshot=source with {Consequences=new([]),OwnedMaterial=new(m.CaptureSimulation(),m.Bindings,
            new([retired],h.Natural,[],new([])))};
        f.Native.Live.Remove(F.Id);f.Native.PresenceOverrides[F.Id]=presence;
        var simulator=new F.Simulator(m.CaptureSimulation());
        Assert.Throws<ArgumentException>(()=>TimberbornOwnedWorldSession<F.Simulator>.PrepareDiagnosticRestore(snapshot,[],_=>simulator,
            (_,_)=>[],f.Effects,f.Guard));
        Assert.Equal(1,simulator.Disposals);Assert.False(f.Guard.IsIndeterminate);
    }
}
