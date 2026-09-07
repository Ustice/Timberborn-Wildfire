using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Persistence;
using F = Wildfire.Timberborn.Tests.OwnedNativeRestoreFixture;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedRestoreStagingConsistencyTests
{
    [Theory]
    [InlineData("definition")]
    [InlineData("quantity")]
    [InlineData("availability")]
    [InlineData("membership")]
    [InlineData("mutable-capture")]
    public void ChangedNativeFactsAfterBackendConstructionRejectAndDisposeUnpublishedSession(string change)
    {
        var f = new F();
        var saved = f.Snapshot();
        var simulator = new F.Simulator(saved.OwnedMaterial!.CaptureSimulation());
        var original = new List<TimberbornInitialMaterialBody> { F.Facts() };
        IReadOnlyList<TimberbornInitialMaterialBody> current = original;
        int captures = 0;
        Assert.Throws<ArgumentException>(() => TimberbornOwnedWorldSession<F.Simulator>.PrepareRestore(saved, [], _ =>
        {
            if (change == "mutable-capture") original[0] = F.Facts(actual: 2);
            current = change switch
            {
                "definition" => [F.Facts(declared: 7)],
                "quantity" => [F.Facts(actual: 2)],
                "availability" => [F.Facts(enabled: false)],
                "mutable-capture" => original,
                _ => [],
            };
            return simulator;
        }, (_, _) => { captures++; return current; }, f.Effects, f.Guard));
        Assert.Equal(2, captures);
        Assert.Equal(1, simulator.Disposals);
        Assert.False(f.Guard.IsIndeterminate);
        Assert.Equal(15, f.Damage.States[F.Key].DamageCapacity);
    }
}
