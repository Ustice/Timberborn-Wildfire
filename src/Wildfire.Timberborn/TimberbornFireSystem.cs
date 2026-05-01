using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornFireSystem
{
    private readonly IGpuFireSimulator _fireSimulator;

    public TimberbornFireSystem(IGpuFireSimulator fireSimulator)
    {
        _fireSimulator = fireSimulator;
    }

    public GpuFireStepResult Tick()
    {
        return _fireSimulator.Tick();
    }

    public void RegisterHeat(int cellIndex, byte heat)
    {
        _fireSimulator.RegisterChange(new FireSimChange(CellIndex: cellIndex, AddHeat: heat));
    }
}
