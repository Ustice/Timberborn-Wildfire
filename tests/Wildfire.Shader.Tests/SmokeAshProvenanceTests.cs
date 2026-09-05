using Wildfire.Core;
using Wildfire.Unity;
using static Wildfire.Shader.Tests.UnityShaderHarness;

namespace Wildfire.Shader.Tests;

public sealed class SmokeAshProvenanceTests
{
    [UnityShaderFact]
    public void ToxicSmokeProducesContaminatedAshOnItsLandingSurface()
    {
        ShaderSnapshotCapture capture = Capture(CreateFixture(depth: 1));
        Assert.NotNull(capture.FinalAtmosphericFields);
        WildfireTransportFieldState deposited = WildfireTransportFieldState.Unpack(capture.FinalAtmosphericFields[0]);

        Assert.Equal(1, deposited.Ash);
        Assert.Equal(7, deposited.AshContamination);
    }

    [UnityShaderFact]
    public void AshCreatedFromAirborneToxicSmokeRetainsContaminationWhenFalling()
    {
        ShaderSnapshotCapture capture = Capture(CreateFixture(depth: 2));
        Assert.NotNull(capture.FinalAtmosphericFields);
        WildfireTransportFieldState deposited = WildfireTransportFieldState.Unpack(capture.FinalAtmosphericFields[0]);
        WildfireTransportFieldState airborne = WildfireTransportFieldState.Unpack(capture.FinalAtmosphericFields[1]);

        Assert.Equal(1, deposited.Ash);
        Assert.Equal(7, deposited.AshContamination);
        Assert.Equal(0, airborne.Ash);
    }

    private static ShaderSnapshotFixture CreateFixture(int depth)
    {
        ushort[] cells = new ushort[depth];
        cells[0] = PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 1, burningLevel: 0);
        uint[] transport = new uint[depth];
        transport[^1] = new WildfireTransportFieldState(
            Steam: 0, Smoke: 7, SmokeContamination: 7, Ash: 0, AshContamination: 0, Source: false).Pack();
        return new ShaderSnapshotFixture(
            FormatVersion: 1,
            Scenario: $"toxic-smoke-ash-provenance-depth{depth}",
            Seed: 21,
            Grid: new ComputeGridDimensions(1, 1, depth),
            SelectedLayer: new ShaderSnapshotLayer(0, 0, 1),
            InitialCells: cells,
            InitialAtmosphericFields: transport);
    }
}
