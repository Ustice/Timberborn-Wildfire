using System.Text.Json;
using System.Text.Json.Nodes;
using Wildfire.Cli;
using Wildfire.Core;
using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed class ShaderSnapshotParameterTests
{
    [Fact]
    public void ExecutionFixtureIncludesTheCompleteCurrentCoreDefaults()
    {
        using JsonDocument document = JsonDocument.Parse(ShaderSnapshotJson.SerializeFixture(Fixture()));
        JsonElement parameters = document.RootElement.GetProperty("parameters");

        Assert.Equal(FireSimParameters.Default.IgnitionPoint, parameters.GetProperty("ignitionPoint").GetUInt32());
        Assert.Equal(FireSimParameters.Default.FireBurnHeatBase, parameters.GetProperty("fireBurnHeatBase").GetUInt32());
        Assert.Equal(FireSimParameters.Default.FireFuelBurnDownPressureNumerator, parameters.GetProperty("fireFuelBurnDownPressureNumerator").GetUInt32());
        Assert.Equal(FireSimParameters.Default.FireFuelBurnDownPressureDenominator, parameters.GetProperty("fireFuelBurnDownPressureDenominator").GetUInt32());
        Assert.Equal(FireSimParameters.Default, parameters.Deserialize<FireSimParameters>(new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void ExplicitFixtureTuningRoundTripsWithoutReplacingItWithDefaults()
    {
        FireSimParameters custom = FireSimParameters.Default with
        {
            IgnitionPoint = 9,
            FireBurnHeatBase = 3,
            FireFuelBurnDownPressureNumerator = 1,
            FireFuelBurnDownPressureDenominator = 10,
            VisualSmokeHeatWeight = 0.375f,
        };

        ShaderSnapshotFixture restored = ShaderSnapshotFixtureLoader.Load(
            ShaderSnapshotJson.SerializeFixture(Fixture() with { Parameters = custom }));

        Assert.Equal(custom, restored.EffectiveParameters);
    }

    [Fact]
    public void LegacyFixtureWithoutParametersUsesCoreDefaultsOnNextExecution()
    {
        JsonObject legacy = JsonNode.Parse(ShaderSnapshotJson.SerializeFixture(Fixture()))!.AsObject();
        legacy.Remove("parameters");

        ShaderSnapshotFixture restored = ShaderSnapshotFixtureLoader.Load(legacy.ToJsonString());
        using JsonDocument execution = JsonDocument.Parse(ShaderSnapshotJson.SerializeFixture(restored));

        Assert.Null(restored.Parameters);
        Assert.Equal(FireSimParameters.Default, restored.EffectiveParameters);
        Assert.Equal(FireSimParameters.Default.IgnitionPoint,
            execution.RootElement.GetProperty("parameters").GetProperty("ignitionPoint").GetUInt32());
    }

    [Fact]
    public void PartialExplicitParametersFailInsteadOfSilentlyUsingZeroes()
    {
        JsonObject fixture = JsonNode.Parse(ShaderSnapshotJson.SerializeFixture(Fixture()))!.AsObject();
        fixture["parameters"]!.AsObject().Remove("ignitionPoint");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => ShaderSnapshotFixtureLoader.Load(fixture.ToJsonString()));

        Assert.Contains("ignitionPoint", exception.Message);
    }

    [Fact]
    public void CliExportIncludesParametersForDirectShaderRunnerUse()
    {
        Scenario scenario = ScenarioCatalog.Build(CliOptions.Parse(["--width=2", "--height=1", "--depth=1"]));
        ShaderSnapshotFixture fixture = ShaderSnapshotFixtureLoader.Load(FixtureExporter.Export(scenario, selectedLayer: 0));

        Assert.Equal(FireSimParameters.Default, fixture.Parameters);
    }

    private static ShaderSnapshotFixture Fixture() => new(
        1, "parameter-contract", 1, new ComputeGridDimensions(1, 1, 1),
        new ShaderSnapshotLayer(0, 0, 1), [0]);
}
