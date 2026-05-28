using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class TimberbornTreeBurnConsequenceTests
{
    [Fact]
    public void TreeBurnConsequenceReducesCuttableYieldFromAppliedBurnDamage()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            TreeDescriptor("Tree.Pine", "Log", amount: 10));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("tree-pine-1", "Tree.Pine", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingTreeBurnConsequenceApi treeApi = new();
        TimberbornTreeBurnConsequenceSink treeSink = new(burnDamageService, treeApi);

        TimberbornBurnDamageApplySummary damageSummary =
            burnDamageService.ApplyDamage(21, [Decision(0, oldFuel: 15, newFuel: 3)]);
        TimberbornTreeBurnConsequenceSummary treeSummary =
            treeSink.ApplyConsequences(21, [Decision(0, oldFuel: 15, newFuel: 3)]);

        Assert.Equal(
            [
                TimberbornTreeBurnConsequenceKind.ReduceYield,
                TimberbornTreeBurnConsequenceKind.KillTree,
                TimberbornTreeBurnConsequenceKind.MarkBurnedVisual,
            ],
            treeApi.Consequences.Select(static consequence => consequence.Kind).ToArray());
        TimberbornTreeBurnConsequence consequence = treeApi.Consequences.First();
        Assert.Equal(TimberbornTreeBurnConsequenceKind.ReduceYield, consequence.Kind);
        Assert.Equal(1, consequence.YieldLost);
        Assert.Equal(9, consequence.RemainingYield);
        Assert.Equal(12, damageSummary.TotalDamageApplied);
        Assert.Equal(1, treeSummary.ConsideredTreeTargetCount);
        Assert.Equal(1, treeSummary.BurnableTreeTargetCount);
        Assert.Equal(1, treeSummary.YieldLost);
        Assert.Equal(1, treeSummary.KilledTreeCount);
        Assert.Equal(1, treeSummary.VisualStateUpdateCount);
    }

    [Fact]
    public void TreeBurnConsequenceDriesTreeFromMoistureEvaporationWithoutFuelLoss()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            TreeDescriptor("Tree.Pine", "Log", amount: 10));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("tree-pine-1", "Tree.Pine", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingTreeBurnConsequenceApi treeApi = new();
        TimberbornTreeBurnConsequenceSink treeSink = new(burnDamageService, treeApi);
        TimberbornFireCellDeltaDecision waterDrop = Decision(0, oldFuel: 15, newFuel: 15, oldWater: 3, newWater: 0);

        burnDamageService.ApplyDamage(21, [waterDrop]);
        TimberbornTreeBurnConsequenceSummary treeSummary = treeSink.ApplyConsequences(21, [waterDrop]);

        Assert.Equal([TimberbornTreeBurnConsequenceKind.DryTree], treeApi.Consequences
            .Select(static consequence => consequence.Kind)
            .ToArray());
        Assert.Equal(1, treeSummary.ConsideredTreeTargetCount);
        Assert.Equal(0, treeSummary.YieldLost);
        Assert.Equal(0, treeSummary.KilledTreeCount);
    }

    [Fact]
    public void TreeBurnConsequenceDoesNotDryDeadTreeFromLaterMoistureEvaporation()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            TreeDescriptor("Tree.Pine", "Log", amount: 1));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("tree-pine-1", "Tree.Pine", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingTreeBurnConsequenceApi treeApi = new();
        TimberbornTreeBurnConsequenceSink treeSink = new(burnDamageService, treeApi);
        TimberbornFireCellDeltaDecision burnStep = Decision(0, oldFuel: 15, newFuel: 9);
        TimberbornFireCellDeltaDecision waterDrop = Decision(0, oldFuel: 9, newFuel: 9, oldWater: 3, newWater: 1);

        burnDamageService.ApplyDamage(22, [burnStep]);
        treeSink.ApplyConsequences(22, [burnStep]);
        burnDamageService.ApplyDamage(23, [waterDrop]);
        TimberbornTreeBurnConsequenceSummary waterSummary = treeSink.ApplyConsequences(23, [waterDrop]);

        Assert.Contains(
            treeApi.Consequences,
            static consequence => consequence.Kind == TimberbornTreeBurnConsequenceKind.KillTree);
        Assert.DoesNotContain(
            treeApi.Consequences,
            static consequence => consequence.Tick == 23 &&
                consequence.Kind == TimberbornTreeBurnConsequenceKind.DryTree);
        Assert.Equal(0, waterSummary.KilledTreeCount);
    }

    [Fact]
    public void SameSpecTreeRegistrationsKeepPerTargetYieldCapacity()
    {
        FireGrid grid = new(2, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService();
        TimberbornBurnDamageDescriptor oneLogPine = TreeDescriptor("Tree.Pine", "Log", amount: 1);
        TimberbornBurnDamageDescriptor threeLogPine = TreeDescriptor("Tree.Pine", "Log", amount: 3);
        burnDamageService.RegisterTargets(
            grid,
            [
                Registration(
                    "tree-pine-depleted",
                    "Tree.Pine",
                    [new TimberbornCellCoordinates(0, 0, 0)],
                    oneLogPine),
                Registration(
                    "tree-pine-full",
                    "Tree.Pine",
                    [new TimberbornCellCoordinates(1, 0, 0)],
                    threeLogPine),
            ]);
        RecordingTreeBurnConsequenceApi treeApi = new();
        TimberbornTreeBurnConsequenceSink treeSink = new(burnDamageService, treeApi);
        TimberbornFireCellDeltaDecision[] decisions =
        [
            Decision(grid.ToIndex(0, 0, 0), oldFuel: 15, newFuel: 3),
            Decision(grid.ToIndex(1, 0, 0), oldFuel: 15, newFuel: 3),
        ];

        burnDamageService.ApplyDamage(22, decisions);
        TimberbornTreeBurnConsequenceSummary treeSummary = treeSink.ApplyConsequences(22, decisions);

        TimberbornBurnDamageTargetState depletedTree =
            burnDamageService.States[new TimberbornBurnDamageTargetKey("tree-pine-depleted")];
        TimberbornBurnDamageTargetState fullTree =
            burnDamageService.States[new TimberbornBurnDamageTargetKey("tree-pine-full")];
        Assert.Equal(12, depletedTree.DamageCapacity);
        Assert.Equal(36, fullTree.DamageCapacity);
        Assert.True(depletedTree.IsFullyDamaged);
        Assert.False(fullTree.IsFullyDamaged);
        Assert.Equal(2, treeSummary.YieldLost);
        Assert.Equal(2, treeSummary.KilledTreeCount);
        Assert.Contains(
            treeApi.Consequences,
            static consequence => consequence.TargetKey.StableId == "tree-pine-full" &&
                consequence.Kind == TimberbornTreeBurnConsequenceKind.ReduceYield &&
                consequence.RemainingYield == 2);
    }

    [Fact]
    public void FullyBurnedTreeRequestsDeathAndBurnedVisualThroughNativeBoundary()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            TreeDescriptor("Tree.Maple", "Log", amount: 1));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("tree-maple-1", "Tree.Maple", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingTreeBurnConsequenceApi treeApi = new();
        TimberbornTreeBurnConsequenceSink treeSink = new(burnDamageService, treeApi);

        burnDamageService.ApplyDamage(22, [Decision(0, oldFuel: 15, newFuel: 0)]);
        TimberbornTreeBurnConsequenceSummary summary =
            treeSink.ApplyConsequences(22, [Decision(0, oldFuel: 15, newFuel: 0)]);

        Assert.Equal(
            [
                TimberbornTreeBurnConsequenceKind.ReduceYield,
                TimberbornTreeBurnConsequenceKind.KillTree,
                TimberbornTreeBurnConsequenceKind.MarkBurnedLeftover,
            ],
            treeApi.Consequences.Select(static consequence => consequence.Kind).ToArray());
        Assert.Equal(1, summary.YieldLost);
        Assert.Equal(1, summary.KilledTreeCount);
        Assert.Equal(1, summary.VisualStateUpdateCount);
        Assert.Equal(0, summary.FailedConsequenceCount);
    }

    [Fact]
    public void TreeThatFallsBelowBurnedFuelThresholdImmediatelyDoesNotEnterDryingState()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            TreeDescriptor("Tree.Pine", "Log", amount: 1));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("tree-pine-1", "Tree.Pine", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingTreeBurnConsequenceApi treeApi = new();
        TimberbornTreeBurnConsequenceSink treeSink = new(burnDamageService, treeApi);

        TimberbornFireCellDeltaDecision burnStep = Decision(0, oldFuel: 15, newFuel: 9);
        burnDamageService.ApplyDamage(22, [burnStep]);
        TimberbornTreeBurnConsequenceSummary summary = treeSink.ApplyConsequences(22, [burnStep]);

        Assert.DoesNotContain(
            treeApi.Consequences,
            static consequence => consequence.Kind == TimberbornTreeBurnConsequenceKind.DryTree);
        Assert.Contains(
            treeApi.Consequences,
            static consequence => consequence.Kind == TimberbornTreeBurnConsequenceKind.KillTree);
        Assert.Equal(1, summary.KilledTreeCount);
    }

    [Fact]
    public void FuelDepletedTreeRequestsBurnedLeftoverEvenBeforeYieldDamageThreshold()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            TreeDescriptor("Tree.Pine", "Log", amount: 10));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("tree-pine-1", "Tree.Pine", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingTreeBurnConsequenceApi treeApi = new();
        TimberbornTreeBurnConsequenceSink treeSink = new(burnDamageService, treeApi);
        TimberbornFireCellDeltaDecision spentFuel = Decision(0, oldFuel: 1, newFuel: 0);

        burnDamageService.ApplyDamage(23, [spentFuel]);
        TimberbornTreeBurnConsequenceSummary summary = treeSink.ApplyConsequences(23, [spentFuel]);

        Assert.DoesNotContain(
            treeApi.Consequences,
            static consequence => consequence.Kind == TimberbornTreeBurnConsequenceKind.DryTree);
        Assert.Contains(
            treeApi.Consequences,
            static consequence => consequence.Kind == TimberbornTreeBurnConsequenceKind.KillTree);
        Assert.Contains(
            treeApi.Consequences,
            static consequence => consequence.Kind == TimberbornTreeBurnConsequenceKind.MarkBurnedLeftover);
        Assert.DoesNotContain(
            treeApi.Consequences,
            static consequence => consequence.Kind == TimberbornTreeBurnConsequenceKind.MarkBurnedVisual);
        Assert.Equal(1, summary.KilledTreeCount);
        Assert.Equal(1, summary.VisualStateUpdateCount);
    }

    [Fact]
    public void TreeSwitchesToBurnedDeadOnlyWhenPackedFuelFallsBelowEightyPercent()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            TreeDescriptor("Tree.Pine", "Log", amount: 10));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("tree-pine-1", "Tree.Pine", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingTreeBurnConsequenceApi treeApi = new();
        TimberbornTreeBurnConsequenceSink treeSink = new(burnDamageService, treeApi);
        TimberbornFireCellDeltaDecision atThreshold =
            Decision(0, oldFuel: 15, newFuel: 12, oldWater: 3, newWater: 3);
        TimberbornFireCellDeltaDecision belowThreshold =
            Decision(0, oldFuel: 12, newFuel: 11, oldWater: 3, newWater: 3);

        burnDamageService.ApplyDamage(30, [atThreshold]);
        TimberbornTreeBurnConsequenceSummary atThresholdSummary = treeSink.ApplyConsequences(30, [atThreshold]);
        burnDamageService.ApplyDamage(31, [belowThreshold]);
        TimberbornTreeBurnConsequenceSummary belowThresholdSummary = treeSink.ApplyConsequences(31, [belowThreshold]);

        Assert.Equal(0, atThresholdSummary.KilledTreeCount);
        Assert.DoesNotContain(
            treeApi.Consequences,
            static consequence => consequence.Tick == 30 &&
                consequence.Kind == TimberbornTreeBurnConsequenceKind.KillTree);
        Assert.Equal(1, belowThresholdSummary.KilledTreeCount);
        Assert.Contains(
            treeApi.Consequences,
            static consequence => consequence.Tick == 31 &&
                consequence.Kind == TimberbornTreeBurnConsequenceKind.KillTree &&
                consequence.DamageTaken == 4 &&
                consequence.DamageCapacity == 120);
        Assert.Contains(
            treeApi.Consequences,
            static consequence => consequence.Tick == 31 &&
                consequence.Kind == TimberbornTreeBurnConsequenceKind.MarkBurnedVisual);
    }

    [Fact]
    public void MultiCellAndVerticalTreeFootprintSuppressesDuplicateTreeConsequences()
    {
        FireGrid grid = new(2, 2, 2);
        TimberbornBurnDamageService burnDamageService = CreateService(
            TreeDescriptor("Tree.Oak", "Log", amount: 2));
        burnDamageService.RegisterTargets(
            grid,
            [Registration(
                "tree-oak-1",
                "Tree.Oak",
                [
                    new TimberbornCellCoordinates(0, 0, 0),
                    new TimberbornCellCoordinates(1, 0, 0),
                    new TimberbornCellCoordinates(0, 1, 0),
                    new TimberbornCellCoordinates(0, 0, 1),
                ])]);
        RecordingTreeBurnConsequenceApi treeApi = new();
        TimberbornTreeBurnConsequenceSink treeSink = new(burnDamageService, treeApi);
        TimberbornFireCellDeltaDecision[] decisions =
        [
            Decision(grid.ToIndex(0, 0, 0), oldFuel: 15, newFuel: 3),
            Decision(grid.ToIndex(1, 0, 0), oldFuel: 15, newFuel: 3),
            Decision(grid.ToIndex(0, 1, 0), oldFuel: 15, newFuel: 3),
            Decision(grid.ToIndex(0, 0, 1), oldFuel: 15, newFuel: 3),
        ];

        burnDamageService.ApplyDamage(23, decisions);
        TimberbornTreeBurnConsequenceSummary summary = treeSink.ApplyConsequences(23, decisions);

        Assert.Equal(
            [
                TimberbornTreeBurnConsequenceKind.ReduceYield,
                TimberbornTreeBurnConsequenceKind.KillTree,
                TimberbornTreeBurnConsequenceKind.MarkBurnedVisual,
            ],
            treeApi.Consequences.Select(static consequence => consequence.Kind).ToArray());
        Assert.Equal(1, summary.ConsideredTreeTargetCount);
        Assert.Equal(3, summary.DuplicateCellSuppressedCount);
        Assert.Equal(1, summary.YieldLost);
    }

    [Fact]
    public void CropTargetsStayOutsideTreeBurnConsequences()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            CropDescriptor("Crop.Carrot", "Carrot", amount: 2));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("crop-carrot-1", "Crop.Carrot", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingTreeBurnConsequenceApi treeApi = new();
        TimberbornTreeBurnConsequenceSink treeSink = new(burnDamageService, treeApi);

        burnDamageService.ApplyDamage(24, [Decision(0, oldFuel: 10, newFuel: 0)]);
        TimberbornTreeBurnConsequenceSummary summary =
            treeSink.ApplyConsequences(24, [Decision(0, oldFuel: 10, newFuel: 0)]);

        Assert.Empty(treeApi.Consequences);
        Assert.Equal(0, summary.ConsideredTreeTargetCount);
        Assert.Equal(0, summary.YieldLost);
    }

    [Fact]
    public void NonBurnableAndUnknownTreeTargetsFailClosed()
    {
        FireGrid grid = new(2, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            new TimberbornBurnDamageDescriptor(
                "Tree.Decorative",
                TimberbornBurnDamageTargetKind.Tree,
                TimberbornBurnMaterialKind.NonBurnable,
                resourceYields: [new TimberbornBurnDamageResourceStack("Log", 1)]),
            TreeDescriptor("Tree.Modded", "MysteryLog", amount: 1));
        burnDamageService.RegisterTargets(
            grid,
            [
                Registration("tree-decorative-1", "Tree.Decorative", [new TimberbornCellCoordinates(0, 0, 0)]),
                Registration("tree-modded-1", "Tree.Modded", [new TimberbornCellCoordinates(1, 0, 0)]),
            ]);
        RecordingTreeBurnConsequenceApi treeApi = new();
        TimberbornTreeBurnConsequenceSink treeSink = new(burnDamageService, treeApi);

        TimberbornFireCellDeltaDecision[] decisions =
        [
            Decision(0, oldFuel: 10, newFuel: 0),
            Decision(1, oldFuel: 10, newFuel: 0),
        ];
        burnDamageService.ApplyDamage(25, decisions);
        TimberbornTreeBurnConsequenceSummary summary = treeSink.ApplyConsequences(25, decisions);

        Assert.Empty(treeApi.Consequences);
        Assert.Equal(2, summary.ConsideredTreeTargetCount);
        Assert.Equal(1, summary.UnknownCuttableResourceCount);
        Assert.Equal(1, summary.NonBurnableTreeTargetCount);
        Assert.Equal(0, summary.YieldLost);
    }

    [Fact]
    public void UnavailableTreeApiFailsLoudly()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            TreeDescriptor("Tree.Birch", "Log", amount: 1));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("tree-birch-1", "Tree.Birch", [new TimberbornCellCoordinates(0, 0, 0)])]);
        TimberbornTreeBurnConsequenceSink treeSink = new(
            burnDamageService,
            UnavailableTimberbornTreeBurnConsequenceApi.Instance);

        burnDamageService.ApplyDamage(26, [Decision(0, oldFuel: 15, newFuel: 0)]);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            treeSink.ApplyConsequences(26, [Decision(0, oldFuel: 15, newFuel: 0)]));

        Assert.Contains("Tree burn consequence API is unavailable", exception.Message);
    }

    [Fact]
    public void BurnedLeftoverOnlyAppliesWhenPackedFuelReachesZero()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            TreeDescriptor("Tree.Pine", "Log", amount: 10));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("tree-pine-1", "Tree.Pine", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingTreeBurnConsequenceApi treeApi = new();
        TimberbornTreeBurnConsequenceSink treeSink = new(burnDamageService, treeApi);

        TimberbornFireCellDeltaDecision burnStep = Decision(0, oldFuel: 15, newFuel: 3);
        TimberbornFireCellDeltaDecision spentFuel = Decision(0, oldFuel: 3, newFuel: 0);

        burnDamageService.ApplyDamage(33, [burnStep]);
        TimberbornTreeBurnConsequenceSummary partialSummary = treeSink.ApplyConsequences(33, [burnStep]);
        Enumerable.Range(34, 8)
            .Select(tick =>
            {
                burnDamageService.ApplyDamage((uint)tick, [burnStep]);
                return treeSink.ApplyConsequences((uint)tick, [burnStep]);
            })
            .ToArray();
        burnDamageService.ApplyDamage(42, [spentFuel]);
        TimberbornTreeBurnConsequenceSummary fullSummary = treeSink.ApplyConsequences(42, [spentFuel]);

        Assert.DoesNotContain(
            treeApi.Consequences,
            static consequence => consequence.Tick < 42 &&
                consequence.Kind == TimberbornTreeBurnConsequenceKind.MarkBurnedLeftover);
        Assert.Contains(
            treeApi.Consequences,
            static consequence => consequence.Tick == 42 &&
                consequence.Kind == TimberbornTreeBurnConsequenceKind.MarkBurnedLeftover &&
                consequence.RemainingYield == 0);
        Assert.Contains(
            treeApi.Consequences,
            static consequence => consequence.Tick == 33 &&
                consequence.Kind == TimberbornTreeBurnConsequenceKind.MarkBurnedVisual);
        Assert.Equal(1, partialSummary.VisualStateUpdateCount);
        Assert.Equal(1, fullSummary.VisualStateUpdateCount);
    }

    [Fact]
    public void FuelDepletedTreeFailsWhenNativeLeftoverRefreshFails()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            TreeDescriptor("Tree.Pine", "Log", amount: 10));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("tree-pine-1", "Tree.Pine", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingTreeBurnConsequenceApi treeApi = new(static consequence =>
            consequence.Kind == TimberbornTreeBurnConsequenceKind.MarkBurnedLeftover
                ? new TimberbornTreeBurnConsequenceResult(Applied: false, Failed: true)
                : new TimberbornTreeBurnConsequenceResult(Applied: true, Failed: false));
        TimberbornTreeBurnConsequenceSink treeSink = new(burnDamageService, treeApi);
        TimberbornFireCellDeltaDecision burnStep = Decision(0, oldFuel: 15, newFuel: 3);
        TimberbornFireCellDeltaDecision spentFuel = Decision(0, oldFuel: 3, newFuel: 0);

        burnDamageService.ApplyDamage(33, [burnStep]);
        TimberbornTreeBurnConsequenceSummary partialSummary = treeSink.ApplyConsequences(33, [burnStep]);
        burnDamageService.ApplyDamage(42, [spentFuel]);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            treeSink.ApplyConsequences(42, [spentFuel]));

        Assert.Contains(
            treeApi.Consequences,
            static consequence => consequence.Tick == 33 &&
                consequence.Kind == TimberbornTreeBurnConsequenceKind.KillTree);
        Assert.Contains(
            treeApi.Consequences,
            static consequence => consequence.Tick == 42 &&
                consequence.Kind == TimberbornTreeBurnConsequenceKind.MarkBurnedLeftover);
        Assert.Equal(1, partialSummary.KilledTreeCount);
        Assert.Equal(1, partialSummary.VisualStateUpdateCount);
        Assert.Contains("Tree burn consequence failed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TreeBurnedTexturePathSkipsInvalidatedUnityRenderersAndMaterials()
    {
        string source = ReadTimberbornSource("TimberbornRuntimeBurnedTextures.cs");

        Assert.Contains("GetLiveRenderers(blockObject)", source, StringComparison.Ordinal);
        Assert.Contains("renderer != null", source, StringComparison.Ordinal);
        Assert.Contains("MissingReferenceException or NullReferenceException", source, StringComparison.Ordinal);
        Assert.Contains("wildfire_timberborn_tree_burned_texture_renderer_skipped", source, StringComparison.Ordinal);
        Assert.Contains("wildfire_timberborn_tree_burned_texture_material_skipped", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DeltaConsumerAndQaStatusExposeTreeBurnTelemetry()
    {
        FireGrid grid = new(1, 1, 1);
        TimberbornBurnDamageService burnDamageService = CreateService(
            TreeDescriptor("Tree.Pine", "Log", amount: 1));
        burnDamageService.RegisterTargets(
            grid,
            [Registration("tree-pine-1", "Tree.Pine", [new TimberbornCellCoordinates(0, 0, 0)])]);
        RecordingTreeBurnConsequenceApi treeApi = new();
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(
                burnDamageSink: burnDamageService,
                treeBurnConsequenceSink: new TimberbornTreeBurnConsequenceSink(burnDamageService, treeApi)));

        TimberbornFireDeltaConsumerSummary summary =
            consumer.Consume(27, [Delta(0, oldFuel: 15, newFuel: 0)]);
        TimberbornQaCommandResult result = TimberbornQaCommandResult.CreateSuccess(
            "qa-readiness",
            new TimberbornQaCommandState(
                IsSimulatorIntegrated: true,
                BurnDamageRegisteredTreeBurnTargetCount: 1,
                BurnDamageRegisteredTreeBurnOwnedCellCount: 1,
                LastDeltaConsumerTreeBurnConsideredTargetCount: summary.TreeBurnConsideredTargetCount,
                LastDeltaConsumerTreeBurnYieldLost: summary.TreeBurnYieldLost,
                LastDeltaConsumerTreeBurnKilledTreeCount: summary.TreeBurnKilledTreeCount,
                LastDeltaConsumerTreeBurnFailedConsequenceCount: summary.TreeBurnFailedConsequenceCount),
            ["help", "qa-readiness", "status"]);

        Assert.Equal(1, summary.TreeBurnConsideredTargetCount);
        Assert.Equal(1, summary.TreeBurnYieldLost);
        Assert.Equal(1, summary.TreeBurnKilledTreeCount);
        Assert.Contains("tree_burn_yield_lost=1", summary.ToLogToken());
        Assert.Contains("burn_damage_registered_tree_burn_targets=1", result.ResultToken);
        Assert.Contains("last_delta_consumer_tree_burn_considered_targets=1", result.ResultToken);
        Assert.Contains("last_delta_consumer_tree_burn_yield_lost=1", result.ResultToken);
    }

    private static TimberbornBurnDamageService CreateService(params TimberbornBurnDamageDescriptor[] descriptors)
    {
        return new TimberbornBurnDamageService(
            new TimberbornBurnDamageDescriptorCatalog(descriptors),
            new TimberbornBurnDamageCapacityCalculator());
    }

    private static TimberbornBurnDamageDescriptor TreeDescriptor(string specId, string resourceId, int amount)
    {
        return new TimberbornBurnDamageDescriptor(
            specId,
            TimberbornBurnDamageTargetKind.Tree,
            TimberbornBurnMaterialKind.Wood,
            resourceYields: [new TimberbornBurnDamageResourceStack(resourceId, amount)]);
    }

    private static TimberbornBurnDamageDescriptor CropDescriptor(string specId, string resourceId, int amount)
    {
        return new TimberbornBurnDamageDescriptor(
            specId,
            TimberbornBurnDamageTargetKind.Crop,
            TimberbornBurnMaterialKind.Organic,
            resourceYields: [new TimberbornBurnDamageResourceStack(resourceId, amount)]);
    }

    private static TimberbornBurnDamageTargetRegistration Registration(
        string stableId,
        string specId,
        IReadOnlyList<TimberbornCellCoordinates> ownedCells,
        TimberbornBurnDamageDescriptor? descriptorOverride = null)
    {
        return new TimberbornBurnDamageTargetRegistration(
            new TimberbornBurnDamageTargetKey(stableId),
            specId,
            ownedCells,
            descriptorOverride: descriptorOverride);
    }

    private static TimberbornFireCellDeltaDecision Decision(
        int cellIndex,
        int oldFuel,
        int newFuel,
        int oldWater = 0,
        int newWater = 0)
    {
        return TimberbornFireCellDeltaDecision.FromDelta(Delta(cellIndex, oldFuel, newFuel, oldWater, newWater));
    }

    private static CellDelta Delta(
        int cellIndex,
        int oldFuel,
        int newFuel,
        int oldWater = 0,
        int newWater = 0)
    {
        return new CellDelta(
            cellIndex,
            PackedCell.Pack(oldFuel, heat: 10, flammability: 3, water: oldWater, terrain: 1, burningLevel: 1),
            PackedCell.Pack(newFuel, heat: 10, flammability: 3, water: newWater, terrain: 1, burningLevel: 1));
    }

    private static string ReadTimberbornSource(string fileName)
    {
        string root = FindRepoRoot();
        string timberbornRoot = Path.Combine(root, "src", "Wildfire.Timberborn");
        string path = Directory
            .EnumerateFiles(timberbornRoot, fileName, SearchOption.AllDirectories)
            .First();
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Wildfire.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate Wildfire repo root.");
    }

    private sealed class RecordingTreeBurnConsequenceApi(
        Func<TimberbornTreeBurnConsequence, TimberbornTreeBurnConsequenceResult>? apply = null)
        : ITimberbornTreeBurnConsequenceApi
    {
        public List<TimberbornTreeBurnConsequence> Consequences { get; } = [];

        public TimberbornTreeBurnConsequenceResult ApplyConsequence(TimberbornTreeBurnConsequence consequence)
        {
            Consequences.Add(consequence);
            return apply?.Invoke(consequence) ?? new TimberbornTreeBurnConsequenceResult(
                Applied: true,
                Failed: false);
        }
    }

    private sealed class RecordingFireLogSink : ITimberbornFireLogSink
    {
        public void Info(string message)
        {
        }

        public void Warning(string message)
        {
        }
    }
}
