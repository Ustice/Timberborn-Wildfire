using System.Linq.Expressions;
using System.Reflection;
using Wildfire.Core;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeAshGrowthTickTests
{
    [Fact]
    public void NativeSingletonLoadPlacesAshAfterClockAndTriggersAndCallsOncePerTick()
    {
        var f = new NativeAshGrowthFixture(.005f, actualClock: true);
        var setup = new TickFixture(f);
        int reads = 0;
        setup.Read = () =>
        {
            reads++;
            Assert.Equal(reads / 100f, (float)f.Get(f.Clock, "PartialDayNumber")!, 5);
            Assert.Equal(1, f.Completed);
            return null; // No current ready fire world. Does not create a native write.
        };
        var tickType = f.T("Timberborn.TickSystem", "ITickableSingleton");
        var repository = NativePersistenceProxy.Create(f.T("Timberborn.SingletonSystem", "ISingletonRepository"), (m, _) =>
        {
            Type type = m.GetGenericArguments().Single();
            object[] values = type == tickType ? [setup.Ticker, f.Clock, f.Service] : [];
            var array = Array.CreateInstance(type, values.Length);
            for (int i = 0; i < values.Length; i++) array.SetValue(values[i], i);
            return array;
        });
        var mode = NativePersistenceProxy.Create(f.T("Timberborn.TickSystem", "ITickingMode"), (_, _) => true);
        var metrics = NativePersistenceProxy.Create(f.T("Timberborn.Metrics", "IMetricsService"), (m, _) => m.Name == "get_MetricsEnabled" ? false : null);
        var scheduler = Activator.CreateInstance(f.T("Timberborn.TickSystem", "TickableSingletonService"), repository, mode, metrics, null, null)!;
        f.Call(scheduler, "Load");
        Assert.Equal(0, reads);
        f.Call(scheduler, "TickSingletons");
        f.Call(scheduler, "TickSingletons");
        Assert.Equal(2, reads);
        f.Call(setup.Coordinator, "ThrowIfSaveUnsafe");
    }

    [Fact]
    public void PreparationExcludesSaveAndMutationAndInvalidationCannotPublishAPlan()
    {
        var setup = new TickFixture(new());
        var f = setup.F;
        setup.Read = () =>
        {
            Assert.Throws<TargetInvocationException>(() => f.Call(setup.Coordinator, "ThrowIfSaveUnsafe"));
            Assert.Throws<TargetInvocationException>(() => f.Call(setup.Coordinator, "TransferInventory", (Action)(() => throw new Exception("must not run"))));
            f.Call(setup.Coordinator, "InvalidateAfterLifecycleFailure");
            return setup.EmptyObservation();
        };
        Assert.Throws<TargetInvocationException>(() => f.Call(setup.Ticker, "Tick"));
        Assert.Equal(true, f.Get(setup.Coordinator, "IsIndeterminate"));
        Assert.Throws<TargetInvocationException>(() => f.Call(setup.Coordinator, "ThrowIfSaveUnsafe"));
        Assert.Equal(0, setup.Applied);
    }

    [Fact]
    public void ReadRejectionIsSaveSafeAndZeroIntervalNeverLooksUpOrWrites()
    {
        var setup = new TickFixture(new());
        var expected = new InvalidOperationException("not ready for capture");
        setup.Read = () => throw expected;
        var error = Assert.Throws<TargetInvocationException>(() => setup.F.Call(setup.Ticker, "Tick"));
        Assert.Same(expected, error.GetBaseException());
        setup.F.Call(setup.Coordinator, "ThrowIfSaveUnsafe");
        setup.F.TickHours = 0;
        setup.Read = setup.EmptyObservation;
        setup.F.Call(setup.Ticker, "Tick");
        Assert.Equal(0, setup.Lookups);
        Assert.Equal(0, setup.Applied);
        setup.F.Call(setup.Coordinator, "ThrowIfSaveUnsafe");
    }

    [Fact]
    public void PreparedEmptyIntervalIsSingleUseAndSuccessfulCompletionHasSeparateTelemetry()
    {
        var setup = new TickFixture(new());
        setup.Read = setup.EmptyObservation;
        setup.F.Call(setup.Ticker, "Tick");
        Assert.Equal(0, setup.Applied);
        var requests = setup.EmptyObservation().GetType().GetProperty("Requests", NativeAshGrowthFixture.Flags)!.GetValue(setup.EmptyObservation());
        var plan = setup.F.Call(setup.Adapter, "Prepare", new FireGrid(1, 1, 2), .01f, requests)!;
        setup.F.Call(setup.Coordinator, "TransferInventory", (Action)(() => setup.F.Call(plan, "Apply")));
        Assert.Throws<TargetInvocationException>(() => setup.F.Call(plan, "Apply"));
    }

    internal sealed class TickFixture
    {
        internal readonly NativeAshGrowthFixture F;
        internal readonly object Coordinator, Adapter, Ticker;
        internal Func<object?> Read = () => null;
        internal int Lookups;
        internal TickFixture(NativeAshGrowthFixture f)
        {
            F = f;
            Coordinator = Activator.CreateInstance(Mod("NativeResourceCoordinator", "Resources"))!;
            var blocks = NativePersistenceProxy.Create(f.T("Timberborn.BlockSystem", "IBlockService"), (m, _) =>
            {
                Lookups++;
                if (m.Name != "GetObjectsWithComponentAt") throw new NotSupportedException(m.Name);
                return Array.CreateInstance(m.GetGenericArguments().Single(), 0);
            });
            Adapter = Activator.CreateInstance(Mod("TimberbornGrowableAshGrowthAdapter"), NativeAshGrowthFixture.Flags, null, [blocks], null)!;
            var observation = Mod("TimberbornAshGrowthObservation");
            Func<object?> read = () => Read();
            var readDelegate = Expression.Lambda(typeof(Func<>).MakeGenericType(observation),
                Expression.Convert(Expression.Invoke(Expression.Constant(read)), observation)).Compile();
            Ticker = Activator.CreateInstance(Mod("TimberbornAshGrowthTick"), NativeAshGrowthFixture.Flags, null, [f.Clock, Coordinator, Adapter, readDelegate], null)!;
        }
        internal Type Mod(string name, string area = "Ash") => F.Native.LoadMod().GetType("Wildfire.Timberborn." + area + "." + name)!;
        internal object EmptyObservation() => Activator.CreateInstance(Mod("TimberbornAshGrowthObservation"), NativeAshGrowthFixture.Flags, null,
            [new FireGrid(1, 1, 2), Array.CreateInstance(Mod("TimberbornAshGrowthBonusRequest"), 0)], null)!;
        internal int Applied
        {
            get
            {
                var result = F.Get(Ticker, "LastApplication")!;
                return (int)result.GetType().GetProperty("AppliedGrowableCount")!.GetValue(result)!;
            }
        }
    }
}
