using Wildfire.Core;
using Wildfire.Unity;
using static Wildfire.Shader.Tests.UnityShaderHarness;

namespace Wildfire.Shader.Tests;

public sealed class AshCollectionShaderTests
{
    [UnityShaderFact]
    public void CompetingCollectorsReceiveOnlyTheAshAvailableInRegistrationOrder()
    {
        var changes = new FireSimChange[] { new(0, CollectCleanAsh: 2), new(0, CollectCleanAsh: 2), new(0, CollectCleanAsh: 1) };
        var capture = Capture(Fixture("ash-collection-competing", 3, 0, changes));
        Assert.Equal(2, Receipt(capture, 0, 2).Collected);
        Assert.Equal(1, Receipt(capture, 1, 2).Collected);
        Assert.Equal(0, Receipt(capture, 2, 1).Collected);
        Assert.Equal(0, FinalAsh(capture));
    }

    [UnityShaderFact]
    public void ContaminationIntroducedBeforeCollectionPreventsRemoval()
    {
        var capture = Capture(Fixture("ash-collection-contaminated", 3, 0,
            [new(0, SetAshContamination: 7), new(0, CollectCleanAsh: 3)]));
        Assert.Equal(0, Receipt(capture, 1, 3).Collected);
        Assert.True(FinalAsh(capture) > 0);
        Assert.Equal(7, WildfireTransportFieldState.Unpack(capture.FinalAtmosphericFields![0]).AshContamination);
    }

    [UnityShaderFact]
    public void EarlierRemovalAndLaterAdditionDoNotChangeTheCollectionReceipt()
    {
        var capture = Capture(Fixture("ash-collection-ordered-ash-changes", 3, 0,
            [new(0, RemoveAsh: 2), new(0, CollectCleanAsh: 3), new(0, SetAsh: 3)]));
        Assert.Equal(1, Receipt(capture, 1, 3).Collected);
        Assert.True(FinalAsh(capture) > 0); // Later input replenished ash; a post-step difference cannot be the receipt.
    }

    [UnityShaderFact]
    public void ExplicitZeroReceivesAValidReceiptAndEmptyAshCannotMintGoods()
    {
        var capture = Capture(Fixture("ash-collection-zero-empty", 2, 0,
            [new(0, CollectCleanAsh: 0), new(0, CollectCleanAsh: 3), new(0, CollectCleanAsh: 3)]));
        Assert.Equal(0, Receipt(capture, 0, 0).Collected);
        Assert.Equal(2, Receipt(capture, 1, 3).Collected);
        Assert.Equal(0, Receipt(capture, 2, 3).Collected);
    }

    [UnityShaderFact]
    public void ReceiptPrecedesNormalSimulationAshMovement()
    {
        // Ash in air falls during simulation. The receiver was empty during external collection.
        var fixture = new ShaderSnapshotFixture(1, "ash-collection-before-fall", 89,
            new ComputeGridDimensions(1, 1, 2), new ShaderSnapshotLayer(0, 0, 1),
            [PackedCell.Pack(0, 0, 0, 0, 1, 0), 0],
            InitialAtmosphericFields: [0, new WildfireTransportFieldState(0, 0, 0, 3, 0, false).Pack()],
            ExternalChanges: [ShaderSnapshotExternalChanges.Encode(1, new FireSimChange(0, CollectCleanAsh: 3))]);
        var capture = Capture(fixture);
        Assert.Equal(0, Receipt(capture, 0, 3).Collected);
        Assert.True(FinalAsh(capture) > 0);
    }

    private static FireSimAshCollectionReceipt Receipt(ShaderSnapshotCapture capture, int changeIndex, byte requested)
    {
        uint[] words = Assert.Single(capture.Ticks).AppliedChangeWords!;
        Assert.NotNull(words);
        int start = changeIndex * FireSimGpuProtocol.UInt32WordsPerChange;
        return FireSimGpuProtocol.DecodeCollectionReceipt(new(words[start], words[start + 1], words[start + 2], words[start + 3]), new(0, requested));
    }

    private static int FinalAsh(ShaderSnapshotCapture capture) =>
        WildfireTransportFieldState.Unpack(capture.FinalAtmosphericFields![0]).Ash;

    private static ShaderSnapshotFixture Fixture(string name, byte ash, byte contamination, FireSimChange[] changes)
    {
        ushort[] cells = new ushort[Math.Max(1, changes.Length)];
        cells[0] = PackedCell.Pack(0, 0, 0, 0, 1, 0);
        uint[] transport = new uint[cells.Length];
        transport[0] = new WildfireTransportFieldState(0, 0, 0, ash, contamination, false).Pack();
        return new ShaderSnapshotFixture(1, name, 89, new(cells.Length, 1, 1), new(0, 0, cells.Length), cells,
            InitialAtmosphericFields: transport, ExternalChanges: [ShaderSnapshotExternalChanges.Encode(1, changes)]);
    }
}
