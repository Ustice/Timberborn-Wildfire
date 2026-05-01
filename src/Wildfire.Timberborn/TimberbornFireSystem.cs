using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornFireSystem
{
    private readonly IFireSimulator _fireSimulator;

    public TimberbornFireSystem(IFireSimulator fireSimulator)
    {
        _fireSimulator = fireSimulator;
    }

    public FireStepResult Tick()
    {
        return _fireSimulator.Tick();
    }

    public void RegisterHeat(int cellIndex, byte heat)
    {
        _fireSimulator.RegisterChange(new FireSimChange(CellIndex: cellIndex, AddHeat: heat));
    }
}
