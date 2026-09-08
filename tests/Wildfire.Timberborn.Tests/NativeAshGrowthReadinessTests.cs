using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Wildfire.Core;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeAshGrowthReadinessTests
{
    [Theory]
    [InlineData("system")]
    [InlineData("revision")]
    [InlineData("reader")]
    [InlineData("unload")]
    [InlineData("poison")]
    public void ReaderCallbackCannotPublishFieldsUnderChangedWorldAuthority(string change)
    {
        var f = new Fixture();
        f.OnTransport = () =>
        {
            if (change == "system") f.ReplaceSystem();
            else if (change == "revision") f.Revise();
            else if (change == "reader") f.ReplaceReaders();
            else if (change == "unload") f.Call(f.Initialization, "Unload");
            else f.Call(f.Resources, "InvalidateAfterLifecycleFailure");
        };
        Assert.False(f.ObserveField());
        Assert.Null(f.Field("_fireObservation"));
    }

    [Fact]
    public void RequestsUseCurrentTransportAndReuseOneImmutableDerivationPerRevision()
    {
        var f = new Fixture();
        var first = f.Read()!;
        Assert.Single(f.Requests(first));
        Assert.Equal(1.1f, f.RequestValue<float>(first, "GrowthMultiplier"));
        f.Transport[0] = 0; // A returned reader array cannot mutate the already derived request copy.
        Assert.Same(first, f.Read());
        Assert.Equal(1, f.Reads);
        Assert.Single(f.Requests(first));
        f.Revise();
        var empty = f.Read()!;
        Assert.NotSame(first, empty);
        Assert.Empty(f.Requests(empty));
        Assert.Equal(2, f.Reads);
        f.Transport[0] = new WildfireTransportFieldState(0, 0, 0, 1, 0, false).Pack();
        f.ReplaceSystem(); // Same numeric revision, different current native world.
        var replacement = f.Read()!;
        Assert.NotSame(empty, replacement);
        Assert.Equal(1, f.RequestValue<int>(replacement, "Strength"));
        Assert.Equal(1f + .1f / 3, f.RequestValue<float>(replacement, "GrowthMultiplier"));
        Assert.Equal(3, f.Reads);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 1)]
    public void EmptyAndTaintedTransportExcludeGrowthDespiteRestoredFertileAsh(byte ash, byte taint)
    {
        var f = new Fixture();
        var service = f.New("Ash.TimberbornAshFieldService", new object?[] { null });
        f.Call(service, "SyncFromTransportFields", 0u, f.Transport, 0);
        var saved = f.Call(service, "SaveSnapshot")!;
        f.Call(service, "RestoreSnapshot", 0u, saved, 0);
        Fixture.Set(f.Runtime, "_ashFieldService", service);
        f.Transport = [new WildfireTransportFieldState(0, 0, 0, ash, taint, false).Pack()];
        Assert.Empty(f.Requests(f.Read()!));
    }

    [Fact]
    public void GlobalDisableDeclinesEvenWithCachedCleanFieldsWithoutDiscardingWorldReadiness()
    {
        var f = new Fixture();
        var first = f.Read();
        f.Enabled = false;
        Assert.Null(f.Read());
        Assert.True(f.ObserveField()); // Field observation itself has no response-policy dependency.
        f.Enabled = true;
        Assert.Same(first, f.Read());
        Assert.Equal(1, f.Reads);
    }

    [Fact]
    public void DisableDuringFieldReadCannotPublishGrowthRequests()
    {
        var f = new Fixture();
        f.OnTransport = () => f.Enabled = false;
        Assert.Null(f.Read());
        Assert.Null(f.Field("_ashGrowthObservation"));
        f.OnTransport = null;
        f.Enabled = true;
        Assert.NotNull(f.Read());
        Assert.Equal(1, f.Reads); // Successfully published field remains valid, policy was separately unavailable.
    }

    [Theory]
    [InlineData("Unloaded")]
    [InlineData("WaitingForWorld")]
    [InlineData("Initializing")]
    [InlineData("Unsupported")]
    [InlineData("Failed")]
    public void ActualInitializationTransitionsCannotPublishCachedGrowth(string state)
    {
        var f = new Fixture();
        Assert.NotNull(f.Read());
        f.Call(f.Initialization, "Unload");
        if (state == "WaitingForWorld") f.Call(f.Initialization, "Load");
        if (state == "Failed") f.Call(f.Initialization, "Fail", new IOException("native startup"));
        if (state is "Unsupported" or "Initializing")
        {
            f.Call(f.Initialization, "Load");
            f.Call(f.Initialization, "Update", (Func<FireGrid>)(() => state == "Unsupported" ? new(int.MaxValue, 1, 1) : new(1, 1, 1)),
                (Func<bool>)(() => true), (Action<FireGrid>)(_ => Assert.Null(f.Read())));
        }
        if (state != "Initializing") Assert.Null(f.Read());
        Assert.Equal(1, f.Reads);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MalformedCurrentFieldLengthsRejectReadWithoutNativeMutationOrSavePoison(bool cells)
    {
        var f = new Fixture();
        if (cells) f.Cells = []; else f.Transport = [];
        Assert.Contains("complete current-world", Assert.Throws<TargetInvocationException>(() => f.Read()).GetBaseException().Message);
        f.Call(f.Resources, "ThrowIfSaveUnsafe");
        Assert.Null(f.Field("_ashGrowthObservation"));
    }

    [Fact]
    public void UnusedAshBitCannotBecomeACachedGrowthRequest()
    {
        var f = new Fixture();
        f.Transport = [4u << 9]; // Bit 11 is unused; legitimate Core/HLSL outputs saturate ash at 3.
        Assert.Contains("simulator application range", Assert.Throws<TargetInvocationException>(() => f.Read()).GetBaseException().Message);
        Assert.Null(f.Field("_ashGrowthObservation"));
        f.Call(f.Resources, "ThrowIfSaveUnsafe");
    }

    [Fact]
    public void ReaderCannotSaveOrMutateInsidePreparationAndCaughtLifecycleFailureRejectsCapture()
    {
        var f = new Fixture();
        f.OnTransport = () =>
        {
            Assert.Throws<TargetInvocationException>(() => f.Call(f.Resources, "ThrowIfSaveUnsafe"));
            Assert.Throws<TargetInvocationException>(() => f.Call(f.Resources, "TransferInventory", (Action)(() => throw new Exception("must not run"))));
            f.Call(f.Resources, "InvalidateAfterLifecycleFailure");
        };
        Assert.Throws<TargetInvocationException>(() => f.Read());
        Assert.Null(f.Field("_fireObservation"));
        Assert.Null(f.Field("_ashGrowthObservation"));
    }

    [Theory]
    [InlineData("starting")]
    [InlineData("unfinished")]
    [InlineData("thread")]
    [InlineData("late_change")]
    public void NativeSettledProviderRejectsWrongWindowBeforeReturningRequests(string change)
    {
        var f = new Fixture();
        var ticksType = NativeManagedTestContext.ProxyContracts.LoadNative("Timberborn.TickSystem")
            .GetType("Timberborn.TickSystem.TickableSingletonService")!;
        var ticks = RuntimeHelpers.GetUninitializedObject(ticksType);
        Fixture.Set(ticks, "<ParalleTicklIsFinished>k__BackingField", true);
        var provider = Activator.CreateInstance(f.M("Ash.TimberbornAshGrowthReadiness"), Fixture.Flags, null, [f.Runtime, ticks], null)!;
        Assert.NotNull(f.Call(provider, "Read"));
        f.Revise();
        if (change == "starting") Fixture.Set(ticks, "<IsStartingParallelTick>k__BackingField", true);
        if (change == "unfinished") Fixture.Set(ticks, "<ParalleTicklIsFinished>k__BackingField", false);
        if (change == "late_change") f.OnTransport = () => Fixture.Set(ticks, "<ParalleTicklIsFinished>k__BackingField", false);
        Action read = () => f.Call(provider, "Read");
        Exception? error = null;
        if (change == "thread")
        {
            var thread = new Thread(() => error = Record.Exception(read));
            thread.Start(); thread.Join();
        }
        else error = Record.Exception(read);
        Assert.Contains("settled native", error!.GetBaseException().Message);
        Assert.Equal(change == "late_change" ? 2 : 1, f.Reads);
        f.Call(f.Resources, "ThrowIfSaveUnsafe");
    }

    private sealed class Fixture
    {
        private readonly NativeManagedTestContext _native = NativeManagedTestContext.ProxyContracts;
        internal readonly object Runtime, Resources, Initialization;
        internal ushort[] Cells = [0];
        internal uint[] Transport = [new WildfireTransportFieldState(0, 0, 0, 3, 0, false).Pack()];
        internal Action? OnTransport;
        internal int Reads;
        internal bool Enabled = true;
        internal Fixture()
        {
            Runtime = RuntimeHelpers.GetUninitializedObject(M("Runtime.TimberbornFireRuntime"));
            Resources = New("Resources.NativeResourceCoordinator");
            Initialization = New("Runtime.TimberbornRuntimeInitialization");
            Set(Runtime, "_resources", Resources);
            Set(Runtime, "<Initialization>k__BackingField", Initialization);
            Call(Initialization, "Load");
            Call(Initialization, "Update", (Func<FireGrid>)(() => new(1, 1, 1)), (Func<bool>)(() => true), (Action<FireGrid>)(_ => { }));
            var store = Proxy("Settings.IWildfireReleaseSettingsStore", (m, a) =>
            {
                if (m.Name == "get_SourceName") return "native readiness fixture";
                var result = M("Settings.WildfireReleaseSettingReadResult");
                return (string)a[0]! == "JasonKleinberg.Wildfire.release.wildfire_enabled"
                    ? result.GetMethod("Valid")!.Invoke(null, [Enabled ? 1 : 0, null])
                    : result.GetMethod("Missing")!.Invoke(null, null);
            });
            Set(Runtime, "_releaseSettings", New("Settings.WildfireReleaseSettings", store));
            ReplaceSystem(); ReplaceReaders();
        }
        internal void ReplaceSystem() => Set(Runtime, "_fireSystem", New("Runtime.TimberbornFireSystem", new TimberbornIncompleteDispatchTests.Simulator()));
        internal void ReplaceReaders()
        {
            Set(Runtime, "_observedCellsReader", Proxy("Simulation.ITimberbornCellFieldReader", (_, _) => Cells));
            Set(Runtime, "_observedTransportReader", Proxy("Simulation.ITimberbornTransportFieldReader", (_, _) =>
            { Reads++; OnTransport?.Invoke(); return Transport; }));
        }
        internal object? Read() => Resources.GetType().GetMethod("CaptureAtRest")!.MakeGenericMethod(typeof(object))
            .Invoke(Resources, [(Func<object?>)(() => Call(Runtime, "ReadAshGrowthObservation"))]);
        internal object[] Requests(object observation) => ((IEnumerable)observation.GetType().GetProperty("Requests", Flags)!.GetValue(observation)!).Cast<object>().ToArray();
        internal T RequestValue<T>(object observation, string property) => (T)Requests(observation).Single().GetType().GetProperty(property)!.GetValue(Requests(observation).Single())!;
        internal bool ObserveField()
        {
            object?[] args = [null];
            return (bool)Runtime.GetType().GetMethod("TryObserveFireField", Flags)!.Invoke(Runtime, args)!;
        }
        internal void Revise() => Set(Resources, "<FieldRevision>k__BackingField", (long)Resources.GetType().GetProperty("FieldRevision")!.GetValue(Resources)! + 1);
        internal object? Field(string name) => Runtime.GetType().GetField(name, Flags)!.GetValue(Runtime);
        internal object? Call(object target, string name, params object?[] args) => target.GetType().GetMethod(name, Flags)!.Invoke(target, args);
        internal Type M(string name) => _native.LoadMod().GetType("Wildfire.Timberborn." + name)!;
        internal object New(string name, params object?[] args) => Activator.CreateInstance(M(name), args)!;
        internal object Proxy(string name, Func<MethodInfo, object?[], object?> action) => NativePersistenceProxy.Create(M(name), action);
        internal static void Set(object target, string name, object? value) => target.GetType().GetField(name, Flags)!.SetValue(target, value);
        internal const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    }
}
