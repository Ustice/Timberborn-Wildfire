using System.Reflection;
using System.Runtime.CompilerServices;
using Wildfire.Core;

namespace Wildfire.Timberborn.Tests;

// Supplied runtime/load state isolates QA sequencing. This is not a game load fixture.
public sealed class NativeQaSessionLifecycleTests
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void PendingRequestRejectsReplacementAndUnloadCancelsWithoutWriting()
    {
        using var f = new Fixture(); var id = Guid.NewGuid();
        Assert.Contains("save_state=queued", f.Queue(id));
        Assert.Throws<TargetInvocationException>(() => f.Queue(Guid.NewGuid()));
        f.Call("Unload"); f.Call("Update");
        Assert.Contains("save_state=failed", f.Call("SaveStatus", id)!.ToString());
        Assert.Empty(Directory.GetFiles(f.SettlementPath));
        Assert.Throws<TargetInvocationException>(() => f.Call("ChangeSpeed", 1));
        Assert.Throws<TargetInvocationException>(() => f.Call("ChangeSpeed", 0));
    }

    [Fact]
    public void ChangedLoadedSaveRejectsBeforeTickerOrWriterAndCannotRetrySameId()
    {
        using var f = new Fixture(); var id = Guid.NewGuid(); f.Queue(id);
        f.Loaded.GetType().GetProperty("SaveName")!.SetValue(f.Loaded, "another-save");
        f.Call("Update"); // Ticker and writer intentionally null: rejection must precede either.
        Assert.Contains("loaded_save_changed", f.Call("SaveStatus", id)!.ToString()!.Replace(" ", "_").ToLowerInvariant());
        Assert.Empty(Directory.GetFiles(f.SettlementPath));
        Assert.Throws<TargetInvocationException>(() => f.Queue(id));
    }

    [Fact]
    public void PauseRemainsAvailableWhileUnsafeOrNonreadyResumeAndSaveRefuse()
    {
        using var f = new Fixture();
        f.Initialization.GetType().GetMethod("Unload")!.Invoke(f.Initialization, null);
        Assert.Throws<TargetInvocationException>(() => f.Queue(Guid.NewGuid()));
        Assert.Throws<TargetInvocationException>(() => f.Call("ChangeSpeed", 1));
        f.Call("ChangeSpeed", 0);
        Assert.Equal(0f, f.Speed.GetType().GetField("_nextSpeed", Hidden)!.GetValue(f.Speed));
        f.Ready();
        f.Call("ChangeSpeed", 1);
        Assert.Equal(1f, f.Speed.GetType().GetField("_nextSpeed", Hidden)!.GetValue(f.Speed));
        var failure = new InvalidOperationException("partial native mutation");
        Assert.Throws<TargetInvocationException>(() => f.Resources.GetType().GetMethod("TransferInventory")!
            .Invoke(f.Resources, new object[] { (Action)(() => throw failure) }));
        Assert.Throws<TargetInvocationException>(() => f.Queue(Guid.NewGuid()));
        Assert.Throws<TargetInvocationException>(() => f.Call("ChangeSpeed", 1));
        f.Call("ChangeSpeed", 0);
        Assert.Equal(0f, f.Speed.GetType().GetField("_nextSpeed", Hidden)!.GetValue(f.Speed));
    }

    [Fact]
    public void ActualNativeSpeedQueuesWithoutBypassingExistingLock()
    {
        using var f = new Fixture();
        f.Speed.GetType().GetMethod("ChangeAndLockSpeed")!.Invoke(f.Speed, new object[] { 0f });
        f.Call("ChangeSpeed", 1);
        var next = f.Speed.GetType().GetField("_nextSpeed", Hidden)!;
        Assert.Equal(0f, next.GetValue(f.Speed)); // Actual native lock rejects requested one.
        Assert.Equal(0f, f.Speed.GetType().GetProperty("CurrentSpeed")!.GetValue(f.Speed));
        f.Speed.GetType().GetMethod("UnlockSpeed")!.Invoke(f.Speed, null);
        f.Call("ChangeSpeed", 1);
        Assert.Equal(1f, next.GetValue(f.Speed));
        f.Speed.GetType().GetMethod("ChangeAndLockSpeed")!.Invoke(f.Speed, new object[] { 1f });
        f.Call("ChangeSpeed", 0);
        Assert.Equal(1f, next.GetValue(f.Speed)); // Emergency pause still honors native lock.
        Assert.Equal(0f, f.Speed.GetType().GetProperty("CurrentSpeed")!.GetValue(f.Speed));
        // LateUpdate calls Unity Time.timeScale before CurrentSpeed publication; actual
        // frame application is an engine boundary, not replaced with a managed shim.

    }

    private sealed class Fixture : IDisposable
    {
        private readonly NativeManagedTestContext _native = new();
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "wildfire-qa-session-test-" + Guid.NewGuid());
        internal string SettlementPath => Path.Combine(_directory, "CopiedSettlement");
        internal object Loaded, Initialization, Resources, Speed;
        private readonly object _session;
        internal Fixture()
        {
            Directory.CreateDirectory(SettlementPath);
            Type T(string assembly, string name) => _native.LoadNative(assembly).GetType(assembly + "." + name)!;
            object New(string assembly, string name, params object?[] args) => Activator.CreateInstance(T(assembly, name), args)!;
            var mod = _native.LoadMod();
            var runtimeType = mod.GetTypes().Single(t => t.Name == "TimberbornFireRuntime");
            var runtime = RuntimeHelpers.GetUninitializedObject(runtimeType);
            Initialization = Activator.CreateInstance(mod.GetTypes().Single(t => t.Name == "TimberbornRuntimeInitialization"))!;
            runtimeType.GetField("<Initialization>k__BackingField", Hidden)!.SetValue(runtime, Initialization);
            Ready();
            Resources = Activator.CreateInstance(mod.GetTypes().Single(t => t.Name == "NativeResourceCoordinator"))!;
            var settlement = New("Timberborn.GameSaveRepositorySystem", "SettlementReference", "CopiedSettlement", _directory);
            Loaded = New("Timberborn.GameSaveRepositorySystem", "SaveReference", "Original", settlement);
            var loader = New("Timberborn.GameSaveRuntimeSystem", "GameLoader", null, null);
            loader.GetType().GetProperty("LoadedSave")!.SetValue(loader, Loaded);
            // FileService constructor queries Unity platform permissions. Only its pure native
            // CombineIntoPath is exercised here; no permission state is supplied or queried.
            var repository = New("Timberborn.GameSaveRepositorySystem", "GameSaveRepository",
                RuntimeHelpers.GetUninitializedObject(T("Timberborn.FileSystem", "FileService")), New("Timberborn.FileSystem", "FilenameValidator"), null, null);
            Speed = New("Timberborn.TimeSystem", "SpeedManager", New("Timberborn.SingletonSystem", "EventBus"));
            _session = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.Qa.TimberbornQaSession")!,
                loader, repository, null, null, null, Speed, runtime, Resources)!;
        }
        internal void Ready()
        {
            Initialization.GetType().GetMethod("Load")!.Invoke(Initialization, null);
            Initialization.GetType().GetMethod("Update")!.Invoke(Initialization,
                new object[] { (Func<FireGrid>)(() => new FireGrid(1, 1, 1)), (Func<bool>)(() => true), (Action<FireGrid>)(_ => { }) });
        }
        internal string Queue(Guid id) => (string)Call("QueueSave", id, "QA-copy")!;
        internal object? Call(string method, params object?[] args) => _session.GetType()
            .GetMethod(method, Hidden | BindingFlags.Public)!.Invoke(_session, args);
        public void Dispose() { Directory.Delete(_directory, true); _native.Dispose(); }
    }
}
