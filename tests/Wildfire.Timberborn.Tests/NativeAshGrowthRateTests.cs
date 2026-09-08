namespace Wildfire.Timberborn.Tests;

public sealed class NativeAshGrowthRateTests
{
    [Theory]
    [InlineData(2f, .55f)]
    [InlineData(10f, .11f)]
    public void NativeTimerAddsOnlyElapsedBonusRelativeToItsOwnDuration(float duration, float expected)
    {
        var f = new NativeAshGrowthFixture(duration);
        f.Day = 1;
        Assert.True(f.Advance(1));
        Assert.Equal(expected, f.Progress, 5);
        Assert.Equal(true, f.Get(f.Growable, "GrowthInProgress"));
    }

    [Fact]
    public void SplitElapsedIntervalsMatchAndPausedTimerIsNeverResumed()
    {
        var whole = new NativeAshGrowthFixture(10); whole.Day = 1; whole.Advance(1);
        var split = new NativeAshGrowthFixture(10);
        for (int i = 0; i < 10; i++) { split.Day = (i + 1) / 10f; split.Advance(.1f); }
        Assert.Equal(whole.Progress, split.Progress, 5);
        split.Call(split.Trigger, "Pause");
        float paused = split.Progress;
        split.Day = 5;
        Assert.False(split.Advance(4));
        Assert.Equal(paused, split.Progress);
        Assert.Equal(false, split.Get(split.Growable, "GrowthInProgress"));
        split.Call(split.Trigger, "Resume"); split.Day += .1f;
        Assert.True(split.Advance(.1f));
        Assert.Equal(paused + .011f, split.Progress, 5);
    }

    [Theory]
    [InlineData("dead")]
    [InlineData("dying")]
    [InlineData("grown")]
    [InlineData("zero")]
    public void ActualLifecycleAndZeroTimeDeclineWithoutNewProgress(string state)
    {
        var f = new NativeAshGrowthFixture();
        if (state == "dead") NativeAshGrowthFixture.Set(f.Living, "<IsDead>k__BackingField", true);
        if (state == "dying") NativeAshGrowthFixture.Set(f.Dying, "<IsDying>k__BackingField", true);
        if (state == "grown") f.Call(f.Trigger, "FastForwardProgress", 1f);
        float before = f.Progress;
        Assert.False(f.Advance(state == "zero" ? 0 : .1f));
        Assert.Equal(before, f.Progress);
        Assert.Equal(state == "grown" ? 1 : 0, f.Completed);
    }

    [Theory]
    [InlineData(float.NaN, 2f)]
    [InlineData(float.PositiveInfinity, 2f)]
    [InlineData(-1f, 2f)]
    [InlineData(1f, 0f)]
    [InlineData(1f, float.PositiveInfinity)]
    public void InvalidElapsedTimeOrDurationRejectsBeforeNativeCallback(float elapsed, float duration)
    {
        var f = new NativeAshGrowthFixture(duration);
        var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => f.Advance(elapsed));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Equal(0, f.Completed);
    }

    [Fact]
    public void NativeProgressSurvivesSaveLoadAndAshRestoreSyncDecayCannotAdvanceIt()
    {
        var f = new NativeAshGrowthFixture(10); f.Day = 1; f.Advance(1);
        object saved = f.Save();
        var loaded = new NativeAshGrowthFixture(10); loaded.Load(saved);
        Assert.Equal(f.Progress, loaded.Progress, 5);
        var field = new TimberbornAshFieldService();
        uint ash = new Wildfire.Core.WildfireTransportFieldState(0, 0, 0, 3, 0, false).Pack();
        field.SyncFromTransportFields(7, [ash], 4);
        var snapshot = field.SaveSnapshot();
        var restored = new TimberbornAshFieldService();
        float before = loaded.Progress;
        restored.RestoreSnapshot(7, snapshot, 4);
        restored.SyncFromTransportFields(7, [ash], 4);
        restored.SyncFromTransportFields(7, [ash], 4);
        restored.ApplyDayDecay(8, 19);
        Assert.Equal(before, loaded.Progress);
        Assert.Equal(0, loaded.Completed);
        loaded.Day = .1f; loaded.Advance(.1f);
        Assert.Equal(before + .011f, loaded.Progress, 5);
    }

    [Fact]
    public void NativeCompletionRunsOnceAndThrowPoisonsTheExistingCoordinatorWithoutReplay()
    {
        var f = new NativeAshGrowthFixture(1);
        var coordinator = Activator.CreateInstance(f.Native.LoadMod().GetType("Wildfire.Timberborn.Resources.NativeResourceCoordinator")!)!;
        var expected = new InvalidOperationException("native completion callback");
        f.Completion = () =>
        {
            Assert.Throws<System.Reflection.TargetInvocationException>(() => f.Call(coordinator, "ThrowIfSaveUnsafe"));
            Assert.Throws<System.Reflection.TargetInvocationException>(() => f.Call(coordinator, "TransferInventory", (Action)(() => {})));
            throw expected;
        };
        f.Day = .99f;
        var error = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            f.Call(coordinator, "TransferInventory", (Action)(() => f.Advance(.2f))));
        Assert.Same(expected, error.GetBaseException());
        Assert.Equal(1f, f.Progress);
        Assert.Equal(1, f.Completed);
        Assert.Equal(true, f.Get(coordinator, "IsIndeterminate"));
        Assert.Throws<System.Reflection.TargetInvocationException>(() => f.Call(coordinator, "ThrowIfSaveUnsafe"));
        Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            f.Call(coordinator, "TransferInventory", (Action)(() => f.Advance(.2f))));
        Assert.Equal(1, f.Completed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeDeathAndPauseHandlerOrEarlierThrowBothPreventBonus(bool earlierThrow)
    {
        var f = new NativeAshGrowthFixture();
        var died = f.Living.GetType().GetEvent("Died")!;
        if (earlierThrow) died.AddEventHandler(f.Living, (EventHandler)((_, _) => throw new InvalidOperationException("earlier death subscriber")));
        var pause = f.Growable.GetType().GetMethod("<InitializeEntity>b__23_2", NativeAshGrowthFixture.Flags)!;
        died.AddEventHandler(f.Living, pause.CreateDelegate(typeof(EventHandler), f.Growable));
        if (earlierThrow) Assert.Throws<System.Reflection.TargetInvocationException>(() => f.Call(f.Living, "Die"));
        else f.Call(f.Living, "Die");
        Assert.Equal(true, f.Get(f.Living, "IsDead"));
        Assert.Equal(earlierThrow, f.Get(f.Growable, "GrowthInProgress"));
        float before = f.Progress;
        Assert.False(f.Advance(.1f));
        Assert.Equal(before, f.Progress);
    }
}
