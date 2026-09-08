using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

public sealed class InitialFormationScopeTests
{
    private static readonly Guid BodyId = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid SourceId = new("00000000-0000-0000-0000-000000000010");
    private static readonly Guid ExcludedId = new("00000000-0000-0000-0000-000000000011");

    [Theory]
    [InlineData("quantity")]
    [InlineData("placement")]
    [InlineData("membership")]
    [InlineData("excluded")]
    [InlineData("source")]
    [InlineData("water")]
    [InlineData("soil")]
    public void FullCaptureComparisonIncludesBodyAndEnvironmentalLifecycleFacts(string mutation)
    {
        var initial = Capture();
        Assert.True(initial.SameReadings(Capture()));
        Assert.False(initial.SameReadings(Capture(mutation)));
    }

    [Fact]
    public void InternalFormationSharesOuterReadScopeWhilePublicWrapperStillRejectsNesting()
    {
        var native = new OwnedConsequenceBatchTests.NativeFake();
        var guard = new NativeResourceTransaction(); var grid = new FireGrid(1, 1, 1);
        var body = OwnedNativeRestoreFixture.Facts();
        var capture = new TimberbornInitialWorldCapture(grid, [body], [], [], new(grid, [], [], []));
        var compiled = TimberbornInitialBodyCompiler.Compile(capture, [new(BodyId,
            TimberbornInitialAccountingBasis.NativeResourceAmounts,
            [new("Gatherable", TimberbornCapturedYieldRole.Gatherable, TimberbornInitialYieldUse.Actual)], [])]);
        var registry = new TimberbornNativeMaterialRegistry(grid, []); registry.Reconcile(compiled.Projections, []);
        var effects = new TimberbornOwnedNativeEffects(native, native, native, native, native);
        native.DuringIsLive = () => Assert.Throws<InvalidOperationException>(guard.ThrowIfSaveUnsafe);
        var consumer = guard.CaptureAtRest(() =>
        {
            Assert.Throws<InvalidOperationException>(() => TimberbornOwnedDeltaConsumer.CreateWithNativeDefinitions(
                registry, compiled.CreateDamage(grid), effects, guard, [body]));
            return TimberbornOwnedDeltaConsumer.CreateWithNativeDefinitionsDuringCapture(registry, compiled.CreateDamage(grid), effects, guard, [body]);
        });
        native.DuringIsLive = null;
        Assert.Single(consumer.CaptureHistory().Owners); guard.ThrowIfSaveUnsafe();
    }

    private static TimberbornInitialWorldCapture Capture(string? mutation = null)
    {
        FireGrid grid = new(1, 1, 3);
        var body = new TimberbornInitialMaterialBody(BodyId, "Carrot", TimberbornInitialBodyShape.Crop,
            [new(new(0, 0, 0), mutation == "placement" ? 2 : 1)],
            [new(TimberbornCapturedYieldRole.Cuttable, "Cuttable", "Carrot", mutation == "quantity" ? 2 : 3, "Carrot", 5, true, true)], [], null);
        var environment = new TimberbornInitialEnvironmentCapture(grid, [0],
            [new(1, mutation == "soil" ? .2f : .1f, 0, true, false)],
            [new(0, 0, 1, 3, mutation == "water" ? .5f : .2f, 0, 0)]);
        return new(grid, mutation == "membership" ? [] : [body],
            [new(ExcludedId, "Building", mutation == "excluded" ? TimberbornInitialCaptureExclusion.Preview : TimberbornInitialCaptureExclusion.Unfinished)],
            [new(SourceId, "WaterSource", mutation == "source", [new(new(0, 0, 0), 2)])], environment);
    }
}
