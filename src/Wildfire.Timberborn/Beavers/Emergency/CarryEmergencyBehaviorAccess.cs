using System.Reflection;
using Timberborn.BehaviorSystem;
using Timberborn.Carrying;
using Timberborn.TickSystem;
using Timberborn.WalkingSystem;

namespace Wildfire.Timberborn.Beavers.Emergency;

/// <summary>Version-checked access to one reviewed native delivery-walk boundary.</summary>
public sealed class CarryEmergencyBehaviorAccess
{
    private readonly FieldInfo _behavior;
    private readonly FieldInfo _executor;
    private readonly FieldInfo _return;
    private readonly FieldInfo _elapsed;
    private CarryEmergencyBehaviorAccess()
    {
        _behavior = Field("_runningBehavior", typeof(Behavior));
        _executor = Field("_runningExecutor", typeof(IExecutor));
        _return = Field("_returnToBehavior", typeof(bool));
        _elapsed = Field("_runningExecutorElapsedTime", typeof(float));
    }
    public static CarryEmergencyBehaviorAccess CreateVerified()
    {
        CarryEmergencyBuild.VerifyDirectory(Path.GetDirectoryName(typeof(BehaviorManager).Assembly.Location)!);
        if (!typeof(ILateTickable).IsAssignableFrom(typeof(BehaviorManager)))
            throw new InvalidOperationException("BehaviorManager no longer has the reviewed late-tick boundary.");
        return new CarryEmergencyBehaviorAccess();
    }
    private static FieldInfo Field(string name, Type type)
    {
        var field = typeof(BehaviorManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null || field.FieldType != type || field.IsInitOnly)
            throw new InvalidOperationException($"Native carrying hook field changed: {name}.");
        return field;
    }
    public void VerifyExecutorIdentity(IEnumerable<IExecutor> executors, IExecutor expected)
    {
        var matches = executors.Where(executor => executor.GetName() == expected.GetName()).ToArray();
        if (matches.Length != 1 || !ReferenceEquals(matches[0], expected))
            throw new InvalidOperationException("Carrying emergency executor name is not unique on this entity.");
    }
    public bool IsNativeDelivery(BehaviorManager manager, CarryRootBehavior behavior, WalkToAccessibleExecutor walk) =>
        behavior.GetType() == typeof(CarryRootBehavior) && walk.GetType() == typeof(WalkToAccessibleExecutor) &&
        ReferenceEquals(_behavior.GetValue(manager), behavior) && ReferenceEquals(_executor.GetValue(manager), walk) &&
        (bool)_return.GetValue(manager)!;
    public bool Owns(BehaviorManager manager, IExecutor executor) => ReferenceEquals(_executor.GetValue(manager), executor);
    public void Replace(BehaviorManager manager, IExecutor expected, IExecutor? next, bool returnToCarry)
    {
        if (!Owns(manager, expected)) throw new InvalidOperationException("Another behavior owns this carrying walk.");
        _executor.SetValue(manager, next);
        _elapsed.SetValue(manager, 0f);
        _return.SetValue(manager, returnToCarry);
    }
}
