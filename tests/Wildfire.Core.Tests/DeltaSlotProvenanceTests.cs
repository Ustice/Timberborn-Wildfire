using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed class DeltaSlotProvenanceTests
{
    [Fact]
    public void FiveWordReadbackPreservesDistinctLocalSlotsAndActualEmissionOrder()
    {
        Assert.Equal(20, FireSimGpuProtocol.DeltaStrideBytes);
        var deltas = FireSimDeltaReadback.Decode([7, 8, 2, 70, 701, 7, 3, 0, 70, 702], 2);
        Assert.Equal([new CellDelta(7, 8, 2, 70, 701), new CellDelta(7, 3, 0, 70, 702)], deltas);
        Assert.NotEqual(deltas[0].NewCell, deltas[1].OldCell); // Administrative handoff changes the material between events.
        Assert.Throws<ArgumentException>(() => FireSimDeltaReadback.Decode([7, 8, 2, 70], 1));
        Assert.Throws<ArgumentException>(() => FireSimDeltaReadback.Decode([7, 8, 2, 70, 701, 7, 3, 0, 70], 2));
        Assert.Equal(0u, new CellDelta(7, 8, 2, 70).SlotId); // Old source constructors explicitly lack slot provenance.
    }
}
