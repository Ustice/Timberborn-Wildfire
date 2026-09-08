using System.Reflection;
using Wildfire.Core;

namespace Wildfire.Timberborn.Tests;

public sealed partial class NativeRuntimeIncompleteDispatchTests
{
    [Theory]
    [InlineData("preparation")]
    [InlineData("listener")]
    [InlineData("followup")]
    [InlineData("final_log")]
    public void RuntimeSaveAndGuardedCaptureRejectThroughoutSuccessfulDispatch(string boundary)
    {
        var f = new Fixture(synchronizationAlreadyComplete: true);
        var saves = new List<Exception?>();
        var captures = new List<Exception?>();
        int captureReads = 0;
        void Probe()
        {
            saves.Add(InnerFailure(f.SaveAdmission));
            captures.Add(InnerFailure(() => f.Capture(() => captureReads++)));
        }
        using var subscription = f.Simulator.Subscribe(new DispatchListener(() =>
        {
            if (boundary == "listener") Probe();
        }));
        if (boundary == "preparation")
        {
            f.PrepareRepeatedInput();
            f.Simulator.OnRegisterChange = Probe;
        }
        f.OnLog = message =>
        {
            if (boundary == "followup" && message.StartsWith("wildfire_timberborn_beaver_field_exposure") ||
                boundary == "final_log" && message.StartsWith("wildfire_timberborn_runtime_dispatched ")) Probe();
        };

        f.Dispatch();

        Assert.NotEmpty(saves);
        Assert.All(saves, failure => Assert.IsType<InvalidOperationException>(failure));
        Assert.All(captures, failure => Assert.IsType<InvalidOperationException>(failure));
        Assert.Equal(0, captureReads);
        Assert.False(f.Poisoned);
        Assert.Equal(1, f.Simulator.Swaps);
        Assert.IsType<ArgumentNullException>(InnerFailure(f.SaveAdmission));
        f.Capture(() => captureReads++);
        Assert.Equal(1, captureReads);
    }

    [Fact]
    public void RecursiveNormalRuntimeEntryRejectsBeforeAnotherStepWithoutPoisoningOuter()
    {
        var f = new Fixture(synchronizationAlreadyComplete: true);
        Exception? nested = null;
        bool attempted = false;
        f.OnLog = message =>
        {
            if (attempted || !message.StartsWith("wildfire_timberborn_runtime_dispatched ")) return;
            attempted = true;
            nested = InnerFailure(f.Dispatch);
        };
        f.Dispatch();
        Assert.True(attempted);
        Assert.IsType<InvalidOperationException>(nested);
        Assert.Equal(1, f.Simulator.Swaps);
        Assert.False(f.Poisoned);
        Assert.IsType<ArgumentNullException>(InnerFailure(f.SaveAdmission));
    }

    [Fact]
    public void BetweenOperationsTransferIsAllowedButItsNestedWriteAndSaveAreRejected()
    {
        var f = new Fixture(synchronizationAlreadyComplete: true);
        int writes = 0;
        Exception? nested = null, save = null;
        f.OnLog = message =>
        {
            if (!message.StartsWith("wildfire_timberborn_runtime_dispatched ")) return;
            f.Transfer(() =>
            {
                writes++;
                nested = InnerFailure(() => f.Transfer(() => writes++));
                save = InnerFailure(f.SaveAdmission);
            });
        };
        f.Dispatch();
        Assert.Equal(1, writes);
        Assert.IsType<InvalidOperationException>(nested);
        Assert.IsType<InvalidOperationException>(save);
        Assert.False(f.Poisoned);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActiveDispatchCannotReplaceItsAttachedWorld(bool reset)
    {
        var f = new Fixture(synchronizationAlreadyComplete: true);
        Exception? failure = null;
        f.OnLog = message =>
        {
            if (message.StartsWith("wildfire_timberborn_runtime_dispatched "))
                failure = InnerFailure(reset ? f.Reset : f.Attach);
        };
        f.Dispatch();
        Assert.IsType<InvalidOperationException>(failure);
        Assert.False(f.Poisoned);
    }

    [Fact]
    public void CaughtLifecycleInvalidationAfterFollowupsCannotReturnSuccessfulDispatch()
    {
        var f = new Fixture(synchronizationAlreadyComplete: true);
        f.OnLog = message =>
        {
            if (message.StartsWith("wildfire_timberborn_runtime_dispatched ")) f.Invalidate();
        };
        Assert.IsType<InvalidOperationException>(InnerFailure(f.Dispatch));
        Assert.True(f.Poisoned);
        Assert.IsType<InvalidOperationException>(InnerFailure(f.SaveAdmission));
        Assert.Equal(1, f.Simulator.Swaps);
    }

    private static Exception? InnerFailure(Action action)
    {
        var exception = Record.Exception(action);
        return exception is TargetInvocationException wrapper ? wrapper.InnerException : exception;
    }

    private sealed class DispatchListener(Action callback) : IFireSimListener
    {
        public void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas) => callback();
    }
}
