namespace Wildfire.Unity;

public interface IFireSimComputeDispatcher
{
    void Dispatch(FireSimComputeDispatch dispatch);
}

public readonly record struct FireSimComputeDispatch(
    string KernelName,
    ComputeGridDimensions Dimensions,
    uint Tick,
    uint Seed,
    IComputeBufferHandle CurrentCells,
    IComputeBufferHandle NextCells,
    IComputeBufferHandle Deltas,
    int ThreadGroupsX,
    int ThreadGroupsY,
    int ThreadGroupsZ);
