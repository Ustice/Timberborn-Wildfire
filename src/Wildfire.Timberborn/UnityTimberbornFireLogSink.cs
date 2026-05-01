using UnityEngine;

namespace Wildfire.Timberborn;

public sealed class UnityTimberbornFireLogSink : ITimberbornFireLogSink
{
    public void Info(string message)
    {
        Debug.Log(message);
    }

    public void Warning(string message)
    {
        Debug.LogWarning(message);
    }
}
