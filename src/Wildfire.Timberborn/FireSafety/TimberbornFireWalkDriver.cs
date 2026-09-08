using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.WalkingSystem;
using Timberborn.Navigation;
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
    ExecutorStatus Launch(IDestination destination);
    IDestination? CurrentDestination { get; }
    Vector3? InstalledEndpoint { get; }
    bool IsCandidateSafe(IDestination destination);
    bool AtSafeEndpoint(Vector3 endpoint);
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
    public ExecutorStatus Launch(IDestination destination) => _walker.GoTo(destination);
    public IDestination? CurrentDestination => _movement.CurrentDestination;
    public Vector3? InstalledEndpoint
    {
        get
        {
            var path = _walker.PathCorners;
            if (path.Count == 0) return null;
            var end = path[path.Count - 1].Position;
            return float.IsFinite(end.x) && float.IsFinite(end.y) && float.IsFinite(end.z) ? end : null;
        }
    }
    public bool IsCandidateSafe(IDestination destination)
    {
        var path = new List<PathCorner>();
        var start = _transform.position;
        return destination.FindPath(start, path, out _) && path.Count > 0 &&
            _field.SafePosition(path[path.Count - 1].Position) && _field.SafeInstalledPath(start, path, escaping: false);
    }
    public bool AtSafeEndpoint(Vector3 endpoint) => _field.AtSafeEndpoint(_transform.position, endpoint);
    public ExecutorStatus Tick(float hours) => _walk.Tick(hours);
    public void Refresh() => _walker.RefreshPath();
    public void Pause() => _movement.RejectRoute();
    public void Stop() => _movement.Stop();
    public void ReleasePause() => _movement.ReleasePause();
}
