namespace Wildfire.Timberborn.Ash;

public sealed class TimberbornAshFieldSynchronizer
{
    private readonly TimberbornAshFieldService _ashFieldService;
    private uint? _lastSyncTick;

    public TimberbornAshFieldSynchronizer(TimberbornAshFieldService ashFieldService)
    {
        _ashFieldService = ashFieldService ?? throw new ArgumentNullException(nameof(ashFieldService));
    }

    public void Sync(TimberbornFireSystem? fireSystem, uint tick, int dayNumber)
    {
        if (_lastSyncTick == tick)
        {
            return;
        }

        if (fireSystem?.ReadTransportFields() is not { Count: > 0 } transportFields)
        {
            return;
        }

        _ashFieldService.SyncFromTransportFields(tick, transportFields, dayNumber);
        _lastSyncTick = tick;
    }

    public void Clear()
    {
        _lastSyncTick = null;
    }
}
