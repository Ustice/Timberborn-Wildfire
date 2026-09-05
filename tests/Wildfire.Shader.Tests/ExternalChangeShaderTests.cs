using Wildfire.Core;
using Wildfire.Unity;
using static Wildfire.Shader.Tests.UnityShaderHarness;

namespace Wildfire.Shader.Tests;

public sealed class ExternalChangeShaderTests
{
    [UnityShaderFact]
    public void ExternalIgnitionAndSimulationPreserveBothDeltasInOneCellGrid()
    {
        ushort ignition = PackedCell.Pack(fuel: 15, heat: 15, flammability: 3, water: 0, terrain: 1, burningLevel: 0);
        ShaderSnapshotFixture fixture = SingleCellFixture(
            "external-ignition-two-transitions",
            ShaderSnapshotExternalChanges.Encode(1, new FireSimChange(0, SetCell: ignition)));

        ShaderSnapshotCapture capture = Capture(fixture);

        ShaderSnapshotTick tick = Assert.Single(capture.Ticks);
        Assert.Equal(2, tick.DeltaCount);
        Assert.Equal(new ShaderSnapshotDelta(0, 0, ignition), tick.Deltas[0]);
        Assert.Equal(ignition, tick.Deltas[1].OldCell);
        Assert.Equal(capture.FinalPackedCells[0], tick.Deltas[1].NewCell);
        Assert.True(PackedCell.BurningLevel(capture.FinalPackedCells[0]) > 0);
    }

    [UnityShaderFact]
    public void ProductionEncodedSmokeCommandReachesSimulationTransportField()
    {
        ShaderSnapshotFixture fixture = SingleCellFixture(
            "external-toxic-smoke",
            ShaderSnapshotExternalChanges.Encode(1, new FireSimChange(0, SetSmoke: 5, SetSmokeContamination: 7)));

        ShaderSnapshotCapture capture = Capture(fixture);

        Assert.NotNull(capture.FinalAtmosphericFields);
        WildfireTransportFieldState transport = WildfireTransportFieldState.Unpack(Assert.Single(capture.FinalAtmosphericFields));
        Assert.Equal(4, transport.Smoke);
        Assert.Equal(7, transport.SmokeContamination);
        Assert.Empty(Assert.Single(capture.Ticks).Deltas);
    }

    private static ShaderSnapshotFixture SingleCellFixture(string scenario, ShaderSnapshotExternalChanges changes)
    {
        return new ShaderSnapshotFixture(
            FormatVersion: 1,
            Scenario: scenario,
            Seed: 89,
            Grid: new ComputeGridDimensions(1, 1, 1),
            SelectedLayer: new ShaderSnapshotLayer(0, 0, 1),
            InitialCells: [0],
            ExternalChanges: [changes]);
    }
}
