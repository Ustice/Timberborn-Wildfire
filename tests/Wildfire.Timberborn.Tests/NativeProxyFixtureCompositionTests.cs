namespace Wildfire.Timberborn.Tests;

public sealed class NativeProxyFixtureCompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnvironmentAndSchedulerKeepNativeTypeIdentityAcrossFixtureOrder(bool schedulerFirst)
    {
        // Both fixtures proxy different interfaces from Timberborn.TickSystem. Creating one
        // must not cause DispatchProxy to bind the other's interface to a foreign native ALC.
        NativeEnvironmentFixture environment;
        NativeWaterCreditSchedulerFixture scheduler;
        if (schedulerFirst) { scheduler = new(); environment = new(); }
        else { environment = new(); scheduler = new(); }
        using (scheduler)
        {
            Assert.Equal(new[] { 4f, 12f }, environment.Capture().SoilSurfaces.Select(value => value.Moisture));
            scheduler.LoadAll();
            scheduler.RunNativeSingletonTicks();
            Assert.Equal(new[] { "observe", "credited", "later" }, scheduler.Events);
        }
    }
}
