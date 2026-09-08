using Timberborn.BaseComponentSystem;
using Timberborn.TickSystem;

namespace Wildfire.Timberborn.Beavers.Emergency;

/// <summary>Ordinary tickables precede BehaviorManager's ILateTickable in the reviewed native scheduler.</summary>
public sealed class CarryEmergencyInterrupter : TickableComponent, IAwakableComponent
{
    private WildfireCarryEmergencyExecutor _executor = null!;
    public void Awake() => _executor = GetComponent<WildfireCarryEmergencyExecutor>();
    public override void Tick() => _executor.BeforeBehaviorTick();
}
