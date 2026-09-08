using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeRuntimeIncompleteDispatchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EscapedRuntimeSynchronizationFailureBlocksActualSaveAndNextDispatch(bool warningAlsoFails)
    {
        var f = new Fixture(synchronizationAlreadyComplete: false);
        f.FailWarnings = warningAlsoFails;
        var error = Assert.Throws<TargetInvocationException>(f.Dispatch);
        Assert.Contains("transport field observations", error.InnerException!.Message);
        Assert.Equal(1, f.Simulator.Swaps);
        Assert.True(f.Poisoned);
        var save = Assert.Throws<TargetInvocationException>(f.SaveAdmission);
        Assert.IsType<InvalidOperationException>(save.InnerException); // Existing resource guard runs before saver validation.
        Assert.Throws<TargetInvocationException>(f.Dispatch);
        Assert.Equal(1, f.Simulator.Swaps);
    }

    [Fact]
    public void RuntimeObservationalLogAfterReturnedFollowupsDoesNotPoisonNativeSaveAdmission()
    {
        var f = new Fixture(synchronizationAlreadyComplete: true);
        f.FailFinalRuntimeLog = true;
        var error = Assert.Throws<TargetInvocationException>(f.Dispatch);
        Assert.IsType<IOException>(error.InnerException);
        Assert.Equal(1, f.Simulator.Swaps);
        Assert.False(f.Poisoned);
        Assert.DoesNotContain(f.Warnings, message => !message.StartsWith("wildfire_timberborn_runtime_dispatch_failed"));
        Assert.Contains(f.Messages, message => message.StartsWith("wildfire_timberborn_beaver_field_exposure"));
        var save = Assert.Throws<TargetInvocationException>(f.SaveAdmission);
        Assert.IsType<ArgumentNullException>(save.InnerException); // Guard admitted save; this fixture supplies no actual saver.
    }

    private sealed class Fixture
    {
        private readonly NativeManagedTestContext _native = NativeManagedTestContext.ProxyContracts;
        internal readonly TimberbornIncompleteDispatchTests.Simulator Simulator = new();
        private readonly object _runtime;
        private readonly object _resources;
        private long _update;
        internal bool FailFinalRuntimeLog;
        internal bool FailWarnings;
        internal List<string> Messages = new();
        internal List<string> Warnings = new();
        internal bool Poisoned => (bool)_resources.GetType().GetProperty("IsIndeterminate")!.GetValue(_resources)!;

        internal Fixture(bool synchronizationAlreadyComplete)
        {
            _resources = New("Resources.NativeResourceCoordinator");
            Call(_resources, "Attach", Simulator);
            var system = New("Runtime.TimberbornFireSystem", Simulator);
            system.GetType().GetProperty("HostDispatch", Flags)!.SetValue(system, _resources);
            var dispatcher = New("Runtime.TimberbornFixedCadenceFireDispatcher", system);
            _runtime = RuntimeHelpers.GetUninitializedObject(M("Runtime.TimberbornFireRuntime"));
            Set(_runtime, "_resources", _resources);
            Set(_runtime, "_fireSystem", system);
            Set(_runtime, "_dispatcher", dispatcher);
            var log = Proxy("Runtime.ITimberbornFireLogSink", (method, args) =>
            {
                string message = (string)args[0]!;
                if (method.Name == "Warning")
                {
                    Warnings.Add(message);
                    if (FailWarnings) throw new IOException("failure diagnostic");
                }
                else Messages.Add(message);
                if (FailFinalRuntimeLog && message.StartsWith("wildfire_timberborn_runtime_dispatched"))
                    throw new IOException("runtime observation after completion");
                return null;
            });
            Set(_runtime, "_logSink", log);
            var clock = NativePersistenceProxy.Create(_native.LoadNative("Timberborn.TimeSystem")
                .GetType("Timberborn.TimeSystem.IDayNightCycle")!, (method, _) =>
                method.Name == "get_DayNumber" ? 0 : throw new InvalidOperationException(method.Name));
            Set(_runtime, "_dayNightCycle", clock);
            var ash = New("Ash.TimberbornAshFieldService", null, log);
            Set(_runtime, "_ashFieldService", ash);
            var synchronizer = New("Ash.TimberbornAshFieldSynchronizer", ash);
            // Test successful followup completion with the already-synchronized current tick;
            // no fake native/GPU transport read or Unity renderer is supplied.
            if (synchronizationAlreadyComplete) Set(synchronizer, "_lastSyncTick", (uint?)1);
            Set(_runtime, "_ashFieldSynchronizer", synchronizer);
            Set(_runtime, "_ashWaterWashoutService", New("Ash.TimberbornAshWaterWashoutService", null, log));
            Set(_runtime, "_taintedAshSoilPoisoningService", New("Ash.TimberbornTaintedAshSoilPoisoningService", null, log));
            Set(_runtime, "_fertileAshCollectionService", New("Ash.TimberbornFertileAshCollectionService", null, log));
            var positions = Proxy("Beavers.ITimberbornBeaverPositionProvider", (_, _) =>
                M("Beavers.TimberbornBeaverPositionSnapshot").GetMethod("Unavailable")!.Invoke(null, ["fixture-no-native-positions"]));
            var visual = Proxy("Visuals.ITimberbornGpuVisualFieldSurface", (method, _) => throw new InvalidOperationException(method.Name));
            Set(_runtime, "_beaverFieldExposureTelemetry", New("Beavers.TimberbornBeaverFieldExposureTelemetry", positions, visual, log, false));
            var actuator = Proxy("Beavers.ITimberbornBeaverFieldBehaviorActuator", (method, _) => throw new InvalidOperationException(method.Name));
            Set(_runtime, "_beaverFieldBehaviorDispatcher", New("Beavers.TimberbornBeaverFieldBehaviorDispatcher", actuator, log, null));
            var notifications = Proxy("Alerts.ITimberbornPlayerNotificationSink", (method, _) => throw new InvalidOperationException(method.Name));
            Set(_runtime, "_playerFireAlerts", New("Alerts.TimberbornPlayerFireAlertSink", notifications, log, 1u));
        }

        internal void Dispatch() => Call(_runtime, "DispatchFireUpdate",
            New("Runtime.TimberbornFireUpdate", ++_update, TimeSpan.FromSeconds(1)));
        internal void SaveAdmission() => Call(_runtime, "Save", new object?[] { null });
        private object Proxy(string name, Func<MethodInfo, object?[], object?> method) => NativePersistenceProxy.Create(M(name), method);
        private Type M(string name) => _native.LoadMod().GetType("Wildfire.Timberborn." + name)!;
        private object New(string name, params object?[] args) => Activator.CreateInstance(M(name), args)!;
        private static void Set(object target, string name, object? value) => target.GetType().GetField(name, Flags)!.SetValue(target, value);
        private static object? Call(object target, string name, params object?[] args) => target.GetType().GetMethod(name, Flags)!.Invoke(target, args);
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    }
}
