using Wildfire.Core;

namespace Wildfire.Unity;

public sealed class UnityComputeFireSimulator
{
    public const string Status = "Planned";

    public static string Describe()
    {
        return "GPU compute backend placeholder. Phase 4 will translate Wildfire.Core packed-cell rules to HLSL and compare snapshots against CPU output.";
    }
}
