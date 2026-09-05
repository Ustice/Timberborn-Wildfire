using Wildfire.Core;

namespace Wildfire.Unity;

public static class FireSimChangeUpload
{
    public const int UInt32WordsPerChange = FireSimGpuProtocol.UInt32WordsPerChange;

    public static uint[] Encode(ReadOnlySpan<FireSimChange> changes, int capacity)
    {
        return FireSimGpuProtocol.EncodeWords(changes, capacity);
    }
}
