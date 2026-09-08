using System.Reflection;
using Wildfire.Core;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

// Coordinator contract tests; supplied producers and simulator do not prove an actual actor trip or GPU step.
public sealed class NativeWaterResponderDispatchTests
{
    [Fact]
    public void DifferentRespondersShareOneGuardedApplicationPath() => RunNative(nameof(DifferentRespondersShareOneGuardedApplicationPathCase));

    private static void DifferentRespondersShareOneGuardedApplicationPathCase()
    {
        var coordinator = new NativeResourceCoordinator();
        var simulator = new Simulator();
        coordinator.Attach(simulator);
        var first = new Responder(2, () => Assert.Throws<InvalidOperationException>(coordinator.ThrowIfSaveUnsafe));
        var second = new Responder(7, () => Assert.Throws<InvalidOperationException>(coordinator.ThrowIfSaveUnsafe));
        coordinator.Register(first);
        coordinator.Register(second);

        coordinator.Tick();
        Assert.Equal(1, first.Commits);
        Assert.Equal(0, second.Commits);
        coordinator.Tick();
        Assert.Equal(new[] { 2, 7 }, simulator.Accepted.Select(input => input.CellIndex));
        Assert.All(simulator.Accepted, input => Assert.Equal((byte?)3, input.AddWater));
        Assert.Equal(1, first.Commits);
        Assert.Equal(1, second.Commits);
        Assert.Equal(0, simulator.OrdinaryTicks);
        coordinator.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void FullQueueRetainsBothIntentsAndDoesNotTryAnotherResponder() => RunNative(nameof(FullQueueRetainsBothIntentsAndDoesNotTryAnotherResponderCase));

    private static void FullQueueRetainsBothIntentsAndDoesNotTryAnotherResponderCase()
    {
        var coordinator = new NativeResourceCoordinator();
        var simulator = new Simulator { Reject = true };
        coordinator.Attach(simulator);
        var first = new Responder(2);
        var second = new Responder(7);
        coordinator.Register(first);
        coordinator.Register(second);

        coordinator.Tick();
        Assert.Equal(1, first.Preparations);
        Assert.Equal(0, second.Preparations);
        Assert.Equal(0, first.Commits + second.Commits);
        Assert.Equal(1, simulator.OrdinaryTicks);
        Assert.Empty(simulator.Accepted);
        simulator.Reject = false;
        coordinator.Tick();
        Assert.Equal(1, first.Commits);
        Assert.Equal(0, second.Commits);
        Assert.Equal(2, Assert.Single(simulator.Accepted).CellIndex);
    }

    [Fact]
    public void RemovedResponderCannotApplyItsPendingIntent() => RunNative(nameof(RemovedResponderCannotApplyItsPendingIntentCase));

    private static void RemovedResponderCannotApplyItsPendingIntentCase()
    {
        var coordinator = new NativeResourceCoordinator();
        var simulator = new Simulator();
        coordinator.Attach(simulator);
        var removed = new Responder(2);
        var retained = new Responder(7);
        coordinator.Register(removed);
        coordinator.Register(retained);
        coordinator.Unregister(removed);

        coordinator.Tick();
        Assert.Equal(0, removed.Preparations);
        Assert.Equal(0, removed.Commits);
        Assert.Equal(1, retained.Commits);
        Assert.Equal(7, Assert.Single(simulator.Accepted).CellIndex);
    }

    private static void RunNative(string method)
    {
        using var native = new NativeManagedTestContext();
        native.LoadMod();
        // Run the typed case inside the existing native resolver; do not add game assemblies to the default test context.
        var assembly = native.LoadFromAssemblyPath(typeof(NativeWaterResponderDispatchTests).Assembly.Location);
        assembly.GetType(typeof(NativeWaterResponderDispatchTests).FullName!)!
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null);
    }

    private sealed class Responder(int cell, Action? observeCommit = null) : INativeWaterApplicationProducer
    {
        public int Preparations { get; private set; }
        public int Commits { get; private set; }
        public bool TryPrepareApplication(out FireSimChange input, out Action commit)
        {
            Preparations++;
            input = new(cell, AddWater: 3);
            commit = () => { observeCommit?.Invoke(); Commits++; };
            return Commits == 0;
        }
    }

    private sealed class Simulator : IFireSimAshCollectionSimulator
    {
        public bool Reject { get; set; }
        public int OrdinaryTicks { get; private set; }
        public List<FireSimChange> Accepted { get; } = [];
        public int Width => 8;
        public int Height => 1;
        public int Depth => 1;
        public GpuFireStepResult Tick() { OrdinaryTicks++; return new([], 1); }
        public GpuFireStepResult? TryTickWithInput(FireSimChange input, Action commitInput)
        {
            if (Reject) return null;
            commitInput();
            Accepted.Add(input);
            return new([], 1);
        }
        public GpuFireStepResult? TryCollectAsh(FireSimAshCollectionInput input, Action<FireSimAshCollectionReceipt> commit) =>
            throw new NotSupportedException();
        public void RegisterChange(FireSimChange change) => throw new NotSupportedException();
        public IDisposable Subscribe(IFireSimListener listener) => throw new NotSupportedException();
    }
}
