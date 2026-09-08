using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeOwnedStorageTests
{
    [Theory]
    [InlineData("Stockpile")]
    [InlineData("SimpleOutput")]
    public void ExactNativeRegistryLookupSkipsMissingDeletedOrUninitializedOwner(string role)
    {
        using var native = new NativeManagedTestContext();
        var mod = native.LoadMod();
        var registryType = native.LoadNative("Timberborn.EntitySystem").GetType("Timberborn.EntitySystem.EntityRegistry")!;
        var entityType = native.LoadNative("Timberborn.EntitySystem").GetType("Timberborn.EntitySystem.EntityComponent")!;
        var registry = Activator.CreateInstance(registryType)!;
        var entries = (IDictionary)registryType.GetField("_entities", Flags)!.GetValue(registry)!;
        var api = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.Consequences.TimberbornOwnedStorageInventoryApi")!, registry)!;
        Guid owner = Guid.NewGuid(), foreign = Guid.NewGuid();
        var roleType = mod.GetType("Wildfire.Timberborn.Mapping.TimberbornNativeInventoryRole")!;
        var declarationType = mod.GetType("Wildfire.Timberborn.Mapping.TimberbornInventoryDeclaration")!;
        var declaration = Activator.CreateInstance(declarationType, Enum.Parse(roleType,role), role)!;
        var declarations = Array.CreateInstance(declarationType,1); declarations.SetValue(declaration,0);
        var familyType=mod.GetType("Wildfire.Timberborn.Mapping.NativeBurnTargetFamily") ?? mod.GetType("Wildfire.Timberborn.Consequences.NativeBurnTargetFamily")!;
        var registration = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.Consequences.TimberbornOwnedStorageRegistration")!, owner,
            Enum.Parse(familyType,role=="Stockpile"?"Stockpile":"Structure"),declarations)!;
        var requested = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.Consequences.TimberbornStoredGoodStack")!, "Log", 1)!;
        var foreignEntity = RuntimeHelpers.GetUninitializedObject(entityType);
        entries.Add(foreign, foreignEntity); // Another registered entity is never a fallback candidate.
        Check();
        var entity = RuntimeHelpers.GetUninitializedObject(entityType);
        entityType.GetField("<EntityId>k__BackingField", Flags)!.SetValue(entity, owner);
        entries.Add(owner, entity);
        Check(); // Not initialized: exits before engine liveness.
        var state = entityType.GetField("_entityState", Flags)!;
        state.SetValue(entity, Enum.Parse(state.FieldType, "Deleted"));
        Check();
        entries.Remove(owner);
        Check();
        Assert.Same(foreignEntity, entries[foreign]);
        void Check()
        {
            var read = NativeInjuryFixture.Call(api, "Read", registration)!;
            Assert.Equal("NotLive", NativeInjuryFixture.Get(read, "Status")!.ToString());
            Assert.Empty((IEnumerable)NativeInjuryFixture.Get(read, "Inventories")!);
            var removed = NativeInjuryFixture.Call(api, "Consume", registration, declaration, requested)!;
            Assert.Equal(0, NativeInjuryFixture.Get(removed, "RemovedAmount"));
        }
        // Positive native component ownership/liveness needs Unity; no engine object was fabricated.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualNativeConsumptionPreservesReservationsAndUsesConsumedAccounting(bool throwAfterMutation)
    {
        using var fixture = new NativeReservationFixture("Log");
        fixture.Reserve(capacity: false); // One physical Log, reserved by the real native reserver.
        fixture.Call(fixture.Inventory, "GiveExistingIgnoringCapacity", fixture.Amount); // One more, unreserved.
        var mod = fixture.Resources.GetType().Assembly;
        var api = mod.GetType("Wildfire.Timberborn.Consequences.TimberbornOwnedStorageInventoryApi")!;
        var request = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.Consequences.TimberbornStoredGoodStack")!, "Log", 99)!;
        string? accounting = null;
        var changed = fixture.Inventory.GetType().GetEvent("InventoryStockChanged")!;
        var argsType = changed.EventHandlerType!.GetMethod("Invoke")!.GetParameters()[1].ParameterType;
        var args = Expression.Parameter(argsType, "args");
        Action<object> observer = value =>
        {
            accounting = NativeInjuryFixture.Get(value, "StockChangeType")!.ToString();
            if (throwAfterMutation) throw new InvalidOperationException("inventory subscriber after stock decrement");
        };
        changed.AddEventHandler(fixture.Inventory, Expression.Lambda(changed.EventHandlerType,
            Expression.Invoke(Expression.Constant(observer), Expression.Convert(args, typeof(object))),
            Expression.Parameter(typeof(object), "sender"), args).Compile());
        int removed = -1;
        Action consume = () => removed = (int)api.GetMethod("ConsumeUnreserved", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new[] { fixture.Inventory, request })!;
        if (throwAfterMutation)
        {
            Assert.Throws<TargetInvocationException>(() => fixture.Transfer(consume));
            Assert.Equal(-1, removed); // No fabricated successful receipt after native failure.
            Assert.Equal(true, fixture.Get(fixture.Resources, "IsIndeterminate"));
            Assert.Throws<TargetInvocationException>(() => fixture.Transfer(consume));
        }
        else
        {
            fixture.Transfer(consume);
            Assert.Equal(1, removed);
            fixture.Transfer(consume);
            Assert.Equal(0, removed); // Reserved survivor never becomes a second removal.
            fixture.Call(fixture.Resources, "ThrowIfSaveUnsafe");
        }
        Assert.Equal("Consumed", accounting);
        Assert.Equal(1, fixture.Call(fixture.Inventory, "AmountInStock", "Log"));
        Assert.Equal(1, fixture.Reserved(stock: true));
        Assert.Same(fixture.Inventory, fixture.ReservedInventory(capacity: false));
    }
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
}
