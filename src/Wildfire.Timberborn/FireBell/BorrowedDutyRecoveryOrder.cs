using Wildfire.Timberborn.Compatibility;
using Timberborn.BehaviorSystem;

namespace Wildfire.Timberborn.FireBell;

/// <summary>One verified insertion; no root replacement, fallback append or per-frame list rewrite.</summary>
internal static class BorrowedDutyRecoveryOrder
{
    internal static void RequireInstallable(BehaviorManager manager, BorrowedDutyBehavior recovery) => Prepare(manager, recovery);
    internal static void Install(BehaviorManager manager, BorrowedDutyBehavior recovery)
    {
        var (roots, index) = Prepare(manager, recovery);
        if (index >= 0) roots.Insert(index, recovery);
    }
    private static (List<RootBehavior> Roots, int Index) Prepare(BehaviorManager manager, BorrowedDutyBehavior recovery)
    {
        var roots = TimberbornAdultRecoveryRoots.Read(manager);
        int worker = TimberbornAdultRecoveryRoots.WorkerIndex(roots);
        if (RecoveryCount(roots) == 0) return (roots, InsertionIndex(roots, worker));
        if (!IsInstalled(manager, recovery)) throw new InvalidOperationException("Borrowed return was duplicated or reordered.");
        return (roots, -1);
    }
    internal static bool IsInstalled(BehaviorManager manager, BorrowedDutyBehavior recovery)
    {
        var roots = TimberbornAdultRecoveryRoots.Read(manager);
        int worker = TimberbornAdultRecoveryRoots.WorkerIndex(roots);
        int insertion = InsertionIndex(roots, worker);
        return RecoveryCount(roots) == 1 && insertion > 0 && ReferenceEquals(roots[insertion - 1], recovery);
    }
    private static int InsertionIndex(List<RootBehavior> roots, int worker)
    {
        int fertilizer = -1;
        for (int index = 0; index < roots.Count; index++)
        {
            if (roots[index] is not Fertilizer.FertilizerRecoveryRoot) continue;
            if (fertilizer >= 0 || index != worker - 1)
                throw new InvalidOperationException("Native fertilizer recovery adjacency changed.");
            fertilizer = index;
        }
        return fertilizer >= 0 ? fertilizer : worker;
    }
    private static int RecoveryCount(List<RootBehavior> roots)
    {
        int count = 0;
        for (int index = 0; index < roots.Count; index++)
            if (roots[index] is BorrowedDutyBehavior) count++;
        return count;
    }

}
