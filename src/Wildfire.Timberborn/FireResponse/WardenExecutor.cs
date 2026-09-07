using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.EntitySystem;
using Timberborn.Persistence;
using Timberborn.WorldPersistence;

namespace Wildfire.Timberborn.FireResponse;

/// <summary>Registered on the character so BehaviorManager can resolve this executor after reload.</summary>
public sealed class WardenExecutor : BaseComponent, IExecutor, IAwakableComponent
{
    private static readonly ComponentKey Key = new("Wildfire.WardenExecutor");
    private static readonly PropertyKey<int> PhaseKey = new("Phase");
    private int _phase;
    public string Status { get; private set; } = "Responder integration pending";
    public void Awake() { }
    public bool TryLaunch(WardenStation station) => false;
    public ExecutorStatus Tick(float deltaTimeInHours) => ExecutorStatus.Failure;
    public void Save(IEntitySaver saver) => saver.GetComponent(Key).Set(PhaseKey, _phase);
    public void Load(IEntityLoader loader)
    {
        var state = loader.GetComponent(Key);
        _phase = state.Has(PhaseKey) ? state.Get(PhaseKey) : 0;
    }
}
