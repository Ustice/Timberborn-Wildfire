using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed partial class NativeWaterOwnershipTests
{
    [Fact]
    public void ActualNativeLoaderReadsExactSavedWaterAndOwnershipButDoesNotArmEarly()
    {
        using var f = new F(); f.Load();
        var (worldLoader, entities, loader, saved) = f.RestoreStage(marker: true);
        f.Call(f.EntitiesLoader, "Load", entities); // Native loader creates its own exact EntityLoader per component.
        Assert.Equal(.2f, f.Water.Buffer(f.Water.Source));
        Assert.False((bool)f.Property(f.Source, "Armed")!);
        Assert.False((bool)f.Property(f.Source, "Tainted")!);
        Assert.True((bool)NativeShorelineWaterFixture.Get(f.Source, "_restorePermit")!);
        // Refusing an incomplete native initialization consumes the permit; it cannot be reused.
        f.Call(f.Source, "CompleteRestore", false);
        Assert.False((bool)NativeShorelineWaterFixture.Get(f.Source, "_restorePermit")!);
        Assert.True((bool)f.Property(f.Source, "Tainted")!);
        f.Call(f.Boundary, "PostLoad");
        Assert.False((bool)f.Call(f.Boundary, "CanRestore", f.Source, loader)!); // Still inside native list, already closed.
        f.Call(worldLoader, "PostLoadNonSingletons"); // Actual native entity PostLoad then list=null.
        Assert.Null(NativeShorelineWaterFixture.Get(worldLoader, "_instantiatedSerializedEntities"));
        Assert.Null(f.Call(f.Contract, "NativeLoadList", f.Repository));
        Assert.False((bool)f.Call(f.Boundary, "CanRestore", f.Source, loader)!);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingMarkerNeverUpgradesEvenAnEmptyNativeBuffer(bool positive)
    {
        using var f = new F(); f.Load();
        var (_, entities, _, _) = f.RestoreStage(marker: false, amount: positive ? .2f : 0f);
        f.Call(f.EntitiesLoader, "Load", entities);
        Assert.True((bool)f.Property(f.Source, "Tainted")!);
        Assert.False((bool)f.Call(f.Source, "TryArmNewSource")!);
        Assert.Equal(positive ? .2f : 0f, f.Water.Buffer(f.Water.Source));
    }
    [Fact]
    public void CorrectGuidWithDifferentSerializedObjectCannotBorrowNativeLoadMembership()
    {
        using var f = new F(); f.Load();
        var (_, _, _, saved) = f.RestoreStage(marker: true);
        var impostor = Activator.CreateInstance(f.T("Timberborn.WorldSerialization", "Timberborn.WorldSerialization.SerializedEntity"), f.Owner, "Fixture.Intake")!;
        var loader = Activator.CreateInstance(f.T("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.EntityLoader"), impostor)!;
        Assert.False((bool)f.Call(f.Boundary, "CanRestore", f.Source, loader)!);
        Assert.NotSame(saved, impostor);
    }
    [Fact]
    public void MissingNativeWaterComponentCannotAuthorizePositiveSavedMarker()
    {
        using var f = new F(); f.Load();
        var (_, entities, _, _) = f.RestoreStage(marker: true, nativeWater: false);
        f.Call(f.EntitiesLoader, "Load", entities);
        Assert.True((bool)f.Property(f.Source, "Tainted")!);
        Assert.False((bool)NativeShorelineWaterFixture.Get(f.Source, "_restorePermit")!);
    }
    [Fact]
    public void ActualNativeSaverRetainsTaintWithoutAQuantityLedger()
    {
        using var f = new F(); f.Load();
        var (_, entities, _, _) = f.RestoreStage(marker: true, tainted: true);
        f.Call(f.EntitiesLoader, "Load", entities);
        var copy = Activator.CreateInstance(f.T("Timberborn.WorldSerialization", "Timberborn.WorldSerialization.SerializedEntity"), f.Owner, "Fixture.Intake")!;
        var saver = Activator.CreateInstance(f.T("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.EntitySaver"), copy)!;
        f.Call(f.Source, "Save", saver); f.Call(f.Water.Source, "Save", saver);
        var loader = Activator.CreateInstance(f.T("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.EntityLoader"), copy)!;
        var component = f.Call(loader, "GetComponent", f.Key("Wildfire.NaturalWaterOwnership"))!;
        Assert.True((bool)f.Call(component, "Get", f.PropertyKey(typeof(bool), "Tainted"))!);
        Assert.True((bool)f.Call(f.Contract, "SavedBufferPresent", loader)!);
        Assert.Equal(.2f, f.Water.Buffer(f.Water.Source));
    }

    [Theory]
    [InlineData("Tainted")]
    [InlineData("X")]
    [InlineData("Y")]
    [InlineData("Z")]
    public void IncompleteWitnessThrowsAndNeverArms(string missing)
    {
        using var f = new F(); f.Load();
        var (_, entities, _, _) = f.RestoreStage(marker: true, omit: missing);
        Assert.Throws<TargetInvocationException>(() => f.Call(f.EntitiesLoader, "Load", entities));
        Assert.False((bool)f.Property(f.Source, "Armed")!);
        Assert.False((bool)NativeShorelineWaterFixture.Get(f.Source, "_restorePermit")!);
    }
    [Theory]
    [InlineData(2, null)]
    [InlineData(1, "bad guid")]
    public void MalformedPresentWitnessIsNotSilentlyRewritten(int version, string? owner)
    {
        using var f = new F(); f.Load();
        var (_, entities, _, _) = f.RestoreStage(marker: true, version: version, ownerText: owner);
        Assert.Throws<TargetInvocationException>(() => f.Call(f.EntitiesLoader, "Load", entities));
        Assert.False((bool)f.Property(f.Source, "Armed")!);
    }
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(-.2f)]
    public void InvalidNativeSavedBufferCannotEarnOwnership(float amount)
    {
        using var f = new F(); f.Load();
        var (_, entities, _, _) = f.RestoreStage(marker: true, amount: amount);
        f.Call(f.EntitiesLoader, "Load", entities);
        Assert.True((bool)f.Property(f.Source, "Tainted")!);
        Assert.False((bool)NativeShorelineWaterFixture.Get(f.Source, "_restorePermit")!);
    }
    [Fact]
    public void MissingNativeSavedBufferKeyRejectsBeforeArming()
    {
        using var f = new F(); f.Load();
        var (_, entities, _, saved) = f.RestoreStage(marker: true, nativeWater: false);
        var saver = Activator.CreateInstance(f.T("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.EntitySaver"), saved)!;
        var component = f.Call(saver, "GetComponent", NativeShorelineWaterFixture.Get(f.Contract, "_waterKey"))!;
        f.Call(component, "Set", NativeShorelineWaterFixture.Get(f.Contract, "_cleanKey"), .2f);
        Assert.Throws<TargetInvocationException>(() => f.Call(f.EntitiesLoader, "Load", entities));
        Assert.False((bool)f.Property(f.Source, "Armed")!);
    }
    [Fact]
    public void LoadAfterFenceClosesRefusesWithoutReadingOrUpgradingWitness()
    {
        using var f = new F(); f.Load(); f.Call(f.Boundary, "PostLoad");
        var (_, entities, _, _) = f.RestoreStage(marker: true);
        f.Call(f.EntitiesLoader, "Load", entities);
        Assert.True((bool)f.Property(f.Source, "Tainted")!);
        Assert.False((bool)NativeShorelineWaterFixture.Get(f.Source, "_restorePermit")!);
    }

    [Fact]
    public void WrongThreadSaveRejectsWithoutPublishingPermanentTaint()
    {
        using var f = new F(); f.Load();
        var (_, entities, _, _) = f.RestoreStage(marker: true);
        f.Call(f.EntitiesLoader, "Load", entities);
        var copy = Activator.CreateInstance(f.T("Timberborn.WorldSerialization", "Timberborn.WorldSerialization.SerializedEntity"), f.Owner, "Fixture.Intake")!;
        var saver = Activator.CreateInstance(f.T("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.EntitySaver"), copy)!;
        Exception? failure = null;
        var thread = new Thread(() => { try { f.Call(f.Source, "Save", saver); } catch (Exception exception) { failure = exception; } });
        thread.Start(); thread.Join();
        Assert.IsType<TargetInvocationException>(failure);
        Assert.False((bool)f.Property(f.Source, "Tainted")!);
        var loader = Activator.CreateInstance(f.T("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.EntityLoader"), copy)!;
        Assert.False((bool)f.Call(loader, "HasComponent", f.Key("Wildfire.NaturalWaterOwnership"))!);
    }

    private sealed partial class F
    {
        internal readonly Guid Owner = Guid.NewGuid();
        internal object EntitiesLoader = null!;
        internal object Repository = null!;
        internal object Key(string name) => Activator.CreateInstance(T("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.ComponentKey"), name)!;
        internal object PropertyKey(Type type, string name) => Activator.CreateInstance(T("Timberborn.Persistence", "Timberborn.Persistence.PropertyKey`1").MakeGenericType(type), name)!;
        internal (object World, object Entities, object Loader, object Saved) RestoreStage(bool marker, float amount = .2f, bool nativeWater = true, bool tainted = false, string? omit = null, int version = 1, string? ownerText = null)
        {
            var entityType = T("Timberborn.EntitySystem", "Timberborn.EntitySystem.EntityComponent");
            var entity = RuntimeHelpers.GetUninitializedObject(entityType);
            NativeShorelineWaterFixture.Set(entity, "<EntityId>k__BackingField", Owner);
            NativeShorelineWaterFixture.Set(Source, "<Entity>k__BackingField", entity);
            var components = new List<object> { entity, Source, Water.Source };
            var system = N.LoadNative("Timberborn.BaseComponentSystem");
            var cache = RuntimeHelpers.GetUninitializedObject(system.GetType("Timberborn.BaseComponentSystem.ComponentCache")!);
            var map = Activator.CreateInstance(system.GetType("Timberborn.BaseComponentSystem.TypeIndexMap")!)!;
            var readOnly = Activator.CreateInstance(T("Timberborn.Common", "Timberborn.Common.ReadOnlyList`1").MakeGenericType(typeof(object)), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [components], null)!;
            foreach (var type in components.Select(c => c.GetType()).Append(T("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.IPersistentEntity")).Append(T("Timberborn.EntitySystem", "Timberborn.EntitySystem.IPostLoadableEntity")))
                map.GetType().GetMethod("CacheType")!.MakeGenericMethod(type).Invoke(map, [readOnly]);
            NativeShorelineWaterFixture.Set(cache, "_components", components); NativeShorelineWaterFixture.Set(cache, "_typeIndexMap", map);
            foreach (var component in components) system.GetType("Timberborn.BaseComponentSystem.BaseComponent")!.GetField("_componentCache", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(component, cache);
            Track();
            var saved = Activator.CreateInstance(T("Timberborn.WorldSerialization", "Timberborn.WorldSerialization.SerializedEntity"), Owner, "Fixture.Intake")!;
            var saver = Activator.CreateInstance(T("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.EntitySaver"), saved)!;
            if (nativeWater) { NativeShorelineWaterFixture.Set(Water.Source, "_cleanWaterAmount", amount); Call(Water.Source, "Save", saver); NativeShorelineWaterFixture.Set(Water.Source, "_cleanWaterAmount", 0f); }
            if (marker)
            {
                var component = Call(saver, "GetComponent", Key("Wildfire.NaturalWaterOwnership"))!;
                foreach (var (name, type, value) in new (string, Type, object)[] { ("Version", typeof(int), version), ("Tainted", typeof(bool), tainted), ("Owner", typeof(string), ownerText ?? Owner.ToString("D")), ("X", typeof(int), 4), ("Y", typeof(int), 5), ("Z", typeof(int), 0) })
                    if (name != omit) Call(component, "Set", PropertyKey(type, name), value);
            }
            var pairType = T("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.InstantiatedSerializedEntity");
            var entities = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(pairType))!;
            entities.Add(Activator.CreateInstance(pairType, entity, saved)!);
            var worldLoader = RuntimeHelpers.GetUninitializedObject(T("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.WorldEntitiesLoader"));
            EntitiesLoader = Activator.CreateInstance(T("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.EntitiesLoader"), Array.CreateInstance(T("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.IEntityBatchLoader"), 0))!;
            NativeShorelineWaterFixture.Set(worldLoader, "_entitiesLoader", EntitiesLoader);
            NativeShorelineWaterFixture.Set(worldLoader, "_instantiatedSerializedEntities", entities); // Explicit native load-stage membership, not Unity instantiation proof.
            _loaders.Add(worldLoader);
            var loader = Activator.CreateInstance(T("Timberborn.WorldPersistence", "Timberborn.WorldPersistence.EntityLoader"), saved)!;
            return (worldLoader, entities, loader, saved);
        }
    }
}
