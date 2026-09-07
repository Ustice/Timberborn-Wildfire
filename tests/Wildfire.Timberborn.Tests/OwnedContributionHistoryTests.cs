using F = Wildfire.Timberborn.Tests.OwnedConsequenceBatchTests.Fixture;

namespace Wildfire.Timberborn.Tests;

public sealed partial class OwnedWorldSessionTests
{
    [Fact]
    public void FractionalContributionSplitByRealCodecAndNewSessionMatchesFrozenBatch()
    {
        var together=new F(witnessed:true); var split=new F(witnessed:true); var stock=together.Registrations[2].EntityId;
        // Existing one-band credit belongs to this owner/resource, independent of location.
        together.Consumer.Consume(1,[together.Delta(stock,1)]);
        split.Consumer.Consume(1,[split.Delta(stock,1)]);
        together.Consumer.Consume(2,[together.Delta(stock,1),together.Delta(stock,2,6)]);
        split.Consumer.Consume(2,[split.Delta(stock,1)]);
        string encoded=TimberbornWildfirePersistenceCodec.Encode(Snapshot(split));
        var saved=TimberbornWildfirePersistenceCodec.Decode(encoded);
        var native=new F(witnessed:true); native.Native.Amounts[stock]=split.Native.Amounts[stock]; // Native inventory save is authority.
        using var restored=Restore(saved,native,out _);
        restored.Consumer.Consume(38,[split.Delta(stock,2,6)]);
        Assert.Equal(8,native.Native.Amounts[stock]);
        Assert.Equal(together.Native.Amounts[stock],native.Native.Amounts[stock]);
        Assert.Empty(restored.Consumer.CaptureHistory().StorageCredits);
        Assert.Empty(together.Consumer.CaptureHistory().StorageCredits);
    }
}
