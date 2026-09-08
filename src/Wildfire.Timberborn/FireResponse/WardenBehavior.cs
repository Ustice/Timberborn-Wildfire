using Timberborn.BehaviorSystem;
using Timberborn.WorkSystem;

namespace Wildfire.Timberborn.FireResponse;

/// <summary>The saved behavior stays with the worker when its station is demolished during a sortie.</summary>
public sealed class WardenBehavior : Behavior, IJobBehavior
{
    public override Decision Decide(BehaviorAgent agent) => Decision.ReleaseNow();

    internal Decision Own(WardenExecutor executor)
    {
        var decision = Decision.ReleaseWhenFinished(executor);
        return Decision.TransferNow(this, in decision);
    }
}
