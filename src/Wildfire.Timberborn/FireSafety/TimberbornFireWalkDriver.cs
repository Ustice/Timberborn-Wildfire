using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.WalkingSystem;
using Wildfire.Timberborn.Compatibility;
using UnityEngine;

namespace Wildfire.Timberborn.FireSafety;

/// <summary>The engine-bound operations needed by the installed-path protocol.</summary>
internal interface ITimberbornFireWalkDriver
{
    event EventHandler<StartedNewPathEventArgs> PathStarted;
    long Revision { get; }
    bool Stopped { get; }
    bool IsInstalledPathSafe(bool escaping);
    ExecutorStatus Launch(Vector3 destination);
    ExecutorStatus Tick(float hours);
    void Refresh();
    void Pause();
    void Stop();
    void ReleasePause();
}

internal sealed class TimberbornFireWalkDriver : ITimberbornFireWalkDriver
{
    private readonly Walker _walker;
    private readonly WalkToPositionExecutor _walk;
    private readonly TimberbornOwnedWalker _movement;
    private readonly Transform _transform;
    private readonly FireSafetyField _field;

    internal TimberbornFireWalkDriver(BaseComponent owner, FireSafetyField field)
    {
        _walker = owner.GetComponent<Walker>();
        _walk = owner.GetComponent<WalkToPositionExecutor>();
        _movement = new TimberbornOwnedWalker(_walker, owner.GetComponent<WalkerMover>());
        _transform = owner.Transform;
        _field = field;
    }

    public event EventHandler<StartedNewPathEventArgs> PathStarted
    {
        add => _walker.StartedNewPath += value;
        remove => _walker.StartedNewPath -= value;
    }
    public long Revision => _field.Revision;
    public bool Stopped => _walker.Stopped();
    public bool IsInstalledPathSafe(bool escaping) =>
        _field.SafeInstalledPath(_transform.position, _walker.PathCorners, escaping);
    public ExecutorStatus Launch(Vector3 destination) => _walk.Launch(destination);
    public ExecutorStatus Tick(float hours) => _walk.Tick(hours);
    public void Refresh() => _walker.RefreshPath();
    public void Pause() => _movement.RejectRoute();
    public void Stop() => _movement.Stop();
    public void ReleasePause() => _movement.ReleasePause();
}
