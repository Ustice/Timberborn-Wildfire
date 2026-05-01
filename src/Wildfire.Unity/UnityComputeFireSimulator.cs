using Wildfire.Core;

namespace Wildfire.Unity;

public sealed class UnityComputeFireSimulator : IGpuFireSimulator
{
    public const string Status = "Buffer scaffold ready; rule dispatch is not implemented.";

    public UnityComputeFireSimulator(int width, int height, int depth)
    {
        Dimensions = new ComputeGridDimensions(width, height, depth);
    }

    public UnityComputeFireSimulator(ComputeBufferGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        BufferGrid = grid;
        Dimensions = grid.Dimensions;
    }

    public ComputeGridDimensions Dimensions { get; }

    public ComputeBufferGrid? BufferGrid { get; }

    public int Width => Dimensions.Width;

    public int Height => Dimensions.Height;

    public int Depth => Dimensions.Depth;

    public static string Describe()
    {
        return "Unity GPU simulator scaffold. Wildfire rules should execute in compute shaders.";
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
