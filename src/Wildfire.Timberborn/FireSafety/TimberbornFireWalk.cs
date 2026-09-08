using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.WalkingSystem;
using UnityEngine;

namespace Wildfire.Timberborn.FireSafety;

internal enum FireWalkMode { Ignore, Outbound, Escape }

/// <summary>Installed native path monitoring for ordinary owned jobs; job state and arrival stay with the caller.</summary>
internal sealed class TimberbornFireWalk : IDisposable
{
    private readonly ITimberbornFireWalkDriver _driver;
    private readonly Func<FireWalkMode> _currentMode;
    private long _revision = -1;
    private bool _rejected;
    private bool _disposed;

    internal static TimberbornFireWalk Create(BaseComponent owner, FireSafetyField field, Func<FireWalkMode> currentMode) =>
        new(new TimberbornFireWalkDriver(owner, field), currentMode);

    internal TimberbornFireWalk(ITimberbornFireWalkDriver driver, Func<FireWalkMode> currentMode)
    {
        _driver = driver;
        _currentMode = currentMode;
        _driver.PathStarted += OnPathStarted;
    }

    // Caller has already checked the candidate route and assigned its phase and saved destination.
    internal bool Launch(Vector3 destination)
    {
        _rejected = false;
        var status = _driver.Launch(destination);
        if (status == ExecutorStatus.Failure || _rejected)
        {
            // FindPath must return before stopping: it assigns its destination after PathStarted.
            _driver.Stop();
            return false;
        }
        _driver.ReleasePause();
        return true;
    }

    internal bool RefreshIfNeeded()
    {
        if (_revision != _driver.Revision && !_driver.Stopped) _driver.Refresh();
        // The caller keeps its existing phase/resource ordering before stopping an unsafe refresh.
        return !_rejected;
    }

    internal ExecutorStatus Tick(float hours) => _driver.Tick(hours);
    internal void Stop() => _driver.Stop();
    internal void RejectRoute() => _driver.Pause();
    internal void ReleasePause() => _driver.ReleasePause();

    private void OnPathStarted(object sender, StartedNewPathEventArgs args)
    {
        var mode = _currentMode();
        if (mode == FireWalkMode.Ignore) return;
        _revision = _driver.Revision;
        _rejected = !_driver.IsInstalledPathSafe(mode == FireWalkMode.Escape);
        if (_rejected) _driver.Pause();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _driver.PathStarted -= OnPathStarted;
    }
}
