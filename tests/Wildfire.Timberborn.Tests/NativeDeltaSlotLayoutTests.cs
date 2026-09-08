using System.Reflection;
using System.Runtime.InteropServices;
using Wildfire.Core;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeDeltaSlotLayoutTests
{
    [Fact]
    public void NativeReadbackStructUsesFiveExplicitWordsWithSlotAtByteSixteen()
    {
        using var native = new NativeManagedTestContext();
        var type = native.LoadMod().GetType("Wildfire.Timberborn.Simulation.TimberbornComputeFireSimulator")!
            .GetNestedType("GpuCellDelta", BindingFlags.NonPublic)!;
        Assert.Equal(20, Marshal.SizeOf(type));
        Assert.Equal(FireSimGpuProtocol.DeltaStrideBytes, Marshal.SizeOf(type));
        Assert.Equal(12, Marshal.OffsetOf(type, "TargetId").ToInt32());
        Assert.Equal(16, Marshal.OffsetOf(type, "SlotId").ToInt32());
    }
}
