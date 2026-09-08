using Wildfire.Core;
using Wildfire.Unity;
using static Wildfire.Shader.Tests.UnityShaderHarness;

namespace Wildfire.Shader.Tests;

public sealed class AshApplicationShaderTests
{
    [UnityShaderFact]
    public void TwoApplicantsAtLimitReceiveOnePositiveReceiptInCommandOrder()
    {
        var capture = Capture(Fixture("ash-apply-competing", 1, 0,
            [new(0, ApplyCleanAshLimit: 2), new(0, ApplyCleanAshLimit: 2), new(0, CollectCleanAsh: 3)]));
        Assert.Equal(FireSimAshApplicationOutcome.Applied, Receipt(capture, 0, 2).Outcome);
        Assert.Equal(1, Receipt(capture, 0, 2).Added);
        Assert.Equal(FireSimAshApplicationOutcome.Full, Receipt(capture, 1, 2).Outcome);
        Assert.Equal(0, Receipt(capture, 1, 2).Added);
        Assert.Equal(2, Collection(capture, 2).Collected);
    }

    [UnityShaderFact]
    public void EarlierRemovalAllowsApplicationAndLaterRemovalCannotEraseReceipt()
    {
        var capture = Capture(Fixture("ash-apply-ordered", 3, 0,
            [new(0, RemoveAsh: 2), new(0, ApplyCleanAshLimit: 2), new(0, SetAsh: 0)]));
        Assert.Equal(1, Receipt(capture, 1, 2).Added);
        Assert.Equal(0, WildfireTransportFieldState.Unpack(capture.FinalAtmosphericFields![0]).Ash);
    }

    [UnityShaderFact]
    public void EarlierTaintWinsOverCapacityAndRejectedApplicationChangesNoFields()
    {
        var fixture = Fixture("ash-apply-tainted", 3, 0,
            [new(0, SetAshContamination: 7), new(0, ApplyCleanAshLimit: 3)]);
        var capture = Capture(fixture);
        Assert.Equal(FireSimAshApplicationOutcome.Tainted, Receipt(capture, 1, 3).Outcome);
        Assert.Equal(0, Receipt(capture, 1, 3).Added);
        AssertSameFields(Capture(fixture with
        {
            Scenario = "ash-apply-tainted-control",
            ExternalChanges = [ShaderSnapshotExternalChanges.Encode(1, new FireSimChange(0, SetAshContamination: 7))],
        }), capture);
    }

    [UnityShaderFact]
    public void OpenSoilAcceptsEvenWithoutTerrainBitAndOnlyAshChanges()
    {
        var fixture = Fixture("ash-apply-open-soil", 0, 0, [new(0, ApplyCleanAshLimit: 1)]);
        Assert.Equal(0, PackedCell.Terrain(fixture.InitialCells[0]));
        var capture = Capture(fixture);
        Assert.Equal(1, Receipt(capture, 0, 1).Added);
        // This control changes only initial ash. Identical ensuing simulation detects unintended
        // clearing of preexisting smoke, steam, soil, water, or source flags by application.
        var controlFields = fixture.InitialAtmosphericFields!.ToArray();
        controlFields[0] = (controlFields[0] & ~(7u << 9)) | (1u << 9);
        AssertSameFields(Capture(fixture with
        {
            Scenario = "ash-apply-open-soil-control", InitialAtmosphericFields = controlFields, ExternalChanges = null,
        }), capture);
    }

    [UnityShaderFact]
    public void InvalidAirRejectsBeforeTaintOrFullAndLeavesAllFieldsUnchanged()
    {
        var fixture = Fixture("ash-apply-invalid-air", 3, 7, [new(0, ApplyCleanAshLimit: 1)]);
        fixture.CompanionFields![0] &= ~255u; // Real class0 air; packed terrain remains0.
        var capture = Capture(fixture);
        Assert.Equal(FireSimAshApplicationOutcome.InvalidSurface, Receipt(capture, 0, 1).Outcome);
        AssertSameFields(Capture(fixture with { Scenario = "ash-apply-invalid-air-control", ExternalChanges = null }), capture);
    }

