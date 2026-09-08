using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class FireSimAshApplicationProtocolTests
{
    [Theory]
    [InlineData(1)] [InlineData(3)]
    public void UploadIsExclusiveAndNeverContainsReceiptBits(byte limit)
    {
        var command = FireSimGpuProtocol.EncodeChange(new(4, ApplyCleanAshLimit: limit));
        Assert.Equal(16, FireSimGpuProtocol.ChangeStrideBytes);
        Assert.Equal(1u << 12, command.SetMask);
        Assert.Equal((uint)limit << 25, command.AddFields);
        Assert.Equal(0u, command.SetValues);
        Assert.Throws<ArgumentException>(() => FireSimGpuProtocol.EncodeChange(new(4, AddAsh: 0, ApplyCleanAshLimit: limit)));
        Assert.Throws<ArgumentException>(() => FireSimGpuProtocol.EncodeChange(new(4, CollectCleanAsh: 0, ApplyCleanAshLimit: limit)));
    }

    [Theory]
    [InlineData(0)] [InlineData(4)]
    public void LimitIsExplicitAndStrict(byte limit) => Assert.Throws<ArgumentOutOfRangeException>(
        () => FireSimGpuProtocol.EncodeChange(new(0, ApplyCleanAshLimit: limit)));

    [Theory]
    [InlineData(0, 1)] [InlineData(1, 0)] [InlineData(2, 0)] [InlineData(3, 0)]
    public void DecodesAllValidOutcomes(byte outcome, byte added)
    {
        var receipt = FireSimGpuProtocol.DecodeApplicationReceipt(Command(added, outcome), new(4, 2));
        Assert.Equal(new FireSimAshApplicationReceipt(4, 2, added, (FireSimAshApplicationOutcome)outcome), receipt);
    }

    [Theory]
    [InlineData("missing")] [InlineData("identity")] [InlineData("limit")]
    [InlineData("mask")] [InlineData("values")] [InlineData("ordinary-bit")]
    [InlineData("applied-zero")] [InlineData("rejected-one")] [InlineData("excess")]
    public void MalformedReceiptCannotAuthorizeStockConsumption(string failure)
    {
        var c = Command(1, 0);
        uint fields = c.AddFields;
        if (failure == "missing") fields &= ~(1u << 29);
        if (failure == "limit") fields ^= 1u << 25;
        if (failure == "ordinary-bit") fields |= 1;
        if (failure == "applied-zero") fields &= ~(3u << 27);
        if (failure == "rejected-one") fields |= 1u << 30;
        if (failure == "excess") fields |= 3u << 27;
        c = new(c.CellIndex + (failure == "identity" ? 1u : 0),
            c.SetMask | (failure == "mask" ? 1u : 0), fields, failure == "values" ? 1u : 0);
        Assert.Throws<InvalidOperationException>(() => FireSimGpuProtocol.DecodeApplicationReceipt(c, new(4, 2)));
    }

    private static FireSimGpuChange Command(byte added, byte outcome) =>
        new(4, 1u << 12, (2u << 25) | ((uint)added << 27) | (1u << 29) | ((uint)outcome << 30), 0);
}
