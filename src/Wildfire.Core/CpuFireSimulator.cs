namespace Wildfire.Core;

public sealed class CpuFireSimulator : IFireSimulator
{
    private readonly FireGrid _grid;
    private readonly ushort[] _cells;
    private readonly uint _seed;
    private readonly object _changesLock = new();
    private readonly Queue<FireSimChange> _queuedChanges = new();
    private readonly List<int> _candidates = [];
    private readonly List<int> _active = [];
    private readonly List<int> _nextActive = [];
    private readonly int[] _queuedGeneration;
    private readonly int[] _nextActiveGeneration;
    private readonly List<CellDelta> _deltas = [];
    private readonly List<IFireSimListener> _listeners = [];
    private int _generation;
    private int _nextActiveStamp = 1;
    private uint _tick;

    public CpuFireSimulator(int width, int height, int depth, uint seed = 1, ReadOnlySpan<ushort> initialCells = default)
    {
        if (width <= 0 || height <= 0 || depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Fire grid dimensions must be positive.");
        }

        _grid = new FireGrid(width, height, depth);
        _cells = new ushort[_grid.CellCount];
        _seed = seed;
        _queuedGeneration = new int[_grid.CellCount];
        _nextActiveGeneration = new int[_grid.CellCount];

        if (!initialCells.IsEmpty)
        {
            if (initialCells.Length != _cells.Length)
            {
                throw new ArgumentException("Initial cell buffer must match the grid cell count.", nameof(initialCells));
            }

            initialCells.CopyTo(_cells);
            SeedActiveFrontier();
        }
    }

    public int Width => _grid.Width;

    public int Height => _grid.Height;

    public int Depth => _grid.Depth;

    public ReadOnlySpan<ushort> Cells => _cells;

    public void RegisterChange(FireSimChange change)
    {
        lock (_changesLock)
        {
            _queuedChanges.Enqueue(change);
        }
    }

    public FireStepResult Tick()
    {
        _tick += 1;
        _generation += 1;
        _nextActiveStamp += 1;
        _candidates.Clear();
        _deltas.Clear();
        _nextActive.Clear();

        ApplyQueuedExternalChanges();
        AddActiveFrontierToCandidates();
        ProcessCandidates();
        NotifyListeners();
        SwapActiveSets();

        return new FireStepResult(_deltas.ToArray(), _tick);
    }

    public IDisposable Subscribe(IFireSimListener listener)
    {
        _listeners.Add(listener);
        return new Subscription(_listeners, listener);
    }

    private void SeedActiveFrontier()
    {
        for (int index = 0; index < _cells.Length; index += 1)
        {
            if (FireRules.ShouldRemainActive(_cells[index]))
            {
                EnqueueNextActiveWithNeighbors(index);
            }
        }

        SwapActiveSets();
    }

    private void ApplyQueuedExternalChanges()
    {
        FireSimChange[] changes;
        lock (_changesLock)
        {
            changes = _queuedChanges.ToArray();
            _queuedChanges.Clear();
        }

        foreach (FireSimChange change in changes)
        {
            if ((uint)change.CellIndex >= (uint)_cells.Length)
            {
                continue;
            }

            ushort oldCell = _cells[change.CellIndex];
            ushort newCell = ApplyChange(oldCell, change);
            if (newCell == oldCell)
            {
                continue;
            }

            _cells[change.CellIndex] = newCell;
            _deltas.Add(new CellDelta(change.CellIndex, oldCell, newCell));
            EnqueueCandidateWithNeighbors(change.CellIndex);
            EnqueueNextActiveWithNeighbors(change.CellIndex);
        }
    }

    private static ushort ApplyChange(ushort cell, FireSimChange change)
    {
        ushort next = change.SetCell ?? cell;

        if (change.AddFuel is { } addFuel)
        {
            next = PackedCell.SetFuel(next, Math.Min(15, PackedCell.Fuel(next) + addFuel));
        }

        if (change.AddHeat is { } addHeat)
        {
            next = PackedCell.SetHeat(next, Math.Min(15, PackedCell.Heat(next) + addHeat));
        }

        if (change.SetFuel is { } setFuel)
        {
            next = PackedCell.SetFuel(next, setFuel);
        }

        if (change.SetHeat is { } setHeat)
        {
            next = PackedCell.SetHeat(next, setHeat);
        }

        if (change.SetWater is { } setWater)
        {
            next = PackedCell.SetWater(next, setWater);
        }

        if (change.SetFlammability is { } setFlammability)
        {
            next = PackedCell.SetFlammability(next, setFlammability);
        }

        if (change.SetHeatLoss is { } setHeatLoss)
        {
            next = PackedCell.SetHeatLoss(next, setHeatLoss);
        }

        if (change.SetTerrain is { } setTerrain)
        {
            next = PackedCell.SetTerrain(next, setTerrain);
        }

        return next;
    }

    private void AddActiveFrontierToCandidates()
    {
        foreach (int index in _active)
        {
            EnqueueCandidateWithNeighbors(index);
        }
    }

    private void ProcessCandidates()
    {
        foreach (int index in _candidates)
        {
            ushort oldCell = _cells[index];
            ushort newCell = FireRules.StepCell(_grid, index, oldCell, _cells, _tick, _seed);
            if (newCell != oldCell)
            {
                _cells[index] = newCell;
                _deltas.Add(new CellDelta(index, oldCell, newCell));
                EnqueueNextActiveWithNeighbors(index);
            }

            if (FireRules.ShouldRemainActive(newCell))
            {
                EnqueueNextActiveWithNeighbors(index);
            }
        }
    }

    private void EnqueueCandidate(int index)
    {
        if ((uint)index >= (uint)_cells.Length || _queuedGeneration[index] == _generation)
        {
            return;
        }

        _queuedGeneration[index] = _generation;
        _candidates.Add(index);
    }

    private void EnqueueCandidateWithNeighbors(int index)
    {
        EnqueueCandidate(index);
        FireRules.ForEachNeighbor(_grid, index, EnqueueCandidate);
    }

    private void EnqueueNextActive(int index)
    {
        if ((uint)index >= (uint)_cells.Length || _nextActiveGeneration[index] == _nextActiveStamp)
        {
            return;
        }

        _nextActiveGeneration[index] = _nextActiveStamp;
        _nextActive.Add(index);
    }

    private void EnqueueNextActiveWithNeighbors(int index)
    {
        EnqueueNextActive(index);
        FireRules.ForEachNeighbor(_grid, index, EnqueueNextActive);
    }

    private void NotifyListeners()
    {
        CellDelta[] snapshot = _deltas.ToArray();
        foreach (IFireSimListener listener in _listeners.ToArray())
        {
            listener.OnFireSimDeltas(snapshot);
        }
    }

    private void SwapActiveSets()
    {
        _active.Clear();
        _active.AddRange(_nextActive);
    }

    private sealed class Subscription(List<IFireSimListener> listeners, IFireSimListener listener) : IDisposable
    {
        public void Dispose()
        {
            listeners.Remove(listener);
        }
    }
}
