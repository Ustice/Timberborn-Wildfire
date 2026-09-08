using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class TimberbornAshFieldSynchronizerTests
{
    [Fact]
    public void RuntimeSynchronizationReadsTransportWithoutCapturingPersistence()
    {
        RecordingTransportSimulator simulator = new() { TransportFields = [Ash(2, contamination: 6)] };
        TimberbornFireSystem fireSystem = new(simulator);
        TimberbornAshFieldService ash = new();
        TimberbornAshFieldSynchronizer synchronizer = new(ash);

        synchronizer.Sync(fireSystem, tick: 9, dayNumber: 4);

        Assert.Equal(1, simulator.TransportReadCount);
        Assert.True(ash.TryGetEntry(0, out TimberbornAshFieldEntry entry));
        Assert.Equal(WildfireAshQuality.Tainted, entry.Quality);
        Assert.Equal(9u, entry.UpdatedTick);
        Assert.Equal(4, entry.UpdatedDayNumber);
    }

    [Fact]
    public void SameTickDoesNotReadAgainAndNextTickReplacesAshFromGpuState()
    {
        RecordingTransportSimulator simulator = new() { TransportFields = [Ash(2)] };
        TimberbornFireSystem fireSystem = new(simulator);
        TimberbornAshFieldService ash = new();
        TimberbornAshFieldSynchronizer synchronizer = new(ash);

        synchronizer.Sync(fireSystem, tick: 9, dayNumber: 4);
        simulator.TransportFields = [0u];
        synchronizer.Sync(fireSystem, tick: 9, dayNumber: 4);

        Assert.Equal(1, simulator.TransportReadCount);
        Assert.True(ash.TryGetEntry(0, out _));
        Assert.Equal(0, ash.LastSummary.GrowthAppliedGrowableCount);

        synchronizer.Sync(fireSystem, tick: 10, dayNumber: 4);

        Assert.Equal(2, simulator.TransportReadCount);
        Assert.False(ash.TryGetEntry(0, out _));
    }

    [Fact]
    public void MissingSystemAndEmptyObservationDoNotConsumeTick()
    {
        RecordingTransportSimulator simulator = new();
        TimberbornFireSystem fireSystem = new(simulator);
        TimberbornAshFieldService ash = new();
        TimberbornAshFieldSynchronizer synchronizer = new(ash);

        synchronizer.Sync(null, tick: 0, dayNumber: 1);
        synchronizer.Sync(fireSystem, tick: 0, dayNumber: 1);
        simulator.TransportFields = [Ash(1)];
        synchronizer.Sync(fireSystem, tick: 0, dayNumber: 1);

        Assert.Equal(2, simulator.TransportReadCount);
        Assert.True(ash.TryGetEntry(0, out _));
    }

    [Fact]
    public void ClearingLifecycleStateAllowsSameTickToBeReadAgain()
    {
        RecordingTransportSimulator simulator = new() { TransportFields = [Ash(1)] };
        TimberbornFireSystem fireSystem = new(simulator);
        TimberbornAshFieldService ash = new();
        TimberbornAshFieldSynchronizer synchronizer = new(ash);

        synchronizer.Sync(fireSystem, tick: 1, dayNumber: 1);
        ash.Clear();
        synchronizer.Clear();
        simulator.TransportFields = [Ash(3, contamination: 7)];
        synchronizer.Sync(fireSystem, tick: 1, dayNumber: 2);

        Assert.Equal(2, simulator.TransportReadCount);
        Assert.True(ash.TryGetEntry(0, out TimberbornAshFieldEntry entry));
        Assert.Equal(WildfireAshQuality.Tainted, entry.Quality);
        Assert.Equal(2, entry.UpdatedDayNumber);
    }

    [Fact]
    public void ReadFailurePropagatesAndDoesNotConsumeTick()
    {
        RecordingTransportSimulator simulator = new() { FailRead = true };
        TimberbornFireSystem fireSystem = new(simulator);
        TimberbornAshFieldService ash = new();
        TimberbornAshFieldSynchronizer synchronizer = new(ash);

        Assert.Throws<InvalidOperationException>(() => synchronizer.Sync(fireSystem, tick: 1, dayNumber: 1));
        simulator.FailRead = false;
        simulator.TransportFields = [Ash(1)];
        synchronizer.Sync(fireSystem, tick: 1, dayNumber: 1);

        Assert.Equal(2, simulator.TransportReadCount);
        Assert.True(ash.TryGetEntry(0, out _));
    }

    [Fact]
    public void InitializedSimulatorWithoutObservationCapabilityFailsClearly()
    {
        TimberbornFireSystem fireSystem = new(new SimulatorWithoutObservations());
        TimberbornAshFieldService ash = new();
        TimberbornAshFieldSynchronizer synchronizer = new(ash);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => synchronizer.Sync(fireSystem, tick: 1, dayNumber: 1));

        Assert.Contains("does not support transport field observations", exception.Message);
    }

    [Fact]
    public void UninitializedSystemDoesNotConsumeTick()
    {
        TimberbornFireSystem uninitialized = new(new UnusedSimulatorFactory());
        RecordingTransportSimulator simulator = new() { TransportFields = [Ash(1)] };
        TimberbornAshFieldService ash = new();
        TimberbornAshFieldSynchronizer synchronizer = new(ash);

        synchronizer.Sync(uninitialized, tick: 0, dayNumber: 1);
        synchronizer.Sync(new TimberbornFireSystem(simulator), tick: 0, dayNumber: 1);

        Assert.Equal(1, simulator.TransportReadCount);
        Assert.True(ash.TryGetEntry(0, out _));
    }

    private static uint Ash(byte amount, byte contamination = 0)
    {
        return new WildfireTransportFieldState(0, 0, 0, amount, contamination, Source: false).Pack();
    }

    private sealed class RecordingTransportSimulator : SimulatorWithoutObservations,
        ITimberbornTransportFieldReader,
        ITimberbornFireSimPersistenceState
    {
        public IReadOnlyList<uint> TransportFields { get; set; } = [];
        public int TransportReadCount { get; private set; }
        public bool FailRead { get; set; }

        public IReadOnlyList<uint> ReadTransportFields()
        {
            TransportReadCount++;
            if (FailRead)
            {
                throw new InvalidOperationException("Transport read failed.");
            }

            return TransportFields;
        }

        public TimberbornFireSimPersistenceSnapshot CaptureFireSimState()
        {
            throw new InvalidOperationException("Runtime ash synchronization must not capture save state.");
        }

        public void RestoreFireSimState(TimberbornFireSimPersistenceSnapshot snapshot) => throw new NotSupportedException();
    }

    private class SimulatorWithoutObservations : IGpuFireSimulator
    {
        public int Width => 1;
        public int Height => 1;
        public int Depth => 1;
        public void RegisterChange(FireSimChange change) => throw new NotSupportedException();
        public GpuFireStepResult Tick() => throw new NotSupportedException();
        public IDisposable Subscribe(IFireSimListener listener) => throw new NotSupportedException();
    }

    private sealed class UnusedSimulatorFactory : ITimberbornFireSimulatorFactory
    {
        public IGpuFireSimulator Create(
            FireGrid grid,
            ReadOnlySpan<ushort> initialCells,
            ReadOnlySpan<WildfireMaterialField> materialFields) => throw new NotSupportedException();
    }

}
