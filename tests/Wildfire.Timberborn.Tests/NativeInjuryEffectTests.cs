using System.Reflection;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeInjuryEffectTests
{
    [Fact]
    public void NativeInjuryPreservesPriorPointsAndClampsAtInstalledLimits()
    {
        using var fixture = new NativeInjuryFixture();
        Assert.Equal(-1f, fixture.InstalledSpec.GetProperty("MinimumValue").GetSingle());
        Assert.Equal(0f, fixture.InstalledSpec.GetProperty("MaximumValue").GetSingle());
        fixture.Apply(-.25f);
        fixture.Apply(-.125f);
        Assert.Equal(-.375f, fixture.Points);
        fixture.Apply(-2f);
        Assert.Equal(-1f, fixture.Points);
        fixture.Apply(.25f); // Native treatment polarity, not a mod healing policy.
        Assert.Equal(-.75f, fixture.Points);
        fixture.Apply(2f);
        Assert.Equal(0f, fixture.Points);
    }

    [Fact]
    public void NativeNeedManagerPersistsActualPointsRatherThanExposureHistory()
    {
        using var fixture = new NativeInjuryFixture();
        fixture.Apply(-.375f);
        var save = fixture.Save();
        fixture.Apply(-.5f);
        fixture.Load(save);
        Assert.Equal(-.375f, fixture.Points);
        fixture.Apply(-.125f);
        Assert.Equal(-.5f, fixture.Points);
    }

    [Fact]
    public void MissingInjurySilentlyNoOpsAndMustBeRejectedByEligibility()
    {
        using var fixture = new NativeInjuryFixture(hasInjury: false);
        Assert.Equal(false, NativeInjuryFixture.Call(fixture.Manager, "HasNeed", "Injury"));
        int events = 0;
        fixture.On("NeedChangedActiveState", () => events++);
        fixture.Apply(-1f);
        Assert.Equal(0, events);
        Assert.Equal(0f, NativeInjuryFixture.Get(fixture.Need, "Points"));
    }

    [Fact]
    public void SaturatedInjuryDoesNotEmitAnotherCriticalTransition()
    {
        using var fixture = new NativeInjuryFixture();
        int transitions = 0;
        fixture.On("NeedChangedCriticalState", () => transitions++);
        fixture.Apply(-1f);
        fixture.Apply(-.25f);
        Assert.Equal(-1f, fixture.Points);
        Assert.Equal(1, transitions);
    }

    [Fact]
    public void DisabledInjuryHasNoPointEffectButRemainsARegisteredNeed()
    {
        using var fixture = new NativeInjuryFixture();
        fixture.Apply(-.25f);
        NativeInjuryFixture.Call(fixture.Manager, "DisableNeed", "Injury");
        Assert.Equal(true, NativeInjuryFixture.Call(fixture.Manager, "HasNeed", "Injury"));
        Assert.Equal(false, NativeInjuryFixture.Call(fixture.Manager, "NeedIsEnabled", "Injury"));
        fixture.Apply(-.5f);
        Assert.Equal(-.25f, fixture.Points);
        NativeInjuryFixture.Call(fixture.Manager, "EnableNeed", "Injury");
        fixture.Apply(-.125f);
        Assert.Equal(-.375f, fixture.Points);
    }

    [Fact]
    public void CallbackFailureOccursAfterPointsChangedAndBeforeLaterSubscribers()
    {
        using var fixture = new NativeInjuryFixture();
        int laterSubscribers = 0;
        fixture.On("NeedChangedCriticalState", () => throw new InvalidOperationException("native subscriber failure"));
        fixture.On("NeedChangedCriticalState", () => laterSubscribers++);
        var failure = Assert.Throws<TargetInvocationException>(() => fixture.Apply(-.25f));
        Assert.IsType<InvalidOperationException>(failure.InnerException);
        Assert.Equal(-.25f, fixture.Points);
        Assert.Equal(0, laterSubscribers);
        var save = fixture.Save();
        fixture.Apply(-.25f); // Deliberately demonstrate why retrying an uncertain attempt is unsafe.
        Assert.Equal(-.5f, fixture.Points);
        Assert.Throws<TargetInvocationException>(() => fixture.Load(save));
        Assert.Equal(-.25f, fixture.Points); // Load also restores points before unconditionally emitting listeners.
    }
}
