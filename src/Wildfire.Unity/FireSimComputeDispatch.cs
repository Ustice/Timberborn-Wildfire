using Wildfire.Core;

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
    IComputeBufferHandle QueuedChanges,
    IAppendComputeBufferHandle Deltas,
    IComputeBufferHandle VisualFields,
    IComputeBufferHandle CurrentTransportFields,
    IComputeBufferHandle NextTransportFields,
    IComputeBufferHandle MaterialFields,
    FireSimParameters Parameters,
    FireSimWind Wind,
    uint ChangeCount,
    int ThreadGroupsX,
    int ThreadGroupsY,
    int ThreadGroupsZ)
{
    public IComputeBufferHandle CurrentAtmosphericFields => CurrentTransportFields;

    public IComputeBufferHandle NextAtmosphericFields => NextTransportFields;

    public IComputeBufferHandle CompanionFields => MaterialFields;
}
