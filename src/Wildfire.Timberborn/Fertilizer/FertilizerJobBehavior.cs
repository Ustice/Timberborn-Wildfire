using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.BlockSystem;
using Timberborn.InventorySystem;
using Timberborn.WorkSystem;

namespace Wildfire.Timberborn.Fertilizer;

public sealed class FertilizerBehavior : Behavior, IJobBehavior
{
    public override Decision Decide(BehaviorAgent agent) => Decision.ReleaseNow();
    internal Decision Own(FertilizerExecutor executor)
    {
        var decision = Decision.ReleaseWhenFinished(executor);
        return Decision.TransferNow(this, in decision);
    }
}

/// <summary>Inactive finite fixture offer. No designation, recurring admission or priority override.</summary>
public sealed class FertilizerWorkplaceBehavior : WorkplaceBehavior, IAwakableComponent
{
    private Workplace _workplace = null!;
    private (BlockObject Plant, Inventory Source, byte Limit)? _offer;
    public void Awake() => _workplace = GetComponent<Workplace>();
    internal void ArmSingleOffer(BlockObject plant, Inventory source, byte limit)
    {
        if (_offer.HasValue) throw new InvalidOperationException("A fertilizer offer is already armed.");
        if (!plant || !source || limit is < 1 or > 3) throw new ArgumentException("A live plant/source and limit1..3 are required.");
        _offer = (plant, source, limit);
    }
    public override Decision Decide(BehaviorAgent agent)
    {
        if (!_offer.HasValue || !ReferenceEquals(agent.GetComponent<Worker>().Workplace, _workplace))
            return Decision.ReleaseNow();
        var executor = agent.GetComponent<FertilizerExecutor>();
        var behavior = agent.GetComponent<FertilizerBehavior>();
        var offer = _offer.Value;
        if (executor is null || behavior is null || !executor.TryLaunch(_workplace, offer.Plant, offer.Source, offer.Limit))
            return Decision.ReleaseNow();
        _offer = null;
        return behavior.Own(executor);
    }
}
