using System.Reflection;
using System.Security.Cryptography;
using Timberborn.BehaviorSystem;

namespace Wildfire.Timberborn.Fertilizer;

/// <summary>One verified insertion; no root replacement, fallback append or per-frame list rewrite.</summary>
internal static class FertilizerRecoveryOrder
{
    private static readonly Lazy<FieldInfo> Roots = new(Verify);
    private static readonly string[] Anchors =
    {
        "Timberborn.CharacterControlSystem.CharacterControlRootBehavior", "Timberborn.MortalSystem.DeadRootBehavior",
        "Timberborn.Carrying.CarryRootBehavior", "Timberborn.DeathSystem.DieRootBehavior",
        "Timberborn.BeaverContaminationSystem.ContaminateRootBehavior", "Timberborn.NeedBehaviorSystem.CriticalNeederRootBehavior",
        "Timberborn.Wandering.StrandedRootBehavior", "Timberborn.WorkSystem.WorkerRootBehavior",
        "Timberborn.NeedBehaviorSystem.NeederRootBehavior", "Timberborn.Wandering.WanderRootBehavior"
    };

    internal static void Install(BehaviorManager manager, FertilizerRecoveryRoot recovery)
    {
        var roots = (List<RootBehavior>)Roots.Value.GetValue(manager)!;
        int worker = WorkerIndex(roots);
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
        var roots = (List<RootBehavior>)Roots.Value.GetValue(manager)!;
        int worker = WorkerIndex(roots);
        return RecoveryCount(roots) == 1 && worker > 0 &&
            ReferenceEquals(roots[worker - 1], recovery);
    }
    private static int WorkerIndex(List<RootBehavior> roots)
    {
        int previous = -1, worker = -1;
        foreach (string anchor in Anchors)
        {
            int match = -1;
            for (int index = 0; index < roots.Count; index++)
            {
                if (roots[index]?.GetType().FullName != anchor) continue;
                if (match >= 0)
                    throw new InvalidOperationException("Native fertilizer recovery requires unique ordered adult roots.");
                match = index;
            }
            if (match <= previous)
                throw new InvalidOperationException("Native fertilizer recovery requires unique ordered adult roots.");
            previous = match;
            if (anchor == "Timberborn.WorkSystem.WorkerRootBehavior") worker = previous;
        }
        return worker;
    }

    private static int RecoveryCount(List<RootBehavior> roots)
    {
        int count = 0;
        for (int index = 0; index < roots.Count; index++)
            if (roots[index] is FertilizerRecoveryRoot) count++;
        return count;
    }

    private static FieldInfo Verify()
    {
        var assembly = typeof(BehaviorManager).Assembly;
        using var sha = SHA256.Create();
        foreach (var file in new[]
        {
            (assembly.Location, "d49c74ab361748bb8bd815a2fade378246224565fdbc3ed048e633f4704ba8a8"),
            (Path.Combine(Path.GetDirectoryName(assembly.Location)!, "Timberborn.BeaverBehavior.dll"), "e295fbe6d941c02093df7ebdd0d06503dad452fe9bef5b99252d98643657600e")
        })
        {
            using var stream = File.OpenRead(file.Item1);
            if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != file.Item2)
                throw new InvalidOperationException("Fertilizer recovery root ordering is not reviewed for this native build.");
        }
        var field = typeof(BehaviorManager).GetField("_rootBehaviors", BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.FieldType == typeof(List<RootBehavior>) ? field :
            throw new InvalidOperationException("Native root behavior list shape changed.");
    }
}
