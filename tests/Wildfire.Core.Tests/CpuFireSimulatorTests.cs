using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class CpuFireSimulatorTests
{
    [Fact]
    public void RegisteredChangeAppliesOnNextTick()
    {
        ushort[] cells = [PackedCell.Pack(5, 0, 2, 0, 1, 0)];
        CpuFireSimulator simulator = new(1, 1, 1, seed: 123, cells);

        Assert.Equal(0, PackedCell.Heat(simulator.Cells[0]));

        simulator.RegisterChange(new FireSimChange(CellIndex: 0, AddHeat: 8));
        FireStepResult result = simulator.Tick();

        Assert.NotEmpty(result.Deltas);
        Assert.NotEqual(0, PackedCell.Heat(simulator.Cells[0]));
    }

    [Fact]
    public void HashIsDeterministic()
    {
        uint first = FireRandom.Hash(12, 34, 56);
        uint second = FireRandom.Hash(12, 34, 56);

        Assert.Equal(first, second);
    }

    [Fact]
    public void ActiveFrontierKeepsHotCellsMovingAcrossTicks()
    {
        ushort[] cells = [PackedCell.Pack(5, 10, 0, 0, 1, 0)];
        CpuFireSimulator simulator = new(1, 1, 1, seed: 1, cells);

        FireStepResult first = simulator.Tick();
        FireStepResult second = simulator.Tick();

        Assert.NotEmpty(first.Deltas);
        Assert.NotEmpty(second.Deltas);
        Assert.True(PackedCell.Heat(simulator.Cells[0]) < 10);
    }

    [Fact]
    public void CandidateDedupeProcessesSharedNeighborOncePerTick()
    {
        ushort[] cells =
        [
            PackedCell.Pack(0, 0, 0, 0, 1, 0),
            PackedCell.Pack(0, 8, 0, 0, 1, 0),
            PackedCell.Pack(0, 0, 0, 0, 1, 0),
        ];
        CpuFireSimulator simulator = new(3, 1, 1, seed: 1, cells);

        simulator.RegisterChange(new FireSimChange(CellIndex: 0, AddFuel: 1));
        simulator.RegisterChange(new FireSimChange(CellIndex: 2, AddFuel: 1));
        FireStepResult result = simulator.Tick();

        Assert.Equal(5, PackedCell.Heat(simulator.Cells[1]));
        Assert.Equal(1, result.Deltas.Count(delta => delta.CellIndex == 1));
    }

    [Fact]
    public void ActiveFrontierPersistsWhenIgnitionRollLeavesCellUnchanged()
    {
        ushort[] cells =
        [
            PackedCell.Pack(5, 8, 3, 0, 1, 0),
            PackedCell.Pack(0, 12, 0, 0, 0, 0),
        ];
        CpuFireSimulator simulator = new(2, 1, 1, seed: 5, cells);

        FireStepResult first = simulator.Tick();
        FireStepResult second = simulator.Tick();

        Assert.DoesNotContain(first.Deltas, delta => delta.CellIndex == 0);
        Assert.Contains(second.Deltas, delta => delta.CellIndex == 0);
    }

    [Fact]
    public void ListenerRegisteredChangesApplyOnFollowingTick()
    {
        ushort[] cells =
        [
            PackedCell.Pack(0, 0, 0, 0, 1, 0),
            PackedCell.Pack(0, 0, 0, 0, 1, 0),
        ];
        CpuFireSimulator simulator = new(2, 1, 1, seed: 1, cells);
        using IDisposable subscription = simulator.Subscribe(new RegisteringListener(simulator));

        simulator.RegisterChange(new FireSimChange(CellIndex: 0, AddHeat: 4));
        FireStepResult first = simulator.Tick();
        FireStepResult second = simulator.Tick();

        Assert.DoesNotContain(first.Deltas, delta => delta.CellIndex == 1);
        Assert.Contains(second.Deltas, delta => delta.CellIndex == 1);
    }

    [Fact]
    public void ExternalAndRuleChangesReportOneNetDeltaPerCell()
    {
        ushort[] cells = [PackedCell.Pack(0, 0, 0, 0, 1, 0)];
        CpuFireSimulator simulator = new(1, 1, 1, seed: 1, cells);

        simulator.RegisterChange(new FireSimChange(CellIndex: 0, AddHeat: 4));
        FireStepResult result = simulator.Tick();
        CellDelta delta = Assert.Single(result.Deltas);

        Assert.Equal(0, delta.CellIndex);
        Assert.Equal(cells[0], delta.OldCell);
        Assert.Equal(simulator.Cells[0], delta.NewCell);
    }

    private sealed class RegisteringListener(CpuFireSimulator simulator) : IFireSimListener
    {
        private bool _registered;

        public void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas)
        {
            if (_registered || deltas.IsEmpty)
            {
                return;
            }

            _registered = true;
            simulator.RegisterChange(new FireSimChange(CellIndex: 1, AddHeat: 6));
        }
    }
}
