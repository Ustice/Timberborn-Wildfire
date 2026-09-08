using Wildfire.Core;
using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeMaterialBaselineTests
{
    private static readonly FireGrid Grid = new(6, 1, 1);
    private static readonly Guid Tree = new("00000000-0000-0000-0000-000000000011");
    private static readonly Guid Building = new("00000000-0000-0000-0000-000000000022");
    private static readonly FireSimBaselineDefinition[] Definitions =
        [FireSimBaselineDefinition.Empty, FireSimBaselineDefinition.SolidTerrain, FireSimBaselineDefinition.OpenSoil,
         FireSimBaselineDefinition.Water, FireSimBaselineDefinition.Badwater];

    [Fact]
    public void EveryUnownedDefinitionHasCoherentProfileWithoutInitialWetnessReplay()
    {
        var values = Definitions.Select((definition, cell) => new KeyValuePair<int, FireSimBaselineDefinition>(cell, definition)).ToArray();
        var baseline = new TimberbornMaterialBaseline(Grid, values);
        var registry = new TimberbornNativeMaterialRegistry(baseline);
        values[4] = new(4, FireSimBaselineDefinition.Empty); // Caller mutation cannot change the compiled baseline.
        for (int cell = 0; cell < Definitions.Length; cell++)
        {
            var resolved = registry.ResolveCell(cell);
            Assert.Null(resolved.Owner);
            Assert.Empty(resolved.Contributors);
            Assert.Equal(Definitions[cell].PackedMaterial, (uint)resolved.PackedDefinition);
            Assert.Equal(Definitions[cell].MaterialClass, resolved.Profile.MaterialClass);
            Assert.Equal(0, PackedCell.Water(resolved.PackedDefinition));
            Assert.Equal(Definitions[cell].CompanionMaterial, WildfireMaterialFieldState.FromMaterialProfile(resolved.Profile).Pack());
            var request = registry.CreateBaselineRequest(cell, default);
            Assert.Equal(Definitions[cell].PackedMaterial, request.PackedMaterial);
            Assert.Equal(Definitions[cell].CompanionMaterial, request.CompanionMaterial);
        }
        Assert.Equal(FireSimBaselineDefinition.Empty, baseline.GetCell(5));
    }

    [Fact]
    public void HiddenLowerContributorWinsBeforeExactOriginalBaselineCanBeRevealed()
    {
        foreach (var definition in Definitions)
        {
            var registry = new TimberbornNativeMaterialRegistry(new TimberbornMaterialBaseline(Grid,
                [new(0, definition)]));
            registry.Reconcile([Projection(Tree, TimberbornMaterialPart.Tree("Pine")),
                Projection(Building, TimberbornMaterialPart.Building("LumberMill.Folktails"))], []);
            var both = registry.ResolveCell(0);
            Assert.Equal(Building, both.Owner!.Value.EntityId);
            var lower = both.Contributors.Single(contributor => contributor.Owner.EntityId == Tree).Owner;
            Assert.Throws<InvalidOperationException>(() => registry.CreateBaselineRequest(0, default));
            registry.Reconcile([], [Building]);
            Assert.Equal(lower, registry.ResolveCell(0).Owner);
            Assert.Throws<InvalidOperationException>(() => registry.CreateBaselineRequest(0, new(lower.TargetId, lower.SlotId)));
            registry.Reconcile([], [Tree]);
            var request = registry.CreateBaselineRequest(0, new(lower.TargetId, lower.SlotId));
            Assert.Equal(definition.PackedMaterial, request.PackedMaterial);
            Assert.Equal(definition.CompanionMaterial, request.CompanionMaterial);
            Assert.Equal(2, registry.CaptureBindings().Entities.Count);
            Assert.True(registry.IsSlotBound(lower.TargetId, lower.SlotId));
        }
    }

    [Fact]
    public void InvalidBaselineAndRejectedReconcileDoNotChangeDefinitionOrContributors()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimberbornMaterialBaseline(Grid, [new(6, FireSimBaselineDefinition.Water)]));
        Assert.Throws<ArgumentException>(() => new TimberbornMaterialBaseline(Grid,
            [new(0, FireSimBaselineDefinition.Water), new(0, FireSimBaselineDefinition.OpenSoil)]));
        var registry = new TimberbornNativeMaterialRegistry(new TimberbornMaterialBaseline(Grid, [new(0, FireSimBaselineDefinition.OpenSoil)]));
        registry.Reconcile([Projection(Building, TimberbornMaterialPart.Building("LumberMill.Folktails"))], []);
        var original = registry.ResolveCell(0).Owner;
        Assert.Throws<InvalidOperationException>(() => registry.Reconcile([Projection(Tree, TimberbornMaterialPart.StoredGood("Log"))], []));
        Assert.Equal(original, registry.ResolveCell(0).Owner);
        Assert.Single(registry.CaptureBindings().Entities);
        registry.Reconcile([], [Building]);
        Assert.Equal(FireSimBaselineDefinition.OpenSoil.CompanionMaterial, registry.CreateBaselineRequest(0, default).CompanionMaterial);
    }

    private static TimberbornMaterialProjection Projection(Guid id, params TimberbornMaterialPart[] parts) =>
        new(id, [new(new(0, 0, 0), 0)], parts);
}
