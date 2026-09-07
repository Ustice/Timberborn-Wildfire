using Wildfire.Timberborn.Qa;

namespace Wildfire.Core.Tests;

public sealed class BorrowedDutyQaCommandTests
{
    private const string Donor = "01234567-89ab-cdef-0123-456789abcdef";
    [Theory]
    [InlineData("qa-borrowed-duty-arm")]
    [InlineData("qa-borrowed-duty-arm invalid 1 2 3")]
    [InlineData("qa-borrowed-duty-arm 00000000-0000-0000-0000-000000000000 1 2 3")]
    [InlineData("qa-borrowed-duty-arm " + Donor + " NaN 2 3")]
    [InlineData("qa-borrowed-duty-arm " + Donor + " 1 Infinity 3")]
    [InlineData("qa-borrowed-duty-arm " + Donor + " 1 2 1e99")]
    [InlineData("qa-borrowed-duty-arm " + Donor + " 1 2 3 extra")]
    [InlineData("qa-borrowed-duty-cancel extra")]
    [InlineData("qa-borrowed-duty-status extra")]
    public void MalformedPayloadCannotReachNativeAdmission(string command)
    {
        var api = new FakeDuty();
        var result = Bridge(api, TimberbornQaCommandAccess.Development).Execute(command);
        Assert.False(result.Success);
        Assert.Equal(0, api.Calls);
    }

    [Fact]
    public void ExistingGateBlocksMutationButAllowsReadOnlyRestoredActorStatus()
    {
        var api = new FakeDuty { Value = new(false, "none", null, new[] { new BorrowedDutyQaActor(Guid.Parse(Donor), "Returning", false, true) }) };
        var bridge = Bridge(api, TimberbornQaCommandAccess.Diagnostics);
        Assert.False(bridge.Execute($"qa-borrowed-duty-arm {Donor} 1 2 3").Success);
        Assert.False(bridge.Execute("qa-borrowed-duty-cancel").Success);
        Assert.Equal(0, api.Calls);
        var status = bridge.Execute("qa-borrowed-duty-status");
        Assert.True(status.Success);
        Assert.Contains("admissions_enabled=false", status.Message);
        Assert.Contains("active_count=1", status.Message);
        Assert.Contains("running_count=1", status.Message);
        Assert.Contains("Returning", status.Message);
        Assert.DoesNotContain(TimberbornQaBorrowedDutyCommands.Arm, bridge.KnownCommands);
        Assert.Contains(TimberbornQaBorrowedDutyCommands.Status, bridge.KnownCommands);
    }

    [Fact]
    public void BridgePreservesExplicitDonorAndAxesAndDistinguishesOfferFromRunning()
    {
        var api = new FakeDuty();
        var bridge = Bridge(api, TimberbornQaCommandAccess.Development);
        var arm = bridge.Execute($"qa-borrowed-duty-arm {Donor} 1.25 2 3.5");
        Assert.True(arm.Success);
        Assert.Equal(new BorrowedDutyQaArmRequest(Guid.Parse(Donor), 1.25f, 2, 3.5f), api.LastArm);
        Assert.Contains("offer=armed", arm.Message);
        Assert.Contains("active_count=0", arm.Message);
        Assert.True(bridge.Execute("qa-borrowed-duty-cancel").Success);
        Assert.Equal(2, api.Calls);
    }

    [Fact]
    public void AbsentNativeCapabilityIsExplicitlyUnsupported()
    {
        var result = Bridge(null, TimberbornQaCommandAccess.Development).Execute($"qa-borrowed-duty-arm {Donor} 1 2 3");
        Assert.False(result.Success);
        Assert.Equal("borrowed_duty_unsupported", result.Message);
    }

    private static TimberbornQaCommandBridge Bridge(ITimberbornQaBorrowedDuty? duty, TimberbornQaCommandAccess access) => new(
        TimberbornQaCommandStateProvider.Placeholder, NullTimberbornQaDeltaStimulus.Instance,
        NullTimberbornQaBuildingBurnoutStimulus.Instance, NullTimberbornQaWaterSuppressionStimulus.Instance,
        NullTimberbornQaBurnDurationStimulus.Instance, NullTimberbornQaFireSimParameterPresetSelector.Instance,
        NullTimberbornQaSoilMoistureMapProbe.Instance, new Log(), access: access, borrowedDuty: duty);
    private sealed class Log : ITimberbornQaCommandLogSink { public void Info(string message) { } public void Warning(string message) { } }
    private sealed class FakeDuty : ITimberbornQaBorrowedDuty
    {
        public int Calls;
        public BorrowedDutyQaArmRequest LastArm;
        public BorrowedDutyQaStatus Value = new(false, "none", null, Array.Empty<BorrowedDutyQaActor>());
        public BorrowedDutyQaStatus Arm(BorrowedDutyQaArmRequest request)
        { Calls++; LastArm = request; return Value = new(true, "armed", request.DonorId, Array.Empty<BorrowedDutyQaActor>()); }
        public BorrowedDutyQaStatus Cancel() { Calls++; return Value = Value with { OfferState = "none", DonorId = null }; }
        public BorrowedDutyQaStatus Status() { Calls++; return Value; }
    }
}
