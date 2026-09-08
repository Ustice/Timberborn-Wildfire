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
    private IDestination? _destination;
    private Vector3? _endpoint;
    private bool _unverifiedPath;
    private bool _callingDestination;
    private int _startsInCall;

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
        _destination = null;
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

    internal bool Launch(IDestination destination)
    {
        if (_callingDestination)
        {
            _rejected = true;
            return false;
        }
        _destination = destination;
        _endpoint = null;
        _rejected = false;
        if (!_driver.IsCandidateSafe(destination)) return false;
        if (CallDestination(() => _driver.Launch(destination))) return true;
        _driver.Stop(); // The native call returned before this stop.
        return false;
    }

    private bool CallDestination(Func<ExecutorStatus> call)
    {
        _callingDestination = true;
        _startsInCall = 0;
        ExecutorStatus status;
        try { status = call(); }
        finally { _callingDestination = false; }
        if (status == ExecutorStatus.Failure || _startsInCall != 1 ||
            !VerifyDestination(allowImmediateArrival: status == ExecutorStatus.Success)) return false;
        _driver.ReleasePause();
        return VerifyDestination(); // Enable callbacks cannot publish a replacement route as ours.
    }

    private bool VerifyDestination(bool allowImmediateArrival = false)
    {
        if (_destination is null || _rejected || !_endpoint.HasValue) return false;
        if (_driver.InstalledEndpoint is not { } end || !end.Equals(_endpoint.Value)) return false;
        // FindPath publishes this reference only after PathStarted subscribers return.
        var current = _driver.CurrentDestination;
        if (current is not null)
        {
            if (!ReferenceEquals(current, _destination) ||
                (_unverifiedPath && !_driver.IsInstalledPathSafe(escaping: false))) return false;
        }
        // Natural arrival clears the native pointer; only an already verified endpoint can survive that.
        else if ((_unverifiedPath && !allowImmediateArrival) ||
            _driver.Tick(0) != ExecutorStatus.Success || !_driver.AtSafeEndpoint(end)) return false;
        _unverifiedPath = false;
        return true;
    }

    internal bool HasArrivedAt(IDestination expected) => ReferenceEquals(_destination, expected) &&
        VerifyDestination() && _driver.Stopped && _driver.Tick(0) == ExecutorStatus.Success &&
        _driver.AtSafeEndpoint(_endpoint!.Value);

    internal bool RefreshIfNeeded()
    {
        if (_destination is not null)
        {
            bool pending = _unverifiedPath;
            if (!VerifyDestination()) return false;
            if (pending)
            {
                _driver.ReleasePause();
                if (!VerifyDestination()) return false;
            }
            if (_revision != _driver.Revision && !_driver.Stopped)
                return CallDestination(() => { _driver.Refresh(); return _driver.Tick(0); });
            return true;
        }
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
        if (_destination is not null)
        {
            // Unsolicited paths remain paused/unverified until their post-return identity is checked.
            bool repeated = _callingDestination ? ++_startsInCall > 1 : _unverifiedPath;
            _unverifiedPath = true;
            _endpoint = _driver.InstalledEndpoint;
            if (mode == FireWalkMode.Ignore)
            {
                _rejected = true;
                return;
            }
            _revision = _driver.Revision;
            _rejected = repeated || !_endpoint.HasValue || !_driver.IsInstalledPathSafe(mode == FireWalkMode.Escape);
            if (_rejected || !_callingDestination) _driver.Pause();
            return;
        }
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
