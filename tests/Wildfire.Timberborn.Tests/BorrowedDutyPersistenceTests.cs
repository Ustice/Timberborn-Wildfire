using System.Reflection;

namespace Wildfire.Timberborn.Tests;

public sealed class BorrowedDutyPersistenceTests
{
    // DispatchProxy caches generated interface implementations; use one native type identity.
    private static readonly NativeManagedTestContext Native = new(collectible: false);
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ExecutorRoundTripDoesNotNeedOptInMovementOrEmploymentServices(int phase)
    {
        var native = Native;
        var mod = native.LoadMod();
        var type = mod.GetType("Wildfire.Timberborn.FireBell.BorrowedDutyExecutor")!;
        var source = Activator.CreateInstance(type, new object?[5])!;
        var progressField = type.GetField("_progress", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var progress = progressField.GetValue(source)!;
        progress.GetType().GetMethod("Restore")!.Invoke(progress, new object[] { phase, .75f, true });
        var values = new Dictionary<object, object?>();
        var persistence = native.LoadNative("Timberborn.Persistence");
        var world = native.LoadNative("Timberborn.WorldPersistence");
        object ObjectProxy(string name) => NativePersistenceProxy.Create(persistence.GetType("Timberborn.Persistence." + name)!, (method, args) =>
        {
            if (method.Name == "Set") { values[args[0]!] = args[1]; return null; }
            if (method.Name == "Has") return values.ContainsKey(args[0]!);
            if (method.Name == "Get") return values[args[0]!];
            throw new NotSupportedException(method.Name);
        });
        var saver = NativePersistenceProxy.Create(world.GetType("Timberborn.WorldPersistence.IEntitySaver")!, (_, _) => ObjectProxy("IObjectSaver"));
        type.GetMethod("Save")!.Invoke(source, new[] { saver });
        var loader = NativePersistenceProxy.Create(world.GetType("Timberborn.WorldPersistence.IEntityLoader")!, (_, _) => ObjectProxy("IObjectLoader"));
        var restored = Activator.CreateInstance(type, new object?[5])!;
        type.GetMethod("Load")!.Invoke(restored, new[] { loader });
        var restoredProgress = progressField.GetValue(restored)!;
        Assert.Equal(phase, Convert.ToInt32(type.GetProperty("Phase")!.GetValue(restored)));
        Assert.Equal(.75f, restoredProgress.GetType().GetProperty("Hours")!.GetValue(restoredProgress));
        Assert.Equal(true, restoredProgress.GetType().GetProperty("CancellationRequested")!.GetValue(restoredProgress));
        Assert.Equal(phase != 0, type.GetField("_restored", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(restored));
        // All movement, worker, fixture and native resource dependencies are deliberately absent.
        // Idle execution must remain a no-op; active execution must await manager-owned native Tick.
        if (phase == 0) Assert.Equal("Success", type.GetMethod("Tick")!.Invoke(restored, new object[] { .01f })!.ToString());
    }
}

public class NativePersistenceProxy : DispatchProxy
{
    private Func<MethodInfo, object?[], object?> _call = null!;
    internal static object Create(Type contract, Func<MethodInfo, object?[], object?> call)
    {
        var proxy = (NativePersistenceProxy)Create(contract, typeof(NativePersistenceProxy));
        proxy._call = call; return proxy;
    }
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => _call(targetMethod!, args ?? Array.Empty<object?>());
}
