namespace Wildfire.Core.Tests;

public sealed class TimberbornFertilizeDesignationServiceTests
{
    [Fact]
    public void FertileAshApplicationUsesResolvedSimulatorLandingCell()
    {
        TimberbornFertilizeDesignationApplicationDecision decision =
            TimberbornFertilizeDesignationRules.DecideApplication(
                cellIndex: 4,
                cellCount: 16,
                applicationCellIndex: 9,
                applicationCellIsTainted: false,
                hasNearbyUnreservedFertileAsh: true);

        Assert.Equal(TimberbornFertilizeDesignationApplicationOutcome.Apply, decision.Outcome);
        Assert.Equal(9, decision.ApplicationCellIndex);
        Assert.True(decision.ShouldConsumeInventory);
        Assert.True(decision.ShouldApplyFertileAsh);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(16)]
    public void FertileAshApplicationSkipsDesignationsOutsideSimulatorGrid(int cellIndex)
    {
        TimberbornFertilizeDesignationApplicationDecision decision =
            TimberbornFertilizeDesignationRules.DecideApplication(
                cellIndex,
                cellCount: 16,
                applicationCellIndex: null,
                applicationCellIsTainted: false,
                hasNearbyUnreservedFertileAsh: true);

        Assert.Equal(TimberbornFertilizeDesignationApplicationOutcome.SkipInvalidCell, decision.Outcome);
        Assert.False(decision.ShouldConsumeInventory);
        Assert.False(decision.ShouldApplyFertileAsh);
    }

    [Fact]
    public void FertileAshApplicationSkipsWhenSimulatorCannotResolveLandingCell()
    {
        TimberbornFertilizeDesignationApplicationDecision decision =
            TimberbornFertilizeDesignationRules.DecideApplication(
                cellIndex: 4,
                cellCount: 16,
                applicationCellIndex: null,
                applicationCellIsTainted: false,
                hasNearbyUnreservedFertileAsh: true);

        Assert.Equal(TimberbornFertilizeDesignationApplicationOutcome.SkipUnresolvedApplicationCell, decision.Outcome);
        Assert.False(decision.ShouldConsumeInventory);
        Assert.False(decision.ShouldApplyFertileAsh);
    }

    [Fact]
    public void FertileAshApplicationBlocksTaintedAshWithoutConsumingInventory()
    {
        TimberbornFertilizeDesignationApplicationDecision decision =
            TimberbornFertilizeDesignationRules.DecideApplication(
                cellIndex: 4,
                cellCount: 16,
                applicationCellIndex: 9,
                applicationCellIsTainted: true,
                hasNearbyUnreservedFertileAsh: true);

        Assert.Equal(TimberbornFertilizeDesignationApplicationOutcome.BlockTaintedAsh, decision.Outcome);
        Assert.Equal(9, decision.ApplicationCellIndex);
        Assert.False(decision.ShouldConsumeInventory);
        Assert.False(decision.ShouldApplyFertileAsh);
    }

    [Fact]
    public void FertileAshApplicationWaitsWhenNoNearbyUnreservedInventoryExists()
    {
        TimberbornFertilizeDesignationApplicationDecision decision =
            TimberbornFertilizeDesignationRules.DecideApplication(
                cellIndex: 4,
                cellCount: 16,
                applicationCellIndex: 9,
                applicationCellIsTainted: false,
                hasNearbyUnreservedFertileAsh: false);

        Assert.Equal(TimberbornFertilizeDesignationApplicationOutcome.WaitForInventory, decision.Outcome);
        Assert.False(decision.ShouldConsumeInventory);
        Assert.False(decision.ShouldApplyFertileAsh);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void FertileAshInventorySelectionRequiresStockAndUnreservedTakeableAsh(
        bool hasFertileAshStock,
        bool hasUnreservedFertileAsh,
        bool expected)
    {
        Assert.Equal(
            expected,
            TimberbornFertilizeDesignationRules.ShouldUseInventory(
                hasFertileAshStock,
                hasUnreservedFertileAsh));
    }

    [Fact]
    public void FertileAshInventoryScanFailuresProduceQaVisibleSkippedWarning()
    {
        string warning = TimberbornFertilizeDesignationRules.FormatInventoryScanSkippedWarning(
            new InvalidOperationException("native cache stale"));

        Assert.Contains(TimberbornFertilizeDesignationRules.InventoryScanSkippedLogToken, warning);
        Assert.Contains("reason=InvalidOperationException", warning);
    }

    [Fact]
    public void FertileAshInventoryScanIncludesGoodStacksAndSimpleOutputInventories()
    {
        Assert.Equal(
            [
                TimberbornFertilizeInventorySourceKind.GoodStack,
                TimberbornFertilizeInventorySourceKind.SimpleOutputInventory,
            ],
            TimberbornFertilizeDesignationRules.SupportedInventorySourceKinds);
    }

    [Theory]
    [InlineData(typeof(NullReferenceException), true)]
    [InlineData(typeof(InvalidOperationException), true)]
    [InlineData(typeof(MissingMethodException), false)]
    public void TreeBurnedLeftoverComponentProbeSkipsOnlyStaleNativeFailures(
        Type exceptionType,
        bool expected)
    {
        Exception exception = (Exception)Activator.CreateInstance(exceptionType)!;

        Assert.Equal(
            expected,
            TimberbornRuntimeBurnedTextureBehavior.ShouldSkipStaleTreeComponentProbe(exception));
    }

    [Fact]
    public void TerminalTreeComponentSkipsRemainAppliedConsequences()
    {
        TimberbornTreeBurnConsequenceResult result =
            TimberbornRuntimeBurnedTextureBehavior.AlreadyTerminalTreeResult();

        Assert.True(result.Applied);
        Assert.False(result.Failed);
    }

    [Fact]
    public void NativeBurnedResourceDeletionReportsCompletedCropVisualState()
    {
        TimberbornCropBurnConsequenceResult result =
            TimberbornRuntimeBurnedTextureBehavior.DeletedBurnedResourceResult();

        Assert.True(result.MatchedCropTarget);
        Assert.True(result.KilledCrop);
        Assert.True(result.VisualStateUpdated);
        Assert.False(result.FailedConsequence);
        Assert.Equal("native_entity_service", TimberbornRuntimeBurnedTextureBehavior.CropBurnedResourceDeletedReason);
    }

    [Fact]
    public void FireSimLeavesWetCellAshWashoutToAccountableExternalMutations()
    {
        string source = ReadUnitySource("FireSim.compute");

        Assert.Contains("uint ApplyAshChange(uint atmospheric, FireSimChangeGpu change)", source, StringComparison.Ordinal);
        Assert.Contains("uint removeAsh = (change.AddFields >> 10) & 0x3u;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Water(newCell) > 0u && ash > 0u && (Tick & 63u) == 0u", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Water(newCell) > 0u && ash > 0u)", source, StringComparison.Ordinal);
    }

    private static string ReadUnitySource(string fileName)
    {
        string root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(root, "src", "Wildfire.Unity", fileName));
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
}
