using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class WildfireAtmosphericFieldStateTests
{
    [Fact]
    public void PackUsesAgreedAtmosphericBitLayout()
    {
        WildfireAtmosphericFieldState state = new(
            Steam: 5,
            Smoke: 6,
            SmokeContamination: 3,
            Ash: 4,
            AshContamination: 7,
            Source: true);

        uint packed = state.Pack();

        Assert.Equal(5u, (packed >> 0) & 0x7u);
        Assert.Equal(6u, (packed >> 3) & 0x7u);
        Assert.Equal(3u, (packed >> 6) & 0x7u);
        Assert.Equal(4u, (packed >> 9) & 0x7u);
        Assert.Equal(7u, (packed >> 12) & 0x7u);
        Assert.Equal(1u, (packed >> 15) & 0x1u);
        Assert.Equal(state, WildfireAtmosphericFieldState.Unpack(packed));
    }

    [Fact]
    public void PackClearsContaminationWhenCarrierAmountIsZero()
    {
        WildfireAtmosphericFieldState state = new(
            Steam: 0,
            Smoke: 0,
            SmokeContamination: 7,
            Ash: 0,
            AshContamination: 7,
            Source: false);

        WildfireAtmosphericFieldState unpacked = WildfireAtmosphericFieldState.Unpack(state.Pack());

        Assert.Equal(0, unpacked.SmokeContamination);
        Assert.Equal(0, unpacked.AshContamination);
    }
}
