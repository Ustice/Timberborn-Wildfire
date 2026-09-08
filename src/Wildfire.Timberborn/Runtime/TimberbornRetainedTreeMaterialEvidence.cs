using Timberborn.Cutting;
using Timberborn.EntitySystem;
using Timberborn.Forestry;
using Timberborn.Yielding;

namespace Wildfire.Timberborn.Runtime;

/// <summary>Current native composition proof for preserving already-known tree material, never permission to create fuel.</summary>
internal static class TimberbornRetainedTreeMaterialEvidence
{
    private static readonly Type DeathRemoverSpec = typeof(Cuttable).Assembly.GetType(
        "Timberborn.Cutting.DeadCuttableYieldRemoverSpec", throwOnError: true)!;
    private static readonly Type DeathRemover = typeof(Cuttable).Assembly.GetType(
        "Timberborn.Cutting.DeadCuttableYieldRemover", throwOnError: true)!;

    internal static bool Observe(EntityComponent entity, TimberbornInitialMaterialBody body)
    {
        if (body.Shape != TimberbornInitialBodyShape.Tree ||
            !entity.TryGetComponent<TreeComponent>(out var tree) ||
            !entity.TryGetComponent<Cuttable>(out var cuttable) || cuttable.RemoveOnCut)
            return false;
        // Specs and their runtime decorators are both in the full native component list. Missing a
        // decorator alone cannot disguise a configured death-time yield removal policy.
        if (entity.AllComponents.Any(component => DeathRemoverSpec.IsInstanceOfType(component) || DeathRemover.IsInstanceOfType(component)))
            return false;
        var yielder = cuttable.Yielder;
        if (yielder is null || !ReferenceEquals(cuttable.YielderSpec, yielder.YielderSpec) ||
            !ReferenceEquals(tree.GetComponent<EntityComponent>(), entity) ||
            !ReferenceEquals(cuttable.GetComponent<EntityComponent>(), entity) ||
            !ReferenceEquals(yielder.GetComponent<EntityComponent>(), entity))
            return false;
        var named = entity.GetComponentsAllocating<Yielder>().Where(value => value.ComponentName == yielder.ComponentName).ToArray();
        if (named.Length != 1 || !ReferenceEquals(named[0], yielder)) return false;
        var captured = body.Yields.Where(value => value.Role == TimberbornCapturedYieldRole.Cuttable).ToArray();
        return captured.Length == 1 && !captured[0].RemoveOnCut &&
            captured[0].ComponentName == yielder.ComponentName &&
            captured[0].DeclaredGoodId == yielder.YielderSpec.Yield.Id &&
            captured[0].DeclaredAmount == yielder.YielderSpec.Yield.Amount;
    }
}
