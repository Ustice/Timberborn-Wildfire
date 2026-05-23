using UnityEngine;

namespace Wildfire.Timberborn.Qa;

public sealed class UnityTimberbornQaCommandLogSink : ITimberbornQaCommandLogSink
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
