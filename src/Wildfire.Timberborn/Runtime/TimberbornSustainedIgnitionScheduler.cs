using Wildfire.Core;

namespace Wildfire.Timberborn.Runtime;

/// <summary>Queues a finite sequence of repeated inputs without overwriting unrelated queued changes.</summary>
public sealed class TimberbornSustainedIgnitionScheduler
{
    public const byte IgnitionHeat = 15;
    private readonly ITimberbornFireChangeQueue _changes;
    private readonly ITimberbornFireLogSink _logSink;
    private FireSimChange[] _changesToRepeat = Array.Empty<FireSimChange>();
    private int _remainingDispatchTicks;
    private string _source = "placeholder";
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
        _changesToRepeat = changes.ToArray();
        _source = source;
        int ignitionPegDispatchTicks = DurationTicks;
        _remainingDispatchTicks = Math.Max(0, ignitionPegDispatchTicks - 1);
        if (_remainingDispatchTicks > 0)
        {
            _logSink.Info(
                "wildfire_timberborn_qa_ignition_heat_peg_started " +
                $"source={source} " +
                $"cell_count={_changesToRepeat.Length} " +
                $"requested_dispatch_ticks={ignitionPegDispatchTicks} " +
                $"remaining_dispatch_ticks={_remainingDispatchTicks}");
        }
    }

    internal string? BeforeTick()
    {
        if (_remainingDispatchTicks <= 0 || _changesToRepeat.Length == 0)
        {
            return null;
        }

        if (_changes.RegisteredChangeCountSinceLastDispatch > 0)
        {
            return null;
        }

        _changesToRepeat
            .ToList()
            .ForEach(change => _changes.RegisterChange(change, "qa_ignition_heat_peg", shouldLog: false));
        string source = _source;

        _remainingDispatchTicks--;
        _changes.LogRegisteredChanges("qa_ignition_heat_peg", _changesToRepeat.Length);
        if (_remainingDispatchTicks == 0)
        {
            Reset();
        }

        return source;
    }

    internal void Reset()
    {
        _changesToRepeat = Array.Empty<FireSimChange>();
        _remainingDispatchTicks = 0;
        _source = "placeholder";
    }

}
