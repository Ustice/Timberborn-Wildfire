using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

// Real installed timers/Growable with supplied backing state. No Unity entity/model liveness claim.
internal sealed class NativeAshGrowthFixture
{
    internal readonly NativeManagedTestContext Native = NativeManagedTestContext.ProxyContracts;
    internal readonly object Clock, Service, Growable, Trigger, Living, Dying;
    internal float Day;
    internal float TickHours = .24f;
    internal int Completed;
    internal Action? Completion;
    internal NativeAshGrowthFixture(float duration = 2, bool actualClock = false)
    {
        Clock = NativePersistenceProxy.Create(T("Timberborn.TimeSystem", "IDayNightCycle"), (m, _) => m.Name switch
        {
            "get_PartialDayNumber" => Day,
            "get_FixedDeltaTimeInHours" => TickHours,
            _ => throw new NotSupportedException(m.Name)
        });
        if (actualClock)
        {
            Clock = New("Timberborn.TimeSystem", "DayNightCycle");
            var clockSpec = Activator.CreateInstance(T("Timberborn.TimeSystem", "DayNightCycleSpec"))!;
            clockSpec.GetType().GetProperty("ConfiguredDayLengthInTicks")!.SetValue(clockSpec, 100);
            Set(Clock, "_dayNightCycleSpec", clockSpec);
            Set(Clock, "_daytimeLengthInTicks", 50);
            Set(Clock, "<FixedDeltaTimeInHours>k__BackingField", .24f);
        }
        Service = Activator.CreateInstance(T("Timberborn.TimeSystem", "TimeTriggerService"), Clock)!;
        Growable = New("Timberborn.Growing", "Growable");
        Trigger = Activator.CreateInstance(T("Timberborn.TimeSystem", "TimeTrigger"), Clock, Service,
            (Action)(() => Call(Growable, "Grow")), duration)!;
        var spec = Activator.CreateInstance(T("Timberborn.Growing", "GrowableSpec"))!;
        spec.GetType().GetProperty("GrowthTimeInDays")!.SetValue(spec, duration);
        Set(Growable, "_growableSpec", spec); Set(Growable, "_timeTrigger", Trigger);
        var reproducible = New("Timberborn.NaturalResourcesReproduction", "Reproducible");
        // A remaining independent native blocker avoids invoking the unsupplied world reproducer.
        Set(reproducible, "_reproductionBlockers", new HashSet<object> { Growable, new object() });
        Set(Growable, "_reproducible", reproducible);
        Growable.GetType().GetEvent("HasGrown")!.AddEventHandler(Growable,
            (EventHandler)((_, _) => { Completed++; Completion?.Invoke(); }));
        Living = New("Timberborn.NaturalResourcesLifecycle", "LivingNaturalResource");
        Dying = New("Timberborn.NaturalResourcesLifecycle", "DyingNaturalResource");
        Set(Growable, "_livingNaturalResource", Living); Set(Growable, "_dyingNaturalResource", Dying);
        AttachCache(Growable, Living, Dying);
        Call(Trigger, "Resume");
    }
    internal Type T(string assembly, string name) => Native.LoadNative(assembly).GetType(assembly + "." + name)!;
    internal object New(string assembly, string name) => RuntimeHelpers.GetUninitializedObject(T(assembly, name));
    internal float Progress => (float)Get(Growable, "GrowthProgress")!;
    internal bool Advance(float elapsed, float multiplier = 1.1f)
    {
        var type = Native.LoadMod().GetType("Wildfire.Timberborn.Ash.TimberbornAshGrowthRate");
        Assert.NotNull(type);
        return (bool)type.GetMethod("Advance", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [Growable, elapsed, multiplier])!;
    }
    internal object Save()
    {
        var serialized = Activator.CreateInstance(T("Timberborn.WorldSerialization", "SerializedEntity"), Guid.NewGuid(), "Fixture.Plant")!;
        var saver = Activator.CreateInstance(T("Timberborn.WorldPersistence", "EntitySaver"), serialized)!;
        Call(Growable, "Save", saver);
        return serialized;
    }
    internal void Load(object serialized) => Call(Growable, "Load", Activator.CreateInstance(T("Timberborn.WorldPersistence", "EntityLoader"), serialized)!);
    internal object? Call(object owner, string name, params object?[] args) => owner.GetType().GetMethod(name, Flags)!.Invoke(owner, args);
    internal object? Get(object owner, string name) => owner.GetType().GetProperty(name, Flags)!.GetValue(owner);
    internal static void Set(object owner, string name, object? value) => owner.GetType().GetField(name, Flags)!.SetValue(owner, value);
    internal void AttachCache(params object[] components)
    {
        var cache = New("Timberborn.BaseComponentSystem", "ComponentCache");
        var map = Activator.CreateInstance(T("Timberborn.BaseComponentSystem", "TypeIndexMap"))!;
        var list = components.ToList();
        var readOnly = Activator.CreateInstance(T("Timberborn.Common", "ReadOnlyList`1").MakeGenericType(typeof(object)), Flags, null, [list], null)!;
        foreach (var type in components.Select(c => c.GetType()).Distinct())
            map.GetType().GetMethod("CacheType")!.MakeGenericMethod(type).Invoke(map, [readOnly]);
        Set(cache, "_components", list); Set(cache, "_typeIndexMap", map);
        foreach (var component in components)
            T("Timberborn.BaseComponentSystem", "BaseComponent").GetField("_componentCache", Flags)!.SetValue(component, cache);
    }
    internal const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
}
