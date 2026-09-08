namespace Wildfire.Timberborn.Tests;

public sealed class QaSessionCommandTests
{
    [Theory]
    [InlineData("qa-save-copy 88fd83d4-0fe3-484d-93d1-183063d87f18 QA-copy", false)]
    [InlineData("qa-game-speed 0", false)]
    [InlineData("qa-game-speed 1", false)]
    [InlineData("qa-save-status 88fd83d4-0fe3-484d-93d1-183063d87f18", true)]
    public void DiagnosticsOnlyAllowsStatus(string command, bool allowed)
    {
        var session = new Session();
        var result = Bridge(session, TimberbornQaCommandAccess.Diagnostics).Execute(command);
        Assert.Equal(allowed, result.Success);
        Assert.Equal(allowed ? 1 : 0, session.Calls);
    }

    [Theory]
    [InlineData("qa-game-speed 2")]
    [InlineData("qa-game-speed -1")]
    [InlineData("qa-game-speed 0 extra")]
    [InlineData("qa-save-status 00000000-0000-0000-0000-000000000000")]
    [InlineData("qa-save-copy malformed QA-copy")]
    [InlineData("qa-save-copy 88fd83d4-0fe3-484d-93d1-183063d87f18")]
    public void InvalidArgumentsNeverReachNativeSession(string command)
    {
        var session = new Session();
        Assert.False(Bridge(session, TimberbornQaCommandAccess.Development).Execute(command).Success);
        Assert.Equal(0, session.Calls);
    }

    [Fact]
    public void ValidRequestRetainsExactGuidAndNameAndSpeedDoesNotRequireFireEnabled()
    {
        var session = new Session();
        var bridge = Bridge(session, TimberbornQaCommandAccess.Development);
        var id = Guid.NewGuid();
        Assert.True(bridge.Execute($"qa-save-copy {id} QA-copy").Success);
        Assert.Equal(id, session.Id); Assert.Equal("QA-copy", session.Name);
        Assert.True(bridge.Execute("QA-GAME-SPEED 0").Success);
        Assert.Equal(0, session.Speed);
    }

    private static TimberbornQaCommandBridge Bridge(Session session, TimberbornQaCommandAccess access) => new(
        TimberbornQaCommandStateProvider.Placeholder, NullTimberbornQaDeltaStimulus.Instance,
        NullTimberbornQaBuildingBurnoutStimulus.Instance, NullTimberbornQaWaterSuppressionStimulus.Instance,
        NullTimberbornQaBurnDurationStimulus.Instance, NullTimberbornQaFireSimParameterPresetSelector.Instance,
        NullTimberbornQaSoilMoistureMapProbe.Instance, new Log(), access: access, session: session);
    private sealed class Log : ITimberbornQaCommandLogSink { public void Info(string message) { } public void Warning(string message) { } }
    private sealed class Session : ITimberbornQaSession
    {
        internal int Calls, Speed = -1;
        internal Guid Id; internal string? Name;
        public string QueueSave(Guid id, string name) { Calls++; Id = id; Name = name; return "queued"; }
        public string SaveStatus(Guid id) { Calls++; return "status"; }
        public string ChangeSpeed(int speed) { Calls++; Speed = speed; return "speed"; }
    }
}
