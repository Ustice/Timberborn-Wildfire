using Wildfire.Core;

namespace Wildfire.Timberborn.Runtime;

/// <summary>Queues a finite sequence of repeated inputs without overwriting unrelated queued changes.</summary>
public sealed class TimberbornSustainedIgnitionScheduler
{
    public const byte IgnitionHeat = 15;
    private readonly ITimberbornFireChangeQueue _changes;
    private readonly ITimberbornFireLogSink _logSink;
    private FireSimChange[] _qaIgnitionPegChanges = Array.Empty<FireSimChange>();
    private int _qaIgnitionPegDispatchTicksRemaining;
    private string _qaIgnitionPegSource = "placeholder";
    public int DurationTicks { get; internal set; } = 12;

    internal TimberbornSustainedIgnitionScheduler(ITimberbornFireChangeQueue changes, ITimberbornFireLogSink logSink)
    {
        _changes = changes;
        _logSink = logSink;
    }

    public int Queue(IEnumerable<FireSimChange> changes, string source)
    {
        FireSimChange[] ignitionChanges = (changes ?? throw new ArgumentNullException(nameof(changes))).ToArray();
        if (ignitionChanges.Length == 0)
        {
            return 0;
        }

        ignitionChanges
            .ToList()
            .ForEach(change => _changes.RegisterChange(change, source, shouldLog: false));
        Start(ignitionChanges, source);
        _changes.LogRegisteredChanges(source, ignitionChanges.Length);
        return DurationTicks;
    }

    internal void Start(FireSimChange[] changes, string source)
    {
        _qaIgnitionPegChanges = changes.ToArray();
        _qaIgnitionPegSource = source;
        int ignitionPegDispatchTicks = DurationTicks;
        _qaIgnitionPegDispatchTicksRemaining = Math.Max(0, ignitionPegDispatchTicks - 1);
        if (_qaIgnitionPegDispatchTicksRemaining > 0)
        {
            _logSink.Info(
                "wildfire_timberborn_qa_ignition_heat_peg_started " +
                $"source={source} " +
                $"cell_count={_qaIgnitionPegChanges.Length} " +
                $"requested_dispatch_ticks={ignitionPegDispatchTicks} " +
                $"fire_step_interval_ticks={(DurationTicks / 12)} " +
                $"remaining_dispatch_ticks={_qaIgnitionPegDispatchTicksRemaining}");
        }
    }

    internal string? BeforeTick()
    {
        if (_qaIgnitionPegDispatchTicksRemaining <= 0 || _qaIgnitionPegChanges.Length == 0)
        {
            return null;
        }

        if (_changes.RegisteredChangeCountSinceLastDispatch > 0)
        {
            return null;
        }

        _qaIgnitionPegChanges
            .ToList()
            .ForEach(change => _changes.RegisterChange(change, "qa_ignition_heat_peg", shouldLog: false));
        string source = _qaIgnitionPegSource;

        _qaIgnitionPegDispatchTicksRemaining--;
        _changes.LogRegisteredChanges("qa_ignition_heat_peg", _qaIgnitionPegChanges.Length);
        if (_qaIgnitionPegDispatchTicksRemaining == 0)
        {
            Reset();
        }

        return source;
    }

    internal void Reset()
    {
        _qaIgnitionPegChanges = Array.Empty<FireSimChange>();
        _qaIgnitionPegDispatchTicksRemaining = 0;
        _qaIgnitionPegSource = "placeholder";
    }

}
