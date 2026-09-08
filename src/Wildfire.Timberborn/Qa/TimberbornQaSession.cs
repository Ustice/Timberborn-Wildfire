using System.Globalization;
using System.Security.Cryptography;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.GameSaveRuntimeSystem;
using Timberborn.TickSystem;
using Timberborn.TimeSystem;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Qa;

public interface ITimberbornQaSession
{
    string QueueSave(Guid requestId, string name);
    string SaveStatus(Guid requestId);
    string ChangeSpeed(int speed);
}

// Driven only by the existing file bridge on the game thread. Never uses the native
// replaceable save queue, changes LoadedSave, or wraps native Save in CaptureAtRest.
public sealed class TimberbornQaSession : ITimberbornQaSession
{
    private readonly GameLoader _loader;
    private readonly GameSaveRepository _repository;
    private readonly GameSaver _saver;
    private readonly Ticker _ticker;
    private readonly IDayNightCycle _clock;
    private readonly SpeedManager _speed;
    private readonly TimberbornFireRuntime _runtime;
    private readonly NativeResourceCoordinator _resources;
    private TimberbornQaSaveCopy? _request;
    private bool _unloaded;

    public TimberbornQaSession(GameLoader loader, GameSaveRepository repository, GameSaver saver,
        Ticker ticker, IDayNightCycle clock, SpeedManager speed, TimberbornFireRuntime runtime,
        NativeResourceCoordinator resources)
    {
        _loader = loader; _repository = repository; _saver = saver; _ticker = ticker;
        _clock = clock; _speed = speed; _runtime = runtime; _resources = resources;
    }

    public string QueueSave(Guid requestId, string name)
    {
        if (requestId == Guid.Empty || !TimberbornQaSaveCopy.ValidName(name) || _repository.NameIsInvalid(name))
            throw new ArgumentException("A nonempty request GUID and QA- prefixed ASCII save name are required.");
        if (_request is { Pending: true }) throw new InvalidOperationException("A QA save is already pending.");
        if (_request?.Id == requestId) throw new InvalidOperationException("This QA save request was already used; query its status.");
        RequireReady();
        var loaded = _loader.LoadedSave ?? throw new InvalidOperationException("A loaded copied save is required.");
        if (_loader.IsNewGame) throw new InvalidOperationException("A loaded copied save is required.");
        var path = _repository.SaveNameToFileName(new SaveReference(name, loaded.SettlementReference));
        TimberbornQaSaveCopy.RequireDestination(path);
        _request = new TimberbornQaSaveCopy(requestId, loaded, path);
        return _request.Describe();
    }

    public string SaveStatus(Guid requestId) => _request is { } request && request.Id == requestId
        ? request.Describe() : $"request_id={requestId:D} save_state=unknown";

    public string ChangeSpeed(int speed)
    {
        if (speed is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(speed));
        RequireReady();
        _speed.ChangeSpeed(speed); // Native lock and late-update application remain authoritative.
        return $"requested_speed={speed} current_speed={_speed.CurrentSpeed.ToString(CultureInfo.InvariantCulture)}";
    }

    internal void Update()
    {
        if (_request is not { State: "queued" } request) return;
        request.State = "running";
        try
        {
            RequireCurrent(request);
            request.NativeDayBefore = _clock.PartialDayNumber;
            _ticker.FinishFullTick();
            RequireCurrent(request);
            request.NativeDayAtSave = _clock.PartialDayNumber;
            request.RuntimeTick = _runtime.GetState().TickCount;
            request.Publish(_saver.SaveWithoutFinishingTick);
            request.State = "completed";
        }
        catch (Exception exception)
        {
            request.Error = exception.ToString();
            request.State = "failed"; // Published remains explicit if only final measurement failed.
        }
    }

    internal void Unload()
    {
        _unloaded = true;
        if (_request is not { Pending: true } request) return;
        request.State = "failed";
        request.Error = "The game session unloaded before QA save completion.";
    }

    private void RequireCurrent(TimberbornQaSaveCopy request)
    {
        RequireReady();
        if (!request.Matches(_loader.LoadedSave) || _loader.IsNewGame)
            throw new InvalidOperationException("The loaded save changed before QA serialization.");
        TimberbornQaSaveCopy.RequireDestination(request.Path);
    }

    private void RequireReady()
    {
        if (_unloaded || _runtime.InitializationState != TimberbornRuntimeInitializationState.Ready)
            throw new InvalidOperationException("The current runtime is not ready for QA session control.");
        _resources.ThrowIfSaveUnsafe();
    }
}
