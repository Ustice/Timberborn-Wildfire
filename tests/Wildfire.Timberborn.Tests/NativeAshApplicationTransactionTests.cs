using System.Reflection;
using Wildfire.Core;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeAshApplicationTransactionTests
{
    [Fact]
    public void OnlyExactPositiveCommitAuthorizesConsumptionAndPhaseAlwaysCloses()
    {
        var transaction = new NativeResourceTransaction();
        Denied(transaction);
        transaction.CaptureAtRest(() => { Denied(transaction); return 0; });
        transaction.TransferInventory(() => Denied(transaction));
        var simulator = new Simulator { BeforeCommit = () => Denied(transaction) };
        transaction.TryDeliver(simulator, new(0), () => Denied(transaction));
        int consumed = 0;
        var result = transaction.TryApplyCleanAsh(simulator, new(0, 2), receipt =>
        {
            transaction.RequireAshApplicationCommit();
            Assert.Equal(1, receipt.Added);
            Assert.Throws<InvalidOperationException>(transaction.ThrowIfSaveUnsafe);
            Assert.Throws<InvalidOperationException>(transaction.ResetForWorldLoad);
            Assert.Throws<InvalidOperationException>(() => transaction.TransferInventory(() => { }));
            Assert.Throws<InvalidOperationException>(() => transaction.TryApplyCleanAsh(simulator, new(0, 2), _ => { }));
            consumed++;
        });
        Assert.Equal(1, consumed);
        Assert.Equal(1, result!.Value.Receipt.Added);
        Denied(transaction);
        transaction.ThrowIfSaveUnsafe();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void NoAdmissionAndValidRejectionNeverOpenConsumption(bool noCapacity)
    {
        var transaction = new NativeResourceTransaction();
        int consumed = 0;
        var result = transaction.TryApplyCleanAsh(new Simulator { NoCapacity = noCapacity, Full = true }, new(0, 1), _ => consumed++);
        Assert.Equal(0, consumed);
        Assert.Equal(noCapacity, result is null);
        if (!noCapacity) Assert.Equal(FireSimAshApplicationOutcome.Full, result!.Value.Receipt.Outcome);
        Denied(transaction);
        transaction.ThrowIfSaveUnsafe();
    }

    [Theory]
    [InlineData("zero")] [InlineData("identity")] [InlineData("limit")] [InlineData("status")]
    public void SimulatorCannotAuthorizeIncorrectReceipt(string corruption)
    {
        var transaction = new NativeResourceTransaction();
        int consumed = 0;
        Assert.Throws<FireSimStepInputException>(() => transaction.TryApplyCleanAsh(
            new Simulator { Corruption = corruption }, new(0, 2), _ => consumed++));
        Assert.Equal(0, consumed);
        Assert.True(transaction.IsIndeterminate);
        Denied(transaction);
    }

    [Fact]
    public void MutationThenCallbackFailurePoisonsWithoutReplayAndClearsCommitPhase()
    {
        var transaction = new NativeResourceTransaction();
        int nativeStock = 1;
        var failure = new IOException("native callback failed after quantity mutation");
        var error = Assert.Throws<FireSimStepInputException>(() => transaction.TryApplyCleanAsh(new Simulator(), new(0, 2), _ =>
        {
            transaction.RequireAshApplicationCommit();
            nativeStock--;
            throw failure;
        }));
        Assert.Same(failure, error.InnerException);
        Assert.Equal(0, nativeStock);
        Assert.True(transaction.IsIndeterminate);
        Denied(transaction);
        Assert.Throws<InvalidOperationException>(transaction.ThrowIfSaveUnsafe);
        Assert.Throws<InvalidOperationException>(() => transaction.TryApplyCleanAsh(new Simulator(), new(0, 2), _ => nativeStock--));
        transaction.ResetForWorldLoad();
        Denied(transaction);
        transaction.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void ListenerFailureIsCommittedAndDoesNotKeepConsumptionOpen()
    {
        var transaction = new NativeResourceTransaction();
        int consumed = 0;
        var error = Assert.Throws<FireSimStepInputException>(() => transaction.TryApplyCleanAsh(
            new Simulator { ListenerFailure = true }, new(0, 2), _ => consumed++));
        Assert.Equal(FireSimStepInputOutcome.Committed, error.Outcome);
        Assert.Equal(1, consumed);
        Assert.False(transaction.IsIndeterminate);
        Denied(transaction);
        transaction.ThrowIfSaveUnsafe();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CaughtLifecycleInvalidationDuringAppliedCommitCannotReturnSuccess(bool throwAfterInvalidation)
    {
        var transaction = new NativeResourceTransaction();
        var original = new IOException("native teardown failed after applied stock mutation");
        int consumed = 0;
        var error = Assert.Throws<FireSimStepInputException>(() => transaction.TryApplyCleanAsh(
            new Simulator(), new(0, 2), _ =>
            {
                transaction.RequireAshApplicationCommit();
                consumed++;
                transaction.InvalidateAfterLifecycleFailure();
                Denied(transaction); // Even a still-open callback loses privilege once invalidated.
                if (throwAfterInvalidation) throw original;
            }));
        Assert.Equal(FireSimStepInputOutcome.Indeterminate, error.Outcome);
        if (throwAfterInvalidation) Assert.Same(original, error.InnerException);
        Assert.Equal(1, consumed);
        Assert.True(transaction.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(transaction.ThrowIfSaveUnsafe);
        Denied(transaction);
        transaction.ResetForWorldLoad(); // Finally released the outer latch and positive phase.
        transaction.ThrowIfSaveUnsafe();
        Denied(transaction);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LifecycleInvalidationCannotHideBehindRejectedReceiptOrNullAdmission(bool noAdmission)
    {
        var transaction = new NativeResourceTransaction();
        int consumed = 0;
        var simulator = new Simulator
        {
            NoCapacity = noAdmission,
            Full = true,
            BeforeApplication = transaction.InvalidateAfterLifecycleFailure
        };
        var error = Assert.Throws<FireSimStepInputException>(() =>
            transaction.TryApplyCleanAsh(simulator, new(0, 2), _ => consumed++));
        Assert.Equal(FireSimStepInputOutcome.Indeterminate, error.Outcome);
        Assert.Equal(0, consumed);
        Assert.True(transaction.IsIndeterminate);
        Denied(transaction);
        Assert.Throws<InvalidOperationException>(transaction.ThrowIfSaveUnsafe);
    }

    [Fact]
    public void CoordinatorUsesAttachedWorldAndForwardsItsExistingTransaction()
    {
        using var native = new NativeManagedTestContext();
        var type = native.LoadMod().GetType("Wildfire.Timberborn.Resources.NativeResourceCoordinator")!;
        var coordinator = Activator.CreateInstance(type)!;
        var apply = type.GetMethod("TryApplyCleanAsh", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object? Invoke(string name, params object[] args) => type.GetMethod(name)!.Invoke(coordinator, args);
        void Apply(Action<FireSimAshApplicationReceipt> commit) => apply.Invoke(coordinator,
            [new FireSimAshApplicationInput(0, 2), commit]);
        var absent = Assert.Throws<TargetInvocationException>(() => Apply(_ => { }));
        Assert.IsType<InvalidOperationException>(absent.InnerException);
        var simulator = new Simulator();
        Invoke("Attach", simulator);
        Apply(_ =>
        {
            Invoke("RequireAshApplicationCommit");
            Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => Invoke("ThrowIfSaveUnsafe")).InnerException);
        });
        Assert.Equal(1, simulator.ApplicationCalls);
        Invoke("ThrowIfSaveUnsafe");
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(() => Invoke("RequireAshApplicationCommit")).InnerException);
        Invoke("ResetForWorldLoad");
        Assert.Throws<TargetInvocationException>(() => Apply(_ => { }));
        Assert.Equal(1, simulator.ApplicationCalls);
    }

    private static void Denied(NativeResourceTransaction transaction) =>
        Assert.Throws<InvalidOperationException>(transaction.RequireAshApplicationCommit);

    private sealed class Simulator : IFireSimAshApplicationSimulator, IFireSimAshCollectionSimulator
    {
        public int ApplicationCalls;
        public GpuFireStepResult? TryCollectAsh(FireSimAshCollectionInput input, Action<FireSimAshCollectionReceipt> commit) => throw new NotSupportedException();
        public bool NoCapacity, Full, ListenerFailure;
        public string? Corruption;
        public Action? BeforeCommit, BeforeApplication;
        public int Width => 1;
        public int Height => 1;
        public int Depth => 1;
        public void RegisterChange(FireSimChange change) => throw new NotSupportedException();
        public IDisposable Subscribe(IFireSimListener listener) => throw new NotSupportedException();
        public GpuFireStepResult Tick() => throw new NotSupportedException();
        public GpuFireStepResult? TryTickWithInput(FireSimChange input, Action commit)
        {
            if (NoCapacity) return null;
            BeforeCommit?.Invoke();
            try { commit(); }
            catch (Exception exception) { throw new FireSimStepInputException(FireSimStepInputOutcome.Indeterminate, exception); }
            if (ListenerFailure) throw new FireSimStepInputException(FireSimStepInputOutcome.Committed, new IOException("listener"));
            return new GpuFireStepResult([], 1);
        }
        public FireSimAshApplicationStepResult? TryApplyCleanAsh(FireSimAshApplicationInput input, Action<FireSimAshApplicationReceipt> commit)
        {
            ApplicationCalls++;
            BeforeApplication?.Invoke();
            var receipt = new FireSimAshApplicationReceipt(input.CellIndex, input.Limit,
                (byte)(Full ? 0 : 1), Full ? FireSimAshApplicationOutcome.Full : FireSimAshApplicationOutcome.Applied);
            receipt = Corruption switch
            {
                "zero" => receipt with { Added = 0 }, "identity" => receipt with { CellIndex = 1 },
                "limit" => receipt with { Limit = 3 }, "status" => receipt with { Outcome = (FireSimAshApplicationOutcome)9 }, _ => receipt,
            };
            var step = TryTickWithInput(new(input.CellIndex), () => { if (!Full) commit(receipt); });
            return step is { } completed ? new FireSimAshApplicationStepResult(completed, receipt) : null;
        }
    }
}
