using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.BlockSystem;
using Timberborn.Gathering;
using Timberborn.InventorySystem;
using Timberborn.SimpleOutputBuildings;
using Timberborn.TemplateSystem;
using Timberborn.WorkSystem;
using UnityEngine;

namespace Wildfire.Timberborn.Ash;

public sealed class TimberbornFertileAshFieldWorkplaceBehavior : WorkplaceBehavior, IAwakableComponent, IUpdatableComponent
{
    private readonly TimberbornFireRuntime _runtime;
    private readonly TimberbornAshWorkplaceOrder _order = new();
    private GatherablePrioritizer _prioritizer = null!;
    private Inventory _inventory = null!;
    private Workplace _workplace = null!;
    private BlockObject _block = null!;
    public TimberbornFertileAshFieldWorkplaceBehavior(TimberbornFireRuntime runtime) => _runtime = runtime;
    public void Awake()
    {
        _prioritizer = GetComponent<GatherablePrioritizer>();
        _inventory = GetComponent<SimpleOutputInventory>().Inventory;
        _workplace = GetComponent<Workplace>();
        _block = GetComponent<BlockObject>();
    }
    public void Update() => _order.EnsureFirst(_workplace, this);
    public override Decision Decide(BehaviorAgent agent)
    {
        if (_prioritizer.PrioritizedGatherable?.GetSpec<TemplateSpec>().TemplateName != TimberbornFireRuntime.FertileAshFieldGatherableTemplateName)
            return Decision.ReleaseNow();
        if (!_workplace.Enabled || !_inventory.Enabled || !_inventory.HasUnreservedCapacity(AshHarvestCargo.Unit)) return Decision.ReleaseNextTick();
        var occupied = _block.PositionedBlocks?.GetOccupiedCoordinates().ToArray();
        if (occupied is null || occupied.Length == 0) return Decision.ReleaseNextTick();
        Vector3Int center = occupied.OrderBy(position => position.x).ThenBy(position => position.y).ThenBy(position => position.z).ElementAt(occupied.Length / 2);
        if (!_runtime.TryFindFertileAshFieldHarvestTarget(center, out var target)) return Decision.ReleaseNextTick();
        var executor = agent.GetComponent<AshHarvestExecutor>();
        return executor.TryLaunch(_workplace, _inventory, target)
            ? agent.GetComponent<AshHarvestBehavior>().Own(executor)
            : Decision.ReleaseNextTick();
    }
}
