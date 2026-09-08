using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class TimberbornFireWalkTests
{
    [Fact]
    public void RejectedInstalledPrefixPausesInsideLaunchAndStopsOnlyAfterNativeReturn()
    {
        var f = new Fixture { Safe = false };
        f.DuringLaunch = () =>
        {
            f.RaisePath();
            Assert.Equal(new[] { "launch", "check:Outbound", "pause" }, f.Trace);
        };
        Assert.False(f.Launch());
        Assert.Equal(new[] { "launch", "check:Outbound", "pause", "returned", "stop" }, f.Trace);
    }

    [Theory]
    [InlineData("Outbound", "Running")]
    [InlineData("Escape", "Running")]
    [InlineData("Outbound", "Success")]
    public void SuccessfulLaunchUsesCurrentModeAndReleasesOnlyAfterReturn(string mode, string status)
    {
        var f = new Fixture { Mode = mode, LaunchStatus = status };
        f.DuringLaunch = f.RaisePath;
        Assert.True(f.Launch());
        Assert.Equal(new[] { "launch", "check:" + mode, "returned", "release" }, f.Trace);
    }

    [Fact]
    public void IgnoredPhaseDoesNotObserveOrPauseAnUnrelatedNativePath()
    {
        var f = new Fixture { Mode = "Ignore", Safe = false };
        f.RaisePath();
        Assert.Empty(f.Trace);
        Assert.Equal(0, f.RevisionReads);
    }

    [Fact]
    public void RevisionRefreshAndUnsolicitedEventsShareValidationButCallerOwnsStop()
    {
        var f = new Fixture();
        f.RaisePath();
        f.Trace.Clear();
        Assert.True(f.Refresh());
        Assert.Empty(f.Trace);
        f.Revision++;
        f.Safe = false;
        Assert.False(f.Refresh());
        Assert.Equal(new[] { "refresh", "check:Outbound", "pause", "refreshed" }, f.Trace);
        f.Call("Stop");
        Assert.Equal("stop", f.Trace.Last());
        f.Trace.Clear();
        f.Safe = true;
        f.RaisePath(); // Native NavMeshObserver can publish a path outside our Refresh call.
        Assert.True(f.Refresh());
        Assert.Equal(new[] { "check:Outbound" }, f.Trace);
    }

    [Fact]
    public void StoppedWalkerIsNotRefreshedAndNativeFailureStillStops()
    {
        var f = new Fixture { Stopped = true, LaunchStatus = "Failure" };
        Assert.True(f.Refresh());
        Assert.Empty(f.Trace);
        Assert.False(f.Launch());
        Assert.Equal(new[] { "launch", "returned", "stop" }, f.Trace);
    }

    [Fact]
    public void NavigationExceptionPropagatesWithoutInventingSuccessOrExtraCleanup()
    {
        var f = new Fixture();
        var failure = new InvalidOperationException("native path subscriber failed");
        f.DuringLaunch = () => throw failure;
        Assert.Same(failure, Assert.Throws<TargetInvocationException>(() => f.Launch()).InnerException);
        Assert.Equal(new[] { "launch" }, f.Trace);
    }

    [Fact]
    public void DisposeUnsubscribesExactlyOnceAndLeavesOtherNativeSubscribers()
    {
        var f = new Fixture();
        int otherCalls = 0;
        f.AddOtherSubscriber(() => otherCalls++);
        f.Call("Dispose");
        f.Call("Dispose");
        f.RaisePath();
        Assert.Equal(1, f.Subscriptions);
        Assert.Equal(1, f.Unsubscriptions);
        Assert.Equal(1, otherCalls);
        Assert.Empty(f.Trace);
    }

    [Theory]
    [InlineData("FireResponse.WardenExecutor", "_movement")]
    [InlineData("Ash.AshHarvestExecutor", "_ownedWalk")]
    [InlineData("FireBell.BorrowedDutyExecutor", "_movement")]
    public void ActiveRestoreDoesNotTouchWalkingUntilItsOwnedTick(string name, string walkField)
    {
        var f = new Fixture();
        var type = f.Mod.GetType("Wildfire.Timberborn." + name)!;
        var executor = Activator.CreateInstance(type, new object?[5])!;
        type.GetField(walkField, Flags)!.SetValue(executor, f.Helper);
        object Key(string name) => type.GetField(name, Flags)!.GetValue(null)!;
        object phaseKey = Key("PhaseKey"), hoursKey = Key("HoursKey");
        var objectLoader = NativePersistenceProxy.Create(f.Native.LoadNative("Timberborn.Persistence")
            .GetType("Timberborn.Persistence.IObjectLoader")!, (method, args) =>
        {
            if (method.Name == "Has") return false;
            if (method.Name != "Get") throw new NotSupportedException(method.Name);
            if (Equals(args[0], phaseKey)) return 1;
            if (Equals(args[0], hoursKey)) return .25f;
            return Activator.CreateInstance(method.ReturnType);
        });
        var loader = NativePersistenceProxy.Create(f.Native.LoadNative("Timberborn.WorldPersistence")
            .GetType("Timberborn.WorldPersistence.IEntityLoader")!, (_, _) => objectLoader);
        type.GetMethod("Load")!.Invoke(executor, [loader]);
        Assert.Equal(1, Convert.ToInt32(type.GetProperty("Phase")!.GetValue(executor)));
        Assert.Empty(f.Trace);
    }

    internal sealed class Fixture
    {
        internal NativeManagedTestContext Native { get; } = NativeManagedTestContext.ProxyContracts;
        internal Assembly Mod { get; }
        internal object Helper { get; }
        internal List<string> Trace { get; } = new();
        internal string Mode = "Outbound", LaunchStatus = "Running";
        internal bool Safe = true, Stopped;
        internal long Revision = 5;
        internal int RevisionReads, Subscriptions, Unsubscriptions;
        internal Action? DuringLaunch, DuringRelease;
        internal object? CurrentDestination, Endpoint;
        internal string TickStatus = "Running";
        internal bool NearEndpoint = true, CandidateSafe = true;
        private Delegate? _pathStarted;
        private readonly Type _eventType;
        private readonly object _position;
        internal Fixture()
        {
            Mod = Native.LoadMod();
            var type = Mod.GetType("Wildfire.Timberborn.FireSafety.TimberbornFireWalk");
            Assert.NotNull(type);
            var contract = Mod.GetType("Wildfire.Timberborn.FireSafety.ITimberbornFireWalkDriver")!;
            _eventType = contract.GetEvent("PathStarted")!.EventHandlerType!;
            var driver = NativePersistenceProxy.Create(contract, (method, args) => method.Name switch
            {
                "add_PathStarted" => Subscribe((Delegate)args[0]!),
                "remove_PathStarted" => Unsubscribe((Delegate)args[0]!),
                "get_Revision" => ReadRevision(),
                "get_Stopped" => Stopped,
                "IsInstalledPathSafe" => Check((bool)args[0]!),
                "Launch" => NativeLaunch(method.ReturnType, args[0]),
                "get_CurrentDestination" => CurrentDestination,
                "get_InstalledEndpoint" => Endpoint,
                "IsCandidateSafe" => CandidateSafe,
                "AtSafeEndpoint" => NearEndpoint,
                "Refresh" => RefreshDriver(),
                "Stop" => Record("stop"),
                "Pause" => Record("pause"),
                "ReleasePause" => Release(),
                "Tick" => Enum.Parse(method.ReturnType, TickStatus),
                _ => throw new NotSupportedException(method.Name)
            });
            var modeType = Mod.GetType("Wildfire.Timberborn.FireSafety.FireWalkMode")!;
            Func<object> mode = () => Enum.Parse(modeType, Mode);
            var currentMode = Expression.Lambda(typeof(Func<>).MakeGenericType(modeType),
                Expression.Convert(Expression.Invoke(Expression.Constant(mode)), modeType)).Compile();
            Helper = Activator.CreateInstance(type!, Flags, null, [driver, currentMode], null)!;
            _position = Activator.CreateInstance(Native.LoadNative("UnityEngine.CoreModule").GetType("UnityEngine.Vector3")!)!;
        }
        internal object? Call(string method, params object[] args) => Helper.GetType().GetMethods(Flags).Single(m => m.Name == method && m.GetParameters().Length == args.Length &&
            m.GetParameters().Select((p, i) => p.ParameterType.IsInstanceOfType(args[i])).All(matches => matches)).Invoke(Helper, args);
        internal bool Launch() => (bool)Call("Launch", _position)!;
        internal bool Refresh() => (bool)Call("RefreshIfNeeded")!;
        internal void RaisePath() => _pathStarted?.DynamicInvoke(null, null);
        internal void AddOtherSubscriber(Action action)
        {
            var parameters = _eventType.GetMethod("Invoke")!.GetParameters()
                .Select(p => Expression.Parameter(p.ParameterType)).ToArray();
            var callback = Expression.Lambda(_eventType, Expression.Invoke(Expression.Constant(action)), parameters).Compile();
            _pathStarted = Delegate.Combine(_pathStarted, callback);
        }
        private object? Subscribe(Delegate action) { Subscriptions++; _pathStarted = Delegate.Combine(_pathStarted, action); return null; }
        private object? Unsubscribe(Delegate action) { Unsubscriptions++; _pathStarted = Delegate.Remove(_pathStarted, action); return null; }
        private long ReadRevision() { RevisionReads++; return Revision; }
        private bool Check(bool escaping) { Trace.Add("check:" + (escaping ? "Escape" : "Outbound")); return Safe; }
        private object NativeLaunch(Type statusType, object? destination)
        {
            Trace.Add("launch");
            DuringLaunch?.Invoke();
            if (destination?.GetType().Name == "PositionDestination")
                CurrentDestination = LaunchStatus == "Running" ? destination : null;
            Trace.Add("returned");
            return Enum.Parse(statusType, LaunchStatus);
        }
        internal object NewDestination() => RuntimeHelpers.GetUninitializedObject(Native.LoadNative("Timberborn.WalkingSystem")
            .GetType("Timberborn.WalkingSystem.PositionDestination")!);
        internal object Position(float x) => Activator.CreateInstance(_position.GetType(), x, 0f, 0f)!;
        internal bool LaunchDestination(object destination) => (bool)Call("Launch", destination)!;
        internal bool Arrived(object destination) => (bool)Call("HasArrivedAt", destination)!;
        private object? Release() { Trace.Add("release"); DuringRelease?.Invoke(); return null; }
        private object? RefreshDriver() { Trace.Add("refresh"); RaisePath(); return Record("refreshed"); }
        private object? Record(string value) { Trace.Add(value); return null; }
    }
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
}
