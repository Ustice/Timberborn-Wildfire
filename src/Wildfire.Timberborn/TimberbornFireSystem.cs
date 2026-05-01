using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornFireSystem
{
    private readonly IGpuFireSimulator _fireSimulator;
    private readonly TimberbornFireCellMapper _cellMapper;

    public TimberbornFireSystem(IGpuFireSimulator fireSimulator)
        : this(fireSimulator, new TimberbornFireCellMapper())
    {
    }

    public TimberbornFireSystem(IGpuFireSimulator fireSimulator, TimberbornFireCellMapper cellMapper)
    {
        if (fireSimulator is null)
        {
            throw new ArgumentNullException(nameof(fireSimulator));
        }

        if (cellMapper is null)
        {
            throw new ArgumentNullException(nameof(cellMapper));
        }

        _fireSimulator = fireSimulator;
        _cellMapper = cellMapper;
    }

    public GpuFireStepResult Tick()
    {
        return _fireSimulator.Tick();
    }

    public void RegisterHeat(int cellIndex, byte heat)
    {
        _fireSimulator.RegisterChange(new FireSimChange(CellIndex: cellIndex, AddHeat: heat));
    }

    public void RegisterMappedCellChanges(FireGrid grid, IEnumerable<TimberbornCellSource> sources)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        _cellMapper.CreateSetCellChanges(grid, sources)
            .ToList()
            .ForEach(_fireSimulator.RegisterChange);
    }
}
