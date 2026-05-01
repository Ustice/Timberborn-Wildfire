using Wildfire.Core;

namespace Wildfire.Unity;

public sealed class UnityComputeFireSimulator : IGpuFireSimulator
{
    public const string Status = "Planned";

    public UnityComputeFireSimulator(int width, int height, int depth)
    {
        if (width <= 0 || height <= 0 || depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Fire grid dimensions must be positive.");
        }

        Width = width;
        Height = height;
        Depth = depth;
    }

    public int Width { get; }

    public int Height { get; }

    public int Depth { get; }

    public static string Describe()
    {
        return "GPU compute simulator placeholder. Wildfire rules should execute in compute shaders.";
    }

    public void RegisterChange(FireSimChange change)
    {
        throw new NotImplementedException("GPU change upload is not implemented yet.");
    }

    public GpuFireStepResult Tick()
    {
        throw new NotImplementedException("GPU compute simulation is not implemented yet.");
    }

    public IDisposable Subscribe(IFireSimListener listener)
    {
        throw new NotImplementedException("GPU delta subscription is not implemented yet.");
    }
}
