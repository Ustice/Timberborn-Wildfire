using Timberborn.BehaviorSystem;

namespace Wildfire.Timberborn.FireBell;

/// <summary>Intentionally not IJobBehavior: the original employer must not count absent labor as production.</summary>
public sealed class BorrowedDutyBehavior : Behavior
{
    public override Decision Decide(BehaviorAgent agent) => Decision.ReleaseNow();
    internal Decision Own(BorrowedDutyExecutor executor)
    {
        var decision = Decision.ReleaseWhenFinished(executor);
        return Decision.TransferNow(this, in decision);
    }
}