    [UnityShaderFact]
    public void LaterFallingAshIsNotASecondApplicationReceipt()
    {
        var fixture = new ShaderSnapshotFixture(1, "ash-apply-before-fall", 89,
            new(1, 1, 2), new(0, 0, 1), [0, 0],
            InitialAtmosphericFields: [0, new WildfireTransportFieldState(0, 0, 0, 3, 0, false).Pack()],
            CompanionFields: [FireSimBaselineDefinition.OpenSoil.CompanionMaterial, 0],
            ExternalChanges: [ShaderSnapshotExternalChanges.Encode(1, new FireSimChange(0, ApplyCleanAshLimit: 1))]);
        var capture = Capture(fixture);
        Assert.Equal(1, Receipt(capture, 0, 1).Added);
        Assert.True(WildfireTransportFieldState.Unpack(capture.FinalAtmosphericFields![0]).Ash > 1);
    }

    [UnityShaderFact]
    public void InvalidOrReusedReceiptWordsCannotMutateFieldsOrProduceValidity()
    {
        foreach (var corruption in new[] { "zero-limit", "receipt-valid", "receipt-reason", "mixed", "value" })
        {
            var fixture = Fixture("ash-apply-invalid-" + corruption, 0, 0, [new(0, ApplyCleanAshLimit: 2)]);
            uint[] words = fixture.ExternalChanges![0].Words;
            if (corruption == "zero-limit") words[2] = 0;
            if (corruption == "receipt-valid") words[2] |= 1u << 29;
            if (corruption == "receipt-reason") words[2] |= 1u << 30;
            if (corruption == "mixed") words[1] |= 1u << 11;
            if (corruption == "value") words[3] = 1;
            var capture = Capture(fixture);
            Assert.Throws<InvalidOperationException>(() => Receipt(capture, 0, 2));
            AssertSameFields(Capture(fixture with { Scenario = fixture.Scenario + "-control", ExternalChanges = null }), capture);
        }
    }

    private static void AssertSameFields(ShaderSnapshotCapture expected, ShaderSnapshotCapture actual)
    {
        Assert.Equal(expected.FinalPackedCells, actual.FinalPackedCells);
        Assert.Equal(expected.FinalAtmosphericFields, actual.FinalAtmosphericFields);
        Assert.Equal(expected.FinalCompanionFields, actual.FinalCompanionFields);
    }

    private static FireSimGpuChange Command(ShaderSnapshotCapture capture, int index)
    {
        uint[] words = Assert.Single(capture.Ticks).AppliedChangeWords!;
        int start = index * 4;
        return new(words[start], words[start + 1], words[start + 2], words[start + 3]);
    }
    private static FireSimAshApplicationReceipt Receipt(ShaderSnapshotCapture capture, int index, byte limit) =>
        FireSimGpuProtocol.DecodeApplicationReceipt(Command(capture, index), new(0, limit));
    private static FireSimAshCollectionReceipt Collection(ShaderSnapshotCapture capture, int index) =>
        FireSimGpuProtocol.DecodeCollectionReceipt(Command(capture, index), new(0, 3));

    private static ShaderSnapshotFixture Fixture(string name, byte ash, byte taint, FireSimChange[] changes)
    {
        int count = Math.Max(1, changes.Length);
        ushort[] cells = new ushort[count];
        cells[0] = PackedCell.Pack(0, 0, 0, 2, 0, 0);
        uint[] atmosphere = new uint[count];
        atmosphere[0] = new WildfireTransportFieldState(2, 3, 5, ash, taint, true).Pack();
        uint[] companion = new uint[count];
        companion[0] = FireSimBaselineDefinition.OpenSoil.CompanionMaterial | (5u << 25);
        return new(1, name, 89, new(count, 1, 1), new(0, 0, count), cells,
            InitialAtmosphericFields: atmosphere, CompanionFields: companion,
            ExternalChanges: [ShaderSnapshotExternalChanges.Encode(1, changes)]);
    }
}
