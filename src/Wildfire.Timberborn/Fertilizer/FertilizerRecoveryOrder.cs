using Wildfire.Timberborn.Compatibility;
using Timberborn.BehaviorSystem;

namespace Wildfire.Timberborn.Fertilizer;

/// <summary>One verified insertion; no root replacement, fallback append or per-frame list rewrite.</summary>
internal static class FertilizerRecoveryOrder
{
    internal static void Install(BehaviorManager manager, FertilizerRecoveryRoot recovery)
    {
        var roots = TimberbornAdultRecoveryRoots.Read(manager);
        int worker = TimberbornAdultRecoveryRoots.WorkerIndex(roots);
        if (RecoveryCount(roots) != 0)
        {
            if (!IsInstalled(manager, recovery))
                throw new InvalidOperationException("Fertilizer recovery was duplicated or reordered.");
            return;
        }
        roots.Insert(worker, recovery); // Foreign entries keep their identities and relative order.
    }
    internal static bool IsInstalled(BehaviorManager manager, FertilizerRecoveryRoot recovery)
    {
        var roots = TimberbornAdultRecoveryRoots.Read(manager);
        int worker = TimberbornAdultRecoveryRoots.WorkerIndex(roots);
        return RecoveryCount(roots) == 1 && worker > 0 &&
            ReferenceEquals(roots[worker - 1], recovery);
    }
    private static int RecoveryCount(List<RootBehavior> roots)
    {
        int count = 0;
        for (int index = 0; index < roots.Count; index++)
            if (roots[index] is FertilizerRecoveryRoot) count++;
        return count;
    }

}
