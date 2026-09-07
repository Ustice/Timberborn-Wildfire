using System.Reflection;
using Timberborn.BehaviorSystem;
using Timberborn.Carrying;
using Timberborn.CharacterMovementSystem;
using Timberborn.Navigation;
using Timberborn.WorldPersistence;
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
    private readonly FieldInfo _path;
    private readonly FieldInfo _nextCorner;
    private CarryEmergencyBehaviorAccess()
    {
        _behavior = Field("_runningBehavior", typeof(Behavior));
        _executor = Field("_runningExecutor", typeof(IExecutor));
        _return = Field("_returnToBehavior", typeof(bool));
        _elapsed = Field("_runningExecutorElapsedTime", typeof(float));
        _path = RequireField(typeof(PathFollower), "_pathCorners", typeof(IReadOnlyList<PathCorner>));
        _nextCorner = RequireField(typeof(PathFollower), "_nextCornerIndex", typeof(int));
        RequireMethod(typeof(BehaviorManager), "Tick", typeof(void));
        RequireMethod(typeof(BehaviorManager), "ProcessBehaviors", typeof(void));
        RequireMethod(typeof(BehaviorManager), "Save", typeof(void), typeof(IEntitySaver));
        RequireMethod(typeof(BehaviorManager), "Load", typeof(void), typeof(IEntityLoader));
        RequireMethod(typeof(WalkToAccessibleExecutor), "Launch", typeof(ExecutorStatus), typeof(Accessible));
        RequireMethod(typeof(WalkToAccessibleExecutor), "Tick", typeof(ExecutorStatus), typeof(float));
        RequireMethod(typeof(Walker), "GoTo", typeof(ExecutorStatus), typeof(IDestination));
        RequireMethod(typeof(Walker), "Stopped", typeof(bool));
    }
    public static CarryEmergencyBehaviorAccess CreateVerified()
    {
        CarryEmergencyBuild.VerifyDirectory(Path.GetDirectoryName(typeof(BehaviorManager).Assembly.Location)!);
        if (!typeof(ILateTickable).IsAssignableFrom(typeof(BehaviorManager)))
            throw new InvalidOperationException("BehaviorManager no longer has the reviewed late-tick boundary.");
        return new CarryEmergencyBehaviorAccess();
    }
    private static FieldInfo Field(string name, Type type)
        => RequireField(typeof(BehaviorManager), name, type);
    private static FieldInfo RequireField(Type owner, string name, Type type)
    {
        var field = owner.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field is null || field.FieldType != type || field.IsInitOnly)
            throw new InvalidOperationException($"Native carrying hook field changed: {name}.");
        return field;
    }
    private static MethodInfo RequireMethod(Type owner, string name, Type result, params Type[] parameters)
    {
        var method = owner.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, types: parameters, modifiers: null);
        if (method is null || method.ReturnType != result)
            throw new InvalidOperationException($"Native carrying hook method changed: {owner.Name}.{name}.");
        return method;
    }
    public IReadOnlyList<PathCorner>? ReadRemainingPath(PathFollower follower)
    {
        var path = (IReadOnlyList<PathCorner>?)_path.GetValue(follower);
        if (path is null) return null; // Stopped PathFollower, including pending Walker.StopNextTick.
        int next = (int)_nextCorner.GetValue(follower)!;
        if (next < 1 || next > path.Count)
            throw new InvalidOperationException("Native carrying path progress is outside the reviewed range.");
        return path.Skip(next).ToArray(); // Caller prepends actual current transform, not a traversed corner.
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
    public bool Owns(BehaviorManager manager, CarryRootBehavior behavior, IExecutor executor) =>
        ReferenceEquals(_executor.GetValue(manager), executor) && ReferenceEquals(_behavior.GetValue(manager), behavior);
    public void Replace(BehaviorManager manager, CarryRootBehavior behavior, IExecutor expected, IExecutor? next, bool returnToCarry)
    {
        if (!Owns(manager, behavior, expected)) throw new InvalidOperationException("Another behavior owns this carrying walk.");
        _executor.SetValue(manager, next);
        _elapsed.SetValue(manager, 0f);
        _return.SetValue(manager, returnToCarry);
    }
}
