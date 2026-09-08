using Timberborn.BehaviorSystem;
using Timberborn.WorkSystem;

namespace Wildfire.Timberborn.Ash;

/// <summary>The saved running behavior lives on the worker, so demolishing its gatherer does not orphan cargo.</summary>
public sealed class AshHarvestBehavior : Behavior, IJobBehavior
{
    public override Decision Decide(BehaviorAgent agent) => Decision.ReleaseNow();

    internal Decision Own(AshHarvestExecutor executor)
    {
        var decision = Decision.ReleaseWhenFinished(executor);
        return Decision.TransferNow(this, in decision);
    }
}
