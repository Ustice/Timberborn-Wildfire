using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Wildfire.Core;

namespace Wildfire.Timberborn.Tests;

public sealed class RetainedBodyCaptureTests
{
    [Fact]
    public void RetainedCaptureUsesCallerGuardWithoutNestingAndReturnsNoUnrequestedBodies()
    {
        var fixture = new Fixture();
        fixture.Add(Guid.NewGuid(), "Deleted"); // Unrequested unrelated entity is never enumerated as a fallback.
        Assert.Empty(fixture.Capture([]));
        fixture.Guard.GetType().GetMethod("ThrowIfSaveUnsafe")!.Invoke(fixture.Guard, null);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Deleted")]
    [InlineData("Uninitialized")]
    public void ExactMissingDeletedOrUninitializedSavedGuidRejectsBeforeGameObjectAccess(string? state)
    {
        var fixture = new Fixture();
        Guid required = Guid.NewGuid();
        if (state is not null) fixture.Add(required, state);
        fixture.Add(Guid.NewGuid(), "Deleted");
        var error = Assert.Throws<TargetInvocationException>(() => fixture.Capture([required]));
        Assert.Contains("Required saved native body", Root(error).Message);
        fixture.Guard.GetType().GetMethod("ThrowIfSaveUnsafe")!.Invoke(fixture.Guard, null); // Read failure is not mutation poison.
    }

    [Fact]
    public void InvalidSavedIdentitySetAndWrongEnvironmentDimensionsRejectWholeCapture()
    {
        var fixture = new Fixture();
        Guid id = Guid.NewGuid();
        Assert.IsType<ArgumentException>(Root(Assert.Throws<TargetInvocationException>(() => fixture.Capture([id, id]))));
        Assert.IsType<ArgumentException>(Root(Assert.Throws<TargetInvocationException>(() => fixture.Capture([Guid.Empty]))));
        Assert.IsType<InvalidOperationException>(Root(Assert.Throws<TargetInvocationException>(() => fixture.Capture([], new(2, 1, 6)))));
        Assert.IsType<ArgumentOutOfRangeException>(Root(Assert.Throws<TargetInvocationException>(() => fixture.Capture([], new(0, 1, 6)))));
    }

    private static Exception Root(Exception error) => error.InnerException is { } inner ? Root(inner) : error;
    private sealed class Fixture
    {
        private readonly object _provider, _registry;
        internal readonly object Guard;
        private readonly NativeManagedTestContext _native = NativeEnvironmentFixture.Context;
        internal Fixture()
        {
            var environment = new NativeEnvironmentFixture();
            var registryType = _native.LoadNative("Timberborn.EntitySystem").GetType("Timberborn.EntitySystem.EntityRegistry")!;
            _registry = Activator.CreateInstance(registryType)!;
            var mod = _native.LoadMod();
            Guard = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.Resources.NativeResourceCoordinator")!)!;
            _provider = Activator.CreateInstance(mod.GetType("Wildfire.Timberborn.Runtime.TimberbornInitialWorldProjectionProvider")!,
                _registry, environment.NativeProvider, Guard)!;
        }
        internal void Add(Guid id, string state)
        {
            var type = _native.LoadNative("Timberborn.EntitySystem").GetType("Timberborn.EntitySystem.EntityComponent")!;
            var entity = RuntimeHelpers.GetUninitializedObject(type);
            type.GetField("<EntityId>k__BackingField", Flags)!.SetValue(entity, id);
            var stateField = type.GetField("_entityState", Flags)!;
            if (state != "Uninitialized") stateField.SetValue(entity, Enum.Parse(stateField.FieldType, state));
            ((IDictionary)_registry.GetType().GetField("_entities", Flags)!.GetValue(_registry)!).Add(id, entity);
        }
        internal object[] Capture(Guid[] ids, FireGrid? grid = null)
        {
            var method = Guard.GetType().GetMethod("CaptureAtRest")!.MakeGenericMethod(typeof(object));
            Func<object> read = () => _provider.GetType().GetMethod("CaptureRetainedBodies")!.Invoke(_provider, [grid ?? new FireGrid(1, 1, 6), ids])!;
            return ((IEnumerable)method.Invoke(Guard, [read])!).Cast<object>().ToArray();
        }
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    }
}
