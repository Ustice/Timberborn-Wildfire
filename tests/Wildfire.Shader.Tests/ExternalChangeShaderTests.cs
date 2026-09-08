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

    [UnityShaderFact]
    public void RepeatedWaterAdditionsSaturateAndPreserveOtherCellFields()
    {
        ushort initial = PackedCell.Pack(fuel: 13, heat: 11, flammability: 2, water: 0, terrain: 1, burningLevel: 3);
        ushort wetOne = PackedCell.SetWater(initial, 1);
        ushort wetTwo = PackedCell.SetWater(initial, 2);
        ushort saturated = PackedCell.SetWater(initial, 3);
        FireSimChange[] changes =
        [
            new(0, AddWater: 1),
            new(0, AddWater: 1),
            new(0, AddWater: 255),
            new(0, AddWater: 1),
        ];
        ShaderSnapshotFixture fixture = WaterFixture("external-water-addition", initial, changes);

        ShaderSnapshotCapture capture = Capture(fixture);

        // External transitions precede the ordinary simulation; checking them also proves
        // suppression does not directly rewrite heat, fuel, terrain, or burning intensity.
        AssertCellTransitions(capture,
        [
            new(0, initial, wetOne),
            new(0, wetOne, wetTwo),
            new(0, wetTwo, saturated),
        ], saturated);
    }

    [UnityShaderFact]
    public void WaterAdditionFollowsCellReplacementAndPrecedesExplicitOverride()
    {
        ushort replacement = PackedCell.Pack(fuel: 7, heat: 9, flammability: 3, water: 1, terrain: 1, burningLevel: 2);
        ushort wetTwo = PackedCell.SetWater(replacement, 2);
        ushort dry = PackedCell.SetWater(replacement, 0);
        ushort wetOne = PackedCell.SetWater(replacement, 1);
        FireSimChange[] changes =
        [
            new(0, SetCell: replacement, AddWater: 1),
            new(0, AddWater: 3, SetWater: 0),
            new(0, AddWater: 1),
            new(0, AddWater: 0),
        ];
        ShaderSnapshotFixture fixture = WaterFixture("external-water-precedence", 0, changes);

        ShaderSnapshotCapture capture = Capture(fixture);

        AssertCellTransitions(capture,
        [
            new(0, 0, wetTwo),
            new(0, wetTwo, dry),
            new(0, dry, wetOne),
        ], wetOne);
    }

    private static ShaderSnapshotFixture WaterFixture(string scenario, ushort initial, FireSimChange[] changes)
    {
        // The fixture upload capacity equals its cell count. Spare cells allow several
        // commands for cell zero in one dispatch without changing the production queue.
        ushort[] cells = new ushort[changes.Length];
        cells[0] = initial;
        return new ShaderSnapshotFixture(
            FormatVersion: 1,
            Scenario: scenario,
            Seed: 89,
            Grid: new ComputeGridDimensions(cells.Length, 1, 1),
            SelectedLayer: new ShaderSnapshotLayer(0, 0, 1),
            InitialCells: cells,
            ExternalChanges: [ShaderSnapshotExternalChanges.Encode(1, changes)]);
    }

    private static void AssertCellTransitions(
        ShaderSnapshotCapture capture, ShaderSnapshotDelta[] externalTransitions, ushort beforeSimulation)
    {
        List<ShaderSnapshotDelta> expected = [.. externalTransitions];
        ushort final = capture.FinalPackedCells[0];
        if (final != beforeSimulation)
        {
            expected.Add(new ShaderSnapshotDelta(0, beforeSimulation, final));
        }

        Assert.Equal(expected, Assert.Single(capture.Ticks).Deltas.Where(delta => delta.CellIndex == 0));
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
