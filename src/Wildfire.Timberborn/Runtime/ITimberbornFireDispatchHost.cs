using Wildfire.Core;

namespace Wildfire.Timberborn.Runtime;

/// <summary>The existing native coordinator supplies both the step and its world-save failure boundary.</summary>
internal interface ITimberbornFireDispatchHost
{
    GpuFireStepResult Tick();
    void ThrowIfStepUnsafe();
    void InvalidateIncompleteDispatch();
}
