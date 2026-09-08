using System.Runtime.InteropServices;
using Wildfire.Core;
using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed class FireSimGpuProtocolTests
{
    [Fact]
    public void ProductionStructAndPortableUploadEncodeTheSameSmokeCommands()
    {
        FireSimChange[] changes =
        [
            new(0, SetSmoke: 5, SetSmokeContamination: 7),
            new(1, SetSmoke: 0, SetSmokeContamination: 0),
        ];

        FireSimGpuChange[] productionUpload = FireSimGpuProtocol.EncodeChanges(changes);
        uint[] actualWords = MemoryMarshal.Cast<FireSimGpuChange, uint>(productionUpload).ToArray();

        Assert.Equal(16, Marshal.SizeOf<FireSimGpuChange>());
        Assert.Equal(
            [0u, 0x600u, (5u << 17) | (7u << 20), 0u, 1u, 0x600u, 0u, 0u],
            actualWords);
        Assert.Equal(actualWords, FireSimChangeUpload.Encode(changes, capacity: 2));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(255, 3)]
    public void WaterAdditionUsesSpareBitsWithoutChangingOtherFields(byte addWater, uint expected)
    {
        FireSimChange change = new(
            0, SetCell: 0xA55A, AddHeat: 15, AddFuel: 15, AddAsh: 3, RemoveAsh: 3,
            SetAsh: 3, SetAshContamination: 7, SetSmoke: 7, SetSmokeContamination: 7,
            SetWater: 1, AddWater: addWater);

        FireSimGpuChange encoded = FireSimGpuProtocol.EncodeChange(change);

        Assert.Equal(0x7FFFFFu | (expected << 23), encoded.AddFields);
        Assert.Equal(0x783u, encoded.SetMask);
        Assert.Equal(0x1A55Au, encoded.SetValues);
        Assert.Equal(
            MemoryMarshal.Cast<FireSimGpuChange, uint>(new[] { encoded }).ToArray(),
            FireSimChangeUpload.Encode([change], capacity: 1));
    }

    [Fact]
    public void DeltaCapacityIncludesBothCommandAndSimulationTransitions()
    {
        Assert.Equal(2, FireSimGpuProtocol.GetDeltaCapacity(cellCount: 1, changeCapacity: 1));
        Assert.Equal(12, FireSimGpuProtocol.GetDeltaCapacity(cellCount: 8, changeCapacity: 4));
        Assert.Throws<OverflowException>(() => FireSimGpuProtocol.GetDeltaCapacity(int.MaxValue, 1));
    }

    [Fact]
    public void QueuePreservesRepeatedCellCommandsAndRetainsOverflowUntilConsumed()
    {
        FireSimChangeQueue queue = new();
        FireSimChange first = new(0, AddHeat: 1);
        FireSimChange second = new(0, SetHeat: 0);
        FireSimChange overflow = new(1, SetSmoke: 5);
        queue.Add(first);
        queue.Add(new FireSimChange(-1));
        queue.Add(second);
        queue.Add(overflow);
        queue.Add(new FireSimChange(2));

        FireSimChangeQueue.Batch batch = queue.PrepareBatch(cellCount: 2, uploadCapacity: 2);

        Assert.Equal([first, second], batch.Changes);
        Assert.Equal(2, batch.IgnoredCount);
        Assert.Equal(5, queue.Count);
        Assert.Equal(batch.Changes, queue.PrepareBatch(cellCount: 2, uploadCapacity: 2).Changes);

        queue.Consume(batch);

        Assert.Equal(1, queue.Count);
        Assert.Equal([overflow], queue.PrepareBatch(cellCount: 2, uploadCapacity: 2).Changes);
        Assert.Throws<InvalidOperationException>(() => queue.Consume(batch));
    }
}
